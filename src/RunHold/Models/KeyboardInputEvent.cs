namespace RunHold.Models;

public sealed record KeyboardInputEvent(int VirtualKey, bool IsDown, bool IsInjected, bool IsAltDown);

