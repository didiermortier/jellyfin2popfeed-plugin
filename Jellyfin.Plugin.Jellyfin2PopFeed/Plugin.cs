using System;
using System.Collections.Generic;
using MediaBrowser.Common.Configuration;
using MediaBrowser.Common.Plugins;
using MediaBrowser.Model.Plugins;
using MediaBrowser.Model.Serialization;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

namespace Jellyfin.Plugin.Jellyfin2PopFeed;

/// <summary>
/// Main plugin class for Jellyfin2PopFeed integration.
/// Posts watched movies to PopFeed via AT Protocol.
/// </summary>
public class Plugin : BasePlugin<PluginConfiguration>, IHasWebPages
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    public static Plugin? Instance { get; private set; }

    public override string Name => "Jellyfin2PopFeed";

    public override Guid Id => Guid.Parse("5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5");

    public override string Description => "Automatically post watched movies to PopFeed via AT Protocol";

    public IEnumerable<PluginPageInfo> GetPages()
    {
        return new[]
        {
            new PluginPageInfo
            {
                Name = "jellyfin2popfeed",
                EmbeddedResourcePath = GetType().Namespace + ".Web.Configuration.html",
            },
            new PluginPageInfo
            {
                Name = "jellyfin2popfeedjs",
                EmbeddedResourcePath = GetType().Namespace + ".Web.Configuration.js",
            }
        };
    }
}