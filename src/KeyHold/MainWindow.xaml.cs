using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KeyHold.Models;
using KeyHold.Services;
using AppInputBinding = KeyHold.Models.InputBinding;
using AppThemeMode = KeyHold.Models.ThemeMode;
using WpfButton = System.Windows.Controls.Button;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfTextBox = System.Windows.Controls.TextBox;

namespace KeyHold;

public partial class MainWindow
{
    private readonly ConfigService configService;
    private readonly KeyHoldEngine engine;
    private readonly IStartupService startupService;
    private AppSettings settings;
    private BindingTarget? captureTarget;
    private bool isLoading;
    private bool allowClose;
    private bool startupEnabled;

    public MainWindow(AppSettings settings, ConfigService configService, KeyHoldEngine engine, IStartupService startupService)
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
        StatusPill.Background = FindBrush(status.IsActive ? "AccentBrush" : "BorderBrush");
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

        CancelCapture("Key capture canceled.");
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (captureTarget is null)
        {
            return;
        }

        e.Handled = true;
        CompleteKeyCapture(KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key));
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
        UpdateBindingUi();
    }

    private void SaveSettingsFromUi()
    {
        if (isLoading)
        {
            return;
        }

        try
        {
            settings.ActivationMode = GetSelectedEnumOrCurrent(ActivationModeBox, settings.ActivationMode);
            settings.Theme = GetSelectedEnumOrCurrent(ThemeBox, settings.Theme);
            settings.MouseTrigger = AppInputBinding.Mouse(GetSelectedMouseTriggerOrCurrent());
            settings.SuppressTriggerInput = SuppressTriggersBox.IsChecked == true;
            settings.LaunchToTray = LaunchToTrayBox.IsChecked == true;
            settings.ShowNotifications = NotificationsBox.IsChecked == true;

            configService.Save(settings);
            TryApplyStartupSetting(StartupBox.IsChecked == true);
            engine.UpdateSettings(settings);
            ThemeService.Apply(settings.Theme);
            UpdateBindingUi();
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

    private static TEnum GetSelectedEnumOrCurrent<TEnum>(WpfComboBox box, TEnum currentValue)
        where TEnum : struct, Enum
    {
        return TryGetSelectedTag(box, out var tag) && Enum.TryParse<TEnum>(tag, out var selectedValue)
            ? selectedValue
            : currentValue;
    }

    private MouseTriggerCode GetSelectedMouseTriggerOrCurrent()
    {
        var current = Enum.IsDefined(typeof(MouseTriggerCode), settings.MouseTrigger.Code)
            ? (MouseTriggerCode)settings.MouseTrigger.Code
            : MouseTriggerCode.XButton1;
        return GetSelectedEnumOrCurrent(MouseBindingBox, current);
    }

    private System.Windows.Media.Brush FindBrush(string resourceKey)
    {
        return TryFindResource(resourceKey) as System.Windows.Media.Brush
            ?? System.Windows.Media.Brushes.Transparent;
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
        if (!IsCaptureTargetAvailable(target))
        {
            return;
        }

        captureTarget = target;
        engine.SetUiCaptureActive(true);
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Press a key to set {GetBindingTargetName(target).ToLowerInvariant()}."));
        Activate();
        Dispatcher.InvokeAsync(() =>
        {
            var targetBox = GetBindingTextBox(target);
            targetBox.Focus();
            Keyboard.Focus(targetBox);
        }, DispatcherPriority.Input);
    }

    internal void CompleteKeyCaptureForTest(int virtualKey)
    {
        CompleteKeyCapture(virtualKey);
    }

    private void CompleteKeyCapture(int virtualKey)
    {
        if (captureTarget is not { } target || virtualKey == 0)
        {
            return;
        }

        var binding = AppInputBinding.Keyboard(virtualKey);
        switch (target)
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
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Set {GetBindingTargetName(target).ToLowerInvariant()} to {binding.DisplayName}."));
    }

    private void CancelCapture(string message)
    {
        captureTarget = null;
        engine.SetUiCaptureActive(false);
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, message));
    }

    private void UpdateBindingUi()
    {
        var activationMode = GetSelectedEnumOrCurrent(ActivationModeBox, settings.ActivationMode);
        var isToggle = activationMode == ActivationMode.Toggle;
        var isSeparateKeys = activationMode == ActivationMode.SeparateKeys;
        var isMouseTrigger = activationMode == ActivationMode.MouseTrigger;

        EnableBindingPanel.Visibility = isMouseTrigger ? Visibility.Collapsed : Visibility.Visible;
        StopBindingPanel.Visibility = isSeparateKeys ? Visibility.Visible : Visibility.Collapsed;
        MouseBindingPanel.Visibility = isMouseTrigger ? Visibility.Visible : Visibility.Collapsed;

        EnableBindingLabel.Text = isToggle ? "Toggle key" : "Enable key";
        SetCaptureControlState(BindingTarget.Enable, EnableBindingText, CaptureEnableButton, settings.EnableBinding.DisplayName);
        SetCaptureControlState(BindingTarget.Stop, StopBindingText, CaptureStopButton, settings.StopBinding.DisplayName);
        SetCaptureControlState(BindingTarget.Emergency, EmergencyBindingText, CaptureEmergencyButton, settings.EmergencyBinding.DisplayName);
    }

    private void SetCaptureControlState(BindingTarget target, WpfTextBox textBox, WpfButton button, string bindingName)
    {
        var isCapturing = captureTarget == target;
        textBox.Text = isCapturing ? "Press a key..." : bindingName;
        button.Content = isCapturing ? "Listening..." : $"Set {GetBindingTargetName(target)}";
    }

    private string GetBindingTargetName(BindingTarget target)
    {
        var activationMode = GetSelectedEnumOrCurrent(ActivationModeBox, settings.ActivationMode);
        return target switch
        {
            BindingTarget.Enable when activationMode == ActivationMode.Toggle => "Toggle Key",
            BindingTarget.Enable => "Enable Key",
            BindingTarget.Stop => "Stop Key",
            BindingTarget.Emergency => "Emergency Key",
            _ => "Key"
        };
    }

    private bool IsCaptureTargetAvailable(BindingTarget target)
    {
        var activationMode = GetSelectedEnumOrCurrent(ActivationModeBox, settings.ActivationMode);
        return target switch
        {
            BindingTarget.Enable => activationMode != ActivationMode.MouseTrigger,
            BindingTarget.Stop => activationMode == ActivationMode.SeparateKeys,
            BindingTarget.Emergency => true,
            _ => false
        };
    }

    private WpfTextBox GetBindingTextBox(BindingTarget target)
    {
        return target switch
        {
            BindingTarget.Enable => EnableBindingText,
            BindingTarget.Stop => StopBindingText,
            BindingTarget.Emergency => EmergencyBindingText,
            _ => EnableBindingText
        };
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
