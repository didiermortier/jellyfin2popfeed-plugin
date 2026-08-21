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

    public async Task<bool> CreateMoviePostAsync(
        PluginConfiguration config,
        string movieTitle,
        int? movieYear,
        string? tmdbId,
        string? releaseDate,
        List<string>? genres,
        string? director,
        string? overview)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        // Build identifiers object
        var identifiers = new Dictionary<string, string>();
        if (!string.IsNullOrEmpty(tmdbId))
            identifiers["tmdbId"] = tmdbId;

        // Build text
        var text = movieYear.HasValue
            ? $"Watched {movieTitle} ({movieYear})"
            : $"Watched {movieTitle}";

        // Build the record matching social.popfeed.feed.note lexicon
        var recordFields = new Dictionary<string, object>
        {
            ["$type"] = "social.popfeed.feed.note",
            ["identifiers"] = identifiers,
            ["creativeWorkType"] = "movie",
            ["text"] = text,
            ["title"] = movieTitle,
            ["createdAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        // Optional: release date
        if (!string.IsNullOrEmpty(releaseDate))
            recordFields["releaseDate"] = releaseDate;

        // Optional: genres
        if (genres != null && genres.Count > 0)
            recordFields["genres"] = genres;

        // Optional: main credit (director)
        if (!string.IsNullOrEmpty(director))
        {
            recordFields["mainCredit"] = director;
            recordFields["mainCreditRole"] = "director";
        }

        var requestObj = new
        {
            repo = config.AtProtocolDid,
            collection = "social.popfeed.feed.note",
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
                    "Posted movie {Title} ({Year}) to PopFeed for {Handle}",
                    movieTitle, movieYear, config.AtProtocolHandle);
                return true;
            }

            _logger.LogError(
                "Failed to post {Title} to PopFeed. Status: {Code}, Body: {Body}",
                movieTitle, response.StatusCode, body);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error posting movie {Title} to PopFeed", movieTitle);
        }
        return false;
    }

    public async Task<bool> MovieExistsOnPopFeedAsync(PluginConfiguration config, string? tmdbId, string movieTitle)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}" +
                  $"&collection=social.popfeed.feed.note" +
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
                        return true;
                }

                // Fallback: check by title
                if (string.IsNullOrEmpty(tmdbId) &&
                    value.TryGetProperty("text", out var textProp))
                {
                    var postText = textProp.GetString() ?? string.Empty;
                    if (postText.Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking if movie exists on PopFeed");
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