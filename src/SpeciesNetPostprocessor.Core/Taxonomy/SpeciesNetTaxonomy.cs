namespace SpeciesNetPostprocessor.Core.Taxonomy;

/// <summary>
/// Constants and helpers for working with SpeciesNet taxonomy strings, ported from
/// megadetector.utils.wi_taxonomy_utils.
/// </summary>
/// <remarks>
/// Seven-token prediction strings look like:
///
///   [guid];mammalia;rodentia;sciuridae;sciurus;carolinensis;eastern gray squirrel
///
/// Some tools (e.g. AddaxAI) put something other than a GUID in the first token, so unlike
/// the Python code, the comparisons here that identify non-taxonomic and human categories
/// only look at the five taxonomic tokens.
/// </remarks>
public static class SpeciesNetTaxonomy
{
    public const string BlankPredictionString =
        "f1856211-cfb7-4a5b-9158-c0f72fd09ee6;;;;;;blank";
    public const string NoCvResultPredictionString =
        "f2efdae9-efb8-48fb-8a91-eccf79ab4ffb;no cv result;no cv result;no cv result;no cv result;no cv result;no cv result";
    public const string AnimalPredictionString =
        "1f689929-883d-4dae-958c-3d57ab5b6c16;;;;;;animal";
    public const string HumanPredictionString =
        "990ae9dd-7a59-4344-afcb-1b7b21368000;mammalia;primates;hominidae;homo;sapiens;human";
    public const string VehiclePredictionString =
        "e2895ed5-780b-48f6-8a11-9e27cb594511;;;;;;vehicle";

    /// <summary>
    /// Standard descriptions for well-known categories, used to fill in missing descriptions.
    /// </summary>
    public static readonly IReadOnlyDictionary<string, string> StandardDescriptionsByName =
        new Dictionary<string, string>(StringComparer.Ordinal)
        {
            ["blank"] = BlankPredictionString,
            ["animal"] = AnimalPredictionString,
            ["vehicle"] = VehiclePredictionString,
            ["no cv result"] = NoCvResultPredictionString,
            ["human"] = HumanPredictionString
        };

    // The five taxonomic tokens of the non-taxonomic prediction strings (blank, animal, and
    // vehicle all share the same empty tokens)
    private static readonly HashSet<string> NonTaxonomicShortStrings = new(StringComparer.Ordinal)
    {
        ShortString(BlankPredictionString)!,
        ShortString(NoCvResultPredictionString)!,
        ShortString(AnimalPredictionString)!,
        ShortString(VehiclePredictionString)!
    };

    private static readonly string HumanShortString = ShortString(HumanPredictionString)!;

    public static readonly string[] TaxonomyLevelNames =
        ["non-taxonomic", "kingdom", "phylum", "class", "order", "family", "genus", "species", "subspecies"];

    /// <summary>
    /// Returns the five taxonomic tokens of a seven-token prediction string (or a five-token
    /// taxonomy string) joined with semicolons, or null if [s] has neither 5 nor 7 tokens.
    /// </summary>
    public static string? ShortString(string s)
    {
        string[] tokens = s.Split(';');
        if (tokens.Length == 7)
        {
            return string.Join(';', tokens, 1, 5);
        }
        if (tokens.Length == 5)
        {
            return s;
        }
        return null;
    }

    /// <summary>
    /// Returns the first of several "|"-delimited descriptions.
    /// </summary>
    public static string FirstDescription(string s)
    {
        int i = s.IndexOf('|');
        return i < 0 ? s : s.Substring(0, i);
    }

    public static bool IsValidPredictionString(string s) =>
        s.Split(';').Length == 7 && string.Equals(s, s.ToLowerInvariant(), StringComparison.Ordinal);

    public static bool IsValidTaxonomyString(string s) =>
        s.Split(';').Length == 5 && string.Equals(s, s.ToLowerInvariant(), StringComparison.Ordinal);

    /// <summary>
    /// If [s] is a seven-token prediction string, trims the first token and the common name
    /// to produce a "clean" taxonomy string; if [s] is a five-token taxonomy string, returns it.
    /// Only the first of several "|"-delimited descriptions is used.
    /// </summary>
    public static string CleanTaxonomyString(string s)
    {
        s = FirstDescription(s);
        if (IsValidTaxonomyString(s))
        {
            return s;
        }
        if (IsValidPredictionString(s))
        {
            return string.Join(';', s.Split(';'), 1, 5);
        }
        throw new PostprocessingException($"Invalid taxonomy string: {s}");
    }

    /// <summary>
    /// Returns the taxonomy level up to which [s] is defined (0 for non-taxonomic, 3 for class,
    /// 4 for order, 5 for family, 6 for genus, 7 for species, 8 for subspecies).
    /// </summary>
    public static int TaxonomyLevelIndex(string s)
    {
        string? shortString = ShortString(s);
        if (shortString == null)
        {
            throw new PostprocessingException($"Illegal taxonomy string {s}");
        }
        if (NonTaxonomicShortStrings.Contains(shortString))
        {
            return 0;
        }

        string[] tokens = shortString.Split(';');

        // Anything without a class is considered non-taxonomic
        if (tokens[0].Length == 0)
        {
            return 0;
        }
        // Taxonomy strings start at class, so we never return 1 (kingdom) or 2 (phylum)
        if (tokens[1].Length == 0)
        {
            return 3;
        }
        if (tokens[2].Length == 0)
        {
            return 4;
        }
        if (tokens[3].Length == 0)
        {
            return 5;
        }
        if (tokens[4].Length == 0)
        {
            return 6;
        }
        // Subspecies are delimited with a space
        return tokens[4].Contains(' ') ? 8 : 7;
    }

    public static int TaxonomyLevelStringToIndex(string s)
    {
        int i = Array.IndexOf(TaxonomyLevelNames, s);
        if (i < 0)
        {
            throw new ArgumentException($"Unrecognized taxonomy level string {s}");
        }
        return i;
    }

    /// <summary>
    /// Determines whether [s] has taxonomic properties; this does not include blank,
    /// vehicle, no cv result, or animal.
    /// </summary>
    public static bool IsTaxonomicPredictionString(string s) => TaxonomyLevelIndex(s) > 0;

    /// <summary>
    /// Determines whether the taxonomic tokens of [s] are those of the SpeciesNet "human" category.
    /// </summary>
    public static bool IsHumanPredictionString(string s) =>
        string.Equals(ShortString(s), HumanShortString, StringComparison.Ordinal);
}
