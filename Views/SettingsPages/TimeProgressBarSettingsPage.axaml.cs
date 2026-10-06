using ClassIsland.Core.Abstractions.Controls;
using ClassIsland.Core.Attributes;
using ClassIsland.TimeProgressBar.Models;

namespace ClassIsland.TimeProgressBar.Views.SettingsPages;

[SettingsPageInfo("classisland.time-progress-bar", "时间点进度条", "", "")]
public partial class TimeProgressBarSettingsPage : SettingsPageBase
{
    public TimeProgressBarSettingsPage(TimeProgressBarSettings settings)
    {
        InitializeComponent();
        DataContext = settings;
    }
}
