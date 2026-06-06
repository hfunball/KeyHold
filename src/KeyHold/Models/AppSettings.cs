namespace KeyHold.Models;

public sealed class AppSettings
{
    public InputBinding ToggleBinding { get; set; } = InputBinding.Keyboard(0x24);

    public ThemeMode Theme { get; set; } = ThemeMode.Dark;

    public bool LaunchToTray { get; set; } = true;

    public bool ShowNotifications { get; set; } = true;

    public bool HasSeenFirstRun { get; set; }
}
