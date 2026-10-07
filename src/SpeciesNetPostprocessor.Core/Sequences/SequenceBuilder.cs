using System.Globalization;
using System.Text.RegularExpressions;
using SpeciesNetPostprocessor.Core.Json;

namespace SpeciesNetPostprocessor.Core.Sequences;

/// <summary>
/// Sequence information for a single image (the equivalent of a COCO Camera Traps image
/// record after create_sequences()).
/// </summary>
public sealed class SequenceImage
{
    public required string FileName { get; init; }
    public required string Location { get; init; }
    public DateTime? DateTime { get; set; }
    public string SeqId { get; set; } = string.Empty;
    public int FrameNum { get; set; }
    public int SeqNumFrames { get; set; }
}

/// <summary>
/// Assembles images into sequences based on location (camera folder) and timestamp, using
/// timestamps that are already present in a results file.  Port of the relevant parts of
/// megadetector.data_management.read_exif.exif_results_to_cct() and
/// megadetector.data_management.cct_json_utils.create_sequences().
/// </summary>
public static partial class SequenceBuilder
{
    /// <summary>
    /// Timestamps older than this are assumed to be junk; lots of cameras use a default time in 2000.
    /// </summary>
    public const int MinValidTimestampYear = 2001;

    // Maximum number of images with timestamp problems to list in the log file
    private const int MaxImagesToList = 1000;

    // Overflow folder styles that should not be treated as separate cameras:
    //
    // 100RECNX is the overflow folder style for Reconyx cameras
    // 100EK113 is (for some reason) the overflow folder style for Bushnell cameras
    // 100_BTCF is the overflow folder style for Browning cameras
    // 100MEDIA is the overflow folder style used on a number of consumer-grade cameras
    private static readonly Regex[] OverflowFolderPatterns =
    [
        new(@"/\d+RECNX/"),
        new(@"/\d+EK\d+/"),
        new(@"/\d+_BTCF/"),
        new(@"/\d+MEDIA/"),
        new(@"/\d+DSCIM/")
    ];

    // Equivalent to Python's datetime.strptime(s, '%Y:%m:%d %H:%M:%S'), which allows one-digit
    // fields (other than the year) and any whitespace between the date and the time
    [GeneratedRegex(@"^(?<Y>[0-9]{4}):(?<m>1[0-2]|0[1-9]|[1-9]):(?<d>3[01]|[12][0-9]|0[1-9]|[1-9]| [1-9])\s+(?<H>2[0-3]|[01][0-9]|[0-9]):(?<M>[0-5][0-9]|[0-9]):(?<S>6[01]|[0-5][0-9]|[0-9])\z")]
    private static partial Regex ExifDateTimeRegex();

    // ISO-8601-style timestamps, e.g. 2022-12-01T09:17:12 or 2022-12-01 09:17:12.5
    [GeneratedRegex(@"^(?<Y>[0-9]{4})-(?<m>[0-9]{2})-(?<d>[0-9]{2})[T ](?<H>[0-9]{2}):(?<M>[0-9]{2}):(?<S>[0-9]{2})(?<f>\.[0-9]+)?\z")]
    private static partial Regex IsoDateTimeRegex();

    /// <summary>
    /// Removes common overflow folders (e.g. RECNX101, RECNX102) from paths, i.e. turns
    /// a\b\c\RECNX101\image001.jpg into a/b/c.  Returns the folder name if no overflow folders
    /// are present.  Always converts backslashes to slashes.  Port of
    /// megadetector.utils.ct_utils.image_file_to_camera_folder().
    /// </summary>
    public static string ImageFileToCameraFolder(string imageFn)
    {
        imageFn = imageFn.Replace('\\', '/');
        foreach (Regex pattern in OverflowFolderPatterns)
        {
            imageFn = pattern.Replace(imageFn, "/");
        }
        return PythonDirname(imageFn);
    }

    /// <summary>
    /// Equivalent to os.path.dirname() for paths that only use forward slashes.
    /// </summary>
    private static string PythonDirname(string path)
    {
        int i = path.LastIndexOf('/');
        if (i < 0)
        {
            return string.Empty;
        }
        string head = path.Substring(0, i + 1);
        string trimmed = head.TrimEnd('/');
        return trimmed.Length == 0 ? head : trimmed;
    }

    /// <summary>
    /// Parses a timestamp in standard EXIF format (%Y:%m:%d %H:%M:%S), or in ISO-8601 format;
    /// returns null if [s] can't be parsed.
    /// </summary>
    public static DateTime? ParseTimestamp(string? s)
    {
        if (s == null)
        {
            return null;
        }

        Match m = ExifDateTimeRegex().Match(s);
        if (!m.Success)
        {
            m = IsoDateTimeRegex().Match(s);
        }
        if (!m.Success)
        {
            return null;
        }

        try
        {
            var dt = new DateTime(int.Parse(m.Groups["Y"].Value, CultureInfo.InvariantCulture),
                                  int.Parse(m.Groups["m"].Value, CultureInfo.InvariantCulture),
                                  int.Parse(m.Groups["d"].Value.Trim(), CultureInfo.InvariantCulture),
                                  int.Parse(m.Groups["H"].Value, CultureInfo.InvariantCulture),
                                  int.Parse(m.Groups["M"].Value, CultureInfo.InvariantCulture),
                                  int.Parse(m.Groups["S"].Value, CultureInfo.InvariantCulture),
                                  DateTimeKind.Unspecified);
            Group fraction = m.Groups["f"];
            if (fraction.Success)
            {
                double seconds = double.Parse("0" + fraction.Value, CultureInfo.InvariantCulture);
                dt = dt.AddTicks((long)Math.Round(seconds * TimeSpan.TicksPerSecond));
            }
            return dt;
        }
        catch (ArgumentOutOfRangeException)
        {
            // E.g. February 30, or second 60
            return null;
        }
    }

