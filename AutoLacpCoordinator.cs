using System.Diagnostics;
using Il2Cpp;
using UnityEngine;

namespace AutoLACP;

internal static class AutoLacpCoordinator
{
    private const float QuietPeriodSeconds = 1f;
    private const float MaximumBatchSeconds = 5f;
    private const float RuntimeNotificationWindowSeconds = 30f;
    private const int MaximumWorkItemsPerFrame = 128;
    private const double ProcessingBudgetMilliseconds = 6d;
    private const int MaximumDiagnosticSwitchSamples = 3;

    private sealed class SwitchRun
    {
        internal Queue<string> CandidateDeviceIds { get; }
        internal HashSet<string> ProcessedSwitchIds { get; } = new(
            StringComparer.Ordinal);
        internal string Trigger { get; }
        internal int CableCount { get; }
        internal int UnresolvedCableRegistrations { get; }
        internal int CandidateCount { get; }
        internal int NonSwitchable { get; set; }
        internal int SwitchesProcessed { get; set; }
        internal int ConnectedRecords { get; set; }
        internal int DecodeFailures { get; set; }
        internal int ParallelConnectionObservations { get; set; }
        internal int CreatedGroups { get; set; }
        internal int ExtendedGroups { get; set; }
        internal int Unchanged { get; set; }
        internal int Skipped { get; set; }
        internal int ProcessingFailures { get; set; }
        internal int PlannerSkips { get; set; }
        internal int ApplyFailures { get; set; }
        internal int DiagnosticSwitchSamples { get; set; }
        internal ConnectedDevicesCapture CurrentCapture { get; set; }
        internal int NextConnectionIndex { get; set; }

        internal SwitchRun(
            IEnumerable<string> candidateDeviceIds,
            string trigger,
            int cableCount,
            int unresolvedCableRegistrations)
        {
            var candidates = candidateDeviceIds.ToArray();
            CandidateDeviceIds = new Queue<string>(candidates);
            CandidateCount = candidates.Length;
            Trigger = trigger;
            CableCount = cableCount;
            UnresolvedCableRegistrations = unresolvedCableRegistrations;
        }
    }

    private sealed class RuntimeNotificationWindow
    {
        internal HashSet<string> SwitchIds { get; } = new(
            StringComparer.Ordinal);
        internal bool IsScheduled { get; set; }
        internal float DueTime { get; set; }
        internal int CableConnections { get; set; }
        internal int Evaluations { get; set; }
        internal int CreatedGroups { get; set; }
        internal int ExtendedGroups { get; set; }
        internal int Errors { get; set; }

        internal void Reset()
        {
            SwitchIds.Clear();
            IsScheduled = false;
            DueTime = 0f;
            CableConnections = 0;
            Evaluations = 0;
            CreatedGroups = 0;
            ExtendedGroups = 0;
            Errors = 0;
        }
    }

    private static readonly HashSet<int> PendingCableIds = new();
    private static readonly HashSet<string> PendingDeviceIds = new(
        StringComparer.Ordinal);
    private static readonly HashSet<int> UnresolvedCableRegistrations = new();
    private static readonly RuntimeNotificationWindow RuntimeNotifications = new();

    private static NativeConnectedDeviceReader _connectedDevices;
    private static NativeLacpState _nativeLacp;
    private static NativeLacpApplier _applier;
    private static SwitchRun _activeRun;
    private static bool _enabled;
    private static bool _isApplying;
    private static bool _loadBatchPending;
    private static bool _batchScheduled;
    private static float _batchFirstEventTime;
    private static float _batchDueTime;

    internal static bool IsAutomaticEnabled => _enabled;

    internal static void Initialize()
    {
        Shutdown();
        _connectedDevices = new NativeConnectedDeviceReader();
        _nativeLacp = new NativeLacpState();
        _applier = new NativeLacpApplier(_nativeLacp);
        _enabled = true;
        AutoLacpLog.Info(
            "Automatic native LACP management initialized.");
    }

