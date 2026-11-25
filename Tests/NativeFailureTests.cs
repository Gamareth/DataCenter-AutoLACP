using AutoLACP;
using Il2Cpp;

internal static class NativeFailureTests
{
    internal static IEnumerable<(string Name, Action Run)> Cases => new (string, Action)[]
    {
        ("missing anchor cannot remove an existing group", MissingAnchor),
        ("mismatched anchor cannot mutate", WrongAnchor),
        ("logical device and registry IDs may differ", DifferentIds),
        ("partial create and failed rollback still request evaluation", FailedRollback),
        ("successful rollback requests evaluation once", SuccessfulRollback),
        ("unrelated malformed group is preserved", UnrelatedMalformed),
        ("overlapping malformed group prevents mutation", OverlappingMalformed),
        ("unknown registry membership fails closed", UnknownMembership),
        ("another pair cannot claim desired cables", CrossPairConflict),
        ("known chain failure excludes only its remote device", KnownChainFailure),
        ("unknown tuple failure remains unsafe", UnknownTuple),
        ("notifications use native delivery and respect preferences", Notifications),
        ("pair capture failure does not skip other pairs on the switch", PairFailureIsolation),
        ("frame budget yields between pairs and resumes remaining work", PairBudget),
        ("load reset discards partially processed switch", ResetPartialSwitch),
        ("switch removed between frames is not mutated", RemovedSwitch),
        ("runtime notifications remain aggregated for thirty seconds", NotificationWindow)
    };

    private static readonly CanonicalEndpointPair Pair = new("Switch_A", "Server_B");
    private static void Reset()
    {
        AutoLacpCoordinator.Shutdown();
        UnityEngine.Time.realtimeSinceStartup = 0;
        StaticUIElements.instance = new();
        ModSettings.ShowNotifications = true;
        NetworkMap.instance = new();
        CablePositions.instance = new();
        WaypointInitializationSystem.Instance = new();
        for (var id = 0; id < 30; id++)
        {
            NetworkMap.instance.cableConnections[id] = new();
            CablePositions.instance.Complete.Add(id);
        }
    }
    private static LacpDecision Create() => LacpPlanner.BuildConnectedDeviceDecision(Pair, "Switch_A", new[] { 10, 11 }, Array.Empty<NativeGroupSnapshot>());
    private static NativeLacpApplier Applier() => new(new NativeLacpState());
    private static NativeGroup Group(string a, string b, params int[] ids)
    {
        var cables = new Il2CppSystem.Collections.Generic.List<int>();
        cables.AddRange(ids);
        return new() { groupId = 5, deviceA = a, deviceB = b, cableIds = cables };
    }
    private static void Require(bool condition)
    {
        if (!condition) throw new InvalidOperationException("Failure-path expectation was not met.");
    }
    private static void MissingAnchor()
    {
        Reset();
        NetworkMap.instance.Groups[5] = Group("Switch_A", "Server_B", 10, 11);
        var decision = LacpPlanner.BuildConnectedDeviceDecision(Pair, "Switch_A", new[] { 10, 11, 12 },
            new[] { new NativeGroupSnapshot(5, Pair, new[] { 10, 11 }) });
        Require(!Applier().Apply(decision, null));
        Require(NetworkMap.instance.RemoveCalls == 0 && NetworkMap.instance.CreateCalls == 0);
        Require(WaypointInitializationSystem.Instance.Requests == 0);
    }
    private static void WrongAnchor()
    {
        Reset();
        Require(!Applier().Apply(Create(), new() { DeviceId = "Wrong", switchId = "Wrong" }));
        Require(NetworkMap.instance.CreateCalls == 0);
    }
    private static void DifferentIds()
    {
        Reset();
        Require(Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.CreateCalls == 1 && WaypointInitializationSystem.Instance.Requests == 1);
    }
    private static void FailedRollback()
    {
        Reset();
        NetworkMap.instance.FailCreateAfterMutation = true;
        NetworkMap.instance.FailRemove = true;
        Require(!Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.Groups.Count == 1);
        Require(WaypointInitializationSystem.Instance.Requests == 1);
    }
    private static void SuccessfulRollback()
    {
        Reset();
        NetworkMap.instance.FailCreateAfterMutation = true;
        Require(!Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.Groups.Count == 0 && WaypointInitializationSystem.Instance.Requests == 1);
    }
    private static void UnrelatedMalformed()
    {
        Reset();
        var corrupt = Group(null, "Other", 20, 21);
        NetworkMap.instance.Groups[5] = corrupt;
        Require(Applier().Apply(Create(), new NetworkSwitch()));
        Require(ReferenceEquals(corrupt, NetworkMap.instance.Groups[5]));
    }
    private static void OverlappingMalformed()
    {
        Reset();
        NetworkMap.instance.Groups[5] = Group(null, "Other", 10, 21);
        Require(!Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.CreateCalls == 0);
    }
    private static void UnknownMembership()
    {
        Reset();
        NetworkMap.instance.Groups[5] = null;
        Require(!Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.CreateCalls == 0);
    }
    private static void CrossPairConflict()
    {
        Reset();
        NetworkMap.instance.Groups[5] = Group("Other_A", "Other_B", 10, 21);
        Require(!Applier().Apply(Create(), new NetworkSwitch()));
        Require(NetworkMap.instance.CreateCalls == 0);
    }
    private static void KnownChainFailure()
    {
        Reset();
        NetworkMap.instance.PatchEndpoints["Patch_1"] = "Server_B";
        var sw = new NetworkSwitch { Connections = new() { "(Patch_1, 0)", "(Server_B, 1)", "(Server_C, 2)", "(Server_C, 3)" },
            cableLinkSwitchPorts = new[] { new CablePort() } };
        var result = new NativeConnectedDeviceReader().Capture(sw, "Registry_A");
        Require(result.DecodeFailureCount == 1 && !result.HasUnidentifiedFailure);
        Require(result.Connections.Count == 1 && result.Connections[0].DeviceId == "Server_C");
        Require(result.Connections[0].RouteCount == 2);
    }
    private static void UnknownTuple()
    {
        Reset();
        var result = new NativeConnectedDeviceReader().Capture(new() { Connections = new() { "invalid", "(Server_B, 1)" } }, "Registry_A");
        Require(result.HasUnidentifiedFailure && result.DecodeFailureCount == 1);
    }
    private static void Notifications()
    {
        StaticUIElements.instance = new();
        ModSettings.ShowNotifications = false;
        PlayerNotifier.Show("automatic");
        Require(StaticUIElements.instance.Messages.Count == 0);
        PlayerNotifier.Show("manual", userRequested: true);
        Require(StaticUIElements.instance.Messages.Count == 1);
        ModSettings.ShowNotifications = true;
        PlayerNotifier.Show("automatic");
        Require(StaticUIElements.instance.Messages.Count == 2);
        StaticUIElements.instance = null;
        PlayerNotifier.Show("no UI");
    }

