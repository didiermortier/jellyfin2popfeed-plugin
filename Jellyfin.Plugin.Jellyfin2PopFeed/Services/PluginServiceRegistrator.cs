using MediaBrowser.Controller.Plugins;
using Microsoft.Extensions.DependencyInjection;
using MediaBrowser.Controller;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

public class PluginServiceRegistrator : IPluginServiceRegistrator
{
    public void RegisterServices(IServiceCollection serviceCollection, IServerApplicationHost appHost)
    {
        serviceCollection.AddHttpClient<IAtProtocolService, AtProtocolService>();
        serviceCollection.AddScoped<IPopFeedService, PopFeedService>();
        serviceCollection.AddHostedService<PlaybackMonitorService>();
    }
}