    internal static void Shutdown()
    {
        ResetWork();
        AutoLacpGui.Reset();
        _connectedDevices = null;
        _nativeLacp = null;
        _applier = null;
        _enabled = false;
    }

    internal static void OnNetworkLoadStarted()
    {
        if (!_enabled)
            return;

        ResetWork();
        _loadBatchPending = true;
        AutoLacpLog.Info(
            "Network load started; collecting registered cable endpoints.");
    }

    internal static void OnCableRegistered(
        int cableId,
        string startSwitchId,
        string endSwitchId,
        string startServerId,
        string endServerId)
    {
        if (!_enabled
            || _isApplying
            || cableId < 0
            || !NativeGameState.MayCollectCableEvent())
        {
            return;
        }

        PendingCableIds.Add(cableId);
        var startResolved = AddCableEnd(startSwitchId, startServerId);
        var endResolved = AddCableEnd(endSwitchId, endServerId);
        if (!startResolved
            && !endResolved
            && UnresolvedCableRegistrations.Add(cableId))
        {
            AutoLacpLog.Warning(
                $"Cable {cableId} was registered without a switch or server " +
                "device ID at either endpoint.");
        }
        ScheduleTrailingBatch();
    }

    internal static void NotifyManualOverrule(string action)
    {
        AutoLacpLog.Info(
            $"Suppressed native manual LACP {action}; automatic management " +
            "remains active.");
        PlayerNotifier.Show(
            "LACP is managed automatically. The full network is checked when " +
            "the game is loaded, and affected devices are rechecked whenever " +
            "one or more new cable connections are made.",
            userRequested: true);
    }

    internal static void Update()
    {
        if (!_enabled || _connectedDevices is null || _nativeLacp is null)
            return;

        if (!NativeGameState.MayMutate())
            return;

        if (_activeRun is null
            && RuntimeNotifications.IsScheduled
            && Time.realtimeSinceStartup >= RuntimeNotifications.DueTime)
        {
            FlushRuntimeNotification();
        }

        if (_activeRun is not null)
        {
            ProcessSwitchBudget();
            return;
        }

        if (_batchScheduled
            && Time.realtimeSinceStartup >= _batchDueTime)
        {
            StartPendingBatch();
            return;
        }

        if (_loadBatchPending && !_batchScheduled)
        {
            _loadBatchPending = false;
            ReportLoadWithoutCableEvents();
        }
    }

    private static bool AddCableEnd(
        string switchId,
        string serverId)
    {
        var deviceId = !string.IsNullOrWhiteSpace(switchId)
            ? switchId
            : serverId;
        if (!string.IsNullOrWhiteSpace(deviceId))
        {
            PendingDeviceIds.Add(deviceId);
            return true;
        }
        return false;
    }

    private static void ScheduleTrailingBatch()
    {
        var now = Time.realtimeSinceStartup;
        if (!_batchScheduled)
        {
            _batchScheduled = true;
            _batchFirstEventTime = now;
        }

        _batchDueTime = Math.Min(
            now + QuietPeriodSeconds,
            _batchFirstEventTime + MaximumBatchSeconds);
    }

