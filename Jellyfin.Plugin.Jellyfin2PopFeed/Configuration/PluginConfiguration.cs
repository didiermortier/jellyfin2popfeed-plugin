using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

/// <summary>
/// Plugin configuration with AT Protocol credentials and settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    /// <summary>
    /// AT Protocol handle (e.g., user.bsky.social).
    /// </summary>
    public string AtProtocolHandle { get; set; } = string.Empty;

    /// <summary>
    /// AT Protocol app password.
    /// </summary>
    public string AtProtocolPassword { get; set; } = string.Empty;

    /// <summary>
    /// AT Protocol PDS host (default: popfeed.social).
    /// </summary>
    public string AtProtocolPdsHost { get; set; } = "popfeed.social";

    /// <summary>
    /// Account DID, populated after successful authentication.
    /// </summary>
    public string AtProtocolDid { get; set; } = string.Empty;

    /// <summary>
    /// Access JWT token.
    /// </summary>
    public string AtProtocolAccessToken { get; set; } = string.Empty;

    /// <summary>
    /// Refresh JWT token.
    /// </summary>
    public string AtProtocolRefreshToken { get; set; } = string.Empty;

    /// <summary>
    /// Whether to automatically post watched movies.
    /// </summary>
    public bool AutoPostMovies { get; set; } = true;

    /// <summary>
    /// TMDB API key for fetching poster/backdrop URLs.
    /// </summary>
    public string TmdbApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Cached URI of the "Watched Movies" list on the user's PDS, discovered at auth time.
    /// </summary>
    public string WatchedMoviesListUri { get; set; } = string.Empty;
}