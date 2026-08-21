using System.Collections.Generic;
using System.Linq;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;
using System.Threading.Tasks;

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

    public async Task PostWatchedMovieAsync(BaseItem item)
    {
        if (item == null || item is not Movie)
            return;

        var config = Plugin.Instance.Configuration;
        if (config == null || !config.AutoPostMovies || string.IsNullOrEmpty(config.AtProtocolAccessToken))
            return;

        var movieTitle = item.Name;
        var movieYear = item.ProductionYear;

        // Get TMDb ID from ProviderIds
        var tmdbId = item.ProviderIds.TryGetValue("Tmdb", out var id) ? id : null;

        // Check if already posted
        if (await _atProtocolService.MovieExistsOnPopFeedAsync(config, tmdbId, movieTitle))
        {
            _logger.LogInformation("Movie {Title} ({Year}) already on PopFeed, skipping",
                movieTitle, movieYear);
            return;
        }

        // Gather movie metadata for enriched post
        var releaseDate = item.PremiereDate?.ToString("o");
        var genres = item.Genres?.ToList() ?? new List<string>();
        var director = GetDirector(item);
        var overview = item.Overview;

        // Post to PopFeed
        var success = await _atProtocolService.CreateMoviePostAsync(
            config, movieTitle, movieYear, tmdbId, releaseDate, genres, director, overview);

        _logger.LogInformation(
            success
                ? "Posted {Title} ({Year}) to PopFeed"
                : "Failed to post {Title} ({Year}) to PopFeed",
            movieTitle, movieYear);
    }

    /// <summary>
    /// Extract director name from movie metadata.
    /// Jellyfin stores directors in the People list.
    /// </summary>
    private static string? GetDirector(BaseItem item)
    {
        // Try to find director from the People property (available on Movie/BaseItem)
        // In Jellyfin, directors are stored as Person with Type = "Director"
        // This requires ILibraryManager, but we can get it from the item's metadata
        try
        {
            // BaseItem doesn't have a direct "Director" property, but we can
            // access it via the People API. For now return null and let
            // the AtProtocolService handle it gracefully.
            // Future enhancement: inject ILibraryManager to look up directors.
            return null;
        }
        catch
        {
            return null;
        }
    }
}