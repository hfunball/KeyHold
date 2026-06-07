using System.Windows.Media;
using KeyHold.Models;
using Microsoft.Win32;
using AppThemeMode = KeyHold.Models.ThemeMode;

namespace KeyHold.Services;

public static class ThemeService
{
    public static void Apply(AppThemeMode mode)
    {
        if (System.Windows.Application.Current is not { } application)
        {
            return;
        }

        var resolved = mode == AppThemeMode.System ? ReadSystemTheme() : mode;
        SetColor(application, "AppBackgroundBrush", resolved == AppThemeMode.Dark ? "#101318" : "#F5F7FB");
        SetColor(application, "PanelBrush", resolved == AppThemeMode.Dark ? "#171B22" : "#FFFFFF");
        SetColor(application, "ControlBrush", resolved == AppThemeMode.Dark ? "#1D232C" : "#FFFFFF");
        SetColor(application, "ControlHoverBrush", resolved == AppThemeMode.Dark ? "#242C37" : "#EAF2FB");
        SetColor(application, "ControlSelectedBrush", resolved == AppThemeMode.Dark ? "#213B4F" : "#DCEEFF");
        SetColor(application, "StatusIdleBrush", resolved == AppThemeMode.Dark ? "#29313D" : "#E5ECF5");
        SetColor(application, "TextBrush", resolved == AppThemeMode.Dark ? "#F5F7FA" : "#161A20");
        SetColor(application, "OnAccentBrush", "#FFFFFF");
        SetColor(application, "MutedTextBrush", resolved == AppThemeMode.Dark ? "#A9B2C3" : "#5E6878");
        SetColor(application, "AccentBrush", resolved == AppThemeMode.Dark ? "#35A7FF" : "#006DD9");
        SetColor(application, "BorderBrush", resolved == AppThemeMode.Dark ? "#29313D" : "#D6DDE8");
        SetColor(application, "DangerBrush", "#D94444");
    }

    private static AppThemeMode ReadSystemTheme()
    {
        using var key = Registry.CurrentUser.OpenSubKey(@"Software\Microsoft\Windows\CurrentVersion\Themes\Personalize");
        return key?.GetValue("AppsUseLightTheme") is int value && value == 1 ? AppThemeMode.Light : AppThemeMode.Dark;
    }

    private static void SetColor(System.Windows.Application application, string resourceKey, string hex)
    {
        application.Resources[resourceKey] =
            new SolidColorBrush((System.Windows.Media.Color)System.Windows.Media.ColorConverter.ConvertFromString(hex));
    }
}
