using System.IO;
using System.Windows;
using System.Windows.Controls;
using KeyHold.Models;
using KeyHold.Services;
using Microsoft.VisualStudio.TestTools.UnitTesting;

namespace KeyHold.Tests;

[TestClass]
public sealed class MainWindowTests
{
    private const int A = 0x41;

    [STATestMethod]
    public void DefaultSettings_ShowHomeToggleOnly()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            Assert.AreEqual("Home", Find<TextBox>(window, "ToggleBindingText").Text);
            Assert.AreEqual("Set Toggle Trigger", Find<Button>(window, "CaptureToggleButton").Content);
            Assert.IsFalse(Find<CheckBox>(window, "StopOnAnyKeyBox").IsChecked == true);
            Assert.IsNotNull(window.FindName("ReadMeViewer"));
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

    private static MainWindow CreateWindow(AppSettings settings)
    {
        var path = Path.Combine(Path.GetTempPath(), "KeyHold.Tests", $"{Guid.NewGuid():N}.json");
        var engine = new KeyHoldEngine(settings, new RecordingInputSender());
        return new MainWindow(settings, new ConfigService(path), engine, new FakeStartupService());
    }

    private static T Find<T>(MainWindow window, string name)
        where T : FrameworkElement
    {
        var element = window.FindName(name);
        Assert.IsNotNull(element, $"Expected {name} to exist.");
        return (T)element;
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
