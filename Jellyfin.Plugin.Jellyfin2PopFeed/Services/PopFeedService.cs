using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using Microsoft.Extensions.Logging;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

public class PopFeedService : IPopFeedService
{
    private readonly IAtProtocolService _atProtocolService;
    private readonly ILogger<PopFeedService> _logger;

    public PopFeedService(IAtProtocolService atProtocolService, ILogger<PopFeedService> logger)
    {
        _atProtocolService = atProtocolService;
        _logger = logger;
    }

    // ======================== MOVIES ========================

    public async Task OnMovieFinishedAsync(BaseItem item)
    {
        if (item == null || item is not Movie)
            return;

        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.AutoPostMovies || string.IsNullOrEmpty(config.AtProtocolAccessToken))
        {
            _logger.LogWarning("Plugin not configured or auto-post disabled, skipping movie");
            return;
        }

        var movieTitle = item.Name;
        var movieYear = item.ProductionYear;
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;

        if (string.IsNullOrEmpty(tmdbId))
        {
            _logger.LogWarning("No TMDb ID for {Title}, cannot log", movieTitle);
            return;
        }

        if (await _atProtocolService.MovieWatchExistsAsync(config, tmdbId, movieTitle))
        {
            _logger.LogInformation("Movie {Title} already in Watched Movies list, skipping", movieTitle);
            return;
        }

        string? posterUrl = null;
        string? backdropUrl = null;
        string? imdbId = null;
        string? director = null;

        if (!string.IsNullOrEmpty(config.TmdbApiKey))
        {
            var tmdb = await _atProtocolService.FetchTmdbMovieAsync(tmdbId, config.TmdbApiKey);
            if (tmdb != null)
            {
                posterUrl = tmdb.PosterUrl;
                backdropUrl = tmdb.BackdropUrl;
                imdbId = tmdb.ImdbId;
                director = tmdb.Director;
            }
        }

        var releaseDate = item.PremiereDate?.ToString("yyyy-MM-dd");
        var genres = item.Genres?.ToList() ?? new List<string>();

        var success = await _atProtocolService.LogMovieWatchAsync(
            config, movieTitle, movieYear, tmdbId, releaseDate, genres, director,
            posterUrl, backdropUrl, imdbId);

