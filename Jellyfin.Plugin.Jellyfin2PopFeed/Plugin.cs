using MediaBrowser.Common.Plugins;

namespace Jellyfin.Plugin.Jellyfin2PopFeed;

/// <summary>
/// Main plugin class for Jellyfin2 PopFeed integration.
/// </summary>
public class Plugin : BasePlugin<Configuration.PluginConfiguration>
{
    public Plugin(IApplicationPaths applicationPaths, IXmlSerializer xmlSerializer)
        : base(applicationPaths, xmlSerializer)
    {
        Instance = this;
    }

    /// <summary>
    /// Singleton instance of the plugin.
    /// </summary>
    public static Plugin Instance { get; private set; }

    /// <summary>
    /// Plugin name displayed in Jellyfin dashboard.
    /// </summary>
    public override string Name => "Jellyfin2 PopFeed";

    /// <summary>
    /// Unique plugin identifier.
    /// Generated: 5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5
    /// </summary>
    public override Guid Id => Guid.Parse("5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5");

    /// <summary>
    /// Plugin description.
    /// </summary>
    public override string Description => "Automatically post watched movies to PopFeed via AT Protocol";

    /// <summary>
    /// Plugin category in Jellyfin dashboard.
    /// </summary>
    public override string Category => "Social";

    /// <summary>
    /// Target Jellyfin ABI version.
    /// </summary>
    public override string TargetAbi => "10.11.0.0";
}
