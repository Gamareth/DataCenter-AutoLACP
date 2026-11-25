using AutoLACP;

var tests = new (string Name, Action Run)[]
{
    ("one cable does not create a group", OneCableDoesNotCreate),
    ("two unique cables create a group", TwoCablesCreate),
    ("duplicate cable observations collapse", DuplicateCableObservationsCollapse),
    ("an exact native group is unchanged", ExactGroupIsNoOp),
    ("a strict membership expansion is allowed", StrictExpansionIsAllowed),
    ("a same-size mismatch is skipped", SameSizeMismatchSkips),
    ("a membership shrink is skipped", ShrinkSkips),
    ("duplicate native groups are skipped", DuplicateGroupsSkip),
    ("invalid native groups are skipped", InvalidGroupsSkip),
    ("an unrelated native group is ignored", UnrelatedGroupIsIgnored),
    ("pair identity is direction independent", PairIdentityIsDirectionIndependent),
    ("a newer Workshop version is accepted", NewerWorkshopVersionIsAccepted),
    ("an older Workshop version is rejected", OlderWorkshopVersionIsRejected),
    ("an identical Workshop payload is ignored", IdenticalWorkshopPayloadIsIgnored),
    ("a newer same-version build is accepted", NewerSameVersionBuildIsAccepted),
    ("an applied same-version build is ignored", AppliedSameVersionBuildIsIgnored),
    ("an invalid Workshop hash is rejected", InvalidWorkshopHashIsRejected)
};

var failures = 0;
var allTests = tests.Concat(NativeFailureTests.Cases).ToArray();
foreach (var test in allTests)
{
    try
    {
        test.Run();
        Console.WriteLine($"PASS {test.Name}");
    }
    catch (Exception exception)
    {
        failures++;
        Console.Error.WriteLine($"FAIL {test.Name}: {exception.Message}");
    }
}

Console.WriteLine($"{allTests.Length - failures}/{allTests.Length} tests passed.");
return failures == 0 ? 0 : 1;

static CanonicalEndpointPair Pair() => new("Switch_A", "Server_B");

static LacpDecision Decide(
    IReadOnlyList<int> cableIds,
    params NativeGroupSnapshot[] groups) =>
    LacpPlanner.BuildConnectedDeviceDecision(
        Pair(),
        "Switch_A",
        cableIds,
        groups);

static void OneCableDoesNotCreate()
{
    var decision = Decide([10]);
    Equal(LacpDecisionKind.NoOp, decision.Kind, "decision kind");
}

static void TwoCablesCreate()
{
    var decision = Decide([10, 11]);
    Equal(LacpDecisionKind.Create, decision.Kind, "decision kind");
    SequenceEqual([10, 11], decision.DesiredCableIds, "desired membership");
}

static void DuplicateCableObservationsCollapse()
{
    var decision = Decide([11, 10, 11, 10]);
    Equal(LacpDecisionKind.Create, decision.Kind, "decision kind");
    SequenceEqual([10, 11], decision.DesiredCableIds, "desired membership");
}

static void ExactGroupIsNoOp()
{
    var decision = Decide(
        [10, 11],
        new NativeGroupSnapshot(5, Pair(), [11, 10]));
    Equal(LacpDecisionKind.NoOp, decision.Kind, "decision kind");
}

static void StrictExpansionIsAllowed()
{
    var decision = Decide(
        [10, 11, 12],
        new NativeGroupSnapshot(5, Pair(), [10, 11]));
    Equal(LacpDecisionKind.Replace, decision.Kind, "decision kind");
    SequenceEqual([10, 11, 12], decision.DesiredCableIds, "desired membership");
}

static void SameSizeMismatchSkips()
{
    var decision = Decide(
        [10, 12],
        new NativeGroupSnapshot(5, Pair(), [10, 11]));
    Equal(LacpDecisionKind.Skip, decision.Kind, "decision kind");
}

static void ShrinkSkips()
{
    var decision = Decide(
        [10, 11],
        new NativeGroupSnapshot(5, Pair(), [10, 11, 12]));
    Equal(LacpDecisionKind.Skip, decision.Kind, "decision kind");
}

static void DuplicateGroupsSkip()
{
    var decision = Decide(
        [10, 11],
        new NativeGroupSnapshot(5, Pair(), [10, 11]),
        new NativeGroupSnapshot(6, Pair(), [10, 11]));
    Equal(LacpDecisionKind.Skip, decision.Kind, "decision kind");
}

static void InvalidGroupsSkip()
{
    var invalidGroups = new[]
    {
        new NativeGroupSnapshot(5, Pair(), []),
        new NativeGroupSnapshot(5, Pair(), [-1, 10]),
        new NativeGroupSnapshot(5, Pair(), [10, 10]),
        new NativeGroupSnapshot(
            5,
            Pair(),
            [10, 11],
            validationIssue: "Cable 11 is incomplete.")
    };

    foreach (var group in invalidGroups)
    {
        var decision = Decide([10, 11], group);
        Equal(LacpDecisionKind.Skip, decision.Kind, "decision kind");
    }
}

static void UnrelatedGroupIsIgnored()
{
    var unrelatedPair = new CanonicalEndpointPair("Switch_C", "Server_D");
    var decision = Decide(
        [10, 11],
        new NativeGroupSnapshot(8, unrelatedPair, [20, 21]));
    Equal(LacpDecisionKind.Create, decision.Kind, "decision kind");
}

static void PairIdentityIsDirectionIndependent()
{
    var forward = new CanonicalEndpointPair("Switch_A", "Server_B");
    var reverse = new CanonicalEndpointPair("Server_B", "Switch_A");
    Equal(forward, reverse, "canonical pair");
    Equal("Server_B", reverse.First, "first endpoint");
    Equal("Switch_A", reverse.Second, "second endpoint");
}

static void NewerWorkshopVersionIsAccepted()
{
    Equal(
        true,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(1, 1, 0),
            100,
            100,
            Hash('A'),
            Hash('B')),
        "update decision");
}

static void OlderWorkshopVersionIsRejected()
{
    Equal(
        false,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(0, 2, 0),
            200,
            100,
            Hash('A'),
            Hash('B')),
        "update decision");
}

static void IdenticalWorkshopPayloadIsIgnored()
{
    Equal(
        false,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(1, 0, 0),
            200,
            100,
            Hash('A'),
            Hash('A')),
        "update decision");
}

static void NewerSameVersionBuildIsAccepted()
{
    Equal(
        true,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(1, 0, 0),
            200,
            100,
            Hash('A'),
            Hash('B')),
        "update decision");
}

static void AppliedSameVersionBuildIsIgnored()
{
    Equal(
        false,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(1, 0, 0),
            100,
            100,
            Hash('A'),
            Hash('B')),
        "update decision");
}

static void InvalidWorkshopHashIsRejected()
{
    Equal(
        false,
        WorkshopUpdateRules.ShouldStageUpdate(
            new Version(1, 0, 0),
            new Version(1, 1, 0),
            200,
            100,
            "invalid",
            Hash('B')),
        "update decision");
}

static string Hash(char value) => new(value, 64);

static void Equal<T>(T expected, T actual, string label)
{
    if (!EqualityComparer<T>.Default.Equals(expected, actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected {expected}, got {actual}");
    }
}

static void SequenceEqual(
    IReadOnlyList<int> expected,
    IReadOnlyList<int> actual,
    string label)
{
    if (!expected.SequenceEqual(actual))
    {
        throw new InvalidOperationException(
            $"{label}: expected [{string.Join(",", expected)}], " +
            $"got [{string.Join(",", actual)}]");
    }
}
