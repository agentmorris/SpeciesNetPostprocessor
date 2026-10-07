using SpeciesNetPostprocessor.Core.Json;

namespace SpeciesNetPostprocessor.Core;

/// <summary>
/// Simple cleanup operations on MD-formatted results.
/// </summary>
public static class ResultsCleanup
{
    /// <summary>
    /// Removes classifications from non-animal detections (port of
    /// remove_classifications_from_non_animal_detections() in
    /// megadetector.postprocessing.classification_postprocessing).
    /// </summary>
    /// <param name="animalCategoryNames">the detection category names that should be treated as
    /// animals (defaults to just "animal")</param>
    public static void RemoveClassificationsFromNonAnimalDetections(JsonObject d,
                                                                    IPostprocessingLog log,
                                                                    IReadOnlyCollection<string>? animalCategoryNames = null)
    {
        var animalNames = new HashSet<string>(animalCategoryNames ?? ["animal"], StringComparer.Ordinal);
        OrderedDictionary<string, string> categoryIdToName = JsonValues.GetStringMap(d, "detection_categories");

        int nClassificationsRemoved = 0;
        int nDetections = 0;

        foreach (object? imObj in JsonValues.GetImages(d))
        {
            var im = (JsonObject)imObj!;
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null)
            {
                continue;
            }

            nDetections += detections.Count;

            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!det.ContainsKey("classifications"))
                {
                    continue;
                }
                string categoryName = categoryIdToName[(string)det["category"]!];
                if (!animalNames.Contains(categoryName))
                {
                    det.Remove("classifications");
                    nClassificationsRemoved++;
                }
            }
        }

        log.Info($"Removed classifications from {nClassificationsRemoved} of {nDetections} detections");
    }

    /// <summary>
    /// Removes all classification categories with fewer than [minCount] instances, without
    /// re-mapping category IDs (port of remove_classification_categories_below_count() in
    /// megadetector.postprocessing.subset_json_detector_output).
    /// </summary>
    public static void RemoveClassificationCategoriesBelowCount(JsonObject d, int minCount, IPostprocessingLog log)
    {
        if (d.Get("classification_categories") is not JsonObject classificationCategories)
        {
            return;
        }

        var categoryIdToCount = new OrderedDictionary<string, int>(StringComparer.Ordinal);
        foreach (var kvp in classificationCategories)
        {
            categoryIdToCount[kvp.Key] = 0;
        }

        // Count the number of occurrences of each classification category
        foreach (object? imObj in JsonValues.GetImages(d))
        {
            var im = (JsonObject)imObj!;
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null)
            {
                continue;
            }
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                List<object?>? classifications = JsonValues.GetClassifications(det);
                if (classifications == null)
                {
                    continue;
                }
                foreach (object? classificationObj in classifications)
                {
                    string categoryId = (string)((List<object?>)classificationObj!)[0]!;
                    categoryIdToCount[categoryId]++;
                }
            }
        }

        // Which categories have above-threshold counts?
        var categoryIdsToKeep = new HashSet<string>(StringComparer.Ordinal);
        foreach (var kvp in categoryIdToCount)
        {
            if (kvp.Value >= minCount)
            {
                categoryIdsToKeep.Add(kvp.Key);
            }
        }

        int nCategoriesRemoved = categoryIdToCount.Count - categoryIdsToKeep.Count;

        log.Info($"Removing {nCategoriesRemoved} of {categoryIdToCount.Count} classification categories");

        if (nCategoriesRemoved == 0)
        {
            return;
        }

        foreach (var kvp in categoryIdToCount)
        {
            if (!categoryIdsToKeep.Contains(kvp.Key))
            {
                log.Debug($"Removing unused classification category {kvp.Key} ({classificationCategories[kvp.Key]})");
            }
        }

        // Filter the category list
        var outputCategories = new JsonObject();
        foreach (var kvp in classificationCategories)
        {
            if (categoryIdsToKeep.Contains(kvp.Key))
            {
                outputCategories.Set(kvp.Key, kvp.Value);
            }
        }
        d["classification_categories"] = outputCategories;

        // If necessary, filter the category descriptions
        if (d.Get("classification_category_descriptions") is JsonObject descriptions)
        {
            var outputDescriptions = new JsonObject();
            foreach (var kvp in descriptions)
            {
                if (categoryIdsToKeep.Contains(kvp.Key))
                {
                    outputDescriptions.Set(kvp.Key, kvp.Value);
                }
            }
            d["classification_category_descriptions"] = outputDescriptions;
        }

        // Filter images
        foreach (object? imObj in JsonValues.GetImages(d))
        {
            var im = (JsonObject)imObj!;
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null)
            {
                continue;
            }
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                List<object?>? classifications = JsonValues.GetClassifications(det);
                if (classifications == null)
                {
                    continue;
                }
                var classificationsToKeep = new List<object?>();
                foreach (object? classificationObj in classifications)
                {
                    if (categoryIdsToKeep.Contains((string)((List<object?>)classificationObj!)[0]!))
                    {
                        classificationsToKeep.Add(classificationObj);
                    }
                }
                det["classifications"] = classificationsToKeep;
            }
        }
    }
}
