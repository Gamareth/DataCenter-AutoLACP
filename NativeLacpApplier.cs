using Il2Cpp;

namespace AutoLACP;

internal sealed class NativeLacpApplier
{
    private readonly NativeLacpState _nativeLacp;

    internal NativeLacpApplier(NativeLacpState nativeLacp)
    {
        _nativeLacp = nativeLacp;
    }

    internal bool Apply(LacpDecision decision)
    {
        if (decision.Kind is LacpDecisionKind.NoOp or LacpDecisionKind.Skip)
            return true;
        if (!NativeGameState.MayMutate())
            return false;

        var map = NetworkMap.instance;
        if (map is null)
            return false;

        try
        {
            ValidateDesiredCables(map, decision);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Skipped stale LACP plan for {decision.Pair}. " +
                $"{exception.GetType().Name}: {exception.Message}");
            return false;
        }

        IReadOnlyList<NativeGroupSnapshot> backupGroups;
        try
        {
            backupGroups = _nativeLacp.CaptureGroupsForPair(decision.Pair)
                .ToArray();
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Skipped {decision.Pair}: native LACP backup capture failed. " +
                $"{exception.GetType().Name}: {exception.Message}");
            return false;
        }
        var currentGroupIds = backupGroups
            .Select(group => group.GroupId)
            .Distinct()
            .OrderBy(groupId => groupId);
        var plannedGroupIds = decision.ExistingGroupIds
            .Distinct()
            .OrderBy(groupId => groupId);
        if (!currentGroupIds.SequenceEqual(plannedGroupIds))
        {
            AutoLacpLog.Warning(
                $"Skipped {decision.Pair}: the native groups changed between " +
                "revalidation and backup capture.");
            return false;
        }

        try
        {
            if (decision.Kind == LacpDecisionKind.Replace)
                RemoveGroups(map, decision);

            CreateGroup(map, decision);

            RequestRouteEvaluation();
            return true;
        }
        catch (Exception exception)
        {
            AutoLacpLog.Error(
                $"Native LACP apply failed for {decision.Pair}. " +
                $"{exception.GetType().Name}: {exception.Message}");
            TryRollback(map, decision.Pair, backupGroups);
            return false;
        }
    }

    private static void ValidateDesiredCables(
        NetworkMap map,
        LacpDecision decision)
    {
        var cablePositions = CablePositions.instance;
        if (map.cableConnections is null || cablePositions is null)
            throw new InvalidOperationException(
                "Native cable topology is unavailable immediately before apply.");

        foreach (var cableId in decision.DesiredCableIds)
        {
            if (!map.cableConnections.ContainsKey(cableId)
                || !cablePositions.IsCableComplete(cableId))
            {
                throw new InvalidOperationException(
                    $"Cable {cableId} is no longer complete immediately before apply.");
            }
        }
    }

    private void RemoveGroups(NetworkMap map, LacpDecision decision)
    {
        foreach (var groupId in decision.ExistingGroupIds)
            map.RemoveLACPGroup(groupId);

        VerifyGroupsAbsent(decision.Pair, decision.ExistingGroupIds);

        if (CoopWorldSync.CoopActive && CoopWorldSync.IsHost)
        {
            CoopWorldSync.SyncLACPRemoved(
                decision.AnchorSwitchId,
                GetRemoteEndpoint(decision));
        }

        AutoLacpLog.Info(
            $"Removed native LACP group(s) " +
            $"[{string.Join(", ", decision.ExistingGroupIds)}] for " +
            $"{decision.Pair}.");
    }

    private void CreateGroup(NetworkMap map, LacpDecision decision)
    {
        if (decision.DesiredCableIds.Count < 2)
            throw new InvalidOperationException(
                "A native LACP group requires at least two cable IDs.");

        var nativeCableIds = ToNativeCableIds(decision.DesiredCableIds);

        var requestedGroupId = decision.Kind == LacpDecisionKind.Replace
            && decision.ExistingGroupIds.Count == 1
                ? decision.ExistingGroupIds[0]
                : -1;
        var remoteEndpoint = GetRemoteEndpoint(decision);
        var groupId = map.CreateLACPGroup(
            decision.AnchorSwitchId,
            remoteEndpoint,
            nativeCableIds,
            requestedGroupId);

        VerifyCreatedGroup(decision.Pair, groupId, decision.DesiredCableIds);

        if (CoopWorldSync.CoopActive && CoopWorldSync.IsHost)
        {
            CoopWorldSync.BroadcastLACPGroupSet(
                groupId,
                decision.AnchorSwitchId,
                remoteEndpoint,
                nativeCableIds);
        }

        HarmonizeAnchorVlans(decision);

        AutoLacpLog.Info(
            $"Created native LACP group {groupId} for {decision.Pair} with " +
            $"{decision.DesiredCableIds.Count} member cable(s) " +
            $"{AutoLacpLog.DescribeCableIds(decision.DesiredCableIds)}.");
    }

    private void VerifyGroupsAbsent(
        CanonicalEndpointPair pair,
        IReadOnlyList<int> groupIds)
    {
        var remaining = _nativeLacp.CaptureGroupsForPair(pair);
        if (remaining.Count > 0)
            throw new InvalidOperationException(
                $"Native LACP group(s) " +
                $"[{string.Join(", ", remaining.Select(group => group.GroupId))}] " +
                $"still exist after removing [{string.Join(", ", groupIds)}].");
    }

    private void VerifyCreatedGroup(
        CanonicalEndpointPair pair,
        int groupId,
        IReadOnlyList<int> desiredCableIds)
    {
        if (groupId < 0)
            throw new InvalidOperationException(
                $"Native LACP creation returned invalid group ID {groupId}.");

        var groups = _nativeLacp.CaptureGroupsForPair(pair);
        if (groups.Count != 1 || groups[0].GroupId != groupId)
            throw new InvalidOperationException(
                $"Expected exactly native LACP group {groupId} after creation, " +
                $"but found [{string.Join(", ", groups.Select(group => group.GroupId))}].");

        if (!groups[0].CableIds.OrderBy(id => id).SequenceEqual(
                desiredCableIds.OrderBy(id => id)))
        {
            throw new InvalidOperationException(
                $"Native LACP group {groupId} has unexpected membership.");
        }
    }

    private void TryRollback(
        NetworkMap map,
        CanonicalEndpointPair pair,
        IReadOnlyList<NativeGroupSnapshot> backupGroups)
    {
        try
        {
            var currentPairGroupIds = _nativeLacp.CaptureGroupsForPair(pair)
                .Select(group => group.GroupId)
                .ToArray();

            foreach (var groupId in currentPairGroupIds)
                map.RemoveLACPGroup(groupId);

            foreach (var backup in backupGroups.OrderBy(group => group.GroupId))
            {
                var cableIds = ToNativeCableIds(backup.CableIds);
                map.CreateLACPGroup(
                    backup.NativeDeviceA,
                    backup.NativeDeviceB,
                    cableIds,
                    backup.GroupId);
            }

            var restoredGroups = _nativeLacp.CaptureGroupsForPair(pair);
            var restoredExactly = restoredGroups.Count == backupGroups.Count
                && backupGroups.All(backup => restoredGroups.Any(restored =>
                    restored.GroupId == backup.GroupId
                    && string.Equals(
                        restored.NativeDeviceA,
                        backup.NativeDeviceA,
                        StringComparison.Ordinal)
                    && string.Equals(
                        restored.NativeDeviceB,
                        backup.NativeDeviceB,
                        StringComparison.Ordinal)
                    && restored.CableIds.OrderBy(id => id).SequenceEqual(
                        backup.CableIds.OrderBy(id => id))));
            if (!restoredExactly)
                throw new InvalidOperationException(
                    "Native LACP rollback did not restore the prior groups exactly.");

            AutoLacpLog.Warning(
                $"Restored {backupGroups.Count} prior native LACP group(s) " +
                $"for {pair} after a failed apply.");
            RequestRouteEvaluation();
        }
        catch (Exception rollbackException)
        {
            AutoLacpLog.Error(
                $"Rollback also failed for {pair}. " +
                $"{rollbackException.GetType().Name}: {rollbackException.Message}");
            return;
        }

        TrySynchronizeRollback(pair, backupGroups);
    }

    private static void TrySynchronizeRollback(
        CanonicalEndpointPair pair,
        IReadOnlyList<NativeGroupSnapshot> backupGroups)
    {
        if (!CoopWorldSync.CoopActive || !CoopWorldSync.IsHost)
            return;

        try
        {
            CoopWorldSync.SyncLACPRemoved(pair.First, pair.Second);
        }
        catch (Exception exception)
        {
            AutoLacpLog.Error(
                $"Local rollback completed for {pair}, but the co-op removal " +
                $"sync failed. {exception.GetType().Name}: {exception.Message}");
            return;
        }

        foreach (var backup in backupGroups.OrderBy(group => group.GroupId))
        {
            try
            {
                var cableIds = ToNativeCableIds(backup.CableIds);
                CoopWorldSync.BroadcastLACPGroupSet(
                    backup.GroupId,
                    backup.NativeDeviceA,
                    backup.NativeDeviceB,
                    cableIds);
            }
            catch (Exception exception)
            {
                AutoLacpLog.Error(
                    $"Could not broadcast restored LACP group " +
                    $"{backup.GroupId}. {exception.GetType().Name}: " +
                    exception.Message);
            }
        }
    }

    private static Il2CppSystem.Collections.Generic.List<int> ToNativeCableIds(
        IReadOnlyList<int> cableIds)
    {
        var nativeCableIds = new Il2CppSystem.Collections.Generic.List<int>();
        foreach (var cableId in cableIds)
            nativeCableIds.Add(cableId);
        return nativeCableIds;
    }

    private static string GetRemoteEndpoint(LacpDecision decision)
    {
        if (string.Equals(
                decision.AnchorSwitchId,
                decision.Pair.First,
                StringComparison.Ordinal))
        {
            return decision.Pair.Second;
        }
        if (string.Equals(
                decision.AnchorSwitchId,
                decision.Pair.Second,
                StringComparison.Ordinal))
        {
            return decision.Pair.First;
        }
        throw new InvalidOperationException(
            $"Anchor {decision.AnchorSwitchId} is not an endpoint of {decision.Pair}.");
    }

    private static void RequestRouteEvaluation()
    {
        try
        {
            WaypointInitializationSystem.Instance?.RequestRouteEvaluation();
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                $"Native route evaluation request failed after LACP apply. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }

    private void HarmonizeAnchorVlans(LacpDecision decision)
    {
        var networkSwitch = _nativeLacp.FindSwitch(decision.AnchorSwitchId);
        if (networkSwitch is null)
            throw new InvalidOperationException(
                $"Anchor switch {decision.AnchorSwitchId} is unavailable.");

        if (decision.DesiredCableIds.Count < 2)
            return;

        var portIndices = decision.DesiredCableIds
            .Select(cableId => networkSwitch.GetPortIndexForCable(cableId))
            .ToArray();
        if (portIndices.Any(portIndex => portIndex < 0))
        {
            AutoLacpLog.Info(
                $"Skipped automatic VLAN harmonization for {decision.Pair}: " +
                "the native LACP group contains a patch-panel cable chain.");
            return;
        }

        var visibleVlans = networkSwitch.GetVisibleVlans();
        if (visibleVlans is null)
            throw new InvalidOperationException(
                "Native VLAN state is unavailable for the LACP anchor.");

        var visibleVlanIds = new List<int>();
        foreach (var vlanId in visibleVlans)
            visibleVlanIds.Add(vlanId);
        if (visibleVlanIds.Count == 0)
            return;

        var baselineCableId = decision.DesiredCableIds[0];
        var requiresCorrection = false;
        foreach (var vlanId in visibleVlanIds)
        {
            var baselineAllowed = networkSwitch.IsVlanAllowedOnCable(
                baselineCableId,
                vlanId);
            for (var cableOffset = 1;
                 cableOffset < decision.DesiredCableIds.Count;
                 cableOffset++)
            {
                if (networkSwitch.IsVlanAllowedOnCable(
                        decision.DesiredCableIds[cableOffset],
                        vlanId) == baselineAllowed)
                {
                    continue;
                }

                requiresCorrection = true;
                break;
            }

            if (requiresCorrection)
                break;
        }

        if (!requiresCorrection)
            return;

        if (portIndices.Distinct().Count() != portIndices.Length)
        {
            throw new InvalidOperationException(
                "Multiple LACP cables resolved to the same native switch port.");
        }
        var baselinePort = portIndices[0];
        var disallowed = networkSwitch.GetDisallowedVlans(baselinePort);
        if (disallowed is null)
            throw new InvalidOperationException(
                "Native VLAN state is unavailable for the LACP anchor.");

        var originalDisallowedByPort = new Dictionary<int, HashSet<int>>();
        foreach (var portIndex in portIndices.Skip(1))
        {
            var originalDisallowed = networkSwitch.GetDisallowedVlans(portIndex);
            if (originalDisallowed is null)
                throw new InvalidOperationException(
                    $"Could not read VLAN state for anchor port {portIndex}.");
            var copy = new HashSet<int>();
            foreach (var vlanId in originalDisallowed)
                copy.Add(vlanId);
            originalDisallowedByPort[portIndex] = copy;
        }

        try
        {
            for (var portOffset = 1;
                 portOffset < portIndices.Length;
                 portOffset++)
            {
                var portIndex = portIndices[portOffset];
                foreach (var vlanId in visibleVlanIds)
                {
                    if (disallowed.Contains(vlanId))
                        networkSwitch.SetVlanDisallowed(portIndex, vlanId);
                    else
                        networkSwitch.SetVlanAllowed(portIndex, vlanId);
                }
            }
            if (CoopWorldSync.CoopActive && CoopWorldSync.IsHost)
                CoopWorldSync.SyncDeviceConfig(networkSwitch);
        }
        catch
        {
            var restored = RestoreVlans(
                networkSwitch,
                visibleVlanIds,
                originalDisallowedByPort);
            if (CoopWorldSync.CoopActive && CoopWorldSync.IsHost)
            {
                try
                {
                    CoopWorldSync.SyncDeviceConfig(networkSwitch);
                }
                catch (Exception syncException)
                {
                    restored = false;
                    AutoLacpLog.Error(
                        $"Could not synchronize restored VLAN state. " +
                        $"{syncException.GetType().Name}: {syncException.Message}");
                }
            }
            if (!restored)
            {
                AutoLacpLog.Error(
                    "VLAN restoration was incomplete after native LACP apply failure.");
            }
            throw;
        }
    }

    private static bool RestoreVlans(
        NetworkSwitch networkSwitch,
        IReadOnlyList<int> visibleVlanIds,
        IReadOnlyDictionary<int, HashSet<int>> originalDisallowedByPort)
    {
        var restored = true;
        foreach (var entry in originalDisallowedByPort)
        {
            foreach (var vlanId in visibleVlanIds)
            {
                try
                {
                    if (entry.Value.Contains(vlanId))
                        networkSwitch.SetVlanDisallowed(entry.Key, vlanId);
                    else
                        networkSwitch.SetVlanAllowed(entry.Key, vlanId);
                }
                catch (Exception exception)
                {
                    restored = false;
                    AutoLacpLog.Error(
                        $"Could not restore VLAN {vlanId} on port {entry.Key}. " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
            }
        }
        return restored;
    }
}
