namespace ZorkDotNet.Game;

/// <summary>Flags the port keeps that the original has no MGVALS entry for.</summary>
public static partial class Flag
{
    /// <summary>The rug in the living room has been moved (act1.38 RUG keeps this in the rug's ORAND).</summary>
    public const string RugMoved = "RUG-MOVED!-FLAG";

    /// <summary>
    /// The flag of dung.56's magic exits (MAGCMACH, MAGALICE: #CEXIT {"FROBOZZ" ...}). It is not in MGVALS and
    /// nothing sets it, so those exits never open.
    /// </summary>
    public const string Frobozz = "FROBOZZ!-FLAG";
}
