using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

/// <summary>
/// Global plugin configuration stored at the server level.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// Whether the plugin is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Whether to enable debug logging.
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;

    /// <summary>
    /// Default PDS host for new users.
    /// </summary>
    public string DefaultPdsHost { get; set; } = "popfeed.social";
}
