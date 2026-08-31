using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Controllers;

[ApiController]
[Authorize]
[Route("Jellyfin2PopFeed/[controller]")]
public class PopFeedController : ControllerBase
{
    private readonly IAtProtocolService _atProtocolService;

    public PopFeedController(IAtProtocolService atProtocolService)
    {
        _atProtocolService = atProtocolService;
    }

    [HttpGet("Status")]
    public ActionResult<object> GetStatus()
    {
        var cfg = Plugin.Instance!.Configuration;
        return Ok(new
        {
            connected = !string.IsNullOrEmpty(cfg.AtProtocolAccessToken),
            handle = cfg.AtProtocolHandle ?? string.Empty,
            pdsHost = cfg.AtProtocolPdsHost ?? "popfeed.social",
            autoPostMovies = cfg.AutoPostMovies,
            autoPostTvShows = cfg.AutoPostTvShows,
            hasTmdbKey = !string.IsNullOrEmpty(cfg.TmdbApiKey),
            hasWatchedList = !string.IsNullOrEmpty(cfg.WatchedMoviesListUri),
            hasTvLists = !string.IsNullOrEmpty(cfg.CurrentlyWatchingTvShowsListUri) &&
                         !string.IsNullOrEmpty(cfg.WatchedTvShowsListUri),
            hasWatchlists = !string.IsNullOrEmpty(cfg.MovieWatchlistUri) &&
                            !string.IsNullOrEmpty(cfg.TvShowWatchlistUri)
        });
    }

    [HttpPost("Authenticate")]
    public async Task<ActionResult<object>> Authenticate([FromBody] AuthRequest request)
    {
        var authResult = await _atProtocolService.AuthenticateAsync(
            request.Handle, request.Password, request.PdsHost);

        if (authResult == null)
            return BadRequest(new { message = "Authentication failed. Check your handle, password, and PDS host." });

        var cfg = Plugin.Instance!.Configuration;
        cfg.AtProtocolHandle = authResult.Handle ?? request.Handle;
        cfg.AtProtocolDid = authResult.Did;
        cfg.AtProtocolAccessToken = authResult.AccessToken;
        cfg.AtProtocolRefreshToken = authResult.RefreshToken;
        cfg.AtProtocolPdsHost = request.PdsHost;
        cfg.AtProtocolPassword = request.Password;
        cfg.AutoPostMovies = request.AutoPostMovies;
        cfg.AutoPostTvShows = request.AutoPostTvShows;
        cfg.TmdbApiKey = request.TmdbApiKey ?? cfg.TmdbApiKey;

        // Discover ALL lists (movies + TV)
        cfg.WatchedMoviesListUri = await _atProtocolService.DiscoverWatchedMoviesListAsync(cfg) ?? cfg.WatchedMoviesListUri;

        var tvLists = await _atProtocolService.DiscoverTvShowListsAsync(cfg);
        if (tvLists != null)
        {
            cfg.CurrentlyWatchingTvShowsListUri = tvLists.CurrentlyWatchingListUri ?? cfg.CurrentlyWatchingTvShowsListUri;
            cfg.WatchedTvShowsListUri = tvLists.WatchedShowsListUri ?? cfg.WatchedTvShowsListUri;
            cfg.MovieWatchlistUri = tvLists.MovieWatchlistUri ?? cfg.MovieWatchlistUri;
            cfg.TvShowWatchlistUri = tvLists.TvShowWatchlistUri ?? cfg.TvShowWatchlistUri;
        }

        Plugin.Instance!.UpdateConfiguration(cfg);

        return Ok(new
        {
            connected = true,
            handle = cfg.AtProtocolHandle,
            pdsHost = cfg.AtProtocolPdsHost,
            autoPostMovies = cfg.AutoPostMovies,
            autoPostTvShows = cfg.AutoPostTvShows,
            hasTmdbKey = !string.IsNullOrEmpty(cfg.TmdbApiKey),
            hasWatchedList = !string.IsNullOrEmpty(cfg.WatchedMoviesListUri),
            hasTvLists = !string.IsNullOrEmpty(cfg.CurrentlyWatchingTvShowsListUri) &&
                         !string.IsNullOrEmpty(cfg.WatchedTvShowsListUri),
            hasWatchlists = !string.IsNullOrEmpty(cfg.MovieWatchlistUri) &&
                            !string.IsNullOrEmpty(cfg.TvShowWatchlistUri)
        });
    }

