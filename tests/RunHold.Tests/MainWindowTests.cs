using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Documents;
using RunHold.Models;
using RunHold.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace RunHold.Tests;

[TestClass]
public sealed class MainWindowTests
{
    private const int A = 0x41;
    private const int W = 0x57;
    private const int Space = 0x20;

    [STATestMethod]
    public void DefaultSettings_ShowHomeToggleOnly()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            Assert.AreEqual("Home", Find<TextBox>(window, "ToggleBindingText").Text);
            Assert.AreEqual("Set Toggle Trigger", Find<Button>(window, "CaptureToggleButton").Content);
            Assert.IsFalse(Find<CheckBox>(window, "StopOnAnyKeyBox").IsChecked == true);
            var readMeViewer = Find<FlowDocumentScrollViewer>(window, "ReadMeViewer");
            Assert.AreEqual(new Thickness(0, 0, 35, 0), readMeViewer.Document.PagePadding);
            Assert.AreEqual("Version 1.1", Find<TextBlock>(window, "ReadMeVersionText").Text);
            Assert.IsFalse(GetDocumentText(readMeViewer.Document).Contains("# RunHold", StringComparison.Ordinal));
            Assert.IsNull(window.FindName(string.Concat("Activation", "ModeBox")));
            Assert.IsNull(window.FindName(string.Concat("Stop", "BindingPanel")));
            Assert.IsNull(window.FindName(string.Concat("Mouse", "BindingPanel")));
            Assert.IsNull(window.FindName(string.Concat("Emer", "gencyBindingPanel")));
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void CaptureButtonShowsPromptAndStoresKey()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            var button = Find<Button>(window, "CaptureToggleButton");

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual("Press key or mouse...", Find<TextBox>(window, "ToggleBindingText").Text);
            Assert.AreEqual("Listening...", button.Content);

            window.CompleteKeyCaptureForTest(A);

            Assert.AreEqual("A", Find<TextBox>(window, "ToggleBindingText").Text);
            Assert.AreEqual("Set Toggle Trigger", button.Content);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void CaptureCanStoreMouseButton()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            var button = Find<Button>(window, "CaptureToggleButton");

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
            window.CompleteMouseCaptureForTest(MouseTriggerCode.XButton1);

            Assert.AreEqual("Mouse Button 4", Find<TextBox>(window, "ToggleBindingText").Text);
            Assert.AreEqual("Set Toggle Trigger", button.Content);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void HistoryShowsHeldCombosByDefaultAndDiagnosticsWhenEnabled()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            var timestamp = new DateTime(2026, 6, 8, 14, 41, 0);

            window.AddDiagnostic(new DiagnosticEntry(timestamp, "Toggle trigger pressed: Home."));
            window.AddHoldHistory(new HoldHistoryEntry(timestamp, new[] { W, Space }));

            var historyList = Find<ListBox>(window, "HistoryList");
            Assert.AreEqual(1, historyList.Items.Count);
            StringAssert.Contains(historyList.Items[0]?.ToString(), "W + Space bar");

            var diagnosticsBox = Find<CheckBox>(window, "DiagnosticsModeBox");
            Assert.IsFalse(diagnosticsBox.IsChecked == true);
            diagnosticsBox.IsChecked = true;
            diagnosticsBox.RaiseEvent(new RoutedEventArgs(System.Windows.Controls.Primitives.ToggleButton.CheckedEvent));

            Assert.AreEqual(2, historyList.Items.Count);
            StringAssert.Contains(historyList.Items[0]?.ToString(), "Held W + Space bar.");
            StringAssert.Contains(historyList.Items[1]?.ToString(), "Toggle trigger pressed: Home.");
        }
        finally
        {
            Close(window);
        }
    }

    private static MainWindow CreateWindow(AppSettings settings)
    {
        var path = Path.Combine(Path.GetTempPath(), "RunHold.Tests", $"{Guid.NewGuid():N}.json");
        var engine = new RunHoldEngine(settings, new RecordingInputSender());
        return new MainWindow(settings, new ConfigService(path), engine, new FakeStartupService());
    }

    private static T Find<T>(MainWindow window, string name)
        where T : FrameworkElement
    {
        var element = window.FindName(name);
        Assert.IsNotNull(element, $"Expected {name} to exist.");
        return (T)element;
    }

    private static string GetDocumentText(FlowDocument document)
    {
        var range = new TextRange(document.ContentStart, document.ContentEnd);
        return range.Text;
    }

    private static void Close(MainWindow window)
    {
        window.AllowClose();
        window.Close();
    }

    private sealed class FakeStartupService : IStartupService
    {
        public bool Enabled { get; private set; }

        public bool IsEnabled()
        {
            return Enabled;
        }

        public void SetEnabled(bool enabled)
        {
            Enabled = enabled;
        }
    }

    private sealed class RecordingInputSender : IInputSender
    {
        public void SendKeyDown(int virtualKey)
        {
        }

        public void SendKeyUp(int virtualKey)
        {
        }
    }
}
