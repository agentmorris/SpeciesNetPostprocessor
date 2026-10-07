using SpeciesNetPostprocessor.Core.Json;
using SpeciesNetPostprocessor.Core.Sequences;
using SpeciesNetPostprocessor.Core.Taxonomy;

namespace SpeciesNetPostprocessor.Core.Smoothing;

/// <summary>
/// Options used to parameterize image-level and sequence-level smoothing (port of
/// ClassificationSmoothingOptions in megadetector.postprocessing.classification_postprocessing).
/// </summary>
public sealed class ClassificationSmoothingOptions
{
    /// <summary>
    /// How many detections do we need in a dominant category to overwrite non-dominant
    /// classifications?  This is irrelevant if MaxDetectionsNondominantClass &lt;= 1.
    /// </summary>
    public int MinDetectionsToOverwriteSecondary { get; set; } = 4;

    /// <summary>
    /// Even if we have a dominant class, if a non-dominant class has at least this many
    /// classifications in an image, leave them alone.  If this is &lt;= 1, we won't replace
    /// non-dominant, non-other classes with the dominant class, even if there are 900 cows
    /// and 1 deer.
    /// </summary>
    public int MaxDetectionsNondominantClass { get; set; } = 1;

    /// <summary>
    /// How many detections do we need in a dominant category to overwrite non-dominant
    /// classifications in the same family?  If this is &lt;= 0, we'll skip this step.
    /// </summary>
    public int MinDetectionsToOverwriteSecondarySameFamily { get; set; } = 2;

    /// <summary>
    /// If we have this many classifications of a nondominant category, we won't do same-family
    /// overwrites.  &lt;= 1 means "even if there are a million deer, if there are two million
    /// moose, call all the deer moose".
    /// </summary>
    public int MaxDetectionsNondominantClassSameFamily { get; set; } = -1;

    /// <summary>
    /// If the dominant class has at least this many classifications, overwrite "other"
    /// classifications with the dominant class.
    /// </summary>
    public int MinDetectionsToOverwriteOther { get; set; } = 2;

    /// <summary>
    /// Names to treat as "other" categories.
    /// </summary>
    public List<string> OtherCategoryNames { get; set; } =
        ["other", "unknown", "no cv result", "animal", "blank", "mammal"];

    /// <summary>
    /// Classifications below this threshold are ignored entirely.
    /// </summary>
    public double ClassificationConfidenceThreshold { get; set; } = 0.5;

    /// <summary>
    /// Detections below this threshold are ignored entirely.
    /// </summary>
    public double DetectionConfidenceThreshold { get; set; } = 0.15;

    /// <summary>
    /// Should we propagate classifications when lower-level taxa are more common in an image?
    /// For example, if we see "carnivore/fox/fox/deer", should we make that "fox/fox/fox/deer"?
    /// </summary>
    public bool PropagateClassificationsThroughTaxonomy { get; set; } = true;

    /// <summary>
    /// Balances taxonomic levels against counts when propagating classifications.
    /// </summary>
    public double TaxonomyPropagationLevelWeight { get; set; } = 1.0;

    /// <summary>
    /// Balances taxonomic levels against counts when propagating classifications; with a
    /// very low default value, this just breaks ties.
    /// </summary>
    public double TaxonomyPropagationCountWeight { get; set; } = 0.01;

    /// <summary>
    /// Should we record information about the state of labels prior to smoothing?
    /// </summary>
    public bool AddPreSmoothingDescription { get; set; } = true;

    /// <summary>
    /// Only include these detection categories in the smoothing process (null to use all
    /// categories).
    /// </summary>
    public List<string>? DetectionCategoryNamesToSmooth { get; set; } = ["animal"];
}

/// <summary>
/// The number of above-threshold detections in each classification category, sorted in
/// descending order by count (ties are kept in order of first appearance).
/// </summary>
internal sealed class CategoryCounts
{
    public readonly List<string> Keys = new();
    public readonly Dictionary<string, int> Counts = new(StringComparer.Ordinal);

    public int Count => Keys.Count;
    public int this[string categoryId] => Counts[categoryId];
    public bool Contains(string categoryId) => Counts.ContainsKey(categoryId);
}

