namespace RunHold.Models;

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

    public static InputBinding Mouse(MouseTriggerCode code)
    {
        return new InputBinding
        {
            Device = InputDeviceKind.Mouse,
            Code = (int)code,
            DisplayName = code switch
            {
                MouseTriggerCode.Middle => "Middle Mouse",
                MouseTriggerCode.XButton1 => "Mouse Button 4",
                MouseTriggerCode.XButton2 => "Mouse Button 5",
                _ => code.ToString()
            }
        };
    }

    public bool MatchesKeyboard(int virtualKey)
    {
        return Device == InputDeviceKind.Keyboard && Code == virtualKey;
    }

    public bool MatchesMouse(MouseTriggerCode code)
    {
        return Device == InputDeviceKind.Mouse && Code == (int)code;
    }
}
