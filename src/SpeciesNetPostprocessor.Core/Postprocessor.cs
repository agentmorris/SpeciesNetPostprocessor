using System.Diagnostics;
using System.Reflection;
using System.Runtime.InteropServices;
using SpeciesNetPostprocessor.Core.Json;
using SpeciesNetPostprocessor.Core.Sequences;
using SpeciesNetPostprocessor.Core.Smoothing;
using SpeciesNetPostprocessor.Core.Taxonomy;

namespace SpeciesNetPostprocessor.Core;

/// <summary>
/// The outcome of a successful postprocessing run.
/// </summary>
public sealed record PostprocessingResult(string OutputFile, string? LogFile, int WarningCount, TimeSpan Elapsed);

/// <summary>
/// Runs the postprocessing steps from MegaDetector's manage_local_batch.py notebook on a results
/// file, in the same order as the notebook:
///
///   * apply a custom taxa list (restrict_to_taxa_list)
///   * remove classifications from non-animal detections
///   * within-image smoothing
///   * sequence-level smoothing
///   * remove unused classification categories
/// </summary>
public static class Postprocessor
{
    /// <summary>
    /// Set in Exception.Data for exceptions that Run() has already sent to the log.
    /// </summary>
    public const string ErrorLoggedKey = "SpeciesNetPostprocessor.ErrorLogged";

    public static string Version =>
        Assembly.GetExecutingAssembly().GetCustomAttribute<AssemblyInformationalVersionAttribute>()?
            .InformationalVersion.Split('+')[0] ?? "unknown";

    /// <summary>
    /// Returns the default output file for [inputFile], e.g. c:\images\results.json -->
    /// c:\images\results_processed.json.
    /// </summary>
    public static string GetDefaultOutputFile(string inputFile)
    {
        string directory = Path.GetDirectoryName(inputFile) ?? string.Empty;
        string name = Path.GetFileNameWithoutExtension(inputFile) + "_processed" + Path.GetExtension(inputFile);
        return Path.Combine(directory, name);
    }

    /// <summary>
    /// Returns the output file that will be used for [options].
    /// </summary>
    public static string ResolveOutputFile(PostprocessingOptions options) =>
        string.IsNullOrWhiteSpace(options.OutputFile)
            ? GetDefaultOutputFile(options.InputFile.Trim())
            : options.OutputFile.Trim();

    /// <summary>
    /// Returns the log file for [outputFile], e.g. c:\images\results_processed.json -->
    /// c:\images\results_processed.txt.
    /// </summary>
    public static string GetLogFile(string outputFile)
    {
        string logFile = Path.ChangeExtension(outputFile, ".txt");
        if (string.Equals(Path.GetFullPath(logFile), Path.GetFullPath(outputFile), StringComparison.OrdinalIgnoreCase))
        {
            logFile = outputFile + ".log.txt";
        }
        return logFile;
    }

    /// <summary>
    /// Counts warnings, so we can tell the user whether there's anything worth looking at in the log.
    /// </summary>
    private sealed class WarningCountingLog(IPostprocessingLog inner) : IPostprocessingLog
    {
        public int WarningCount { get; private set; }

        public void Log(LogLevel level, string message)
        {
            if (level == LogLevel.Warning)
            {
                WarningCount++;
            }
            inner.Log(level, message);
        }
    }

