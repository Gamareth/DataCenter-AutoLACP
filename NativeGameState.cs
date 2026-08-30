using Il2Cpp;

namespace AutoLACP;

internal static class NativeGameState
{
    internal static bool IsReady()
    {
        var networkSystem = WaypointInitializationSystem.Instance;
        return networkSystem is not null
            && NetworkMap.instance is not null
            && !networkSystem.isLoading
            && networkSystem.HasCompletedPostLoadEvaluation
            && !CoopWorldSync.IsApplying
            && !CoopWorldSync.IsApplyingRemoval;
    }

    internal static bool MayMutate()
    {
        return IsReady()
            && (!CoopWorldSync.CoopActive || CoopWorldSync.IsHost)
            && !CoopWorldSync.IsCoopClient;
    }

    internal static bool MayCollectCableEvent()
    {
        var networkSystem = WaypointInitializationSystem.Instance;
        return networkSystem is not null
            && NetworkMap.instance is not null
            && (!CoopWorldSync.CoopActive || CoopWorldSync.IsHost)
            && !CoopWorldSync.IsCoopClient;
    }
}
