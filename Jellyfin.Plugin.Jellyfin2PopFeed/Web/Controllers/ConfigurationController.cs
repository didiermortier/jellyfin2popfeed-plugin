using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Controllers;

/// <summary>
/// API controller for AT Protocol authentication and connection testing.
/// Configuration save/load uses Jellyfin's built-in plugin API.
/// </summary>
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
    /// Authenticate against AT Protocol / PopFeed and store credentials.
    /// </summary>
    [HttpPost("Authenticate")]
    public async Task<ActionResult<AuthResult>> Authenticate([FromBody] AuthRequest request)
    {
        var authResult = await _atProtocolService.AuthenticateAsync(
            request.Handle, request.Password, request.PdsHost);

        if (authResult == null)
            return BadRequest(new { message = "Authentication failed. Check your handle and password." });

        // Save credentials to plugin config, preserving existing settings
        var config = Plugin.Instance.Configuration;
        config.AtProtocolHandle = authResult.Handle ?? request.Handle;
        config.AtProtocolDid = authResult.Did;
        config.AtProtocolAccessToken = authResult.AccessToken;
        config.AtProtocolRefreshToken = authResult.RefreshToken;
        config.AtProtocolPdsHost = request.PdsHost;
        Plugin.Instance.UpdateConfiguration(config);

        // Don't send password back to client
        return Ok(new
        {
            authResult.Did,
            authResult.Handle,
            authResult.AccessToken,
            authResult.RefreshToken,
            authResult.Expiry
        });
    }

    /// <summary>
    /// Test that the stored connection is still valid.
    /// </summary>
    [HttpGet("TestConnection")]
    public async Task<ActionResult<bool>> TestConnection()
    {
        var connected = await _atProtocolService.TestConnectionAsync(Plugin.Instance.Configuration);
        return Ok(connected);
    }
}

/// <summary>
/// Request model for authentication.
/// </summary>
public class AuthRequest
{
    public string? Handle { get; set; }
    public string? Password { get; set; }
    public string PdsHost { get; set; } = "popfeed.social";
}