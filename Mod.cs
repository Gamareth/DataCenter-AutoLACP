using MelonLoader;

[assembly: MelonInfo(
    typeof(AutoLACP.Mod),
    AutoLACP.Mod.DisplayName,
    AutoLACP.Mod.Version,
    AutoLACP.Mod.Author)]
[assembly: MelonGame("Waseku", "Data Center")]

namespace AutoLACP;

public sealed class Mod : MelonMod
{
    internal const string DisplayName = "AutoLACP";
    internal const string Version = "1.0.1";
    internal const string Author = "Gamareth";

    public override void OnInitializeMelon()
    {
        ModSettings.Initialize();
        WorkshopAutoUpdater.CleanupAfterSuccessfulUpdate();
        AutoLacpCoordinator.Initialize();
        var workshopUpdateState = ModSettings.AutoUpdateFromWorkshop
            ? "enabled for subscribers"
            : "disabled";
        LoggerInstance.Msg(
            $"{DisplayName} {Version} initialized. LACP is fully managed by " +
            "automatic native reconciliation. Workshop auto-update: " +
            $"{workshopUpdateState}.");
    }

    public override void OnLateInitializeMelon()
    {
        WorkshopAutoUpdater.CheckAfterInitialization();
    }

    public override void OnSceneWasInitialized(int buildIndex, string sceneName)
    {
        WorkshopAutoUpdater.CheckAfterInitialization();
    }

    public override void OnUpdate()
    {
        AutoLacpCoordinator.Update();
    }

    public override void OnApplicationQuit()
    {
        WorkshopAutoUpdater.OnApplicationQuit();
        AutoLacpCoordinator.Shutdown();
    }
}
