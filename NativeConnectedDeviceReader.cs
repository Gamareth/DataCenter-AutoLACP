using System.Globalization;
using Il2Cpp;

namespace AutoLACP;

internal sealed class ConnectedDeviceCables
{
    internal string DeviceId { get; }
    internal int RouteCount { get; }
    internal IReadOnlyList<int> CableIds { get; }

    internal ConnectedDeviceCables(
        string deviceId,
        int routeCount,
        IReadOnlyList<int> cableIds)
    {
        DeviceId = deviceId;
        RouteCount = routeCount;
        CableIds = cableIds;
    }
}

internal sealed class ConnectedDevicesCapture
{
    internal string CandidateDeviceId { get; }
    internal string SwitchId { get; }
    internal string DeviceId { get; }
    internal int NativeRecordCount { get; }
    internal int DecodedRecordCount { get; }
    internal int PatchPanelRecordCount { get; }
    internal int DecodeFailureCount { get; }
    internal IReadOnlyList<string> RawSamples { get; }
    internal IReadOnlyList<ConnectedDeviceCables> Connections { get; }

    internal ConnectedDevicesCapture(
        string candidateDeviceId,
        string switchId,
        string deviceId,
        int nativeRecordCount,
        int decodedRecordCount,
        int patchPanelRecordCount,
        int decodeFailureCount,
        IReadOnlyList<string> rawSamples,
        IReadOnlyList<ConnectedDeviceCables> connections)
    {
        CandidateDeviceId = candidateDeviceId;
        SwitchId = switchId;
        DeviceId = deviceId;
        NativeRecordCount = nativeRecordCount;
        DecodedRecordCount = decodedRecordCount;
        PatchPanelRecordCount = patchPanelRecordCount;
        DecodeFailureCount = decodeFailureCount;
        RawSamples = rawSamples;
        Connections = connections;
    }
}

internal sealed class NativeConnectedDeviceReader
{
    private const int MaximumConnectionsPerSwitch = 256;
    private const int MaximumRawSamples = 3;

    internal ConnectedDevicesCapture Capture(
        NetworkSwitch networkSwitch,
        string candidateDeviceId)
    {
        if (networkSwitch is null)
            throw new ArgumentNullException(nameof(networkSwitch));

        var nativeConnections = networkSwitch.GetConnectedDevices();
        if (nativeConnections is null)
            throw new InvalidOperationException(
                "GetConnectedDevices returned a null list.");

        var nativeRecordCount = nativeConnections.Count;
        var recordsToRead = Math.Min(
            nativeRecordCount,
            MaximumConnectionsPerSwitch);
        var cableIdsByDevice = new Dictionary<string, HashSet<int>>(
            StringComparer.Ordinal);
        var switchCableIdsByDevice = new Dictionary<string, HashSet<int>>(
            StringComparer.Ordinal);
        var patchPanelSwitchCableIds = new HashSet<int>();
        var rawSamples = new List<string>();
        var decoded = 0;
        var patchPanelRecords = 0;
        var failures = 0;
        var map = NetworkMap.instance;
        if (map is null)
            throw new InvalidOperationException("NetworkMap is unavailable.");
        var localDeviceId = GetLocalDeviceId(networkSwitch, candidateDeviceId);

        for (var index = 0; index < recordsToRead; index++)
        {
            string rendered = null;
            try
            {
                rendered = nativeConnections[index]?.ToString();
                if (rawSamples.Count < MaximumRawSamples)
                    rawSamples.Add(rendered ?? "<null>");
                if (!TryParseTuple(rendered, out var deviceId, out var cableId))
                    throw new InvalidOperationException(
                        $"Native tuple text could not be decoded: '{rendered}'.");

                if (map.IsPatchPanelPort(deviceId))
                {
                    patchPanelRecords++;
                    var remoteDeviceId = map.ResolveThroughPatchPanel(
                        deviceId,
                        localDeviceId);
                    if (string.IsNullOrWhiteSpace(remoteDeviceId)
                        || map.IsPatchPanelPort(remoteDeviceId))
                    {
                        throw new InvalidOperationException(
                            $"Patch-panel endpoint '{Describe(deviceId)}' " +
                            "could not be resolved to a final device.");
                    }

                    AddCableIds(
                        cableIdsByDevice,
                        remoteDeviceId,
                        new[] { cableId });
                    AddCableIds(
                        switchCableIdsByDevice,
                        remoteDeviceId,
                        new[] { cableId });
                    patchPanelSwitchCableIds.Add(cableId);
                }
                else
                {
                    AddCableIds(cableIdsByDevice, deviceId, new[] { cableId });
                    AddCableIds(
                        switchCableIdsByDevice,
                        deviceId,
                        new[] { cableId });
                }
                decoded++;
            }
            catch (Exception exception)
            {
                failures++;
                AutoLacpLog.Warning(
                    $"GetConnectedDevices tuple {index} could not be read for " +
                    $"switch='{Describe(networkSwitch.switchId)}'; raw=" +
                    $"'{Describe(rendered)}'. {exception.GetType().Name}: " +
                    exception.Message);
            }
        }

        if (nativeRecordCount > MaximumConnectionsPerSwitch)
        {
            failures += nativeRecordCount - MaximumConnectionsPerSwitch;
            AutoLacpLog.Warning(
                $"GetConnectedDevices returned {nativeRecordCount} records for " +
                $"switch='{Describe(networkSwitch.switchId)}'; only the first " +
                $"{MaximumConnectionsPerSwitch} were inspected.");
        }

        foreach (var entry in switchCableIdsByDevice)
        {
            if (entry.Value.Count < 2)
                continue;

            foreach (var switchCableId in entry.Value)
            {
                if (!patchPanelSwitchCableIds.Contains(switchCableId))
                    continue;

                try
                {
                    AddCableIds(
                        cableIdsByDevice,
                        entry.Key,
                        CollectPatchPanelCableIds(
                            networkSwitch,
                            switchCableId));
                }
                catch (Exception exception)
                {
                    failures++;
                    AutoLacpLog.Warning(
                        $"Patch-panel cable chain could not be read for " +
                        $"switch='{Describe(networkSwitch.switchId)}', " +
                        $"cable={switchCableId}. " +
                        $"{exception.GetType().Name}: {exception.Message}");
                }
            }
        }

        var connections = cableIdsByDevice
            .OrderBy(entry => entry.Key, StringComparer.Ordinal)
            .Select(entry => new ConnectedDeviceCables(
                entry.Key,
                switchCableIdsByDevice[entry.Key].Count,
                entry.Value.OrderBy(cableId => cableId).ToArray()))
            .ToArray();
        return new ConnectedDevicesCapture(
            candidateDeviceId,
            networkSwitch.switchId,
            networkSwitch.GetDeviceId(),
            nativeRecordCount,
            decoded,
            patchPanelRecords,
            failures,
            rawSamples,
            connections);
    }

