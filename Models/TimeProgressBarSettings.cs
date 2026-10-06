using System.ComponentModel;
using System.Runtime.CompilerServices;
using System.Text.Json;
using System.Text.Json.Serialization;
using Avalonia.Media;

namespace ClassIsland.TimeProgressBar.Models;

/// <summary>
/// 进度条的流逝方向。
/// </summary>
public enum ProgressBarFlowDirection
{
    /// <summary>
    /// 空白自左侧吞没进度条，剩余进度条贴屏幕右边缘。
    /// </summary>
    LeftToRight,

    /// <summary>
    /// 空白自右侧吞没进度条，剩余进度条贴屏幕左边缘。
    /// </summary>
    RightToLeft
}

/// <summary>
/// 插件设置。任意设置项被修改后立即保存到插件配置目录。
/// </summary>
public class TimeProgressBarSettings : INotifyPropertyChanged
{
    public const string SettingsFileName = "settings.json";
    public const string DefaultBarColorHex = "#FF2563EB";
    public const string DefaultWarningColorHex = "#FFE11D48";

    private static readonly Color DefaultBarColor = new(0xFF, 0x25, 0x63, 0xEB);
    private static readonly Color DefaultWarningColor = new(0xFF, 0xE1, 0x1D, 0x48);

    private static readonly JsonSerializerOptions SerializerOptions = new()
    {
        WriteIndented = true,
        Converters = { new JsonStringEnumConverter() }
    };

    private bool _loading = true;
    private string _configPath = "";

    private bool _isEnabled = true;
    private int _thickness = 6;
    private ProgressBarFlowDirection _flowDirection = ProgressBarFlowDirection.LeftToRight;
    private string _barColorHex = DefaultBarColorHex;
    private string _warningColorHex = DefaultWarningColorHex;
    private int _warningThresholdSeconds = 30;
    private bool _onlyForegroundMaximizedOrFullscreen;

    private Color _barColor = DefaultBarColor;
    private Color _warningColor = DefaultWarningColor;

    public event PropertyChangedEventHandler? PropertyChanged;

    /// <summary>
    /// 是否启用进度条。
    /// </summary>
    public bool IsEnabled
    {
        get => _isEnabled;
        set => Set(ref _isEnabled, value);
    }

    /// <summary>
    /// 进度条宽度（粗细），以逻辑像素为单位。
    /// </summary>
    public int Thickness
    {
        get => _thickness;
        set => Set(ref _thickness, Math.Clamp(value, 1, 50));
    }

    /// <summary>
    /// 进度条流逝方向。
    /// </summary>
    public ProgressBarFlowDirection FlowDirection
    {
        get => _flowDirection;
        set => Set(ref _flowDirection, value);
    }

    /// <summary>
    /// 进度条常规颜色，以 #AARRGGBB 表示。
    /// </summary>
    public string BarColorHex
    {
        get => _barColorHex;
        set
        {
            if (!TryParseColor(value, out var color) || !Set(ref _barColorHex, value, save: false))
            {
                return;
            }
            _barColor = color;
            RaisePropertyChanged(nameof(BarColor));
            Save();
        }
    }

    /// <summary>
    /// 进度条常规颜色。
    /// </summary>
    [JsonIgnore]
    public Color BarColor
    {
        get => _barColor;
        set => BarColorHex = ToHex(value);
    }

    /// <summary>
    /// 距时间点结束不足 <see cref="WarningThresholdSeconds"/> 秒时使用的颜色，以 #AARRGGBB 表示。
    /// </summary>
    public string WarningColorHex
    {
        get => _warningColorHex;
        set
        {
            if (!TryParseColor(value, out var color) || !Set(ref _warningColorHex, value, save: false))
            {
                return;
            }
            _warningColor = color;
            RaisePropertyChanged(nameof(WarningColor));
            Save();
        }
    }

    /// <summary>
    /// 警示颜色。
    /// </summary>
    [JsonIgnore]
    public Color WarningColor
    {
        get => _warningColor;
        set => WarningColorHex = ToHex(value);
    }

    /// <summary>
    /// 切换到警示颜色的剩余时间阈值，以秒为单位。
    /// </summary>
    public int WarningThresholdSeconds
    {
        get => _warningThresholdSeconds;
        set => Set(ref _warningThresholdSeconds, Math.Clamp(value, 0, 3600));
    }

    /// <summary>
    /// 是否仅当前台窗口处于全屏或最大化状态时显示进度条。
    /// </summary>
    public bool OnlyForegroundMaximizedOrFullscreen
    {
        get => _onlyForegroundMaximizedOrFullscreen;
        set => Set(ref _onlyForegroundMaximizedOrFullscreen, value);
    }

    /// <summary>
    /// 以索引表示的流逝方向，供设置页面的 <c>ComboBox.SelectedIndex</c> 绑定使用。
    /// </summary>
    [JsonIgnore]
    public int FlowDirectionIndex
    {
        get => (int)_flowDirection;
        set => FlowDirection = (ProgressBarFlowDirection)Math.Clamp(value, 0, 1);
    }

    /// <summary>
    /// 从插件配置目录读取设置；文件不存在或损坏时使用默认设置。
    /// </summary>
    public static TimeProgressBarSettings Load(string folder)
    {
        TimeProgressBarSettings? settings = null;
        var path = Path.Combine(folder, SettingsFileName);
        try
        {
            if (File.Exists(path))
            {
                settings = JsonSerializer.Deserialize<TimeProgressBarSettings>(File.ReadAllText(path), SerializerOptions);
            }
        }
        catch (Exception)
        {
            // 设置文件损坏时回退到默认设置
        }

        settings ??= new TimeProgressBarSettings();
        settings._configPath = path;
        settings._loading = false;
        settings._barColor = ResolveColor(settings._barColorHex, DefaultBarColor);
        settings._warningColor = ResolveColor(settings._warningColorHex, DefaultWarningColor);
        return settings;
    }

    private void Save()
    {
        if (_loading || _configPath.Length == 0)
        {
            return;
        }
        try
        {
            File.WriteAllText(_configPath, JsonSerializer.Serialize(this, SerializerOptions));
        }
        catch (Exception)
        {
            // 忽略保存失败，避免影响主程序运行
        }
    }

    private bool Set<T>(ref T field, T value, [CallerMemberName] string? propertyName = null, bool save = true)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return false;
        }
        field = value;
        RaisePropertyChanged(propertyName);
        if (save)
        {
            Save();
        }
        return true;
    }

    private void RaisePropertyChanged([CallerMemberName] string? propertyName = null) =>
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));

    private static string ToHex(Color color) => $"#{color.A:X2}{color.R:X2}{color.G:X2}{color.B:X2}";

    private static bool TryParseColor(string? text, out Color color)
    {
        if (!string.IsNullOrWhiteSpace(text) && Color.TryParse(text, out var parsed))
        {
            color = parsed;
            return true;
        }
        color = default;
        return false;
    }

    private static Color ResolveColor(string? text, Color fallback) =>
        TryParseColor(text, out var color) && color.A != 0 ? color : fallback;
}
