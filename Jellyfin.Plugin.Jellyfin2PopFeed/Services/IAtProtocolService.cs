using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

public class AuthResult
{
    public string? Did { get; set; }
    public string? Handle { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public System.DateTime? Expiry { get; set; }
}

public class BlobResult
{
    public Dictionary<string, object>? Blob { get; set; }
}

public class TvShowListDiscoveryResult
{
    public string? CurrentlyWatchingListUri { get; set; }
    public string? WatchedShowsListUri { get; set; }
}

public class TmdbMovieResult
{
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? ImdbId { get; set; }
    public string? Director { get; set; }
}

public class TmdbTvShowResult
{
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? ImdbId { get; set; }
    public string? MainCredit { get; set; }  // Network or Creator
    public string? MainCreditRole { get; set; } // "Network" or "Creator"
    public string? FirstAirDate { get; set; }
}

public class TmdbSeasonResult
{
    public int EpisodeCount { get; set; }
    public List<TmdbEpisodeInfo> Episodes { get; set; } = new();
}

public class TmdbEpisodeInfo
{
    public int EpisodeNumber { get; set; }
    public string? AirDate { get; set; }
}

public interface IAtProtocolService
{
    // Auth
    Task<AuthResult?> AuthenticateAsync(string handle, string password, string pdsHost);
    Task<bool> TestConnectionAsync(PluginConfiguration config);

    // List discovery
    Task<string?> DiscoverWatchedMoviesListAsync(PluginConfiguration config);
    Task<TvShowListDiscoveryResult?> DiscoverTvShowListsAsync(PluginConfiguration config);

    // TMDB
    Task<TmdbMovieResult?> FetchTmdbMovieAsync(string tmdbId, string apiKey);
    Task<TmdbTvShowResult?> FetchTmdbTvShowAsync(string tmdbId, string apiKey);
    Task<TmdbSeasonResult?> FetchTmdbSeasonAsync(string tmdbId, int seasonNumber, string apiKey);

    // Movies
    Task<bool> LogMovieWatchAsync(
        PluginConfiguration config,
        string movieTitle,
        int? movieYear,
        string? tmdbId,
        string? releaseDate,
        List<string>? genres,
        string? director,
        string? posterUrl,
        string? backdropUrl,
        string? imdbId);

    Task<bool> MovieWatchExistsAsync(PluginConfiguration config, string? tmdbId, string movieTitle);

    // TV shows
    Task<string?> FindTvShowInListAsync(PluginConfiguration config, string tmdbId, string listUri);
    Task<bool> CreateTvShowListItemAsync(
        PluginConfiguration config,
        string listUri,
        string listType,
        string title,
        string? tmdbId,
        string? imdbId,
        string? releaseDate,
        List<string>? genres,
        string? mainCredit,
        string? mainCreditRole,
        string? posterUrl,
        string? backdropUrl,
        Dictionary<string, object>? posterBlob);
    Task<bool> DeleteListItemAsync(PluginConfiguration config, string recordUri);
    Task<BlobResult?> UploadBlobAsync(PluginConfiguration config, string imageUrl);
}