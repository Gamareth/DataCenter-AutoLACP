# Changelog

## 1.0.1

- Check the soft frame budget between device pairs as well as switch captures.
  Native calls and each apply/rollback transaction remain indivisible.
- Continue with other pairs on a switch after a pair-specific processing failure.
- Added coordinator regression checks for yielding/resuming, load reset, removed
  switches, pair failure isolation, and 30-second notification aggregation.
- Validate and reuse the captured anchor switch before changing native groups,
  avoiding a second lookup with potentially different registry and device IDs.
- Request native route evaluation after every attempted mutation, including
  failed rollback after partial changes.
- Isolate cable-chain failures with known endpoints to the affected remote
  device. Unknown endpoints still stop that switch's processing safely.
- Preserve and skip provably unrelated malformed groups; unknown membership
  and overlapping cable ownership still prevent mutation.
- Added fault-path tests for native apply, rollback, connection decoding, and
  notification delivery using controlled game doubles.
- Route player messages through native `AddMeesageInField` so the game keeps
  message entries and expiry times synchronized.
- Added `ShowNotifications` (default `true`) to disable automatic in-game
  messages while preserving logs, processing, and user-requested explanations.
- Kept cable-batch processing immediate while grouping runtime player feedback
  into one 30-second activity summary.
- Added unique switch-capable device and AutoLACP evaluation counts to the
  grouped summary without adding a native route-evaluation hook.
- Retained detailed per-batch logging for diagnosis.

## 1.0.0

- Added automatic Steam Workshop updates after the first manual installation,
  enabled by default through `AutoUpdateFromWorkshop`, with assembly identity,
  version, timestamp, and SHA-256 verification.
- Added assembly identity, version, timestamp, and SHA-256 validation plus a
  post-exit helper that verifies the installed DLL.
- Promoted the tested automatic native LACP pipeline to the first public
  release.

- Enabled automatic native LACP creation and strict membership extension.
- Removed the dry-run branch, probe labels, pair-level work deduplication, and
  obsolete topology-snapshot planner.
- Processed every affected switch through the proven native
  `GetConnectedDevices()` pipeline.
- Resolved native patch-panel endpoints to their final devices and included
  complete native cable chains without counting chain segments as extra routes.
- Limited replacement to a strict membership superset; non-additive native
  state is left unchanged and logged.
- Added lazy VLAN comparison so native port indices are requested only when a
  real VLAN difference needs correction.
- Declared custom VLAN restrictions outside the tested and officially supported
  scope of version 1.0.0.
- Added concise one-line player results while retaining extended alpha logs.
- Grouped partial-load errors behind the normal load result and added explicit
  empty-network and missing-registration-event messages.
- Replaced the ineffective transient GUI title with muted native `[Auto]`
  information buttons while preserving the original border colors.
- Anchored native LACP-button discovery to the central switch port panel and
  stopped treating a valid one-sided `Base_*` registration as a load error.
- Updated tests and documentation to cover only the active production path.

## 0.2.0-alpha.1

- Internal mutation-enabled test build used for single-player acceptance.

## 0.1.6-connected-devices-probe

- Disabled all automatic LACP mutation and manual GUI interception for a
  read-only runtime probe.
- Sampled native `NetworkSwitch.GetConnectedDevices()` results after load.
- Kept each tuple native through `ToString()` and logged decoded device IDs and
  port indices without changing LACP state. The generated proxy exposes no
  native `Deconstruct` method.

## 0.1.5-test

- Held all post-load pair work until the initial cable registrations and their
  bounded resolution retries had drained.
- Enabled the single-link fast path only on the resulting stable load batch.
- Prevented later runtime cable events from inheriting the load-only fast path.

## 0.1.4-test

- Filtered load-time pairs with one registration and no native LACP group
  before the expensive switch topology capture.
- Limited an unresolved native port to its own triggering pair instead of
  marking every pair on the switch incomplete.
- Aggregated unchanged pairs into the final summary instead of logging each
  exact native group separately.

## 0.1.3-test

- Reused one native port and patch-panel capture for every device pair on the
  same switch instead of rebuilding it per pair.
- Processed lightweight pair decisions within a bounded per-frame time budget.
- Added an immediate post-load progress notification and suppressed no-op log
  lines for ordinary single-link pairs.

## 0.1.2-test

- Stopped reading cable endpoint strings from the broken IL2CPP
  `ValueTuple<string, string>` wrapper.
- Captured the native registration inputs per cable and derived endpoint names
  through the same native `GenerateDeviceName` path used by the game.
- Removed the redundant `GetConnectedDevices()` tuple read and filtered native
  switch ports through the captured cable registrations instead.
- Retained native cable-registry and completeness checks before every plan and
  apply.

## 0.1.1-diagnostic

- Added named cable-resolution stages and one complete diagnostic stack trace.
- Aggregated repeated resolver failures with at most five endpoint examples.
- Silently summarized transient load registrations that disappeared before
  evaluation.
- Added a diagnostic safety gate that prevents LACP mutation when any resolver
  stage throws within the batch.

## 0.1.0 - Development

- Added native cable-registration batching with direction-independent device
  pair deduplication.
- Added a one-second trailing debounce and a five-second hard batch limit for
  runtime cable bursts.
- Reused the same event pipeline for load reconstruction, with an immediate
  post-load flush.
- Added targeted native endpoint and patch-panel resolution without recurring
  full-network scans.
- Added exact native LACP create, replace, verification, rollback, and one
  bounded retry.
- Left physical cable removal completely under native game control.
- Added full overrule of manual LACP configuration and automatic GUI status.
- Added host-authoritative native co-op synchronization.
- Added deterministic pure planning and canonical pair tests.
