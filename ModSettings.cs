using MelonLoader;
using MelonLoader.Utils;

namespace AutoLACP;

internal static class ModSettings
{
    internal const bool DefaultAutoUpdateFromWorkshop = true;
    internal const bool DefaultShowNotifications = true;

    private const string CategoryIdentifier = "AutoLACP";
    private const string PreferencesFileName = "AutoLACP.cfg";

    private static MelonPreferences_Category _category;
    private static MelonPreferences_Entry<bool> _autoUpdateFromWorkshop;
    private static MelonPreferences_Entry<bool> _showNotifications;

    internal static bool ShowNotifications =>
        _showNotifications?.Value ?? DefaultShowNotifications;

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

        _showNotifications = _category.CreateEntry(
            "ShowNotifications",
            DefaultShowNotifications,
            "Show automatic in-game notifications",
            "Show automatic load and runtime results, including errors. " +
            "Disabling this leaves logging and automatic LACP processing active. " +
            "Clicking an Auto button still shows its explanation.");

        _category.LoadFromFile(printmsg: false);
        _category.SaveToFile(printmsg: false);
    }
}
