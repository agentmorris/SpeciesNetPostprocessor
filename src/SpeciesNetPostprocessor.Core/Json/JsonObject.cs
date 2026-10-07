using System.Collections;

namespace SpeciesNetPostprocessor.Core.Json;

/// <summary>
/// An insertion-ordered JSON object with the same semantics as a Python dict loaded via
/// json.load(): assigning to an existing key keeps its position, assigning to a new key
/// appends it, and removing a key closes the gap.
/// </summary>
/// <remarks>
/// JSON values are represented as: null, bool, string, long, double, BigInteger (integers
/// that don't fit in a long), JsonObject, and List&lt;object?&gt; (arrays).
/// </remarks>
public sealed class JsonObject : IEnumerable<KeyValuePair<string, object?>>
{
    // Objects with more than this many entries get a hash index for lookups
    private const int IndexThreshold = 8;

    private string[] _keys;
    private object?[] _values;
    private int _count;
    private Dictionary<string, int>? _index;

    public JsonObject()
    {
        _keys = [];
        _values = [];
    }

    /// <summary>
    /// Creates an object that takes ownership of the supplied arrays, which must be
    /// the same length and must not contain duplicate keys.
    /// </summary>
    internal JsonObject(string[] keys, object?[] values)
    {
        _keys = keys;
        _values = values;
        _count = keys.Length;
        if (_count > IndexThreshold)
        {
            BuildIndex();
        }
    }

    public int Count => _count;

    /// <summary>
    /// The keys in this object, in insertion order (a copy).
    /// </summary>
    public List<string> Keys => new(_keys.AsSpan(0, _count).ToArray());

    public object? this[string key]
    {
        get
        {
            int i = IndexOf(key);
            if (i < 0)
            {
                throw new KeyNotFoundException($"Key \"{key}\" not found");
            }
            return _values[i];
        }
        set => Set(key, value);
    }

    public bool ContainsKey(string key) => IndexOf(key) >= 0;

    public bool TryGetValue(string key, out object? value)
    {
        int i = IndexOf(key);
        if (i < 0)
        {
            value = null;
            return false;
        }
        value = _values[i];
        return true;
    }

    /// <summary>
    /// Returns the value for [key], or null if [key] is not present (equivalent to
    /// Python's dict.get()).
    /// </summary>
    public object? Get(string key)
    {
        int i = IndexOf(key);
        return i < 0 ? null : _values[i];
    }

    public void Set(string key, object? value)
    {
        int i = IndexOf(key);
        if (i >= 0)
        {
            _values[i] = value;
            return;
        }
        if (_count == _keys.Length)
        {
            int newCapacity = Math.Max(4, _count * 2);
            Array.Resize(ref _keys, newCapacity);
            Array.Resize(ref _values, newCapacity);
        }
        _keys[_count] = key;
        _values[_count] = value;
        _count++;
        if (_index != null)
        {
            _index[key] = _count - 1;
        }
        else if (_count > IndexThreshold)
        {
            BuildIndex();
        }
    }

    public bool Remove(string key)
    {
        int i = IndexOf(key);
        if (i < 0)
        {
            return false;
        }
        Array.Copy(_keys, i + 1, _keys, i, _count - i - 1);
        Array.Copy(_values, i + 1, _values, i, _count - i - 1);
        _count--;
        _keys[_count] = null!;
        _values[_count] = null;
        if (_index != null)
        {
            BuildIndex();
        }
        return true;
    }

    public IEnumerator<KeyValuePair<string, object?>> GetEnumerator()
    {
        for (int i = 0; i < _count; i++)
        {
            yield return new KeyValuePair<string, object?>(_keys[i], _values[i]);
        }
    }

    IEnumerator IEnumerable.GetEnumerator() => GetEnumerator();

    private int IndexOf(string key)
    {
        if (_index != null)
        {
            return _index.TryGetValue(key, out int i) ? i : -1;
        }
        for (int i = 0; i < _count; i++)
        {
            if (string.Equals(_keys[i], key, StringComparison.Ordinal))
            {
                return i;
            }
        }
        return -1;
    }

    private void BuildIndex()
    {
        if (_count <= IndexThreshold)
        {
            _index = null;
            return;
        }
        _index = new Dictionary<string, int>(_count, StringComparer.Ordinal);
        for (int i = 0; i < _count; i++)
        {
            _index[_keys[i]] = i;
        }
    }
}
