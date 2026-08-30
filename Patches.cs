using HarmonyLib;
using Il2Cpp;

namespace AutoLACP;

[HarmonyPatch(typeof(NetworkMap), nameof(NetworkMap.RegisterCableConnection))]
internal static class CableRegistrationPatch
{
    private static void Postfix(
        int cableId,
        string startSwitchID,
        string endSwitchID,
        string startServerID,
        string endServerID)
    {
        try
        {
            AutoLacpCoordinator.OnCableRegistered(
                cableId,
                startSwitchID,
                endSwitchID,
                startServerID,
                endServerID);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Cable registration hook failed for {cableId}. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}

[HarmonyPatch(
    typeof(WaypointInitializationSystem),
    nameof(WaypointInitializationSystem.LoadNetworkState))]
internal static class NetworkLoadPatch
{
    private static void Prefix()
    {
        try
        {
            AutoLacpCoordinator.OnNetworkLoadStarted();
        }
        catch (Exception exception)
        {
            AutoLacpLog.Error(
                $"Load hook failed. {exception.GetType().Name}: {exception.Message}");
        }
    }
}

[HarmonyPatch(
    typeof(NetworkSwitchConfiguration),
    nameof(NetworkSwitchConfiguration.CreateLACP))]
internal static class ManualCreateLacpPatch
{
    private static bool Prefix()
    {
        return ManualLacpOverrule.ShouldRunNativeAction("creation");
    }
}

[HarmonyPatch(
    typeof(NetworkSwitchConfiguration),
    nameof(NetworkSwitchConfiguration.RemoveLACP))]
internal static class ManualRemoveLacpPatch
{
    private static bool Prefix()
    {
        return ManualLacpOverrule.ShouldRunNativeAction("removal");
    }
}

internal static class ManualLacpOverrule
{
    internal static bool ShouldRunNativeAction(string action)
    {
        if (!AutoLacpCoordinator.IsAutomaticEnabled)
            return true;

        try
        {
            AutoLacpCoordinator.NotifyManualOverrule(action);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Manual LACP notification failed; the automatic overrule " +
                $"remains active. {exception.GetType().Name}: {exception.Message}");
        }
        return false;
    }
}

[HarmonyPatch(
    typeof(NetworkSwitchConfiguration),
    nameof(NetworkSwitchConfiguration.OpenConfig))]
internal static class NetworkSwitchConfigOpenPatch
{
    private static void Postfix(NetworkSwitchConfiguration __instance)
    {
        try
        {
            AutoLacpGui.Apply(__instance);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Automatic GUI status hook failed. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}

[HarmonyPatch(
    typeof(NetworkSwitchConfiguration),
    nameof(NetworkSwitchConfiguration.RefreshPortDisplay))]
internal static class NetworkSwitchConfigRefreshPatch
{
    private static void Postfix(NetworkSwitchConfiguration __instance)
    {
        try
        {
            AutoLacpGui.Apply(__instance);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Automatic GUI refresh hook failed. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