    /// <summary>
    /// Runs all the selected postprocessing steps.  Throws PostprocessingException for problems
    /// that should be reported to the user, and OperationCanceledException if [cancellationToken]
    /// is cancelled.
    /// </summary>
    /// <param name="intermediateFolder">if not null, the results after each step are written to
    /// this folder (for debugging)</param>
    public static PostprocessingResult Run(PostprocessingOptions options,
                                           IPostprocessingLog log,
                                           IProgress<ProgressUpdate>? progress = null,
                                           CancellationToken cancellationToken = default,
                                           string? intermediateFolder = null)
    {
        if (string.IsNullOrWhiteSpace(options.InputFile))
        {
            throw new PostprocessingException("No input file specified");
        }

        string outputFile = ResolveOutputFile(options);
        string? logFile = options.WriteLogFile ? GetLogFile(outputFile) : null;

        FileLog? fileLog = null;
        if (logFile != null)
        {
            try
            {
                fileLog = new FileLog(logFile);
            }
            catch (Exception e) when (e is IOException or UnauthorizedAccessException)
            {
                throw new PostprocessingException($"Could not create log file {logFile}: {e.Message}");
            }
        }

        var countingLog = new WarningCountingLog(fileLog == null ? log : new CompositeLog(log, fileLog));
        var stopwatch = Stopwatch.StartNew();

        try
        {
            RunInternal(options, outputFile, logFile, countingLog, progress, cancellationToken, intermediateFolder);
            stopwatch.Stop();
            string warningString = countingLog.WarningCount == 0 ? "no warnings" :
                countingLog.WarningCount == 1 ? "1 warning" : $"{countingLog.WarningCount} warnings";
            countingLog.Info($"Finished processing in {FormatElapsed(stopwatch.Elapsed)} ({warningString})");
            return new PostprocessingResult(outputFile, logFile, countingLog.WarningCount, stopwatch.Elapsed);
        }
        catch (OperationCanceledException)
        {
            countingLog.Warning("Processing was cancelled, no output file was written");
            throw;
        }
        catch (PostprocessingException e)
        {
            countingLog.Error(e.Message);
            e.Data[ErrorLoggedKey] = true;
            throw;
        }
        catch (Exception e)
        {
            countingLog.Error($"Unexpected error: {e}");
            e.Data[ErrorLoggedKey] = true;
            throw;
        }
        finally
        {
            fileLog?.Dispose();
        }
    }

    private static string FormatElapsed(TimeSpan t) =>
        t.TotalMinutes >= 1 ? $"{(int)t.TotalMinutes} minutes, {t.Seconds} seconds" : $"{t.TotalSeconds:F1} seconds";

    private static void LogHeader(PostprocessingOptions options, string outputFile, string? logFile, IPostprocessingLog log)
    {
        log.Info($"SpeciesNet Postprocessor version {Version}");
        log.Debug($"Started at {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
        log.Debug($"OS: {RuntimeInformation.OSDescription} ({RuntimeInformation.OSArchitecture})");
        log.Debug($"Runtime: {RuntimeInformation.FrameworkDescription} ({RuntimeInformation.ProcessArchitecture})");
        log.Debug($"Processors: {Environment.ProcessorCount}, available memory: " +
                  $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024.0 * 1024 * 1024):F1} GB");
        log.Info($"Input file: {options.InputFile}");
        log.Info($"Output file: {outputFile}");
        if (logFile != null)
        {
            log.Debug($"Log file: {logFile}");
        }
        log.Debug($"Options:\n{options.ToJson()}");
    }

    private static void LogInputSummary(JsonObject d, IPostprocessingLog log)
    {
        List<object?> images = JsonValues.GetImages(d);
        int nFailures = 0;
        long nDetections = 0;
        long nClassifiedDetections = 0;
        foreach (object? imObj in images)
        {
            var im = (JsonObject)imObj!;
            if (im.Get("failure") != null)
            {
                nFailures++;
            }
            List<object?>? detections = JsonValues.GetDetections(im);
            if (detections == null)
            {
                continue;
            }
            nDetections += detections.Count;
            foreach (object? detObj in detections)
            {
                if (JsonValues.GetClassifications((JsonObject)detObj!) is { Count: > 0 })
                {
                    nClassifiedDetections++;
                }
            }
        }

        log.Info($"Loaded {images.Count} images ({nFailures} failed images), {nDetections} detections " +
                 $"({nClassifiedDetections} with classifications)");

        int nClassificationCategories = (d.Get("classification_categories") as JsonObject)?.Count ?? 0;
        int nDescriptions = (d.Get("classification_category_descriptions") as JsonObject)?.Count ?? 0;
        log.Debug($"Classification categories: {nClassificationCategories}, descriptions: {nDescriptions}");
        log.Debug($"Top-level fields: {string.Join(", ", d.Keys)}");
        if (d.Get("info") is JsonObject info)
        {
            log.Debug($"Info: {PythonJsonWriter.Serialize(info)}");
        }
        log.Debug($"Detection categories: {PythonJsonWriter.Serialize(d.Get("detection_categories"))}");
    }

