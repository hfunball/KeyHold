namespace RunHold.Models;

public sealed record HoldStatus(bool IsActive, IReadOnlyCollection<int> HeldKeys, string Reason);

