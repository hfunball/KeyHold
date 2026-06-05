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
        StartupBox.IsChecked = startupService.IsEnabled();
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

        settings.ActivationMode = Enum.Parse<ActivationMode>(GetSelectedTag(ActivationModeBox));
        settings.Theme = Enum.Parse<AppThemeMode>(GetSelectedTag(ThemeBox));
        settings.MouseTrigger = AppInputBinding.Mouse(Enum.Parse<MouseTriggerCode>(GetSelectedTag(MouseBindingBox)));
        settings.SuppressTriggerInput = SuppressTriggersBox.IsChecked == true;
        settings.LaunchToTray = LaunchToTrayBox.IsChecked == true;
        settings.ShowNotifications = NotificationsBox.IsChecked == true;

        configService.Save(settings);
        startupService.SetEnabled(StartupBox.IsChecked == true);
        engine.UpdateSettings(settings);
        ThemeService.Apply(settings.Theme);
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

    private static string GetSelectedTag(WpfComboBox box)
    {
        return (string)((WpfComboBoxItem)box.SelectedItem).Tag;
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
