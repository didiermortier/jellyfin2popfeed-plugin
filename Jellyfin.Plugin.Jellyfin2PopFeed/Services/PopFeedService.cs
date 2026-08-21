using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;

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

    public async Task OnMovieFinishedAsync(BaseItem item)
    {
        if (item == null || item is not Movie)
            return;

        var config = Plugin.Instance?.Configuration;
        if (config == null || !config.AutoPostMovies || string.IsNullOrEmpty(config.AtProtocolAccessToken))
        {
            _logger.LogWarning("Plugin not configured or auto-post disabled, skipping");
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

        // Check if already in Watched Movies list
        if (await _atProtocolService.MovieWatchExistsAsync(config, tmdbId, movieTitle))
        {
            _logger.LogInformation("Movie {Title} already in Watched Movies list, skipping", movieTitle);
            return;
        }

        // Fetch poster/backdrop/director from TMDB if API key is configured
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
                _logger.LogDebug("Fetched TMDB data for {Title}: poster={Poster}, director={Director}",
                    movieTitle, posterUrl != null ? "yes" : "no", director ?? "none");
            }
        }

        // Gather remaining metadata
        var releaseDate = item.PremiereDate?.ToString("yyyy-MM-dd");
        var genres = item.Genres?.ToList() ?? new List<string>();

        // Create both listItem (Diary/Library) and review (Activity)
        var success = await _atProtocolService.LogMovieWatchAsync(
            config, movieTitle, movieYear, tmdbId, releaseDate, genres, director,
            posterUrl, backdropUrl, imdbId);

        if (success)
            _logger.LogInformation("Logged watch for {Title} ({Year}) to PopFeed Watched Movies", movieTitle, movieYear);
        else
            _logger.LogError("Failed to log watch for {Title} ({Year})", movieTitle, movieYear);
    }
}