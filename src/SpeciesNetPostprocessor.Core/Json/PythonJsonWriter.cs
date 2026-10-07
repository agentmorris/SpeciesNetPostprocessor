using System.Globalization;
using System.Numerics;

namespace SpeciesNetPostprocessor.Core.Json;

/// <summary>
/// Writes a JsonObject-based DOM in exactly the format produced by Python's
/// json.dump(content, f, indent=1, ensure_ascii=True), which is what MegaDetector's
/// write_json() uses, so output files are byte-for-byte comparable with files written
/// by the MegaDetector Python package.
/// </summary>
public sealed class PythonJsonWriter : IDisposable
{
    private const int BufferSize = 1 << 20;
    private const string HexDigits = "0123456789abcdef";

    private readonly Stream _stream;
    private readonly int _indent;
    private readonly byte[] _buffer = new byte[BufferSize];
    private int _position;

    // Called with (index, count) for each element of arrays that are direct children of the root
    private readonly Action<int, int>? _topLevelArrayProgress;
    private readonly CancellationToken _cancellationToken;

    private PythonJsonWriter(Stream stream, int indent, Action<int, int>? topLevelArrayProgress,
                             CancellationToken cancellationToken)
    {
        _stream = stream;
        _indent = indent;
        _topLevelArrayProgress = topLevelArrayProgress;
        _cancellationToken = cancellationToken;
    }

    /// <summary>
    /// Writes [root] to [path], creating the parent folder if necessary.
    /// </summary>
    public static void WriteFile(string path,
                                 object? root,
                                 int indent = 1,
                                 Action<int, int>? topLevelArrayProgress = null,
                                 CancellationToken cancellationToken = default)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
        using var stream = new FileStream(path, FileMode.Create, FileAccess.Write, FileShare.None, 1 << 16);
        using var writer = new PythonJsonWriter(stream, indent, topLevelArrayProgress, cancellationToken);
        writer.WriteValue(root, 0);
        writer.Flush();
    }

    /// <summary>
    /// Serializes [root] to a string.
    /// </summary>
    public static string Serialize(object? root, int indent = 1)
    {
        using var stream = new MemoryStream();
        using (var writer = new PythonJsonWriter(stream, indent, null, CancellationToken.None))
        {
            writer.WriteValue(root, 0);
            writer.Flush();
        }
        return System.Text.Encoding.ASCII.GetString(stream.ToArray());
    }

    public void Dispose()
    {
        Flush();
    }

    private void Flush()
    {
        if (_position > 0)
        {
            _stream.Write(_buffer, 0, _position);
            _position = 0;
        }
    }

    private void EnsureCapacity(int n)
    {
        if (_position + n > BufferSize)
        {
            Flush();
        }
    }

    private void WriteByte(byte b)
    {
        if (_position == BufferSize)
        {
            Flush();
        }
        _buffer[_position++] = b;
    }

    private void WriteAscii(string s)
    {
        foreach (char c in s)
        {
            WriteByte((byte)c);
        }
    }

    private void WriteNewlineAndIndent(int level)
    {
        WriteByte((byte)'\n');
        int n = level * _indent;
        for (int i = 0; i < n; i++)
        {
            WriteByte((byte)' ');
        }
    }

    private void WriteValue(object? value, int level)
    {
        switch (value)
        {
            case null:
                WriteAscii("null");
                break;
            case bool b:
                WriteAscii(b ? "true" : "false");
                break;
            case string s:
                WriteString(s);
                break;
            case long l:
                WriteAscii(l.ToString(CultureInfo.InvariantCulture));
                break;
            case int i:
                WriteAscii(i.ToString(CultureInfo.InvariantCulture));
                break;
            case double d:
                WriteAscii(PythonFloat.Repr(d));
                break;
            case BigInteger bi:
                WriteAscii(bi.ToString(CultureInfo.InvariantCulture));
                break;
            case JsonObject obj:
                WriteObject(obj, level);
                break;
            case List<object?> list:
                WriteList(list, level);
                break;
            default:
                throw new InvalidOperationException($"Cannot serialize value of type {value.GetType()}");
        }
    }

    private void WriteObject(JsonObject obj, int level)
    {
        if (obj.Count == 0)
        {
            WriteAscii("{}");
            return;
        }
        WriteByte((byte)'{');
        bool first = true;
        foreach (var kvp in obj)
        {
            if (!first)
            {
                WriteByte((byte)',');
            }
            first = false;
            WriteNewlineAndIndent(level + 1);
            WriteString(kvp.Key);
            WriteByte((byte)':');
            WriteByte((byte)' ');
            WriteValue(kvp.Value, level + 1);
        }
        WriteNewlineAndIndent(level);
        WriteByte((byte)'}');
    }

    private void WriteList(List<object?> list, int level)
    {
        if (list.Count == 0)
        {
            WriteAscii("[]");
            return;
        }
        bool reportProgress = (level == 1) && (_topLevelArrayProgress != null);
        WriteByte((byte)'[');
        for (int i = 0; i < list.Count; i++)
        {
            if (i > 0)
            {
                WriteByte((byte)',');
            }
            WriteNewlineAndIndent(level + 1);
            WriteValue(list[i], level + 1);
            if (reportProgress && (i % 1000 == 0))
            {
                _topLevelArrayProgress!(i, list.Count);
                _cancellationToken.ThrowIfCancellationRequested();
            }
        }
        if (reportProgress)
        {
            _topLevelArrayProgress!(list.Count, list.Count);
        }
        WriteNewlineAndIndent(level);
        WriteByte((byte)']');
    }

    /// <summary>
    /// Writes a quoted string with the escaping rules of Python's
    /// json.encoder.c_encode_basestring_ascii.
    /// </summary>
    private void WriteString(string s)
    {
        EnsureCapacity(2);
        WriteByte((byte)'"');
        foreach (char c in s)
        {
            if (c >= ' ' && c <= '~' && c != '\\' && c != '"')
            {
                WriteByte((byte)c);
                continue;
            }
            WriteByte((byte)'\\');
            switch (c)
            {
                case '\\': WriteByte((byte)'\\'); break;
                case '"': WriteByte((byte)'"'); break;
                case '\b': WriteByte((byte)'b'); break;
                case '\f': WriteByte((byte)'f'); break;
                case '\n': WriteByte((byte)'n'); break;
                case '\r': WriteByte((byte)'r'); break;
                case '\t': WriteByte((byte)'t'); break;
                default:
                    // Characters outside the BMP are already surrogate pairs in .NET strings,
                    // which matches Python's output for those characters.
                    WriteByte((byte)'u');
                    WriteByte((byte)HexDigits[(c >> 12) & 0xF]);
                    WriteByte((byte)HexDigits[(c >> 8) & 0xF]);
                    WriteByte((byte)HexDigits[(c >> 4) & 0xF]);
                    WriteByte((byte)HexDigits[c & 0xF]);
                    break;
            }
        }
        WriteByte((byte)'"');
    }
}
