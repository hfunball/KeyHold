using System.Windows;
using KeyHold.Models;
using KeyHold.Services;

namespace KeyHold;

public partial class App
{
    private NotifyIconHost? notifyIconHost;
    private KeyboardHookService? keyboardHook;
    private MouseHookService? mouseHook;
    private KeyHoldEngine? engine;
    private MainWindow? mainWindow;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        var config = new ConfigService();
        var settings = config.Load();
        ThemeService.Apply(settings.Theme);

        var sender = new SendInputService();
        engine = new KeyHoldEngine(settings, sender);
        keyboardHook = new KeyboardHookService(engine);
        mouseHook = new MouseHookService(engine);
        notifyIconHost = new NotifyIconHost(engine, ShowMainWindow, ExitApplication);
        mainWindow = new MainWindow(settings, config, engine, new StartupService());

        keyboardHook.Start();
        mouseHook.Start();
        notifyIconHost.Update(engine.Status);

        engine.StatusChanged += (_, status) =>
        {
            Dispatcher.Invoke(() =>
            {
                notifyIconHost.Update(status);
                mainWindow?.UpdateStatus(status);
            });
        };

        engine.DiagnosticLogged += (_, entry) =>
        {
            Dispatcher.Invoke(() => mainWindow?.AddDiagnostic(entry));
        };

        if (settings.LaunchToTray)
        {
            mainWindow.Hide();
            return;
        }

        ShowMainWindow();
    }

    protected override void OnExit(ExitEventArgs e)
    {
        engine?.ReleaseAll("Application exit");
        keyboardHook?.Dispose();
        mouseHook?.Dispose();
        notifyIconHost?.Dispose();
        base.OnExit(e);
    }

    private void ShowMainWindow()
    {
        if (mainWindow is null)
        {
            return;
        }

        mainWindow.Show();
        mainWindow.WindowState = WindowState.Normal;
        mainWindow.Activate();
    }

    private void ExitApplication()
    {
        mainWindow?.AllowClose();
        Shutdown();
    }
}
