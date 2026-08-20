using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Persistence;
using MediaBrowser.Model.Entities;
using MediaBrowser.Model.Providers;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Service that handles posting watched movies to PopFeed.
/// </summary>
public class PopFeedService : IPopFeedService
{
    private readonly IAtProtocolService _atProtocolService;
    private readonly IUserDataManager _userDataManager;
    private readonly ILogger<PopFeedService> _logger;

    public PopFeedService(
        IAtProtocolService atProtocolService,
        IUserDataManager userDataManager,
        ILogger<PopFeedService> logger)
    {
        _atProtocolService = atProtocolService;
        _userDataManager = userDataManager;
        _logger = logger;
    }

    /// <summary>
    /// Posts a watched movie to PopFeed.
    /// </summary>
    public async Task PostWatchedMovieAsync(BaseItem item, MediaBrowser.Controller.Entities.User user)
    {
        if (item == null || user == null)
        {
            _logger.LogWarning("Null item or user provided to PostWatchedMovieAsync");
            return;
        }

        // Only process Movies
        if (item is not Movie)
        {
            _logger.LogDebug("Skipping non-movie item: {ItemName}", item.Name);
            return;
        }

        // Get user settings
        var settings = GetUserSettings(user.Id);
        if (settings == null)
        {
            _logger.LogWarning("No settings found for user {UserId}", user.Id);
            return;
        }

        // Check if auto-posting is enabled
        if (!settings.AutoPostMovies)
        {
            _logger.LogDebug("Auto-posting disabled for user {UserId}", user.Id);
            return;
        }

        // Check if user is connected to PopFeed
        if (!settings.IsConnected)
        {
            _logger.LogWarning("User {UserId} is not connected to PopFeed", user.Id);
            return;
        }

        // Get movie identifier (prefer TMDB, fall back to IMDb)
        var movieIdentifier = item.GetProviderId(MetadataProviders.Tmdb) ?? 
                             item.GetProviderId(MetadataProviders.Imdb);

        if (string.IsNullOrEmpty(movieIdentifier))
        {
            _logger.LogWarning("No TMDB or IMDb ID found for movie {MovieName}", item.Name);
            return;
        }

        // Check if movie already exists on PopFeed
        var alreadyExists = await _atProtocolService.MovieExistsOnPopFeedAsync(settings, movieIdentifier);
        if (alreadyExists)
        {
            _logger.LogInformation("Movie {MovieName} ({MovieId}) already exists on PopFeed for user {UserId}", 
                item.Name, movieIdentifier, user.Id);
            return;
        }

        // Build the note record for PopFeed
        var noteRecord = new
        {
            identifiers = new
            {
                tmdbId = item.GetProviderId(MetadataProviders.Tmdb),
                imdbId = item.GetProviderId(MetadataProviders.Imdb)
            },
            creativeWorkType = "movie",
            text = GeneratePostText(item),
            title = item.Name,
            releaseDate = item.PremiereDate?.ToString("o"),
            releaseYear = item.ProductionYear,
            genres = item.Genres,
            mainCredit = GetMainCredit(item),
            mainCreditRole = GetMainCreditRole(item),
            duration = item.RunTimeTicks.HasValue ? TimeSpan.FromTicks(item.RunTimeTicks.Value).TotalSeconds : null,
            overview = item.Overview,
            createdAt = DateTime.UtcNow.ToString("o")
        };

        // Post to PopFeed
        var success = await _atProtocolService.CreatePostAsync(settings, noteRecord);
        
        if (success)
        {
            _logger.LogInformation("Successfully posted movie {MovieName} to PopFeed for user {UserId}", 
                item.Name, user.Id);
        }
        else
        {
            _logger.LogError("Failed to post movie {MovieName} to PopFeed for user {UserId}", 
                item.Name, user.Id);
        }
    }

    /// <summary>
    /// Generates the post text for the movie.
    /// </summary>
    private string GeneratePostText(BaseItem item)
    {
        if (item.ProductionYear.HasValue)
        {
            return $"Watched {item.Name} ({item.ProductionYear})";
        }
        return $"Watched {item.Name}";
    }

    /// <summary>
    /// Gets the main credit (director) for the movie.
    /// </summary>
    private string GetMainCredit(BaseItem item)
    {
        return item.People?.FirstOrDefault(p => p.Type == PersonType.Director)?.Name;
    }

    /// <summary>
    /// Gets the role of the main credit.
    /// </summary>
    private string GetMainCreditRole(BaseItem item)
    {
        return item.People?.Any(p => p.Type == PersonType.Director) == true ? "director" : "creator";
    }

    /// <summary>
    /// Gets user settings from persistent storage.
    /// </summary>
    private UserPopFeedSettings GetUserSettings(string userId)
    {
        try
        {
            var userData = _userDataManager.GetUserData(userId);
            var settingsJson = userData?.Get("Jellyfin2PopFeed_Settings");

            if (!string.IsNullOrEmpty(settingsJson))
            {
                return JsonSerializer.Deserialize<UserPopFeedSettings>(settingsJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings for user {UserId}", userId);
        }

        // Return default settings
        return new UserPopFeedSettings
        {
            UserId = userId,
            AutoPostMovies = true,
            AtProtocolPdsHost = "popfeed.social"
        };
    }

    /// <summary>
    /// Saves user settings to persistent storage.
    /// </summary>
    public void SaveUserSettings(UserPopFeedSettings settings)
    {
        try
        {
            var userData = _userDataManager.GetUserData(settings.UserId);
            var settingsJson = JsonSerializer.Serialize(settings);
            userData.Set("Jellyfin2PopFeed_Settings", settingsJson);
            _userDataManager.SaveUserData(settings.UserId, userData);
            _logger.LogInformation("Saved settings for user {UserId}", settings.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings for user {UserId}", settings.UserId);
        }
    }
}
