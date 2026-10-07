namespace SpeciesNetPostprocessor.Core;

public enum LogLevel
{
    /// <summary>Detailed information that only goes to the log file.</summary>
    Debug,
    Info,
    Warning,
    Error
}

/// <summary>
/// Destination for messages generated during processing.
/// </summary>
public interface IPostprocessingLog
{
    void Log(LogLevel level, string message);
}

public static class PostprocessingLogExtensions
{
    public static void Debug(this IPostprocessingLog log, string message) => log.Log(LogLevel.Debug, message);
    public static void Info(this IPostprocessingLog log, string message) => log.Log(LogLevel.Info, message);
    public static void Warning(this IPostprocessingLog log, string message) => log.Log(LogLevel.Warning, message);
    public static void Error(this IPostprocessingLog log, string message) => log.Log(LogLevel.Error, message);
}

/// <summary>
/// A log that discards all messages.
/// </summary>
public sealed class NullLog : IPostprocessingLog
{
    public static readonly NullLog Instance = new();

    public void Log(LogLevel level, string message)
    {
    }
}

/// <summary>
/// A log that forwards messages to several other logs.
/// </summary>
public sealed class CompositeLog(params IPostprocessingLog[] logs) : IPostprocessingLog
{
    public void Log(LogLevel level, string message)
    {
        foreach (var log in logs)
        {
            log.Log(level, message);
        }
    }
}

/// <summary>
/// Writes log messages to a text file, flushing after every message so the file is useful
/// even if the process crashes.
/// </summary>
public sealed class FileLog : IPostprocessingLog, IDisposable
{
    private readonly StreamWriter _writer;
    private readonly object _lock = new();

    public FileLog(string path)
    {
        string? parent = Path.GetDirectoryName(Path.GetFullPath(path));
        if (!string.IsNullOrEmpty(parent))
        {
            Directory.CreateDirectory(parent);
        }
        _writer = new StreamWriter(path, append: false, new System.Text.UTF8Encoding(false));
        FilePath = path;
    }

    public string FilePath { get; }

    public void Log(LogLevel level, string message)
    {
        lock (_lock)
        {
            string prefix = level switch
            {
                LogLevel.Warning => "Warning: ",
                LogLevel.Error => "Error: ",
                _ => string.Empty
            };
            _writer.WriteLine($"[{DateTime.Now:yyyy-MM-dd HH:mm:ss}] {prefix}{message}");
            _writer.Flush();
        }
    }

    public void Dispose()
    {
        lock (_lock)
        {
            _writer.Dispose();
        }
    }
}

/// <summary>
/// Progress information reported during processing.
/// </summary>
/// <param name="StepIndex">1-based index of the current step</param>
/// <param name="StepCount">total number of steps in this run</param>
/// <param name="StepName">description of the current step</param>
/// <param name="Fraction">progress within the current step (0-1), or null if unknown</param>
public readonly record struct ProgressUpdate(int StepIndex, int StepCount, string StepName, double? Fraction);

/// <summary>
/// An error that should be reported to the user as-is (without a stack trace).
/// </summary>
public sealed class PostprocessingException(string message) : Exception(message);
