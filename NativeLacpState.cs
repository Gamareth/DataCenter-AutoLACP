using Il2Cpp;

namespace AutoLACP;

internal sealed class NativeLacpState
{
    internal IReadOnlyList<NativeGroupSnapshot> CaptureGroupsForPair(
        CanonicalEndpointPair requestedPair,
        IReadOnlyList<int> protectedCableIds)
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
                if (group is not null && NativeGroupIsolation.IsUnrelated(
                        requestedPair, protectedCableIds,
                        group.deviceA, group.deviceB, CopyCableIds(group.cableIds)))
                {
                    AutoLacpLog.Warning(
                        $"Native LACP group {entry.Key} has missing endpoints; " +
                        $"its known cables are unrelated to {requestedPair} and it is left unchanged.");
                    continue;
                }
                throw new InvalidOperationException(
                    $"Native LACP group {entry.Key} is malformed and cannot " +
                    "be reconciled safely.");
            }

            var pair = new CanonicalEndpointPair(group.deviceA, group.deviceB);
            if (!pair.Equals(requestedPair))
            {
                if (group.cableIds is not null
                    && CopyCableIds(group.cableIds).Any(protectedCableIds.Contains))
                {
                    throw new InvalidOperationException(
                        $"Native LACP group {entry.Key} for another device pair " +
                        "claims a cable required by this pair.");
                }
                continue;
            }
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
