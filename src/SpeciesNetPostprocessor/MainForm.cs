using System.Runtime;
using SpeciesNetPostprocessor.Core;

namespace SpeciesNetPostprocessor;

public partial class MainForm : Form
{
    private PostprocessingOptions _options;
    private CancellationTokenSource? _cancellationTokenSource;
    private bool _running;
    private bool _closeWhenFinished;

    /// <summary>
    /// Sends log messages (other than debug messages) to the log pane.
    /// </summary>
    private sealed class LogPaneLog(MainForm form) : IPostprocessingLog
    {
        public void Log(LogLevel level, string message)
        {
            if (level == LogLevel.Debug)
            {
                return;
            }
            string prefix = level switch
            {
                LogLevel.Warning => "Warning: ",
                LogLevel.Error => "Error: ",
                _ => string.Empty
            };
            string text = (prefix + message).ReplaceLineEndings(Environment.NewLine) + Environment.NewLine;

            // Logging should never interfere with processing, e.g. if the window is closing
            try
            {
                if (!form.IsDisposed && form.IsHandleCreated)
                {
                    form.BeginInvoke(() => form.textLog.AppendText(text));
                }
            }
            catch (InvalidOperationException)
            {
            }
        }
    }

    public MainForm()
    {
        InitializeComponent();

        Text = $"SpeciesNet Postprocessor {Postprocessor.Version}";

        // Loading the .ico from a stream (rather than extracting the .exe's icon) gives Windows
        // all the sizes in the file, so the title bar and taskbar icons are sharp
        using (Stream? iconStream = typeof(MainForm).Assembly.GetManifestResourceStream("SpeciesNetPostprocessor.app.ico"))
        {
            if (iconStream != null)
            {
                Icon = new Icon(iconStream);
            }
        }

        toolTip.SetToolTip(checkRemoveNonAnimal,
            "Remove species classifications from objects that MegaDetector thinks are people or vehicles, " +
            "rather than animals.");
        toolTip.SetToolTip(checkApplyTaxaList,
            "Restrict species classifications to the taxa in a mapping list, and use the names from that list.  " +
            "Predictions of taxa that aren't on the list are mapped to the nearest taxon that is on the list " +
            "(e.g. the genus or family), or to \"animal\".");
        toolTip.SetToolTip(radioCsvTaxaList,
            "A .csv file with at least the columns \"latin\" and \"common\" (and optionally \"original_common\").");
        toolTip.SetToolTip(checkWithinImageSmoothing,
            "Clean up inconsistent classifications within each image, e.g. an image with 20 zebras and 1 horse " +
            "is probably 21 zebras.");
        toolTip.SetToolTip(checkSequenceSmoothing,
            "Clean up inconsistent classifications within each sequence (burst) of images, e.g. " +
            "zebra/zebra/zebra/equus species/zebra is probably all zebras.  Each folder is treated as a camera, " +
            "and image timestamps are required.");
        toolTip.SetToolTip(checkWriteLogFile,
            "Write debugging information to a .txt file next to the output file (e.g. results_processed.txt).  " +
            "If something goes wrong, please send this file to whoever supports this program.");

        comboBuiltInTaxaList.Items.AddRange(BuiltInResources.TaxaLists.Select(t => (object)t.Name).ToArray());

        _options = AppSettings.Load();
        ApplyOptionsToControls(_options);
        UpdateControlState();
    }

    #region Options <--> controls

    private void ApplyOptionsToControls(PostprocessingOptions options)
    {
        textInputFile.Text = options.InputFile;
        textOutputFile.Text = options.OutputFile ?? string.Empty;
        checkRemoveNonAnimal.Checked = options.RemoveClassificationsFromNonAnimals;
        checkApplyTaxaList.Checked = options.ApplyTaxaList;
        radioBuiltInTaxaList.Checked = options.TaxaListSource == TaxaListSource.BuiltIn;
        radioCsvTaxaList.Checked = options.TaxaListSource == TaxaListSource.CsvFile;
        comboBuiltInTaxaList.SelectedItem = options.BuiltInTaxaList;
        if (comboBuiltInTaxaList.SelectedIndex < 0 && comboBuiltInTaxaList.Items.Count > 0)
        {
            comboBuiltInTaxaList.SelectedIndex = 0;
        }
        textTaxaListFile.Text = options.TaxaListFile ?? string.Empty;
        checkWithinImageSmoothing.Checked = options.ApplyWithinImageSmoothing;
        checkSequenceSmoothing.Checked = options.ApplySequenceLevelSmoothing;
        checkWriteLogFile.Checked = options.WriteLogFile;
    }

