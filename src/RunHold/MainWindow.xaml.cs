using System.IO;
using System.Reflection;
using System.Windows;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Threading;
using RunHold.Models;
using RunHold.Services;
using AppInputBinding = RunHold.Models.InputBinding;
using WpfComboBox = System.Windows.Controls.ComboBox;
using WpfComboBoxItem = System.Windows.Controls.ComboBoxItem;
using WpfKeyEventArgs = System.Windows.Input.KeyEventArgs;
using WpfMouseButtonEventArgs = System.Windows.Input.MouseButtonEventArgs;

namespace RunHold;

public partial class MainWindow
{
    private readonly ConfigService configService;
    private readonly RunHoldEngine engine;
    private readonly IStartupService startupService;
    private readonly List<string> holdHistoryLines = [];
    private readonly List<string> diagnosticHistoryLines = [];
    private AppSettings settings;
    private bool isCapturingToggle;
    private bool isLoading;
    private bool allowClose;
    private bool startupEnabled;
    private const int MaxHistoryRows = 80;

    public MainWindow(AppSettings settings, ConfigService configService, RunHoldEngine engine, IStartupService startupService)
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
            : "No keys held by RunHold.";
    }

    public void AddDiagnostic(DiagnosticEntry entry)
    {
        AddHistoryLine(diagnosticHistoryLines, $"{entry.Timestamp:T}  {entry.Message}");
        if (DiagnosticsModeBox.IsChecked == true)
        {
            RefreshHistoryList();
        }
    }

    public void AddHoldHistory(HoldHistoryEntry entry)
    {
        var combo = FormatHoldCombo(entry.HeldKeys);
        AddHistoryLine(holdHistoryLines, $"{entry.Timestamp:T}  {combo}");
        AddHistoryLine(diagnosticHistoryLines, $"{entry.Timestamp:T}  Held {combo}.");
        RefreshHistoryList();
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
            LoadReadMeToUi();
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

    private void DiagnosticsModeChanged(object sender, RoutedEventArgs e)
    {
        RefreshHistoryList();
    }

    private void RefreshHistoryList()
    {
        var isDiagnosticsMode = DiagnosticsModeBox.IsChecked == true;
        var source = isDiagnosticsMode ? diagnosticHistoryLines : holdHistoryLines;
        HistoryDescriptionText.Text = isDiagnosticsMode
            ? "Diagnostics shows trigger, release, capture, settings, and hook messages for troubleshooting."
            : "Only key combinations RunHold actually held are shown here.";

        HistoryList.Items.Clear();
        foreach (var line in source)
        {
            HistoryList.Items.Add(line);
        }
    }

    private static void AddHistoryLine(List<string> lines, string line)
    {
        lines.Insert(0, line);
        while (lines.Count > MaxHistoryRows)
        {
            lines.RemoveAt(lines.Count - 1);
        }
    }

    private static string FormatHoldCombo(IEnumerable<int> heldKeys)
    {
        return string.Join(" + ", heldKeys
            .OrderBy(GetHoldDisplayGroup)
            .ThenBy(GetHoldDisplayOrder)
            .Select(FormatHoldKeyName));
    }

    private static string FormatHoldKeyName(int virtualKey)
    {
        var name = VirtualKeyNames.GetName(virtualKey);
        return string.Equals(name, "Space", StringComparison.Ordinal) ? "Space bar" : name;
    }

    private static int GetHoldDisplayGroup(int virtualKey)
    {
        return virtualKey switch
        {
            0x10 or 0x11 or 0x12 => 0,
            >= 0x30 and <= 0x5A => 1,
            0x20 => 2,
            _ => 3
        };
    }

    private static int GetHoldDisplayOrder(int virtualKey)
    {
        return virtualKey switch
        {
            0x11 => 0,
            0x10 => 1,
            0x12 => 2,
            _ => virtualKey
        };
    }

    private void LoadReadMeToUi()
    {
        var path = Path.Combine(AppContext.BaseDirectory, "Content", "ReadMe.md");
        var markdown = File.Exists(path)
            ? File.ReadAllText(path)
            : "## RunHold\n\nRunHold keeps held movement keys down until you stop it.";

        ReadMeVersionText.Text = $"Version {GetAppVersion()}";
        ReadMeViewer.Document = BuildReadMeDocument(RemoveReadMeTitle(markdown));
    }

    private FlowDocument BuildReadMeDocument(string markdown)
    {
        var document = new FlowDocument
        {
            Background = FindBrush("AppBackgroundBrush"),
            Foreground = FindBrush("TextBrush"),
            FontFamily = FontFamily,
            FontSize = 14,
            PagePadding = new Thickness(0, 0, 35, 0)
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

    private static string RemoveReadMeTitle(string markdown)
    {
        var normalized = markdown.Replace("\r\n", "\n");
        if (normalized.StartsWith("# ", StringComparison.Ordinal))
        {
            var lineBreak = normalized.IndexOf('\n');
            if (lineBreak > 0)
            {
                return normalized[lineBreak..].TrimStart();
            }
        }

        return normalized;
    }

    private static string GetAppVersion()
    {
        var informationalVersion = typeof(MainWindow).Assembly
            .GetCustomAttribute<AssemblyInformationalVersionAttribute>()
            ?.InformationalVersion;
        if (!string.IsNullOrWhiteSpace(informationalVersion))
        {
            var sourceRevisionStart = informationalVersion.IndexOf('+', StringComparison.Ordinal);
            return sourceRevisionStart > 0
                ? informationalVersion[..sourceRevisionStart]
                : informationalVersion;
        }

        return typeof(MainWindow).Assembly.GetName().Version?.ToString(2) ?? "1.11";
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
