using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Controller.Persistence;

namespace Jellyfin.Plugin.Jellyfin2PopFeed;

/// <summary>
/// Registers plugin services with Jellyfin's dependency injection container.
/// </summary>
public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection)
    {
        // Services
        serviceCollection.AddSingleton<IAtProtocolService, Services.AtProtocolService>();
        serviceCollection.AddSingleton<IPopFeedService, Services.PopFeedService>();
        serviceCollection.AddHostedService<Services.PlaybackMonitorService>();

        // HTTP Client for AT Protocol
        serviceCollection.AddHttpClient<IAtProtocolService, Services.AtProtocolService>(client =>
        {
            client.Timeout = TimeSpan.FromSeconds(30);
            client.DefaultRequestHeaders.Add("Accept", "application/json");
        });

        // Web Configuration
        serviceCollection.AddSingleton<IPluginConfigurationPage, Web.Configuration.PopFeedConfigPage>();
    }
}
