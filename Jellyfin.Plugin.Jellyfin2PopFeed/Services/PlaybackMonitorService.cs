using MediaBrowser.Controller.Session;
using MediaBrowser.Model.Session;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Background service that monitors playback sessions and triggers PopFeed posts.
/// </summary>
public class PlaybackMonitorService : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly IPopFeedService _popFeedService;
    private readonly IEventManager _eventManager;
    private readonly ILogger<PlaybackMonitorService> _logger;

    public PlaybackMonitorService(
        ISessionManager sessionManager,
        IPopFeedService popFeedService,
        IEventManager eventManager,
        ILogger<PlaybackMonitorService> logger)
    {
        _sessionManager = sessionManager;
        _popFeedService = popFeedService;
        _eventManager = eventManager;
        _logger = logger;
    }

    /// <summary>
    /// Starts the playback monitoring service.
    /// </summary>
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _eventManager.SessionActivity += OnSessionActivity;
        _logger.LogInformation("PlaybackMonitorService started - monitoring for watched movies");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Stops the playback monitoring service.
    /// </summary>
    public Task StopAsync(CancellationToken cancellationToken)
    {
        _eventManager.SessionActivity -= OnSessionActivity;
        _logger.LogInformation("PlaybackMonitorService stopped");
        return Task.CompletedTask;
    }

    /// <summary>
    /// Handles session activity events.
    /// </summary>
    private async void OnSessionActivity(object sender, SessionEventArgs e)
    {
        try
        {
            // Only handle PlaybackStopped events
            if (e.EventType != SessionEventType.PlaybackStopped)
            {
                return;
            }

            // Check if playback was completed
            var stopInfo = e.PlaybackStopInfo;
            if (stopInfo == null || !stopInfo.PlaybackFinished)
            {
                return;
            }

            // Get the item and user
            var item = stopInfo.Item;
            var user = e.SessionInfo?.User;

            if (item == null || user == null)
            {
                _logger.LogDebug("No item or user in playback stopped event");
                return;
            }

            // Only process Movies
            if (item is Movie movie)
            {
                _logger.LogInformation("Movie playback completed: {MovieName} by user {UserId}", 
                    movie.Name, user.Id);
                
                // Post to PopFeed
                await _popFeedService.PostWatchedMovieAsync(movie, user);
            }
            else
            {
                _logger.LogDebug("Skipping non-movie item: {ItemName} (Type: {ItemType})", 
                    item.Name, item.GetType().Name);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error handling playback event");
        }
    }

    /// <summary>
    /// Disposes the service.
    /// </summary>
    public void Dispose()
    {
        _eventManager.SessionActivity -= OnSessionActivity;
        GC.SuppressFinalize(this);
    }
}
