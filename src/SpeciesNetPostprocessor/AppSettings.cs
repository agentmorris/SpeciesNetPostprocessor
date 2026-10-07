using SpeciesNetPostprocessor.Core;

namespace SpeciesNetPostprocessor;

/// <summary>
/// Persists settings between runs in %APPDATA%\SpeciesNetPostprocessor\settings.json.
/// </summary>
internal static class AppSettings
{
    public static string SettingsFile =>
        Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
                     "SpeciesNetPostprocessor", "settings.json");

    /// <summary>
    /// Loads saved settings, or returns defaults if there are no saved settings (or they can't be read).
    /// </summary>
    public static PostprocessingOptions Load()
    {
        try
        {
            if (File.Exists(SettingsFile))
            {
                PostprocessingOptions options = PostprocessingOptions.FromJson(File.ReadAllText(SettingsFile));
                if (!BuiltInResources.TaxaLists.Any(t => t.Name == options.BuiltInTaxaList))
                {
                    options.BuiltInTaxaList = BuiltInResources.DefaultTaxaListName;
                }
                return options;
            }
        }
        catch (Exception)
        {
            // Fall back to defaults if the settings file is corrupt
        }
        return new PostprocessingOptions();
    }

    public static void Save(PostprocessingOptions options)
    {
        try
        {
            Directory.CreateDirectory(Path.GetDirectoryName(SettingsFile)!);
            File.WriteAllText(SettingsFile, options.ToJson());
        }
        catch (Exception)
        {
            // Failing to save settings shouldn't interfere with processing
        }
    }
}
