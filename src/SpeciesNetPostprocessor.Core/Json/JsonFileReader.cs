using System.Globalization;
using System.Numerics;
using System.Text;
using System.Text.Json;

namespace SpeciesNetPostprocessor.Core.Json;

/// <summary>
/// Streaming JSON parser that builds a JsonObject-based DOM, following the conventions of
/// Python's json.load(): numbers with a fraction or exponent become doubles, other numbers
/// become integers, and duplicate keys keep the position of the first occurrence and the
/// value of the last occurrence.
/// </summary>
/// <remarks>
/// Files are read in chunks, so this works for files larger than 2GB.  Repeated small values
/// (keys, category IDs, confidence values) are shared rather than allocated per occurrence,
/// which matters for results files with millions of detections.
/// </remarks>
public static class JsonFileReader
{
    private const int InitialBufferSize = 1 << 20;

    /// <summary>
    /// Loads the JSON file [path].  [progress] is called periodically with
    /// (bytes read, total bytes).
    /// </summary>
    public static object? Load(string path,
                               Action<long, long>? progress = null,
                               CancellationToken cancellationToken = default)
    {
        // Allow other processes to have the file open for writing (e.g. a tool that still has its
        // output file open); this is also how Python opens files on Windows.
        using var stream = new FileStream(path, FileMode.Open, FileAccess.Read,
                                          FileShare.ReadWrite | FileShare.Delete,
                                          1 << 16, FileOptions.SequentialScan);
        return Load(stream, stream.Length, progress, cancellationToken);
    }

    /// <summary>
    /// Parses a JSON string.
    /// </summary>
    public static object? Parse(string json)
    {
        using var stream = new MemoryStream(Encoding.UTF8.GetBytes(json));
        return Load(stream, stream.Length, null, CancellationToken.None);
    }

    private static object? Load(Stream stream,
                                long totalLength,
                                Action<long, long>? progress,
                                CancellationToken cancellationToken)
    {
        var builder = new DomBuilder();
        byte[] buffer = new byte[InitialBufferSize];
        int dataLength = 0;
        bool isFinalBlock = false;
        bool isFirstBlock = true;
        long totalConsumed = 0;
        var state = new JsonReaderState(new JsonReaderOptions { MaxDepth = 256 });

        while (true)
        {
            while (!isFinalBlock && dataLength < buffer.Length)
            {
                int n = stream.Read(buffer, dataLength, buffer.Length - dataLength);
                if (n == 0)
                {
                    isFinalBlock = true;
                    break;
                }
                dataLength += n;
            }

            // Skip a UTF-8 byte order mark if present
            int start = 0;
            if (isFirstBlock)
            {
                isFirstBlock = false;
                if (dataLength >= 3 && buffer[0] == 0xEF && buffer[1] == 0xBB && buffer[2] == 0xBF)
                {
                    start = 3;
                }
            }

            var reader = new Utf8JsonReader(buffer.AsSpan(start, dataLength - start), isFinalBlock, state);
            while (reader.Read())
            {
                builder.HandleToken(ref reader);
            }
            state = reader.CurrentState;

            int consumed = start + (int)reader.BytesConsumed;
            totalConsumed += consumed;

            if (isFinalBlock)
            {
                break;
            }

            int remaining = dataLength - consumed;

            // If we couldn't make progress, a single token is larger than the buffer
            if (consumed == 0 && remaining == buffer.Length)
            {
                byte[] larger = new byte[buffer.Length * 2];
                Buffer.BlockCopy(buffer, 0, larger, 0, remaining);
                buffer = larger;
            }
            else if (remaining > 0)
            {
                Buffer.BlockCopy(buffer, consumed, buffer, 0, remaining);
            }
            dataLength = remaining;

            progress?.Invoke(totalConsumed, totalLength);
            cancellationToken.ThrowIfCancellationRequested();
        }

        progress?.Invoke(totalLength, totalLength);

        if (!builder.HasResult)
        {
            throw new JsonException("No JSON content found");
        }
        return builder.Result;
    }

    private sealed class Frame
    {
        public bool IsObject;
        public readonly List<string> Keys = new();
        public readonly List<object?> Values = new();
        public string? PendingKey;

        // Only used for objects with many keys, to detect duplicates efficiently
        public Dictionary<string, int>? KeyIndex;

        public void Reset(bool isObject)
        {
            IsObject = isObject;
            Keys.Clear();
            Values.Clear();
            PendingKey = null;
            KeyIndex = null;
        }
    }

    private sealed class DomBuilder
    {
        private const int MaxCachedStringLength = 12;
        private const int MaxCacheEntries = 1 << 17;

        private static readonly object BoxedTrue = true;
        private static readonly object BoxedFalse = false;

        private readonly List<Frame> _frames = new();
        private int _depth;

