namespace RunHold.Models;

public sealed record MouseInputEvent(MouseTriggerCode Button, bool IsDown, bool IsInjected);
