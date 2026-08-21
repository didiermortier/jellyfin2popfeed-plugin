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
            _logger.LogWarning("No TMDb ID available for {Title}", movieTitle);

        // Check if already logged - prevents overwriting the watch date
        if (await _atProtocolService.MovieWatchExistsAsync(config, tmdbId, movieTitle))
        {
            _logger.LogInformation("Movie {Title} ({Year}) already logged, skipping", movieTitle, movieYear);
            return;
        }

        // Gather metadata for enriched log
        var releaseDate = item.PremiereDate?.ToString("o");
        var genres = item.Genres?.ToList() ?? new List<string>();
        var director = GetDirector(item);

        // Create the review record (acts as a watch log)
        var success = await _atProtocolService.LogMovieWatchAsync(
            config, movieTitle, movieYear, tmdbId, releaseDate, genres, director);

        if (success)
            _logger.LogInformation("Logged watch for {Title} ({Year}) on PopFeed", movieTitle, movieYear);
        else
            _logger.LogError("Failed to log watch for {Title} ({Year}) on PopFeed", movieTitle, movieYear);
    }

    private static string? GetDirector(BaseItem item)
    {
        return null; // Future: inject ILibraryManager to look up directors from People
    }
}