    private static void StartPendingBatch()
    {
        _batchScheduled = false;
        var trigger = _loadBatchPending ? "post-load" : "cable-placement";
        var notifyPlayer = _loadBatchPending;
        _loadBatchPending = false;

        var cableIds = PendingCableIds.OrderBy(id => id).ToArray();
        var deviceIds = PendingDeviceIds
            .OrderBy(id => id, StringComparer.Ordinal)
            .ToArray();
        var unresolvedCableRegistrations = UnresolvedCableRegistrations.Count;
        PendingCableIds.Clear();
        PendingDeviceIds.Clear();
        UnresolvedCableRegistrations.Clear();
        _batchFirstEventTime = 0f;
        _batchDueTime = 0f;

        AutoLacpLog.Info(
            $"{trigger} batch: cables={cableIds.Length} " +
            $"sample={DescribeSample(cableIds.Select(id => id.ToString()))}, " +
            $"unique endpoint device IDs={deviceIds.Length} " +
            $"sample={DescribeSample(deviceIds.Select(Describe))}, " +
            $"fully unresolved cable registrations=" +
            $"{unresolvedCableRegistrations}.");

        if (deviceIds.Length == 0)
        {
            AutoLacpLog.Warning(
                $"{trigger} batch contains no resolvable endpoint IDs.");
            if (notifyPlayer)
            {
                PlayerNotifier.Show(
                    "Loaded network could not be processed: cable connections " +
                    "were reported without connected device IDs; no LACP " +
                    "changes applied. See log.");
            }
            else
            {
                AccumulateRuntimeNotification(
                    cableIds.Length,
                    Array.Empty<string>(),
                    createdGroups: 0,
                    extendedGroups: 0,
                    errors: Math.Max(1, unresolvedCableRegistrations));
            }
            return;
        }

        _activeRun = new SwitchRun(
            deviceIds,
            trigger,
            cableIds.Length,
            unresolvedCableRegistrations);
    }

    private static void ProcessSwitchBudget()
    {
        var stopwatch = Stopwatch.StartNew();
        for (var processed = 0;
             processed < MaximumWorkItemsPerFrame && _activeRun is not null;
             processed++)
        {
            if (_activeRun.CurrentCapture is not null)
                ProcessNextConnection(_activeRun);
            else
                ProcessNextSwitchCandidate();
            if (stopwatch.Elapsed.TotalMilliseconds >= ProcessingBudgetMilliseconds)
                break;
        }
    }

    private static void ProcessNextSwitchCandidate()
    {
        var run = _activeRun;
        if (run is null)
            return;
        if (run.CandidateDeviceIds.Count == 0)
        {
            FinishActiveRun();
            return;
        }

        var candidateDeviceId = run.CandidateDeviceIds.Dequeue();
        try
        {
            var networkSwitch = NetworkMap.instance?.GetSwitchById(candidateDeviceId);
            if (networkSwitch is null)
            {
                run.NonSwitchable++;
                return;
            }

            var nativeSwitchId = networkSwitch.switchId;
            var nativeDeviceId = networkSwitch.GetDeviceId();
            var switchKey = !string.IsNullOrWhiteSpace(nativeSwitchId)
                ? nativeSwitchId
                : !string.IsNullOrWhiteSpace(nativeDeviceId)
                    ? nativeDeviceId
                    : candidateDeviceId;
            if (!run.ProcessedSwitchIds.Add(switchKey))
                return;

            run.SwitchesProcessed++;
            var capture = _connectedDevices.Capture(
                networkSwitch,
                candidateDeviceId);
            run.ConnectedRecords += capture.DecodedRecordCount;
            run.DecodeFailures += capture.DecodeFailureCount;
            var hasParallelConnection = capture.Connections.Any(connection =>
                !IsSelf(capture, connection.DeviceId)
                && connection.RouteCount >= 2);
            if (hasParallelConnection
                || capture.DecodeFailureCount > 0
                || run.DiagnosticSwitchSamples < MaximumDiagnosticSwitchSamples)
            {
                LogConnectedDevices(capture);
                if (!hasParallelConnection && capture.DecodeFailureCount == 0)
                    run.DiagnosticSwitchSamples++;
            }

            if (capture.HasUnidentifiedFailure)
            {
                run.Skipped++;
                AutoLacpLog.Warning(
                    $"Switch='{Describe(switchKey)}' produced " +
                    $"{capture.DecodeFailureCount} undecodable record(s); " +
                    "planning for this switch is skipped because at least one " +
                    "failed connection could not be isolated to a remote device.");
                return;
            }

            // Retain only this switch's snapshot while yielding between pairs.
            run.CurrentCapture = capture;
            run.NextConnectionIndex = 0;
        }
        catch (Exception exception)
        {
            run.Skipped++;
            run.ProcessingFailures++;
            AutoLacpLog.Warning(
                $"Failed while processing endpoint=" +
                $"'{Describe(candidateDeviceId)}'. " +
                $"{exception.GetType().Name}: {exception.Message}\n{exception}");
        }
        finally
        {
            if (run.CandidateDeviceIds.Count == 0 && run.CurrentCapture is null)
                FinishActiveRun();
        }
    }

