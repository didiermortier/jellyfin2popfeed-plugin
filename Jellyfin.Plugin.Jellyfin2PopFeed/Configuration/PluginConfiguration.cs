using MediaBrowser.Model.Plugins;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

/// <summary>
/// Plugin configuration with AT Protocol credentials and settings.
/// </summary>
public class PluginConfiguration : BasePluginConfiguration
{
    // --- AT Protocol credentials ---
    public string AtProtocolHandle { get; set; } = string.Empty;
    public string AtProtocolPassword { get; set; } = string.Empty;
    public string AtProtocolPdsHost { get; set; } = "popfeed.social";
    public string AtProtocolDid { get; set; } = string.Empty;
    public string AtProtocolAccessToken { get; set; } = string.Empty;
    public string AtProtocolRefreshToken { get; set; } = string.Empty;

    // --- TMDB ---
    public string TmdbApiKey { get; set; } = string.Empty;

    // --- Movie settings ---
    public bool AutoPostMovies { get; set; } = true;

    /// <summary>
    /// Cached URI of the "Watched Movies" list on the user's PDS, discovered at auth time.
    /// </summary>
    public string WatchedMoviesListUri { get; set; } = string.Empty;

    // --- TV settings ---
    public bool AutoPostTvShows { get; set; } = true;

    /// <summary>
    /// Cached URI of the "Currently Watching Shows" list on the user's PDS.
    /// </summary>
    public string CurrentlyWatchingTvShowsListUri { get; set; } = string.Empty;

    /// <summary>
    /// Cached URI of the "Watched Shows" list on the user's PDS.
    /// </summary>
    public string WatchedTvShowsListUri { get; set; } = string.Empty;

    // --- Watchlists ---

    /// <summary>
    /// Cached URI of the "Movie Watchlist" list (to-watch movies).
    /// </summary>
    public string MovieWatchlistUri { get; set; } = string.Empty;

    /// <summary>
    /// Cached URI of the "Show Watchlist" list (to-watch shows).
    /// </summary>
    public string TvShowWatchlistUri { get; set; } = string.Empty;
}