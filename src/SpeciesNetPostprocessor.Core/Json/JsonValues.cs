using System.Numerics;

namespace SpeciesNetPostprocessor.Core.Json;

/// <summary>
/// Helpers for working with values in the JsonObject-based DOM.
/// </summary>
public static class JsonValues
{
    public static bool IsNumber(object? v) => v is double or long or int or BigInteger;

    public static double ToDouble(object? v) => v switch
    {
        double d => d,
        long l => l,
        int i => i,
        BigInteger b => (double)b,
        _ => throw new InvalidCastException($"Expected a number, found {TypeName(v)}")
    };

    /// <summary>
    /// Returns a Python-style type name for [v], for error messages.
    /// </summary>
    public static string TypeName(object? v) => v switch
    {
        null => "null",
        bool => "bool",
        string => "string",
        double => "float",
        long or int or BigInteger => "int",
        JsonObject => "dict",
        List<object?> => "list",
        _ => v.GetType().Name
    };

    /// <summary>
    /// Returns the "detections" list for an image, or null if the field is missing or null
    /// (or isn't a list).
    /// </summary>
    public static List<object?>? GetDetections(JsonObject im) => im.Get("detections") as List<object?>;

    /// <summary>
    /// Returns the "classifications" list for a detection, or null if the field is missing or null.
    /// </summary>
    public static List<object?>? GetClassifications(JsonObject det) => det.Get("classifications") as List<object?>;

    /// <summary>
    /// Returns the images list from a results dict.
    /// </summary>
    public static List<object?> GetImages(JsonObject d) => (List<object?>)d["images"]!;

    /// <summary>
    /// Returns a string-to-string dict (e.g. "detection_categories") as an ordered dictionary.
    /// </summary>
    public static OrderedDictionary<string, string> GetStringMap(JsonObject d, string key)
    {
        var result = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        if (d.Get(key) is JsonObject obj)
        {
            foreach (var kvp in obj)
            {
                result[kvp.Key] = (string)kvp.Value!;
            }
        }
        return result;
    }

    /// <summary>
    /// Converts an ordered string-to-string dictionary to a JsonObject.
    /// </summary>
    public static JsonObject ToJsonObject(IEnumerable<KeyValuePair<string, string>> map)
    {
        var obj = new JsonObject();
        foreach (var kvp in map)
        {
            obj.Set(kvp.Key, kvp.Value);
        }
        return obj;
    }
}
