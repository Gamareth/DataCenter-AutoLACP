namespace AutoLACP;

internal static class NativeGroupIsolation
{
    internal static bool IsUnrelated(
        CanonicalEndpointPair pair,
        IReadOnlyList<int> protectedCableIds,
        string deviceA,
        string deviceB,
        IReadOnlyList<int> groupCableIds)
    {
        // Unknown membership cannot prove independence. Never guess or repair it.
        if (protectedCableIds is null || protectedCableIds.Count == 0
            || groupCableIds is null || groupCableIds.Count == 0
            || groupCableIds.Any(id => id < 0))
            return false;

        if (deviceA == pair.First || deviceA == pair.Second
            || deviceB == pair.First || deviceB == pair.Second)
            return false;

        return !groupCableIds.Any(protectedCableIds.Contains);
    }
}
