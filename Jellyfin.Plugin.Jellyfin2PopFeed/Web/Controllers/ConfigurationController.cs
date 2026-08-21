using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Controllers;

[ApiController]
[Authorize]
[Route("Jellyfin2PopFeed/[controller]")]
public class PopFeedController : ControllerBase
{
    private readonly IAtProtocolService _atProtocolService;

    public PopFeedController(IAtProtocolService atProtocolService)
    {
        _atProtocolService = atProtocolService;
    }

    /// <summary>
    /// Authenticate against AT Protocol and save everything in one call.
    /// Returns the config state (without password) for the UI.
    /// </summary>
    [HttpPost("Authenticate")]
    public async Task<ActionResult<object>> Authenticate([FromBody] AuthRequest request)
    {
        var authResult = await _atProtocolService.AuthenticateAsync(
            request.Handle, request.Password, request.PdsHost);

        if (authResult == null)
            return BadRequest(new { message = "Authentication failed. Check your handle and password." });

        // Save EVERYTHING in one shot - handle, password, tokens, host, options
        var config = Plugin.Instance!.Configuration;
        config.AtProtocolHandle = authResult.Handle ?? request.Handle;
        config.AtProtocolDid = authResult.Did;
        config.AtProtocolAccessToken = authResult.AccessToken;
        config.AtProtocolRefreshToken = authResult.RefreshToken;
        config.AtProtocolPdsHost = request.PdsHost;
        config.AtProtocolPassword = request.Password;
        config.AutoPostMovies = request.AutoPostMovies;
        Plugin.Instance!.UpdateConfiguration(config);

        return Ok(new
        {
            connected = true,
            handle = config.AtProtocolHandle,
            pdsHost = config.AtProtocolPdsHost,
            did = config.AtProtocolDid,
            autoPostMovies = config.AutoPostMovies
        });
    }

    /// <summary>
    /// Save settings without touching tokens. Server-side merge prevents race conditions.
    /// </summary>
    [HttpPost("Settings")]
    public ActionResult<object> SaveSettings([FromBody] SettingsRequest request)
    {
        var config = Plugin.Instance!.Configuration;

        // Only update fields the user explicitly sent
        if (!string.IsNullOrEmpty(request.Handle))
            config.AtProtocolHandle = request.Handle;
        if (!string.IsNullOrEmpty(request.Password))
            config.AtProtocolPassword = request.Password;
        if (!string.IsNullOrEmpty(request.PdsHost))
            config.AtProtocolPdsHost = request.PdsHost;
        config.AutoPostMovies = request.AutoPostMovies;

        // Preserve existing tokens if they exist
        // (tokens are never sent from the UI, only from Authenticate/Disconnect)

        Plugin.Instance!.UpdateConfiguration(config);

        return Ok(new
        {
            connected = !string.IsNullOrEmpty(config.AtProtocolAccessToken),
            handle = config.AtProtocolHandle,
            pdsHost = config.AtProtocolPdsHost,
            autoPostMovies = config.AutoPostMovies
        });
    }

    /// <summary>
    /// Disconnect: clear all auth tokens. Handle/password/host stay for easy re-auth.
    /// </summary>
    [HttpPost("Disconnect")]
    public ActionResult<object> Disconnect()
    {
        var config = Plugin.Instance!.Configuration;
        config.AtProtocolAccessToken = string.Empty;
        config.AtProtocolRefreshToken = string.Empty;
        config.AtProtocolDid = string.Empty;
        Plugin.Instance!.UpdateConfiguration(config);

        return Ok(new { connected = false });
    }

    /// <summary>
    /// Test that the stored connection is still valid.
    /// </summary>
    [HttpGet("TestConnection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        var config = Plugin.Instance!.Configuration;
        var connected = await _atProtocolService.TestConnectionAsync(config);
        return Ok(new
        {
            connected,
            handle = connected ? config.AtProtocolHandle : null,
            pdsHost = connected ? config.AtProtocolPdsHost : null
        });
    }
}

public class AuthRequest
{
    public string Handle { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string PdsHost { get; set; } = "popfeed.social";
    public bool AutoPostMovies { get; set; } = true;
}

public class SettingsRequest
{
    public string? Handle { get; set; }
    public string? Password { get; set; }
    public string? PdsHost { get; set; }
    public bool AutoPostMovies { get; set; } = true;
}