    private static string GetLocalDeviceId(
        NetworkSwitch networkSwitch,
        string candidateDeviceId)
    {
        var deviceId = networkSwitch.GetDeviceId();
        if (!string.IsNullOrWhiteSpace(deviceId))
            return deviceId;
        if (!string.IsNullOrWhiteSpace(networkSwitch.switchId))
            return networkSwitch.switchId;
        if (!string.IsNullOrWhiteSpace(candidateDeviceId))
            return candidateDeviceId;
        throw new InvalidOperationException(
            "The switch has no device ID for patch-panel traversal.");
    }

    private static IReadOnlyList<int> CollectPatchPanelCableIds(
        NetworkSwitch networkSwitch,
        int switchCableId)
    {
        var portIndex = networkSwitch.GetPortIndexForCable(switchCableId);
        var switchPorts = networkSwitch.cableLinkSwitchPorts;
        if (portIndex < 0
            || switchPorts is null
            || portIndex >= switchPorts.Length
            || switchPorts[portIndex] is null)
        {
            throw new InvalidOperationException(
                $"Switch port for patch-panel cable {switchCableId} " +
                "could not be resolved.");
        }

        var nativeCableIds = switchPorts[portIndex]
            .CollectPatchPanelChainCables(switchCableId);
        if (nativeCableIds is null || nativeCableIds.Count == 0)
        {
            throw new InvalidOperationException(
                $"Native patch-panel traversal returned no cables for " +
                $"switch cable {switchCableId}.");
        }

        var cableIds = new HashSet<int>();
        foreach (var cableId in nativeCableIds)
        {
            if (cableId < 0)
            {
                throw new InvalidOperationException(
                    "Native patch-panel traversal returned an invalid cable ID.");
            }
            cableIds.Add(cableId);
        }
        if (!cableIds.Contains(switchCableId))
        {
            throw new InvalidOperationException(
                $"Native patch-panel traversal omitted switch cable " +
                $"{switchCableId}.");
        }
        return cableIds.ToArray();
    }

    private static void AddCableIds(
        IDictionary<string, HashSet<int>> cableIdsByDevice,
        string deviceId,
        IEnumerable<int> cableIds)
    {
        if (!cableIdsByDevice.TryGetValue(deviceId, out var existingCableIds))
        {
            existingCableIds = new HashSet<int>();
            cableIdsByDevice.Add(deviceId, existingCableIds);
        }

        foreach (var cableId in cableIds)
            existingCableIds.Add(cableId);
    }

    private static bool TryParseTuple(
        string rendered,
        out string deviceId,
        out int cableId)
    {
        deviceId = null;
        cableId = -1;
        if (string.IsNullOrEmpty(rendered)
            || rendered[0] != '('
            || rendered[^1] != ')')
        {
            return false;
        }

        var separatorIndex = rendered.LastIndexOf(", ", StringComparison.Ordinal);
        if (separatorIndex <= 1
            || !int.TryParse(
                rendered.AsSpan(
                    separatorIndex + 2,
                    rendered.Length - separatorIndex - 3),
                NumberStyles.Integer,
                CultureInfo.InvariantCulture,
                out cableId)
            || cableId < 0)
        {
            return false;
        }

        deviceId = rendered.Substring(1, separatorIndex - 1);
        return !string.IsNullOrWhiteSpace(deviceId);
    }

    internal static string Describe(string value)
    {
        if (value is null)
            return "<null>";
        var singleLine = value.Replace("\r", "\\r").Replace("\n", "\\n");
        return singleLine.Length <= 120
            ? singleLine
            : singleLine.Substring(0, 120) + "...";
    }
}
