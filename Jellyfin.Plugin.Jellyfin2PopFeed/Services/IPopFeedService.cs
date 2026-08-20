using MediaBrowser.Controller.Entities;
using MediaBrowser.Controller.Entities.Movies;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Interface for PopFeed posting service.
/// </summary>
public interface IPopFeedService
{
    /// <summary>
    /// Posts a watched movie to PopFeed.
    /// </summary>
    /// <param name="item">The movie that was watched</param>
    /// <param name="user">The user who watched it</param>
    Task PostWatchedMovieAsync(BaseItem item, MediaBrowser.Controller.Entities.User user);
}
