using System.Text.Json;
using System.Text.Json.Serialization;
using SpeciesNetPostprocessor.Core.Smoothing;

namespace SpeciesNetPostprocessor.Core;

public enum TaxaListSource
{
    /// <summary>A taxa list that's built into the program</summary>
    BuiltIn,

    /// <summary>A user-supplied .csv file</summary>
    CsvFile
}

/// <summary>
/// All the settings for a postprocessing run.  Defaults match the defaults in MegaDetector's
/// manage_local_batch.py notebook.
/// </summary>
public sealed class PostprocessingOptions
{
    public string InputFile { get; set; } = string.Empty;

    /// <summary>
    /// If this is null or empty, output goes to [input]_processed.json.
    /// </summary>
    public string? OutputFile { get; set; }

    public bool RemoveClassificationsFromNonAnimals { get; set; } = true;

    public bool ApplyTaxaList { get; set; } = true;

    public TaxaListSource TaxaListSource { get; set; } = TaxaListSource.BuiltIn;

    /// <summary>
    /// Name of the built-in taxa list to use when TaxaListSource is BuiltIn.
    /// </summary>
    public string BuiltInTaxaList { get; set; } = BuiltInResources.DefaultTaxaListName;

    /// <summary>
    /// The .csv file to use when TaxaListSource is CsvFile.
    /// </summary>
    public string? TaxaListFile { get; set; }

    public bool ApplyWithinImageSmoothing { get; set; } = true;

    public bool ApplySequenceLevelSmoothing { get; set; } = true;

    /// <summary>
    /// Write a log file with debug information next to the output file.
    /// </summary>
    public bool WriteLogFile { get; set; } = true;

    public AdvancedOptions Advanced { get; set; } = new();

    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter(JsonNamingPolicy.SnakeCaseLower) }
    };

    public string ToJson() => JsonSerializer.Serialize(this, JsonOptions);

    public static PostprocessingOptions FromJson(string json) =>
        JsonSerializer.Deserialize<PostprocessingOptions>(json, JsonOptions) ?? new PostprocessingOptions();

    public PostprocessingOptions Clone() => FromJson(ToJson());
}

/// <summary>
/// Settings that most users shouldn't need to change.
/// </summary>
public sealed class AdvancedOptions
{
    public static readonly IReadOnlyList<string> DefaultOtherCategoryNames =
        new ClassificationSmoothingOptions().OtherCategoryNames;

    // Taxonomic mapping

    /// <summary>
    /// When mapping to the taxa list, if a parent taxon has only a single allowable child,
    /// map predictions of the parent to the child (e.g. if your list includes only one canid,
    /// map "canidae family" to that canid).
    /// </summary>
    public bool AllowWalkDown { get; set; }

    /// <summary>
    /// Use the "original_common" column of the taxa list for output names, when present.
    /// </summary>
    public bool UseOriginalCommonNamesIfAvailable { get; set; } = true;

    /// <summary>
    /// SpeciesNet taxonomy file to use instead of the built-in taxonomy (null for built-in).
    /// </summary>
    public string? TaxonomyFile { get; set; }

    // Smoothing

    public double ClassificationConfidenceThreshold { get; set; } = 0.5;

    public double DetectionConfidenceThreshold { get; set; } = 0.15;

    public int MinDetectionsToOverwriteSecondary { get; set; } = 4;

    public int MaxDetectionsNondominantClass { get; set; } = 1;

    public int MinDetectionsToOverwriteOther { get; set; } = 2;

    public List<string> OtherCategoryNames { get; set; } = [.. DefaultOtherCategoryNames];

    public bool PropagateClassificationsThroughTaxonomy { get; set; } = true;

    /// <summary>
    /// If two species from the same family are predicted in an image/sequence, make them all the
    /// more common species.  Don't enable this if multiple species from the same family can
    /// occur in the same image/sequence.
    /// </summary>
    public bool AllowSameFamilySmoothing { get; set; }

    public int MinDetectionsToOverwriteSecondarySameFamily { get; set; } = 2;

    public double TaxonomyPropagationLevelWeight { get; set; } = 1.0;

    public double TaxonomyPropagationCountWeight { get; set; } = 0.01;

    // Sequences

    /// <summary>
    /// Images from the same camera separated by this many seconds or less are in the same sequence.
    /// </summary>
    public double EpisodeIntervalSeconds { get; set; } = 60.0;

    // Output

    /// <summary>
    /// Add descriptive fields to each image (pre_filtering_description, post_filtering_description,
    /// pre_smoothing_description) summarizing classifications before each step.
    /// </summary>
    public bool IncludeDescriptionFields { get; set; } = true;

    /// <summary>
    /// Remove classification categories that aren't used in the output.
    /// </summary>
    public bool RemoveUnusedCategories { get; set; } = true;

    public ClassificationSmoothingOptions ToSmoothingOptions() => new()
    {
        ClassificationConfidenceThreshold = ClassificationConfidenceThreshold,
        DetectionConfidenceThreshold = DetectionConfidenceThreshold,
        MinDetectionsToOverwriteSecondary = MinDetectionsToOverwriteSecondary,
        MaxDetectionsNondominantClass = MaxDetectionsNondominantClass,
        MinDetectionsToOverwriteOther = MinDetectionsToOverwriteOther,
        OtherCategoryNames = [.. OtherCategoryNames],
        PropagateClassificationsThroughTaxonomy = PropagateClassificationsThroughTaxonomy,
        // This is how the notebook implements allow_same_family_smoothing
        MaxDetectionsNondominantClassSameFamily = AllowSameFamilySmoothing ? 10000 : -1,
        MinDetectionsToOverwriteSecondarySameFamily = MinDetectionsToOverwriteSecondarySameFamily,
        TaxonomyPropagationLevelWeight = TaxonomyPropagationLevelWeight,
        TaxonomyPropagationCountWeight = TaxonomyPropagationCountWeight,
        AddPreSmoothingDescription = IncludeDescriptionFields
    };
}
