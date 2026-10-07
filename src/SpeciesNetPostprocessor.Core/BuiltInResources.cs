using System.Reflection;

namespace SpeciesNetPostprocessor.Core;

/// <summary>
/// A taxa list that's built into the program.
/// </summary>
/// <param name="Name">display name</param>
/// <param name="ResourceFileName">file name in the Resources folder</param>
public sealed record BuiltInTaxaList(string Name, string ResourceFileName);

/// <summary>
/// Access to the data files that are embedded in the program.
/// </summary>
public static class BuiltInResources
{
    public const string DefaultTaxaListName = "Snapshot Serengeti";

    public static readonly IReadOnlyList<BuiltInTaxaList> TaxaLists =
    [
        new(DefaultTaxaListName, "snapshot_serengeti_speciesnet_20251031.csv")
    ];

    /// <summary>
    /// The SpeciesNet taxonomy file (taxonomy_release.txt from the SpeciesNet v4.0.3a model
    /// package, which is also what https://lila.science/speciesnet-taxonomy-file points to).
    /// </summary>
    public const string TaxonomyResourceFileName = "speciesnet_taxonomy_release_20260609.txt";

    public static BuiltInTaxaList GetTaxaList(string name) =>
        TaxaLists.FirstOrDefault(t => t.Name == name) ??
        throw new PostprocessingException($"Unknown built-in taxonomy mapping: {name}");

    public static string ReadText(string resourceFileName)
    {
        string resourceName = "SpeciesNetPostprocessor.Core.Resources." + resourceFileName;
        using Stream stream = Assembly.GetExecutingAssembly().GetManifestResourceStream(resourceName) ??
                              throw new InvalidOperationException($"Missing embedded resource {resourceName}");
        using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
        return reader.ReadToEnd();
    }
}
