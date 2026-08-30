namespace AutoLACP;

internal static class LacpPlanner
{
    internal static LacpDecision BuildConnectedDeviceDecision(
        CanonicalEndpointPair pair,
        string anchorSwitchId,
        IReadOnlyList<int> observedCableIds,
        IReadOnlyList<NativeGroupSnapshot> groups)
    {
        var desiredCableIds = observedCableIds
            .Where(cableId => cableId >= 0)
            .Distinct()
            .OrderBy(cableId => cableId)
            .ToArray();
        var existingGroups = groups
            .Where(group => group is not null && group.Pair.Equals(pair))
            .OrderBy(group => group.GroupId)
            .ToArray();
        var existingGroupIds = existingGroups
            .Select(group => group.GroupId)
            .Distinct()
            .OrderBy(groupId => groupId)
            .ToArray();

        LacpDecision Decision(LacpDecisionKind kind, string reason) => new(
            kind,
            pair,
            anchorSwitchId,
            desiredCableIds,
            existingGroupIds,
            reason);

        var invalidGroup = existingGroups.FirstOrDefault(group =>
            !string.IsNullOrWhiteSpace(group.ValidationIssue)
            || group.CableIds.Count < 2
            || group.CableIds.Any(cableId => cableId < 0)
            || group.CableIds.Distinct().Count() != group.CableIds.Count);
        if (invalidGroup is not null)
        {
            return Decision(
                LacpDecisionKind.Skip,
                $"Native group {invalidGroup.GroupId} is invalid and is left " +
                $"unchanged. {invalidGroup.ValidationIssue}");
        }

        if (existingGroups.Length > 1)
        {
            return Decision(
                LacpDecisionKind.Skip,
                "Multiple native groups exist for the pair; conflicting state " +
                "is not removed automatically.");
        }

        if (desiredCableIds.Length < 2)
        {
            return Decision(
                existingGroups.Length == 0
                    ? LacpDecisionKind.NoOp
                    : LacpDecisionKind.Skip,
                existingGroups.Length == 0
                    ? "The connected device has fewer than two cable IDs."
                    : "A native group exists without two observed cable IDs; " +
                      "add-only reconciliation leaves it unchanged.");
        }

        var exact = existingGroups.Length == 1
            && SetsEqual(existingGroups[0].CableIds, desiredCableIds);
        var strictExpansion = existingGroups.Length == 1
            && existingGroups[0].CableIds.Count < desiredCableIds.Length
            && existingGroups[0].CableIds.All(desiredCableIds.Contains);
        return Decision(
            exact
                ? LacpDecisionKind.NoOp
                : existingGroups.Length == 0
                    ? LacpDecisionKind.Create
                    : strictExpansion
                        ? LacpDecisionKind.Replace
                        : LacpDecisionKind.Skip,
            exact
                ? "The native group already matches the connected cable IDs."
                : existingGroups.Length == 0
                    ? "The connected device needs a native LACP group."
                    : strictExpansion
                        ? "The connected cable IDs strictly extend the native group."
                        : "The native group differs non-additively and is left unchanged.");
    }

    private static bool SetsEqual(
        IReadOnlyList<int> left,
        IReadOnlyList<int> right)
    {
        return left.Count == right.Count
            && left.OrderBy(id => id).SequenceEqual(right.OrderBy(id => id));
    }
}