    [HttpPost("Settings")]
    public ActionResult<object> SaveSettings([FromBody] SettingsRequest request)
    {
        var cfg = Plugin.Instance!.Configuration;

        if (!string.IsNullOrEmpty(request.Handle))
            cfg.AtProtocolHandle = request.Handle;
        if (!string.IsNullOrEmpty(request.Password))
            cfg.AtProtocolPassword = request.Password;
        if (!string.IsNullOrEmpty(request.PdsHost))
            cfg.AtProtocolPdsHost = request.PdsHost;
        if (!string.IsNullOrEmpty(request.TmdbApiKey))
            cfg.TmdbApiKey = request.TmdbApiKey;
        cfg.AutoPostMovies = request.AutoPostMovies;

        Plugin.Instance!.UpdateConfiguration(cfg);

        return Ok(new
        {
            connected = !string.IsNullOrEmpty(cfg.AtProtocolAccessToken),
            handle = cfg.AtProtocolHandle,
            pdsHost = cfg.AtProtocolPdsHost,
            autoPostMovies = cfg.AutoPostMovies,
            autoPostTvShows = cfg.AutoPostTvShows,
            hasTmdbKey = !string.IsNullOrEmpty(cfg.TmdbApiKey),
            hasWatchedList = !string.IsNullOrEmpty(cfg.WatchedMoviesListUri),
            hasTvLists = !string.IsNullOrEmpty(cfg.CurrentlyWatchingTvShowsListUri) &&
                         !string.IsNullOrEmpty(cfg.WatchedTvShowsListUri)
        });
    }

    [HttpPost("DiscoverList")]
    public async Task<ActionResult<object>> DiscoverList()
    {
        var cfg = Plugin.Instance!.Configuration;
        var listUri = await _atProtocolService.DiscoverWatchedMoviesListAsync(cfg);
        if (listUri != null)
        {
            cfg.WatchedMoviesListUri = listUri;
            var tvLists = await _atProtocolService.DiscoverTvShowListsAsync(cfg);
            if (tvLists != null)
            {
                cfg.CurrentlyWatchingTvShowsListUri = tvLists.CurrentlyWatchingListUri ?? cfg.CurrentlyWatchingTvShowsListUri;
                cfg.WatchedTvShowsListUri = tvLists.WatchedShowsListUri ?? cfg.WatchedTvShowsListUri;
                cfg.MovieWatchlistUri = tvLists.MovieWatchlistUri ?? cfg.MovieWatchlistUri;
                cfg.TvShowWatchlistUri = tvLists.TvShowWatchlistUri ?? cfg.TvShowWatchlistUri;
            }
            Plugin.Instance!.UpdateConfiguration(cfg);
            return Ok(new { found = true, listUri, tvListsFound = tvLists != null });
        }
        return Ok(new { found = false });
    }

    [HttpPost("Disconnect")]
    public ActionResult<object> Disconnect()
    {
        var cfg = Plugin.Instance!.Configuration;
        cfg.AtProtocolAccessToken = string.Empty;
        cfg.AtProtocolRefreshToken = string.Empty;
        cfg.AtProtocolDid = string.Empty;
        Plugin.Instance!.UpdateConfiguration(cfg);
        return Ok(new { connected = false });
    }

    [HttpGet("TestConnection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        var cfg = Plugin.Instance!.Configuration;
        var connected = await _atProtocolService.TestConnectionAsync(cfg);
        return Ok(new
        {
            connected,
            handle = connected ? cfg.AtProtocolHandle : null,
            pdsHost = connected ? cfg.AtProtocolPdsHost : null
        });
    }
}

public class AuthRequest
{
    public string Handle { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PdsHost { get; set; } = "popfeed.social";
    public bool AutoPostMovies { get; set; } = true;
    public bool AutoPostTvShows { get; set; } = true;
    public string? TmdbApiKey { get; set; }
}

public class SettingsRequest
{
    public string? Handle { get; set; }
    public string? Password { get; set; }
    public string? PdsHost { get; set; }
    public bool AutoPostMovies { get; set; } = true;
    public string? TmdbApiKey { get; set; }
}