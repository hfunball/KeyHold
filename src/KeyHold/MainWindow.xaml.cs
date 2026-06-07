using System.IO;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using KeyHold.Models;
using KeyHold.Services;
using AppInputBinding = KeyHold.Models.InputBinding;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

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
        engine.ToggleTriggerCaptured += Engine_ToggleTriggerCaptured;

        LoadSettingsToUi();
        LoadReadMeToUi();
        UpdateStatus(engine.Status);
    }

    public void UpdateStatus(HoldStatus status)
    {
        StatusText.Text = status.IsActive ? "Holding" : "Idle";
        StatusPill.Background = FindBrush(status.IsActive ? "AccentBrush" : "StatusIdleBrush");
        StatusText.Foreground = FindBrush(status.IsActive ? "OnAccentBrush" : "TextBrush");
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
        engine.CancelToggleTriggerCapture();
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

    protected override void OnPreviewMouseDown(WpfMouseButtonEventArgs e)
    {
        base.OnPreviewMouseDown(e);

        if (!isCapturingToggle || !TryGetMouseTrigger(e.ChangedButton, out var button))
        {
            return;
        }

        e.Handled = true;
        CompleteMouseCapture(button);
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
        StopOnAnyKeyBox.IsChecked = settings.StopOnAnyKeyboardPress;
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
            settings.StopOnAnyKeyboardPress = StopOnAnyKeyBox.IsChecked == true;

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
        engine.BeginToggleTriggerCapture();
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, "Press a key or supported mouse button to set toggle trigger."));
        Activate();
        Dispatcher.InvokeAsync(() =>
        {
            ToggleBindingText.Focus();
            Keyboard.Focus(ToggleBindingText);
        }, DispatcherPriority.Input);
    }

    internal void CompleteKeyCaptureForTest(int virtualKey)
    {
        CompleteCapture(AppInputBinding.Keyboard(virtualKey));
    }

    internal void CompleteMouseCaptureForTest(MouseTriggerCode button)
    {
        CompleteCapture(AppInputBinding.Mouse(button));
    }

    private void CompleteKeyCapture(int virtualKey)
    {
        if (!isCapturingToggle || virtualKey == 0)
        {
            return;
        }

        var binding = AppInputBinding.Keyboard(virtualKey);
        CompleteCapture(binding);
    }

    private void CompleteMouseCapture(MouseTriggerCode button)
    {
        if (!isCapturingToggle)
        {
            return;
        }

        CompleteCapture(AppInputBinding.Mouse(button));
    }

    private void CompleteCapture(AppInputBinding binding)
    {
        settings.ToggleBinding = binding;
        isCapturingToggle = false;
        engine.CancelToggleTriggerCapture();
        SaveSettingsFromUi();
        LoadSettingsToUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, $"Set toggle trigger to {binding.DisplayName}."));
    }

    private void CancelCapture(string message)
    {
        isCapturingToggle = false;
        engine.CancelToggleTriggerCapture();
        UpdateBindingUi();
        AddDiagnostic(new DiagnosticEntry(DateTime.Now, message));
    }

    private void UpdateBindingUi()
    {
        ToggleBindingText.Text = isCapturingToggle ? "Press key or mouse..." : settings.ToggleBinding.DisplayName;
        CaptureToggleButton.Content = isCapturingToggle ? "Listening..." : "Set Toggle Trigger";
    }

    private void SettingChanged(object sender, RoutedEventArgs e)
    {
        SaveSettingsFromUi();
    }

    private void LoadReadMeToUi()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "ReadMe.md");
        var markdown = File.Exists(path)
            ? File.ReadAllText(path)
            : "## KeyHold\n\nKeyHold keeps held movement keys down until you stop it.";

        ReadMeViewer.Document = BuildReadMeDocument(markdown);
    }

    private FlowDocument BuildReadMeDocument(string markdown)
    {
        var document = new FlowDocument
        {
            Background = FindBrush("AppBackgroundBrush"),
            Foreground = FindBrush("TextBrush"),
            FontFamily = FontFamily,
            FontSize = 14,
            PagePadding = new Thickness(0)
        };

        foreach (var rawLine in markdown.Replace("\r\n", "\n").Split('\n'))
        {
            var line = rawLine.TrimEnd();
            if (string.IsNullOrWhiteSpace(line))
            {
                continue;
            }

            if (line.StartsWith("# ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateParagraph(line[2..], 24, FontWeights.SemiBold, 0, 0, 0, 10));
            }
            else if (line.StartsWith("## ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateParagraph(line[3..], 17, FontWeights.SemiBold, 14, 0, 0, 6));
            }
            else if (line.StartsWith("- ", StringComparison.Ordinal))
            {
                document.Blocks.Add(CreateParagraph($"• {line[2..]}", 14, FontWeights.Normal, 0, 0, 0, 4));
            }
            else
            {
                document.Blocks.Add(CreateParagraph(line, 14, FontWeights.Normal, 0, 0, 0, 8));
            }
        }

        return document;
    }

    private Paragraph CreateParagraph(string text, double size, FontWeight weight, double left, double top, double right, double bottom)
    {
        return new Paragraph(new Run(text))
        {
            FontSize = size,
            FontWeight = weight,
            Margin = new Thickness(left, top, right, bottom),
            Foreground = weight == FontWeights.Normal ? FindBrush("TextBrush") : FindBrush("TextBrush")
        };
    }

    private void Engine_ToggleTriggerCaptured(object? sender, AppInputBinding binding)
    {
        Dispatcher.Invoke(() =>
        {
            if (isCapturingToggle)
            {
                CompleteCapture(binding);
            }
        });
    }

    private static bool TryGetMouseTrigger(MouseButton button, out MouseTriggerCode trigger)
    {
        trigger = button switch
        {
            MouseButton.Middle => MouseTriggerCode.Middle,
            MouseButton.XButton1 => MouseTriggerCode.XButton1,
            MouseButton.XButton2 => MouseTriggerCode.XButton2,
            _ => default
        };

        return button is MouseButton.Middle or MouseButton.XButton1 or MouseButton.XButton2;
    }
}
