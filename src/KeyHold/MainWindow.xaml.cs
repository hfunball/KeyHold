using System.IO;
using System.Windows;
using System.Windows.Input;
using System.Windows.Threading;
using KeyHold.Models;
using KeyHold.Services;
using AppInputBinding = KeyHold.Models.InputBinding;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;

namespace KeyHold;

public partial class MainWindow
{
    private readonly ConfigService configService;
    private readonly KeyHoldEngine engine;
    private readonly IStartupService startupService;
    private AppSettings settings;
    private bool isCapturingToggle;
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

        isCapturingToggle = false;
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
        if (!isCapturingToggle)
        {
            return;
        }

        CancelCapture("Key capture canceled.");
    }

    protected override void OnPreviewKeyDown(WpfKeyEventArgs e)
    {
        base.OnPreviewKeyDown(e);

        if (!isCapturingToggle)
        {
            return;
        }

        e.Handled = true;
        CompleteKeyCapture(KeyInterop.VirtualKeyFromKey(e.Key == Key.System ? e.SystemKey : e.Key));
    }

    private void LoadSettingsToUi()
    {
        isLoading = true;
        SetComboSelection(ThemeBox, settings.Theme.ToString());
        ToggleBindingText.Text = settings.ToggleBinding.DisplayName;
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
            settings.Theme = GetSelectedEnumOrCurrent(ThemeBox, settings.Theme);
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

    private void CaptureToggle_Click(object sender, RoutedEventArgs e)
    {
        BeginCapture();
    }

    private void BeginCapture()
    {
        isCapturingToggle = true;
        engine.SetUiCaptureActive(true);
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, "Press a key to set toggle key."));
        Activate();
        Dispatcher.InvokeAsync(() =>
        {
            ToggleBindingText.Focus();
            Keyboard.Focus(ToggleBindingText);
        }, DispatcherPriority.Input);
    }

    internal void CompleteKeyCaptureForTest(int virtualKey)
    {
        CompleteKeyCapture(virtualKey);
    }

    private void CompleteKeyCapture(int virtualKey)
    {
        if (!isCapturingToggle || virtualKey == 0)
        {
            return;
        }

        var binding = AppInputBinding.Keyboard(virtualKey);
        settings.ToggleBinding = binding;
        isCapturingToggle = false;
        engine.SetUiCaptureActive(false);
        SaveSettingsFromUi();
        LoadSettingsToUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Set toggle key to {binding.DisplayName}."));
    }

    private void CancelCapture(string message)
    {
        isCapturingToggle = false;
        engine.SetUiCaptureActive(false);
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, message));
    }

    private void UpdateBindingUi()
    {
        ToggleBindingText.Text = isCapturingToggle ? "Press a key..." : settings.ToggleBinding.DisplayName;
        CaptureToggleButton.Content = isCapturingToggle ? "Listening..." : "Set Toggle Key";
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }
}
