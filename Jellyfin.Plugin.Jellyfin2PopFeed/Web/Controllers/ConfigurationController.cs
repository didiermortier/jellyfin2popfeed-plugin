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
    /// Returns current config state. Used by UI instead of the generic PluginController
    /// to avoid serialization issues with BasePluginConfiguration properties.
    /// </summary>
    [HttpGet("Status")]
    public ActionResult<object> GetStatus()
    {
        var cfg = Plugin.Instance!.Configuration;
        return Ok(new
        {
            connected = !string.IsNullOrEmpty(cfg.AtProtocolAccessToken),
            handle = cfg.AtProtocolHandle ?? string.Empty,
            pdsHost = cfg.AtProtocolPdsHost ?? "popfeed.social",
            autoPostMovies = cfg.AutoPostMovies
        });
    }

    /// <summary>
    /// Authenticate against AT Protocol and save everything in one call.
    /// Validates that the authenticated handle matches what was requested.
    /// </summary>
    [HttpPost("Authenticate")]
    public async Task<ActionResult<object>> Authenticate([FromBody] AuthRequest request)
    {
        var authResult = await _atProtocolService.AuthenticateAsync(
            request.Handle, request.Password, request.PdsHost);

        if (authResult == null)
            return BadRequest(new { message = "Authentication failed. Check your handle, password, and PDS host." });

        // Save EVERYTHING in one shot via server-side UpdateConfiguration
        var cfg = Plugin.Instance!.Configuration;
        cfg.AtProtocolHandle = authResult.Handle ?? request.Handle;
        cfg.AtProtocolDid = authResult.Did;
        cfg.AtProtocolAccessToken = authResult.AccessToken;
        cfg.AtProtocolRefreshToken = authResult.RefreshToken;
        cfg.AtProtocolPdsHost = request.PdsHost;
        cfg.AtProtocolPassword = request.Password;
        cfg.AutoPostMovies = request.AutoPostMovies;
        Plugin.Instance!.UpdateConfiguration(cfg);

        return Ok(new
        {
            connected = true,
            handle = cfg.AtProtocolHandle,
            pdsHost = cfg.AtProtocolPdsHost,
            did = cfg.AtProtocolDid,
            autoPostMovies = cfg.AutoPostMovies
        });
    }

    /// <summary>
    /// Save settings without touching tokens. Server-side merge.
    /// </summary>
    [HttpPost("Settings")]
    public ActionResult<object> SaveSettings([FromBody] SettingsRequest request)
    {
        var cfg = Plugin.Instance!.Configuration;

        if (!string.IsNullOrEmpty(request.Handle))
            cfg.AtProtocolHandle = request.Handle;
        if (!string.IsNullOrEmpty(request.Password))
            cfg.AtProtocolPassword = request.Password;
        if (!string.IsNullOrEmpty(request.PdsHost))
            cfg.AtProtocolPdsHost = request.PdsHost;
        cfg.AutoPostMovies = request.AutoPostMovies;

        Plugin.Instance!.UpdateConfiguration(cfg);

        return Ok(new
        {
            connected = !string.IsNullOrEmpty(cfg.AtProtocolAccessToken),
            handle = cfg.AtProtocolHandle,
            pdsHost = cfg.AtProtocolPdsHost,
            autoPostMovies = cfg.AutoPostMovies
        });
    }

    /// <summary>
    /// Disconnect: clear auth tokens. Handle/password/host persist for re-auth.
    /// </summary>
    [HttpPost("Disconnect")]
    public ActionResult<object> Disconnect()
    {
        var cfg = Plugin.Instance!.Configuration;
        cfg.AtProtocolAccessToken = string.Empty;
        cfg.AtProtocolRefreshToken = string.Empty;
        cfg.AtProtocolDid = string.Empty;
        Plugin.Instance!.UpdateConfiguration(cfg);

        return Ok(new { connected = false });
    }

    /// <summary>
    /// Test stored connection against the AT Protocol PDS.
    /// </summary>
    [HttpGet("TestConnection")]
    public async Task<ActionResult<object>> TestConnection()
    {
        var cfg = Plugin.Instance!.Configuration;
        var connected = await _atProtocolService.TestConnectionAsync(cfg);
        return Ok(new
        {
            connected,
            handle = connected ? cfg.AtProtocolHandle : null,
            pdsHost = connected ? cfg.AtProtocolPdsHost : null
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