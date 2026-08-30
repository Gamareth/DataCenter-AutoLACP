namespace AutoLACP;

internal readonly struct CanonicalEndpointPair : IEquatable<CanonicalEndpointPair>
{
    internal string First { get; }
    internal string Second { get; }

    internal CanonicalEndpointPair(string endpointA, string endpointB)
    {
        if (string.IsNullOrWhiteSpace(endpointA))
            throw new ArgumentException("Endpoint A is required.", nameof(endpointA));
        if (string.IsNullOrWhiteSpace(endpointB))
            throw new ArgumentException("Endpoint B is required.", nameof(endpointB));

        if (string.CompareOrdinal(endpointA, endpointB) <= 0)
        {
            First = endpointA;
            Second = endpointB;
        }
        else
        {
            First = endpointB;
            Second = endpointA;
        }
    }

    public bool Equals(CanonicalEndpointPair other)
    {
        return string.Equals(First, other.First, StringComparison.Ordinal)
            && string.Equals(Second, other.Second, StringComparison.Ordinal);
    }

    public override bool Equals(object obj) =>
        obj is CanonicalEndpointPair other && Equals(other);

    public override int GetHashCode() => HashCode.Combine(First, Second);

    public override string ToString() => $"{First} <-> {Second}";
}

internal sealed class NativeGroupSnapshot
{
    internal int GroupId { get; }
    internal CanonicalEndpointPair Pair { get; }
    internal IReadOnlyList<int> CableIds { get; }
    internal string NativeDeviceA { get; }
    internal string NativeDeviceB { get; }
    internal string ValidationIssue { get; }

    internal NativeGroupSnapshot(
        int groupId,
        CanonicalEndpointPair pair,
        IReadOnlyList<int> cableIds,
        string nativeDeviceA = null,
        string nativeDeviceB = null,
        string validationIssue = null)
    {
        GroupId = groupId;
        Pair = pair;
        CableIds = cableIds;
        NativeDeviceA = nativeDeviceA ?? pair.First;
        NativeDeviceB = nativeDeviceB ?? pair.Second;
        ValidationIssue = validationIssue;
    }
}

internal enum LacpDecisionKind
{
    NoOp,
    Create,
    Replace,
    Skip
}

internal sealed class LacpDecision
{
    internal LacpDecisionKind Kind { get; }
    internal CanonicalEndpointPair Pair { get; }
    internal string AnchorSwitchId { get; }
    internal IReadOnlyList<int> DesiredCableIds { get; }
    internal IReadOnlyList<int> ExistingGroupIds { get; }
    internal string Reason { get; }

    internal LacpDecision(
        LacpDecisionKind kind,
        CanonicalEndpointPair pair,
        string anchorSwitchId,
        IReadOnlyList<int> desiredCableIds,
        IReadOnlyList<int> existingGroupIds,
        string reason)
    {
        Kind = kind;
        Pair = pair;
        AnchorSwitchId = anchorSwitchId;
        DesiredCableIds = desiredCableIds;
        ExistingGroupIds = existingGroupIds;
        Reason = reason;
    }
}
