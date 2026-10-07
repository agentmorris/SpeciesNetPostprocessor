using SpeciesNetPostprocessor.Core.Json;
using SpeciesNetPostprocessor.Core.Smoothing;

namespace SpeciesNetPostprocessor.Core.Taxonomy;

/// <summary>
/// Options for TaxaListRestriction.RestrictToTaxaList().
/// </summary>
public sealed class TaxaListRestrictionOptions
{
    /// <summary>
    /// Should we walk down the taxonomy tree when making mappings if a parent has only a single
    /// allowable child?  For example, if only a single felid species is allowed, should other
    /// felid predictions be mapped to that species, as opposed to being mapped to the family?
    /// </summary>
    public bool AllowWalkDown { get; set; }

    /// <summary>
    /// Should we add a field to each image that summarizes its classifications prior to
    /// taxonomic restriction?
    /// </summary>
    public bool AddPreFilteringDescription { get; set; } = true;

    /// <summary>
    /// Should we add a field to each image that summarizes its classifications after
    /// taxonomic restriction?
    /// </summary>
    public bool AddPostFilteringDescription { get; set; } = true;

    /// <summary>
    /// If false, we'll raise an error if the same latin name appears twice in the taxonomy
    /// list; if true, we'll log a warning and keep only the first entry for that name.
    /// </summary>
    public bool AllowRedundantLatinNames { get; set; } = true;

    /// <summary>
    /// These categories should be unmodified, even if they aren't used, or have the same
    /// taxonomic description as other categories.
    /// </summary>
    public List<string>? ProtectedCommonNames { get; set; }

    /// <summary>
    /// If an "original_common" column is present in the taxa list, use those common names
    /// instead of the ones in the "common" column.
    /// </summary>
    public bool UseOriginalCommonNamesIfAvailable { get; set; } = true;

    /// <summary>
    /// Should we combine categories with the same common name?
    /// </summary>
    public bool CombineRedundantCategories { get; set; } = true;
}

/// <summary>
/// Restricts SpeciesNet output to a custom list of taxa (port of restrict_to_taxa_list() and
/// combine_redundant_classification_categories() in
/// megadetector.postprocessing.classification_postprocessing).
/// </summary>
public static class TaxaListRestriction
{
    /// <summary>
    /// Reads a taxa list .csv file (with at least the columns "latin" and "common"), returning
    /// a map from (lowercase) latin names to output common names, in file order.
    /// </summary>
    public static OrderedDictionary<string, string> ReadTaxaList(string csvText,
                                                                 string sourceName,
                                                                 TaxaListRestrictionOptions options,
                                                                 IPostprocessingLog log)
    {
        CsvTable table = CsvTable.Parse(csvText, sourceName);

        foreach (string s in new[] { "latin", "common" })
        {
            if (!table.Columns.Contains(s))
            {
                throw new PostprocessingException($"Required column {s} missing from taxonomy list file {sourceName}");
            }
        }

        bool hasOriginalCommon = table.Columns.Contains("original_common");

        // Maps target latin names to target common names
        var targetLatinToCommon = new OrderedDictionary<string, string>(StringComparer.Ordinal);

        int nRowsWithoutLatinName = 0;
        var duplicateLatinNames = new List<string>();

        foreach (Dictionary<string, string> row in table.Rows)
        {
            string latin = row["latin"].ToLowerInvariant();
            string common = row["common"].ToLowerInvariant();

            if (latin.Length == 0)
            {
                string rowString = string.Join(", ", table.Columns.Select(c => $"'{c}': '{row[c]}'"));
                log.Debug($"Skipping row with empty \"latin\" column in {sourceName}: {{{rowString}}}");
                nRowsWithoutLatinName++;
                continue;
            }

            if (options.UseOriginalCommonNamesIfAvailable && hasOriginalCommon &&
                row["original_common"].Trim().Length > 0)
            {
                common = row["original_common"].Trim().ToLowerInvariant();
            }

            // Valid latin names have either one token (e.g. "canidae"),
            // two tokens (e.g. "bos taurus"), or three tokens (e.g. "canis lupus familiaris")
            int nTokens = latin.Split(' ').Length;
            if (nTokens < 1 || nTokens > 3)
            {
                throw new PostprocessingException($"Illegal binomial name {latin} in taxonomy list {sourceName}");
            }

            if (targetLatinToCommon.TryGetValue(latin, out string? existingCommon))
            {
                string errorString = $"scientific name {latin} appears multiple times in the taxonomy list";
                if (options.AllowRedundantLatinNames)
                {
                    log.Debug($"{errorString}, keeping the first entry ({existingCommon}) and ignoring ({common})");
                    if (!duplicateLatinNames.Contains(latin))
                    {
                        duplicateLatinNames.Add(latin);
                    }
                    continue;
                }
                throw new PostprocessingException(errorString);
            }

            targetLatinToCommon[latin] = common;
        }

        log.Info($"Read {targetLatinToCommon.Count} taxa from {sourceName}");
        if (nRowsWithoutLatinName > 0)
        {
            log.Info($"Skipped {nRowsWithoutLatinName} rows with no scientific name");
        }
        if (duplicateLatinNames.Count > 0)
        {
            log.Info("Some scientific names appear more than once in the taxonomy list, keeping the first " +
                     $"entry for each: {string.Join(", ", duplicateLatinNames)}");
        }

        return targetLatinToCommon;
    }