    /// <summary>
    /// Returns the timestamp strings available for an image, in order of preference:
    /// exif_metadata.DateTimeOriginal (written by AddaxAI, and by MegaDetector with
    /// --include_exif_tags), then datetime (written by MegaDetector with
    /// --include_image_timestamp).
    /// </summary>
    private static IEnumerable<(string Source, string Value)> GetTimestampStrings(JsonObject im)
    {
        if (im.Get("exif_metadata") is JsonObject exif && exif.Get("DateTimeOriginal") is string dto)
        {
            yield return ("exif_metadata.DateTimeOriginal", dto);
        }
        if (im.Get("datetime") is string dt)
        {
            yield return ("datetime", dt);
        }
    }

    /// <summary>
    /// Assigns every image in [d] to a sequence.  Images are grouped by camera (see
    /// ImageFileToCameraFolder), sorted by time, and split into sequences wherever consecutive
    /// images are more than [episodeIntervalSeconds] apart.  Images without a valid timestamp
    /// become single-image sequences.
    /// </summary>
    /// <returns>Sequence information for each image, sorted by filename</returns>
    public static List<SequenceImage> CreateSequences(JsonObject d,
                                                      double episodeIntervalSeconds,
                                                      IPostprocessingLog log,
                                                      DateTime? now = null)
    {
        DateTime currentTime = now ?? System.DateTime.Now;

        // Sort by filename (with forward slashes), which is the order in which the MegaDetector
        // notebook enumerates images
        var images = JsonValues.GetImages(d)
            .Cast<JsonObject>()
            .OrderBy(im => ((string)im["file"]!).Replace('\\', '/'), StringComparer.Ordinal)
            .ToList();

        // Read timestamps (port of exif_results_to_cct)

        var imageInfo = new List<SequenceImage>(images.Count);

        int nWithoutTimestampField = 0;
        int nFailedImagesWithoutTimestamp = 0;
        var unparseableTimestamps = new List<string>();
        var invalidTimestamps = new List<string>();
        int nFromFallbackSource = 0;

        foreach (JsonObject im in images)
        {
            string fileName = (string)im["file"]!;
            var seqIm = new SequenceImage
            {
                FileName = fileName,
                Location = ImageFileToCameraFolder(fileName)
            };
            imageInfo.Add(seqIm);

            var timestampStrings = GetTimestampStrings(im).ToList();
            if (timestampStrings.Count == 0)
            {
                nWithoutTimestampField++;
                if (im.Get("failure") != null)
                {
                    nFailedImagesWithoutTimestamp++;
                }
                continue;
            }

            DateTime? dt = null;
            for (int i = 0; i < timestampStrings.Count; i++)
            {
                dt = ParseTimestamp(timestampStrings[i].Value);
                if (dt != null)
                {
                    if (i > 0)
                    {
                        nFromFallbackSource++;
                    }
                    break;
                }
            }

            if (dt == null)
            {
                unparseableTimestamps.Add($"{fileName}: {timestampStrings[0].Value}");
                continue;
            }

            // An image from the future (or within the last 24 hours) is invalid
            if ((currentTime - dt.Value).TotalSeconds <= 24 * 60 * 60)
            {
                invalidTimestamps.Add($"{fileName}: {dt.Value:yyyy-MM-dd HH:mm:ss} (in the future or within the last 24 hours)");
                continue;
            }

            // An image from before the dawn of time is also invalid
            if (dt.Value.Year < MinValidTimestampYear)
            {
                invalidTimestamps.Add($"{fileName}: {dt.Value:yyyy-MM-dd HH:mm:ss} (before {MinValidTimestampYear})");
                continue;
            }

            seqIm.DateTime = dt;
        }

        int nValid = imageInfo.Count(im => im.DateTime != null);
        int nWithoutValidTimestamp = imageInfo.Count - nValid;

        log.Info($"Found valid timestamps for {nValid} of {imageInfo.Count} images");

        if (nValid == 0)
        {
            foreach (string example in unparseableTimestamps.Concat(invalidTimestamps).Take(20))
            {
                log.Debug($"Example of an unusable timestamp: {example}");
            }
            string reason = nWithoutTimestampField == imageInfo.Count
                ? "no images in this file have timestamps (exif_metadata.DateTimeOriginal or datetime fields)"
                : "none of the timestamps in this file are valid";
            throw new PostprocessingException(
                $"Sequence-level smoothing requires image timestamps, but {reason}.  Uncheck " +
                "\"Apply sequence-level smoothing\" to process this file without sequence-level smoothing.");
        }

        if (nFromFallbackSource > 0)
        {
            log.Debug($"Used the \"datetime\" field for {nFromFallbackSource} images whose " +
                      "exif_metadata.DateTimeOriginal field could not be parsed");
        }

        // It's expected that images that failed to process don't have timestamps, so that's not a warning
        if (nFailedImagesWithoutTimestamp > 0)
        {
            log.Info($"{nFailedImagesWithoutTimestamp} images that failed to process have no timestamps");
        }

        int nWithoutTimestampFieldNotFailed = nWithoutTimestampField - nFailedImagesWithoutTimestamp;
        int nProblems = nWithoutTimestampFieldNotFailed + unparseableTimestamps.Count + invalidTimestamps.Count;

        if (nProblems > 0)
        {
            log.Warning($"{nProblems} images have no valid timestamp; each of these will be treated as its own " +
                        "sequence (see the log file for a list of images)");
            if (nWithoutTimestampFieldNotFailed > 0)
            {
                log.Warning($"{nWithoutTimestampFieldNotFailed} images have no timestamp");
                foreach (JsonObject im in images.Where(im => !GetTimestampStrings(im).Any() && im.Get("failure") == null)
                                                .Take(MaxImagesToList))
                {
                    log.Debug($"No timestamp: {im["file"]}");
                }
            }
            if (unparseableTimestamps.Count > 0)
            {
                log.Warning($"{unparseableTimestamps.Count} images have timestamps that could not be parsed");
                foreach (string s in unparseableTimestamps.Take(MaxImagesToList))
                {
                    log.Debug($"Unparseable timestamp: {s}");
                }
            }
            if (invalidTimestamps.Count > 0)
            {
                log.Warning($"{invalidTimestamps.Count} images have invalid timestamps (before {MinValidTimestampYear}, " +
                            "or in the future)");
                foreach (string s in invalidTimestamps.Take(MaxImagesToList))
                {
                    log.Debug($"Invalid timestamp: {s}");
                }
            }
        }

        // Assemble images into sequences (port of create_sequences)

        var locationToImages = new Dictionary<string, List<SequenceImage>>(StringComparer.Ordinal);
        foreach (SequenceImage im in imageInfo)
        {
            if (!locationToImages.TryGetValue(im.Location, out List<SequenceImage>? list))
            {
                list = new List<SequenceImage>();
                locationToImages[im.Location] = list;
            }
            list.Add(im);
        }

        var locations = locationToImages.Keys.ToList();
        locations.Sort(StringComparer.Ordinal);

        log.Info($"Found {locations.Count} locations (cameras)");

        int nSequences = 0;

        foreach (string location in locations)
        {
            // Sort by datetime, with invalid datetimes at the end (OrderBy is a stable sort,
            // like Python's sorted())
            var sortedImagesThisLocation = locationToImages[location]
                .OrderBy(im => im.DateTime == null)
                .ThenBy(im => im.DateTime ?? System.DateTime.MinValue)
                .ToList();

            var sequenceIdToImagesThisLocation = new OrderedDictionary<string, List<SequenceImage>>(StringComparer.Ordinal);

            string currentSequenceId = string.Empty;
            int nextFrameNumber = 0;
            int nextSequenceNumber = 0;
            DateTime? previousDatetime = null;
            double? delta = null;

            foreach (SequenceImage im in sortedImagesThisLocation)
            {
                bool invalidDatetime = false;

                if (previousDatetime == null)
                {
                    delta = null;
                }
                else if (im.DateTime == null)
                {
                    invalidDatetime = true;
                }
                else
                {
                    delta = (im.DateTime.Value - previousDatetime.Value).TotalSeconds;
                }

                // Start a new sequence if necessary, including the case where this datetime is invalid
                if (delta == null || delta > episodeIntervalSeconds || invalidDatetime)
                {
                    nextFrameNumber = 0;
                    currentSequenceId = $"location_{location}_sequence_index_{nextSequenceNumber:D5}";
                    nextSequenceNumber++;
                    nSequences++;
                }

                im.SeqId = currentSequenceId;
                im.FrameNum = nextFrameNumber;
                if (!sequenceIdToImagesThisLocation.TryGetValue(currentSequenceId, out List<SequenceImage>? seqImages))
                {
                    seqImages = new List<SequenceImage>();
                    sequenceIdToImagesThisLocation.Add(currentSequenceId, seqImages);
                }
                seqImages.Add(im);
                nextFrameNumber++;

                // If this was an invalid datetime, this will record the previous datetime
                // as null, which will force the next image to start a new sequence.
                previousDatetime = im.DateTime;
            }

            foreach (var kvp in sequenceIdToImagesThisLocation)
            {
                foreach (SequenceImage im in kvp.Value)
                {
                    im.SeqNumFrames = kvp.Value.Count;
                }
            }
        }

        log.Info($"Created {nSequences} sequences from {imageInfo.Count} images");

        return imageInfo;
    }
}
