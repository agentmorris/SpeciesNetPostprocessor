using System.Globalization;
using SpeciesNetPostprocessor.Core.Json;
using SpeciesNetPostprocessor.Core.Taxonomy;

namespace SpeciesNetPostprocessor.Core;

/// <summary>
/// Validation of MD-formatted results files.
/// </summary>
public static class ResultsValidation
{
    private const int MaxErrorsToReport = 10;

    private static readonly string[] RequiredKeys = ["info", "images", "detection_categories"];
    private static readonly string[] TypicalKeys = ["classification_categories", "classification_category_descriptions"];
    private static readonly string[] VideoExtensions = [".mp4", ".avi", ".mpeg", ".mpg", ".mov", ".mkv", ".flv"];

    /// <summary>
    /// Checks that [root] has the structure that processing depends on, and throws a
    /// PostprocessingException describing the problem(s) if it doesn't.  This is deliberately
    /// less strict than ValidateBatchResults(); it only rejects files that can't be processed.
    /// </summary>
    public static JsonObject ValidateInputStructure(object? root, IPostprocessingLog log)
    {
        if (root is not JsonObject d)
        {
            throw new PostprocessingException("This doesn't look like a MegaDetector/SpeciesNet results file " +
                                              "(the file does not contain a JSON object)");
        }
        if (d.Get("images") is not List<object?> images)
        {
            throw new PostprocessingException("This doesn't look like a MegaDetector/SpeciesNet results file " +
                                              "(no \"images\" list)");
        }

        var errors = new List<string>();

        void CheckStringMap(string key, bool required)
        {
            object? value = d.Get(key);
            if (value == null)
            {
                if (required)
                {
                    errors.Add($"The file has no \"{key}\" field");
                }
                return;
            }
            if (value is not JsonObject obj)
            {
                errors.Add($"The \"{key}\" field is not a dict");
                return;
            }
            foreach (var kvp in obj)
            {
                if (kvp.Value is not string)
                {
                    errors.Add($"The \"{key}\" field contains a non-string value for key {kvp.Key}");
                }
            }
        }

        CheckStringMap("detection_categories", required: true);
        CheckStringMap("classification_categories", required: false);
        CheckStringMap("classification_category_descriptions", required: false);

        if (errors.Count > 0)
        {
            ThrowErrors(errors);
        }

        if (images.Count == 0)
        {
            throw new PostprocessingException("This file doesn't contain any images");
        }

        if (d.Get("classification_categories") == null)
        {
            throw new PostprocessingException(
                "This file doesn't contain any species classification results (there is no " +
                "\"classification_categories\" field), so there is nothing to postprocess");
        }

        var detectionCategories = (JsonObject)d["detection_categories"]!;
        var classificationCategories = (JsonObject)d["classification_categories"]!;


        for (int iImage = 0; iImage < images.Count; iImage++)
        {
            if (images[iImage] is not JsonObject im)
            {
                errors.Add($"Image {iImage} is not a dict");
                continue;
            }
            if (im.Get("file") is not string file)
            {
                errors.Add($"Image {iImage} has no filename");
                continue;
            }

            object? detectionsValue = im.Get("detections");
            if (detectionsValue == null)
            {
                continue;
            }
            if (detectionsValue is not List<object?> detections)
            {
                errors.Add($"Image {file} has an invalid detections list");
                continue;
            }

            foreach (object? detObj in detections)
            {
                if (detObj is not JsonObject det)
                {
                    errors.Add($"Image {file} has a detection that is not a dict");
                    continue;
                }
                if (det.Get("category") is not string category || !detectionCategories.ContainsKey(category))
                {
                    errors.Add($"Image {file} has a detection with a missing or unmapped category");
                    continue;
                }
                if (!JsonValues.IsNumber(det.Get("conf")))
                {
                    errors.Add($"Image {file} has a detection with a missing or invalid confidence value");
                    continue;
                }

                object? classificationsValue = det.Get("classifications");
                if (classificationsValue == null)
                {
                    continue;
                }
                if (classificationsValue is not List<object?> classifications)
                {
                    errors.Add($"Image {file} has a detection with an invalid classifications list");
                    continue;
                }
                foreach (object? classificationObj in classifications)
                {
                    if (classificationObj is not List<object?> { Count: >= 2 } c ||
                        c[0] is not string classificationCategory ||
                        !JsonValues.IsNumber(c[1]))
                    {
                        errors.Add($"Image {file} has an invalid classification");
                        break;
                    }
                    if (!classificationCategories.ContainsKey(classificationCategory))
                    {
                        errors.Add($"Image {file} has a classification with category {classificationCategory}, " +
                                   "which is not in the classification category list");
                        break;
                    }
                }
            }

            if (errors.Count > 1000)
            {
                break;
            }
        }

        if (errors.Count > 0)
        {
            ThrowErrors(errors);
        }

        return d;
    }

