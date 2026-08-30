using MelonLoader;

namespace AutoLACP;

internal static class AutoLacpLog
{
    internal static void Info(string message) =>
        Melon<Mod>.Logger.Msg(message);

    internal static void Warning(string message) =>
        Melon<Mod>.Logger.Warning(message);

    internal static void Error(string message) =>
        Melon<Mod>.Logger.Error(message);

    internal static string DescribeCableIds(IEnumerable<int> cableIds) =>
        $"[{string.Join(", ", cableIds.OrderBy(id => id))}]";
}
