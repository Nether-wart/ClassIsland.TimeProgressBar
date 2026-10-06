using ClassIsland.Core;
using ClassIsland.Core.Abstractions;
using ClassIsland.Core.Attributes;
using ClassIsland.Core.Extensions.Registry;
using ClassIsland.Shared;
using ClassIsland.TimeProgressBar.Models;
using ClassIsland.TimeProgressBar.Services;
using ClassIsland.TimeProgressBar.Views.SettingsPages;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

namespace ClassIsland.TimeProgressBar;

[PluginEntrance]
public class Plugin : PluginBase
{
    public override void Initialize(HostBuilderContext context, IServiceCollection services)
    {
        var settings = TimeProgressBarSettings.Load(PluginConfigFolder);

        services.AddSingleton(settings);
        services.AddSingleton<TimeProgressBarService>();
        services.AddSettingsPage<TimeProgressBarSettingsPage>();

        AppBase.Current.AppStarted += (_, _) => IAppHost.GetService<TimeProgressBarService>().Start();
    }
}
