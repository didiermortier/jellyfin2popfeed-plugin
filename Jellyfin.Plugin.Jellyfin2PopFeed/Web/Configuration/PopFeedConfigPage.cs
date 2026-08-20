using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Configuration;

/// <summary>
/// Configuration page for the plugin in Jellyfin dashboard.
/// </summary>
public class PopFeedConfigPage : IPluginConfigurationPage
{
    /// <summary>
    /// Page name displayed in dashboard.
    /// </summary>
    public string Name => "Jellyfin2 PopFeed";

    /// <summary>
    /// Reference to the plugin instance.
    /// </summary>
    public IPlugin Plugin => Plugin.Instance;

    /// <summary>
    /// Menu section where this page appears.
    /// </summary>
    public string MenuSection => "Plugins";

    /// <summary>
    /// Font Awesome icon for the menu item.
    /// </summary>
    public string MenuIcon => "fa fa-film";

    /// <summary>
    /// Page title in the browser tab.
    /// </summary>
    public string PageTitle => "PopFeed Settings";

    /// <summary>
    /// URL for the configuration page.
    /// </summary>
    public string PageUrl => $"{Plugin.Instance.GetPluginUrl()}/Configuration";

    /// <summary>
    /// Whether to show in main menu.
    /// </summary>
    public bool EnableInMainMenu => true;
}