    private PostprocessingOptions ReadOptionsFromControls()
    {
        PostprocessingOptions options = _options.Clone();
        options.InputFile = textInputFile.Text.Trim().Trim('"');
        options.OutputFile = textOutputFile.Text.Trim().Trim('"');
        options.RemoveClassificationsFromNonAnimals = checkRemoveNonAnimal.Checked;
        options.ApplyTaxaList = checkApplyTaxaList.Checked;
        options.TaxaListSource = radioCsvTaxaList.Checked ? TaxaListSource.CsvFile : TaxaListSource.BuiltIn;
        options.BuiltInTaxaList = comboBuiltInTaxaList.SelectedItem as string ?? BuiltInResources.DefaultTaxaListName;
        options.TaxaListFile = textTaxaListFile.Text.Trim().Trim('"');
        options.ApplyWithinImageSmoothing = checkWithinImageSmoothing.Checked;
        options.ApplySequenceLevelSmoothing = checkSequenceSmoothing.Checked;
        options.WriteLogFile = checkWriteLogFile.Checked;
        return options;
    }

    private void UpdateControlState()
    {
        bool idle = !_running;

        foreach (Control c in new Control[] { textInputFile, buttonBrowseInput, textOutputFile, buttonBrowseOutput,
                                              checkRemoveNonAnimal, checkApplyTaxaList, checkWithinImageSmoothing,
                                              checkSequenceSmoothing, checkWriteLogFile, buttonAdvanced, buttonProcess })
        {
            c.Enabled = idle;
        }

        bool taxaListEnabled = idle && checkApplyTaxaList.Checked;
        radioBuiltInTaxaList.Enabled = taxaListEnabled;
        radioCsvTaxaList.Enabled = taxaListEnabled;
        comboBuiltInTaxaList.Enabled = taxaListEnabled && radioBuiltInTaxaList.Checked;
        textTaxaListFile.Enabled = taxaListEnabled && radioCsvTaxaList.Checked;
        buttonBrowseTaxaList.Enabled = taxaListEnabled && radioCsvTaxaList.Checked;

        buttonCancel.Enabled = _running;
    }

    private void OnControlStateChanged(object? sender, EventArgs e) => UpdateControlState();

    #endregion

    #region File selection

    private static string? ExistingDirectory(string path)
    {
        try
        {
            string? directory = Path.GetDirectoryName(path.Trim().Trim('"'));
            return !string.IsNullOrEmpty(directory) && Directory.Exists(directory) ? directory : null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private void buttonBrowseInput_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a MegaDetector/SpeciesNet results file",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            InitialDirectory = ExistingDirectory(textInputFile.Text) ?? string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textInputFile.Text = dialog.FileName;
        }
    }

    private void buttonBrowseOutput_Click(object? sender, EventArgs e)
    {
        string current = textOutputFile.Text.Trim().Trim('"');
        string input = textInputFile.Text.Trim().Trim('"');
        string suggestion = current.Length > 0 ? current :
                            input.Length > 0 ? Postprocessor.GetDefaultOutputFile(input) : string.Empty;

        using var dialog = new SaveFileDialog
        {
            Title = "Choose an output file",
            Filter = "JSON files (*.json)|*.json|All files (*.*)|*.*",
            DefaultExt = "json",
            // We ask about overwriting when processing starts
            OverwritePrompt = false,
            InitialDirectory = ExistingDirectory(suggestion) ?? string.Empty,
            FileName = suggestion.Length > 0 ? Path.GetFileName(suggestion) : string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textOutputFile.Text = dialog.FileName;
        }
    }