    private static void RunInternal(PostprocessingOptions options,
                                    string outputFile,
                                    string? logFile,
                                    IPostprocessingLog log,
                                    IProgress<ProgressUpdate>? progress,
                                    CancellationToken cancellationToken,
                                    string? intermediateFolder)
    {
        LogHeader(options, outputFile, logFile, log);

        string inputFile = options.InputFile.Trim();
        AdvancedOptions advanced = options.Advanced;

        // Check files

        if (!File.Exists(inputFile))
        {
            throw new PostprocessingException($"Input file {inputFile} does not exist");
        }
        if (string.Equals(Path.GetFullPath(inputFile), Path.GetFullPath(outputFile), StringComparison.OrdinalIgnoreCase))
        {
            throw new PostprocessingException("The output file can't be the same as the input file");
        }
        if (logFile != null &&
            string.Equals(Path.GetFullPath(inputFile), Path.GetFullPath(logFile), StringComparison.OrdinalIgnoreCase))
        {
            throw new PostprocessingException($"The log file ({logFile}) can't be the same as the input file");
        }

        // Set up the list of steps (for progress reporting)

        var steps = new List<string> { "Reading input file", "Checking input file" };
        if (options.ApplyTaxaList)
        {
            steps.Add("Applying taxonomic mapping");
        }
        if (options.RemoveClassificationsFromNonAnimals)
        {
            steps.Add("Removing classifications from non-animal detections");
        }
        if (options.ApplyWithinImageSmoothing)
        {
            steps.Add("Within-image smoothing");
        }
        if (options.ApplySequenceLevelSmoothing)
        {
            steps.Add("Sequence-level smoothing");
        }
        if (advanced.RemoveUnusedCategories)
        {
            steps.Add("Removing unused categories");
        }
        steps.Add("Writing output file");

        int currentStep = 0;
        void StartStep(string name)
        {
            currentStep = steps.IndexOf(name) + 1;
            Debug.Assert(currentStep > 0);
            log.Info($"Step {currentStep} of {steps.Count}: {name}");
            progress?.Report(new ProgressUpdate(currentStep, steps.Count, name, 0));
        }
        void ReportProgress(double fraction)
        {
            progress?.Report(new ProgressUpdate(currentStep, steps.Count, steps[currentStep - 1], fraction));
        }

        int intermediateIndex = 0;
        void WriteIntermediate(JsonObject d, string name)
        {
            if (intermediateFolder == null)
            {
                return;
            }
            intermediateIndex++;
            string fn = Path.Combine(intermediateFolder, $"step{intermediateIndex}_{name}.json");
            log.Debug($"Writing intermediate results to {fn}");
            PythonJsonWriter.WriteFile(fn, d);
        }

        // Read the taxa list and taxonomy before loading the (possibly large) results file, so we
        // fail fast if there's a problem with either.

        var taxaListOptions = new TaxaListRestrictionOptions
        {
            AllowWalkDown = advanced.AllowWalkDown,
            UseOriginalCommonNamesIfAvailable = advanced.UseOriginalCommonNamesIfAvailable,
            AddPreFilteringDescription = advanced.IncludeDescriptionFields,
            AddPostFilteringDescription = advanced.IncludeDescriptionFields
        };

        OrderedDictionary<string, string>? targetLatinToCommon = null;
        string? taxonomyText = null;

        if (options.ApplyTaxaList)
        {
            string csvText;
            string csvSourceName;
            if (options.TaxaListSource == TaxaListSource.BuiltIn)
            {
                BuiltInTaxaList taxaList = BuiltInResources.GetTaxaList(options.BuiltInTaxaList);
                csvText = BuiltInResources.ReadText(taxaList.ResourceFileName);
                csvSourceName = $"built-in taxonomy mapping \"{taxaList.Name}\" ({taxaList.ResourceFileName})";
            }
            else
            {
                if (string.IsNullOrWhiteSpace(options.TaxaListFile))
                {
                    throw new PostprocessingException("No taxonomy mapping .csv file specified");
                }
                string taxaListFile = options.TaxaListFile.Trim();
                if (!File.Exists(taxaListFile))
                {
                    throw new PostprocessingException($"Taxonomy mapping file {taxaListFile} does not exist");
                }
                csvText = File.ReadAllText(taxaListFile);
                csvSourceName = taxaListFile;
            }
            targetLatinToCommon = TaxaListRestriction.ReadTaxaList(csvText, csvSourceName, taxaListOptions, log);

            if (string.IsNullOrWhiteSpace(advanced.TaxonomyFile))
            {
                taxonomyText = BuiltInResources.ReadText(BuiltInResources.TaxonomyResourceFileName);
                log.Debug($"Using built-in SpeciesNet taxonomy ({BuiltInResources.TaxonomyResourceFileName})");
            }
            else
            {
                string taxonomyFile = advanced.TaxonomyFile.Trim();
                if (!File.Exists(taxonomyFile))
                {
                    throw new PostprocessingException($"SpeciesNet taxonomy file {taxonomyFile} does not exist");
                }
                taxonomyText = File.ReadAllText(taxonomyFile);
                log.Info($"Using SpeciesNet taxonomy file {taxonomyFile}");
            }
        }

        if (!options.ApplyTaxaList && !options.RemoveClassificationsFromNonAnimals &&
            !options.ApplyWithinImageSmoothing && !options.ApplySequenceLevelSmoothing)
        {
            log.Warning("No processing steps are selected; the output will be a copy of the input " +
                        (advanced.RemoveUnusedCategories ? "(with unused categories removed)" : string.Empty));
        }

        // Read input

        StartStep("Reading input file");
        log.Info($"Reading {new FileInfo(inputFile).Length / (1024.0 * 1024.0):F1} MB from {inputFile}");
        object? root;
        try
        {
            root = JsonFileReader.Load(inputFile,
                                       (read, total) => ReportProgress(total == 0 ? 1.0 : (double)read / total),
                                       cancellationToken);
        }
        catch (System.Text.Json.JsonException e)
        {
            throw new PostprocessingException($"Error reading {inputFile}, this does not appear to be a valid " +
                                              $".json file: {e.Message}");
        }

        // Validate input

        StartStep("Checking input file");
        JsonObject d = ResultsValidation.ValidateInputStructure(root, log);
        LogInputSummary(d, log);

        var (inputErrors, inputWarnings) = ResultsValidation.ValidateBatchResults(d);
        foreach (string s in inputErrors)
        {
            log.Warning($"Input file is not fully compliant with the MegaDetector output format: {s}");
        }
        foreach (string s in inputWarnings)
        {
            log.Debug($"Input file validation: {s}");
        }

        ResultsValidation.NormalizeClassificationDescriptions(d, log);

        if (options.ApplyTaxaList && d.Get("classification_category_descriptions") is not JsonObject)
        {
            throw new PostprocessingException(
                "This file has no classification category descriptions (\"classification_category_descriptions\"), " +
                "which are required for taxonomic mapping; this may not be a SpeciesNet results file.  Uncheck " +
                "\"Apply taxonomic mapping\" to process this file without taxonomic mapping.");
        }

        // Build sequences now, so we fail fast if there are no timestamps (sequence information only
        // depends on filenames and timestamps, which none of the other steps change)
        List<SequenceImage>? sequenceImages = null;
        if (options.ApplySequenceLevelSmoothing)
        {
            if (d.Get("images") is List<object?> images)
            {
                var duplicates = images.Cast<JsonObject>()
                    .GroupBy(im => (string)im["file"]!, StringComparer.Ordinal)
                    .Where(g => g.Count() > 1)
                    .Select(g => g.Key)
                    .ToList();
                if (duplicates.Count > 0)
                {
                    throw new PostprocessingException(
                        $"{duplicates.Count} filenames appear more than once in this file (e.g. {duplicates[0]}), " +
                        "so sequence-level smoothing can't be applied");
                }
            }
            log.Info("Assembling images into sequences");
            sequenceImages = SequenceBuilder.CreateSequences(d, advanced.EpisodeIntervalSeconds, log);
        }

        cancellationToken.ThrowIfCancellationRequested();

        ClassificationSmoothingOptions smoothingOptions = advanced.ToSmoothingOptions();

        // Possibly apply a custom taxa list

        if (options.ApplyTaxaList)
        {
            StartStep("Applying taxonomic mapping");
            TaxaListRestriction.RestrictToTaxaList(d, targetLatinToCommon!, taxonomyText!, taxaListOptions, log,
                                                   ReportProgress, cancellationToken);
            WriteIntermediate(d, "custom_taxa");
        }

        // Possibly remove classifications from non-animal detections

        if (options.RemoveClassificationsFromNonAnimals)
        {
            StartStep("Removing classifications from non-animal detections");
            ResultsCleanup.RemoveClassificationsFromNonAnimalDetections(d, log);
            WriteIntermediate(d, "animal_classifications_only");
        }

        // Within-image classification smoothing

        if (options.ApplyWithinImageSmoothing)
        {
            StartStep("Within-image smoothing");
            ClassificationSmoothing.SmoothClassificationResultsImageLevel(d, smoothingOptions, log,
                                                                         ReportProgress, cancellationToken);
            WriteIntermediate(d, "within_image_smoothing");
        }

        // Sequence-level smoothing

        if (options.ApplySequenceLevelSmoothing)
        {
            StartStep("Sequence-level smoothing");
            ClassificationSmoothing.SmoothClassificationResultsSequenceLevel(d, sequenceImages!, smoothingOptions, log,
                                                                            ReportProgress, cancellationToken);
            WriteIntermediate(d, "seqsmoothing");
        }

        // Remove unused categories

        if (advanced.RemoveUnusedCategories)
        {
            StartStep("Removing unused categories");
            ResultsCleanup.RemoveClassificationCategoriesBelowCount(d, 1, log);
        }

        var (outputErrors, outputWarnings) = ResultsValidation.ValidateBatchResults(d);
        foreach (string s in outputErrors)
        {
            log.Warning($"Output file is not fully compliant with the MegaDetector output format: {s}");
        }
        foreach (string s in outputWarnings)
        {
            log.Debug($"Output file validation: {s}");
        }

        // Write output, via a temporary file so we never leave a partially-written output file

        StartStep("Writing output file");
        string tempFile = outputFile + ".tmp";
        try
        {
            PythonJsonWriter.WriteFile(tempFile, d, 1,
                                       (i, n) => ReportProgress(n == 0 ? 1.0 : (double)i / n),
                                       cancellationToken);
            File.Move(tempFile, outputFile, overwrite: true);
        }
        catch (Exception e) when (e is IOException or UnauthorizedAccessException)
        {
            throw new PostprocessingException($"Error writing output file {outputFile}: {e.Message}");
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }

        log.Info($"Wrote results to {outputFile}");
    }
}
