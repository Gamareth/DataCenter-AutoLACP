# Native API Notes

These notes describe the Data Center v1.1.8 IL2CPP surface used by the current
production pipeline. Proxy signatures prove callability, not runtime behavior.

## Cable and switch input

`NetworkMap.RegisterCableConnection` supplies the cable ID plus native switch
and server IDs for both ends. Positions and endpoint-type arguments are not
captured because the active algorithm does not use them.

`NetworkMap.GetSwitchById(string)` is the switch-capability lookup. It is used
instead of a global Unity object scan so modular router/firewall switch
components remain a native game concern.

## Connected devices

`NetworkSwitch.GetConnectedDevices()` returns
`List<ValueTuple<string, int>>`, where the runtime tuple renders as
`(deviceId, cableId)`. Direct access to the IL2CPP string field crashed in the
current runtime; the tuple therefore stays native and is decoded from its
`ToString()` representation. Any undecodable entry makes that switch capture
incomplete and prevents planning for the switch.

The runtime dry run decoded 8,446 records with zero failures. When a returned
device ID is a patch-panel port, the reader uses
`NetworkMap.ResolveThroughPatchPanel` for the final device and
`CableLink.CollectPatchPanelChainCables` for the native group membership. Live
validation remains required.

## Native LACP

The applier uses:

- `NetworkMap.GetAllLACPGroups()`;
- `NetworkMap.CreateLACPGroup(string, string, List<int>, int)`;
- `NetworkMap.RemoveLACPGroup(int)`;
- `CoopWorldSync.SyncLACPRemoved(string, string)`;
- `CoopWorldSync.BroadcastLACPGroupSet(int, string, string, List<int>)`; and
- `WaypointInitializationSystem.RequestRouteEvaluation()`.

The host mutates and verifies local native state before broadcasting. Exact
single-player and co-op ordering remains an alpha runtime gate.

## VLAN

These APIs document the existing experimental path. Custom VLAN restrictions
are not tested or officially supported in version 1.0.0.

The lazy VLAN path uses:

- `NetworkSwitch.GetVisibleVlans()`;
- `NetworkSwitch.IsVlanAllowedOnCable(int cableId, int vlanId)`;
- `NetworkSwitch.GetPortIndexForCable(int cableId)` only after a difference;
- `NetworkSwitch.GetDisallowedVlans(int portIndex)`;
- native allow/disallow setters; and
- `CoopWorldSync.SyncDeviceConfig(NetworkSwitch)` on the host.

## Authority and load

Mutation requires a ready `WaypointInitializationSystem`, a native network map,
completed post-load evaluation, idle co-op apply/removal state, and either
single-player or host authority. Clients collect no work and perform no LACP
mutation.
