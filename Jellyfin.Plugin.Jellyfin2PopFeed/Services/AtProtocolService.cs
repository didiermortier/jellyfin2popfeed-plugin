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
        var obj = new { identifier = handle, password };
        var url = $"https://{pdsHost}/xrpc/com.atproto.server.createSession";
        var content = new StringContent(JsonSerializer.Serialize(obj), Encoding.UTF8, "application/json");

        try
        {
            var response = await _httpClient.PostAsync(url, content);
            if (response.IsSuccessStatusCode)
            {
                var json = await JsonSerializer.DeserializeAsync<JsonElement>(await response.Content.ReadAsStreamAsync());
                return new AuthResult
                {
                    Did = json.TryGetProperty("did", out var did) ? did.GetString() : null,
                    Handle = json.TryGetProperty("handle", out var h) ? h.GetString() : null,
                    AccessToken = json.TryGetProperty("accessJwt", out var at) ? at.GetString() : null,
                    RefreshToken = json.TryGetProperty("refreshJwt", out var rt) ? rt.GetString() : null,
                    Expiry = DateTime.UtcNow.AddHours(24)
                };
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Auth error"); }
        return null;
    }

    public async Task<string?> DiscoverWatchedMoviesListAsync(PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return null;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}&collection=social.popfeed.feed.list&limit=20";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            if (!json.TryGetProperty("records", out var records)) return null;

            foreach (var record in records.EnumerateArray())
            {
                if (!record.TryGetProperty("value", out var value)) continue;
                if (value.TryGetProperty("listType", out var listType) && listType.GetString() == "watched_movies")
                {
                    var uri = record.TryGetProperty("uri", out var u) ? u.GetString() : null;
                    if (uri != null)
                    {
                        _logger.LogInformation("Discovered Watched Movies list: {Uri}", uri);
                        return uri;                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error discovering watched movies list"); }
        return null;
    }

    /// <summary>
    /// Fetch movie metadata from TMDB API to get poster, backdrop, credits, and IMDB ID.
    /// </summary>
    public async Task<TmdbMovieResult?> FetchTmdbMovieAsync(string tmdbId, string apiKey)
    {
        var url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}&append_to_response=credits";
        try
        {
            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            var posterPath = json.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null;
            var backdropPath = json.TryGetProperty("backdrop_path", out var bp) ? bp.GetString() : null;
            var imdbId = json.TryGetProperty("imdb_id", out var imdb) ? imdb.GetString() : null;

            // Extract director from credits
            string? director = null;
            if (json.TryGetProperty("credits", out var credits) &&
                credits.TryGetProperty("crew", out var crew))
            {
                foreach (var member in crew.EnumerateArray())
                {
                    if (member.TryGetProperty("job", out var job) &&
                        string.Equals(job.GetString(), "Director", StringComparison.OrdinalIgnoreCase))
                    {
                        director = member.TryGetProperty("name", out var name) ? name.GetString() : null;
                        break;
                    }
                }
            }

            return new TmdbMovieResult
            {
                PosterUrl = posterPath != null ? $"https://image.tmdb.org/t/p/original{posterPath}" : null,
                BackdropUrl = backdropPath != null ? $"https://image.tmdb.org/t/p/original{backdropPath}" : null,
                ImdbId = imdbId,
                Director = director
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching TMDB data for {TmdbId}", tmdbId); }
        return null;
    }

    /// <summary>
    /// Log a movie watch by creating a social.popfeed.feed.listItem in the Watched Movies list.
    /// The Popfeed AppView generates the activity text from listItem's mainCredit/mainCreditRole.
    /// </summary>
    public async Task<bool> LogMovieWatchAsync(
        PluginConfiguration config,
        string movieTitle,
        int? movieYear,
        string? tmdbId,
        string? releaseDate,
        List<string>? genres,
        string? director,
        string? posterUrl,
        string? backdropUrl,
        string? imdbId)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;
        if (string.IsNullOrEmpty(config.WatchedMoviesListUri))
        {
            _logger.LogError("No Watched Movies list URI discovered.Authenticate again or click Save.");
            return false;
        }

        var identifiers = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(tmdbId)) identifiers["tmdbId"] = tmdbId;
        if (!string.IsNullOrEmpty(imdbId)) identifiers["imdbId"] = imdbId;

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Create listItem record (drives Diary, Library, and Activity)
        var item = new Dictionary<string, object>
        {
            ["$type"] = "social.popfeed.feed.listItem",
            ["identifiers"] = identifiers,
            ["creativeWorkType"] = "movie",
            ["title"] = movieTitle,
            ["listUri"] = config.WatchedMoviesListUri,
            ["listType"] = "watched_movies",
            ["addedAt"] = now
        };
        if (!string.IsNullOrEmpty(releaseDate)) item["releaseDate"] = releaseDate;
        if (genres != null && genres.Count > 0) item["genres"] = genres;
        if (!string.IsNullOrEmpty(director))
        {
            item["mainCredit"] = director;
            item["mainCreditRole"] = "Directed by";
        }
        if (!string.IsNullOrEmpty(posterUrl)) item["posterUrl"] = posterUrl;
        if (!string.IsNullOrEmpty(backdropUrl)) item["backdropUrl"] = backdropUrl;

        var itemOk = await PostRecordAsync(config, "social.popfeed.feed.listItem", item);

        if (itemOk)
            _logger.LogInformation("Logged watch for {Title} to Popfeed (review + listItem)", movieTitle);

        return itemOk;
    }

    /// <summary>
    /// Refresh the access token using the stored refresh token.
    /// Called automatically when the server returns ExpiredToken.
    /// </summary>
    private async Task<bool> TryRefreshTokenAsync(PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(config.AtProtocolRefreshToken)) return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.server.refreshSession";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolRefreshToken);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            var newAccess = json.TryGetProperty("accessJwt", out var access) ? access.GetString() : null;
            var newRefresh = json.TryGetProperty("refreshJwt", out var refresh) ? refresh.GetString() : null;

            if (string.IsNullOrEmpty(newAccess)) return false;

            config.AtProtocolAccessToken = newAccess;
            if (!string.IsNullOrEmpty(newRefresh))
                config.AtProtocolRefreshToken = newRefresh;

            Plugin.Instance!.UpdateConfiguration(config);
            _logger.LogInformation("AT Protocol token refreshed successfully");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to refresh AT Protocol token");
            return false;
        }
    }

    /// <summary>
    /// Send a POST request, auto-refreshing the token on ExpiredToken.
    /// </summary>
    private async Task<bool> PostRecordAsync(PluginConfiguration config, string collection, Dictionary<string, object> record)
    {
        var body = new { repo = config.AtProtocolDid, collection, record };
        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.createRecord";
        var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Post, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
                req.Content = jsonContent;
                var resp = await _httpClient.SendAsync(req);
                var respBody = await resp.Content.ReadAsStringAsync();

                if (resp.IsSuccessStatusCode) return true;

                // Check for expired token and refresh
                if (respBody.Contains("ExpiredToken") && attempt == 0)
                {
                    if (await TryRefreshTokenAsync(config))
                        continue; // Retry with new token
                }

                _logger.LogError("Post to {Coll} failed. Status: {Code}, Body: {Body}", collection, resp.StatusCode, respBody);
                return false;
            }
            catch (Exception ex)
            {
                if (attempt == 0 && await TryRefreshTokenAsync(config))
                    continue;
                _logger.LogError(ex, "Error posting to {Coll}", collection);
                return false;
            }
        }
        return false;
    }

    public async Task<bool> MovieWatchExistsAsync(PluginConfiguration config, string? tmdbId, string movieTitle)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}&collection=social.popfeed.feed.listItem&limit=100";

        for (int attempt = 0; attempt < 2; attempt++)
        {
            try
            {
                using var req = new HttpRequestMessage(HttpMethod.Get, url);
                req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
                var resp = await _httpClient.SendAsync(req);
                if (!resp.IsSuccessStatusCode)
                {
                    var body = await resp.Content.ReadAsStringAsync();
                    if (body.Contains("ExpiredToken") && attempt == 0)
                    {
                        if (await TryRefreshTokenAsync(config))
                            continue;
                    }
                    return false;
                }

                var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
                if (!json.TryGetProperty("records", out var records)) return false;

                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("value", out var value)) continue;

                    if (!string.IsNullOrEmpty(tmdbId) &&
                        value.TryGetProperty("identifiers", out var ids) &&
                        ids.TryGetProperty("tmdbId", out var tid))
                    {
                        if (string.Equals(tid.GetString(), tmdbId, StringComparison.OrdinalIgnoreCase))
                        {
                            _logger.LogInformation("Movie {Title} already in Watched Movies, skipping", movieTitle);
                            return true;
                        }
                    }

                    if (string.IsNullOrEmpty(tmdbId) &&
                        value.TryGetProperty("title", out var tp))
                    {
                        if ((tp.GetString() ?? "").Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
                            return true;
                    }
                }
                return false;
            }
            catch (Exception ex)
            {
                if (attempt == 0 && await TryRefreshTokenAsync(config))
                    continue;
                _logger.LogError(ex, "Error checking if movie in Watched Movies list");
                return false;
            }
        }
        return false;
    }

    public async Task<bool> TestConnectionAsync(PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;
        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.server.getSession";
        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch { return false; }
    }
}

public class TmdbMovieResult
{
    public string? PosterUrl { get; set; }
    public string? BackdropUrl { get; set; }
    public string? ImdbId { get; set; }
    public string? Director { get; set; }
}