    private static void LogConnectedDevices(ConnectedDevicesCapture capture)
    {
        var groups = capture.Connections.Select(connection =>
        {
            var isSelf = IsSelf(capture, connection.DeviceId);
            var isParallel = !isSelf && connection.RouteCount >= 2;
            return $"'{Describe(connection.DeviceId)}'=>" +
                $"routes={connection.RouteCount} " +
                $"cables=[{string.Join(",", connection.CableIds)}] " +
                $"self={(isSelf ? "yes" : "no")} " +
                $"parallel={(isParallel ? "yes" : "no")}";
        });

        AutoLacpLog.Info(
            $"GetConnectedDevices candidate=" +
            $"'{Describe(capture.CandidateDeviceId)}' " +
            $"switchId='{Describe(capture.SwitchId)}' " +
            $"deviceId='{Describe(capture.DeviceId)}' " +
            $"nativeRecords={capture.NativeRecordCount} " +
            $"decoded={capture.DecodedRecordCount} " +
            $"patchPanelRecords={capture.PatchPanelRecordCount} " +
            $"failures={capture.DecodeFailureCount} " +
            $"rawSamples=[{string.Join("; ", capture.RawSamples.Select(Describe))}] " +
            $"connections={string.Join("; ", groups)}");
    }

    private static void ProcessNextConnection(SwitchRun run)
    {
        var capture = run.CurrentCapture;
        if (run.NextConnectionIndex >= capture.Connections.Count)
        {
            run.CurrentCapture = null;
            if (run.CandidateDeviceIds.Count == 0)
                FinishActiveRun();
            return;
        }

        var connection = capture.Connections[run.NextConnectionIndex++];
        if (IsSelf(capture, connection.DeviceId) || connection.RouteCount < 2)
            return;

        var localDeviceId = !string.IsNullOrWhiteSpace(capture.DeviceId)
            ? capture.DeviceId
            : !string.IsNullOrWhiteSpace(capture.SwitchId)
                ? capture.SwitchId
                : capture.CandidateDeviceId;

        try
        {
            run.ParallelConnectionObservations++;
            // A frame may have passed since capture. Resolve the original lookup
            // key again so a removed switch cannot be mutated through an old proxy.
            var anchorSwitch = NetworkMap.instance?.GetSwitchById(capture.CandidateDeviceId);
            if (anchorSwitch is null)
                throw new InvalidOperationException("The captured switch is no longer available.");
            var pair = new CanonicalEndpointPair(
                localDeviceId,
                connection.DeviceId);
            var existingGroups = _nativeLacp.CaptureGroupsForPair(pair, connection.CableIds);
            var decision = LacpPlanner.BuildConnectedDeviceDecision(
                pair,
                localDeviceId,
                connection.CableIds,
                existingGroups);

            AutoLacpLog.Info(
                $"Plan pair={decision.Pair} " +
                $"cables={AutoLacpLog.DescribeCableIds(decision.DesiredCableIds)} " +
                $"existingGroups=[{string.Join(",", decision.ExistingGroupIds)}] " +
                $"decision={decision.Kind} reason='{decision.Reason}'.");
            HandleDecision(decision, run, anchorSwitch);
        }
        catch (Exception exception)
        {
            run.Skipped++;
            run.ProcessingFailures++;
            AutoLacpLog.Warning(
                $"Failed while processing pair '{Describe(localDeviceId)}' <-> " +
                $"'{Describe(connection.DeviceId)}'. " +
                $"{exception.GetType().Name}: {exception.Message}\n{exception}");
        }
    }

