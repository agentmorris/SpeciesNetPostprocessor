namespace SpeciesNetPostprocessor;

partial class AdvancedOptionsForm
{
    /// <summary>
    ///  Required designer variable.
    /// </summary>
    private System.ComponentModel.IContainer components = null;

    /// <summary>
    ///  Clean up any resources being used.
    /// </summary>
    /// <param name="disposing">true if managed resources should be disposed; otherwise, false.</param>
    protected override void Dispose(bool disposing)
    {
        if (disposing && (components != null))
        {
            components.Dispose();
        }
        base.Dispose(disposing);
    }

    #region Windows Form Designer generated code

    /// <summary>
    ///  Required method for Designer support - do not modify
    ///  the contents of this method with the code editor.
    /// </summary>
    private void InitializeComponent()
    {
        components = new System.ComponentModel.Container();
        tableMain = new TableLayoutPanel();
        groupTaxonomy = new GroupBox();
        tableTaxonomy = new TableLayoutPanel();
        checkAllowWalkDown = new CheckBox();
        checkUseOriginalCommonNames = new CheckBox();
        labelTaxonomyFile = new Label();
        textTaxonomyFile = new TextBox();
        buttonBrowseTaxonomyFile = new Button();
        groupSmoothing = new GroupBox();
        tableSmoothing = new TableLayoutPanel();
        labelClassificationThreshold = new Label();
        numericClassificationThreshold = new NumericUpDown();
        labelDetectionThreshold = new Label();
        numericDetectionThreshold = new NumericUpDown();
        labelMinDetectionsToOverwriteSecondary = new Label();
        numericMinDetectionsToOverwriteSecondary = new NumericUpDown();
        labelMaxDetectionsNondominantClass = new Label();
        numericMaxDetectionsNondominantClass = new NumericUpDown();
        labelMinDetectionsToOverwriteOther = new Label();
        numericMinDetectionsToOverwriteOther = new NumericUpDown();
        labelOtherCategoryNames = new Label();
        textOtherCategoryNames = new TextBox();
        checkPropagateThroughTaxonomy = new CheckBox();
        checkAllowSameFamilySmoothing = new CheckBox();
        labelMinDetectionsSameFamily = new Label();
        numericMinDetectionsSameFamily = new NumericUpDown();
        groupSequences = new GroupBox();
        tableSequences = new TableLayoutPanel();
        labelEpisodeInterval = new Label();
        numericEpisodeInterval = new NumericUpDown();
        groupOutput = new GroupBox();
        tableOutput = new TableLayoutPanel();
        checkIncludeDescriptionFields = new CheckBox();
        checkRemoveUnusedCategories = new CheckBox();
        tableButtons = new TableLayoutPanel();
        buttonRestoreDefaults = new Button();
        buttonOK = new Button();
        buttonCancel = new Button();
        toolTip = new ToolTip(components);
        tableMain.SuspendLayout();
        groupTaxonomy.SuspendLayout();
        tableTaxonomy.SuspendLayout();
        groupSmoothing.SuspendLayout();
        tableSmoothing.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numericClassificationThreshold).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericDetectionThreshold).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsToOverwriteSecondary).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMaxDetectionsNondominantClass).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsToOverwriteOther).BeginInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsSameFamily).BeginInit();
        groupSequences.SuspendLayout();
        tableSequences.SuspendLayout();
        ((System.ComponentModel.ISupportInitialize)numericEpisodeInterval).BeginInit();
        groupOutput.SuspendLayout();
        tableOutput.SuspendLayout();
        tableButtons.SuspendLayout();
        SuspendLayout();
        //
        // tableMain
        //
        tableMain.AutoSize = true;
        tableMain.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableMain.ColumnCount = 1;
        tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableMain.Controls.Add(groupTaxonomy, 0, 0);
        tableMain.Controls.Add(groupSmoothing, 0, 1);
        tableMain.Controls.Add(groupSequences, 0, 2);
        tableMain.Controls.Add(groupOutput, 0, 3);
        tableMain.Controls.Add(tableButtons, 0, 4);
        tableMain.Dock = DockStyle.Fill;
        tableMain.Name = "tableMain";
        tableMain.Padding = new Padding(10);
        tableMain.RowCount = 5;
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.TabIndex = 0;
        //
        // groupTaxonomy
        //
        groupTaxonomy.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupTaxonomy.AutoSize = true;
        groupTaxonomy.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupTaxonomy.Controls.Add(tableTaxonomy);
        groupTaxonomy.Name = "groupTaxonomy";
        groupTaxonomy.Padding = new Padding(8, 6, 8, 6);
        groupTaxonomy.TabIndex = 0;
        groupTaxonomy.TabStop = false;
        groupTaxonomy.Text = "Taxonomic mapping";
        //
        // tableTaxonomy
        //
        tableTaxonomy.AutoSize = true;
        tableTaxonomy.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableTaxonomy.ColumnCount = 3;
        tableTaxonomy.ColumnStyles.Add(new ColumnStyle());
        tableTaxonomy.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableTaxonomy.ColumnStyles.Add(new ColumnStyle());
        tableTaxonomy.Controls.Add(checkAllowWalkDown, 0, 0);
        tableTaxonomy.Controls.Add(checkUseOriginalCommonNames, 0, 1);
        tableTaxonomy.Controls.Add(labelTaxonomyFile, 0, 2);
        tableTaxonomy.Controls.Add(textTaxonomyFile, 1, 2);
        tableTaxonomy.Controls.Add(buttonBrowseTaxonomyFile, 2, 2);
        tableTaxonomy.Dock = DockStyle.Fill;
        tableTaxonomy.Name = "tableTaxonomy";
        tableTaxonomy.RowCount = 3;
        tableTaxonomy.RowStyles.Add(new RowStyle());
        tableTaxonomy.RowStyles.Add(new RowStyle());
        tableTaxonomy.RowStyles.Add(new RowStyle());
        tableTaxonomy.TabIndex = 0;
        //
        // checkAllowWalkDown
        //
        checkAllowWalkDown.AutoSize = true;
        tableTaxonomy.SetColumnSpan(checkAllowWalkDown, 3);
        checkAllowWalkDown.Name = "checkAllowWalkDown";
        checkAllowWalkDown.TabIndex = 0;
        checkAllowWalkDown.Text = "Map parent taxa to a unique allowed child taxon (walk down the taxonomy)";
        checkAllowWalkDown.UseVisualStyleBackColor = true;
        //
        // checkUseOriginalCommonNames
        //
        checkUseOriginalCommonNames.AutoSize = true;
        tableTaxonomy.SetColumnSpan(checkUseOriginalCommonNames, 3);
        checkUseOriginalCommonNames.Name = "checkUseOriginalCommonNames";
        checkUseOriginalCommonNames.TabIndex = 1;
        checkUseOriginalCommonNames.Text = "Use names from the \"original_common\" column of the mapping file, when available";
        checkUseOriginalCommonNames.UseVisualStyleBackColor = true;
        //
        // labelTaxonomyFile
        //
        labelTaxonomyFile.Anchor = AnchorStyles.Left;
        labelTaxonomyFile.AutoSize = true;
        labelTaxonomyFile.Name = "labelTaxonomyFile";
        labelTaxonomyFile.TabIndex = 2;
        labelTaxonomyFile.Text = "SpeciesNet taxonomy file:";
        //
        // textTaxonomyFile
        //
        textTaxonomyFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textTaxonomyFile.Name = "textTaxonomyFile";
        textTaxonomyFile.PlaceholderText = "Optional; leave blank to use the built-in taxonomy";
        textTaxonomyFile.TabIndex = 3;
        //
        // buttonBrowseTaxonomyFile
        //
        buttonBrowseTaxonomyFile.AutoSize = true;
        buttonBrowseTaxonomyFile.Name = "buttonBrowseTaxonomyFile";
        buttonBrowseTaxonomyFile.TabIndex = 4;
        buttonBrowseTaxonomyFile.Text = "Browse...";
        buttonBrowseTaxonomyFile.UseVisualStyleBackColor = true;
        buttonBrowseTaxonomyFile.Click += buttonBrowseTaxonomyFile_Click;
        //
        // groupSmoothing
        //
        groupSmoothing.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupSmoothing.AutoSize = true;
        groupSmoothing.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupSmoothing.Controls.Add(tableSmoothing);
        groupSmoothing.Margin = new Padding(3, 8, 3, 3);
        groupSmoothing.Name = "groupSmoothing";
        groupSmoothing.Padding = new Padding(8, 6, 8, 6);
        groupSmoothing.TabIndex = 1;
        groupSmoothing.TabStop = false;
        groupSmoothing.Text = "Smoothing (within-image and sequence-level)";
        //
        // tableSmoothing
        //
        tableSmoothing.AutoSize = true;
        tableSmoothing.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableSmoothing.ColumnCount = 2;
        tableSmoothing.ColumnStyles.Add(new ColumnStyle());
        tableSmoothing.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableSmoothing.Controls.Add(labelClassificationThreshold, 0, 0);
        tableSmoothing.Controls.Add(numericClassificationThreshold, 1, 0);
        tableSmoothing.Controls.Add(labelDetectionThreshold, 0, 1);
        tableSmoothing.Controls.Add(numericDetectionThreshold, 1, 1);
        tableSmoothing.Controls.Add(labelMinDetectionsToOverwriteSecondary, 0, 2);
        tableSmoothing.Controls.Add(numericMinDetectionsToOverwriteSecondary, 1, 2);
        tableSmoothing.Controls.Add(labelMaxDetectionsNondominantClass, 0, 3);
        tableSmoothing.Controls.Add(numericMaxDetectionsNondominantClass, 1, 3);
        tableSmoothing.Controls.Add(labelMinDetectionsToOverwriteOther, 0, 4);
        tableSmoothing.Controls.Add(numericMinDetectionsToOverwriteOther, 1, 4);
        tableSmoothing.Controls.Add(labelOtherCategoryNames, 0, 5);
        tableSmoothing.Controls.Add(textOtherCategoryNames, 0, 6);
        tableSmoothing.Controls.Add(checkPropagateThroughTaxonomy, 0, 7);
        tableSmoothing.Controls.Add(checkAllowSameFamilySmoothing, 0, 8);
        tableSmoothing.Controls.Add(labelMinDetectionsSameFamily, 0, 9);
        tableSmoothing.Controls.Add(numericMinDetectionsSameFamily, 1, 9);
        tableSmoothing.Dock = DockStyle.Fill;
        tableSmoothing.Name = "tableSmoothing";
        tableSmoothing.RowCount = 10;
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.RowStyles.Add(new RowStyle());
        tableSmoothing.TabIndex = 0;
        //
        // labelClassificationThreshold
        //
        labelClassificationThreshold.Anchor = AnchorStyles.Left;
        labelClassificationThreshold.AutoSize = true;
        labelClassificationThreshold.Name = "labelClassificationThreshold";
        labelClassificationThreshold.TabIndex = 0;
        labelClassificationThreshold.Text = "Classification confidence threshold:";
        //
        // numericClassificationThreshold
        //
        numericClassificationThreshold.DecimalPlaces = 3;
        numericClassificationThreshold.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
        numericClassificationThreshold.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
        numericClassificationThreshold.Name = "numericClassificationThreshold";
        numericClassificationThreshold.Size = new Size(80, 23);
        numericClassificationThreshold.TabIndex = 1;
        //
        // labelDetectionThreshold
        //
        labelDetectionThreshold.Anchor = AnchorStyles.Left;
        labelDetectionThreshold.AutoSize = true;
        labelDetectionThreshold.Name = "labelDetectionThreshold";
        labelDetectionThreshold.TabIndex = 2;
        labelDetectionThreshold.Text = "Detection confidence threshold:";
        //
        // numericDetectionThreshold
        //
        numericDetectionThreshold.DecimalPlaces = 3;
        numericDetectionThreshold.Increment = new decimal(new int[] { 5, 0, 0, 131072 });
        numericDetectionThreshold.Maximum = new decimal(new int[] { 1, 0, 0, 0 });
        numericDetectionThreshold.Name = "numericDetectionThreshold";
        numericDetectionThreshold.Size = new Size(80, 23);
        numericDetectionThreshold.TabIndex = 3;
        //
        // labelMinDetectionsToOverwriteSecondary
        //
        labelMinDetectionsToOverwriteSecondary.Anchor = AnchorStyles.Left;
        labelMinDetectionsToOverwriteSecondary.AutoSize = true;
        labelMinDetectionsToOverwriteSecondary.Name = "labelMinDetectionsToOverwriteSecondary";
        labelMinDetectionsToOverwriteSecondary.TabIndex = 4;
        labelMinDetectionsToOverwriteSecondary.Text = "Min. detections of the dominant category needed to overwrite other categories:";
        //
        // numericMinDetectionsToOverwriteSecondary
        //
        numericMinDetectionsToOverwriteSecondary.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        numericMinDetectionsToOverwriteSecondary.Name = "numericMinDetectionsToOverwriteSecondary";
        numericMinDetectionsToOverwriteSecondary.Size = new Size(80, 23);
        numericMinDetectionsToOverwriteSecondary.TabIndex = 5;
        //
        // labelMaxDetectionsNondominantClass
        //
        labelMaxDetectionsNondominantClass.Anchor = AnchorStyles.Left;
        labelMaxDetectionsNondominantClass.AutoSize = true;
        labelMaxDetectionsNondominantClass.Name = "labelMaxDetectionsNondominantClass";
        labelMaxDetectionsNondominantClass.TabIndex = 6;
        labelMaxDetectionsNondominantClass.Text = "Max. detections of a non-dominant category that can be overwritten:";
        //
        // numericMaxDetectionsNondominantClass
        //
        numericMaxDetectionsNondominantClass.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        numericMaxDetectionsNondominantClass.Name = "numericMaxDetectionsNondominantClass";
        numericMaxDetectionsNondominantClass.Size = new Size(80, 23);
        numericMaxDetectionsNondominantClass.TabIndex = 7;
        //
        // labelMinDetectionsToOverwriteOther
        //
        labelMinDetectionsToOverwriteOther.Anchor = AnchorStyles.Left;
        labelMinDetectionsToOverwriteOther.AutoSize = true;
        labelMinDetectionsToOverwriteOther.Name = "labelMinDetectionsToOverwriteOther";
        labelMinDetectionsToOverwriteOther.TabIndex = 8;
        labelMinDetectionsToOverwriteOther.Text = "Min. detections of the dominant category needed to overwrite \"other\" categories:";
        //
        // numericMinDetectionsToOverwriteOther
        //
        numericMinDetectionsToOverwriteOther.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        numericMinDetectionsToOverwriteOther.Name = "numericMinDetectionsToOverwriteOther";
        numericMinDetectionsToOverwriteOther.Size = new Size(80, 23);
        numericMinDetectionsToOverwriteOther.TabIndex = 9;
        //
        // labelOtherCategoryNames
        //
        labelOtherCategoryNames.Anchor = AnchorStyles.Left;
        labelOtherCategoryNames.AutoSize = true;
        tableSmoothing.SetColumnSpan(labelOtherCategoryNames, 2);
        labelOtherCategoryNames.Name = "labelOtherCategoryNames";
        labelOtherCategoryNames.TabIndex = 10;
        labelOtherCategoryNames.Text = "\"Other\" categories, which can be overwritten by the dominant category (comma-separated):";
        //
        // textOtherCategoryNames
        //
        textOtherCategoryNames.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        tableSmoothing.SetColumnSpan(textOtherCategoryNames, 2);
        textOtherCategoryNames.Margin = new Padding(24, 3, 3, 3);
        textOtherCategoryNames.Name = "textOtherCategoryNames";
        textOtherCategoryNames.TabIndex = 11;
        //
        // checkPropagateThroughTaxonomy
        //
        checkPropagateThroughTaxonomy.AutoSize = true;
        tableSmoothing.SetColumnSpan(checkPropagateThroughTaxonomy, 2);
        checkPropagateThroughTaxonomy.Margin = new Padding(3, 6, 3, 3);
        checkPropagateThroughTaxonomy.Name = "checkPropagateThroughTaxonomy";
        checkPropagateThroughTaxonomy.TabIndex = 12;
        checkPropagateThroughTaxonomy.Text = "Change higher-level taxa to lower-level taxa when the lower-level taxa are present (e.g. \"canid\" --> \"jackal\")";
        checkPropagateThroughTaxonomy.UseVisualStyleBackColor = true;
        //
        // checkAllowSameFamilySmoothing
        //
        checkAllowSameFamilySmoothing.AutoSize = true;
        tableSmoothing.SetColumnSpan(checkAllowSameFamilySmoothing, 2);
        checkAllowSameFamilySmoothing.Name = "checkAllowSameFamilySmoothing";
        checkAllowSameFamilySmoothing.TabIndex = 13;
        checkAllowSameFamilySmoothing.Text = "Allow same-family smoothing (change less common species to the most common species in the same family)";
        checkAllowSameFamilySmoothing.UseVisualStyleBackColor = true;
        checkAllowSameFamilySmoothing.CheckedChanged += checkAllowSameFamilySmoothing_CheckedChanged;
        //
        // labelMinDetectionsSameFamily
        //
        labelMinDetectionsSameFamily.Anchor = AnchorStyles.Left;
        labelMinDetectionsSameFamily.AutoSize = true;
        labelMinDetectionsSameFamily.Margin = new Padding(24, 0, 3, 0);
        labelMinDetectionsSameFamily.Name = "labelMinDetectionsSameFamily";
        labelMinDetectionsSameFamily.TabIndex = 14;
        labelMinDetectionsSameFamily.Text = "Min. detections of the dominant species needed for same-family smoothing:";
        //
        // numericMinDetectionsSameFamily
        //
        numericMinDetectionsSameFamily.Maximum = new decimal(new int[] { 100000, 0, 0, 0 });
        numericMinDetectionsSameFamily.Name = "numericMinDetectionsSameFamily";
        numericMinDetectionsSameFamily.Size = new Size(80, 23);
        numericMinDetectionsSameFamily.TabIndex = 15;
        //
        // groupSequences
        //
        groupSequences.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupSequences.AutoSize = true;
        groupSequences.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupSequences.Controls.Add(tableSequences);
        groupSequences.Margin = new Padding(3, 8, 3, 3);
        groupSequences.Name = "groupSequences";
        groupSequences.Padding = new Padding(8, 6, 8, 6);
        groupSequences.TabIndex = 2;
        groupSequences.TabStop = false;
        groupSequences.Text = "Sequences";
        //
        // tableSequences
        //
        tableSequences.AutoSize = true;
        tableSequences.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableSequences.ColumnCount = 2;
        tableSequences.ColumnStyles.Add(new ColumnStyle());
        tableSequences.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableSequences.Controls.Add(labelEpisodeInterval, 0, 0);
        tableSequences.Controls.Add(numericEpisodeInterval, 1, 0);
        tableSequences.Dock = DockStyle.Fill;
        tableSequences.Name = "tableSequences";
        tableSequences.RowCount = 1;
        tableSequences.RowStyles.Add(new RowStyle());
        tableSequences.TabIndex = 0;
        //
        // labelEpisodeInterval
        //
        labelEpisodeInterval.Anchor = AnchorStyles.Left;
        labelEpisodeInterval.AutoSize = true;
        labelEpisodeInterval.Name = "labelEpisodeInterval";
        labelEpisodeInterval.TabIndex = 0;
        labelEpisodeInterval.Text = "Max. time between images in the same sequence (seconds):";
        //
        // numericEpisodeInterval
        //
        numericEpisodeInterval.DecimalPlaces = 1;
        numericEpisodeInterval.Maximum = new decimal(new int[] { 86400, 0, 0, 0 });
        numericEpisodeInterval.Name = "numericEpisodeInterval";
        numericEpisodeInterval.Size = new Size(80, 23);
        numericEpisodeInterval.TabIndex = 1;
        //
        // groupOutput
        //
        groupOutput.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupOutput.AutoSize = true;
        groupOutput.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        groupOutput.Controls.Add(tableOutput);
        groupOutput.Margin = new Padding(3, 8, 3, 3);
        groupOutput.Name = "groupOutput";
        groupOutput.Padding = new Padding(8, 6, 8, 6);
        groupOutput.TabIndex = 3;
        groupOutput.TabStop = false;
        groupOutput.Text = "Output";
        //
        // tableOutput
        //
        tableOutput.AutoSize = true;
        tableOutput.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableOutput.ColumnCount = 1;
        tableOutput.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableOutput.Controls.Add(checkIncludeDescriptionFields, 0, 0);
        tableOutput.Controls.Add(checkRemoveUnusedCategories, 0, 1);
        tableOutput.Dock = DockStyle.Fill;
        tableOutput.Name = "tableOutput";
        tableOutput.RowCount = 2;
        tableOutput.RowStyles.Add(new RowStyle());
        tableOutput.RowStyles.Add(new RowStyle());
        tableOutput.TabIndex = 0;
        //
        // checkIncludeDescriptionFields
        //
        checkIncludeDescriptionFields.AutoSize = true;
        checkIncludeDescriptionFields.Name = "checkIncludeDescriptionFields";
        checkIncludeDescriptionFields.TabIndex = 0;
        checkIncludeDescriptionFields.Text = "Add fields to each image describing its classifications before each step";
        checkIncludeDescriptionFields.UseVisualStyleBackColor = true;
        //
        // checkRemoveUnusedCategories
        //
        checkRemoveUnusedCategories.AutoSize = true;
        checkRemoveUnusedCategories.Name = "checkRemoveUnusedCategories";
        checkRemoveUnusedCategories.TabIndex = 1;
        checkRemoveUnusedCategories.Text = "Remove unused classification categories";
        checkRemoveUnusedCategories.UseVisualStyleBackColor = true;
        //
        // tableButtons
        //
        tableButtons.AutoSize = true;
        tableButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableButtons.ColumnCount = 4;
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.Controls.Add(buttonRestoreDefaults, 0, 0);
        tableButtons.Controls.Add(buttonOK, 2, 0);
        tableButtons.Controls.Add(buttonCancel, 3, 0);
        tableButtons.Dock = DockStyle.Fill;
        tableButtons.Margin = new Padding(0, 10, 0, 0);
        tableButtons.Name = "tableButtons";
        tableButtons.RowCount = 1;
        tableButtons.RowStyles.Add(new RowStyle());
        tableButtons.TabIndex = 4;
        //
        // buttonRestoreDefaults
        //
        buttonRestoreDefaults.AutoSize = true;
        buttonRestoreDefaults.MinimumSize = new Size(100, 30);
        buttonRestoreDefaults.Name = "buttonRestoreDefaults";
        buttonRestoreDefaults.TabIndex = 0;
        buttonRestoreDefaults.Text = "Restore defaults";
        buttonRestoreDefaults.UseVisualStyleBackColor = true;
        buttonRestoreDefaults.Click += buttonRestoreDefaults_Click;
        //
        // buttonOK
        //
        buttonOK.AutoSize = true;
        buttonOK.MinimumSize = new Size(100, 30);
        buttonOK.Name = "buttonOK";
        buttonOK.TabIndex = 1;
        buttonOK.Text = "OK";
        buttonOK.UseVisualStyleBackColor = true;
        buttonOK.Click += buttonOK_Click;
        //
        // buttonCancel
        //
        buttonCancel.AutoSize = true;
        buttonCancel.DialogResult = DialogResult.Cancel;
        buttonCancel.MinimumSize = new Size(100, 30);
        buttonCancel.Name = "buttonCancel";
        buttonCancel.TabIndex = 2;
        buttonCancel.Text = "Cancel";
        buttonCancel.UseVisualStyleBackColor = true;
        //
        // AdvancedOptionsForm
        //
        AcceptButton = buttonOK;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        AutoSize = true;
        AutoSizeMode = AutoSizeMode.GrowAndShrink;
        CancelButton = buttonCancel;
        Controls.Add(tableMain);
        FormBorderStyle = FormBorderStyle.FixedDialog;
        MaximizeBox = false;
        MinimizeBox = false;
        Name = "AdvancedOptionsForm";
        ShowIcon = false;
        ShowInTaskbar = false;
        StartPosition = FormStartPosition.CenterParent;
        Text = "Advanced options";
        tableMain.ResumeLayout(false);
        tableMain.PerformLayout();
        groupTaxonomy.ResumeLayout(false);
        groupTaxonomy.PerformLayout();
        tableTaxonomy.ResumeLayout(false);
        tableTaxonomy.PerformLayout();
        groupSmoothing.ResumeLayout(false);
        groupSmoothing.PerformLayout();
        tableSmoothing.ResumeLayout(false);
        tableSmoothing.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numericClassificationThreshold).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericDetectionThreshold).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsToOverwriteSecondary).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMaxDetectionsNondominantClass).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsToOverwriteOther).EndInit();
        ((System.ComponentModel.ISupportInitialize)numericMinDetectionsSameFamily).EndInit();
        groupSequences.ResumeLayout(false);
        groupSequences.PerformLayout();
        tableSequences.ResumeLayout(false);
        tableSequences.PerformLayout();
        ((System.ComponentModel.ISupportInitialize)numericEpisodeInterval).EndInit();
        groupOutput.ResumeLayout(false);
        groupOutput.PerformLayout();
        tableOutput.ResumeLayout(false);
        tableOutput.PerformLayout();
        tableButtons.ResumeLayout(false);
        tableButtons.PerformLayout();
        ResumeLayout(false);
        PerformLayout();
    }

    #endregion

    private TableLayoutPanel tableMain;
    private GroupBox groupTaxonomy;
    private TableLayoutPanel tableTaxonomy;
    private CheckBox checkAllowWalkDown;
    private CheckBox checkUseOriginalCommonNames;
    private Label labelTaxonomyFile;
    private TextBox textTaxonomyFile;
    private Button buttonBrowseTaxonomyFile;
    private GroupBox groupSmoothing;
    private TableLayoutPanel tableSmoothing;
    private Label labelClassificationThreshold;
    private NumericUpDown numericClassificationThreshold;
    private Label labelDetectionThreshold;
    private NumericUpDown numericDetectionThreshold;
    private Label labelMinDetectionsToOverwriteSecondary;
    private NumericUpDown numericMinDetectionsToOverwriteSecondary;
    private Label labelMaxDetectionsNondominantClass;
    private NumericUpDown numericMaxDetectionsNondominantClass;
    private Label labelMinDetectionsToOverwriteOther;
    private NumericUpDown numericMinDetectionsToOverwriteOther;
    private Label labelOtherCategoryNames;
    private TextBox textOtherCategoryNames;
    private CheckBox checkPropagateThroughTaxonomy;
    private CheckBox checkAllowSameFamilySmoothing;
    private Label labelMinDetectionsSameFamily;
    private NumericUpDown numericMinDetectionsSameFamily;
    private GroupBox groupSequences;
    private TableLayoutPanel tableSequences;
    private Label labelEpisodeInterval;
    private NumericUpDown numericEpisodeInterval;
    private GroupBox groupOutput;
    private TableLayoutPanel tableOutput;
    private CheckBox checkIncludeDescriptionFields;
    private CheckBox checkRemoveUnusedCategories;
    private TableLayoutPanel tableButtons;
    private Button buttonRestoreDefaults;
    private Button buttonOK;
    private Button buttonCancel;
    private ToolTip toolTip;
}
