using System.Drawing;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using RunHold.Models;

namespace RunHold.Services;

public sealed class NotifyIconHost : IDisposable
{
    private readonly RunHoldEngine engine;
    private readonly Action showWindow;
    private readonly Action exitApplication;
    private readonly NotifyIcon notifyIcon;
    private Icon? idleIcon;
    private Icon? activeIcon;

    public NotifyIconHost(RunHoldEngine engine, Action showWindow, Action exitApplication)
    {
        this.engine = engine;
        this.showWindow = showWindow;
        this.exitApplication = exitApplication;
        idleIcon = CreateIcon(active: false);
        activeIcon = CreateIcon(active: true);

        notifyIcon = new NotifyIcon
        {
            Text = "RunHold",
            Icon = idleIcon,
            Visible = true,
            ContextMenuStrip = BuildMenu()
        };

        notifyIcon.DoubleClick += (_, _) => showWindow();
        notifyIcon.MouseUp += (_, e) =>
        {
            if (e.Button == MouseButtons.Left)
            {
                showWindow();
            }
        };
    }

    public void Update(HoldStatus status)
    {
        notifyIcon.Text = status.IsActive ? "RunHold: holding keys" : "RunHold: idle";
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
        menu.Items.Add("Open RunHold", null, (_, _) => showWindow());
        menu.Items.Add("Release All", null, (_, _) => engine.ReleaseAll("Tray release"));
        menu.Items.Add(new ToolStripSeparator());
        menu.Items.Add("Exit", null, (_, _) => exitApplication());
        return menu;
    }

    private static Icon CreateIcon(bool active)
    {
        using var bitmap = new Bitmap(32, 32);
        using var graphics = Graphics.FromImage(bitmap);
        graphics.Clear(Color.Transparent);
        graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.AntiAlias;

        var borderColor = active ? Color.FromArgb(0x03, 0x04, 0x06) : Color.FromArgb(0x35, 0xA7, 0xFF);
        var keyColor = active ? Color.FromArgb(0x35, 0xA7, 0xFF) : Color.FromArgb(0x24, 0x29, 0x32);
        var keySideColor = active ? Color.FromArgb(0x1F, 0x8B, 0xF4) : Color.FromArgb(0x10, 0x13, 0x18);
        var railColor = active ? Color.FromArgb(0x03, 0x04, 0x06) : Color.FromArgb(0x35, 0xA7, 0xFF);

        using var bgBrush = new SolidBrush(Color.FromArgb(0x17, 0x1B, 0x22));
        using var keyBrush = new SolidBrush(keyColor);
        using var keySideBrush = new SolidBrush(keySideColor);
        using var railBrush = new SolidBrush(railColor);
        using var textBrush = new SolidBrush(Color.White);
        using var pen = new Pen(borderColor, 2.4f);

        graphics.FillRoundedRectangle(bgBrush, new RectangleF(2, 2, 28, 28), 7);
        graphics.DrawRoundedRectangle(pen, new RectangleF(3.5f, 3.5f, 25, 25), 6);
        graphics.FillRoundedRectangle(keyBrush, new RectangleF(8, 7, 16, 18), 4);
        graphics.FillPolygon(keySideBrush, [
            new PointF(9, 20),
            new PointF(23, 20),
            new PointF(21, 24),
            new PointF(11, 24)
        ]);
        graphics.FillRoundedRectangle(railBrush, new RectangleF(9, 22.5f, 14, 4.8f), 2.4f);
        graphics.FillRoundedRectangle(textBrush, new RectangleF(12, 14, 8, 2.5f), 1.25f);

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
