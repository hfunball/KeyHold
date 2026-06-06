namespace KeyHold.Models;

public sealed class InputBinding
{
    public InputDeviceKind Device { get; set; }

    public int Code { get; set; }

    public string DisplayName { get; set; } = string.Empty;

    public static InputBinding Keyboard(int virtualKey)
    {
        return new InputBinding
        {
            Device = InputDeviceKind.Keyboard,
            Code = virtualKey,
            DisplayName = VirtualKeyNames.GetName(virtualKey)
        };
    }

    public bool MatchesKeyboard(int virtualKey)
    {
        return Device == InputDeviceKind.Keyboard && Code == virtualKey;
    }
}
