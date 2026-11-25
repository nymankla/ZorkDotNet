namespace ZorkDotNet.Ui;

/// <summary>One screen of characters, each with a <see cref="Style"/>. The view draws into it; the renderer shows it.</summary>
public sealed class CellBuffer
{
    private readonly char[] _chars;
    private readonly Style[] _styles;

    public CellBuffer(int width, int height)
    {
        Width = Math.Max(0, width);
        Height = Math.Max(0, height);
        _chars = new char[Width * Height];
        _styles = new Style[Width * Height];
        Fill(0, 0, Width, Height, Style.Background);
    }

    public int Width { get; }
    public int Height { get; }

    public char CharAt(int x, int y) => _chars[(y * Width) + x];

    public Style StyleAt(int x, int y) => _styles[(y * Width) + x];

    /// <summary>Blanks a rectangle in <paramref name="style"/>.</summary>
    public void Fill(int x, int y, int width, int height, Style style)
    {
        for (var row = Math.Max(0, y); row < Math.Min(Height, y + height); row++)
            for (var col = Math.Max(0, x); col < Math.Min(Width, x + width); col++)
                Set(col, row, ' ', style);
    }

    /// <summary>Writes <paramref name="text"/> from (x, y), cut at <paramref name="maxWidth"/> and at the edge. Returns the x after it.</summary>
    public int Put(int x, int y, string text, Style style, int maxWidth = int.MaxValue)
    {
        if (y < 0 || y >= Height) return x;
        var end = Math.Min(Width, x + Math.Min(maxWidth, text.Length));
        for (var col = x; col < end; col++)
            if (col >= 0) Set(col, y, text[col - x], style);
        return end;
    }

    public void Set(int x, int y, char c, Style style)
    {
        if (x < 0 || y < 0 || x >= Width || y >= Height) return;
        _chars[(y * Width) + x] = c;
        _styles[(y * Width) + x] = style;
    }

    /// <summary>The characters of row <paramref name="y"/>, for tests.</summary>
    public string RowText(int y) => new(_chars, y * Width, Width);
}
