using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using KeyHold.Models;

namespace KeyHold.Services;

public sealed class NotifyIconHost : IDisposable
{
    private readonly KeyHoldEngine engine;
    private readonly Action showWindow;
    private readonly Action exitApplication;
    private readonly NotifyIcon notifyIcon;
    private Icon? idleIcon;
    private Icon? activeIcon;

    public NotifyIconHost(KeyHoldEngine engine, Action showWindow, Action exitApplication)
    {
        this.engine = engine;
        this.showWindow = showWindow;
        this.exitApplication = exitApplication;
        idleIcon = CreateIcon(Color.FromArgb(0x35, 0xA7, 0xFF), false);
        activeIcon = CreateIcon(Color.FromArgb(0x58, 0xD6, 0x8D), true);

        notifyIcon = new NotifyIcon
        {
            Text = "KeyHold",
            Icon = idleIcon,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        notifyIcon.DoubleClick += (_, _) => showWindow();
    }

    public void Update(HoldStatus status)
    {
        notifyIcon.Text = status.IsActive ? "KeyHold: holding keys" : "KeyHold: idle";
        notifyIcon.Icon = status.IsActive ? activeIcon : idleIcon;
    }

    public void Dispose()
    {
        notifyIcon.Visible = false;
        notifyIcon.Dispose();
        idleIcon?.Dispose();
        activeIcon?.Dispose();
    }

    private ContextMenuStrip BuildMenu()
    {
        var menu = new ContextMenuStrip();
        menu.Items.Add("Open KeyHold", null, (_, _) => showWindow());
        menu.Items.Add("Release All", null, (_, _) => engine.ReleaseAll("Tray release"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exitApplication());
        return menu;
    }

    private static Icon CreateIcon(Color accent, bool active)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        using var bgBrush = new SolidBrush(Color.FromArgb(0x17, 0x1B, 0x22));
        using var accentBrush = new SolidBrush(accent);
        using var keyBrush = new SolidBrush(Color.White);
        using var textBrush = new SolidBrush(Color.White);
        using var pen = new Pen(accent, 2.4f);

        graphics.FillRoundedRectangle(bgBrush, new RectangleF(2, 2, 28, 28), 7);
        graphics.DrawRoundedRectangle(pen, new RectangleF(3.5f, 3.5f, 25, 25), 6);
        graphics.FillRoundedRectangle(accentBrush, new RectangleF(8, 10, 14, 5), 2);
        graphics.FillRoundedRectangle(accentBrush, new RectangleF(19, 13, 5, 10), 2);
        graphics.FillRoundedRectangle(keyBrush, new RectangleF(8, 18, 13, 10), 3);

        if (active)
        {
            graphics.FillEllipse(textBrush, 21, 22, 4, 4);
        }

        var handle = bitmap.GetHicon();
        try
        {
            using var icon = Icon.FromHandle(handle);
            return (Icon)icon.Clone();
        }
        finally
        {
            DestroyIcon(handle);
        }
    }

    [DllImport("user32.dll", SetLastError = true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    private static extern bool DestroyIcon(IntPtr handle);
}
