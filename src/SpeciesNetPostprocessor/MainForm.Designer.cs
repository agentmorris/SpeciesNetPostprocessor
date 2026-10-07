namespace SpeciesNetPostprocessor;

partial class MainForm
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
        labelInputFile = new Label();
        textInputFile = new TextBox();
        buttonBrowseInput = new Button();
        labelOutputFile = new Label();
        textOutputFile = new TextBox();
        buttonBrowseOutput = new Button();
        groupSteps = new GroupBox();
        tableSteps = new TableLayoutPanel();
        checkRemoveNonAnimal = new CheckBox();
        checkApplyTaxaList = new CheckBox();
        radioBuiltInTaxaList = new RadioButton();
        comboBuiltInTaxaList = new ComboBox();
        radioCsvTaxaList = new RadioButton();
        textTaxaListFile = new TextBox();
        buttonBrowseTaxaList = new Button();
        checkWithinImageSmoothing = new CheckBox();
        checkSequenceSmoothing = new CheckBox();
        checkWriteLogFile = new CheckBox();
        tableButtons = new TableLayoutPanel();
        buttonAdvanced = new Button();
        buttonProcess = new Button();
        buttonCancel = new Button();
        labelStatus = new Label();
        progressBar = new ProgressBar();
        textLog = new TextBox();
        toolTip = new ToolTip(components);
        tableMain.SuspendLayout();
        groupSteps.SuspendLayout();
        tableSteps.SuspendLayout();
        tableButtons.SuspendLayout();
        SuspendLayout();
        //
        // tableMain
        //
        tableMain.ColumnCount = 3;
        tableMain.ColumnStyles.Add(new ColumnStyle());
        tableMain.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableMain.ColumnStyles.Add(new ColumnStyle());
        tableMain.Controls.Add(labelInputFile, 0, 0);
        tableMain.Controls.Add(textInputFile, 1, 0);
        tableMain.Controls.Add(buttonBrowseInput, 2, 0);
        tableMain.Controls.Add(labelOutputFile, 0, 1);
        tableMain.Controls.Add(textOutputFile, 1, 1);
        tableMain.Controls.Add(buttonBrowseOutput, 2, 1);
        tableMain.Controls.Add(groupSteps, 0, 2);
        tableMain.Controls.Add(checkWriteLogFile, 0, 3);
        tableMain.Controls.Add(tableButtons, 0, 4);
        tableMain.Controls.Add(labelStatus, 0, 5);
        tableMain.Controls.Add(progressBar, 0, 6);
        tableMain.Controls.Add(textLog, 0, 7);
        tableMain.Dock = DockStyle.Fill;
        tableMain.Location = new Point(0, 0);
        tableMain.Name = "tableMain";
        tableMain.Padding = new Padding(10);
        tableMain.RowCount = 8;
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle());
        tableMain.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));
        tableMain.Size = new Size(784, 661);
        tableMain.TabIndex = 0;
        //
        // labelInputFile
        //
        labelInputFile.Anchor = AnchorStyles.Left;
        labelInputFile.AutoSize = true;
        labelInputFile.Name = "labelInputFile";
        labelInputFile.TabIndex = 0;
        labelInputFile.Text = "Input file:";
        //
        // textInputFile
        //
        textInputFile.AllowDrop = true;
        textInputFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textInputFile.Name = "textInputFile";
        textInputFile.PlaceholderText = "MegaDetector/SpeciesNet results file (.json), e.g. from AddaxAI";
        textInputFile.TabIndex = 1;
        textInputFile.DragDrop += OnFileDragDrop;
        textInputFile.DragEnter += OnFileDragEnter;
        //
        // buttonBrowseInput
        //
        buttonBrowseInput.AutoSize = true;
        buttonBrowseInput.Name = "buttonBrowseInput";
        buttonBrowseInput.TabIndex = 2;
        buttonBrowseInput.Text = "Browse...";
        buttonBrowseInput.UseVisualStyleBackColor = true;
        buttonBrowseInput.Click += buttonBrowseInput_Click;
        //
        // labelOutputFile
        //
        labelOutputFile.Anchor = AnchorStyles.Left;
        labelOutputFile.AutoSize = true;
        labelOutputFile.Name = "labelOutputFile";
        labelOutputFile.TabIndex = 3;
        labelOutputFile.Text = "Output file:";
        //
        // textOutputFile
        //
        textOutputFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textOutputFile.Name = "textOutputFile";
        textOutputFile.PlaceholderText = "Optional; if blank, output goes to [input file]_processed.json";
        textOutputFile.TabIndex = 4;
        //
        // buttonBrowseOutput
        //
        buttonBrowseOutput.AutoSize = true;
        buttonBrowseOutput.Name = "buttonBrowseOutput";
        buttonBrowseOutput.TabIndex = 5;
        buttonBrowseOutput.Text = "Browse...";
        buttonBrowseOutput.UseVisualStyleBackColor = true;
        buttonBrowseOutput.Click += buttonBrowseOutput_Click;
        //
        // groupSteps
        //
        groupSteps.Anchor = AnchorStyles.Top | AnchorStyles.Left | AnchorStyles.Right;
        groupSteps.AutoSize = true;
        groupSteps.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableMain.SetColumnSpan(groupSteps, 3);
        groupSteps.Controls.Add(tableSteps);
        groupSteps.Margin = new Padding(3, 10, 3, 3);
        groupSteps.Name = "groupSteps";
        groupSteps.Padding = new Padding(8, 6, 8, 6);
        groupSteps.TabIndex = 6;
        groupSteps.TabStop = false;
        groupSteps.Text = "Processing steps";
        //
        // tableSteps
        //
        tableSteps.AutoSize = true;
        tableSteps.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableSteps.ColumnCount = 3;
        tableSteps.ColumnStyles.Add(new ColumnStyle());
        tableSteps.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableSteps.ColumnStyles.Add(new ColumnStyle());
        tableSteps.Controls.Add(checkRemoveNonAnimal, 0, 0);
        tableSteps.Controls.Add(checkApplyTaxaList, 0, 1);
        tableSteps.Controls.Add(radioBuiltInTaxaList, 0, 2);
        tableSteps.Controls.Add(comboBuiltInTaxaList, 1, 2);
        tableSteps.Controls.Add(radioCsvTaxaList, 0, 3);
        tableSteps.Controls.Add(textTaxaListFile, 1, 3);
        tableSteps.Controls.Add(buttonBrowseTaxaList, 2, 3);
        tableSteps.Controls.Add(checkWithinImageSmoothing, 0, 4);
        tableSteps.Controls.Add(checkSequenceSmoothing, 0, 5);
        tableSteps.Dock = DockStyle.Fill;
        tableSteps.Name = "tableSteps";
        tableSteps.RowCount = 6;
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.RowStyles.Add(new RowStyle());
        tableSteps.TabIndex = 0;
        //
        // checkRemoveNonAnimal
        //
        checkRemoveNonAnimal.AutoSize = true;
        checkRemoveNonAnimal.Checked = true;
        checkRemoveNonAnimal.CheckState = CheckState.Checked;
        tableSteps.SetColumnSpan(checkRemoveNonAnimal, 3);
        checkRemoveNonAnimal.Name = "checkRemoveNonAnimal";
        checkRemoveNonAnimal.TabIndex = 0;
        checkRemoveNonAnimal.Text = "Remove species classifications from non-animal objects";
        checkRemoveNonAnimal.UseVisualStyleBackColor = true;
        //
        // checkApplyTaxaList
        //
        checkApplyTaxaList.AutoSize = true;
        checkApplyTaxaList.Checked = true;
        checkApplyTaxaList.CheckState = CheckState.Checked;
        tableSteps.SetColumnSpan(checkApplyTaxaList, 3);
        checkApplyTaxaList.Margin = new Padding(3, 6, 3, 3);
        checkApplyTaxaList.Name = "checkApplyTaxaList";
        checkApplyTaxaList.TabIndex = 1;
        checkApplyTaxaList.Text = "Apply taxonomic mapping";
        checkApplyTaxaList.UseVisualStyleBackColor = true;
        checkApplyTaxaList.CheckedChanged += OnControlStateChanged;
        //
        // radioBuiltInTaxaList
        //
        radioBuiltInTaxaList.Anchor = AnchorStyles.Left;
        radioBuiltInTaxaList.AutoSize = true;
        radioBuiltInTaxaList.Checked = true;
        radioBuiltInTaxaList.Margin = new Padding(24, 3, 3, 3);
        radioBuiltInTaxaList.Name = "radioBuiltInTaxaList";
        radioBuiltInTaxaList.TabIndex = 2;
        radioBuiltInTaxaList.TabStop = true;
        radioBuiltInTaxaList.Text = "Built-in mapping:";
        radioBuiltInTaxaList.UseVisualStyleBackColor = true;
        radioBuiltInTaxaList.CheckedChanged += OnControlStateChanged;
        //
        // comboBuiltInTaxaList
        //
        comboBuiltInTaxaList.Anchor = AnchorStyles.Left;
        comboBuiltInTaxaList.DropDownStyle = ComboBoxStyle.DropDownList;
        comboBuiltInTaxaList.Name = "comboBuiltInTaxaList";
        comboBuiltInTaxaList.Size = new Size(250, 23);
        comboBuiltInTaxaList.TabIndex = 3;
        //
        // radioCsvTaxaList
        //
        radioCsvTaxaList.Anchor = AnchorStyles.Left;
        radioCsvTaxaList.AutoSize = true;
        radioCsvTaxaList.Margin = new Padding(24, 3, 3, 3);
        radioCsvTaxaList.Name = "radioCsvTaxaList";
        radioCsvTaxaList.TabIndex = 4;
        radioCsvTaxaList.Text = "Mapping file (.csv):";
        radioCsvTaxaList.UseVisualStyleBackColor = true;
        radioCsvTaxaList.CheckedChanged += OnControlStateChanged;
        //
        // textTaxaListFile
        //
        textTaxaListFile.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        textTaxaListFile.Name = "textTaxaListFile";
        textTaxaListFile.TabIndex = 5;
        //
        // buttonBrowseTaxaList
        //
        buttonBrowseTaxaList.AutoSize = true;
        buttonBrowseTaxaList.Name = "buttonBrowseTaxaList";
        buttonBrowseTaxaList.TabIndex = 6;
        buttonBrowseTaxaList.Text = "Browse...";
        buttonBrowseTaxaList.UseVisualStyleBackColor = true;
        buttonBrowseTaxaList.Click += buttonBrowseTaxaList_Click;
        //
        // checkWithinImageSmoothing
        //
        checkWithinImageSmoothing.AutoSize = true;
        checkWithinImageSmoothing.Checked = true;
        checkWithinImageSmoothing.CheckState = CheckState.Checked;
        tableSteps.SetColumnSpan(checkWithinImageSmoothing, 3);
        checkWithinImageSmoothing.Margin = new Padding(3, 6, 3, 3);
        checkWithinImageSmoothing.Name = "checkWithinImageSmoothing";
        checkWithinImageSmoothing.TabIndex = 7;
        checkWithinImageSmoothing.Text = "Apply within-image smoothing";
        checkWithinImageSmoothing.UseVisualStyleBackColor = true;
        //
        // checkSequenceSmoothing
        //
        checkSequenceSmoothing.AutoSize = true;
        checkSequenceSmoothing.Checked = true;
        checkSequenceSmoothing.CheckState = CheckState.Checked;
        tableSteps.SetColumnSpan(checkSequenceSmoothing, 3);
        checkSequenceSmoothing.Name = "checkSequenceSmoothing";
        checkSequenceSmoothing.TabIndex = 8;
        checkSequenceSmoothing.Text = "Apply sequence-level smoothing";
        checkSequenceSmoothing.UseVisualStyleBackColor = true;
        //
        // checkWriteLogFile
        //
        checkWriteLogFile.AutoSize = true;
        checkWriteLogFile.Checked = true;
        checkWriteLogFile.CheckState = CheckState.Checked;
        tableMain.SetColumnSpan(checkWriteLogFile, 3);
        checkWriteLogFile.Margin = new Padding(11, 6, 3, 3);
        checkWriteLogFile.Name = "checkWriteLogFile";
        checkWriteLogFile.TabIndex = 7;
        checkWriteLogFile.Text = "Write log file";
        checkWriteLogFile.UseVisualStyleBackColor = true;
        //
        // tableButtons
        //
        tableButtons.AutoSize = true;
        tableButtons.AutoSizeMode = AutoSizeMode.GrowAndShrink;
        tableButtons.ColumnCount = 4;
        tableMain.SetColumnSpan(tableButtons, 3);
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.ColumnStyles.Add(new ColumnStyle());
        tableButtons.Controls.Add(buttonAdvanced, 0, 0);
        tableButtons.Controls.Add(buttonProcess, 2, 0);
        tableButtons.Controls.Add(buttonCancel, 3, 0);
        tableButtons.Dock = DockStyle.Fill;
        tableButtons.Margin = new Padding(0, 8, 0, 3);
        tableButtons.Name = "tableButtons";
        tableButtons.RowCount = 1;
        tableButtons.RowStyles.Add(new RowStyle());
        tableButtons.TabIndex = 8;
        //
        // buttonAdvanced
        //
        buttonAdvanced.AutoSize = true;
        buttonAdvanced.MinimumSize = new Size(100, 30);
        buttonAdvanced.Name = "buttonAdvanced";
        buttonAdvanced.TabIndex = 0;
        buttonAdvanced.Text = "Advanced...";
        buttonAdvanced.UseVisualStyleBackColor = true;
        buttonAdvanced.Click += buttonAdvanced_Click;
        //
        // buttonProcess
        //
        buttonProcess.AutoSize = true;
        buttonProcess.Font = new Font("Segoe UI", 9F, FontStyle.Bold);
        buttonProcess.MinimumSize = new Size(100, 30);
        buttonProcess.Name = "buttonProcess";
        buttonProcess.TabIndex = 1;
        buttonProcess.Text = "Process";
        buttonProcess.UseVisualStyleBackColor = true;
        buttonProcess.Click += buttonProcess_Click;
        //
        // buttonCancel
        //
        buttonCancel.AutoSize = true;
        buttonCancel.Enabled = false;
        buttonCancel.MinimumSize = new Size(100, 30);
        buttonCancel.Name = "buttonCancel";
        buttonCancel.TabIndex = 2;
        buttonCancel.Text = "Cancel";
        buttonCancel.UseVisualStyleBackColor = true;
        buttonCancel.Click += buttonCancel_Click;
        //
        // labelStatus
        //
        labelStatus.AutoSize = true;
        tableMain.SetColumnSpan(labelStatus, 3);
        labelStatus.Margin = new Padding(3, 8, 3, 3);
        labelStatus.Name = "labelStatus";
        labelStatus.TabIndex = 9;
        labelStatus.Text = "Ready";
        //
        // progressBar
        //
        progressBar.Anchor = AnchorStyles.Left | AnchorStyles.Right;
        tableMain.SetColumnSpan(progressBar, 3);
        progressBar.Name = "progressBar";
        progressBar.Size = new Size(758, 20);
        progressBar.TabIndex = 10;
        //
        // textLog
        //
        textLog.BackColor = SystemColors.Window;
        tableMain.SetColumnSpan(textLog, 3);
        textLog.Dock = DockStyle.Fill;
        textLog.Font = new Font("Consolas", 9F);
        textLog.Multiline = true;
        textLog.Name = "textLog";
        textLog.ReadOnly = true;
        textLog.ScrollBars = ScrollBars.Vertical;
        textLog.TabIndex = 11;
        //
        // MainForm
        //
        AllowDrop = true;
        AutoScaleDimensions = new SizeF(7F, 15F);
        AutoScaleMode = AutoScaleMode.Font;
        ClientSize = new Size(784, 661);
        Controls.Add(tableMain);
        MinimumSize = new Size(640, 560);
        Name = "MainForm";
        StartPosition = FormStartPosition.CenterScreen;
        Text = "SpeciesNet Postprocessor";
        FormClosing += MainForm_FormClosing;
        DragDrop += OnFileDragDrop;
        DragEnter += OnFileDragEnter;
        tableMain.ResumeLayout(false);
        tableMain.PerformLayout();
        groupSteps.ResumeLayout(false);
        groupSteps.PerformLayout();
        tableSteps.ResumeLayout(false);
        tableSteps.PerformLayout();
        tableButtons.ResumeLayout(false);
        tableButtons.PerformLayout();
        ResumeLayout(false);
    }

    #endregion

    private TableLayoutPanel tableMain;
    private Label labelInputFile;
    private TextBox textInputFile;
    private Button buttonBrowseInput;
    private Label labelOutputFile;
    private TextBox textOutputFile;
    private Button buttonBrowseOutput;
    private GroupBox groupSteps;
    private TableLayoutPanel tableSteps;
    private CheckBox checkRemoveNonAnimal;
    private CheckBox checkApplyTaxaList;
    private RadioButton radioBuiltInTaxaList;
    private ComboBox comboBuiltInTaxaList;
    private RadioButton radioCsvTaxaList;
    private TextBox textTaxaListFile;
    private Button buttonBrowseTaxaList;
    private CheckBox checkWithinImageSmoothing;
    private CheckBox checkSequenceSmoothing;
    private CheckBox checkWriteLogFile;
    private TableLayoutPanel tableButtons;
    private Button buttonAdvanced;
    private Button buttonProcess;
    private Button buttonCancel;
    private Label labelStatus;
    private ProgressBar progressBar;
    private TextBox textLog;
    private ToolTip toolTip;
}
