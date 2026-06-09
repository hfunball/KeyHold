using System.Windows;
using System.Windows.Media.Animation;
using System.Windows.Threading;

namespace RunHold;

public partial class StartupSplashWindow
{
    private static readonly TimeSpan DisplayDuration = TimeSpan.FromSeconds(3);

    public StartupSplashWindow()
    {
        InitializeComponent();
        Loaded += OnLoaded;
    }

    private void OnLoaded(object sender, RoutedEventArgs e)
    {
        PositionNearNotificationArea();
        DismissProgress.BeginAnimation(
            System.Windows.Controls.Primitives.RangeBase.ValueProperty,
            new DoubleAnimation(100, 0, DisplayDuration));

        var timer = new DispatcherTimer
        {
            Interval = DisplayDuration
        };
        timer.Tick += (_, _) =>
        {
            timer.Stop();
            Close();
        };
        timer.Start();
    }

    private void PositionNearNotificationArea()
    {
        const double margin = 16;
        var workArea = SystemParameters.WorkArea;
        Left = workArea.Right - Width - margin;
        Top = workArea.Bottom - Height - margin;
    }
}