        private readonly Dictionary<string, string> _stringCache = new(StringComparer.Ordinal);
        private readonly Dictionary<long, object> _doubleCache = new();
        private readonly Dictionary<long, object> _longCache = new();

        public object? Result { get; private set; }
        public bool HasResult { get; private set; }

        public void HandleToken(ref Utf8JsonReader reader)
        {
            switch (reader.TokenType)
            {
                case JsonTokenType.StartObject:
                    PushFrame(isObject: true);
                    break;
                case JsonTokenType.StartArray:
                    PushFrame(isObject: false);
                    break;
                case JsonTokenType.EndObject:
                {
                    Frame frame = _frames[--_depth];
                    var obj = new JsonObject(frame.Keys.ToArray(), frame.Values.ToArray());
                    frame.Reset(true);
                    AddValue(obj);
                    break;
                }
                case JsonTokenType.EndArray:
                {
                    Frame frame = _frames[--_depth];
                    var list = new List<object?>(frame.Values);
                    frame.Reset(false);
                    AddValue(list);
                    break;
                }
                case JsonTokenType.PropertyName:
                    _frames[_depth - 1].PendingKey = CacheString(reader.GetString()!, force: true);
                    break;
                case JsonTokenType.String:
                    AddValue(CacheString(reader.GetString()!, force: false));
                    break;
                case JsonTokenType.Number:
                    AddValue(ParseNumber(reader.ValueSpan));
                    break;
                case JsonTokenType.True:
                    AddValue(BoxedTrue);
                    break;
                case JsonTokenType.False:
                    AddValue(BoxedFalse);
                    break;
                case JsonTokenType.Null:
                    AddValue(null);
                    break;
                default:
                    throw new JsonException($"Unexpected JSON token {reader.TokenType}");
            }
        }

        private void PushFrame(bool isObject)
        {
            if (_depth == _frames.Count)
            {
                _frames.Add(new Frame());
            }
            _frames[_depth].Reset(isObject);
            _depth++;
        }

        private void AddValue(object? value)
        {
            if (_depth == 0)
            {
                Result = value;
                HasResult = true;
                return;
            }

            Frame frame = _frames[_depth - 1];
            if (!frame.IsObject)
            {
                frame.Values.Add(value);
                return;
            }

            string key = frame.PendingKey!;
            frame.PendingKey = null;

            // Python dicts keep the first position and the last value for duplicate keys
            int existingIndex = FindKey(frame, key);
            if (existingIndex >= 0)
            {
                frame.Values[existingIndex] = value;
                return;
            }

            frame.Keys.Add(key);
            frame.Values.Add(value);
            frame.KeyIndex?.Add(key, frame.Keys.Count - 1);
        }

        private static int FindKey(Frame frame, string key)
        {
            if (frame.KeyIndex != null)
            {
                return frame.KeyIndex.TryGetValue(key, out int i) ? i : -1;
            }
            List<string> keys = frame.Keys;
            for (int i = 0; i < keys.Count; i++)
            {
                if (string.Equals(keys[i], key, StringComparison.Ordinal))
                {
                    return i;
                }
            }
            if (keys.Count >= 16)
            {
                frame.KeyIndex = new Dictionary<string, int>(StringComparer.Ordinal);
                for (int i = 0; i < keys.Count; i++)
                {
                    frame.KeyIndex[keys[i]] = i;
                }
            }
            return -1;
        }

        private string CacheString(string s, bool force)
        {
            if (!force && s.Length > MaxCachedStringLength)
            {
                return s;
            }
            if (_stringCache.TryGetValue(s, out string? cached))
            {
                return cached;
            }
            if (_stringCache.Count < MaxCacheEntries)
            {
                _stringCache[s] = s;
            }
            return s;
        }

        private object ParseNumber(ReadOnlySpan<byte> span)
        {
            bool isFloat = span.IndexOfAny((byte)'.', (byte)'e', (byte)'E') >= 0;

            if (isFloat)
            {
                double d = double.Parse(span, NumberStyles.Float, CultureInfo.InvariantCulture);
                long bits = BitConverter.DoubleToInt64Bits(d);
                if (_doubleCache.TryGetValue(bits, out object? boxed))
                {
                    return boxed;
                }
                boxed = d;
                if (_doubleCache.Count < MaxCacheEntries)
                {
                    _doubleCache[bits] = boxed;
                }
                return boxed;
            }

            if (long.TryParse(span, NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out long l))
            {
                if (_longCache.TryGetValue(l, out object? boxed))
                {
                    return boxed;
                }
                boxed = l;
                if (_longCache.Count < MaxCacheEntries)
                {
                    _longCache[l] = boxed;
                }
                return boxed;
            }

            return BigInteger.Parse(Encoding.ASCII.GetString(span), NumberStyles.AllowLeadingSign,
                                    CultureInfo.InvariantCulture);
        }
    }
}
