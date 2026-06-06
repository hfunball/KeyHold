using System.IO;
using System.Windows;
using System.Windows.Input;
using KeyHold.Models;
using KeyHold.Services;
using AppInputBinding = KeyHold.Models.InputBinding;
using AppThemeMode = KeyHold.Models.ThemeMode;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace KeyHold;

public partial class MainWindow
{
    private readonly ConfigService configService;
    private readonly KeyHoldEngine engine;
    private readonly StartupService startupService;
    private AppSettings settings;
    private BindingTarget? captureTarget;
    private bool isLoading;
    private bool allowClose;
    private bool startupEnabled;

    public MainWindow(AppSettings settings, ConfigService configService, KeyHoldEngine engine, StartupService startupService)
    {
        InitializeComponent();
        this.settings = settings;
        this.configService = configService;
        this.engine = engine;
        this.startupService = startupService;

        LoadSettingsToUi();
        UpdateStatus(engine.Status);
    }

    public void UpdateStatus(HoldStatus status)
    {
        StatusText.Text = status.IsActive ? "Holding" : "Idle";
        StatusPill.Background = (System.Windows.Media.Brush)FindResource(status.IsActive ? "AccentBrush" : "BorderBrush");
        HeldKeysText.Text = status.IsActive
            ? $"Holding: {string.Join(", ", status.HeldKeys.Select(VirtualKeyNames.GetName))}"
            : "No keys held by KeyHold.";
    }

    public void AddDiagnostic(DiagnosticEntry entry)
    {
        DiagnosticsList.Items.Insert(0, $"{entry.Timestamp:T}  {entry.Message}");
        while (DiagnosticsList.Items.Count > 80)
        {
            DiagnosticsList.Items.RemoveAt(DiagnosticsList.Items.Count - 1);
        }
    }

    protected override void OnClosing(System.ComponentModel.CancelEventArgs e)
    {
        if (allowClose)
        {
            base.OnClosing(e);
            return;
        }

        captureTarget = null;
        engine.SetUiCaptureActive(false);
        e.Cancel = true;
        Hide();
    }

    public void AllowClose()
    {
        allowClose = true;
    }

    protected override void OnDeactivated(EventArgs e)
    {
        base.OnDeactivated(e);
        if (captureTarget is null)
        {
            return;
        }

        captureTarget = null;
        engine.SetUiCaptureActive(false);
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, "Key capture canceled."));
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (captureTarget is null)
        {
            return;
        }

        e.Handled = true;
        var binding = AppInputBinding.Keyboard(KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key));

        switch (captureTarget)
        {
            case BindingTarget.Enable:
                settings.EnableBinding = binding;
                break;
            case BindingTarget.Stop:
                settings.StopBinding = binding;
                break;
            case BindingTarget.Emergency:
                settings.EmergencyBinding = binding;
                break;
        }

        captureTarget = null;
        engine.SetUiCaptureActive(false);
        SaveSettingsFromUi();
        LoadSettingsToUi();
    }

    private void LoadSettingsToUi()
    {
        isLoading = true;
        SetComboSelection(ActivationModeBox, settings.ActivationMode.ToString());
        SetComboSelection(ThemeBox, settings.Theme.ToString());
        SetComboSelection(MouseBindingBox, settings.MouseTrigger.Code.ToString());
        EnableBindingText.Text = settings.EnableBinding.DisplayName;
        StopBindingText.Text = settings.StopBinding.DisplayName;
        EmergencyBindingText.Text = settings.EmergencyBinding.DisplayName;
        SuppressTriggersBox.IsChecked = settings.SuppressTriggerInput;
        startupEnabled = TryReadStartupEnabled();
        StartupBox.IsChecked = startupEnabled;
        LaunchToTrayBox.IsChecked = settings.LaunchToTray;
        NotificationsBox.IsChecked = settings.ShowNotifications;
        isLoading = false;
    }

    private void SaveSettingsFromUi()
    {
        if (isLoading)
        {
            return;
        }

        try
        {
            if (!TryGetSelectedTag(ActivationModeBox, out var activationMode)
                || !TryGetSelectedTag(ThemeBox, out var theme)
                || !TryGetSelectedTag(MouseBindingBox, out var mouseTrigger))
            {
                AddDiagnostic(new DiagnosticEntry(DateTime.Now, "Settings change skipped while controls were still loading."));
                return;
            }

            settings.ActivationMode = Enum.Parse<ActivationMode>(activationMode);
            settings.Theme = Enum.Parse<AppThemeMode>(theme);
            settings.MouseTrigger = AppInputBinding.Mouse(Enum.Parse<MouseTriggerCode>(mouseTrigger));
            settings.SuppressTriggerInput = SuppressTriggersBox.IsChecked == true;
            settings.LaunchToTray = LaunchToTrayBox.IsChecked == true;
            settings.ShowNotifications = NotificationsBox.IsChecked == true;

            configService.Save(settings);
            TryApplyStartupSetting(StartupBox.IsChecked == true);
            engine.UpdateSettings(settings);
            ThemeService.Apply(settings.Theme);
        }
        catch (Exception ex) when (ex is ArgumentException or IOException or UnauthorizedAccessException or InvalidOperationException)
        {
            AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Settings change failed: {ex.Message}"));
            LoadSettingsToUi();
        }
    }

    private static void SetComboSelection(WpfComboBox box, string tag)
    {
        foreach (WpfComboBoxItem item in box.Items)
        {
            if (string.Equals((string)item.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                box.SelectedItem = item;
                return;
            }
        }
    }

    private static bool TryGetSelectedTag(WpfComboBox box, out string tag)
    {
        tag = string.Empty;
        if (box.SelectedItem is not WpfComboBoxItem item || item.Tag is not string selectedTag)
        {
            return false;
        }

        tag = selectedTag;
        return true;
    }

    private bool TryReadStartupEnabled()
    {
        try
        {
            return startupService.IsEnabled();
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Could not read Windows startup setting: {ex.Message}"));
            return false;
        }
    }

    private void TryApplyStartupSetting(bool enabled)
    {
        if (enabled == startupEnabled)
        {
            return;
        }

        try
        {
            startupService.SetEnabled(enabled);
            startupEnabled = enabled;
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or System.Security.SecurityException)
        {
            AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Could not update Windows startup setting: {ex.Message}"));
            isLoading = true;
            StartupBox.IsChecked = startupEnabled;
            isLoading = false;
        }
    }

    private void ReleaseAll_Click(object sender, RoutedEventArgs e)
    {
        engine.ReleaseAll("Manual release");
    }

    private void HideToTray_Click(object sender, RoutedEventArgs e)
    {
        Hide();
    }

    private void CaptureEnable_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(BindingTarget.Enable);
    }

    private void CaptureStop_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(BindingTarget.Stop);
    }

    private void CaptureEmergency_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture(BindingTarget.Emergency);
    }

    private void BeginCapture(BindingTarget target)
    {
        captureTarget = target;
        engine.SetUiCaptureActive(true);
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Press a key to set {target}."));
        Activate();
        Focus();
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private enum BindingTarget
    {
        Enable,
        Stop,
        Emergency
    }
}