/// <summary>
/// Summary of the changes made while smoothing one list of detections.
/// </summary>
internal sealed record SmoothingChanges(int OtherClassificationsChanged,
                                        int DetectionsFlipped,
                                        int TaxonomicChanges,
                                        int WithinFamilyChanges);

/// <summary>
/// Image-level and sequence-level classification smoothing (port of the smoothing functions
/// in megadetector.postprocessing.classification_postprocessing).
/// </summary>
public static class ClassificationSmoothing
{
    /// <summary>
    /// State derived from the results file and the options, shared by all smoothing calls.
    /// </summary>
    internal sealed class Context
    {
        public required ClassificationSmoothingOptions Options;
        public required HashSet<string> OtherCategoryIds;
        public HashSet<string>? DetectionCategoryIdsToSmooth;
        public Dictionary<string, string>? Descriptions;
        public Dictionary<string, string>? DescriptionsClean;
    }

    #region Utility functions

    private static bool DetectionIsRelevantForSmoothing(JsonObject det,
                                                        ClassificationSmoothingOptions options,
                                                        HashSet<string>? detectionCategoryIdsToSmooth)
    {
        if (JsonValues.GetClassifications(det) is not { Count: > 0 } ||
            JsonValues.ToDouble(det["conf"]) < options.DetectionConfidenceThreshold)
        {
            return false;
        }

        // Ignore non-smoothed categories
        if (detectionCategoryIdsToSmooth != null &&
            !detectionCategoryIdsToSmooth.Contains((string)det["category"]!))
        {
            return false;
        }

        return true;
    }

    private static List<object?> TopClassification(JsonObject det) =>
        (List<object?>)JsonValues.GetClassifications(det)![0]!;

    /// <summary>
    /// Counts the number of instances of each classification category in [detections] that
    /// have an above-threshold detection, considering only the top classification for each
    /// detection.  Results are sorted in descending order by count.
    /// </summary>
    internal static CategoryCounts CountDetectionsByClassificationCategory(
        List<object?>? detections,
        ClassificationSmoothingOptions options,
        HashSet<string>? detectionCategoryIdsToSmooth)
    {
        var unsorted = new CategoryCounts();
        if (detections == null || detections.Count == 0)
        {
            return unsorted;
        }

        foreach (object? detObj in detections)
        {
            var det = (JsonObject)detObj!;
            if (!DetectionIsRelevantForSmoothing(det, options, detectionCategoryIdsToSmooth))
            {
                continue;
            }
            List<object?> c = TopClassification(det);
            if (JsonValues.ToDouble(c[1]) >= options.ClassificationConfidenceThreshold)
            {
                string categoryId = (string)c[0]!;
                if (unsorted.Counts.TryGetValue(categoryId, out int n))
                {
                    unsorted.Counts[categoryId] = n + 1;
                }
                else
                {
                    unsorted.Counts[categoryId] = 1;
                    unsorted.Keys.Add(categoryId);
                }
            }
        }

        // OrderByDescending is a stable sort, like Python's sorted(..., reverse=True)
        var sorted = new CategoryCounts();
        foreach (string categoryId in unsorted.Keys.OrderByDescending(k => unsorted.Counts[k]))
        {
            sorted.Keys.Add(categoryId);
            sorted.Counts[categoryId] = unsorted.Counts[categoryId];
        }
        return sorted;
    }

    internal static CategoryCounts CountDetectionsByClassificationCategory(List<object?>? detections,
                                                                           Context context) =>
        CountDetectionsByClassificationCategory(detections, context.Options, context.DetectionCategoryIdsToSmooth);

    /// <summary>
    /// Returns a string summarizing the image content according to [categoryToCount], e.g.
    /// "rabbit (4), human (1)".
    /// </summary>
    internal static string GetClassificationDescriptionString(CategoryCounts categoryToCount,
                                                              IReadOnlyDictionary<string, string> classificationDescriptions)
    {
        var categoryStrings = new List<string>();
        foreach (string categoryId in categoryToCount.Keys)
        {
            // Only the first of several "|"-delimited descriptions is used
            string categoryDescription = SpeciesNetTaxonomy.FirstDescription(classificationDescriptions[categoryId]);
            string[] tokens = categoryDescription.Split(';');
            if (tokens.Length != 7)
            {
                throw new PostprocessingException($"Invalid category description {categoryDescription}");
            }
            string categoryName = tokens[^1];
            if (categoryName.Length == 0)
            {
                categoryName = "undefined category";
            }
            categoryStrings.Add($"{categoryName} ({categoryToCount[categoryId]})");
        }
        return string.Join(", ", categoryStrings);
    }

