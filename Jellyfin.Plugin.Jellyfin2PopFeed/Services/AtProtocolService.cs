using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

/// <summary>
/// Implementation of AT Protocol service for PopFeed integration.
/// Uses social.popfeed.feed.note collection.
/// </summary>
public class AtProtocolService : IAtProtocolService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<AtProtocolService> _logger;

    public AtProtocolService(HttpClient httpClient, ILogger<AtProtocolService> logger)
    {
        _httpClient = httpClient;
        _logger = logger;
        _httpClient.Timeout = TimeSpan.FromSeconds(30);
    }

    /// <summary>
    /// Authenticates with AT Protocol PDS.
    /// </summary>
    public async Task<AuthResult> AuthenticateAsync(string handle, string password, string pdsHost)
    {
        var request = new
        {
            identifier = handle,
            password = password
        };

        var url = $"https://{pdsHost}/xrpc/com.atproto.server.createSession";

        try
        {
            var response = await _httpClient.PostAsJsonAsync(url, request);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                // Parse JWT to get expiry
                DateTime? expiry = null;
                if (json.TryGetProperty("accessJwt", out var accessJwt))
                {
                    // JWT tokens have expiry in their payload, but we'll use a default of 24 hours
                    expiry = DateTime.UtcNow.AddHours(24);
                }

                return new AuthResult
                {
                    Did = json.GetProperty("did").GetString(),
                    Handle = json.GetProperty("handle").GetString(),
                    AccessToken = json.GetProperty("accessJwt").GetString(),
                    RefreshToken = json.GetProperty("refreshJwt").GetString(),
                    Expiry = expiry
                };
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Authentication failed. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for handle {Handle} at {PdsHost}", handle, pdsHost);
        }

        return null;
    }

    /// <summary>
    /// Creates a post on PopFeed using the social.popfeed.feed.note collection.
    /// </summary>
    public async Task<bool> CreatePostAsync(UserPopFeedSettings settings, object noteRecord)
    {
        if (string.IsNullOrEmpty(settings.AtProtocolAccessToken))
        {
            _logger.LogWarning("No access token available for user {UserId}", settings.UserId);
            return false;
        }

        // Build the record with proper PopFeed collection
        var record = new
        {
            $type = "social.popfeed.feed.note",
            createdAt = DateTime.UtcNow.ToString("o")
        };

        // Merge with the provided noteRecord
        var recordDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(record));
        var noteRecordDict = JsonSerializer.Deserialize<Dictionary<string, object>>(
            JsonSerializer.Serialize(noteRecord));

        foreach (var kvp in noteRecordDict)
        {
            recordDict[kvp.Key] = kvp.Value;
        }

        var request = new
        {
            repo = settings.AtProtocolDid,
            collection = "social.popfeed.feed.note",
            record = recordDict
        };

        var url = $"https://{settings.AtProtocolPdsHost}/xrpc/com.atproto.repo.createRecord";

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", 
                settings.AtProtocolAccessToken);
            httpRequest.Content = JsonContent.Create(request);
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Successfully posted to PopFeed for user {UserId}", settings.UserId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogError("Failed to post to PopFeed. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting to PopFeed for user {UserId}", settings.UserId);
        }

        return false;
    }

    /// <summary>
    /// Checks if a movie already exists on PopFeed for the user.
    /// Queries the user's feed to see if they've already posted about this movie.
    /// </summary>
    public async Task<bool> MovieExistsOnPopFeedAsync(UserPopFeedSettings settings, string movieIdentifier)
    {
        if (string.IsNullOrEmpty(settings.AtProtocolAccessToken))
        {
            return false;
        }

        // Query the user's feed for posts with this movie identifier
        // Using com.atproto.repo.listRecords to get all the user's posts
        var url = $"https://{settings.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords?repo={settings.AtProtocolDid}&collection=social.popfeed.feed.note&limit=100";

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", 
                settings.AtProtocolAccessToken);
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (response.IsSuccessStatusCode)
            {
                var json = await response.Content.ReadFromJsonAsync<JsonElement>();
                
                if (json.TryGetProperty("records", out var records) && records.ValueKind == JsonValueKind.Array)
                {
                    foreach (var record in records.EnumerateArray())
                    {
                        if (record.TryGetProperty("value", out var value))
                        {
                            // Check if this post contains our movie identifier
                            if (value.TryGetProperty("identifiers", out var identifiers))
                            {
                                if (identifiers.TryGetProperty("tmdbId", out var tmdbId) && 
                                    tmdbId.GetString() == movieIdentifier)
                                {
                                    _logger.LogDebug("Movie {MovieId} already exists on PopFeed for user {UserId}", 
                                        movieIdentifier, settings.UserId);
                                    return true;
                                }
                                if (identifiers.TryGetProperty("imdbId", out var imdbId) && 
                                    imdbId.GetString() == movieIdentifier)
                                {
                                    _logger.LogDebug("Movie {MovieId} already exists on PopFeed for user {UserId}", 
                                        movieIdentifier, settings.UserId);
                                    return true;
                                }
                            }
                        }
                    }
                }
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Failed to check movie existence. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking movie existence on PopFeed for user {UserId}", settings.UserId);
        }

        return false;
    }

    /// <summary>
    /// Tests the connection to PopFeed by fetching the user's profile.
    /// </summary>
    public async Task<bool> TestConnectionAsync(UserPopFeedSettings settings)
    {
        if (string.IsNullOrEmpty(settings.AtProtocolAccessToken))
        {
            return false;
        }

        var url = $"https://{settings.AtProtocolPdsHost}/xrpc/app.bsky.actor.getProfile?actor={settings.AtProtocolHandle}";

        try
        {
            var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization = new AuthenticationHeaderValue("Bearer", 
                settings.AtProtocolAccessToken);
            
            var response = await _httpClient.SendAsync(httpRequest);
            
            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Connection test successful for user {UserId}", settings.UserId);
                return true;
            }
            else
            {
                var errorContent = await response.Content.ReadAsStringAsync();
                _logger.LogWarning("Connection test failed. Status: {StatusCode}, Response: {Response}", 
                    response.StatusCode, errorContent);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Connection test error for user {UserId}", settings.UserId);
        }

        return false;
    }
}
