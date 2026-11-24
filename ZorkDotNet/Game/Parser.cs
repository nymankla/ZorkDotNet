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
    /// <summary>
    /// rooms.99 START: "Welcome to Dungeon." and the version line (SAVE-IT passes it in), then the room you start in.
    /// </summary>
    public static void Start(GameState state)
    {
        state.Output.WriteLine("Welcome to Dungeon.");
        state.Output.WriteLine(Verbs.VersionText());
        Execute(state, "LOOK");
    }

    public static void PlayTurn(GameState state, string input)
    {
        if (state.AwaitingQuitConfirmation)
        {
            Scoring.AnswerQuit(state, input.Trim());
            return;
        }
        if (state.EchoListening)
        {
            EchoTurn(state, input);
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
        if (state.Running && !state.AwaitingPatchAnswer) ObjectActions.BucketReadOut(state); // RDCOM: the vehicle's READ-OUT
    }

    /// <summary>
    /// act1.38 ECHO-ROOM's own input loop: every line counts as a move and is echoed back, except walking out of the
    /// room, or a line starting with ECHO, which quiets the room for good.
    /// </summary>
    private static void EchoTurn(GameState state, string input)
    {
        state.Winner.Moves++;
        var words = input.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries)
            .Where(w => !BuzzWords.Contains(NormWord(w))).ToArray();
        var dir = words.Length == 0 ? null : ResolveDirection(NormWord(words[0]));
        if (dir == null && words.Length >= 2 && NormWord(words[0]) == "WALK") dir = ResolveDirection(NormWord(words[1]));
        if (dir != null && state.Here.Exits.ContainsKey(dir))
        {
            var room = state.Here;
            state.EchoListening = false;
            TryGo(state, dir);
            if (state.Here != room) RoomActions.EchoRestore(state);
            else state.EchoListening = true;
            if (state.Running && !state.AwaitingPatchAnswer) state.ProcessClocks();
            return;
        }
        state.Output.WriteLine(input);
        if (input.TrimStart().StartsWith("ECHO", StringComparison.OrdinalIgnoreCase))
        {
            state.Output.WriteLine("The acoustics of the room change subtly.");
            state.SetFlag(Flag.Echo, true);
            state.EchoListening = false;
            RoomActions.EchoRestore(state);
        }
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
                    SynMatch(state, orphanSpec, obj, null, null);
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

        // rooms.99 COMMAND: TELL actor '...' (the robot is the only actor)
        if (first is "TELL" or "COMMA" or "REQUE" && words.Length >= 2)
        {
            var actor = ResolveObject(state, [words[1].Trim('\'', '"')], 0, out _);
            if (actor != null) Robot.Tell(state, actor, words[2..]);
            return;
        }

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
        SynMatch(state, spec, o1, prep2, o2, prep1);
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
    private static void SynMatch(GameState state, VerbSpec spec, GameObject? o1, string? prep2, GameObject? o2, string? prep1 = null)
    {
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
        state.LastVerb = chosen.Action;
        state.LastDirectObject = dobj;
        state.LastIndirectObject = iobj;
        Dispatch(state, chosen.Action, dobj, iobj);
        // rooms.99 RDCOM: after the verb, the room's own action gets its turn (the dam's buttons, digging on the beach).
        // After LOOK, ROOM-INFO has swapped the verb for the innocuous FOO, so the description isn't repeated.
        if (state.Running && !state.AwaitingPatchAnswer)
            state.Here.RoomAction?.Invoke(state, state.LastVerb == "LOOK" ? "FOO" : state.LastVerb!);
    }

    /// <summary>Dispatch to Do* or ObjectActions by action name (SFCN).</summary>
    private static void Dispatch(GameState state, string action, GameObject? direct, GameObject? indirect)
    {
        switch (action)
        {
            case "LOOK":
                DoLook(state);
                Balloon.ReadOut(state);
                break;
            case "TAKE":
                if (Balloon.ReadIn(state, "TAKE", direct, null, null)) break;
                if (direct?.Id is Obj.Every or Obj.Valua) { DoLots(state, "TAKE", direct, null); break; }
                if (direct != null) DoTake(state, direct);
                else state.Output.WriteLine("What do you want to take?");
                break;
            case "DROP":
                if (direct?.Id is Obj.Every or Obj.Valua) DoLots(state, "DROP", direct, null);
                else if (direct != null) Dropper(state, "DROP", direct, indirect);
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
            case "THROW":
                if (direct != null) Dropper(state, "THROW", direct, indirect);
                else state.Output.WriteLine("What do you want to throw?");
                break;
            case "WAVE":
                if (direct != null) DoWave(state, direct);
                else state.Output.WriteLine("What do you want to wave?");
                break;
            case "ATTACK": DoAttack(state, direct, indirect); break;
            case "MOVE":
                if (direct != null) DoMove(state, direct);
                else state.Output.WriteLine("What do you want to move?");
                break;
            case "LIFT":
                if (direct != null) DoLift(state, direct);
                else state.Output.WriteLine("What do you want to lift?");
                break;
            case "PUT":
                if (direct?.Id is Obj.Every or Obj.Valua) DoLots(state, "PUT", direct, indirect);
                else DoPut(state, direct, indirect);
                break;
            case "GIVE":
                // dung.56 GIVE obj TO victim is DROPPER; a missing victim is GWIM'd.
                if (direct == null) { state.Output.WriteLine("What do you want to give?"); break; }
                indirect ??= Gwim(state, ObjectFlags.Victim, noTake: true);
                if (indirect == null) state.Output.WriteLine("To what?");
                else Dropper(state, "GIVE", direct, indirect);
                break;
            case "BURN":
                if (direct != null) DoBurn(state, direct, indirect);
                else state.Output.WriteLine("What do you want to burn?");
                break;
            case "FILL": DoFill(state, direct); break;
            case "TIE": DoTie(state, direct, indirect); break;
            case "UNTIE":
                // act1.38 UNTIE
                if (direct == null) state.Output.WriteLine("What do you want to untie?");
                else if (ObjectActions.ObjectAction(state, "UNTIE", direct, indirect)) { }
                else if ((direct.Flags & ObjectFlags.Tie) != 0) state.Output.WriteLine("I don't think so.");
                else state.Output.WriteLine("This cannot be tied, so it cannot be untied!");
                break;
            case "BREAK":
                // dung.56 makes BREAK a synonym of POKE (act1.38 MUNGER).
                goto case "POKE";
            case "POUR":
                if (direct != null) Dropper(state, "POUR", direct, indirect);
                else state.Output.WriteLine("What do you want to pour?");
                break;
            case "TURN":
            case "TURNON":
            case "TURNOFF":
                // rooms.99 LAMP-ON: LIGHT something burnable WITH something is BURN.
                if (action == "TURNON" && direct != null && indirect != null && (direct.Flags & ObjectFlags.Burnable) != 0)
                {
                    state.LastVerb = "BURN"; // <PUT .PRSVEC 1 ,BURN!-WORDS>: the room's action sees BURN
                    DoBurn(state, direct, indirect);
                    break;
                }
                if (direct == null)
                {
                    state.Output.WriteLine(action == "TURN" ? "What do you want to turn?" : "What do you want to turn " + (action == "TURNON" ? "on" : "off") + "?");
                    break;
                }
                if (action == "TURN")
                {
                    // act1.38 TURNER (TURN obj WITH tool; GWIM picks the one tool you carry).
                    if ((direct.Flags & ObjectFlags.Turnable) == 0) { state.Output.WriteLine("You can't turn that!"); break; }
                    indirect ??= ObjectActions.FindOnlyTool(state);
                    if (indirect == null) { state.Output.WriteLine("With what?"); break; }
                    state.LastIndirectObject = indirect;
                    if ((indirect.Flags & ObjectFlags.Tool) == 0)
                        state.Output.WriteLine("You certainly can't turn it with a " + indirect.DescriptionShort + ".");
                    else if (!ObjectActions.ObjectAction(state, action, direct, indirect))
                        state.Output.WriteLine("Nothing happens.");
                    break;
                }
                if (ObjectActions.ObjectAction(state, action, direct, indirect)) break;
                if (action == "TURNON") LampOn(state, direct);
                else LampOff(state, direct);
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
                Dispatch(state, direct?.IsVillain == true ? "ATTACK" : "TURNON", direct, indirect);
                break;
            case "POKE":
                // act1.38 MUNGER: a villain with a weapon is attacked; the rest is Verbs' MUNG.
                if (direct?.IsVillain == true && indirect != null && (indirect.Flags & ObjectFlags.Weapon) != 0)
                    Dispatch(state, "ATTACK", direct, indirect);
                else
                    Verbs.Run(state, "MUNG", direct, indirect);
                break;
            case "SWING":
                // act1.38 SWINGER: SWING weapon AT villain is ATTACK villain WITH weapon.
                if (indirect == null) { state.Output.WriteLine("Swing at what?"); break; }
                Dispatch(state, "ATTACK", indirect, direct);
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
            case "DROP": Dropper(state, "DROP", obj, null); break;
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
        var lit = GameState.IsLit(state);
        var adjectives = new List<string>();
        for (var i = startIndex; i < words.Length; i++)
        {
            var w = NormWord(words[i]);
            if (string.IsNullOrEmpty(w)) continue;
            var inInv = WithVisibleContents(state.Winner.Inventory).Where(o => MatchObject(o, w)).ToList();
            IEnumerable<GameObject> inRoomList = Array.Empty<GameObject>();
            if (lit && state.Here != null)
            {
                inRoomList = WithVisibleContents(state.Here.Objects.Where(o => o.IsVisible || o.IsOpenOrTransparent)).ToList();
            }
            // MDL ADD-STAR objects are within reach everywhere, light or dark.
            inRoomList = inRoomList.Concat(state.World.Objects.Where(o => o.IsGlobal));
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

    /// <summary>The objects, and whatever can be seen inside them, however deep (open or transparent containers).</summary>
    private static IEnumerable<GameObject> WithVisibleContents(IEnumerable<GameObject> objects)
    {
        foreach (var o in objects)
        {
            yield return o;
            if (o.IsOpenOrTransparent)
                foreach (var inner in WithVisibleContents(o.Contents.Where(c => c.IsVisible)))
                    yield return inner;
        }
    }

    internal static bool Matches(GameObject o, string word) => MatchObject(o, word);

    internal static string? Direction(string normWord) => ResolveDirection(normWord);

    private static bool MatchObject(GameObject o, string word)
    {
        if (NormWord(o.Id) == word) return true;
        if (o.Synonyms.Any(s => NormWord(s) == word)) return true;
        if (NormWord(o.DescriptionShort) == word) return true;
        return false;
    }

    private const string NoWay = "There is no way to go in that direction.";

    /// <summary>
    /// rooms.99 GOTO: the river (RWATERBIT) needs the magic boat, the volcano's air (RAIRBIT) the balloon, and you
    /// can't walk from land to land sitting in either.
    /// </summary>
    private static bool CanGo(GameState state, Room from, Room to)
    {
        var vehicle = state.Winner.Vehicle;
        // The vehicle's ORAND: the boat goes on water (RWATERBIT), the balloon in the air (RAIRBIT).
        var boat = vehicle?.Id == Obj.Rboat;
        var balloon = vehicle?.Id == Obj.Ballo;
        static bool Land(Room r) => !r.IsWater && !r.IsAir;
        var bucket = vehicle?.Id == Obj.Bucke; // RBUCKBIT: up and down the well only
        var fits = to.IsWater ? boat : !to.IsAir || balloon;
        if (!fits || (Land(from) && Land(to) && (boat || balloon || (bucket && !to.IsBucket))))
        {
            state.Output.WriteLine(vehicle != null ? "You can't go there in a " + vehicle.DescriptionShort + "." : "You can't go there without a vehicle.");
            return false;
        }
        return true;
    }

    private static void TryGo(GameState state, string direction)
    {
        var r = state.Here;
        if (Balloon.ReadIn(state, "WALK", null, null, direction)) return;
        if (r.OnWalk?.Invoke(state, direction) == true) return;
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
            var lit = open ? state.World.FindRoom(dark.RoomId!) : null;
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
            if (state.World.TryGetBeforeExitAction(r.Id, direction, out var beforeAction) && beforeAction != null)
                beforeAction(state);
            if (state.World.TryGetExitAction(r.Id, direction, out var exitAction) && exitAction != null
                && exitAction(state, direction))
                return;
            if (!state.GetFlag(target.ConditionFlag))
            {
                if (target.BlockedMessage == null) state.Output.WriteLine(NoWay);
                else if (target.BlockedMessage.Length > 0) state.Output.WriteLine(target.BlockedMessage);
                return;
            }
        }
        var nextRoom = state.World.FindRoom(target.RoomId!);
        if (nextRoom == null) { state.Output.WriteLine(NoWay); return; }
        if (!CanGo(state, r, nextRoom)) return;
        if (nextRoom.Munged)
        {
            state.Output.WriteLine(nextRoom.MungedMessage ?? "The way is blocked.");
            return;
        }
        MoveToRoom(state, nextRoom);
    }

    /// <summary>rooms.99 ROOM-INFO T: describe the room in full, as LOOK does.</summary>
    public static void Describe(GameState state) => DoLook(state);

    /// <summary>
    /// rooms.99 GOTO without ROOM-INFO: the player (and vehicle) is now in <paramref name="room"/>, its points are
    /// scored, but nothing is described.
    /// </summary>
    public static void Goto(GameState state, Room room)
    {
        state.Winner.CurrentRoom = room;
        if (!room.Seen) state.Winner.Score += room.VisitScore;
        room.Seen = true;
    }

    /// <summary>Move player to a room (score, seen, onEnter, look). Used by TryGo and exit actions (e.g. CAROUSEL-OUT).</summary>
    public static void MoveToRoom(GameState state, Room nextRoom)
    {
        state.Winner.Vehicle?.MoveTo(nextRoom);
        state.Winner.CurrentRoom = nextRoom;
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
                if (o.IsActor) Robot.DescribeCarrying(state); // ROOM-INFO: INVENT of an actor
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
        if (container.Id == Obj.Tcase)
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
    private static string ListContents(IReadOnlyList<GameObject> objects)
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

    /// <summary>rooms.99 BOARD</summary>
    private static void DoBoard(GameState state, GameObject obj)
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
    }

    /// <summary>rooms.99 UNBOARD</summary>
    private static void DoDisembark(GameState state)
    {
        var v = state.Winner.Vehicle;
        if (v == null)
        {
            state.Output.WriteLine("You aren't in that!");
            return;
        }
        if (ObjectActions.Try(state, v, "DISEMBARK", null)) return;
        if (state.Here.IsWater || state.Here.IsAir)
            state.Output.WriteLine("You realize, just in time, that disembarking here would probably be fatal.");
        else
        {
            state.Output.WriteLine("You are on your own feet again.");
            state.Winner.Vehicle = null;
        }
    }

    // act1.38 YUKS: TAKE of something that can't be taken and has nothing to say about it.
    private static readonly string[] Yuks = ["Nice try.", "You can't be serious.", "Chomp, Chomp.", "Not a prayer.", "I don't think so."];

    /// <summary>
    /// rooms.99 TAKE. The object's own action is asked first (APPLY-OBJECT) and it is taken only if the action lets
    /// it be; it goes to the head of your possessions. Returns false when TAKE failed.
    /// </summary>
    private static bool DoTake(GameState state, GameObject obj, bool tell = true)
    {
        if ((obj.Flags & ObjectFlags.NoCheck) != 0) return ObjectActions.ObjectAction(state, "TAKE", obj, null);
        var container = obj.Container;
        var getter = false;
        if (container != null)
        {
            if (!container.IsVisible || !container.IsOpenOrTransparent) { state.Output.WriteLine("I can't see one here."); return false; }
            if (!container.Open) { state.Output.WriteLine("I can't reach that."); return false; }
            getter = true;
        }
        if (obj == state.Winner.Vehicle) { state.Output.WriteLine("You are in it, loser!"); return false; }
        if (!obj.CanTake)
        {
            if (!ObjectActions.Try(state, obj, "TAKE", null)) state.Output.WriteLine(Yuks[state.Random.Next(Yuks.Length)]);
            return false;
        }
        if (getter || state.Here.Objects.Contains(obj))
        {
            var fromCarried = getter && container!.Holder == state.Winner;
            if (!fromCarried && GameObject.Weight(state.Winner.Inventory) + GameObject.Weight(obj.Contents) + obj.Size > Adventurer.MaxLoad)
            {
                state.Output.WriteLine("Your load is too heavy.  You will have to leave something behind.");
                return false;
            }
            if (ObjectActions.Try(state, obj, "TAKE", null)) return true;
            obj.MoveTo(state.Winner, first: true);
            // OTOUCH? is set on every take; SCORE-OBJ scores the find only the first time.
            if (!obj.Touched) state.Winner.Score += obj.FindScore;
            obj.Touched = true;
            if (tell) state.Output.WriteLine("Taken.");
            return true;
        }
        if (obj.Carrier == state.Winner) { state.Output.WriteLine("You already have it."); return false; }
        state.Output.WriteLine("I can't see one here.");
        return false;
    }

    /// <summary>
    /// rooms.99 DROPPER, for DROP, THROW, GIVE and POUR: what you carry (or hold in an open container) lands in the
    /// room, or in the vehicle you're in; then OBJECT-ACTION (the target first: the troll catches it, the glacier
    /// melts), and "Dropped." or "Thrown." if nothing answered. DROP or POUR into something is PUT.
    /// </summary>
    private static void Dropper(GameState state, string verb, GameObject obj, GameObject? indirect)
    {
        if (verb is "DROP" or "POUR" && indirect != null) { DoPut(state, obj, indirect); return; }
        bool Held(GameObject o) => o.Carrier == state.Winner || o.Container?.Carrier == state.Winner;
        if (indirect != null && !Held(obj)) (obj, indirect) = (indirect, obj);
        state.LastDirectObject = obj;
        state.LastIndirectObject = indirect;
        if ((obj.Flags & ObjectFlags.NoCheck) != 0) { ObjectActions.ObjectAction(state, verb, obj, indirect); return; }
        var getter = false;
        if (obj.Container is { } container && container.Carrier == state.Winner)
        {
            if (container.Open) getter = true;
            else if (container.IsTransparent) { state.Output.WriteLine("I can't reach that."); return; }
            else state.Output.WriteLine("I can't see that here.");
        }
        if (!getter && obj.Carrier != state.Winner) { state.Output.WriteLine("You are not carrying that."); return; }
        // (In a vehicle DROPPER calls PUTTER to put it there; the port just puts it there.)
        if (state.Winner.Vehicle is { } vehicle) obj.MoveTo(vehicle);
        else obj.MoveTo(state.Here);
        if (ObjectActions.ObjectAction(state, verb, obj, indirect)) return;
        if (verb == "DROP") state.Output.WriteLine("Dropped.");
        else if (verb == "THROW") state.Output.WriteLine("Thrown.");
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


    private static void DoWave(GameState state, GameObject obj)
    {
        if (obj.Carrier != state.Winner) { state.Output.WriteLine("You're not carrying that."); return; }
        if (ObjectActions.Try(state, obj, "WAVE", null)) return;
        state.Output.WriteLine("Nothing happens.");
    }

    private static void DoAttack(GameState state, GameObject? obj, GameObject? withObj)
    {
        state.LastDirectObject = obj;
        state.LastIndirectObject = withObj;
        // OBJECT-ACTION asks the weapon too: the rusty knife turns on its owner.
        if (withObj?.Id == Obj.Rknif) { ObjectActions.RustyKnife(state, "ATTACK", null); return; }
        if (obj == null) { state.Output.WriteLine("What do you want to attack?"); return; }
        withObj ??= state.Winner.Inventory.FirstOrDefault(o => (o.Flags & ObjectFlags.Weapon) != 0);
        if (obj.Id is Obj.Troll or Obj.Thief && obj.InRoom == state.Here)
        {
            Combat.Attack(state, obj, withObj);
            return;
        }
        if (ObjectActions.Try(state, obj, "ATTACK", withObj)) return;
        state.Output.WriteLine("Violence isn't the answer to this one.");
    }

    private static void DoMove(GameState state, GameObject obj)
    {
        state.LastDirectObject = obj;
        if (ObjectActions.Try(state, obj, "MOVE", null)) return;
        state.Output.WriteLine("You can't move that.");
    }

    private static void DoLift(GameState state, GameObject obj)
    {
        state.LastDirectObject = obj;
        if (ObjectActions.Try(state, obj, "LIFT", null)) return;
        state.Output.WriteLine("You can't lift that.");
    }

    /// <summary>
    /// rooms.99 EVERYTHING / VALUABLES and FROB-LOTS: TAKE, DROP or PUT each object in turn ("lamp:" and then what
    /// happened), stopping if one of them moves you.
    /// </summary>
    private static void DoLots(GameState state, string action, GameObject which, GameObject? into)
    {
        var valuablesOnly = which.Id == Obj.Valua;
        bool Counts(GameObject o) => !valuablesOnly || o.TrophyScore != 0;
        bool InRoom(GameObject o) => o.IsVisible && !o.IsActor && !o.IsGlobal && (o.Flags & ObjectFlags.NoDescribe) == 0 && Counts(o);
        List<GameObject> lots = action switch
        {
            "TAKE" => state.Here.Objects.Where(InRoom).Where(o => o.CanTake || (o.Flags & ObjectFlags.TryTake) != 0).ToList(), // FROB-LOTS
            "DROP" => state.Winner.Inventory.Where(Counts).ToList(),
            _ => state.Here.Objects.Where(InRoom).Concat(state.Winner.Inventory.Where(Counts)).Where(o => o != into).ToList(),
        };
        if (lots.Count == 0)
        {
            state.Output.WriteLine("I couldn't find anything.");
            return;
        }
        var here = state.Here;
        foreach (var o in lots)
        {
            state.Output.Write(o.DescriptionShort + ":" + Environment.NewLine + "  ");
            state.LastDirectObject = o;
            state.LastIndirectObject = into;
            switch (action)
            {
                case "TAKE": DoTake(state, o); break;
                case "DROP": Dropper(state, "DROP", o, null); break;
                default: DoPut(state, o, into); break;
            }
            if (state.Here != here) break;
        }
    }

    /// <summary>rooms.99 PUTTER: PUT obj IN container (anything open, or a container or vehicle).</summary>
    private static void DoPut(GameState state, GameObject? obj, GameObject? cont)
    {
        if (obj == null) { state.Output.WriteLine("What do you want to put?"); return; }
        if (cont == null) { state.Output.WriteLine("What do you want to put that in?"); return; }
        state.LastDirectObject = obj;
        state.LastIndirectObject = cont;
        if (Balloon.ReadIn(state, "PUT", obj, cont, null)) return;
        if (obj.IsGlobal || cont.IsGlobal) { state.Output.WriteLine("Nice try."); return; }
        if (!cont.Open && !cont.IsContainer && (cont.Flags & ObjectFlags.Vehicle) == 0) { state.Output.WriteLine("I can't do that."); return; }
        if (!cont.Open) { state.Output.WriteLine("I can't reach inside."); return; }
        if (cont == obj) { state.Output.WriteLine("How can you do that?"); return; }
        if (GameObject.Weight(cont.Contents) + obj.Size > cont.Capacity) { state.Output.WriteLine("It won't fit."); return; }
        if (obj.Carrier != state.Winner)
        {
            // PUTTER takes it first, without saying so.
            if (!DoTake(state, obj, tell: false)) return;
        }
        state.LastDirectObject = obj;
        state.LastIndirectObject = cont;
        if (ObjectActions.ObjectAction(state, "PUT", obj, cont)) return;
        obj.MoveTo(cont, first: true);
        if (cont.Id == Obj.Tcase && obj.TrophyScore > 0)
            state.Winner.Score += obj.TrophyScore;
        state.Output.WriteLine("Done.");
    }

    /// <summary>
    /// act1.38 BURNER: BURN obj WITH something aflame (FLAMING?: FLAMEBIT and lit). The objects' actions come first,
    /// then the balloon's receptacle; anything burnable burns, and kills you if you're holding it.
    /// </summary>
    private static void DoBurn(GameState state, GameObject obj, GameObject? flame)
    {
        flame ??= Gwim(state, ObjectFlags.OnFire, noTake: false);
        if (flame == null) { state.Output.WriteLine("With what?"); return; } // np.93 ORTELL
        state.LastDirectObject = obj;
        state.LastIndirectObject = flame;
        if ((flame.Flags & ObjectFlags.OnFire) == 0 || flame.LightAmount != 1)
        {
            state.Output.WriteLine("With a " + flame.DescriptionShort + "??!?");
            return;
        }
        if (ObjectActions.ObjectAction(state, "BURN", obj, flame)) return;
        if (state.Winner.Vehicle?.Id == Obj.Ballo && obj.Container?.Id == Obj.Recep)
        {
            Balloon.Burn(state, obj); // act2.27 BALLOON, for BURN
            return;
        }
        if ((obj.Flags & ObjectFlags.Burnable) == 0)
            state.Output.WriteLine("I don't think you can burn a " + obj.DescriptionShort + ".");
        else if (obj.Carrier == state.Winner)
        {
            state.Output.WriteLine("The " + obj.DescriptionShort + " catches fire.");
            state.JigsUp("Unfortunately, you were holding it at the time.");
        }
        else if (Hackable(state, obj))
        {
            state.Output.WriteLine("The " + obj.DescriptionShort + " catches fire and is consumed.");
            obj.MoveTo(null);
        }
        else
            state.Output.WriteLine("You don't have that.");
    }

    /// <summary>act1.38 HACKABLE?: on the floor here, or in the vehicle you're in.</summary>
    private static bool Hackable(GameState state, GameObject obj) =>
        state.Winner.Vehicle is { } vehicle ? obj.Container == vehicle : obj.InRoom == state.Here;

    /// <summary>
    /// np.93 GWIM for a missing object: the one visible object with <paramref name="bit"/> you carry, or else the one
    /// here (which you pick up first, unless <paramref name="noTake"/>). Two candidates, or none, find nothing.
    /// </summary>
    private static GameObject? Gwim(GameState state, ObjectFlags bit, bool noTake)
    {
        var carried = Fwim(state.Winner.Inventory, bit, noTake);
        var here = GameState.IsLit(state) ? Fwim(state.Here.Objects, bit, noTake) : [];
        if (here.Count == 0) return carried.Count == 1 ? carried[0] : null;
        if (here.Count > 1 || carried.Count > 0) return null;
        return noTake || DoTake(state, here[0], tell: false) ? here[0] : null;
    }

    /// <summary>np.93 FWIM: visible objects with the bit (takeable ones unless no-take), and those in open containers.</summary>
    private static List<GameObject> Fwim(IEnumerable<GameObject> objects, ObjectFlags bit, bool noTake)
    {
        var found = new List<GameObject>();
        foreach (var o in objects)
        {
            if (o.IsVisible && (noTake || o.CanTake) && (o.Flags & bit) != 0) found.Add(o);
            if (o.IsVisible && o.Open)
                found.AddRange(o.Contents.Where(c => c.IsVisible && (c.Flags & bit) != 0));
        }
        return found;
    }

    private static void DoFill(GameState state, GameObject? obj)
    {
        if (obj == null) { state.Output.WriteLine("What do you want to fill?"); return; }
        var fillableRooms = new[] { "RESES", "STREA" };
        if (!fillableRooms.Contains(state.Here.Id)) { state.Output.WriteLine("I can't find any water here."); return; }
        var bottle = state.World.FindObject(Obj.Bottl);
        if (obj != bottle) { state.Output.WriteLine("You can only fill the bottle with water."); return; }
        if (bottle == null || !state.Winner.Inventory.Contains(bottle)) { state.Output.WriteLine("You're not carrying the bottle."); return; }
        if (!bottle.Open) { state.Output.WriteLine("The bottle is closed."); return; }
        if (bottle.Contents.Count > 0) { state.Output.WriteLine("The bottle is already full."); return; }
        var water = state.World.FindObject(Obj.Water);
        if (water == null) { state.Output.WriteLine("I can't find any water here."); return; }
        water.MoveTo(bottle);
        state.Output.WriteLine("The bottle is now full of water.");
    }

    /// <summary>act1.38 TIE: only something that can be tied (TIEBIT), and then only where an action allows it.</summary>
    private static void DoTie(GameState state, GameObject? obj, GameObject? toObj)
    {
        if (obj == null) { state.Output.WriteLine("What do you want to tie?"); return; }
        state.LastDirectObject = obj;
        state.LastIndirectObject = toObj;
        if ((obj.Flags & ObjectFlags.Tie) == 0) state.Output.WriteLine("How can you tie that to anything.");
        else if (!ObjectActions.ObjectAction(state, "TIE", obj, toObj)) state.Output.WriteLine("You can't tie it to that.");
    }
}
