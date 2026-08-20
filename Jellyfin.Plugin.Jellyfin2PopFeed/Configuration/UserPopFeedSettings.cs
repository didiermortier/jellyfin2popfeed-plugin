namespace Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

/// <summary>
/// Per-user PopFeed settings stored via Jellyfin's IUserDataManager.
/// </summary>
public class UserPopFeedSettings
{
    /// <summary>
    /// Jellyfin user ID.
    /// </summary>
    public string UserId { get; set; }

    /// <summary>
    /// AT Protocol handle (e.g., @username.popfeed.social).
    /// </summary>
    public string AtProtocolHandle { get; set; }

    /// <summary>
    /// AT Protocol DID (Decentralized Identifier).
    /// </summary>
    public string AtProtocolDid { get; set; }

    /// <summary>
    /// AT Protocol PDS host.
    /// </summary>
    public string AtProtocolPdsHost { get; set; } = "popfeed.social";

    /// <summary>
    /// AT Protocol access token (JWT).
    /// </summary>
    public string AtProtocolAccessToken { get; set; }

    /// <summary>
    /// AT Protocol refresh token (JWT).
    /// </summary>
    public string AtProtocolRefreshToken { get; set; }

    /// <summary>
    /// Token expiration timestamp.
    /// </summary>
    public DateTime? TokenExpiry { get; set; }

    /// <summary>
    /// Whether to automatically post watched movies.
    /// </summary>
    public bool AutoPostMovies { get; set; } = true;

    /// <summary>
    /// Whether the user is connected to PopFeed.
    /// </summary>
    public bool IsConnected => !string.IsNullOrEmpty(AtProtocolAccessToken) && TokenExpiry.HasValue && TokenExpiry > DateTime.UtcNow;
}
