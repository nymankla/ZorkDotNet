namespace ZorkDotNet.Game;

/// <summary>
/// Command parser. Words distinguished by first five letters (madadv.help). Ported from np.92/np.93 SPARSE/SPAROUT.
/// </summary>
public static class Parser
{
    private static readonly Dictionary<string, string> DirectionSynonyms = new(StringComparer.OrdinalIgnoreCase)
    {
        ["N"] = "NORTH", ["S"] = "SOUTH", ["E"] = "EAST", ["W"] = "WEST",
        ["U"] = "UP", ["D"] = "DOWN", ["IN"] = "ENTER", ["OUT"] = "EXIT", ["LEAVE"] = "EXIT"
    };

    /// <summary>PREPVEC synonym: WITH, USING, THROU (dung.56 SYNONYM "WITH" "USING" "THROU").</summary>
    private static bool IsPrepWith(string normWord) =>
        normWord == "WITH" || normWord == "USING" || normWord == "THROU";

    /// <summary>PREPVEC synonym: IN, INSID, INTO (dung.56 SYNONYM "IN" "INSID" "INTO").</summary>
    private static bool IsPrepIn(string normWord) =>
        normWord == "IN" || normWord == "INSID" || normWord == "INTO";

    /// <summary>GWIM-style: ask "Which X?" and list options when multiple match.</summary>
    private static void WriteWhichPrompt(GameState state, string ambiguousWord)
    {
        var cand = state.PendingAmbiguousCandidates;
        state.PendingAmbiguousCandidates = null;
        if (cand != null && cand.Count > 1)
        {
            var opts = string.Join(", ", cand.Take(cand.Count - 1).Select(o => "the " + o.DescriptionShort))
                + (cand.Count > 2 ? ", or " : " or ") + "the " + cand[cand.Count - 1].DescriptionShort;
            state.Output.WriteLine("Which " + ambiguousWord + " (" + opts + ")?");
        }
        else
            state.Output.WriteLine("Which " + ambiguousWord + "?");
    }

    /// <summary>First five letters (MDL: "all words are distinguished by their first five letters").</summary>
    public static string NormWord(string word)
    {
        if (string.IsNullOrWhiteSpace(word)) return "";
        var w = word.Trim().ToUpperInvariant();
        return w.Length > 5 ? w[..5] : w;
    }

    /// <summary>
    /// One turn of rooms.99 RDCOM for a line the player typed: count the move, run the command, then the clocks
    /// and demons. An answer to QUIT's "Do you wish to leave the game?" is read by FINISH itself, so it is
    /// neither a move nor a turn.
    /// </summary>
    public static void PlayTurn(GameState state, string input)
    {
        if (state.AwaitingQuitConfirmation)
        {
            Scoring.AnswerQuit(state, input.Trim());
            return;
        }
        if (state.AwaitingPatchAnswer)
        {
            // JIGS-UP reads the answer inside the turn the player died in; the rest of that turn follows.
            if (Death.AnswerPatch(state, input.Trim()) && state.ClocksAfterPatch) state.ProcessClocks();
            state.ClocksAfterPatch = false;
            return;
        }
        state.Winner.Moves++;
        var output = state.Output;
        var tell = new TellWriter(output);
        state.Output = tell;
        try
        {
            Execute(state, input);
        }
        finally
        {
            state.Output = output;
        }
        // rooms.99 RDCOM: a command that told nothing gets "Nothing happens."
        if (!tell.Told) output.WriteLine("Nothing happens.");
        if (state.AwaitingPatchAnswer)
        {
            state.ClocksAfterPatch = true;
            return;
        }
        if (state.Running) state.ProcessClocks();
    }

    /// <summary>dung.56 ADD-BUZZ: words the parser skips ("WHERE IS THE GRUE", "GO NORTH").</summary>
    private static readonly HashSet<string> BuzzWords = new(StringComparer.Ordinal)
        { "BY", "IS", "ONE", "IT", "A", "THE", "AN", "THIS", "OVER", "RUN", "GO", "PROCE" };

    public static void Execute(GameState state, string input)
    {
        var words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !BuzzWords.Contains(NormWord(w))).ToArray();
        if (words.Length == 0)
        {
            state.Output.WriteLine("Huh?");
            return;
        }

        // Orphan handling: after "Which X?" next input can be just the noun to disambiguate
        if (state.PendingVerb != null && state.PendingWords != null)
        {
            var obj = ResolveObject(state, words, 0, out var amb);
            if (amb != null)
            {
                WriteWhichPrompt(state, amb);
                return;
            }
            if (obj != null)
            {
                var verb = state.PendingVerb!;
                state.PendingVerb = null;
                state.PendingWords = null;
                state.PendingNeedsDirect = state.PendingNeedsIndirect = false;
                state.PendingAmbiguousWord = null;
                var orphanSpec = VerbTable.GetByActionName(verb);
                if (orphanSpec != null)
                    SynMatch(state, orphanSpec, obj, null, null, words);
                else
                    DispatchOrphanVerb(state, verb, obj);
                return;
            }
            state.PendingVerb = null;
            state.PendingWords = null;
            state.PendingNeedsIndirect = state.PendingNeedsDirect = false;
            state.PendingAmbiguousWord = null;
        }

        var first = NormWord(words[0]);

        // Direction only -> walk (np.92 SPARSE: direction before verb)
        var dir = ResolveDirection(first);
        if (dir != null)
        {
            TryGo(state, dir);
            return;
        }

        // Table-driven verb (VSPEC/SYNTAX/VARG from np.92/np.93)
        if (!VerbTable.Table.TryGetValue(first, out var spec))
        {
            state.Output.WriteLine("I don't know the word \"" + words[0] + "\".");
            return;
        }

        // WALK/GO + direction: "GO NORTH" etc.
        if (spec.ActionName == "WALK" && words.Length >= 2)
        {
            dir = ResolveDirection(NormWord(words[1]));
            if (dir != null) { TryGo(state, dir); return; }
        }

        // Sparse: build PRSVEC (o1, prep2, o2) from remaining words
        Sparse(state, words, spec, out var prep1, out var o1, out var prep2, out var o2, out var ambiguousWord);
        if (ambiguousWord != null)
        {
            SetPending(state, spec.ActionName, words);
            WriteWhichPrompt(state, ambiguousWord);
            return;
        }