    /// <summary>
    /// The SpeciesNet taxonomy, indexed by latin name.
    /// </summary>
    private sealed class SpeciesNetTaxonomyIndex
    {
        // Maps the latin name of every taxon to the corresponding full taxon string, with the GUID
        // replaced by "guid".  For species, the key is a binomial name.
        public readonly OrderedDictionary<string, string> LatinNameToTaxonString = new(StringComparer.Ordinal);
        public readonly OrderedDictionary<string, string> CommonNameToTaxonString = new(StringComparer.Ordinal);

        /// <summary>
        /// Inserts the latin name represented by [s] (the lowest-level non-empty token, or the
        /// genus+species if all tokens are populated) into LatinNameToTaxonString, and the
        /// common name into CommonNameToTaxonString.
        /// </summary>
        public void Insert(string s)
        {
            string[] tokens = s.Split(';');
            if (tokens.Length != 7)
            {
                throw new PostprocessingException($"Illegal taxonomy string {s}");
            }

            tokens[0] = "guid";
            string className = tokens[1];
            string order = tokens[2];
            string family = tokens[3];
            string genus = tokens[4];
            string species = tokens[5];
            string commonName = tokens[6];

            s = string.Join(';', tokens);

            if (className.Length == 0)
            {
                if (commonName is not ("animal" or "vehicle" or "blank"))
                {
                    throw new PostprocessingException($"Illegal common name {commonName} in taxonomy string {s}");
                }
                return;
            }

            if (species.Length > 0)
            {
                if (genus.Length == 0 || family.Length == 0 || order.Length == 0)
                {
                    throw new PostprocessingException($"Higher-level taxa missing for {s}: {genus},{family},{order}");
                }
                TryAdd(LatinNameToTaxonString, genus + " " + species, s);
            }
            else if (genus.Length > 0)
            {
                if (family.Length == 0 || order.Length == 0)
                {
                    throw new PostprocessingException($"Higher-level taxa missing for {s}: {family},{order}");
                }
                TryAdd(LatinNameToTaxonString, genus, s);
            }
            else if (family.Length > 0)
            {
                if (order.Length == 0)
                {
                    throw new PostprocessingException($"Higher-level taxa missing for {s}: {order}");
                }
                TryAdd(LatinNameToTaxonString, family, s);
            }
            else if (order.Length > 0)
            {
                TryAdd(LatinNameToTaxonString, order, s);
            }
            else
            {
                TryAdd(LatinNameToTaxonString, className, s);
            }

            if (commonName.Length > 0)
            {
                TryAdd(CommonNameToTaxonString, commonName, s);
            }
        }

        private static void TryAdd(OrderedDictionary<string, string> d, string key, string value)
        {
            if (!d.ContainsKey(key))
            {
                d.Add(key, value);
            }
        }
    }

