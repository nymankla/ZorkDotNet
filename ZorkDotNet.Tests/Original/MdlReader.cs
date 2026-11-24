namespace ZorkDotNet.Tests.Original;

/// <summary>A parsed MDL value.</summary>
internal abstract record MdlNode;

/// <summary>"string" (backslash escapes the next character).</summary>
internal sealed record MdlString(string Value) : MdlNode;

/// <summary>An atom, including numbers such as -1.</summary>
internal sealed record MdlAtom(string Name) : MdlNode;

internal enum MdlSeqKind
{
    List,
    Vector,
    Brace,
    Form,
}

/// <summary>( ) list, [ ] vector, { } (used by #ROOM/#OBJECT in dung.56) or &lt; &gt; form.</summary>
internal sealed record MdlSeq(MdlSeqKind Kind, IReadOnlyList<MdlNode> Items) : MdlNode
{
    public string? Head => Items.Count > 0 && Items[0] is MdlAtom a ? a.Name : null;
}

/// <summary>#TYPE value, e.g. #ROOM {...}, #FIND-OBJ {"LAMP"}, #NEXIT "msg".</summary>
internal sealed record MdlHash(string Type, MdlNode Value) : MdlNode;

/// <summary>Prefixed value: % (read-time eval), ' (quote), , (global value), . (local value).</summary>
internal sealed record MdlPrefix(char Prefix, MdlNode Value) : MdlNode;

/// <summary>
/// Minimal reader for the MDL syntax used in dung.56. It doesn't evaluate anything; it only builds a tree.
/// </summary>
internal sealed class MdlReader
{
    private const string Closers = ")]}>";
    private readonly string _text;
    private int _pos;

    public MdlReader(string text) => _text = text;

    public static List<MdlNode> ReadAll(string text)
    {
        var reader = new MdlReader(text);
        var nodes = new List<MdlNode>();
        while (reader.Read() is { } node)
            nodes.Add(node);
        return nodes;
    }

    private char Peek(int offset = 0) => _pos + offset < _text.Length ? _text[_pos + offset] : '\0';

    private void SkipWhitespaceAndComments()
    {
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            if (char.IsWhiteSpace(c))
            {
                _pos++;
            }
            else if (c == ';')
            {
                // A comment is ";" followed by any object, which is discarded.
                _pos++;
                Read();
            }
            else
            {
                break;
            }
        }
    }

    private MdlNode? Read()
    {
        SkipWhitespaceAndComments();
        if (_pos >= _text.Length) return null;
        var c = _text[_pos];

        // "!" before a bracket is a segment marker (e.g. '![...!]); it doesn't change the structure.
        if (c == '!' && "([{<>)]}\"".Contains(Peek(1), StringComparison.Ordinal))
        {
            _pos++;
            return Read();
        }

        switch (c)
        {
            case '"':
                return ReadString();
            case '(':
                return ReadSeq(MdlSeqKind.List, ')');
            case '[':
                return ReadSeq(MdlSeqKind.Vector, ']');
            case '{':
                return ReadSeq(MdlSeqKind.Brace, '}');
            case '<':
                return ReadSeq(MdlSeqKind.Form, '>');
            case '#':
                _pos++;
                var type = ReadAtomName();
                return new MdlHash(type, Read() ?? throw Error("value expected after #" + type));
            case '%' or '\'' or ',' or '.':
                _pos++;
                return new MdlPrefix(c, Read() ?? throw Error("value expected after " + c));
        }

        if (Closers.Contains(c, StringComparison.Ordinal))
            throw Error("unexpected '" + c + "'");
        return new MdlAtom(ReadAtomName());
    }

    private MdlString ReadString()
    {
        _pos++;
        var sb = new System.Text.StringBuilder();
        while (Peek() != '"')
        {
            if (_pos >= _text.Length) throw Error("unterminated string");
            if (_text[_pos] == '\\') _pos++;
            sb.Append(_text[_pos]);
            _pos++;
        }
        _pos++;
        return new MdlString(sb.ToString());
    }

    private MdlSeq ReadSeq(MdlSeqKind kind, char close)
    {
        _pos++;
        var items = new List<MdlNode>();
        while (true)
        {
            SkipWhitespaceAndComments();
            if (Peek() == '!' && Closers.Contains(Peek(1), StringComparison.Ordinal)) _pos++;
            if (_pos >= _text.Length) throw Error("unterminated '" + close + "'");
            if (_text[_pos] == close)
            {
                _pos++;
                return new MdlSeq(kind, items);
            }
            items.Add(Read() ?? throw Error("unterminated '" + close + "'"));
        }
    }

    private string ReadAtomName()
    {
        var sb = new System.Text.StringBuilder();
        while (_pos < _text.Length)
        {
            var c = _text[_pos];
            if (c == '\\')
            {
                sb.Append(Peek(1));
                _pos += 2;
                continue;
            }
            if (char.IsWhiteSpace(c) || "()[]{}<>\";".Contains(c, StringComparison.Ordinal)) break;
            if (c == '!' && "()[]{}<>".Contains(Peek(1), StringComparison.Ordinal)) break;
            sb.Append(c);
            _pos++;
        }
        return sb.ToString();
    }

    private FormatException Error(string message)
    {
        var line = 1 + _text.AsSpan(0, Math.Min(_pos, _text.Length)).Count('\n');
        return new FormatException($"MDL parse error at line {line}: {message}");
    }
}
