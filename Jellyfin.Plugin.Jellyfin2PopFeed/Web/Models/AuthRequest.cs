namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Models;

/// <summary>
/// Model for authentication requests.
/// </summary>
public class AuthRequest
{
    /// <summary>
    /// AT Protocol handle (e.g., username.popfeed.social).
    /// </summary>
    public string Handle { get; set; }

    /// <summary>
    /// User password.
    /// </summary>
    public string Password { get; set; }

    /// <summary>
    /// PDS host (e.g., popfeed.social).
    /// </summary>
    public string PdsHost { get; set; } = "popfeed.social";
}
