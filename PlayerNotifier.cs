using Il2Cpp;

namespace AutoLACP;

internal static class PlayerNotifier
{
    internal static void Show(string message)
    {
        try
        {
            var ui = StaticUIElements.instance;
            if (ui is null || ui.messageQueue is null)
                return;

            ui.messageQueue.Enqueue($"<color=#00FFFF>[AutoLACP]</color> {message}");
            ui.UpdateMessageDisplay();
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                "Could not show the in-game AutoLACP message. " +
                $"{exception.GetType().Name}: {exception.Message}");
        }
    }
}
