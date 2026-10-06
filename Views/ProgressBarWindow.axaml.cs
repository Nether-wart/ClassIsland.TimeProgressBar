using Avalonia.Controls;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Enums;

namespace ClassIsland.TimeProgressBar.Views;

/// <summary>
/// 屏幕顶部的时间点进度条窗口。
/// </summary>
public partial class ProgressBarWindow : Window
{
    public ProgressBarWindow()
    {
        InitializeComponent();
        TransparencyLevelHint = [WindowTransparencyLevel.Transparent];
        Closing += OnClosing;
    }

    /// <summary>
    /// 进度条主体。
    /// </summary>
    public Border BarControl => Bar;

    private static void OnClosing(object? sender, WindowClosingEventArgs e)
    {
        if (e.CloseReason is WindowCloseReason.OSShutdown or WindowCloseReason.ApplicationShutdown)
        {
            return;
        }
        e.Cancel = true;
    }

    public override void Show()
    {
        ShowActivated = false;
        ShowInTaskbar = false;
        Topmost = true;
        base.Show();
        PlatformServices.WindowPlatformService.SetWindowFeature(this,
            WindowFeatures.Transparent | WindowFeatures.ToolWindow | WindowFeatures.Topmost |
            WindowFeatures.SkipManagement, true);
    }
}