    private static void ThrowErrors(List<string> errors)
    {
        string message = "This file can't be processed:\n\n" +
                         string.Join("\n", errors.Take(MaxErrorsToReport));
        if (errors.Count > MaxErrorsToReport)
        {
            message += $"\n\n...and {errors.Count - MaxErrorsToReport} more problems";
        }
        throw new PostprocessingException(message);
    }

    /// <summary>
    /// Makes sure every classification category has a description, filling in standard
    /// SpeciesNet descriptions for well-known categories (blank, animal, vehicle, no cv result,
    /// human) and an empty (non-taxonomic) description for anything else.  Descriptions for
    /// categories that don't exist are removed.  Does nothing if the file has no descriptions
    /// at all.
    /// </summary>
    public static void NormalizeClassificationDescriptions(JsonObject d, IPostprocessingLog log)
    {
        if (d.Get("classification_category_descriptions") is not JsonObject descriptions)
        {
            return;
        }

        var categories = (JsonObject)d["classification_categories"]!;

        // Remove descriptions for categories that don't exist
        foreach (string categoryId in descriptions.Keys)
        {
            if (!categories.ContainsKey(categoryId))
            {
                log.Warning($"Removing description for classification category {categoryId}, which is not " +
                            $"in the category list: {descriptions[categoryId]}");
                descriptions.Remove(categoryId);
            }
        }

        // Fill in missing descriptions
        bool anyMissing = categories.Keys.Any(k => !descriptions.ContainsKey(k));
        if (anyMissing)
        {
            var newDescriptions = new JsonObject();
            foreach (var kvp in categories)
            {
                if (descriptions.TryGetValue(kvp.Key, out object? existing))
                {
                    newDescriptions.Set(kvp.Key, existing);
                    continue;
                }

                string categoryName = (string)kvp.Value!;
                string normalizedName = categoryName.Trim().ToLowerInvariant();

                // Filling in standard descriptions for well-known categories is routine (e.g. AddaxAI
                // doesn't write descriptions for blank/animal/vehicle), so that's not a warning.
                if (SpeciesNetTaxonomy.StandardDescriptionsByName.TryGetValue(normalizedName, out string? description))
                {
                    log.Info($"Classification category {kvp.Key} ({categoryName}) has no description, " +
                             $"using the standard SpeciesNet description: {description}");
                }
                else
                {
                    string sanitizedName = normalizedName.Replace(';', ' ').Replace('|', ' ').Trim();
                    description = $"{sanitizedName};;;;;;{sanitizedName}";
                    log.Warning($"Classification category {kvp.Key} ({categoryName}) has no description, " +
                                $"treating it as a non-taxonomic category: {description}");
                }
                newDescriptions.Set(kvp.Key, description);
            }
            d["classification_category_descriptions"] = newDescriptions;
            descriptions = newDescriptions;
        }

        // Make sure all descriptions are usable
        var notLowercase = new List<string>();
        foreach (string categoryId in descriptions.Keys)
        {
            string description = (string)descriptions[categoryId]!;
            foreach (string part in description.Split('|'))
            {
                if (part.Split(';').Length != 7)
                {
                    throw new PostprocessingException(
                        $"The description for classification category {categoryId} is not a valid SpeciesNet " +
                        $"taxonomy string: {description}");
                }
            }

            // SpeciesNet taxonomy strings are always lowercase
            string lowercase = description.ToLowerInvariant();
            if (!string.Equals(description, lowercase, StringComparison.Ordinal))
            {
                log.Debug($"Converting the description for classification category {categoryId} to lowercase: " +
                          description);
                notLowercase.Add(categoryId);
                descriptions[categoryId] = lowercase;
            }
        }
        if (notLowercase.Count > 0)
        {
            log.Warning($"{notLowercase.Count} classification category descriptions were not lowercase, and were " +
                        $"converted to lowercase (categories {string.Join(", ", notLowercase)})");
        }
    }

