using System.Globalization;
using SpeciesNetPostprocessor.Core;

namespace SpeciesNetPostprocessor;

/// <summary>
/// Dialog for editing AdvancedOptions.
/// </summary>
public partial class AdvancedOptionsForm : Form
{
    // The options we started from; settings that aren't exposed in this dialog come from here
    private AdvancedOptions _base;

    /// <summary>
    /// The edited options (valid after the dialog returns OK).
    /// </summary>
    public AdvancedOptions Options { get; private set; }

    public AdvancedOptionsForm(AdvancedOptions options)
    {
        InitializeComponent();

        _base = options;
        Options = options;

        toolTip.SetToolTip(checkAllowWalkDown,
            "If a taxon in the mapping list has only one allowed child taxon, map predictions of the parent to " +
            "the child.  For example, if the only canid in the list is the black-backed jackal, map \"canid " +
            "family\" predictions to black-backed jackal.  (allow_walk_down)");
        toolTip.SetToolTip(checkUseOriginalCommonNames,
            "If the mapping file has an \"original_common\" column, use those names for output categories, " +
            "rather than the names in the \"common\" column.  (use_original_common_names_if_available)");
        toolTip.SetToolTip(textTaxonomyFile,
            "The SpeciesNet taxonomy file (taxonomy_release.txt) used to look up relationships between taxa.  " +
            "Leave blank to use the taxonomy that is built into this program.");
        toolTip.SetToolTip(numericClassificationThreshold,
            "Classifications below this confidence are ignored during smoothing.  " +
            "(classification_confidence_threshold)");
        toolTip.SetToolTip(numericDetectionThreshold,
            "Detections below this confidence are ignored during smoothing.  (detection_confidence_threshold)");
        toolTip.SetToolTip(numericMinDetectionsToOverwriteSecondary,
            "How many detections of the most common category are required before less common categories are " +
            "changed to the most common category.  (min_detections_to_overwrite_secondary)");
        toolTip.SetToolTip(numericMaxDetectionsNondominantClass,
            "Categories with more than this many detections are left alone, even if another category is more " +
            "common.  If this is 1, an image with 20 zebras and 1 horse becomes 21 zebras, but an image with " +
            "20 zebras and 2 horses is left alone.  (max_detections_nondominant_class)");
        toolTip.SetToolTip(numericMinDetectionsToOverwriteOther,
            "How many detections of the most common category are required before \"other\" categories (e.g. " +
            "\"animal\", \"blank\") are changed to the most common category.  (min_detections_to_overwrite_other)");
        toolTip.SetToolTip(textOtherCategoryNames,
            "Category names that are treated as uninformative, and are changed to the most common category " +
            "when possible.  (other_category_names)");
        toolTip.SetToolTip(checkPropagateThroughTaxonomy,
            "If an image/sequence has predictions at different taxonomic levels, change higher-level predictions " +
            "to lower-level predictions, e.g. \"canid family\" predictions become \"black-backed jackal\" if there " +
            "are also black-backed jackal predictions.  (propagate_classifications_through_taxonomy)");
        toolTip.SetToolTip(checkAllowSameFamilySmoothing,
            "If two species from the same family are predicted in the same image/sequence, change the less " +
            "common species to the more common species.  Don't enable this if multiple species from the same " +
            "family often occur together.  (allow_same_family_smoothing)");
        toolTip.SetToolTip(numericMinDetectionsSameFamily,
            "(min_detections_to_overwrite_secondary_same_family)");
        toolTip.SetToolTip(numericEpisodeInterval,
            "Images from the same camera (folder) that are this close together in time (or closer) are " +
            "considered part of the same sequence.  (episode_interval_seconds)");
        toolTip.SetToolTip(checkIncludeDescriptionFields,
            "Add pre_filtering_description, post_filtering_description, and pre_smoothing_description fields to " +
            "each image, summarizing classifications before each processing step.  These are useful for " +
            "reviewing results, and are ignored by most tools.");
        toolTip.SetToolTip(checkRemoveUnusedCategories,
            "Remove classification categories that aren't used by any detection in the output.");

        ApplyToControls(options);
    }

    private static decimal ToDecimal(double d, NumericUpDown control) =>
        Math.Clamp((decimal)d, control.Minimum, control.Maximum);

    // Converting via a string makes sure we get the double closest to the displayed value
    private static double ToDouble(NumericUpDown control) =>
        double.Parse(control.Value.ToString(CultureInfo.InvariantCulture), CultureInfo.InvariantCulture);

