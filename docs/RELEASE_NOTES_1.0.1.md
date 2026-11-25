# AutoLACP 1.0.1

This update fixes AutoLACP's direct message-queue writes, which could leave the
game's message and expiry lists out of sync and interrupt route evaluation.
Messages now go through the game's native message method.

- Disable automatic in-game notifications with `ShowNotifications = false` in
  `UserData/AutoLACP.cfg`. Logging and automatic LACP management remain active.
- Runtime notifications are grouped into 30-second summaries with unique device
  and AutoLACP evaluation counts. Cable processing still uses the existing
  one-second debounce with a five-second maximum.
- Validate the anchor switch before mutation and request native route evaluation
  even after a failed rollback.
- Isolate safely identifiable connection and group failures so unrelated work
  can continue. Unknown or conflicting state remains unchanged and is logged.
- Check the soft processing budget between device pairs. Individual native calls
  and apply/rollback transactions remain indivisible.

Validation: 34 automated tests passed, Release build completed without warnings
or errors, and the installed 1.0.1 DLL initialized successfully in-game. The user
accepted the build for publication; long-session route evaluation is not proven
by the startup log alone.

Custom VLAN restrictions and multiplayer/co-op remain untested and unsupported.
GitHub users update manually; subscribed Workshop users with automatic updates
enabled receive the update after closing the game and restarting.
