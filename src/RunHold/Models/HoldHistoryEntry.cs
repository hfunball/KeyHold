namespace RunHold.Models;

public sealed record HoldHistoryEntry(DateTime Timestamp, IReadOnlyCollection<int> HeldKeys);