    private static SpeciesNetTaxonomyIndex LoadTaxonomy(string taxonomyText, IPostprocessingLog log)
    {
        var index = new SpeciesNetTaxonomyIndex();

        var lines = taxonomyText.Split('\n')
            .Select(s => s.Trim())
            .Where(s => s.Length > 0)
            .ToList();

        foreach (string s in lines)
        {
            index.Insert(s);
        }

        // Make sure all parent taxa are represented in the taxonomy

        // In theory any taxon that appears as the parent of another taxon should
        // also be in the taxonomy, but this isn't always true, so we fix it here.
        var newTaxonStringToMissingTokens = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        foreach (var kvp in index.LatinNameToTaxonString)
        {
            string latinName = kvp.Key;
            if (latinName.Contains("no cv result", StringComparison.Ordinal))
            {
                continue;
            }

            string[] tokens = kvp.Value.Split(';');

            // Don't process GUID, species, or common name
            for (int iToken = 1; iToken < tokens.Length - 2; iToken++)
            {
                string testToken = tokens[iToken];
                if (testToken.Length == 0)
                {
                    continue;
                }

                // Do we need to make up a taxon for this token?
                if (!index.LatinNameToTaxonString.ContainsKey(testToken))
                {
                    string[] newTokens = Enumerable.Repeat(string.Empty, 7).ToArray();
                    newTokens[0] = "guid";
                    for (int iCopyToken = 1; iCopyToken <= iToken; iCopyToken++)
                    {
                        newTokens[iCopyToken] = tokens[iCopyToken];
                    }
                    newTokens[^1] = testToken + " species";
                    string newTaxonString = string.Join(';', newTokens);
                    if (!newTaxonStringToMissingTokens.TryGetValue(newTaxonString, out List<string>? missing))
                    {
                        missing = new List<string>();
                        newTaxonStringToMissingTokens[newTaxonString] = missing;
                    }
                    missing.Add(testToken);
                }
            }
        }

        var newTaxonStrings = newTaxonStringToMissingTokens.Keys.ToList();
        newTaxonStrings.Sort(StringComparer.Ordinal);

        if (newTaxonStrings.Count > 0)
        {
            log.Debug($"Found {newTaxonStrings.Count} taxa that need to be inserted to make the taxonomy valid");
        }

        foreach (string newTaxonString in newTaxonStrings)
        {
            index.Insert(newTaxonString);
        }

        return index;
    }

