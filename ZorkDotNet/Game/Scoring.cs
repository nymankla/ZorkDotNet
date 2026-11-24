namespace ZorkDotNet.Game;

/// <summary>
/// Score report, ranks and quitting: rooms.99 SCORE / FINISH, defs.63 YES/NO, makstr.7 and act2.27 SCORE-MAX.
/// </summary>
public static class Scoring
{
    /// <summary>act2.27 LIGHT-SHAFT: points for reaching the bottom of the shaft (NO-OBJS), counted in SCORE-MAX.</summary>
    public const int LightShaftPoints = 10;

    /// <summary>
    /// SCORE-MAX: the original adds every room's RVAL and every object's OFVAL + OTVAL as the dungeon is
    /// built (makstr.7 ROOM / OBJECT), plus LIGHT-SHAFT.
    /// </summary>
    public static int ComputeMaxScore(IEnumerable<Room> rooms, IEnumerable<GameObject> objects) =>
        rooms.Sum(r => r.VisitScore) + objects.Sum(o => o.FindScore + o.TrophyScore) + LightShaftPoints;

    /// <summary>rooms.99 SCORE rank table, by fraction of SCORE-MAX.</summary>
    public static string Rank(int score, int maxScore)
    {
        var pct = maxScore > 0 ? (double)score / maxScore : 0;
        return pct switch
        {
            1.0 => "Cheater",
            > 0.95 => "Wizard",
            > 0.89999999 => "Master",
            > 0.79999999 => "Winner",
            > 0.60 => "Hacker",
            > 0.39999999 => "Adventurer",
            > 0.19999999 => "Junior Adventurer",
            > 0.09999999 => "Novice Adventurer",
            > 0.04999999 => "Amateur Adventurer",
            _ => "Beginner",
        };
    }

    /// <summary>
    /// rooms.99 SCORE. <paramref name="ask"/> is true for the SCORE and QUIT commands ("would be") and false
    /// when the game ends by itself ("is").
    /// </summary>
    public static void PrintScore(GameState state, bool ask)
    {
        var score = state.Winner.Score;
        var moves = state.Winner.Moves;
        state.Output.WriteLine();
        state.Output.WriteLine((ask ? "Your score would be " : "Your score is ") + score
            + " [total of " + state.MaxScore + " points], in " + moves + (moves == 1 ? " move." : " moves."));
        state.Output.WriteLine("This score gives you the rank of " + Rank(score, state.MaxScore) + ".");
    }

    /// <summary>rooms.99 FINISH for QUIT: show the score and ask; the next input line is the answer.</summary>
    public static void Quit(GameState state)
    {
        PrintScore(state, ask: true);
        state.Output.WriteLine("Do you wish to leave the game? (Y is affirmative): ");
        state.AwaitingQuitConfirmation = true;
    }

    /// <summary>defs.63 YES/NO: an answer starting with T or Y (any case) is yes; anything else keeps playing.</summary>
    public static void AnswerQuit(GameState state, string answer)
    {
        state.AwaitingQuitConfirmation = false;
        if (answer.Length > 0 && "TtYy".Contains(answer[0], StringComparison.Ordinal))
            state.Running = false;
    }
}
