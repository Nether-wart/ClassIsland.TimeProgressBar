using Avalonia;
using Avalonia.Controls;
using Avalonia.Layout;
using Avalonia.Media;
using Avalonia.Platform;
using Avalonia.Threading;
using ClassIsland.Core;
using ClassIsland.Core.Abstractions.Services;
using ClassIsland.Platforms.Abstraction;
using ClassIsland.Platforms.Abstraction.Enums;
using ClassIsland.Shared.Enums;
using ClassIsland.TimeProgressBar.Models;
using ClassIsland.TimeProgressBar.Views;

namespace ClassIsland.TimeProgressBar.Services;

/// <summary>
/// 驱动屏幕顶部的时间点进度条窗口：按固定频率刷新进度、颜色、位置与可见性。
/// </summary>
public class TimeProgressBarService : IDisposable
{
    private static readonly TimeSpan RefreshInterval = TimeSpan.FromMilliseconds(100);
    private const int TopmostReassertTicks = 10;

    private readonly ILessonsService _lessonsService;
    private readonly IExactTimeService _exactTimeService;
    private readonly TimeProgressBarSettings _settings;

    private ProgressBarWindow? _window;
    private DispatcherTimer? _timer;
    private bool _started;
    private bool _isBarVisible;

    private int? _appliedThickness;
    private int _ticks;
    private double? _appliedWidth;
    private PixelPoint? _appliedPosition;
    private ProgressBarFlowDirection? _appliedDirection;
    private double? _appliedBarLength;
    private Color? _appliedColor;

    public TimeProgressBarService(ILessonsService lessonsService, IExactTimeService exactTimeService,
        TimeProgressBarSettings settings)
    {
        _lessonsService = lessonsService;
        _exactTimeService = exactTimeService;
        _settings = settings;
    }

    /// <summary>
    /// 创建进度条窗口并启动刷新计时器。必须在应用启动完成后调用。
    /// </summary>
    public void Start()
    {
        if (_started)
        {
            return;
        }
        _started = true;
        if (Dispatcher.UIThread.CheckAccess())
        {
            StartOnUIThread();
        }
        else
        {
            Dispatcher.UIThread.Post(StartOnUIThread);
        }
    }

    private void StartOnUIThread()
    {
        _window = new ProgressBarWindow();
        _timer = new DispatcherTimer(RefreshInterval, DispatcherPriority.Background, OnTick);
        _timer.Start();
        OnTick(_timer, EventArgs.Empty);
    }

    private void OnTick(object? sender, EventArgs e)
    {
        if (_window is null)
        {
            return;
        }

        if (!TryGetProgress(out var ratio, out var color))
        {
            HideBar();
            return;
        }

        if (_settings.OnlyForegroundMaximizedOrFullscreen && !IsForegroundMaximizedOrFullscreen())
        {
            HideBar();
            return;
        }

        ShowBar(ratio, color);
    }

    /// <summary>
    /// 计算当前时间点的剩余时间占比与应使用的颜色。当前不处于任何时间点时返回 false。
    /// </summary>
    private bool TryGetProgress(out double ratio, out Color color)
    {
        ratio = 0;
        color = default;

        if (!_settings.IsEnabled)
        {
            return false;
        }

        var lessons = _lessonsService;
        if (!lessons.IsLessonConfirmed ||
            lessons.CurrentState is not (TimeState.OnClass or TimeState.Breaking))
        {
            return false;
        }

        var timeLayoutItem = lessons.CurrentTimeLayoutItem;
        if (timeLayoutItem is null)
        {
            return false;
        }

        var total = timeLayoutItem.Last;
        if (total <= TimeSpan.Zero)
        {
            return false;
        }

        var now = _exactTimeService.GetCurrentLocalDateTime().TimeOfDay;
        if (now < timeLayoutItem.StartTime || now > timeLayoutItem.EndTime)
        {
            return false;
        }

        var remaining = timeLayoutItem.EndTime - now;
        ratio = Math.Clamp(remaining.TotalMilliseconds / total.TotalMilliseconds, 0d, 1d);
        color = remaining <= TimeSpan.FromSeconds(_settings.WarningThresholdSeconds)
            ? _settings.WarningColor
            : _settings.BarColor;
        return true;
    }

    private bool IsForegroundMaximizedOrFullscreen()
    {
        var screen = GetTargetScreen();
        if (screen is null)
        {
            return false;
        }
        var windowPlatformService = PlatformServices.WindowPlatformService;
        return windowPlatformService.IsForegroundWindowFullscreen(screen) ||
               windowPlatformService.IsForegroundWindowMaximized(screen);
    }

    /// <summary>
    /// 获取进度条所在屏幕：优先使用主界面所在的屏幕，否则使用主屏幕。
    /// </summary>
    private Screen? GetTargetScreen()
    {
        if (_window is not { } window)
        {
            return null;
        }
        var mainWindow = AppBase.Current?.MainWindow;
        var screen = mainWindow is { IsVisible: true }
            ? window.Screens.ScreenFromWindow(mainWindow)
            : null;
        return screen ?? window.Screens.Primary;
    }

    private void ShowBar(double ratio, Color color)
    {
        if (_window is not { } window)
        {
            return;
        }

        var screen = GetTargetScreen();
        if (screen is null)
        {
            HideBar();
            return;
        }

        var thickness = Math.Clamp(_settings.Thickness, 1, 50);
        var width = screen.Bounds.Width / screen.Scaling;
        var position = new PixelPoint(screen.Bounds.X, screen.Bounds.Y);
        var direction = _settings.FlowDirection;
        var barLength = width * ratio;

        if (!_isBarVisible)
        {
            // 重新显示时强制恢复窗口几何状态，避免窗口管理器保留上次的显示状态
            _appliedThickness = null;
            _appliedWidth = null;
            _appliedPosition = null;
            _appliedDirection = null;
            _appliedBarLength = null;
            _appliedColor = null;
        }

        if (_appliedThickness != thickness)
        {
            window.Height = thickness;
            window.BarControl.Height = thickness;
            _appliedThickness = thickness;
        }
        if (_appliedWidth != width)
        {
            window.Width = width;
            _appliedWidth = width;
        }
        if (_appliedPosition != position)
        {
            window.Position = position;
            _appliedPosition = position;
        }
        if (_appliedDirection != direction)
        {
            window.BarControl.HorizontalAlignment = direction == ProgressBarFlowDirection.LeftToRight
                ? HorizontalAlignment.Right
                : HorizontalAlignment.Left;
            _appliedDirection = direction;
        }
        if (_appliedBarLength != barLength)
        {
            window.BarControl.Width = barLength;
            _appliedBarLength = barLength;
        }
        if (_appliedColor != color)
        {
            window.BarControl.Background = new SolidColorBrush(color);
            _appliedColor = color;
        }

        if (!_isBarVisible)
        {
            window.Show();
            _isBarVisible = true;
        }

        // 周期性重新声明置顶，防止被其它置顶窗口（如全屏应用）压到下层
        _ticks++;
        if (_ticks % TopmostReassertTicks == 0)
        {
            PlatformServices.WindowPlatformService.SetWindowFeature(window, WindowFeatures.Topmost, true);
        }
    }

    private void HideBar()
    {
        if (!_isBarVisible || _window is null)
        {
            return;
        }
        _window.Hide();
        _isBarVisible = false;
    }

    public void Dispose()
    {
        _timer?.Stop();
        HideBar();
        _window?.Close();
        _window = null;
        GC.SuppressFinalize(this);
    }
}