    /// <summary>
    /// Given results [d] (modified in place), likely without having had a geofence applied,
    /// applies a custom taxa list.
    /// </summary>
    /// <param name="targetLatinToCommon">the taxa list, from ReadTaxaList()</param>
    /// <param name="taxonomyText">contents of the SpeciesNet taxonomy file (with 7-token entries)</param>
    public static void RestrictToTaxaList(JsonObject d,
                                          OrderedDictionary<string, string> targetLatinToCommon,
                                          string taxonomyText,
                                          TaxaListRestrictionOptions options,
                                          IPostprocessingLog log,
                                          Action<double>? progress = null,
                                          CancellationToken cancellationToken = default)
    {
        SpeciesNetTaxonomyIndex taxonomy = LoadTaxonomy(taxonomyText, log);
        var speciesnetLatinNameToTaxonString = taxonomy.LatinNameToTaxonString;

        // Store custom common name mappings based on the taxonomy list

        var speciesnetLatinNameToOutputCommonName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (string latinName in speciesnetLatinNameToTaxonString.Keys)
        {
            if (targetLatinToCommon.TryGetValue(latinName, out string? common))
            {
                speciesnetLatinNameToOutputCommonName[latinName] = common;
            }
        }

        // Make sure all taxa on the allow-list are in the taxonomy

        var failedMappings = new List<string>();
        foreach (var kvp in targetLatinToCommon)
        {
            if (!speciesnetLatinNameToTaxonString.ContainsKey(kvp.Key))
            {
                string s = $"{kvp.Key} ({kvp.Value}) not in SpeciesNet taxonomy";
                if (taxonomy.CommonNameToTaxonString.TryGetValue(kvp.Value, out string? taxonString))
                {
                    s += $" (common name maps to {taxonString})";
                }
                failedMappings.Add(s);
            }
        }

        if (failedMappings.Count > 0)
        {
            foreach (string s in failedMappings)
            {
                log.Error(s);
            }
            throw new PostprocessingException(
                $"Cannot continue with taxonomic restriction: {failedMappings.Count} taxa in the taxonomy " +
                "list are not in the SpeciesNet taxonomy (see the log for details)");
        }

        // For the allow-list, map each parent taxon to a set of allowable child taxa

        // Maps parent names to all allowed child names, or {null} if this is the
        // lowest-level allowable taxon on this path
        var allowedParentTaxonToChildTaxa = new Dictionary<string, HashSet<string?>>(StringComparer.Ordinal);

        HashSet<string?> GetOrCreateChildren(string parent)
        {
            if (!allowedParentTaxonToChildTaxa.TryGetValue(parent, out HashSet<string?>? children))
            {
                children = new HashSet<string?>();
                allowedParentTaxonToChildTaxa[parent] = children;
            }
            return children;
        }

        foreach (string latinName in targetLatinToCommon.Keys)
        {
            string taxonString = speciesnetLatinNameToTaxonString[latinName];

            // Remove GUID and common name; this is now always class/order/family/genus/species
            string[] tokens = taxonString.Split(';')[1..^1];

            string? childTaxon = null;

            // If this is a species
            if (tokens[^1].Length > 0)
            {
                string binomialName = tokens[^2] + " " + tokens[^1];
                if (binomialName != latinName)
                {
                    throw new PostprocessingException($"Binomial/latin mismatch: {binomialName} vs {latinName}");
                }
                // If this already exists, it should only allow null
                if (allowedParentTaxonToChildTaxa.TryGetValue(binomialName, out HashSet<string?>? existing))
                {
                    if (existing.Count != 1 || !existing.Contains(null))
                    {
                        throw new PostprocessingException($"Species-level entry {binomialName} has non-leaf children");
                    }
                }
                GetOrCreateChildren(binomialName).Add(null);
                childTaxon = binomialName;
            }

            // The first level that can ever be a parent taxon is the genus level
            int parentTokenIndex = tokens.Length - 2;

            // Walk up from genus to class
            while (parentTokenIndex >= 0)
            {
                string parentTaxon = tokens[parentTokenIndex];

                // Don't create entries for blank taxa
                if (parentTaxon.Length > 0)
                {
                    bool createChild = true;

                    // This is the lowest-level taxon in this entry
                    if (childTaxon == null)
                    {
                        // ...but we don't want to remove existing children from any parents
                        if (allowedParentTaxonToChildTaxa.TryGetValue(parentTaxon, out HashSet<string?>? children) &&
                            children.Count > 0)
                        {
                            log.Debug($"Not creating empty child for parent {parentTaxon} " +
                                      $"(already has children {{{string.Join(", ", children.Select(c => c ?? "None"))}}})");
                            createChild = false;
                        }
                    }
                    // If we're adding a new child entry, clear out any leaf node markers
                    else
                    {
                        if (allowedParentTaxonToChildTaxa.TryGetValue(parentTaxon, out HashSet<string?>? children) &&
                            children.Contains(null))
                        {
                            if (children.Count != 1)
                            {
                                throw new PostprocessingException("Illegal parent/child configuration");
                            }
                            log.Debug($"Un-marking parent {parentTaxon} as a leaf node because of child {childTaxon}");
                            allowedParentTaxonToChildTaxa[parentTaxon] = new HashSet<string?>();
                        }
                    }

                    if (createChild)
                    {
                        GetOrCreateChildren(parentTaxon).Add(childTaxon);
                    }

                    childTaxon = parentTaxon;
                }

                parentTokenIndex--;
            }
        }

        foreach (var kvp in allowedParentTaxonToChildTaxa)
        {
            // null should only ever appear alone; this marks a leaf node with no children
            if (kvp.Value.Contains(null) && kvp.Value.Count != 1)
            {
                throw new PostprocessingException("null should only appear alone in a child taxon list");
            }
        }

        // Map all predictions that exist in this dataset to the prediction we should generate

        OrderedDictionary<string, string> inputCategoryIdToCommonName = JsonValues.GetStringMap(d, "classification_categories");
        OrderedDictionary<string, string> inputCategoryIdToTaxonomyString =
            JsonValues.GetStringMap(d, "classification_category_descriptions");

        var inputCategoryIdToOutputTaxonString = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        var taxaNotInTaxonomy = new SortedSet<string>(StringComparer.Ordinal);

        foreach (var kvp in inputCategoryIdToTaxonomyString)
        {
            string inputCategoryId = kvp.Key;

            // Only the first of several "|"-delimited descriptions is used
            string inputTaxonString = SpeciesNetTaxonomy.FirstDescription(kvp.Value);
            string[] inputTaxonTokens = inputTaxonString.Split(';');
            if (inputTaxonTokens.Length != 7)
            {
                throw new PostprocessingException($"Illegal taxonomy string: {inputTaxonString}");
            }

            // Don't mess with blank/no-cv-result/human (or "animal", which is really "unknown")
            if (!SpeciesNetTaxonomy.IsTaxonomicPredictionString(inputTaxonString) ||
                SpeciesNetTaxonomy.IsHumanPredictionString(inputTaxonString))
            {
                log.Debug($"Not messing with non-taxonomic category {inputTaxonString}");
                inputCategoryIdToOutputTaxonString[inputCategoryId] = inputTaxonString;
                continue;
            }

            // Don't mess with protected categories
            string commonName = inputTaxonTokens[^1];

            if (options.ProtectedCommonNames != null && options.ProtectedCommonNames.Contains(commonName))
            {
                log.Debug($"Not messing with protected category {commonName}: {inputTaxonString}");
                inputCategoryIdToOutputTaxonString[inputCategoryId] = inputTaxonString;
                continue;
            }

            // Remove GUID and common name; this is always class/order/family/genus/species
            inputTaxonTokens = inputTaxonTokens[1..^1];

            // Start at the species level (the last element in inputTaxonTokens),
            // and see whether each taxon is allowed
            int testIndex = inputTaxonTokens.Length - 1;
            string? targetTaxon = null;

            while (testIndex >= 0 && targetTaxon == null)
            {
                // Species are represented as binomial names, i.e. when testIndex is 4,
                // testTaxonName will have two tokens (e.g. "canis lupus"), otherwise
                // testTaxonName will have one token (e.g. "canis", or "aves")
                string testTaxonName;
                if (testIndex == inputTaxonTokens.Length - 1 && inputTaxonTokens[^1].Length > 0)
                {
                    testTaxonName = inputTaxonTokens[^2] + " " + inputTaxonTokens[^1];
                }
                else
                {
                    testTaxonName = inputTaxonTokens[testIndex];
                }

                // If we haven't yet found the level at which this taxon is non-empty, keep going up
                if (testTaxonName.Length == 0)
                {
                    testIndex--;
                    continue;
                }

                // The Python code asserts that every taxon is in the taxonomy; we just note it
                // and keep walking up the tree (a taxon that isn't in the taxonomy can't be in
                // the allow-list).
                if (!speciesnetLatinNameToTaxonString.ContainsKey(testTaxonName))
                {
                    taxaNotInTaxonomy.Add(testTaxonName);
                }

                // Is this taxon allowed according to the custom species list?
                if (allowedParentTaxonToChildTaxa.TryGetValue(testTaxonName, out HashSet<string?>? allowedChildTaxa))
                {
                    // If this is the lowest-level allowable token or there is not a
                    // unique child, don't walk any further, even if walking down
                    // is enabled.
                    if (allowedChildTaxa.Contains(null) || allowedChildTaxa.Count > 1)
                    {
                        targetTaxon = testTaxonName;
                    }
                    else if (!options.AllowWalkDown)
                    {
                        targetTaxon = testTaxonName;
                    }
                    else
                    {
                        string? candidateTaxon = null;
                        // If there's a unique child, walk back *down* the allowable
                        // taxa until we run out of unique children
                        while (allowedChildTaxa.Count == 1 && allowedChildTaxa.First() != null)
                        {
                            candidateTaxon = allowedChildTaxa.First()!;
                            if (!allowedParentTaxonToChildTaxa.TryGetValue(candidateTaxon, out allowedChildTaxa))
                            {
                                throw new PostprocessingException($"{candidateTaxon} should be in the allowed taxa list");
                            }
                        }
                        targetTaxon = candidateTaxon ?? throw new PostprocessingException("Empty candidate taxon");
                    }
                }

                testIndex--;
            }

            string outputTaxonString = targetTaxon == null
                ? SpeciesNetTaxonomy.AnimalPredictionString
                : speciesnetLatinNameToTaxonString[targetTaxon];
            inputCategoryIdToOutputTaxonString[inputCategoryId] = outputTaxonString;
        }

        if (taxaNotInTaxonomy.Count > 0)
        {
            log.Warning($"{taxaNotInTaxonomy.Count} taxa in this file are not in the SpeciesNet taxonomy: " +
                        string.Join(", ", taxaNotInTaxonomy));
        }

        foreach (var kvp in inputCategoryIdToOutputTaxonString)
        {
            log.Debug($"Mapping {kvp.Key} ({inputCategoryIdToCommonName[kvp.Key]}) to {kvp.Value}");
        }

        // Map input category IDs to output category IDs

        // Equivalent to Python's invert_dictionary(), i.e. last wins for duplicate values
        var speciesnetTaxonStringToLatinName = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in speciesnetLatinNameToTaxonString)
        {
            speciesnetTaxonStringToLatinName[kvp.Value] = kvp.Key;
        }