        if (success)
            _logger.LogInformation("Logged watch for {Title} ({Year}) to PopFeed Watched Movies", movieTitle, movieYear);
        else
            _logger.LogError("Failed to log watch for {Title} ({Year})", movieTitle, movieYear);
    }

    // ======================== TV SHOWS ========================

    public async Task OnTvPlaybackDetectedAsync(BaseItem item, bool isFinished)
    {
        if (item == null || item is not Episode episode)
            return;

        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.AutoPostTvShows || string.IsNullOrEmpty(config.AtProtocolAccessToken))
        {
            _logger.LogWarning("Plugin not configured or TV auto-post disabled, skipping TV");
            return;
        }

        var showName = episode.SeriesName ?? episode.Series?.Name ?? "Unknown";
        // Read TMDb ID from the Series, not the Episode — episodes don't have one
        var tmdbId = episode.Series?.ProviderIds.TryGetValue("Tmdb", out var seriesId) == true ? seriesId : null;
        if (string.IsNullOrEmpty(tmdbId))
        {
            _logger.LogWarning("No TMDb ID for TV show {Series} (could not read from Series.ProviderIds), skipping", showName);
            return;
        }

        var seasonNumber = episode.ParentIndexNumber ?? 0;
        var episodeNumber = episode.IndexNumber ?? 0;
        var imdbId = episode.ProviderIds.TryGetValue("Imdb", out var imdb) ? imdb : null;

        // Collect genres from the Series (Jellyfin Episodes inherit Series genres)
        var genres = episode.Series?.Genres?.ToList() ?? episode.Genres?.ToList() ?? new List<string>();

        var currentlyWatchingListUri = config.CurrentlyWatchingTvShowsListUri;
        var watchedShowsListUri = config.WatchedTvShowsListUri;

        if (string.IsNullOrEmpty(currentlyWatchingListUri) || string.IsNullOrEmpty(watchedShowsListUri))
        {
            _logger.LogWarning("TV show lists not discovered. Please authenticate again.");
            return;
        }

        _logger.LogDebug("TV detection: {Show} S{Season}E{Episode} isFinished={IsFinished}",
            showName, seasonNumber, episodeNumber, isFinished);

        if (isFinished)
            await HandleEpisodeFinishedAsync(config, tmdbId, showName, imdbId, genres,
                seasonNumber, episodeNumber, currentlyWatchingListUri, watchedShowsListUri);
        else
            await HandleEpisodeStartedAsync(config, tmdbId, showName, imdbId, genres,
                currentlyWatchingListUri, watchedShowsListUri);
    }

    private async Task HandleEpisodeStartedAsync(
        PluginConfiguration config,
        string tmdbId,
        string showName,
        string? imdbId,
        List<string> genres,
        string currentlyWatchingListUri,
        string watchedShowsListUri)
    {
        // Check if show is in watched list (new season / resuming)
        var watchedRecordUri = await _atProtocolService.FindTvShowInListAsync(config, tmdbId, watchedShowsListUri);
        if (!string.IsNullOrEmpty(watchedRecordUri))
        {
            _logger.LogInformation("TV show {Show} is in watched list, moving back to currently watching", showName);

            var tmdb = await _atProtocolService.FetchTmdbTvShowAsync(tmdbId, config.TmdbApiKey);
            Dictionary<string, object>? posterBlob = null;
            if (tmdb?.PosterUrl != null)
                posterBlob = (await _atProtocolService.UploadBlobAsync(config, tmdb.PosterUrl))?.Blob;

            await _atProtocolService.DeleteListItemAsync(config, watchedRecordUri);
            await _atProtocolService.CreateTvShowListItemAsync(
                config, currentlyWatchingListUri, "currently_watching_tv_shows",
                showName, tmdbId, imdbId,
                tmdb?.FirstAirDate, genres,
                tmdb?.MainCredit, tmdb?.MainCreditRole,
                tmdb?.PosterUrl, tmdb?.BackdropUrl,
                posterBlob);
            return;
        }

        // Check if already in currently watching
        var currentRecordUri = await _atProtocolService.FindTvShowInListAsync(config, tmdbId, currentlyWatchingListUri);
        if (!string.IsNullOrEmpty(currentRecordUri))
        {
            _logger.LogDebug("TV show {Show} already in currently watching", showName);
            return;
        }

        // First time watching
        _logger.LogInformation("TV show {Show} is new, adding to currently watching", showName);
        var tmdbData = await _atProtocolService.FetchTmdbTvShowAsync(tmdbId, config.TmdbApiKey);

        Dictionary<string, object>? posterBlobNew = null;
        if (tmdbData?.PosterUrl != null)
            posterBlobNew = (await _atProtocolService.UploadBlobAsync(config, tmdbData.PosterUrl))?.Blob;

        await _atProtocolService.CreateTvShowListItemAsync(
            config, currentlyWatchingListUri, "currently_watching_tv_shows",
            showName, tmdbId, imdbId,
            tmdbData?.FirstAirDate, genres,
            tmdbData?.MainCredit, tmdbData?.MainCreditRole,
            tmdbData?.PosterUrl, tmdbData?.BackdropUrl,
            posterBlobNew);
    }

    private async Task HandleEpisodeFinishedAsync(
        PluginConfiguration config,
        string tmdbId,
        string showName,
        string? imdbId,
        List<string> genres,
        int seasonNumber,
        int episodeNumber,
        string currentlyWatchingListUri,
        string watchedShowsListUri)
    {
        // Only act if show is in currently_watching
        var currentRecordUri = await _atProtocolService.FindTvShowInListAsync(config, tmdbId, currentlyWatchingListUri);
        if (string.IsNullOrEmpty(currentRecordUri))
        {
            _logger.LogDebug("TV show {Show} not in currently watching, nothing to do", showName);
            return;
        }

        // Fetch season data from TMDB
        var seasonData = await _atProtocolService.FetchTmdbSeasonAsync(tmdbId, seasonNumber, config.TmdbApiKey);
        if (seasonData == null)
        {
            _logger.LogWarning("Could not fetch season data for {Show} S{Season}", showName, seasonNumber);
            return;
        }

        // Check if this is the last episode of the season
        if (episodeNumber != seasonData.EpisodeCount)
        {
            _logger.LogDebug("Episode {E} not last (season has {Count} eps)", episodeNumber, seasonData.EpisodeCount);
            return;
        }

        // Last episode - check air date
        var airedEpisode = seasonData.Episodes.FirstOrDefault(e => e.EpisodeNumber == episodeNumber);
        if (airedEpisode?.AirDate != null)
        {
            if (DateTime.TryParse(airedEpisode.AirDate, out var airedDate) && airedDate > DateTime.UtcNow)
            {
                _logger.LogInformation("Last ep of {Show} S{Season} not aired yet ({AirDate}), keeping",
                    showName, seasonNumber, airedEpisode.AirDate);
                return;
            }
        }

        _logger.LogInformation("TV show {Show} finished S{Season}, moving to watched", showName, seasonNumber);

        var tmdb = await _atProtocolService.FetchTmdbTvShowAsync(tmdbId, config.TmdbApiKey);
        Dictionary<string, object>? posterBlob = null;
        if (tmdb?.PosterUrl != null)
            posterBlob = (await _atProtocolService.UploadBlobAsync(config, tmdb.PosterUrl))?.Blob;

        await _atProtocolService.DeleteListItemAsync(config, currentRecordUri);
        await _atProtocolService.CreateTvShowListItemAsync(
            config, watchedShowsListUri, "watched_tv_shows",
            showName, tmdbId, imdbId,
            tmdb?.FirstAirDate, genres,
            tmdb?.MainCredit, tmdb?.MainCreditRole,
            tmdb?.PosterUrl, tmdb?.BackdropUrl,
            posterBlob);
    }
}