    private static void HandleDecision(
        LacpDecision decision,
        SwitchRun run,
        NetworkSwitch anchorSwitch)
    {
        if (decision.Kind == LacpDecisionKind.NoOp)
        {
            run.Unchanged++;
            return;
        }
        if (decision.Kind == LacpDecisionKind.Skip)
        {
            run.Skipped++;
            run.PlannerSkips++;
            return;
        }

        _isApplying = true;
        try
        {
            if (!_applier.Apply(decision, anchorSwitch))
            {
                run.Skipped++;
                run.ApplyFailures++;
                AutoLacpLog.Warning(
                    $"Native apply did not complete for {decision.Pair}.");
                return;
            }

            if (decision.Kind == LacpDecisionKind.Create)
                run.CreatedGroups++;
            else
                run.ExtendedGroups++;
        }
        finally
        {
            _isApplying = false;
        }
    }

    private static bool IsSelf(
        ConnectedDevicesCapture capture,
        string deviceId)
    {
        return string.Equals(
                deviceId,
                capture.CandidateDeviceId,
                StringComparison.Ordinal)
            || string.Equals(
                deviceId,
                capture.SwitchId,
                StringComparison.Ordinal)
            || string.Equals(
                deviceId,
                capture.DeviceId,
                StringComparison.Ordinal);
    }

    private static void FinishActiveRun()
    {
        var run = _activeRun;
        _activeRun = null;
        if (run is null)
            return;

        var diagnostics =
            $"{run.Trigger} complete: endpoint candidates=" +
            $"{run.CandidateCount}, non-switchable={run.NonSwitchable}, " +
            $"unique switches processed={run.SwitchesProcessed}, " +
            $"connected records decoded={run.ConnectedRecords}, " +
            $"connection read failures={run.DecodeFailures}, " +
            $"parallel connection observations={run.ParallelConnectionObservations}, " +
            $"groups added={run.CreatedGroups}, " +
            $"groups extended={run.ExtendedGroups}, " +
            $"unchanged={run.Unchanged}, skipped={run.Skipped}.";
        AutoLacpLog.Info(diagnostics);

        if (run.Trigger != "post-load")
        {
            AccumulateRuntimeNotification(run);
            return;
        }

        var errors = BuildLoadErrors(run);
        var summary = BuildLoadPlayerSummary(run, errors.Count);
        AutoLacpLog.Info(summary);
        PlayerNotifier.Show(summary);
        for (var index = 0; index < errors.Count; index++)
        {
            PlayerNotifier.Show(
                $"Load error {index + 1}/{errors.Count}: {errors[index]}");
        }
    }

    private static void AccumulateRuntimeNotification(SwitchRun run)
    {
        AccumulateRuntimeNotification(
            run.CableCount,
            run.ProcessedSwitchIds,
            run.CreatedGroups,
            run.ExtendedGroups,
            run.UnresolvedCableRegistrations
                + run.DecodeFailures
                + run.ProcessingFailures
                + run.PlannerSkips
                + run.ApplyFailures);
    }

    private static void AccumulateRuntimeNotification(
        int cableConnections,
        IEnumerable<string> switchIds,
        int createdGroups,
        int extendedGroups,
        int errors)
    {
        if (!RuntimeNotifications.IsScheduled)
        {
            RuntimeNotifications.IsScheduled = true;
            RuntimeNotifications.DueTime =
                Time.realtimeSinceStartup + RuntimeNotificationWindowSeconds;
        }

        RuntimeNotifications.SwitchIds.UnionWith(switchIds);
        RuntimeNotifications.CableConnections += cableConnections;
        RuntimeNotifications.Evaluations++;
        RuntimeNotifications.CreatedGroups += createdGroups;
        RuntimeNotifications.ExtendedGroups += extendedGroups;
        RuntimeNotifications.Errors += errors;
    }

