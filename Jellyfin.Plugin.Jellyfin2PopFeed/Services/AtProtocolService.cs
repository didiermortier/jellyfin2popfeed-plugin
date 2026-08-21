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

    /// <summary>
    /// Discover the user's "Watched Movies" list URI by querying their PDS.
    /// </summary>
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
                        return uri;
                    }
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error discovering watched movies list"); }
        return null;
    }

    /// <summary>
    /// Fetch movie metadata from TMDB API to get poster and backdrop paths.
    /// </summary>
    public async Task<TmdbMovieResult?> FetchTmdbMovieAsync(string tmdbId, string apiKey)
    {
        var url = $"https://api.themoviedb.org/3/movie/{tmdbId}?api_key={apiKey}";
        try
        {
            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            var posterPath = json.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null;
            var backdropPath = json.TryGetProperty("backdrop_path", out var bp) ? bp.GetString() : null;
            var imdbId = json.TryGetProperty("imdb_id", out var imdb) ? imdb.GetString() : null;

            return new TmdbMovieResult
            {
                PosterUrl = posterPath != null ? $"https://image.tmdb.org/t/p/original{posterPath}" : null,
                BackdropUrl = backdropPath != null ? $"https://image.tmdb.org/t/p/original{backdropPath}" : null,
                ImdbId = imdbId
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching TMDB data for {TmdbId}", tmdbId); }
        return null;
    }

    /// <summary>
    /// Log a movie watch by creating a social.popfeed.feed.listItem in the Watched Movies list.
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
            _logger.LogError("No Watched Movies list URI discovered. Authenticate again or click Save.");
            return false;
        }

        var identifiers = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(tmdbId)) identifiers["tmdbId"] = tmdbId;
        if (!string.IsNullOrEmpty(imdbId)) identifiers["imdbId"] = imdbId;

        var record = new Dictionary<string, object>
        {
            ["$type"] = "social.popfeed.feed.listItem",
            ["identifiers"] = identifiers,
            ["creativeWorkType"] = "movie",
            ["title"] = movieTitle,
            ["listUri"] = config.WatchedMoviesListUri,
            ["listType"] = "watched_movies",
            ["addedAt"] = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture)
        };

        if (!string.IsNullOrEmpty(releaseDate)) record["releaseDate"] = releaseDate;
        if (genres != null && genres.Count > 0) record["genres"] = genres;
        if (!string.IsNullOrEmpty(director))
        {
            record["mainCredit"] = director;
            record["mainCreditRole"] = "director";
        }
        if (!string.IsNullOrEmpty(posterUrl)) record["posterUrl"] = posterUrl;
        if (!string.IsNullOrEmpty(backdropUrl)) record["backdropUrl"] = backdropUrl;

        var body = new { repo = config.AtProtocolDid, collection = "social.popfeed.feed.listItem", record };
        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.createRecord";
        var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            req.Content = jsonContent;
            var resp = await _httpClient.SendAsync(req);
            var respBody = await resp.Content.ReadAsStringAsync();

            if (resp.IsSuccessStatusCode)
            {
                _logger.LogInformation("Logged watch for {Title} to Watched Movies list", movieTitle);
                return true;
            }
            _logger.LogError("Failed to log {Title}. Status: {Code}, Body: {Body}", movieTitle, resp.StatusCode, respBody);
        }
        catch (Exception ex) { _logger.LogError(ex, "Error logging watch for {Title}", movieTitle); }
        return false;
    }

    /// <summary>
    /// Check if a listItem for this movie already exists in the Watched Movies collection.
    /// </summary>
    public async Task<bool> MovieWatchExistsAsync(PluginConfiguration config, string? tmdbId, string movieTitle)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}&collection=social.popfeed.feed.listItem&limit=100";

        try
        {
            using var req = new HttpRequestMessage(HttpMethod.Get, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            var resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode) return false;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            if (!json.TryGetProperty("records", out var records)) return false;

            foreach (var record in records.EnumerateArray())
            {
                if (!record.TryGetProperty("value", out var value)) continue;

                // Check by TMDb ID
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

                // Fallback by title
                if (string.IsNullOrEmpty(tmdbId) &&
                    value.TryGetProperty("title", out var tp))
                {
                    if ((tp.GetString() ?? "").Contains(movieTitle, StringComparison.OrdinalIgnoreCase))
                        return true;
                }
            }
        }
        catch (Exception ex) { _logger.LogError(ex, "Error checking if movie in Watched Movies list"); }
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
}