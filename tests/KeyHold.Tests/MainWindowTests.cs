using System.IO;
using System.Reflection;
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
    private const int PageUp = 0x21;
    private const int PageDown = 0x22;

    [STATestMethod]
    public void DefaultSettings_ShowHomeToggleOnly()
    {
        var window = CreateWindow(new AppSettings());
        try
        {
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "EnableBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "StopBindingPanel").Visibility);
            Assert.AreEqual("Toggle key", Find<TextBlock>(window, "EnableBindingLabel").Text);
            Assert.AreEqual("Home", Find<TextBox>(window, "EnableBindingText").Text);
            Assert.AreEqual("Set Toggle Key", Find<Button>(window, "CaptureEnableButton").Content);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void ToggleMode_ShowsOneKeyboardTrigger()
    {
        var window = CreateWindow(new AppSettings { ActivationMode = ActivationMode.Toggle });
        try
        {
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "EnableBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "StopBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "MouseBindingPanel").Visibility);
            Assert.AreEqual("Toggle key", Find<TextBlock>(window, "EnableBindingLabel").Text);
            Assert.AreEqual("Set Toggle Key", Find<Button>(window, "CaptureEnableButton").Content);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void ActivationModeChangeToToggle_SavesAndHidesStopKey()
    {
        var settings = new AppSettings
        {
            ActivationMode = ActivationMode.SeparateKeys,
            EnableBinding = InputBinding.Keyboard(PageUp),
            StopBinding = InputBinding.Keyboard(PageDown)
        };
        var window = CreateWindow(settings);
        try
        {
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "StopBindingPanel").Visibility);

            SelectComboItem(Find<ComboBox>(window, "ActivationModeBox"), "Toggle");
            InvokePrivate(window, "SaveSettingsFromUi");

            Assert.AreEqual(ActivationMode.Toggle, settings.ActivationMode);
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "EnableBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "StopBindingPanel").Visibility);
            Assert.AreEqual("Toggle key", Find<TextBlock>(window, "EnableBindingLabel").Text);
            Assert.AreEqual("Set Toggle Key", Find<Button>(window, "CaptureEnableButton").Content);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void MouseTriggerMode_ShowsMouseTriggerInsteadOfKeyboardTriggers()
    {
        var window = CreateWindow(new AppSettings { ActivationMode = ActivationMode.MouseTrigger });
        try
        {
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "EnableBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Collapsed, Find<FrameworkElement>(window, "StopBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "MouseBindingPanel").Visibility);
            Assert.AreEqual(Visibility.Visible, Find<FrameworkElement>(window, "EmergencyBindingPanel").Visibility);
        }
        finally
        {
            Close(window);
        }
    }

    [STATestMethod]
    public void CaptureButtonShowsPromptAndStoresKey()
    {
        var window = CreateWindow(new AppSettings { ActivationMode = ActivationMode.Toggle });
        try
        {
            var button = Find<Button>(window, "CaptureEnableButton");

            button.RaiseEvent(new RoutedEventArgs(Button.ClickEvent));

            Assert.AreEqual("Press a key...", Find<TextBox>(window, "EnableBindingText").Text);
            Assert.AreEqual("Listening...", button.Content);

            window.CompleteKeyCaptureForTest(A);

            Assert.AreEqual("A", Find<TextBox>(window, "EnableBindingText").Text);
            Assert.AreEqual("Set Toggle Key", button.Content);
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

    private static void SelectComboItem(ComboBox comboBox, string tag)
    {
        foreach (ComboBoxItem item in comboBox.Items)
        {
            if (string.Equals((string)item.Tag, tag, StringComparison.OrdinalIgnoreCase))
            {
                comboBox.SelectedItem = item;
                return;
            }
        }

        Assert.Fail($"Expected combo item with tag {tag}.");
    }

    private static void InvokePrivate(MainWindow window, string methodName)
    {
        var method = typeof(MainWindow).GetMethod(methodName, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.IsNotNull(method, $"Expected private method {methodName} to exist.");
        method.Invoke(window, null);
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
