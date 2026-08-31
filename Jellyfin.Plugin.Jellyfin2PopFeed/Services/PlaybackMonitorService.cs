using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Monitors playback events and posts finished movies to PopFeed.
/// Listens for PlaybackStop events at 90%+ completion.
/// </summary>
public class PlaybackMonitorService : IHostedService, IDisposable
{
    private readonly ISessionManager _sessionManager;
    private readonly IPopFeedService _popFeedService;
    private readonly ILogger<PlaybackMonitorService> _logger;

    public PlaybackMonitorService(
        ISessionManager sessionManager,
        IPopFeedService popFeedService,
        ILogger<PlaybackMonitorService> logger)
    {
        _sessionManager = sessionManager;
        _popFeedService = popFeedService;
        _logger = logger;
    }

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart += OnPlaybackStart;
        _sessionManager.PlaybackStopped += OnPlaybackStopped;
        _logger.LogInformation("Jellyfin2PopFeed PlaybackMonitorService started");
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken)
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
        _logger.LogInformation("Jellyfin2PopFeed PlaybackMonitorService stopped");
        return Task.CompletedTask;
    }

    private void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        // We only care about playback stop events
    }

    private async void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            if (e.Item == null)
                return;

            // Determine if the item was watched to completion (90%+, matching Jellyfin's MaxResumePct)
            var totalTicks = e.Item.RunTimeTicks ?? 0;
            if (totalTicks <= 0)
                return;

            var positionTicks = e.PlaybackPositionTicks ?? 0;
            var watchedPercent = (double)positionTicks / totalTicks * 100;

            if (watchedPercent >= 90.0)
            {
                _logger.LogDebug(
                    "Movie {Name} watched to {Percent:F1}%, posting to PopFeed",
                    e.Item.Name, watchedPercent);

                await _popFeedService.OnMovieFinishedAsync(e.Item);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing playback stopped event");
        }
    }

    public void Dispose()
    {
        _sessionManager.PlaybackStart -= OnPlaybackStart;
        _sessionManager.PlaybackStopped -= OnPlaybackStopped;
    }
}