        // SYN-MATCH: find syntax, optionally GWIM, then dispatch
        SynMatch(state, spec, o1, prep2, o2, words, prep1);
    }

    /// <summary>SPARSE: parse words after verb into direct object and optional (prep, indirect object).</summary>
    private static void Sparse(GameState state, string[] words, VerbSpec spec,
        out string? prep1, out GameObject? o1, out string? prep2, out GameObject? o2, out string? ambiguousWord)
    {
        prep1 = null;
        o1 = null;
        prep2 = null;
        o2 = null;
        ambiguousWord = null;
        var start = 1;
        if (start >= words.Length) return;

        // A preposition right after the verb belongs to the first slot when a syntax wants one there ("TURN ON LAMP").
        var leading = Preposition.Normalize(NormWord(words[start]));
        if (leading != null && spec.Syntaxes.Any(s => s.Syn1.Prep == leading))
        {
            prep1 = leading;
            start++;
            if (start >= words.Length) return;
        }

        // Prepositions that this verb's slot2 can use (PREPVEC-style)
        var slot2Preps = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var syn in spec.Syntaxes)
            if (syn.Syn2.Prep != null) slot2Preps.Add(syn.Syn2.Prep);

        // Find first word that is a slot2 preposition
        int prepIndex = -1;
        string? foundPrep = null;
        for (var i = start; i < words.Length; i++)
        {
            var p = Preposition.Normalize(NormWord(words[i]));
            if (p != null && slot2Preps.Contains(p)) { prepIndex = i; foundPrep = p; break; }
        }

        string[] phrase1Words = prepIndex < 0 ? words.Skip(start).ToArray() : words.Skip(start).Take(prepIndex - start).ToArray();
        string[] phrase2Words = prepIndex < 0 ? Array.Empty<string>() : words.Skip(prepIndex + 1).ToArray();
        if (foundPrep != null) prep2 = foundPrep;

        o1 = phrase1Words.Length > 0 ? ResolveObject(state, phrase1Words, 0, out ambiguousWord) : null;
        if (ambiguousWord != null) return;
        o2 = phrase2Words.Length > 0 ? ResolveObject(state, phrase2Words, 0, out ambiguousWord) : null;
    }

    /// <summary>SYN-EQUAL: does (obj, prep) match this slot's VARG?</summary>
    private static bool SynEqual(VArg varg, GameObject? obj, string? prep)
    {
        if (varg.VWord == VArgFlags.None && varg.Prep == null)
            return obj == null && prep == null;
        if (obj == null)
            return false;
        if (varg.Prep != null)
            return string.Equals(varg.Prep, prep, StringComparison.OrdinalIgnoreCase);
        return prep == null;
    }

    /// <summary>SYN-MATCH: try syntaxes, GWIM if missing object, then dispatch (np.93).</summary>
    private static void SynMatch(GameState state, VerbSpec spec, GameObject? o1, string? prep2, GameObject? o2, string[] words, string? prep1 = null)
    {
        state.LastVerb = spec.ActionName;
        Syntax? driver = null;
        Syntax? match = null;
        foreach (var syn in spec.Syntaxes)
        {
            if (SynEqual(syn.Syn1, o1, prep1) && SynEqual(syn.Syn2, o2, prep2))
            { match = syn; break; }
            if ((o1 == null || o2 == null) && prep1 == null && syn.IsDriver) driver = syn;
        }
        var chosen = match ?? driver;
        if (chosen == null)
        {
            state.Output.WriteLine("I can't make sense out of that.");
            return;
        }
        // FLIP: swap o1/o2 (MDL PUT .OBJS 1 .O2> <PUT .OBJS 2 .O1>)
        var dobj = chosen.IsFlip ? o2 : o1;
        var iobj = chosen.IsFlip ? o1 : o2;
        // TAKE-IT-OR-LEAVE-IT (np.93): if direct object has VRBIT and is in room/takeable, implicit TAKE so command can proceed.
        // Not for TAKE itself: Dispatch performs that take, and taking twice printed "Taken." then "You already have that."
        if (chosen.Action != "TAKE" && chosen.Syn1.WantsRoom && dobj != null && dobj.InRoom != null && dobj.CanTake && !chosen.Syn1.NoTake)
            DoTake(state, dobj);
        // Dispatch
        state.LastDirectObject = dobj;
        state.LastIndirectObject = iobj;
        Dispatch(state, chosen.Action, dobj, iobj, words);
        // rooms.99 RDCOM: after the verb, the room's own action gets its turn (the dam's buttons, digging on the beach).
        if (chosen.Action != "LOOK" && state.Running && !state.AwaitingPatchAnswer)
            state.Here.RoomAction?.Invoke(state, chosen.Action);
    }

    /// <summary>Dispatch to Do* or ObjectActions by action name (SFCN).</summary>
    private static void Dispatch(GameState state, string action, GameObject? direct, GameObject? indirect, string[] words)
    {
        switch (action)
        {
            case "LOOK": DoLook(state); break;
            case "TAKE":
                if (direct != null)
                {
                    // Objects with custom TAKE message (e.g. skeleton, trophy case, thief) are not takeable
                    if (direct.ObjectAction != null && !direct.Flags.HasFlag(ObjectFlags.Take))
                    {
                        direct.ObjectAction(state, "TAKE", null);
                        return;
                    }
                    DoTake(state, direct);
                }
                else state.Output.WriteLine("What do you want to take?");
                break;
            case "DROP":
                if (direct != null) DoDrop(state, direct);
                else state.Output.WriteLine("What do you want to drop?");
                break;
            case "INVENTORY": DoInventory(state); break;
            case "OPEN":
                if (direct != null) DoOpen(state, direct);
                else state.Output.WriteLine("What do you want to open?");
                break;
            case "CLOSE":
                if (direct != null) DoClose(state, direct);
                else state.Output.WriteLine("What do you want to close?");
                break;
            case "UNLOCK":
                // np.93 ORTELL wording for a missing object.
                if (direct != null) ObjectActions.Unlocker(state, direct, indirect);
                else state.Output.WriteLine("Unlock what?");
                break;
            case "LOCK":
                if (direct != null) ObjectActions.Locker(state, direct);
                else state.Output.WriteLine("Lock what?");
                break;
            case "READ":
                if (direct != null) ObjectActions.ReadObject(state, direct);
                else state.Output.WriteLine("What do you want to read?");
                break;
            case "EAT":
                if (direct != null) ObjectActions.EatObject(state, direct);
                else state.Output.WriteLine("What do you want to eat?");
                break;
            case "DRINK":
                if (direct != null) ObjectActions.DrinkObject(state, direct);
                else state.Output.WriteLine("What do you want to drink?");
                break;
            case "THROW": DoThrow(state, words); break;
            case "WAVE":
                if (direct != null) DoWave(state, direct);
                else state.Output.WriteLine("What do you want to wave?");
                break;
            case "ATTACK": DoAttack(state, words); break;
            case "MOVE":
                if (direct != null) DoMove(state, direct);
                else state.Output.WriteLine("What do you want to move?");
                break;
            case "LIFT":
                if (direct != null) DoLift(state, direct);
                else state.Output.WriteLine("What do you want to lift?");
                break;
            case "PUT": DoPut(state, words); break;
            case "GIVE": DoGive(state, words); break;
            case "RAISE":
                if (direct != null && direct.ObjectAction != null) { direct.ObjectAction(state, "RAISE", null); }
                else state.Output.WriteLine("You can't raise that.");
                break;
            case "LOWER":
                if (direct != null && direct.ObjectAction != null) { direct.ObjectAction(state, "LOWER", null); }
                else state.Output.WriteLine("You can't lower that.");
                break;
            case "BURN":
                if (direct != null) DoBurn(state, direct);
                else state.Output.WriteLine("What do you want to burn?");
                break;
            case "FILL": DoFill(state, words); break;
            case "TIE": DoTie(state, words); break;
            case "UNTIE":
                if (direct != null && direct.ObjectAction != null) direct.ObjectAction(state, "UNTIE", null);
                else state.Output.WriteLine("What do you want to untie?");
                break;
            case "BREAK":
                if (direct != null && direct.ObjectAction != null) { direct.ObjectAction(state, "MUNG", null); }
                else state.Output.WriteLine("What do you want to break?");
                break;
            case "POUR":
                if (direct != null && direct.ObjectAction != null) direct.ObjectAction(state, "POUR", null);
                else state.Output.WriteLine("What do you want to pour?");
                break;
            case "TURN":
            case "TURNON":
            case "TURNOFF":
                if (direct == null)
                {
                    state.Output.WriteLine(action == "TURN" ? "What do you want to turn?" : "What do you want to turn " + (action == "TURNON" ? "on" : "off") + "?");
                    break;
                }
                if (action == "TURN" && (direct.Flags & ObjectFlags.Turnable) != 0)
                {
                    // dung.56 TURN obj WITH tool: GWIM picks the one tool you carry.
                    indirect ??= ObjectActions.FindOnlyTool(state);
                    if (indirect == null) { state.Output.WriteLine("With what?"); break; }
                    state.LastIndirectObject = indirect;
                }
                if (direct.ObjectAction != null) direct.ObjectAction(state, action, indirect);
                else if (action == "TURNON") LampOn(state, direct);
                else if (action == "TURNOFF") LampOff(state, direct);
                else state.Output.WriteLine("Nothing happens.");
                break;
            case "QUIT": Scoring.Quit(state); break;
            case "SCORE": Scoring.PrintScore(state, ask: true); break;
            case "BRIEF": state.Winner.BriefMode = true; state.Output.WriteLine("Brief mode is now on."); break;
            case "UNBRIEF": state.Winner.BriefMode = false; state.Output.WriteLine("Brief mode is now off."); break;
            case "SUPERBRIEF": state.Winner.SuperBriefMode = true; state.Output.WriteLine("Super-brief mode is now on."); break;
            case "UNSUPERBRIEF": state.Winner.SuperBriefMode = false; state.Output.WriteLine("Super-brief mode is now off."); break;
            case "INFO": state.Output.WriteLine("Zork is a game of adventure, danger, and low cunning."); break;
            case "SAVE": GameSave.Save(state); break;
            case "RESTORE": GameSave.Restore(state); break;
            case "SCRIPT":
                try
                {
                    state.ScriptFile?.Close();
                    state.ScriptFile = new StreamWriter(GameSave.DefaultScriptPath, append: true);
                    state.Output = new TeeWriter(Console.Out, state.ScriptFile);
                    state.Output.WriteLine("Scripting to " + GameSave.DefaultScriptPath + ".");
                }
                catch (Exception ex) { state.Output.WriteLine("Script failed: " + ex.Message); }
                break;
            case "UNSCRIPT":
                state.ScriptFile?.Close();
                state.ScriptFile = null;
                state.Output = Console.Out;
                state.Output.WriteLine("Script file closed.");
                break;
            case "DIAGNOSE": ObjectActions.Diagnose(state); break;
            case "EXORCISE": break; // act1.38 EXORCISE: the room (LLD-ROOM) answers, after the verb
            case "WELL": RoomActions.Well(state); break;
            case "BOARD":
                if (direct != null) DoBoard(state, direct);
                else state.Output.WriteLine("What do you want to board?");
                break;
            case "DISEMBARK": DoDisembark(state); break;
            case "WALK": state.Output.WriteLine("Go where?"); break;
            case "STRIKE":
                // dung.56 STRIKE: a villain is attacked, anything else is lit.
                Dispatch(state, direct?.IsVillain == true ? "ATTACK" : "TURNON", direct, indirect, words);
                break;
            case "POKE":
                // act1.38 MUNGER: a villain with a weapon is attacked; the rest is Verbs' MUNG.
                if (direct?.IsVillain == true && indirect != null && (indirect.Flags & ObjectFlags.Weapon) != 0)
                    Dispatch(state, "ATTACK", direct, indirect, words);
                else
                    Verbs.Run(state, "MUNG", direct, indirect);
                break;
            case "SWING":
                // act1.38 SWINGER: SWING weapon AT villain is ATTACK villain WITH weapon.
                if (indirect == null) { state.Output.WriteLine("Swing at what?"); break; }
                state.LastDirectObject = indirect;
                state.LastIndirectObject = direct;
                Dispatch(state, "ATTACK", indirect, direct, words);
                break;
            default:
                if (!Verbs.Run(state, action, direct, indirect)) state.Output.WriteLine("I don't know how to do that.");
                break;
        }
    }

    private static void SetPending(GameState state, string verb, string[] words)
    {
        state.PendingVerb = verb;
        state.PendingWords = words;
        state.PendingNeedsDirect = true;
    }

    private static void DispatchOrphanVerb(GameState state, string verb, GameObject obj)
    {
        state.LastDirectObject = obj;
        switch (verb)
        {
            case "TAKE": DoTake(state, obj); break;
            case "DROP": DoDrop(state, obj); break;
            case "OPEN": DoOpen(state, obj); break;
            case "CLOSE": DoClose(state, obj); break;
            default: state.Output.WriteLine("I don't know how to do that."); break;
        }
    }

    private static string? ResolveDirection(string w)
    {
        if (DirectionSynonyms.TryGetValue(w, out var d)) return d;
        if (w == "TRAVE") return "CROSS"; // dung.56 DSYNONYM "CROSS" "TRAVE"
        var dirs = new[] { "NORTH", "SOUTH", "EAST", "WEST", "UP", "DOWN", "ENTER", "EXIT", "NE", "NW", "SE", "SW", "LAUNC", "LAND", "CROSS", "CLIMB" };
        return dirs.FirstOrDefault(d => NormWord(d) == w);
    }

    private static GameObject? ResolveObject(GameState state, string[] words, int startIndex, out string? ambiguousWord)
    {
        ambiguousWord = null;
        state.PendingAmbiguousCandidates = null;
        if (state.World == null) return null;
        var lit = GameState.IsLit(state);
        var adjectives = new List<string>();
        for (var i = startIndex; i < words.Length; i++)
        {
            var w = NormWord(words[i]);
            if (string.IsNullOrEmpty(w)) continue;
            // Trap door in LROOM: OPEN TRAP / OPEN DOOR finds it even in dark (so player can open after moving rug)
            if (state.Here?.Id == "LROOM" && (w == "TRAP" || w == "DOOR"))
            {
                var door = state.World?.FindObject("DOOR");
                if (door != null && door.InRoom == state.Here)
                {
                    state.PendingAmbiguousCandidates = null;
                    return door;
                }
            }
            // Rug in LROOM: MOVE RUG must find it so RUG-MOVED gets set (check word first so we only run when resolving RUG)
            if ((w == "RUG" || w == "CARPE") && state.Here?.Id == "LROOM")
            {
                var rug = state.Here.Objects.FirstOrDefault(o => o.Id == "RUG") ?? state.World?.FindObject("RUG");
                if (rug != null)
                {
                    state.PendingAmbiguousCandidates = null;
                    return rug;
                }
            }
            var inInv = state.Winner.Inventory.Where(o => MatchObject(o, w)).ToList();
            IEnumerable<GameObject> inRoomList = Array.Empty<GameObject>();
            if (lit && state.Here != null)
            {
                var roomList = state.Here.Objects.Where(o => o.IsVisible || o.IsOpenOrTransparent)
                    .Concat(state.Here.Objects.Where(o => o.IsOpenOrTransparent || o.Open).SelectMany(o => o.Contents.Where(c => c.IsVisible))).ToList();
                // Trap door in living room: matchable for OPEN once rug is moved
                if (state.Here.Id == "LROOM" && state.GetFlag("RUG-MOVED!-FLAG"))
                {
                    var door = state.World?.FindObject("DOOR");
                    if (door != null && door.InRoom == state.Here && !roomList.Contains(door))
                        roomList.Add(door);
                }
                // Rug in LROOM: ensure MOVE RUG finds it when lit (belt-and-braces)
                if (state.Here.Id == "LROOM")
                {
                    var rug = state.World?.FindObject("RUG");
                    if (rug != null && rug.InRoom == state.Here && !roomList.Contains(rug))
                        roomList.Add(rug);
                }
                inRoomList = roomList;
            }
            // MDL ADD-STAR objects are within reach everywhere, light or dark.
            inRoomList = inRoomList.Concat(state.World!.Objects.Where(o => o.IsGlobal));
            var inRoom = inRoomList.Where(o => MatchObject(o, w)).ToList();
            var all = inInv.Concat(inRoom).Distinct().ToList();
            // An adjective (MDL OADJS) narrows the noun that follows it: "PUSH YELLOW BUTTON".
            if (all.Count == 0 && state.Winner.Inventory.Concat(inRoomList).Any(o => o.Adjectives.Contains(w)))
            {
                adjectives.Add(w);
                continue;
            }
            if (adjectives.Count > 0)
                all = all.Where(o => adjectives.All(o.Adjectives.Contains)).ToList();
            if (all.Count > 1) { ambiguousWord = words[i]; state.PendingAmbiguousCandidates = all; return null; }
            if (all.Count == 1) { state.PendingAmbiguousCandidates = null; return all[0]; }
            if (!lit && inInv.Count == 0) state.Output.WriteLine("It is too dark in here to see.");
            else state.Output.WriteLine("I can't see a " + words[i] + " here.");
            return null;
        }
        return null;
    }

    private static bool MatchObject(GameObject o, string word)
    {
        if (NormWord(o.Id) == word) return true;
        if (o.Synonyms.Any(s => NormWord(s) == word)) return true;
        if (NormWord(o.DescriptionShort) == word) return true;
        return false;
    }

    private const string NoWay = "There is no way to go in that direction.";

    /// <summary>
    /// rooms.99 GOTO: the river (RWATERBIT) needs the magic boat, and you can't walk from land to land sitting in it.
    /// (The balloon, not ported yet, counts as a land vehicle here.)
    /// </summary>
    private static bool CanGo(GameState state, Room from, Room to)
    {
        var vehicle = state.Winner.Vehicle;
        var boat = vehicle?.Id == "RBOAT";
        if ((to.IsWater && !boat) || (!from.IsWater && !to.IsWater && boat))
        {
            state.Output.WriteLine(vehicle != null ? "You can't go there in a " + vehicle.DescriptionShort + "." : "You can't go there without a vehicle.");
            return false;
        }
        return true;
    }

    private static void TryGo(GameState state, string direction)
    {
        var r = state.Here;
        // rooms.99 WALK: leaving a dark room, PROB 75 that a grue gets you unless the way leads somewhere lit.
        // (A closed conditional exit's own action, such as the carousel's, isn't tried in the dark.)
        if (!GameState.IsLit(state) && state.Random.Next(100) < 75)
        {
            if (!r.Exits.TryGetValue(direction, out var dark))
            {
                state.JigsUp("Oh, no!  You walked into the slavering fangs of a lurking grue.");
                return;
            }
            var open = !dark.IsBlocked && dark.RoomId != null && (!dark.IsConditional || state.GetFlag(dark.ConditionFlag!));
            var lit = open ? state.World?.FindRoom(dark.RoomId!) : null;
            if (lit != null && GameState.IsLit(state, lit))
                MoveToRoom(state, lit);
            else
                state.JigsUp("Oh, no!  A fearsome grue slithered into the room and devoured you.");
            return;
        }
        // rooms.99 WALK: no exit -> "There is no way to go in that direction."; #NEXIT -> its message (even "");
        // a closed #CEXIT -> its message, nothing if that is "", or the no-way message if it has none.
        if (!r.Exits.TryGetValue(direction, out var target))
        {
            state.Output.WriteLine(NoWay);
            return;
        }
        if (target.IsBlocked)
        {
            state.Output.WriteLine(target.BlockedMessage);
            return;
        }
        if (target.IsConditional && target.ConditionFlag != null)
        {
            if (state.World != null && state.World.TryGetBeforeExitAction(r.Id, direction, out var beforeAction) && beforeAction != null)
                beforeAction(state);
            if (state.World != null && state.World.TryGetExitAction(r.Id, direction, out var exitAction) && exitAction != null
                && exitAction(state, direction))
                return;
            if (!state.GetFlag(target.ConditionFlag))
            {
                if (target.BlockedMessage == null) state.Output.WriteLine(NoWay);
                else if (target.BlockedMessage.Length > 0) state.Output.WriteLine(target.BlockedMessage);
                return;
            }
        }
        var nextRoom = state.World!.FindRoom(target.RoomId!);
        if (nextRoom == null) { state.Output.WriteLine(NoWay); return; }
        if (!CanGo(state, r, nextRoom)) return;
        if (nextRoom.Munged)
        {
            state.Output.WriteLine(nextRoom.MungedMessage ?? "The way is blocked.");
            return;
        }
        MoveToRoom(state, nextRoom);
    }

    /// <summary>Move player to a room (score, seen, onEnter, look). Used by TryGo and exit actions (e.g. CAROUSEL-OUT).</summary>
    public static void MoveToRoom(GameState state, Room nextRoom)
    {
        var vehicle = state.Winner.Vehicle;
        if (vehicle != null && vehicle.InRoom != null)
        {
            vehicle.InRoom.Objects.Remove(vehicle);
            vehicle.InRoom = null;
        }
        state.Winner.CurrentRoom = nextRoom;
        if (vehicle != null)
        {
            vehicle.InRoom = nextRoom;
            if (!nextRoom.Objects.Contains(vehicle))
                nextRoom.Objects.Add(vehicle);
        }
        // No Moves++ here: the original (rooms.99 RDCOM) counts one move per command, and GOTO never adds one.
        if (!nextRoom.Seen) state.Winner.Score += nextRoom.VisitScore;
        var wasSeen = nextRoom.Seen;
        nextRoom.Seen = true;
        // rooms.99 ROOM-INFO: describe the room and its objects first, then run the room's WALK-IN action. In the
        // dark ROOM-INFO stops after "It is pitch black", so the WALK-IN action doesn't run.
        if (DoLook(state, full: false, seen: wasSeen) && state.Running)
            nextRoom.OnEnter?.Invoke(state);
    }

    /// <summary>
    /// rooms.99 ROOM-INFO. <paramref name="full"/> is true for an explicit LOOK. Returns false when the room is dark
    /// (nothing but the grue warning was printed).
    /// </summary>
    private static bool DoLook(GameState state, bool full = true, bool seen = true)
    {
        var r = state.Here;
        if (!GameState.IsLit(state))
        {
            state.Output.WriteLine("It is pitch black.  You are likely to be eaten by a grue.");
            return false;
        }
        var brief = state.Winner.BriefMode && seen && !full;
        var superBrief = state.Winner.SuperBriefMode && !full;
        // ROOM-INFO: the room's action describes it only when the room has no description of its own.
        if ((superBrief || brief) && !string.IsNullOrEmpty(r.DescriptionShort))
            state.Output.WriteLine(r.DescriptionShort);
        else if (!string.IsNullOrEmpty(r.DescriptionLong))
            state.Output.WriteLine(r.DescriptionLong);
        else if (r.RoomAction != null)
            r.RoomAction(state, "LOOK");
        else
            state.Output.WriteLine(r.DescriptionShort);
        var vehicle = state.Winner.Vehicle;
        if (vehicle != null) state.Output.WriteLine("You are in the " + vehicle.DescriptionShort + ".");
        // PRINT-CONT's CASE?: the trophy case contents are left out of brief room descriptions.
        var showCase = full || !(state.Winner.SuperBriefMode || state.Winner.BriefMode);
        foreach (var o in r.Objects)
        {
            if (o.IsVisible && (o.Flags & ObjectFlags.NoDescribe) == 0 && o != vehicle)
            {
                var desc = LongDescription(o);
                if (!string.IsNullOrEmpty(desc))
                    state.Output.WriteLine(vehicle != null ? desc + " [in the room]" : desc);
            }
            if (SeeInside(o)) PrintContents(state, o, "", showCase);
        }
        return true;
    }

    /// <summary>rooms.99 LONG-DESC-OBJ: the "untouched" description (ODESCO) until the object is first taken.</summary>
    private static string LongDescription(GameObject o) =>
        o.Touched || string.IsNullOrEmpty(o.DescriptionUntouched) ? o.DescriptionHere : o.DescriptionUntouched;

    /// <summary>defs.63 SEE-INSIDE?: visible, and transparent or open.</summary>
    private static bool SeeInside(GameObject o) => o.IsVisible && (o.IsTransparent || o.Open);

    /// <summary>rooms.99 PRINT-CONT: "The bottle contains:" and one line per visible item, nested containers indented.</summary>
    private static void PrintContents(GameState state, GameObject container, string indent, bool showCase)
    {
        if (container.Contents.Count == 0) return;
        if (container.Id == "TCASE")
        {
            if (!showCase) return;
            state.Output.WriteLine("Your collection of treasures consists of:");
        }
        else
            state.Output.WriteLine(indent + "The " + container.DescriptionShort + " contains:");
        foreach (var y in container.Contents)
        {
            if (y.IsVisible && (y.Flags & ObjectFlags.NoDescribe) == 0 && !string.IsNullOrEmpty(y.DescriptionShort))
                state.Output.WriteLine(indent + " A " + y.DescriptionShort);
            if (SeeInside(y)) PrintContents(state, y, indent + " ", showCase);
        }
    }

    /// <summary>rooms.99 PRINT-CONTENTS: "a leaflet", "a bag, and a bottle", "a, b, and c".</summary>
    private static string ListContents(List<GameObject> objects)
    {
        var sb = new System.Text.StringBuilder();
        for (var i = 0; i < objects.Count; i++)
        {
            sb.Append("a ").Append(objects[i].DescriptionShort);
            var left = objects.Count - i;
            if (left > 2) sb.Append(", ");
            else if (left == 2) sb.Append(", and ");
        }
        return sb.ToString();
    }

    /// <summary>rooms.99 BOARD (the balloon keeps the port's own rules until it is ported).</summary>
    private static void DoBoard(GameState state, GameObject obj)
    {
        if (obj.Id != "BALLO")
        {
            if (obj.InRoom != state.Here)
                state.Output.WriteLine("The " + obj.DescriptionShort + " must be on the ground to be boarded.");
            else if ((obj.Flags & ObjectFlags.Vehicle) == 0)
                state.Output.WriteLine("I suppose you have a theory on boarding " + obj.DescriptionShort + "s.");
            else if (state.Winner.Vehicle != null)
                state.Output.WriteLine("You are already in a " + obj.DescriptionShort + ", cretin!");
            else if (!ObjectActions.Try(state, obj, "BOARD", null))
            {
                state.Output.WriteLine("You are in the " + obj.DescriptionShort + ".");
                state.Winner.Vehicle = obj;
            }
            return;
        }
        if (obj.InRoom != state.Here)
        {
            state.Output.WriteLine("You need to be at the balloon.");
            return;
        }
        if (!state.GetFlag("BINF!-FLAG"))
        {
            state.Output.WriteLine("The balloon isn't inflated.");
            return;
        }
        if (state.Winner.Vehicle == obj)
        {
            state.Output.WriteLine("You're already in the balloon.");
            return;
        }
        state.Winner.Vehicle = obj;
        state.SetFlag("IN-BALLOON!-FLAG", true);
        state.Output.WriteLine("You are now in the basket of the balloon.");
    }

    private static void DoDisembark(GameState state)
    {
        if (state.Winner.Vehicle == null)
        {
            state.Output.WriteLine("You're not in anything.");
            return;
        }
        var v = state.Winner.Vehicle;
        if (v.Id != "BALLO")
        {
            // rooms.99 UNBOARD
            if (ObjectActions.Try(state, v, "DISEMBARK", null)) return;
            if (state.Here.IsWater)
                state.Output.WriteLine("You realize, just in time, that disembarking here would probably be fatal.");
            else
            {
                state.Output.WriteLine("You are on your own feet again.");
                state.Winner.Vehicle = null;
            }
            return;
        }
        state.Winner.Vehicle = null;
        if (v.Id == "BALLO")
            state.SetFlag("IN-BALLOON!-FLAG", false);
        state.Output.WriteLine("You have left the " + v.DescriptionShort + ".");
    }

    private static void DoTake(GameState state, GameObject obj)
    {
        if (obj == state.Winner.Vehicle)
        {
            state.Output.WriteLine("You're already in the " + obj.DescriptionShort + ".");
            return;
        }
        if (obj.Carrier == state.Winner) { state.Output.WriteLine("You already have that."); return; }
        if (!obj.CanTake) { state.Output.WriteLine("You can't take that."); return; }
        if (obj.Container != null && !obj.Container.Open && !obj.Container.IsTransparent)
        { state.Output.WriteLine("You can't reach that."); return; }
        if (obj.InRoom != null)
        {
            if (state.Winner.CarryingSize + obj.Size > Adventurer.MaxLoad)
            { state.Output.WriteLine("You're carrying too much."); return; }
            obj.InRoom.Objects.Remove(obj);
        }
        else if (obj.Container != null)
            obj.Container.Contents.Remove(obj);
        obj.InRoom = null;
        obj.Container = null;
        obj.Carrier = state.Winner;
        state.Winner.Inventory.Add(obj);
        // TAKE calls the object's action (rooms.99 APPLY-OBJECT); for the candles that starts their clock.
        if (obj.Id == "CANDL") obj.ObjectAction?.Invoke(state, "TAKE", null);
        // rooms.99 TAKE sets OTOUCH? on every take; SCORE-OBJ scores the find only the first time.
        if (!obj.Touched) state.Winner.Score += obj.FindScore;
        obj.Touched = true;
        state.Output.WriteLine("Taken.");
    }

    private static void DoDrop(GameState state, GameObject obj)
    {
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        state.Winner.Inventory.Remove(obj);
        obj.Carrier = null;
        obj.InRoom = state.Here;
        state.Here.Objects.Add(obj);
        state.Output.WriteLine("Dropped.");
    }

    /// <summary>OPEN: try ObjectAction first (e.g. window, trap door) so objects with custom open behavior work even if not Container/Door.</summary>
    private static void DoOpen(GameState state, GameObject obj)
    {
        // rooms.99 OPENER: the object's own action first, if it has something to say.
        state.LastDirectObject = obj;
        if (ObjectActions.Try(state, obj, "OPEN", null)) return;
        if (!obj.IsContainer && (obj.Flags & ObjectFlags.Door) != 0)
        {
            // Doors without their own action (the original's DDOOR-FUNCTION isn't ported yet).
            if (obj.Open) state.Output.WriteLine("It is already open.");
            else { obj.Open = true; state.Output.WriteLine("Opened."); }
            return;
        }
        // rooms.99 OPENER
        if (!obj.IsContainer)
            state.Output.WriteLine("You must tell me how to do that to a " + obj.DescriptionShort + ".");
        else if (obj.Capacity == 0)
            state.Output.WriteLine("The " + obj.DescriptionShort + " cannot be opened.");
        else if (obj.Open)
            state.Output.WriteLine("It is already open.");
        else
        {
            obj.Open = true;
            if (obj.Contents.Count == 0 || obj.IsTransparent)
                state.Output.WriteLine("Opened.");
            else
                state.Output.WriteLine("Opening the " + obj.DescriptionShort + " reveals " + ListContents(obj.Contents) + ".");
        }
    }

    /// <summary>CLOSE: try ObjectAction first (e.g. window) so objects with custom close behavior work.</summary>
    private static void DoClose(GameState state, GameObject obj)
    {
        // rooms.99 CLOSER: the object's own action first, if it has something to say.
        state.LastDirectObject = obj;
        if (ObjectActions.Try(state, obj, "CLOSE", null)) return;
        // rooms.99 CLOSER (doors without their own action keep the plain open/closed toggle)
        if (!obj.IsContainer && (obj.Flags & ObjectFlags.Door) == 0)
            state.Output.WriteLine("You must tell me how to do that to a " + obj.DescriptionShort + ".");
        else if (obj.IsContainer && obj.Capacity == 0)
            state.Output.WriteLine("You cannot close that.");
        else if (!obj.Open) state.Output.WriteLine("It is already closed.");
        else { obj.Open = false; state.Output.WriteLine("Closed."); }
    }

    /// <summary>
    /// rooms.99 LAMP-ON: light a carried light source (OLIGHT? -1 = off, 1 = on, 0 = can't be lit). If that lights
    /// a dark room, the room is described.
    /// </summary>
    internal static void LampOn(GameState state, GameObject obj)
    {
        if (obj.LightAmount == 0 || !state.Winner.Inventory.Contains(obj))
        {
            state.Output.WriteLine("You can't turn that on.");
            return;
        }
        if (obj.LightAmount > 0)
        {
            state.Output.WriteLine("It is already on.");
            return;
        }
        var wasLit = GameState.IsLit(state);
        obj.LightAmount = 1;
        state.Output.WriteLine("The " + obj.DescriptionShort + " is now on.");
        if (!wasLit) DoLook(state);
    }

    /// <summary>rooms.99 LAMP-OFF: put out a carried light source.</summary>
    internal static void LampOff(GameState state, GameObject obj)
    {
        if (obj.LightAmount == 0 || !state.Winner.Inventory.Contains(obj))
        {
            state.Output.WriteLine("You can't turn that off.");
            return;
        }
        if (obj.LightAmount < 0)
        {
            state.Output.WriteLine("It is already off.");
            return;
        }
        obj.LightAmount = -1;
        state.Output.WriteLine("The " + obj.DescriptionShort + " is now off.");
        if (!GameState.IsLit(state)) state.Output.WriteLine("It is now pitch black.");
    }

    private static void DoInventory(GameState state)
    {
        if (state.Winner.Inventory.Count == 0)
            state.Output.WriteLine("You are empty-handed.");
        else
            state.Output.WriteLine("You are carrying:" + Environment.NewLine + "  " + string.Join(Environment.NewLine + "  ", state.Winner.Inventory.Select(o => o.DescriptionShort)));
    }


    private static void DoThrow(GameState state, string[] words)
    {
        var obj = state.LastDirectObject ?? ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to throw?"); return; }
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        var atStart = 2;
        if (words.Length > 3 && (NormWord(words[2]) == "AT" || string.Equals(words[2], "at", StringComparison.OrdinalIgnoreCase))) atStart = 3;
        var atObj = state.LastIndirectObject ?? (words.Length >= atStart + 1 ? ResolveObject(state, words, atStart, out _) : null);
        state.LastDirectObject = obj;
        state.LastIndirectObject = atObj;
        if (state.Here.Id == "ICY" && atObj?.Id == "ICE" && obj.Id == "TORCH" && obj.LightAmount > 0)
        {
            state.Output.WriteLine("The torch hits the glacier and explodes into a great ball of flame, devouring the glacier.  The water from the melting glacier rushes downstream, carrying the torch with it.  In the place of the glacier, there is a passageway leading west.");
            state.Winner.Inventory.Remove(obj);
            var ice = state.World!.FindObject("ICE");
            if (ice != null) { state.Here.Objects.Remove(ice); }
            var strea = state.World.FindRoom("STREA");
            if (strea != null) { obj.InRoom = strea; obj.Carrier = null; strea.Objects.Add(obj); obj.LightAmount = 0; obj.Flags &= ~ObjectFlags.OnFire; obj.DescriptionShort = "burned out ivory torch"; obj.DescriptionHere = "There is a burned out ivory torch here."; }
            state.SetFlag("GLACIER-FLAG!-FLAG", true);
            return;
        }
        if (state.Here.Id == "MTROL" && atObj?.Id == "TROLL")
        {
            state.Output.WriteLine("The troll, who is remarkably coordinated, catches the " + obj.DescriptionShort + ".");
            return;
        }
        if (obj.ObjectAction != null)
        {
            obj.ObjectAction(state, "THROW", atObj);
            return;
        }
        state.Winner.Inventory.Remove(obj);
        obj.Carrier = null;
        obj.InRoom = state.Here;
        state.Here.Objects.Add(obj);
        state.Output.WriteLine("The " + obj.DescriptionShort + " lands on the ground.");
    }

    private static void DoWave(GameState state, GameObject obj)
    {
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        if (obj.ObjectAction != null) { obj.ObjectAction(state, "WAVE", null); return; }
        state.Output.WriteLine("Nothing happens.");
    }

    private static void DoAttack(GameState state, string[] words)
    {
        var obj = state.LastDirectObject ?? ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to attack?"); return; }
        var withStart = 2;
        if (words.Length > 3 && IsPrepWith(NormWord(words[2]))) withStart = 3;
        var withObj = state.LastIndirectObject ?? (words.Length >= withStart + 1 ? ResolveObject(state, words, withStart, out _) : state.Winner.Inventory.FirstOrDefault(o => (o.Flags & ObjectFlags.Weapon) != 0));
        if (obj.Id == "TROLL" && state.Here.Id == "MTROL")
        {
            if (withObj == null || withObj.Carrier != state.Winner) { state.Output.WriteLine("You need a weapon to attack the troll."); return; }
            if ((withObj.Flags & ObjectFlags.Weapon) == 0) { state.Output.WriteLine("You can't attack with that."); return; }
            var troll = state.World?.FindObject("TROLL");
            if (troll != null)
            {
                if ((troll.Flags & ObjectFlags.Fighting) != 0)
                {
                    // Already in melee; FightDemon handles rounds. One extra hit from explicit ATTACK.
                    if (state.VillainStrength.TryGetValue(troll.Id, out var s))
                    {
                        s--;
                        state.VillainStrength[troll.Id] = s;
                        if (s <= 0)
                        {
                            state.VillainStrength.Remove(troll.Id);
                            troll.Flags &= ~ObjectFlags.Fighting;
                            state.Here.Objects.Remove(troll);
                            troll.InRoom = null;
                            state.SetFlag("TROLL-FLAG!-FLAG", true);
                            state.Output.WriteLine("The troll is killed by the force of your attack.  The body has vanished.");
                        }
                        else state.Output.WriteLine("You hit the troll.");
                    }
                    return;
                }
                // Enter melee (FIGHT-DEMON will run each move)
                troll.Flags |= ObjectFlags.Fighting;
                state.VillainStrength[troll.Id] = 2; // two hits to kill
                state.Output.WriteLine("The troll blocks your blow with his axe, and you are thrown back.");
            }
            return;
        }
        if (obj.ObjectAction != null)
        {
            state.LastDirectObject = obj;
            obj.ObjectAction(state, "ATTACK", withObj);
            return;
        }
        state.Output.WriteLine("Violence isn't the answer to this one.");
    }

    private static void DoMove(GameState state, GameObject obj)
    {
        if (obj.ObjectAction != null) { state.LastDirectObject = obj; obj.ObjectAction(state, "MOVE", null); return; }
        if (state.Here.Id == "CLEAR" && obj.Id == "LEAVE" && state.Here.RVars == 0)
        {
            state.Output.WriteLine("A grating appears on the ground.");
            state.Here.RVars = 1;
            var grat = state.World?.FindObject("GRAT1");
            if (grat != null) grat.Flags |= ObjectFlags.Visible;
            return;
        }
        state.Output.WriteLine("You can't move that.");
    }

    private static void DoLift(GameState state, GameObject obj)
    {
        if (obj.ObjectAction != null) { state.LastDirectObject = obj; obj.ObjectAction(state, "LIFT", null); return; }
        state.Output.WriteLine("You can't lift that.");
    }

    private static void DoPut(GameState state, string[] words)
    {
        var obj = state.LastDirectObject ?? ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to put?"); return; }
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        var inIdx = Array.FindIndex(words, i => IsPrepIn(NormWord(i)));
        var cont = state.LastIndirectObject;
        if (cont == null && inIdx >= 0 && inIdx + 1 < words.Length)
            cont = ResolveObject(state, words, inIdx + 1, out _);
        if (cont == null) { state.Output.WriteLine("What do you want to put that in?"); return; }
        state.LastDirectObject = obj;
        state.LastIndirectObject = cont;
        if (obj.ObjectAction != null)
        {
            obj.ObjectAction(state, "PUT", cont);
            return;
        }
        // OBJECT-ACTION asks the indirect object too (e.g. TUBE-FUNCTION refuses everything).
        if (ObjectActions.Try(state, cont, "PUT", obj)) return;
        if (!cont.IsContainer && (cont.Flags & ObjectFlags.Container) == 0) { state.Output.WriteLine("You can't put things in that."); return; }
        if (!cont.Open && !cont.IsTransparent) { state.Output.WriteLine("That's closed."); return; }
        if (cont.Contents.Sum(o => o.Size) + obj.Size > cont.Capacity) { state.Output.WriteLine("That won't fit."); return; }
        state.Winner.Inventory.Remove(obj);
        obj.Carrier = null;
        obj.Container = cont;
        cont.Contents.Add(obj);
        if (cont.Id == "TCASE" && obj.TrophyScore > 0)
            state.Winner.Score += obj.TrophyScore;
        state.Output.WriteLine("Done.");
    }

    private static void DoGive(GameState state, string[] words)
    {
        var obj = state.LastDirectObject ?? ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to give?"); return; }
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        var toIdx = Array.FindIndex(words, i => i.Equals("to", StringComparison.OrdinalIgnoreCase));
        var toObj = state.LastIndirectObject ?? (toIdx >= 0 && toIdx + 1 < words.Length ? ResolveObject(state, words, toIdx + 1, out _) : null);
        if (toObj == null) { state.Output.WriteLine("Who do you want to give that to?"); return; }
        state.LastDirectObject = obj;
        state.LastIndirectObject = toObj;
        if (toObj.ObjectAction != null) toObj.ObjectAction(state, "GIVE", obj);
        else state.Output.WriteLine("The " + toObj.DescriptionShort + " doesn't want that.");
    }

    private static void DoBurn(GameState state, GameObject obj)
    {
        if (state.Here.Id == "CLEAR" && obj.Id == "LEAVE" && state.Here.RVars == 0)
        {
            state.Output.WriteLine("A grating appears on the ground.");
            state.Here.RVars = 1;
            var grat = state.World?.FindObject("GRAT1");
            if (grat != null) grat.Flags |= ObjectFlags.Visible;
            return;
        }
        if (obj.Container?.Id == "RECEP")
        {
            ObjectActions.BurnInReceptacle(state, obj);
            return;
        }
        if (obj.ObjectAction != null) { state.LastDirectObject = obj; obj.ObjectAction(state, "BURN", null); return; }
        state.Output.WriteLine("You can't burn that.");
    }

    private static void DoFill(GameState state, string[] words)
    {
        var obj = ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to fill?"); return; }
        var fillableRooms = new[] { "RESES", "STREA" };
        if (!fillableRooms.Contains(state.Here.Id)) { state.Output.WriteLine("I can't find any water here."); return; }
        var bottle = state.World?.FindObject("BOTTL");
        if (obj != bottle) { state.Output.WriteLine("You can only fill the bottle with water."); return; }
        if (bottle == null || !state.Winner.Inventory.Contains(bottle)) { state.Output.WriteLine("You're not carrying the bottle."); return; }
        if (!bottle.Open) { state.Output.WriteLine("The bottle is closed."); return; }
        if (bottle.Contents.Count > 0) { state.Output.WriteLine("The bottle is already full."); return; }
        var water = state.World?.FindObject("WATER");
        if (water == null) { state.Output.WriteLine("I can't find any water here."); return; }
        if (water.Container != null) water.Container.Contents.Remove(water);
        if (water.InRoom != null) { water.InRoom.Objects.Remove(water); water.InRoom = null; }
        water.Carrier = null;
        water.Container = bottle;
        bottle.Contents.Add(water);
        state.Output.WriteLine("The bottle is now full of water.");
    }

    private static void DoTie(GameState state, string[] words)
    {
        var obj = state.LastDirectObject ?? ResolveObject(state, words, 1, out _);
        if (obj == null) { state.Output.WriteLine("What do you want to tie?"); return; }
        var toIdx = Array.FindIndex(words, i => i.Equals("to", StringComparison.OrdinalIgnoreCase));
        var toObj = state.LastIndirectObject ?? (toIdx >= 0 && toIdx + 1 < words.Length ? ResolveObject(state, words, toIdx + 1, out _) : null);
        state.LastDirectObject = obj;
        state.LastIndirectObject = toObj;
        if (obj.ObjectAction != null) obj.ObjectAction(state, "TIE", toObj);
        else state.Output.WriteLine("You can't tie that.");
    }
}