        var inputCategoryIdToOutputCategoryId = new Dictionary<string, string>(StringComparer.Ordinal);
        var outputTaxonStringToCategoryId = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        var outputCategoryIdToCommonName = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        var substitutionsLogged = new HashSet<string>(StringComparer.Ordinal);

        foreach (var kvp in inputCategoryIdToOutputTaxonString)
        {
            string inputCategoryId = kvp.Key;
            string outputTaxonString = kvp.Value;

            string outputCommonName = outputTaxonString.Split(';')[^1];

            // Possibly substitute a custom common name
            if (speciesnetTaxonStringToLatinName.TryGetValue(outputTaxonString, out string? speciesnetLatinName) &&
                speciesnetLatinNameToOutputCommonName.TryGetValue(speciesnetLatinName, out string? customCommonName) &&
                customCommonName != outputCommonName)
            {
                string substitutionName = customCommonName + " --> " + outputCommonName;
                if (substitutionsLogged.Add(substitutionName))
                {
                    log.Debug($"Substituting common name {customCommonName} for {outputCommonName}");
                }
                outputCommonName = customCommonName;
            }

            // Do we need to create a new output category?
            if (!outputTaxonStringToCategoryId.TryGetValue(outputTaxonString, out string? outputCategoryId))
            {
                outputCategoryId = outputTaxonStringToCategoryId.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
                outputTaxonStringToCategoryId.Add(outputTaxonString, outputCategoryId);
                outputCategoryIdToCommonName.Add(outputCategoryId, outputCommonName);
            }

            inputCategoryIdToOutputCategoryId[inputCategoryId] = outputCategoryId;
        }