    private static void FlushRuntimeNotification()
    {
        var cableConnections = RuntimeNotifications.CableConnections;
        var switchCount = RuntimeNotifications.SwitchIds.Count;
        var evaluations = RuntimeNotifications.Evaluations;
        var createdGroups = RuntimeNotifications.CreatedGroups;
        var extendedGroups = RuntimeNotifications.ExtendedGroups;
        var errors = RuntimeNotifications.Errors;
        RuntimeNotifications.Reset();

        var summary = BuildRuntimePlayerSummary(
            cableConnections,
            switchCount,
            evaluations,
            createdGroups,
            extendedGroups,
            errors);
        AutoLacpLog.Info(
            $"30-second activity window complete: cable connections=" +
            $"{cableConnections}, unique switch-capable devices={switchCount}, " +
            $"evaluations={evaluations}, groups added={createdGroups}, " +
            $"groups extended={extendedGroups}, errors={errors}.");
        AutoLacpLog.Info(summary);
        PlayerNotifier.Show(summary);
    }

    private static string BuildRuntimePlayerSummary(
        int cableConnections,
        int switchCount,
        int evaluations,
        int createdGroups,
        int extendedGroups,
        int errors)
    {
        var subject =
            $"{cableConnections} " +
            $"{Pluralize(cableConnections, "cable connection")} processed " +
            $"across {switchCount} " +
            $"{Pluralize(switchCount, "switch-capable device")} in " +
            $"{evaluations} {Pluralize(evaluations, "evaluation")}";

        string outcome;
        if (createdGroups > 0 && extendedGroups > 0)
        {
            outcome =
                $"{createdGroups} {Pluralize(createdGroups, "LACP group")} " +
                $"added and {extendedGroups} extended";
        }
        else if (createdGroups > 0)
        {
            outcome =
                $"{createdGroups} {Pluralize(createdGroups, "LACP group")} added";
        }
        else if (extendedGroups > 0)
        {
            outcome =
                $"{extendedGroups} " +
                $"{Pluralize(extendedGroups, "LACP group")} extended";
        }
        else
        {
            outcome = errors == 0
                ? "no LACP changes"
                : "no LACP changes applied";
        }

        if (errors > 0)
        {
            outcome +=
                $"; {errors} {Pluralize(errors, "error")} occurred. See log";
        }

        return $"{subject}: {outcome}.";
    }

    private static string Describe(string value) =>
        NativeConnectedDeviceReader.Describe(value);

    private static string DescribeSample(IEnumerable<string> values)
    {
        const int maximum = 10;
        var sample = values.Take(maximum).ToArray();
        return $"[{string.Join(", ", sample)}" +
            (sample.Length == maximum ? ", ...]" : "]");
    }

    private static string BuildLoadPlayerSummary(
        SwitchRun run,
        int errorCategoryCount)
    {
        var subject =
            $"Loaded network processed across {run.SwitchesProcessed} " +
            Pluralize(run.SwitchesProcessed, "switch-capable device");

        var changes = new List<string>();
        if (run.CreatedGroups > 0)
        {
            changes.Add(
                $"{run.CreatedGroups} " +
                $"{Pluralize(run.CreatedGroups, "LACP group")} added");
        }
        if (run.ExtendedGroups > 0)
        {
            changes.Add(run.CreatedGroups > 0
                ? $"{run.ExtendedGroups} extended"
                : $"{run.ExtendedGroups} " +
                    $"{Pluralize(run.ExtendedGroups, "LACP group")} extended");
        }

        var outcome = changes.Count == 0
            ? run.Skipped == 0 && errorCategoryCount == 0
                ? "no LACP changes needed"
                : "no LACP changes applied"
            : string.Join(", ", changes);
        if (errorCategoryCount > 0)
        {
            outcome += $"; {errorCategoryCount} " +
                $"{Pluralize(errorCategoryCount, "error")} found";
        }
        return $"{subject}: {outcome}.";
    }