    private void buttonBrowseTaxaList_Click(object? sender, EventArgs e)
    {
        using var dialog = new OpenFileDialog
        {
            Title = "Choose a taxonomic mapping file",
            Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
            InitialDirectory = ExistingDirectory(textTaxaListFile.Text) ?? string.Empty
        };
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            textTaxaListFile.Text = dialog.FileName;
        }
    }

    private void OnFileDragEnter(object? sender, DragEventArgs e)
    {
        e.Effect = !_running && e.Data?.GetDataPresent(DataFormats.FileDrop) == true
            ? DragDropEffects.Copy
            : DragDropEffects.None;
    }

    private void OnFileDragDrop(object? sender, DragEventArgs e)
    {
        if (!_running && e.Data?.GetData(DataFormats.FileDrop) is string[] { Length: > 0 } files)
        {
            textInputFile.Text = files[0];
        }
    }

    #endregion

    #region Processing

    private void buttonAdvanced_Click(object? sender, EventArgs e)
    {
        using var dialog = new AdvancedOptionsForm(_options.Advanced);
        if (dialog.ShowDialog(this) == DialogResult.OK)
        {
            _options.Advanced = dialog.Options;
            _options = ReadOptionsFromControls();
            AppSettings.Save(_options);
        }
    }

    private void ShowError(string message)
    {
        MessageBox.Show(this, message, "SpeciesNet Postprocessor", MessageBoxButtons.OK, MessageBoxIcon.Error);
    }

    /// <summary>
    /// Checks the options before we start processing; returns false (after telling the user why)
    /// if we shouldn't start.
    /// </summary>
    private bool ConfirmOptions(PostprocessingOptions options)
    {
        if (options.InputFile.Length == 0)
        {
            ShowError("Please choose an input file.");
            return false;
        }
        if (!File.Exists(options.InputFile))
        {
            ShowError($"The input file doesn't exist:\n\n{options.InputFile}");
            return false;
        }
        if (options.ApplyTaxaList && options.TaxaListSource == TaxaListSource.CsvFile)
        {
            if (string.IsNullOrEmpty(options.TaxaListFile))
            {
                ShowError("Please choose a taxonomic mapping (.csv) file, or use a built-in mapping.");
                return false;
            }
            if (!File.Exists(options.TaxaListFile))
            {
                ShowError($"The taxonomic mapping file doesn't exist:\n\n{options.TaxaListFile}");
                return false;
            }
        }

        string outputFile;
        try
        {
            outputFile = Postprocessor.ResolveOutputFile(options);
            if (string.Equals(Path.GetFullPath(outputFile), Path.GetFullPath(options.InputFile),
                              StringComparison.OrdinalIgnoreCase))
            {
                ShowError("The output file can't be the same as the input file.");
                return false;
            }
        }
        catch (Exception e) when (e is ArgumentException or NotSupportedException or PathTooLongException)
        {
            ShowError($"Invalid output file: {e.Message}");
            return false;
        }

        if (File.Exists(outputFile))
        {
            DialogResult result = MessageBox.Show(this,
                $"The output file already exists:\n\n{outputFile}\n\nDo you want to overwrite it?",
                "SpeciesNet Postprocessor", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                MessageBoxDefaultButton.Button2);
            if (result != DialogResult.Yes)
            {
                return false;
            }
        }

        return true;
    }

    private void SetRunning(bool running)
    {
        _running = running;
        UpdateControlState();
        Cursor = running ? Cursors.AppStarting : Cursors.Default;
    }

    private void OnProgress(ProgressUpdate update)
    {
        if (!_running)
        {
            return;
        }
        double fraction = update.Fraction ?? 0;
        double overall = (update.StepIndex - 1 + fraction) / update.StepCount;
        progressBar.Value = Math.Clamp((int)Math.Round(overall * progressBar.Maximum), 0, progressBar.Maximum);
        labelStatus.Text = $"Step {update.StepIndex} of {update.StepCount}: {update.StepName}" +
                           (update.Fraction.HasValue ? $" ({fraction * 100:F0}%)" : string.Empty);
    }

    private async void buttonProcess_Click(object? sender, EventArgs e)
    {
        PostprocessingOptions options = ReadOptionsFromControls();
        _options = options;
        AppSettings.Save(options);

        if (!ConfirmOptions(options))
        {
            return;
        }

        string? logFile = null;
        try
        {
            logFile = options.WriteLogFile ? Postprocessor.GetLogFile(Postprocessor.ResolveOutputFile(options)) : null;
        }
        catch (Exception)
        {
            // Postprocessor.Run() will report any problems with the output path
        }

        string LogFileHint(string prefix) =>
            logFile != null && File.Exists(logFile) ? $"\n\n{prefix}\n\n{logFile}" : string.Empty;

        textLog.Clear();
        progressBar.Value = 0;
        SetRunning(true);

        _cancellationTokenSource = new CancellationTokenSource();
        CancellationToken token = _cancellationTokenSource.Token;
        var progress = new Progress<ProgressUpdate>(OnProgress);
        var log = new LogPaneLog(this);

        try
        {
            PostprocessingResult result = await Task.Run(() => Postprocessor.Run(options, log, progress, token));

            SetRunning(false);
            progressBar.Value = progressBar.Maximum;
            labelStatus.Text = "Finished";

            string message = $"Finished processing.  Results were written to:\n\n{result.OutputFile}";
            if (result.WarningCount > 0)
            {
                string warnings = result.WarningCount == 1 ? "1 warning" : $"{result.WarningCount} warnings";
                message += $"\n\nThere {(result.WarningCount == 1 ? "was" : "were")} {warnings}";
                message += result.LogFile != null ? $"; see the log file for details:\n\n{result.LogFile}" : ".";
            }
            MessageBox.Show(this, message, "SpeciesNet Postprocessor", MessageBoxButtons.OK,
                            result.WarningCount > 0 ? MessageBoxIcon.Warning : MessageBoxIcon.Information);
        }
        catch (OperationCanceledException)
        {
            SetRunning(false);
            progressBar.Value = 0;
            labelStatus.Text = "Cancelled";
        }
        catch (PostprocessingException ex)
        {
            SetRunning(false);
            labelStatus.Text = "Error";
            ShowError(ex.Message + LogFileHint("The log file may have more information:"));
        }
        catch (Exception ex)
        {
            SetRunning(false);
            labelStatus.Text = "Error";
            ShowError($"An unexpected error occurred: {ex.Message}" +
                      LogFileHint("Please send the log file to whoever supports this program:"));
        }
        finally
        {
            _cancellationTokenSource.Dispose();
            _cancellationTokenSource = null;

            // Release the memory used by the (possibly very large) results
            GCSettings.LargeObjectHeapCompactionMode = GCLargeObjectHeapCompactionMode.CompactOnce;
            GC.Collect();

            if (_closeWhenFinished)
            {
                Close();
            }
        }
    }

    private void buttonCancel_Click(object? sender, EventArgs e)
    {
        if (_cancellationTokenSource != null)
        {
            labelStatus.Text = "Cancelling...";
            buttonCancel.Enabled = false;
            _cancellationTokenSource.Cancel();
        }
    }

    private void MainForm_FormClosing(object? sender, FormClosingEventArgs e)
    {
        if (_running)
        {
            if (!_closeWhenFinished)
            {
                DialogResult result = MessageBox.Show(this,
                    "Processing is still running.  Do you want to cancel processing and exit?",
                    "SpeciesNet Postprocessor", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
                if (result == DialogResult.Yes)
                {
                    _closeWhenFinished = true;
                    buttonCancel_Click(this, EventArgs.Empty);
                }
            }
            // We'll close when processing stops
            e.Cancel = true;
            return;
        }

        _options = ReadOptionsFromControls();
        AppSettings.Save(_options);
    }

    #endregion
}
