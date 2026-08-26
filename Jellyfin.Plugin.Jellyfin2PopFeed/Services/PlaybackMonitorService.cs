using System;
using System.Threading;
using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;
using MediaBrowser.Controller.Entities.TV;
using MediaBrowser.Controller.Library;
using MediaBrowser.Controller.Session;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Monitors playback events and posts finished movies/series to PopFeed.
/// Listens for PlaybackStart and PlaybackStop events.
/// Movies: posts on 90%+ completion.
/// TV: posts on start (new season/first watch) and finish (last episode of season).
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

    private async void OnPlaybackStart(object? sender, PlaybackProgressEventArgs e)
    {
        try
        {
            if (e.Item == null)
                return;

            // TV only: detect new season / new series start
            if (e.Item is Episode)
            {
                _logger.LogDebug("TV playback started: {Name}", e.Item.Name);
                await _popFeedService.OnTvPlaybackDetectedAsync(e.Item, isFinished: false);
            }
            // Movies: nothing special on start
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error processing playback start event");
        }
    }

    private async void OnPlaybackStopped(object? sender, PlaybackStopEventArgs e)
    {
        try
        {
            if (e.Item == null)
                return;

            var totalTicks = e.Item.RunTimeTicks ?? 0;
            if (totalTicks <= 0)
                return;

            var positionTicks = e.PlaybackPositionTicks ?? 0;
            var watchedPercent = (double)positionTicks / totalTicks * 100;

            if (watchedPercent < 90.0)
                return;

            _logger.LogDebug(
                "Playback stopped at {Percent:F1}% for {Name} ({Type})",
                watchedPercent, e.Item.Name, e.Item.GetType().Name);

            if (e.Item is Movie)
            {
                await _popFeedService.OnMovieFinishedAsync(e.Item);
            }
            else if (e.Item is Episode)
            {
                await _popFeedService.OnTvPlaybackDetectedAsync(e.Item, isFinished: true);
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