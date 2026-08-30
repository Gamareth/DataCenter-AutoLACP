using Il2Cpp;

namespace AutoLACP;

internal sealed class NativeLacpState
{
    internal IReadOnlyList<NativeGroupSnapshot> CaptureGroupsForPair(
        CanonicalEndpointPair requestedPair)
    {
        var groups = NetworkMap.instance?.GetAllLACPGroups();
        if (groups is null)
            throw new InvalidOperationException(
                "Native LACP group registry is unavailable during capture.");

        var snapshots = new List<NativeGroupSnapshot>();
        foreach (var entry in groups)
        {
            var group = entry.Value;
            if (group is null
                || string.IsNullOrWhiteSpace(group.deviceA)
                || string.IsNullOrWhiteSpace(group.deviceB))
            {
                throw new InvalidOperationException(
                    $"Native LACP group {entry.Key} is malformed and cannot " +
                    "be reconciled safely.");
            }

            var pair = new CanonicalEndpointPair(group.deviceA, group.deviceB);
            if (!pair.Equals(requestedPair))
                continue;
            if (entry.Key != group.groupId)
            {
                throw new InvalidOperationException(
                    $"Native LACP group dictionary key {entry.Key} does not " +
                    $"match stored group ID {group.groupId}.");
            }

            var cableIds = CopyCableIds(group.cableIds);
            snapshots.Add(new NativeGroupSnapshot(
                group.groupId,
                pair,
                cableIds,
                group.deviceA,
                group.deviceB,
                GetValidationIssue(cableIds)));
        }

        return snapshots;
    }

    internal NetworkSwitch FindSwitch(string switchId)
    {
        if (string.IsNullOrWhiteSpace(switchId))
            return null;
        return NetworkMap.instance?.GetSwitchById(switchId);
    }

    private static IReadOnlyList<int> CopyCableIds(
        Il2CppSystem.Collections.Generic.List<int> nativeCableIds)
    {
        if (nativeCableIds is null)
            return Array.Empty<int>();

        var cableIds = new int[nativeCableIds.Count];
        for (var index = 0; index < nativeCableIds.Count; index++)
            cableIds[index] = nativeCableIds[index];
        return cableIds;
    }

    private static string GetValidationIssue(IReadOnlyList<int> cableIds)
    {
        if (cableIds.Count < 2)
            return "Native group membership contains fewer than two cable IDs.";
        if (cableIds.Any(cableId => cableId < 0))
            return "Native group membership contains a negative cable ID.";
        if (cableIds.Distinct().Count() != cableIds.Count)
            return "Native group membership contains duplicate cable IDs.";

        var map = NetworkMap.instance;
        var cablePositions = CablePositions.instance;
        if (map?.cableConnections is null || cablePositions is null)
            return "Native cable state is unavailable while validating membership.";

        foreach (var cableId in cableIds)
        {
            if (!map.cableConnections.ContainsKey(cableId)
                || !cablePositions.IsCableComplete(cableId))
            {
                return $"Native group cable {cableId} is missing or incomplete.";
            }
        }

        return null;
    }
}
