# AutoLACP Implementation Review

## Candidate

Version 1.0.0 targets Data Center v1.1.8 through the current MelonLoader IL2CPP
proxy. Single-player behavior is accepted; co-op and custom VLAN restrictions
are outside the supported release scope.

## Current implementation

- one cable-registration trigger;
- one shared debounce/load pipeline;
- endpoint-based switch discovery through `GetSwitchById`;
- one `GetConnectedDevices()` read per affected native switch;
- no pair-level deduplication or shadow LACP administration;
- add-only pure planning with strict expansion checks;
- native create/remove, read-back verification, rollback, route evaluation,
  host broadcast, and lazy VLAN correction;
- native cable removal left untouched;
- automatic-only native GUI behavior;
- an independent, opt-out Workshop updater copied from the proven Auto Client
  Selector implementation.

## Cleanup result

The removed prototype topology snapshot, logical-path planner, retry flags,
unused registration positions/types, dry-run branches, probe prints, and their
obsolete tests are no longer compiled. Source and documentation describe only
the connected-device production pipeline.

Extended alpha diagnostics remain intentionally enabled: load batches,
connected-device captures for relevant switches, pair plans, native mutations,
and final counters are logged.

## Verified evidence

- The last read-only in-game run processed 3,498 cable registrations, 393
  switches, and 8,446 native connected-device records in about 1.05 seconds.
- That run reported zero tuple failures, skips, and AutoLACP exceptions.
- Switch-to-switch pairs were observed from both sides without pair skipping.
- The production source builds against the current proxy.
- Pure tests cover only the active planner contract.

## Remaining runtime boundary

Native creation, strict extension, persistence, direct and chained patch panels,
and native cable removal have passed live single-player acceptance. Automatic
Workshop replacement still requires a hidden end-to-end test with the final
item ID. Custom VLAN restrictions and co-op remain unclaimed.
