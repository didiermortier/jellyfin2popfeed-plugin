using Microsoft.AspNetCore.Mvc;
using MediaBrowser.Controller;
using MediaBrowser.Controller.Persistence;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;
using Jellyfin.Plugin.Jellyfin2PopFeed.Services;
using Jellyfin.Plugin.Jellyfin2PopFeed.Web.Models;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Web.Controllers;

/// <summary>
/// Controller for handling configuration requests.
/// </summary>
[Route("Jellyfin2PopFeed/Configuration")]
public class ConfigurationController : BaseJellyfinApiController
{
    private readonly IAtProtocolService _atProtocolService;
    private readonly IPopFeedService _popFeedService;
    private readonly IUserDataManager _userDataManager;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IAtProtocolService atProtocolService,
        IPopFeedService popFeedService,
        IUserDataManager userDataManager,
        ILogger<ConfigurationController> logger)
    {
        _atProtocolService = atProtocolService;
        _popFeedService = popFeedService;
        _userDataManager = userDataManager;
        _logger = logger;
    }

    /// <summary>
    /// Gets the configuration for the current user.
    /// </summary>
    [HttpGet]
    public ActionResult GetConfiguration()
    {
        var userId = User.GetUserId();
        var settings = GetUserSettings(userId);

        return View(new SettingsViewModel
        {
            UserId = userId,
            AtProtocolHandle = settings.AtProtocolHandle,
            AtProtocolPdsHost = settings.AtProtocolPdsHost,
            AtProtocolAccessToken = settings.AtProtocolAccessToken,
            IsConnected = settings.IsConnected,
            AutoPostMovies = settings.AutoPostMovies
        });
    }

    /// <summary>
    /// Saves the configuration for the current user.
    /// </summary>
    [HttpPost]
    public async Task<ActionResult> SaveConfiguration([FromBody] SettingsViewModel model)
    {
        if (!ModelState.IsValid)
        {
            return BadRequest(ModelState);
        }

        var userId = User.GetUserId();
        var settings = GetUserSettings(userId);

        // Update settings from model
        settings.AtProtocolHandle = model.AtProtocolHandle;
        settings.AtProtocolPdsHost = model.AtProtocolPdsHost;
        settings.AtProtocolAccessToken = model.AtProtocolAccessToken;
        settings.AutoPostMovies = model.AutoPostMovies;
        settings.UserId = userId;

        // Save settings
        SaveUserSettings(settings);

        _logger.LogInformation("Configuration saved for user {UserId}", userId);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Authenticates with AT Protocol.
    /// </summary>
    [HttpPost("Authenticate")]
    public async Task<ActionResult> Authenticate([FromBody] AuthRequest request)
    {
        try
        {
            if (string.IsNullOrEmpty(request.Handle) || string.IsNullOrEmpty(request.Password))
            {
                return BadRequest(new 
                {
                    Success = false,
                    Error = "Handle and password are required"
                });
            }

            var result = await _atProtocolService.AuthenticateAsync(
                request.Handle,
                request.Password,
                request.PdsHost);

            if (result != null)
            {
                var userId = User.GetUserId();
                var settings = GetUserSettings(userId);

                settings.AtProtocolHandle = result.Handle;
                settings.AtProtocolDid = result.Did;
                settings.AtProtocolPdsHost = request.PdsHost;
                settings.AtProtocolAccessToken = result.AccessToken;
                settings.AtProtocolRefreshToken = result.RefreshToken;
                settings.TokenExpiry = result.Expiry;
                settings.UserId = userId;

                SaveUserSettings(settings);

                _logger.LogInformation("Authentication successful for user {UserId}, DID: {Did}",
                    userId, result.Did);

                return Ok(new
                {
                    Success = true,
                    Handle = result.Handle,
                    Did = result.Did
                });
            }

            return BadRequest(new
            {
                Success = false,
                Error = "Authentication failed - invalid credentials or PDS host"
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error");
            return BadRequest(new
            {
                Success = false,
                Error = "Authentication error: " + ex.Message
            });
        }
    }

    /// <summary>
    /// Tests the connection to PopFeed.
    /// </summary>
    [HttpGet("TestConnection")]
    public async Task<ActionResult> TestConnection()
    {
        var userId = User.GetUserId();
        var settings = GetUserSettings(userId);

        if (!settings.IsConnected)
        {
            return Ok(new
            {
                Success = false,
                Error = "Not connected to PopFeed"
            });
        }

        var isConnected = await _atProtocolService.TestConnectionAsync(settings);

        if (isConnected)
        {
            _logger.LogInformation("Connection test successful for user {UserId}", userId);
            return Ok(new { Success = true });
        }

        return Ok(new
        {
            Success = false,
            Error = "Connection failed - invalid token or PDS host"
        });
    }

    /// <summary>
    /// Clears the authentication tokens.
    /// </summary>
    [HttpPost("Disconnect")]
    public ActionResult Disconnect()
    {
        var userId = User.GetUserId();
        var settings = GetUserSettings(userId);

        settings.AtProtocolHandle = null;
        settings.AtProtocolDid = null;
        settings.AtProtocolAccessToken = null;
        settings.AtProtocolRefreshToken = null;
        settings.TokenExpiry = null;

        SaveUserSettings(settings);

        _logger.LogInformation("Disconnected from PopFeed for user {UserId}", userId);

        return Ok(new { Success = true });
    }

    /// <summary>
    /// Gets user settings from persistent storage.
    /// </summary>
    private UserPopFeedSettings GetUserSettings(string userId)
    {
        try
        {
            var userData = _userDataManager.GetUserData(userId);
            var settingsJson = userData?.Get("Jellyfin2PopFeed_Settings");

            if (!string.IsNullOrEmpty(settingsJson))
            {
                return JsonSerializer.Deserialize<UserPopFeedSettings>(settingsJson);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error loading settings for user {UserId}", userId);
        }

        // Return default settings
        return new UserPopFeedSettings
        {
            UserId = userId,
            AutoPostMovies = true,
            AtProtocolPdsHost = "popfeed.social"
        };
    }

    /// <summary>
    /// Saves user settings to persistent storage.
    /// </summary>
    private void SaveUserSettings(UserPopFeedSettings settings)
    {
        try
        {
            var userData = _userDataManager.GetUserData(settings.UserId);
            var settingsJson = JsonSerializer.Serialize(settings);
            userData.Set("Jellyfin2PopFeed_Settings", settingsJson);
            _userDataManager.SaveUserData(settings.UserId, userData);
            _logger.LogInformation("Saved settings for user {UserId}", settings.UserId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error saving settings for user {UserId}", settings.UserId);
        }
    }
}
