using System.Threading.Tasks;
using MediaBrowser.Controller.Entities;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

public interface IPopFeedService
{
    Task PostWatchedMovieAsync(BaseItem item);
}