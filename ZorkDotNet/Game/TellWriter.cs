using System.Text;

namespace ZorkDotNet.Game;

/// <summary>
/// Passes output through and records whether anything was written: MDL TELL-FLAG, which rooms.99 RDCOM checks to
/// print "Nothing happens." after a command that said nothing.
/// </summary>
public sealed class TellWriter(TextWriter inner) : TextWriter
{
    public bool Told { get; private set; }

    public override Encoding Encoding => inner.Encoding;

    public override void Write(char value)
    {
        Told = true;
        inner.Write(value);
    }

    public override void Write(string? value)
    {
        Told = true;
        inner.Write(value);
    }

    public override void WriteLine(string? value)
    {
        Told = true;
        inner.WriteLine(value);
    }

    public override void WriteLine()
    {
        Told = true;
        inner.WriteLine();
    }

    public override void Flush() => inner.Flush();
}
