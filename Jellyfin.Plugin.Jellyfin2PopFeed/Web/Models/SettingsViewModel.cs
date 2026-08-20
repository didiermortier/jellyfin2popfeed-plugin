namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Models;

/// <summary>
/// View model for the configuration page.
/// </summary>
public class SettingsViewModel
{
    /// <summary>
    /// Jellyfin user ID.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// AT Protocol handle.
    /// </summary>
    public string AtProtocolHandle { get; set; }

    /// <summary>
    /// AT Protocol PDS host.
    /// </summary>
    public string AtProtocolPdsHost { get; set; } = "popfeed.social";

    /// <summary>
    /// AT Protocol access token (not exposed in UI, only for internal use).
    /// </summary>
    public string AtProtocolAccessToken { get; set; }

    /// <summary>
    /// Whether the user is connected to PopFeed.
    /// </summary>
    public bool IsConnected { get; set; }

    /// <summary>
    /// Whether to automatically post watched movies.
    /// </summary>
    public bool AutoPostMovies { get; set; } = true;

    /// <summary>
    /// Status of the last post attempt.
    /// </summary>
    public string LastPostStatus { get; set; }

    /// <summary>
    /// Error message if authentication or connection fails.
    /// </summary>
    public string ErrorMessage { get; set; }
}
