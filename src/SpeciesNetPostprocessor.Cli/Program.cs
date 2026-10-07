using System.Diagnostics;
using SpeciesNetPostprocessor.Core;
using SpeciesNetPostprocessor.Core.Json;

namespace SpeciesNetPostprocessor.Cli;

/// <summary>
/// Command-line interface to the SpeciesNet Postprocessor, mostly used for testing.
/// </summary>
internal static class Program
{
    private const string Usage =
        """
        Usage:

          SpeciesNetPostprocessor.Cli <input.json> [<output.json>] [options]

            --options <file.json>          options file (see --write-default-options)
            --intermediate-folder <dir>    write the results after each step to this folder
            --verbose                      print debug messages

          SpeciesNetPostprocessor.Cli --write-default-options <file.json>

          SpeciesNetPostprocessor.Cli --roundtrip <input.json> <output.json>

            Reads and re-writes a .json file (for testing the JSON reader/writer)
        """;

    private sealed class ConsoleLog(bool verbose) : IPostprocessingLog
    {
        public void Log(LogLevel level, string message)
        {
            if (level == LogLevel.Debug && !verbose)
            {
                return;
            }
            string prefix = level switch
            {
                LogLevel.Warning => "Warning: ",
                LogLevel.Error => "Error: ",
                LogLevel.Debug => "  ",
                _ => string.Empty
            };
            Console.WriteLine(prefix + message);
        }
    }

    private static int Main(string[] args)
    {
        try
        {
            return Run(args);
        }
        catch (PostprocessingException e)
        {
            // Errors from Postprocessor.Run() have already been printed by the log
            if (!e.Data.Contains(Postprocessor.ErrorLoggedKey))
            {
                Console.Error.WriteLine($"Error: {e.Message}");
            }
            return 1;
        }
    }

    private static int Run(string[] args)
    {
        if (args.Length == 0 || args.Contains("--help") || args.Contains("-h"))
        {
            Console.WriteLine(Usage);
            return args.Length == 0 ? 1 : 0;
        }

        if (args[0] == "--write-default-options" && args.Length == 2)
        {
            File.WriteAllText(args[1], new PostprocessingOptions().ToJson());
            Console.WriteLine($"Wrote default options to {args[1]}");
            return 0;
        }

        if (args[0] == "--roundtrip" && args.Length == 3)
        {
            var stopwatch = Stopwatch.StartNew();
            object? root = JsonFileReader.Load(args[1]);
            Console.WriteLine($"Read {args[1]} in {stopwatch.Elapsed.TotalSeconds:F1}s, " +
                              $"managed memory: {GC.GetTotalMemory(true) / (1024.0 * 1024):F0} MB");
            stopwatch.Restart();
            PythonJsonWriter.WriteFile(args[2], root);
            Console.WriteLine($"Wrote {args[2]} in {stopwatch.Elapsed.TotalSeconds:F1}s, " +
                              $"peak working set: {Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024):F0} MB");
            return 0;
        }

        var positional = new List<string>();
        string? optionsFile = null;
        string? intermediateFolder = null;
        bool verbose = false;

        for (int i = 0; i < args.Length; i++)
        {
            switch (args[i])
            {
                case "--options":
                    optionsFile = args[++i];
                    break;
                case "--intermediate-folder":
                    intermediateFolder = args[++i];
                    break;
                case "--verbose":
                    verbose = true;
                    break;
                default:
                    if (args[i].StartsWith("--", StringComparison.Ordinal))
                    {
                        Console.Error.WriteLine($"Unrecognized option {args[i]}\n\n{Usage}");
                        return 1;
                    }
                    positional.Add(args[i]);
                    break;
            }
        }

        if (positional.Count is < 1 or > 2)
        {
            Console.Error.WriteLine(Usage);
            return 1;
        }

        PostprocessingOptions options = optionsFile == null
            ? new PostprocessingOptions()
            : PostprocessingOptions.FromJson(File.ReadAllText(optionsFile));
        options.InputFile = positional[0];
        if (positional.Count == 2)
        {
            options.OutputFile = positional[1];
        }

        PostprocessingResult result = Postprocessor.Run(options, new ConsoleLog(verbose), null,
                                                        CancellationToken.None, intermediateFolder);
        Console.WriteLine($"Peak working set: {Process.GetCurrentProcess().PeakWorkingSet64 / (1024.0 * 1024):F0} MB");
        return 0;
    }
}
