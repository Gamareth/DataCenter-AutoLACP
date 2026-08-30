# AutoLACP Architecture

## Scope

AutoLACP is add-only automation around Data Center's native cable and LACP
state. It creates missing groups and strictly extends existing groups. Native
state remains the only persistent source of truth.

## Pipeline

1. The `RegisterCableConnection` postfix records the cable ID and supplied
   switch/server endpoint IDs.
2. A one-second trailing debounce batches cable bursts, capped at five seconds.
3. After the game is ready, every unique endpoint ID is passed to
   `NetworkMap.GetSwitchById`. Non-switchable endpoints stop there.
4. Each resulting native switch is processed once per batch.
5. `NetworkSwitch.GetConnectedDevices()` is decoded into remote device IDs and
   switch-side cable IDs. Native patch-panel endpoints are resolved to their
   final device IDs.
6. Every remote device with at least two physical routes becomes an independent
   plan. Patch-panel candidates are expanded to their complete native cable
   chains so Data Center can track removal of every segment.
7. The planner compares that native cable set with the affected LACP group.
8. The applier validates the cables, captures the current group, mutates native
   state, verifies the result, and requests route evaluation.

There is no pair-level work registry. For switch-to-switch links both switches
are processed; after the first successful synchronous mutation, the reverse
observation reads the resulting native group and becomes `NoOp`.

## Load handling

`LoadNetworkState` clears transient work and marks the incoming registrations
as a load batch. Processing waits for `isLoading == false`, completed post-load
evaluation, idle co-op apply state, and single-player/host authority. Missing
load registrations produce a visible warning; there is no fallback scan.

## Decisions

- fewer than two observed cable IDs: `NoOp`;
- no native group and at least two IDs: `Create`;
- one exact native group: `NoOp`;
- one native group whose membership is a strict subset: `Replace` as an
  extension while preserving its ID;
- non-additive membership, invalid membership, or multiple native groups:
  `Skip` without mutation.

## Apply safety

Immediately before mutation, every desired cable must still exist and be
complete. Existing group IDs are recaptured and must match the plan. Creation
and removal are read back from the native registry.

If mutation or verification fails, the applier attempts to remove the partial
result and restore the captured native groups with their original IDs and
memberships. This backup exists only on the apply stack and is discarded after
the operation; it is not shadow desired state.

## VLAN behavior

Custom VLAN restrictions are not tested or officially supported in version
1.0.0. The implementation below is retained as an experimental native path and
is not part of the public compatibility claim.

For direct groups, the applier compares every visible VLAN across the member
cable IDs with `IsVlanAllowedOnCable`. Patch-panel groups currently skip this
additional harmonization because their native membership also contains cable
segments that are not switch ports. Their VLAN behavior is a separate alpha
validation item.

## Ownership boundaries

- Data Center owns cable removal, native persistence, routing, and co-op data.
- AutoLACP does not patch cable removal.
- AutoLACP suppresses the native manual LACP actions while active. The native
  controls remain clickable as muted `[Auto]` information buttons, but never
  execute native create or remove behavior.
- Clients do not allocate or mutate LACP groups.
- No periodic scanner, custom save format, custom network opcode, gameplay
  setting, hotkey, or extra window exists. The only preference controls the
  independent Workshop updater.

## Workshop updates

The first installation remains manual. Subscribers may disable subsequent
automatic updates in `AutoLACP.cfg`. A candidate must pass native Workshop
subscription and install-state checks, assembly identity and version checks,
and SHA-256 comparison. A separate embedded helper waits for Data Center to
exit, verifies the candidate again, replaces only `Mods/AutoLACP.dll`, verifies
the installed result, and records an audit state and success marker.
