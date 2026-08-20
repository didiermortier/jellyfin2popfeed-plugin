using System.Text.Json;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Interface for AT Protocol operations.
/// </summary>
public interface IAtProtocolService
{
    /// <summary>
    /// Authenticates with AT Protocol PDS.
    /// </summary>
    /// <param name="handle">AT Protocol handle (e.g., username.popfeed.social)</param>
    /// <param name="password">User password</param>
    /// <param name="pdsHost">PDS host</param>
    /// <returns>Authentication result with tokens and DID</returns>
    Task<AuthResult> AuthenticateAsync(string handle, string password, string pdsHost);

    /// <summary>
    /// Creates a post on PopFeed using the social.popfeed.feed.note collection.
    /// </summary>
    /// <param name="settings">User settings</param>
    /// <param name="noteRecord">The note record to post</param>
    /// <returns>True if successful</returns>
    Task<bool> CreatePostAsync(UserPopFeedSettings settings, object noteRecord);

    /// <summary>
    /// Checks if a movie already exists on PopFeed for the user.
    /// </summary>
    /// <param name="settings">User settings</param>
    /// <param name="movieIdentifier">Movie identifier (TMDB or IMDb ID)</param>
    /// <returns>True if the movie already exists on PopFeed</returns>
    Task<bool> MovieExistsOnPopFeedAsync(UserPopFeedSettings settings, string movieIdentifier);

    /// <summary>
    /// Tests the connection to PopFeed.
    /// </summary>
    /// <param name="settings">User settings</param>
    /// <returns>True if connection is valid</returns>
    Task<bool> TestConnectionAsync(UserPopFeedSettings settings);
}

/// <summary>
/// Result of AT Protocol authentication.
/// </summary>
public class AuthResult
{
    public string Did { get; set; }
    public string Handle { get; set; }
    public string AccessToken { get; set; }
    public string RefreshToken { get; set; }
    public DateTime? Expiry { get; set; }
}