    /// <summary>
    /// Checks that [d] is compliant with the MD output format (port of validate_batch_results()
    /// in megadetector.postprocessing.validate_batch_results).  Like the Python code, validation
    /// stops at the first error.
    /// </summary>
    /// <returns>(errors, warnings); errors has at most one element</returns>
    public static (List<string> Errors, List<string> Warnings) ValidateBatchResults(JsonObject d)
    {
        var errors = new List<string>();
        var warnings = new List<string>();

        try
        {
            ValidateBatchResultsInternal(d, warnings);
        }
        catch (ValidationException e)
        {
            errors.Add(e.Message);
        }

        return (errors, warnings);
    }

    private sealed class ValidationException(string message) : Exception(message);

    private static void Fail(string message) => throw new ValidationException(message);

    private static bool IsPythonInt(string s) =>
        long.TryParse(s.Trim(), NumberStyles.AllowLeadingSign, CultureInfo.InvariantCulture, out _);

    private static bool IsVideoFile(string s) =>
        VideoExtensions.Contains(Path.GetExtension(s).ToLowerInvariant());

    private static void ValidateBatchResultsInternal(JsonObject d, List<string> warnings)
    {
        // Info validation

        if (!d.ContainsKey("info"))
        {
            Fail("Input does not contain info field");
        }
        if (d["info"] is not JsonObject info)
        {
            Fail("Input contains invalid info field");
            return;
        }
        if (!info.ContainsKey("format_version"))
        {
            Fail("Input does not specify format version");
        }

        object? formatVersionValue = info["format_version"];
        double formatVersion = 0;
        if (formatVersionValue is string formatVersionString)
        {
            if (!double.TryParse(formatVersionString.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture,
                                 out formatVersion))
            {
                Fail($"Invalid format version {formatVersionString}");
            }
        }
        else if (JsonValues.IsNumber(formatVersionValue))
        {
            formatVersion = JsonValues.ToDouble(formatVersionValue);
        }
        else
        {
            Fail("Invalid format version");
        }

        if (formatVersion < 1.3)
        {
            Fail("This validator can only be used with format version 1.3 or later");
        }

        // We were ambiguous on string vs float version numbers prior to the 1.6 format
        if (formatVersion >= 1.6 && formatVersionValue is not string)
        {
            Fail("format_version is not a string");
        }

        // Category validation

        if (!d.ContainsKey("detection_categories"))
        {
            Fail("Input does not contain detection_categories field");
        }

        void ValidateCategories(JsonObject categories, string categoryType)
        {
            foreach (var kvp in categories)
            {
                // Category IDs should be string-formatted ints
                if (!IsPythonInt(kvp.Key))
                {
                    Fail($"Invalid {categoryType} category ID: {kvp.Key}");
                }
                if (kvp.Value is not string categoryName)
                {
                    Fail($"Invalid {categoryType} category name: {kvp.Value}");
                    return;
                }
                if (formatVersion >= 1.6 && categoryName.Trim().Length == 0)
                {
                    string capitalized = char.ToUpperInvariant(categoryType[0]) + categoryType.Substring(1);
                    Fail($"{capitalized} category name is blank for ID {kvp.Key}");
                }
            }
        }

        var detectionCategories = (JsonObject)d["detection_categories"]!;
        ValidateCategories(detectionCategories, "detection");

        var classificationCategories = d.Get("classification_categories") as JsonObject;
        if (classificationCategories != null)
        {
            ValidateCategories(classificationCategories, "classification");
        }

        // Image validation

        if (!d.ContainsKey("images"))
        {
            Fail("images field not present");
        }
        if (d["images"] is not List<object?> images)
        {
            Fail("Invalid images field");
            return;
        }

        for (int iImage = 0; iImage < images.Count; iImage++)
        {
            if (images[iImage] is not JsonObject im)
            {
                Fail($"Invalid image at index {iImage}");
                return;
            }
            if (!im.ContainsKey("file"))
            {
                Fail($"Image without filename at index {iImage}");
            }

            string file = im["file"] as string ?? string.Empty;

            if (im.Get("detections") is List<object?> detections)
            {
                foreach (object? detObj in detections)
                {
                    var det = (JsonObject)detObj!;
                    if (!det.ContainsKey("category"))
                    {
                        Fail($"Image {file} has a detection with no category");
                    }
                    if (!det.ContainsKey("conf"))
                    {
                        Fail($"Image {file} has a detection with no confidence");
                    }
                    if (det["conf"] is not double)
                    {
                        Fail($"Image {file} has an illegal confidence value");
                    }
                    if (!det.ContainsKey("bbox"))
                    {
                        Fail($"Image {file} has a detection with no box");
                    }
                    if (det["category"] is not string category || !detectionCategories.ContainsKey(category))
                    {
                        Fail($"Image {file} has a detection with an unmapped category {det["category"]}");
                    }

                    if (det.Get("classifications") is List<object?> classifications)
                    {
                        foreach (object? cObj in classifications)
                        {
                            var c = (List<object?>)cObj!;
                            if (c[0] is not string classificationCategory)
                            {
                                Fail($"Image {file} has an illegal classification category: {c[0]}");
                                return;
                            }
                            if (classificationCategories == null ||
                                !classificationCategories.ContainsKey(classificationCategory))
                            {
                                Fail($"Classification category {classificationCategory} appears in an image, " +
                                     "but not in the category list");
                            }
                            if (!IsPythonInt(classificationCategory))
                            {
                                Fail($"Image {file} has an illegal classification category: {classificationCategory}");
                            }
                            if (c[1] is not (double or long or System.Numerics.BigInteger))
                            {
                                Fail($"Image {file} has an illegal classification confidence");
                            }
                        }
                    }
                }
            }

            if (im.ContainsKey("failure"))
            {
                object? failure = im["failure"];
                if (failure != null)
                {
                    if (failure is not string)
                    {
                        Fail($"Image {file} has an illegal [failure] value: {failure}");
                    }
                    if (!im.ContainsKey("detections"))
                    {
                        warnings.Add($"Image {file} has a failure value, should also have a null detections array");
                    }
                    else if (im["detections"] != null)
                    {
                        Fail($"Image {file} has a failure value but a non-null detections array");
                    }
                }
            }
            else
            {
                if (im.Get("detections") is not List<object?>)
                {
                    Fail($"Invalid detections list for image {file}");
                }
            }

            if (IsVideoFile(file) && formatVersion >= 1.5 && !im.ContainsKey("frames_processed"))
            {
                Fail($"Video without frames_processed field: {file}");
            }

            if (IsVideoFile(file) && formatVersion >= 1.4)
            {
                if (!im.ContainsKey("frame_rate"))
                {
                    Fail($"Video without frame rate: {file}");
                }
                if (JsonValues.IsNumber(im["frame_rate"]) && JsonValues.ToDouble(im["frame_rate"]) < 0 &&
                    !im.ContainsKey("failure"))
                {
                    Fail($"Video with illegal frame rate {im["frame_rate"]}: {file}");
                }
                if (im.Get("detections") is List<object?> videoDetections)
                {
                    foreach (object? detObj in videoDetections)
                    {
                        if (!((JsonObject)detObj!).ContainsKey("frame_number"))
                        {
                            Fail($"Frame without frame number in video {file}");
                        }
                    }
                }
            }
        }

        // Validation of other keys

        foreach (string k in d.Keys)
        {
            if (!TypicalKeys.Contains(k) && !RequiredKeys.Contains(k))
            {
                warnings.Add($"Warning: non-standard key {k} present at file level");
            }
        }
    }
}
