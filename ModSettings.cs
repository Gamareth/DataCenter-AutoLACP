using MelonLoader;
using MelonLoader.Utils;

namespace AutoLACP;

internal static class ModSettings
{
    internal const bool DefaultAutoUpdateFromWorkshop = true;

    private const string CategoryIdentifier = "AutoLACP";
    private const string PreferencesFileName = "AutoLACP.cfg";

    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<bool> _autoUpdateFromWorkshop;

    internal static bool AutoUpdateFromWorkshop =>
        _autoUpdateFromWorkshop?.Value ?? DefaultAutoUpdateFromWorkshop;

    internal static void Initialize()
    {
        _category = MelonPreferences.CreateCategory(
            CategoryIdentifier,
            "AutoLACP");
        _category.SetFilePath(
            Path.Combine(MelonEnvironment.UserDataDirectory, PreferencesFileName),
            autoload: false,
            printmsg: false);

        _autoUpdateFromWorkshop = _category.CreateEntry(
            "AutoUpdateFromWorkshop",
            DefaultAutoUpdateFromWorkshop,
            "Automatically update from Steam Workshop",
            "After Workshop synchronization, stage a verified newer " +
            "AutoLACP DLL and install it after Data Center closes.");

        _category.LoadFromFile(printmsg: false);
        _category.SaveToFile(printmsg: false);
    }
}
