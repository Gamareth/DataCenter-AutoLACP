# AutoLACP

AutoLACP is a lightweight Data Center mod that automatically creates and
extends the game's native LACP groups after cable connections are registered.

Version 1.0.0 includes a verified-payload Steam Workshop updater for releases
after the first manual installation. Automatic updates can be disabled with
`AutoUpdateFromWorkshop = false` in `UserData/AutoLACP.cfg`.

## Behavior

- `NetworkMap.RegisterCableConnection` is the only cable trigger.
- Cable bursts use a one-second trailing debounce with a five-second maximum.
- Save loading reuses the same registered-cable pipeline after native post-load
  evaluation completes.
- Each affected endpoint is resolved with `NetworkMap.GetSwitchById`.
- Every affected switch reads its connected devices once through the native
  `NetworkSwitch.GetConnectedDevices()` result.
- Two or more physical routes to the same remote device form an LACP candidate.
  Patch-panel ports are resolved through the native network map, and the full
  native cable chain is included in the group.
- A missing group is created; an exact group is unchanged.
- An existing group is extended only when the observed cable set is a strict
  superset. Non-additive, duplicate, or invalid native state is left unchanged
  and logged.
- Switch-to-switch connections are evaluated from both sides. The first
  successful native mutation makes the second evaluation a native no-op.
- Custom VLAN restrictions are not tested or officially supported in version
  1.0.0. The normal/default VLAN configuration remains the release target.
- Native LACP create/remove actions are suppressed while AutoLACP is active.
  Their native GUI buttons become muted `[Auto]` information buttons;
  clicking either explains when automatic checks run.
- Load results use one normal summary followed by grouped error messages when
  part of the load could not be evaluated safely.
- Physical cable removal remains entirely owned by Data Center.
- Only single-player or the authoritative co-op host may mutate LACP state.

AutoLACP has no custom save data, recurring topology scan, pair-deduplication
registry, custom cable-removal behavior, or custom network protocol. Its only
setting controls automatic Workshop updates.

## Build

```powershell
dotnet build AutoLACP.csproj -c Release `
  /p:DataCenterGamePath='D:\SteamLibrary\steamapps\common\Data Center'
```

## Release status

Version 1.0.0 has completed single-player validation for cable registration,
native LACP creation and extension, save/load persistence, native cable
removal, switch discovery, batching, GUI automation, and direct and chained
patch-panel routes. Custom VLAN restrictions and co-op convergence are outside
the supported release scope and are listed in `docs/TEST_PLAN.md`.

## License

GPL-3.0-or-later with the Data Center linking exception in
`LINKING_EXCEPTION.md`.