    private void ApplyToControls(AdvancedOptions options)
    {
        checkAllowWalkDown.Checked = options.AllowWalkDown;
        checkUseOriginalCommonNames.Checked = options.UseOriginalCommonNamesIfAvailable;
        textTaxonomyFile.Text = options.TaxonomyFile ?? string.Empty;

        numericClassificationThreshold.Value = ToDecimal(options.ClassificationConfidenceThreshold,
                                                         numericClassificationThreshold);
        numericDetectionThreshold.Value = ToDecimal(options.DetectionConfidenceThreshold, numericDetectionThreshold);
        numericMinDetectionsToOverwriteSecondary.Value = ToDecimal(options.MinDetectionsToOverwriteSecondary,
                                                                   numericMinDetectionsToOverwriteSecondary);
        numericMaxDetectionsNondominantClass.Value = ToDecimal(options.MaxDetectionsNondominantClass,
                                                               numericMaxDetectionsNondominantClass);
        numericMinDetectionsToOverwriteOther.Value = ToDecimal(options.MinDetectionsToOverwriteOther,
                                                               numericMinDetectionsToOverwriteOther);
        textOtherCategoryNames.Text = string.Join(", ", options.OtherCategoryNames);
        checkPropagateThroughTaxonomy.Checked = options.PropagateClassificationsThroughTaxonomy;
        checkAllowSameFamilySmoothing.Checked = options.AllowSameFamilySmoothing;
        numericMinDetectionsSameFamily.Value = ToDecimal(options.MinDetectionsToOverwriteSecondarySameFamily,
                                                         numericMinDetectionsSameFamily);

        numericEpisodeInterval.Value = ToDecimal(options.EpisodeIntervalSeconds, numericEpisodeInterval);

        checkIncludeDescriptionFields.Checked = options.IncludeDescriptionFields;
        checkRemoveUnusedCategories.Checked = options.RemoveUnusedCategories;

        UpdateControlState();
    }

    private AdvancedOptions ReadFromControls()
    {
        string json = System.Text.Json.JsonSerializer.Serialize(_base, PostprocessingOptions.JsonOptions);
        AdvancedOptions options = System.Text.Json.JsonSerializer.Deserialize<AdvancedOptions>(
            json, PostprocessingOptions.JsonOptions)!;

        options.AllowWalkDown = checkAllowWalkDown.Checked;
        options.UseOriginalCommonNamesIfAvailable = checkUseOriginalCommonNames.Checked;
        string taxonomyFile = textTaxonomyFile.Text.Trim().Trim('"');
        options.TaxonomyFile = taxonomyFile.Length > 0 ? taxonomyFile : null;

        options.ClassificationConfidenceThreshold = ToDouble(numericClassificationThreshold);
        options.DetectionConfidenceThreshold = ToDouble(numericDetectionThreshold);
        options.MinDetectionsToOverwriteSecondary = (int)numericMinDetectionsToOverwriteSecondary.Value;
        options.MaxDetectionsNondominantClass = (int)numericMaxDetectionsNondominantClass.Value;
        options.MinDetectionsToOverwriteOther = (int)numericMinDetectionsToOverwriteOther.Value;
        options.OtherCategoryNames = textOtherCategoryNames.Text
            .Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .ToList();
        options.PropagateClassificationsThroughTaxonomy = checkPropagateThroughTaxonomy.Checked;
        options.AllowSameFamilySmoothing = checkAllowSameFamilySmoothing.Checked;
        options.MinDetectionsToOverwriteSecondarySameFamily = (int)numericMinDetectionsSameFamily.Value;

        options.EpisodeIntervalSeconds = ToDouble(numericEpisodeInterval);

        options.IncludeDescriptionFields = checkIncludeDescriptionFields.Checked;
        options.RemoveUnusedCategories = checkRemoveUnusedCategories.Checked;

        return options;
    }

    private void UpdateControlState()
    {
        labelMinDetectionsSameFamily.Enabled = checkAllowSameFamilySmoothing.Checked;
        numericMinDetectionsSameFamily.Enabled = checkAllowSameFamilySmoothing.Checked;
    }

    private void checkAllowSameFamilySmoothing_CheckedChanged(object? sender, EventArgs e) => UpdateControlState();

    private void buttonBrowseTaxonomyFile_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a SpeciesNet taxonomy file",
            Filter = "Text files (*.txt)|*.txt|All files (*.*)|*.*"
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textTaxonomyFile.Text = dialog.FileName;
        }
    }

    private void buttonRestoreDefaults_Click(object? sender, EventArgs e)
    {
        _base = new AdvancedOptions();
        ApplyToControls(_base);
    }

    private void buttonOK_Click(object? sender, EventArgs e)
    {
        string taxonomyFile = textTaxonomyFile.Text.Trim().Trim('"');
        if (taxonomyFile.Length > 0 && !File.Exists(taxonomyFile))
        {
            MessageBox.Show(this, $"The taxonomy file doesn't exist:\n\n{taxonomyFile}", "SpeciesNet Postprocessor",
                            MessageBoxButtons.OK, MessageBoxIcon.Error);
            return;
        }

        Options = ReadFromControls();
        DialogResult = DialogResult.OK;
        Close();
    }
}