    private static void StartTwoPairs()
    {
        Reset();
        NetworkMap.instance.Switches["Registry_A"] = new NetworkSwitch
        {
            Connections = new() { "(Server_B, 10)", "(Server_B, 11)", "(Server_C, 12)", "(Server_C, 13)" }
        };
        AutoLacpCoordinator.Initialize();
        AutoLacpCoordinator.OnCableRegistered(10, "Registry_A", null, null, "Server_B");
        UnityEngine.Time.realtimeSinceStartup = 2;
        AutoLacpCoordinator.Update();
    }

    private static void Drain()
    {
        for (var i = 0; i < 12; i++)
            AutoLacpCoordinator.Update();
    }

    private static void PairFailureIsolation()
    {
        StartTwoPairs();
        // A mismatched dictionary key makes only the first pair's capture fail.
        NetworkMap.instance.Groups[6] = Group("Switch_A", "Server_B", 10, 11);
        Drain();
        Require(NetworkMap.instance.Groups.Values.Any(g => g.deviceB == "Server_C"));
        Require(NetworkMap.instance.CreateCalls == 1);
    }

    private static void RunFirstSlowPair()
    {
        StartTwoPairs();
        // Simulate a single indivisible native call exceeding the soft budget.
        NetworkMap.instance.AfterCreate = () => Thread.Sleep(20);
        for (var i = 0; i < 12 && NetworkMap.instance.CreateCalls == 0; i++)
            AutoLacpCoordinator.Update();
        Require(NetworkMap.instance.CreateCalls == 1);
        NetworkMap.instance.AfterCreate = null;
    }

    private static void PairBudget()
    {
        RunFirstSlowPair();
        Drain();
        Require(NetworkMap.instance.CreateCalls == 2);
        Require(WaypointInitializationSystem.Instance.Requests == 2);
    }

    private static void ResetPartialSwitch()
    {
        RunFirstSlowPair();
        AutoLacpCoordinator.OnNetworkLoadStarted();
        Drain();
        Require(NetworkMap.instance.CreateCalls == 1);
    }

    private static void RemovedSwitch()
    {
        RunFirstSlowPair();
        NetworkMap.instance.Switches.Clear();
        Drain();
        Require(NetworkMap.instance.CreateCalls == 1);
    }

    private static void NotificationWindow()
    {
        StartTwoPairs();
        Drain();
        Require(StaticUIElements.instance.Messages.Count == 0);
        UnityEngine.Time.realtimeSinceStartup = 10;
        AutoLacpCoordinator.OnCableRegistered(11, "Registry_A", null, null, "Server_B");
        UnityEngine.Time.realtimeSinceStartup = 12;
        Drain();
        UnityEngine.Time.realtimeSinceStartup = 31;
        AutoLacpCoordinator.Update();
        Require(StaticUIElements.instance.Messages.Count == 0);
        UnityEngine.Time.realtimeSinceStartup = 32;
        AutoLacpCoordinator.Update();
        Require(StaticUIElements.instance.Messages.Count == 1);
        var message = StaticUIElements.instance.Messages[0];
        Require(message.Contains("1 switch-capable device") && message.Contains("2 evaluations"));
        Require(message.Contains("2 LACP groups added"));
        Drain();
        Require(StaticUIElements.instance.Messages.Count == 1);
    }
}