    /// <summary>
    /// Prepares results for smoothing: removes all but the top classification for each detection,
    /// prepares category descriptions, and optionally adds pre-smoothing descriptions to every image.
    /// </summary>
    internal static Context PrepareResultsForSmoothing(JsonObject d,
                                                       ClassificationSmoothingOptions options,
                                                       IPostprocessingLog log)
    {
        // Category processing

        var categoryNameToId = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in JsonValues.GetStringMap(d, "classification_categories"))
        {
            categoryNameToId[kvp.Value] = kvp.Key;
        }
        var otherCategoryIds = new HashSet<string>(StringComparer.Ordinal);
        foreach (string s in options.OtherCategoryNames)
        {
            if (categoryNameToId.TryGetValue(s, out string? id))
            {
                otherCategoryIds.Add(id);
            }
        }

        // Possibly update the list of category IDs we should smooth
        HashSet<string>? detectionCategoryIdsToSmooth = null;
        if (options.DetectionCategoryNamesToSmooth != null)
        {
            var detectionCategoryNameToId = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in JsonValues.GetStringMap(d, "detection_categories"))
            {
                detectionCategoryNameToId[kvp.Value] = kvp.Key;
            }
            detectionCategoryIdsToSmooth = new HashSet<string>(StringComparer.Ordinal);
            foreach (string categoryName in options.DetectionCategoryNamesToSmooth)
            {
                if (detectionCategoryNameToId.TryGetValue(categoryName, out string? id))
                {
                    detectionCategoryIdsToSmooth.Add(id);
                }
                else
                {
                    log.Warning($"Detection category \"{categoryName}\" is not present in this file, " +
                                "detections in that category will not be smoothed");
                }
            }
        }

        // Before we do anything else, get rid of everything but the top classification
        // for each detection, and remove the 'classifications' field from detections with
        // no classifications.
        int nUnsortedLists = 0;

        foreach (object? imObj in JsonValues.GetImages(d))
        {
            var im = (JsonObject)imObj!;
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null || detections.Count == 0)
            {
                continue;
            }

            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!det.ContainsKey("classifications"))
                {
                    continue;
                }
                List<object?>? classifications = JsonValues.GetClassifications(det);
                if (classifications == null)
                {
                    continue;
                }
                if (classifications.Count == 0)
                {
                    det.Remove("classifications");
                    continue;
                }

                // The Python code asserts that classifications are sorted by confidence; if they
                // aren't, we sort them (stably) rather than failing.
                bool sorted = true;
                for (int i = 0; i < classifications.Count - 1; i++)
                {
                    if (!(JsonValues.ToDouble(((List<object?>)classifications[i]!)[1]) >=
                          JsonValues.ToDouble(((List<object?>)classifications[i + 1]!)[1])))
                    {
                        sorted = false;
                        break;
                    }
                }
                if (!sorted)
                {
                    nUnsortedLists++;
                    classifications = classifications
                        .OrderByDescending(c => JsonValues.ToDouble(((List<object?>)c!)[1]))
                        .ToList();
                }

                det["classifications"] = new List<object?> { classifications[0] };
            }
        }

        if (nUnsortedLists > 0)
        {
            log.Warning($"{nUnsortedLists} detections had classifications that were not sorted by " +
                        "confidence; these were sorted before smoothing");
        }

        // Clean up classification descriptions...

        // ...so we can test taxonomic relationships by substring testing.

        Dictionary<string, string>? classificationDescriptions = null;
        Dictionary<string, string>? classificationDescriptionsClean = null;

        if (d.Get("classification_category_descriptions") is JsonObject descriptionsObj)
        {
            classificationDescriptions = new Dictionary<string, string>(StringComparer.Ordinal);
            classificationDescriptionsClean = new Dictionary<string, string>(StringComparer.Ordinal);

            // We use "|" to delimit multiple descriptions, just use the first for smoothing.
            foreach (var kvp in descriptionsObj)
            {
                classificationDescriptions[kvp.Key] = SpeciesNetTaxonomy.FirstDescription((string)kvp.Value!);
            }

            // The Python code requires descriptions to be lowercase; we lowercase them here
            // rather than failing (the clean descriptions are lowercased anyway).
            foreach (var kvp in classificationDescriptions)
            {
                classificationDescriptionsClean[kvp.Key] =
                    SpeciesNetTaxonomy.CleanTaxonomyString(kvp.Value.ToLowerInvariant()).Trim(';').ToLowerInvariant();
            }
        }

        var context = new Context
        {
            Options = options,
            OtherCategoryIds = otherCategoryIds,
            DetectionCategoryIdsToSmooth = detectionCategoryIdsToSmooth,
            Descriptions = classificationDescriptions,
            DescriptionsClean = classificationDescriptionsClean
        };

        // Optionally add pre-smoothing descriptions to every image

        if (options.AddPreSmoothingDescription && classificationDescriptions != null)
        {
            foreach (object? imObj in JsonValues.GetImages(d))
            {
                var im = (JsonObject)imObj!;
                List<object?>? detections = JsonValues.GetDetections(im);
                if (detections == null || detections.Count == 0)
                {
                    continue;
                }
                CategoryCounts categoryToCount = CountDetectionsByClassificationCategory(detections, context);
                im["pre_smoothing_description"] =
                    GetClassificationDescriptionString(categoryToCount, classificationDescriptions);
            }
        }

        return context;
    }

    #endregion

    #region Smoothing a list of detections

    /// <summary>
    /// Smooths classifications for a list of detections, which may have come from a single image,
    /// or may represent an entire sequence.  Returns null if no changes were attempted.
    /// </summary>
    /// <remarks>
    /// Assumes there is only one classification per detection, i.e. that non-top classifications
    /// have already been removed.
    /// </remarks>
    internal static SmoothingChanges? SmoothClassificationsForListOfDetections(List<object?> detections,
                                                                              Context context)
    {
        ClassificationSmoothingOptions options = context.Options;
        HashSet<string> otherCategoryIds = context.OtherCategoryIds;
        HashSet<string>? idsToSmooth = context.DetectionCategoryIdsToSmooth;
        Dictionary<string, string>? classificationDescriptions = context.Descriptions;
        Dictionary<string, string>? classificationDescriptionsClean = context.DescriptionsClean;

        // Count the number of instances of each category in this image

        CategoryCounts categoryToCount = CountDetectionsByClassificationCategory(detections, context);

        if (categoryToCount.Count <= 1)
        {
            return null;
        }

        List<string> keys = new(categoryToCount.Keys);

        // Handle a quirky special case: if the most common category is "other" and
        // it's "tied" with the second-most-common category, swap them
        if (keys.Count > 1 &&
            otherCategoryIds.Contains(keys[0]) &&
            !otherCategoryIds.Contains(keys[1]) &&
            categoryToCount[keys[0]] == categoryToCount[keys[1]])
        {
            (keys[1], keys[0]) = (keys[0], keys[1]);
        }

        int maxCount = categoryToCount[keys[0]];
        string mostCommonCategory = keys[0];

        // Possibly change "other" classifications to the most common category

        // ...if the dominant category is not an "other" category.

        int nOtherClassificationsChanged = 0;

        // If we have at least MinDetectionsToOverwriteOther in a category that isn't
        // "other", change all "other" classifications to that category
        if (maxCount >= options.MinDetectionsToOverwriteOther &&
            !otherCategoryIds.Contains(mostCommonCategory))
        {
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!DetectionIsRelevantForSmoothing(det, options, idsToSmooth))
                {
                    continue;
                }

                List<object?> c = TopClassification(det);

                if (JsonValues.ToDouble(c[1]) >= options.ClassificationConfidenceThreshold &&
                    otherCategoryIds.Contains((string)c[0]!))
                {
                    nOtherClassificationsChanged++;
                    c[0] = mostCommonCategory;
                }
            }
        }

        // Re-count

        categoryToCount = CountDetectionsByClassificationCategory(detections, context);
        maxCount = categoryToCount[categoryToCount.Keys[0]];
        mostCommonCategory = categoryToCount.Keys[0];

        // Possibly change some non-dominant classifications to the dominant category

        bool processTaxonomicRules =
            classificationDescriptionsClean != null &&
            classificationDescriptionsClean.Count > 0 &&
            categoryToCount.Count > 1;

        int nDetectionsFlipped = 0;

        // Don't do this if the most common category is an "other" category, or
        // if we don't have enough of the most common category
        if (!otherCategoryIds.Contains(mostCommonCategory) &&
            maxCount >= options.MinDetectionsToOverwriteSecondary)
        {
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!DetectionIsRelevantForSmoothing(det, options, idsToSmooth))
                {
                    continue;
                }

                List<object?> c = TopClassification(det);
                string categoryIdThisClassification = (string)c[0]!;

                // Don't over-write the most common category with itself
                if (categoryIdThisClassification == mostCommonCategory)
                {
                    continue;
                }

                // Don't bother with below-threshold classifications
                if (JsonValues.ToDouble(c[1]) < options.ClassificationConfidenceThreshold)
                {
                    continue;
                }

                // If we're doing taxonomic processing, at this stage, don't turn children
                // into parents; we'll likely turn parents into children in the next stage.
                if (processTaxonomicRules)
                {
                    string mostCommonCategoryDescription = classificationDescriptionsClean![mostCommonCategory];
                    string categoryDescriptionThisClassification =
                        classificationDescriptionsClean[categoryIdThisClassification];

                    // An empty description corresponds to the "animal" category.  We don't handle
                    // "animal" here as a parent category, that would be handled in the "other smoothing"
                    // step above.
                    if (categoryDescriptionThisClassification.Length == 0)
                    {
                        continue;
                    }

                    bool mostCommonCategoryIsParentOfThisCategory =
                        categoryDescriptionThisClassification.Contains(mostCommonCategoryDescription,
                                                                       StringComparison.Ordinal);

                    if (mostCommonCategoryIsParentOfThisCategory)
                    {
                        continue;
                    }
                }

                // If we have fewer of this category than the most common category,
                // but not *too* many, flip it to the most common category.
                if (maxCount > categoryToCount[categoryIdThisClassification] &&
                    categoryToCount[categoryIdThisClassification] <= options.MaxDetectionsNondominantClass)
                {
                    c[0] = mostCommonCategory;
                    nDetectionsFlipped++;
                }
            }
        }

        // Re-count

        categoryToCount = CountDetectionsByClassificationCategory(detections, context);
        mostCommonCategory = categoryToCount.Keys[0];

        // Possibly collapse higher-level taxonomic predictions down to lower levels

        int nTaxonomicChanges = 0;

        processTaxonomicRules =
            classificationDescriptionsClean != null &&
            classificationDescriptionsClean.Count > 0 &&
            categoryToCount.Count > 1;

        if (processTaxonomicRules && options.PropagateClassificationsThroughTaxonomy)
        {
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!DetectionIsRelevantForSmoothing(det, options, idsToSmooth))
                {
                    continue;
                }

                List<object?> c = TopClassification(det);

                // Don't bother with any classifications below the confidence threshold
                if (JsonValues.ToDouble(c[1]) < options.ClassificationConfidenceThreshold)
                {
                    continue;
                }

                string categoryIdThisClassification = (string)c[0]!;
                string categoryDescriptionThisClassification =
                    classificationDescriptionsClean![categoryIdThisClassification];

                // An empty description corresponds to the "animal" category.  We don't handle
                // "animal" here as a parent category, that would be handled in the "other smoothing"
                // step above.
                if (categoryDescriptionThisClassification.Length == 0)
                {
                    continue;
                }

                // We may have multiple child categories to choose from; this keeps track of
                // the "best" we've seen so far.  "Best" is based on the level (species is better
                // than genus) and number.
                string? bestChildCategory = null;
                double bestChildScore = double.NegativeInfinity;

                foreach (string categoryIdOfCandidateChild in categoryToCount.Keys)
                {
                    // A category is never its own child
                    if (categoryIdOfCandidateChild == categoryIdThisClassification)
                    {
                        continue;
                    }

                    // Is this candidate a child of the current classification?
                    string categoryDescriptionCandidateChild =
                        classificationDescriptionsClean[categoryIdOfCandidateChild];

                    // An empty description corresponds to "animal", which can never
                    // be a child of another category.
                    if (categoryDescriptionCandidateChild.Length == 0)
                    {
                        continue;
                    }

                    // If two categories have different IDs but the same "clean" description, this
                    // means they're different common names for the same species; we don't smooth
                    // those predictions, because it's not a parent/child relationship.
                    if (categoryDescriptionCandidateChild == categoryDescriptionThisClassification)
                    {
                        continue;
                    }

                    // As long as we're using "clean" descriptions, parent/child taxonomic
                    // relationships are defined by a substring relationship
                    bool isChild = categoryDescriptionCandidateChild.Contains(categoryDescriptionThisClassification,
                                                                              StringComparison.Ordinal);
                    if (!isChild)
                    {
                        continue;
                    }

                    // How many instances of this child category are there?
                    int childCategoryCount = categoryToCount[categoryIdOfCandidateChild];

                    // What taxonomy level is this child category defined at?
                    int childCategoryLevel =
                        SpeciesNetTaxonomy.TaxonomyLevelIndex(classificationDescriptions![categoryIdOfCandidateChild]);

                    double score = childCategoryLevel * options.TaxonomyPropagationLevelWeight +
                                   childCategoryCount * options.TaxonomyPropagationCountWeight;

                    // Equivalent to a stable descending sort followed by taking the first element
                    if (bestChildCategory == null || score > bestChildScore)
                    {
                        bestChildCategory = categoryIdOfCandidateChild;
                        bestChildScore = score;
                    }
                }

                // Did we find a category we want to change this classification to?
                if (bestChildCategory != null)
                {
                    c[0] = bestChildCategory;
                    nTaxonomicChanges++;
                }
            }
        }

        // Re-count

        categoryToCount = CountDetectionsByClassificationCategory(detections, context);
        mostCommonCategory = categoryToCount.Keys[0];

        // Possibly do within-family smoothing

        int nWithinFamilySmoothingChanges = 0;

        int familyLevel = SpeciesNetTaxonomy.TaxonomyLevelStringToIndex("family");

        int mostCommonCategoryTaxonomicLevel = 0;
        int nMostCommonCategory = 0;
        string mostCommonCategoryFamily = string.Empty;
        string mostCommonCategoryGenus = string.Empty;

        if (processTaxonomicRules)
        {
            string categoryDescriptionMostCommonCategory = classificationDescriptions![mostCommonCategory];
            mostCommonCategoryTaxonomicLevel = SpeciesNetTaxonomy.TaxonomyLevelIndex(categoryDescriptionMostCommonCategory);
            nMostCommonCategory = categoryToCount[mostCommonCategory];
            string[] tokens = SplitDescription(categoryDescriptionMostCommonCategory);
            mostCommonCategoryFamily = tokens[3];
            mostCommonCategoryGenus = tokens[4];
        }

        // Only consider remapping to genus or species level, and only when we have
        // a high enough count in the most common category
        if (processTaxonomicRules &&
            options.MinDetectionsToOverwriteSecondarySameFamily > 0 &&
            !otherCategoryIds.Contains(mostCommonCategory) &&
            mostCommonCategoryTaxonomicLevel > familyLevel &&
            nMostCommonCategory >= options.MinDetectionsToOverwriteSecondarySameFamily)
        {
            foreach (object? detObj in detections)
            {
                var det = (JsonObject)detObj!;
                if (!DetectionIsRelevantForSmoothing(det, options, idsToSmooth))
                {
                    continue;
                }

                List<object?> c = TopClassification(det);
                string categoryIdThisClassification = (string)c[0]!;

                // Don't over-write the most common category with itself
                if (categoryIdThisClassification == mostCommonCategory)
                {
                    continue;
                }

                // Don't bother with below-threshold classifications
                if (JsonValues.ToDouble(c[1]) < options.ClassificationConfidenceThreshold)
                {
                    continue;
                }

                int nCandidateFlipCategory = categoryToCount[categoryIdThisClassification];

                // Do we have too many of the non-dominant category to do this kind of swap?
                if (nCandidateFlipCategory > options.MaxDetectionsNondominantClassSameFamily)
                {
                    continue;
                }

                // Don't flip classes when it's a tie
                if (nCandidateFlipCategory == nMostCommonCategory)
                {
                    continue;
                }

                string categoryDescriptionCandidateFlip = classificationDescriptions![categoryIdThisClassification];
                string[] tokens = SplitDescription(categoryDescriptionCandidateFlip);
                string candidateFlipCategoryFamily = tokens[3];
                string candidateFlipCategoryGenus = tokens[4];
                int candidateFlipCategoryTaxonomicLevel =
                    SpeciesNetTaxonomy.TaxonomyLevelIndex(categoryDescriptionCandidateFlip);

                // Only proceed if we have valid family strings
                if (candidateFlipCategoryFamily.Length == 0 || mostCommonCategoryFamily.Length == 0)
                {
                    continue;
                }

                // Only proceed if the candidate and the most common category are in the same family
                if (candidateFlipCategoryFamily != mostCommonCategoryFamily)
                {
                    continue;
                }

                // Don't flip from a species to the genus level in the same genus
                if (candidateFlipCategoryGenus == mostCommonCategoryGenus &&
                    candidateFlipCategoryTaxonomicLevel > mostCommonCategoryTaxonomicLevel)
                {
                    continue;
                }

                c[0] = mostCommonCategory;
                nWithinFamilySmoothingChanges++;
            }
        }

        return new SmoothingChanges(nOtherClassificationsChanged,
                                    nDetectionsFlipped,
                                    nTaxonomicChanges,
                                    nWithinFamilySmoothingChanges);
    }

    private static string[] SplitDescription(string description)
    {
        string[] tokens = description.Split(';');
        if (tokens.Length != 7)
        {
            throw new PostprocessingException($"Invalid category description {description}");
        }
        return tokens;
    }

    #endregion

    #region Image-level smoothing

    /// <summary>
    /// Smooths classifications at the image level for all results in [d] (modified in place).
    /// This generally expresses the notion that an image with 700 cows and one deer is really
    /// just 701 cows.
    /// </summary>
    public static void SmoothClassificationResultsImageLevel(JsonObject d,
                                                             ClassificationSmoothingOptions options,
                                                             IPostprocessingLog log,
                                                             Action<double>? progress = null,
                                                             CancellationToken cancellationToken = default)
    {
        Context context = PrepareResultsForSmoothing(d, options, log);

        int nOtherClassificationsChanged = 0;
        int nOtherImagesChanged = 0;
        int nTaxonomicImagesChanged = 0;

        int nDetectionsFlipped = 0;
        int nImagesChanged = 0;
        int nTaxonomicClassificationChanges = 0;

        List<object?> images = JsonValues.GetImages(d);

        for (int iImage = 0; iImage < images.Count; iImage++)
        {
            if (iImage % 1000 == 0)
            {
                progress?.Invoke((double)iImage / images.Count);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var im = (JsonObject)images[iImage]!;
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null || detections.Count == 0)
            {
                continue;
            }

            SmoothingChanges? r = SmoothClassificationsForListOfDetections(detections, context);
            if (r == null)
            {
                continue;
            }

            nDetectionsFlipped += r.DetectionsFlipped;
            nOtherClassificationsChanged += r.OtherClassificationsChanged;
            nTaxonomicClassificationChanges += r.TaxonomicChanges;

            if (r.DetectionsFlipped > 0)
            {
                nImagesChanged++;
            }
            if (r.OtherClassificationsChanged > 0)
            {
                nOtherImagesChanged++;
            }
            if (r.TaxonomicChanges > 0)
            {
                nTaxonomicImagesChanged++;
            }
        }

        progress?.Invoke(1.0);

        log.Info($"Classification smoothing: changed {nDetectionsFlipped} detections on {nImagesChanged} images");
        log.Info($"\"Other\" smoothing: changed {nOtherClassificationsChanged} detections on {nOtherImagesChanged} images");
        log.Info($"Taxonomic smoothing: changed {nTaxonomicClassificationChanges} detections on {nTaxonomicImagesChanged} images");
    }

    #endregion

    #region Sequence-level smoothing

    /// <summary>
    /// Smooths classifications at the sequence level for all results in [d] (modified in place).
    /// This generally expresses the notion that a sequence that looks like
    /// deer/deer/deer/elk/deer/deer/deer/deer is really just a deer.
    /// </summary>
    /// <param name="sequenceImages">sequence assignments for each image, in the order
    /// produced by SequenceBuilder</param>
    public static void SmoothClassificationResultsSequenceLevel(JsonObject d,
                                                                IReadOnlyList<SequenceImage> sequenceImages,
                                                                ClassificationSmoothingOptions options,
                                                                IPostprocessingLog log,
                                                                Action<double>? progress = null,
                                                                CancellationToken cancellationToken = default)
    {
        Context context = PrepareResultsForSmoothing(d, options, log);

        // Make a list of images appearing in each sequence

        var sequenceToImageFilenames = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (SequenceImage seqIm in sequenceImages)
        {
            if (!sequenceToImageFilenames.TryGetValue(seqIm.SeqId, out List<string>? filenames))
            {
                filenames = new List<string>();
                sequenceToImageFilenames.Add(seqIm.SeqId, filenames);
            }
            filenames.Add(seqIm.FileName);
        }

        var imageFnToClassificationResults = new Dictionary<string, JsonObject>(StringComparer.Ordinal);
        foreach (object? imObj in JsonValues.GetImages(d))
        {
            var im = (JsonObject)imObj!;
            string fn = (string)im["file"]!;
            if (!imageFnToClassificationResults.TryAdd(fn, im))
            {
                throw new PostprocessingException($"Image file {fn} appears more than once in the results file");
            }
        }

        // Smoothing

        int nOtherClassificationsChanged = 0;
        int nOtherSequencesChanged = 0;
        int nTaxonomicSequencesChanged = 0;
        int nWithinFamilySequencesChanged = 0;

        int nDetectionsFlipped = 0;
        int nSequencesChanged = 0;
        int nTaxonomicClassificationChanges = 0;
        int nWithinFamilyChanges = 0;

        var imagesMissingFromResults = new SortedSet<string>(StringComparer.Ordinal);

        int iSequence = 0;
        foreach (var kvp in sequenceToImageFilenames)
        {
            if (iSequence++ % 1000 == 0)
            {
                progress?.Invoke((double)iSequence / sequenceToImageFilenames.Count);
                cancellationToken.ThrowIfCancellationRequested();
            }

            var detectionsThisSequence = new List<object?>();
            foreach (string imageFilename in kvp.Value)
            {
                if (!imageFnToClassificationResults.TryGetValue(imageFilename, out JsonObject? im))
                {
                    imagesMissingFromResults.Add(imageFilename);
                    continue;
                }
                List<object?>? detections = JsonValues.GetDetections(im);
                if (detections == null)
                {
                    continue;
                }
                detectionsThisSequence.AddRange(detections);
            }

            if (detectionsThisSequence.Count == 0)
            {
                continue;
            }

            SmoothingChanges? r = SmoothClassificationsForListOfDetections(detectionsThisSequence, context);
            if (r == null)
            {
                continue;
            }

            nDetectionsFlipped += r.DetectionsFlipped;
            nOtherClassificationsChanged += r.OtherClassificationsChanged;
            nTaxonomicClassificationChanges += r.TaxonomicChanges;
            nWithinFamilyChanges += r.WithinFamilyChanges;

            if (r.DetectionsFlipped > 0)
            {
                nSequencesChanged++;
            }
            if (r.OtherClassificationsChanged > 0)
            {
                nOtherSequencesChanged++;
            }
            if (r.TaxonomicChanges > 0)
            {
                nTaxonomicSequencesChanged++;
            }
            if (r.WithinFamilyChanges > 0)
            {
                nWithinFamilySequencesChanged++;
            }
        }

        progress?.Invoke(1.0);

        if (imagesMissingFromResults.Count > 0)
        {
            log.Warning($"{imagesMissingFromResults.Count} images in sequence records but not in results, for example:");
            foreach (string fn in imagesMissingFromResults.Take(10))
            {
                log.Warning(fn);
            }
        }

        log.Info($"Classification smoothing: changed {nDetectionsFlipped} detections in {nSequencesChanged} sequences");
        log.Info($"\"Other\" smoothing: changed {nOtherClassificationsChanged} detections in {nOtherSequencesChanged} sequences");
        log.Info($"Taxonomic smoothing: changed {nTaxonomicClassificationChanges} detections in {nTaxonomicSequencesChanged} sequences");
        log.Info($"Within-family smoothing: changed {nWithinFamilyChanges} detections in {nWithinFamilySequencesChanged} sequences");
    }

    #endregion
}
