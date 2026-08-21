using System;
using System.Collections.Generic;
using System.Globalization;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;
using Microsoft.Extensions.Logging;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

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

    public async Task<AuthResult?> AuthenticateAsync(string handle, string password, string pdsHost)
    {
        var requestObj = new { identifier = handle, password };
        var url = $"https://{pdsHost}/xrpc/com.atproto.server.createSession";
        var content = new StringContent(JsonSerializer.Serialize(requestObj), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                    await response.Content.ReadAsStreamAsync());
                return new AuthResult
                {
                    Did = json.TryGetProperty("did", out var did) ? did.GetString() : null,
                    Handle = json.TryGetProperty("handle", out var handleVal) ? handleVal.GetString() : null,
                    AccessToken = json.TryGetProperty("accessJwt", out var accessToken) ? accessToken.GetString() : null,
                    RefreshToken = json.TryGetProperty("refreshJwt", out var refreshToken) ? refreshToken.GetString() : null,
                    Expiry = DateTime.UtcNow.AddHours(24)
                };
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Authentication error for {Handle}", handle);
        }
        return null;
    }

    /// <summary>
    /// Log a movie watch by creating a social.popfeed.feed.review record.
    /// Uses rating=0 as neutral/no-rating to function as a watch log.
    /// The tmdbId in identifiers is used for deduplication.
    /// createdAt determines the "watched on" date in Popfeed.
    /// </summary>
    public async Task<bool> LogMovieWatchAsync(
        PluginConfiguration config,
        string movieTitle,
        int? movieYear,
        string? tmdbId,
        string? releaseDate,
        List<string>? genres,
        string? director)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken))
            return false;

        var identifiers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(tmdbId))
            identifiers["tmdbId"] = tmdbId;

        var recordFields = new Dictionary<string, object>
        {
            ["$type"] = "social.popfeed.feed.review",
            ["identifiers"] = identifiers,
            ["creativeWorkType"] = "movie",
            ["rating"] = 0,
            ["createdAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrEmpty(movieTitle))
            recordFields["title"] = movieTitle;
        if (!string.IsNullOrEmpty(releaseDate))
            recordFields["releaseDate"] = releaseDate;
        if (genres != null && genres.Count > 0)
            recordFields["genres"] = genres;
        if (!string.IsNullOrEmpty(director))
        {
            recordFields["mainCredit"] = director;
            recordFields["mainCreditRole"] = "director";
        }

        var requestObj = new
        {
            repo = config.AtProtocolDid,
            collection = "social.popfeed.feed.review",
            record = recordFields
        };

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.createRecord";
        var jsonContent = new StringContent(
            JsonSerializer.Serialize(requestObj), Encoding.UTF8, "application/json");

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Post, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            httpRequest.Content = jsonContent;

            var response = await _httpClient.SendAsync(httpRequest);
            var body = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation(
                    "Logged watch for {Title} ({Year}) on PopFeed review collection",
                    movieTitle, movieYear);
                return true;
            }

            _logger.LogError(
                "Failed to log watch for {Title}. Status: {Code}, Body: {Body}",
                movieTitle, response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error logging watch for {Title} on PopFeed", movieTitle);
        }
        return false;
    }

    /// <summary>
    /// Check if a review record for this movie already exists.
    /// Queries the social.popfeed.feed.review collection and compares tmdbId.
    /// </summary>
    public async Task<bool> MovieWatchExistsAsync(PluginConfiguration config, string? tmdbId, string movieTitle)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken))
            return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}" +
                  $"&collection=social.popfeed.feed.review" +
                  $"&limit=100";

        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);

        var response = await _httpClient.SendAsync(httpRequest);
            if (!response.IsSuccessStatusCode) return false;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(
                await response.Content.ReadAsStreamAsync());

            if (!json.TryGetProperty("records", out var records))
                return false;

            foreach (var record in records.EnumerateArray())
            {
                if (!record.TryGetProperty("value", out var value))
                    continue;

                // Check by TMDb ID (preferred)
                if (!string.IsNullOrEmpty(tmdbId) &&
                    value.TryGetProperty("identifiers", out var ids) &&
                    ids.TryGetProperty("tmdbId", out var existingTmdb))
                {
                    var existingVal = existingTmdb.GetString();
                    if (string.Equals(existingVal, tmdbId, StringComparison.OrdinalIgnoreCase))
                    {
                        _logger.LogInformation(
                            "Movie {Title} already logged, skipping", movieTitle);
                        return true;
                    }
                }

                // Fallback: check by title
                if (string.IsNullOrEmpty(tmdbId) &&
                    value.TryGetProperty("title", out var titleProp))
                {
                    var existingTitle = titleProp.GetString() ?? string.Empty;
                    if (existingTitle.Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if movie log exists");
        }
        return false;
    }

    public async Task<bool> TestConnectionAsync(PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.server.getSession";
        try
        {
            using var httpRequest = new HttpRequestMessage(HttpMethod.Get, url);
            httpRequest.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);

            var response = await _httpClient.SendAsync(httpRequest);
            return response.IsSuccessStatusCode;
        }
        catch
        {
            return false;
        }
    }
}