    private static List<string> BuildLoadErrors(SwitchRun run)
    {
        var errors = new List<string>();
        if (run.UnresolvedCableRegistrations > 0)
        {
            errors.Add(
                $"{run.UnresolvedCableRegistrations} " +
                $"{Pluralize(run.UnresolvedCableRegistrations, "cable connection")} " +
                $"{WasWere(run.UnresolvedCableRegistrations)} reported without " +
                "connected device IDs.");
        }
        if (run.DecodeFailures > 0)
        {
            errors.Add(
                $"{run.DecodeFailures} connected-device or cable-chain " +
                $"{Pluralize(run.DecodeFailures, "record")} could not be read; " +
                "affected LACP checks were skipped.");
        }
        if (run.ProcessingFailures > 0)
        {
            errors.Add(
                $"Processing failed for {run.ProcessingFailures} " +
                $"{Pluralize(run.ProcessingFailures, "switch or device-pair check")}; " +
                "affected LACP checks were skipped.");
        }
        if (run.PlannerSkips > 0)
        {
            errors.Add(
                $"{run.PlannerSkips} " +
                $"{Pluralize(run.PlannerSkips, "LACP check")} could not be " +
                $"completed safely and {WasWere(run.PlannerSkips)} skipped.");
        }
        if (run.ApplyFailures > 0)
        {
            errors.Add(
                $"{run.ApplyFailures} native " +
                $"{Pluralize(run.ApplyFailures, "LACP update")} did not " +
                "complete; see the log for recovery details.");
        }
        return errors;
    }

    private static void ReportLoadWithoutCableEvents()
    {
        if (!TryGetRegisteredCableCount(out var cableCount))
        {
            const string unknownMessage =
                "Loaded network could not be processed: no cable connections " +
                "were reported to AutoLACP; no LACP changes applied. See log.";
            AutoLacpLog.Warning(
                "Post-load received no cable registration events and the native " +
                "cable count could not be read. No fallback scan was performed.");
            PlayerNotifier.Show(unknownMessage);
            return;
        }

        if (cableCount == 0)
        {
            const string emptyMessage =
                "Loaded network contained no cable connections: no LACP " +
                "changes needed.";
            AutoLacpLog.Info(
                "Post-load received no cable registration events because the " +
                "native network contains no cable connections.");
            PlayerNotifier.Show(emptyMessage);
            return;
        }

        const string failedMessage =
            "Loaded network could not be processed: cable connections exist, " +
            "but none were reported to AutoLACP; no LACP changes applied. " +
            "See log.";
        AutoLacpLog.Warning(
            $"Post-load received 0 cable registration events while the native " +
            $"network contains {cableCount} " +
            $"{Pluralize(cableCount, "cable connection")}. No fallback scan " +
            "was performed.");
        PlayerNotifier.Show(failedMessage);
    }

    private static bool TryGetRegisteredCableCount(out int cableCount)
    {
        cableCount = 0;
        try
        {
            var connections = NetworkMap.instance?.cableConnections;
            if (connections is null)
                return false;

            cableCount = connections.Count;
            return true;
        }
        catch (Exception exception)
        {
            AutoLacpLog.Warning(
                "Could not read the native cable count after network load. " +
                $"{exception.GetType().Name}: {exception.Message}");
            return false;
        }
    }

    private static string Pluralize(int count, string singular) =>
        count == 1 ? singular : singular + "s";

    private static string WasWere(int count) =>
        count == 1 ? "was" : "were";

    private static void ResetWork()
    {
        PendingCableIds.Clear();
        PendingDeviceIds.Clear();
        UnresolvedCableRegistrations.Clear();
        _activeRun = null;
        _isApplying = false;
        _loadBatchPending = false;
        _batchScheduled = false;
        _batchFirstEventTime = 0f;
        _batchDueTime = 0f;
        RuntimeNotifications.Reset();
    }
}