        // Remap all category labels

        var outputCategoryIdToTaxonString = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in outputTaxonStringToCategoryId)
        {
            outputCategoryIdToTaxonString[kvp.Value] = kvp.Key;
        }

        // These are only used for the pre/post-filtering descriptions, which always use the
        // default thresholds
        var descriptionOptions = new ClassificationSmoothingOptions();
        var classificationDescriptions = new Dictionary<string, string>(inputCategoryIdToTaxonomyString, StringComparer.Ordinal);
        var outputDescriptions = new Dictionary<string, string>(outputCategoryIdToTaxonString, StringComparer.Ordinal);

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
            if (detections == null)
            {
                continue;
            }

            // Possibly prepare a pre-filtering description
            if (options.AddPreFilteringDescription)
            {
                var categoryToCount = ClassificationSmoothing.CountDetectionsByClassificationCategory(
                    detections, descriptionOptions, null);
                im["pre_filtering_description"] =
                    ClassificationSmoothing.GetClassificationDescriptionString(categoryToCount, classificationDescriptions);
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
                    var classification = (List<object?>)classificationObj!;
                    classification[0] = inputCategoryIdToOutputCategoryId[(string)classification[0]!];
                }
            }

            if (options.AddPostFilteringDescription)
            {
                var categoryToCount = ClassificationSmoothing.CountDetectionsByClassificationCategory(
                    detections, descriptionOptions, null);
                im["post_filtering_description"] =
                    ClassificationSmoothing.GetClassificationDescriptionString(categoryToCount, outputDescriptions);
            }
        }

        progress?.Invoke(1.0);

        d["classification_categories"] = JsonValues.ToJsonObject(outputCategoryIdToCommonName);
        d["classification_category_descriptions"] = JsonValues.ToJsonObject(outputCategoryIdToTaxonString);

        log.Info($"Mapped {inputCategoryIdToOutputTaxonString.Count} input categories to " +
                 $"{outputCategoryIdToCommonName.Count} output categories");

        if (options.CombineRedundantCategories)
        {
            CombineRedundantClassificationCategories(d, log);
        }
    }

    /// <summary>
    /// If [d] contains multiple categories with the same category name, merges each equivalence
    /// class into a single category (modifies [d] in place).
    /// </summary>
    /// <param name="classificationThreshold">only used when sorting descriptions by count</param>
    public static void CombineRedundantClassificationCategories(JsonObject d,
                                                                IPostprocessingLog log,
                                                                double classificationThreshold = 0.5)
    {
        OrderedDictionary<string, string> classificationCategories = JsonValues.GetStringMap(d, "classification_categories");

        var inputCategoryNameToIds = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
        foreach (var kvp in classificationCategories)
        {
            if (!inputCategoryNameToIds.TryGetValue(kvp.Value, out List<string>? ids))
            {
                ids = new List<string>();
                inputCategoryNameToIds.Add(kvp.Value, ids);
            }
            ids.Add(kvp.Key);
        }

        // Return early if there are no redundant categories
        int maxCount = inputCategoryNameToIds.Values.Select(ids => ids.Count).DefaultIfEmpty(0).Max();
        if (maxCount <= 1)
        {
            return;
        }

        // Map input category IDs to output category IDs

        var inputCategoryIdToOutputCategoryId = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        int iCategory = 0;
        foreach (var kvp in inputCategoryNameToIds)
        {
            string outputCategoryId = iCategory.ToString(System.Globalization.CultureInfo.InvariantCulture);
            foreach (string inputCategoryId in kvp.Value)
            {
                inputCategoryIdToOutputCategoryId[inputCategoryId] = outputCategoryId;
            }
            iCategory++;
        }

        int nInputCategories = classificationCategories.Count;
        int nOutputCategories = inputCategoryNameToIds.Count;
        log.Info($"Removing {nInputCategories - nOutputCategories} redundant categories");

        // Create a new category dict

        var outputCategoryNameToId = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in inputCategoryIdToOutputCategoryId)
        {
            string categoryName = classificationCategories[kvp.Key];
            outputCategoryNameToId.TryAdd(categoryName, kvp.Value);
        }

        // Create new classification category descriptions

        if (d.Get("classification_category_descriptions") is JsonObject)
        {
            OrderedDictionary<string, string> descriptions =
                JsonValues.GetStringMap(d, "classification_category_descriptions");

            if (descriptions.Count != classificationCategories.Count)
            {
                throw new PostprocessingException(
                    $"Mismatch between {descriptions.Count} descriptions and {classificationCategories.Count} categories");
            }

            // Sort descriptions by count overall, so we can sort by description within categories later
            var descriptionToCount = new Dictionary<string, int>(StringComparer.Ordinal);
            foreach (string description in descriptions.Values)
            {
                descriptionToCount[description] = 0;
            }

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
                    if (classifications == null || classifications.Count == 0)
                    {
                        continue;
                    }
                    var top = (List<object?>)classifications[0]!;
                    if (JsonValues.ToDouble(top[1]) < classificationThreshold)
                    {
                        continue;
                    }
                    descriptionToCount[descriptions[(string)top[0]!]]++;
                }
            }

            // Create descriptions for the output categories
            var outputCategoryIdToDescriptions = new OrderedDictionary<string, List<string>>(StringComparer.Ordinal);
            foreach (var kvp in inputCategoryIdToOutputCategoryId)
            {
                if (!outputCategoryIdToDescriptions.TryGetValue(kvp.Value, out List<string>? list))
                {
                    list = new List<string>();
                    outputCategoryIdToDescriptions.Add(kvp.Value, list);
                }
                list.Add(descriptions[kvp.Key]);
            }

            var outputClassificationCategoryDescriptions = new OrderedDictionary<string, string>(StringComparer.Ordinal);
            foreach (var kvp in outputCategoryIdToDescriptions)
            {
                List<string> descriptionList = kvp.Value;
                // If this output category combines multiple input categories, sort descriptions
                // in descending order by count (stable, like Python's list.sort())
                if (descriptionList.Count > 1)
                {
                    descriptionList = descriptionList.OrderByDescending(x => descriptionToCount[x]).ToList();
                }
                outputClassificationCategoryDescriptions[kvp.Key] = string.Join('|', descriptionList);
            }

            d["classification_category_descriptions"] = JsonValues.ToJsonObject(outputClassificationCategoryDescriptions);
        }

        var outputCategoryIdToName = new OrderedDictionary<string, string>(StringComparer.Ordinal);
        foreach (var kvp in outputCategoryNameToId)
        {
            outputCategoryIdToName[kvp.Value] = kvp.Key;
        }
        d["classification_categories"] = JsonValues.ToJsonObject(outputCategoryIdToName);

        // Remap classifications
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
                    var classification = (List<object?>)classificationObj!;
                    classification[0] = inputCategoryIdToOutputCategoryId[(string)classification[0]!];
                }
            }
        }
    }
}
