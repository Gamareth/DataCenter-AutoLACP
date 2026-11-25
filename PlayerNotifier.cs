using Il2Cpp;

namespace AutoLACP;

internal static class PlayerNotifier
{
    internal static void Show(string message, bool userRequested = false)
    {
        if (!userRequested && !ModSettings.ShowNotifications)
            return;

        try
        {
            var ui = StaticUIElements.instance;
            if (ui is null)
                return;

            // The native method owns both message storage and expiry bookkeeping.
            ui.AddMeesageInField($"<color=#00FFFF>[AutoLACP]</color> {message}");
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                "Could not show the in-game AutoLACP message. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
