// Minimal controlled game boundaries for exercising the production code's
// failure paths. These doubles do not establish IL2CPP or in-game compatibility.
namespace Il2CppSystem.Collections.Generic
{
    public class List<T> : System.Collections.Generic.List<T> { }
}

namespace AutoLACP
{
    internal static class NativeGameState
    {
        internal static bool MayMutate() => true;
        internal static bool MayCollectCableEvent() => true;
    }
    internal static class AutoLacpGui
    {
        internal static void Reset() { }
    }
    internal static class AutoLacpLog
    {
        internal static void Info(string message) { }
        internal static void Warning(string message) { }
        internal static void Error(string message) { }
        internal static string DescribeCableIds(IReadOnlyList<int> ids) => string.Join(",", ids);
    }
    internal static class ModSettings
    {
        internal static bool ShowNotifications = true;
    }
}

namespace UnityEngine
{
    internal static class Time
    {
        internal static float realtimeSinceStartup;
    }
}

namespace Il2Cpp
{
    internal sealed class NativeGroup
    {
        internal int groupId;
        internal string deviceA;
        internal string deviceB;
        internal Il2CppSystem.Collections.Generic.List<int> cableIds;
    }
    internal sealed class NetworkMap
    {
        internal static NetworkMap instance;
        internal Dictionary<int, object> cableConnections = new();
        internal Dictionary<int, NativeGroup> Groups = new();
        internal int CreateCalls;
        internal int RemoveCalls;
        internal bool FailCreateAfterMutation;
        internal bool FailRemove;
        internal Action AfterCreate;
        internal Dictionary<string, NetworkSwitch> Switches = new();
        internal NetworkSwitch GetSwitchById(string id) => Switches.GetValueOrDefault(id);
        internal Dictionary<string, string> PatchEndpoints = new();
        internal Dictionary<int, NativeGroup> GetAllLACPGroups() => Groups;
        internal bool IsPatchPanelPort(string id) => id.StartsWith("Patch_");
        internal string ResolveThroughPatchPanel(string id, string local) => PatchEndpoints.GetValueOrDefault(id);
        internal void RemoveLACPGroup(int id)
        {
            RemoveCalls++;
            if (FailRemove) throw new InvalidOperationException("Injected remove failure");
            Groups.Remove(id);
        }
        internal int CreateLACPGroup(string a, string b, Il2CppSystem.Collections.Generic.List<int> cables, int requested)
        {
            CreateCalls++;
            var id = requested < 0 ? 100 + CreateCalls : requested;
            Groups[id] = new NativeGroup { groupId = id, deviceA = a, deviceB = b, cableIds = cables };
            AfterCreate?.Invoke();
            if (FailCreateAfterMutation) throw new InvalidOperationException("Injected partial create failure");
            return id;
        }
    }
    internal sealed class CablePositions
    {
        internal static CablePositions instance;
        internal HashSet<int> Complete = new();
        internal bool IsCableComplete(int id) => Complete.Contains(id);
    }
    internal sealed class WaypointInitializationSystem
    {
        internal static WaypointInitializationSystem Instance;
        internal int Requests;
        internal void RequestRouteEvaluation() => Requests++;
    }
    internal static class CoopWorldSync
    {
        internal static bool CoopActive => false;
        internal static bool IsHost => false;
        internal static void SyncLACPRemoved(string a, string b) { }
        internal static void BroadcastLACPGroupSet(int id, string a, string b, Il2CppSystem.Collections.Generic.List<int> cables) { }
        internal static void SyncDeviceConfig(NetworkSwitch value) { }
    }
    internal sealed class CablePort
    {
        internal Il2CppSystem.Collections.Generic.List<int> Chain { get; set; }
        internal Il2CppSystem.Collections.Generic.List<int> CollectPatchPanelChainCables(int id) => Chain;
    }
    internal sealed class NetworkSwitch
    {
        internal string switchId = "Registry_A";
        internal string DeviceId = "Switch_A";
        internal List<object> Connections = new();
        internal CablePort[] cableLinkSwitchPorts = Array.Empty<CablePort>();
        internal string GetDeviceId() => DeviceId;
        internal List<object> GetConnectedDevices() => Connections;
        internal int GetPortIndexForCable(int id) => id;
        internal List<int> GetVisibleVlans() => new();
        internal bool IsVlanAllowedOnCable(int id, int vlan) => true;
        internal HashSet<int> GetDisallowedVlans(int port) => new();
        internal void SetVlanDisallowed(int port, int vlan) { }
        internal void SetVlanAllowed(int port, int vlan) { }
    }
    internal sealed class StaticUIElements
    {
        internal static StaticUIElements instance;
        internal List<string> Messages = new();
        internal void AddMeesageInField(string message) => Messages.Add(message);
    }
}
