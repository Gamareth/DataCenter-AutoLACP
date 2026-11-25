# AutoLACP Release Test Plan

## Evidence

Use a backed-up save and record the candidate DLL hash, game build, log path,
tester, and date. Build success and dry-run discovery are not mutation proof.

## Automated planner checks

- [x] One cable does not create a group.
- [x] Two unique cables create one group plan.
- [x] Duplicate cable observations collapse.
- [x] Exact native membership is unchanged.
- [x] A strict membership superset is accepted as an extension.
- [x] Same-size mismatches and membership shrinkage are skipped.
- [x] Duplicate native groups are skipped.
- [x] Empty, negative, duplicate, and validation-failed membership is skipped.
- [x] Unrelated groups are ignored.
- [x] Pair identity is direction independent.

## Version 1.0.1 regression checks

The following automated checks use controlled native-game doubles; they do not
replace in-game acceptance against IL2CPP.

- [x] Missing or mismatched anchors cannot mutate groups.
- [x] Partial apply and rollback failures still request native route evaluation.
- [x] Known chain failures isolate the affected remote device.
- [x] Unknown or overlapping malformed state prevents unsafe mutation.
- [x] Pair capture failure does not skip other pairs on the same switch.
- [x] Processing yields between pairs and resumes remaining work.
- [x] Load reset discards partially processed switch work.
- [x] A switch removed between frames is not mutated.
- [x] Runtime notifications aggregate across batches for 30 seconds.
- [x] Notification preferences suppress automatic messages, not manual explanations.
- [ ] In-game message overflow followed by new server connections keeps working.
- [ ] In-game load, live cable bursts, and patch-panel groups pass with this DLL.
- [ ] Measure frame times on a large network; 6 ms is a soft scheduling budget.

## Historical evidence: final 1.0.0 dry run

- [x] Loaded cables enter through `RegisterCableConnection`.
- [x] The load batch waits for the native readiness gate.
- [x] 3,498 cable registrations resolve to 393 native switches.
- [x] 8,446 connected-device tuples decode with zero failures.
- [x] Switch-to-switch pairs are processed from both sides.
- [x] The load processing phase completes in about 1.05 seconds.
- [x] No pair-deduplication registry or fallback topology scan is used.

## First mutation-enabled run

- [ ] Back up the save before launching the alpha.
- [x] Missing parallel groups are created once with exact cable membership.
- [x] Reverse switch-side observations become native no-ops.
- [x] The completion message reports only successful additions/extensions.
- [x] No native apply, rollback, tuple, or skipped-check errors appear.
- [x] Reload preserves group IDs, endpoints, and membership.
- [ ] A partial load reports one normal summary followed by one message per
      grouped error category.
- [ ] An empty network is distinguished from a network whose cable registration
      events were not reported to AutoLACP.
- [ ] A `Base_*` cable endpoint with a valid switch/router endpoint is processed
      without a warning or player-facing load error.

## Targeted behavior

- [x] Adding a second direct cable creates one group.
- [x] Adding a third cable strictly extends the group and preserves its ID.
- [x] An exact group is not recreated.
- [ ] A non-additive mismatch is left unchanged and logged.
- [x] SFP, router, firewall, server, load-balancer, and modular devices work.
- [x] One route through a patch panel remains a single route and creates no
      group by itself.
- [x] Direct, one-panel, and chained-panel routes to the same device produce one
      group with complete native cable-chain membership.
- [x] Native removal of one member updates or removes the group correctly.

Custom VLAN restrictions are not part of the tested or officially supported
version 1.0.0 behavior. VLAN-specific acceptance is intentionally deferred.

## GUI and co-op

- [x] Native LACP controls show muted `[Auto]` labels while preserving their
      original border colors.
- [x] Clicking either AutoLACP control shows the automatic-check explanation
      without invoking native create/remove behavior.
- [ ] Host placement converges on both peers.
- [ ] Client-originated placement reaches host processing.
- [ ] Extension, rollback, reload, late join, and native removal converge.
- [ ] Clients never allocate native LACP group IDs.

## Workshop updater

- [x] `AutoLACP.cfg` is created with `AutoUpdateFromWorkshop = true`.
- [ ] Setting `AutoUpdateFromWorkshop = false` prevents the Workshop check.
- [x] Newer, older, identical, same-version, and invalid-hash decisions pass
      the pure updater tests.
- [x] A newer hidden Workshop payload is verified and staged.
- [x] The staged payload replaces only `Mods/AutoLACP.dll` after Data Center
      exits.
- [x] The installed DLL SHA-256 matches the verified Workshop payload.
- [x] The next launch reports success and removes temporary updater files.

## Release gates

- [x] Release build has no compiler errors or code warnings.
- [x] All 34 planner, updater, and controlled game-boundary tests pass on `net6.0`.
- [x] Production source contains no dry-run branch, probe label, or obsolete
      topology-snapshot implementation.
- [x] No periodic scanner, removal hook, saved shadow state, or custom co-op
      protocol exists.
- [x] Single-player mutation acceptance passes before any public release.
- [ ] Co-op is not claimed until two-player acceptance passes.
- [x] Custom VLAN restrictions are explicitly not claimed for version 1.0.0.
- [x] The final Workshop item ID `3787887235` replaces placeholder `0`.
- [x] The exact final DLL passes one clean-launch in-game smoke test.
