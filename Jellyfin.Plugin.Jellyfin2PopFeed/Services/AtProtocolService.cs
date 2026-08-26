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

    // ======================== AUTH ========================

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

    // ======================== LIST DISCOVERY ========================

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

    public async Task<TvShowListDiscoveryResult?> DiscoverTvShowListsAsync(PluginConfiguration config)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return null;

        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.listRecords" +
                  $"?repo={config.AtProtocolDid}&collection=social.popfeed.feed.list&limit=20";

        var result = new TvShowListDiscoveryResult();

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
                if (!value.TryGetProperty("listType", out var listType)) continue;
                var type = listType.GetString();
                var uri = record.TryGetProperty("uri", out var u) ? u.GetString() : null;
                if (uri == null) continue;

                if (type == "currently_watching_tv_shows")
                {
                    result.CurrentlyWatchingListUri = uri;
                    _logger.LogInformation("Discovered Currently Watching Shows list: {Uri}", uri);
                }
                else if (type == "watched_tv_shows" || type == "watched_tv_shows")
                {
                    result.WatchedShowsListUri = uri;
                    _logger.LogInformation("Discovered Watched Shows list: {Uri}", uri);
                }
            }

            if (result.CurrentlyWatchingListUri != null && result.WatchedShowsListUri != null)
                return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error discovering TV show lists"); }

        _logger.LogWarning("Could not discover both TV show lists. Found: currentlyWatching={CW}, watched={W}",
            result.CurrentlyWatchingListUri ?? "null", result.WatchedShowsListUri ?? "null");
        return null;
    }

    // ======================== TMDB ========================

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
public async Task<TmdbTvShowResult?> FetchTmdbTvShowAsync(string tmdbId, string apiKey)
    {
        var url = $"https://api.themoviedb.org/3/tv/{tmdbId}?api_key={apiKey}&append_to_response=externalids";
        try
        {
            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            var posterPath = json.TryGetProperty("poster_path", out var pp) ? pp.GetString() : null;
            var backdropPath = json.TryGetProperty("backdrop_path", out var bp) ? bp.GetString() : null;
            var firstAirDate = json.TryGetProperty("first_air_date", out var fd) ? fd.GetString() : null;

            // Get IMDB ID from external_ids
            string? imdbId = null;
            var extUrl = $"https://api.themoviedb.org/3/tv/{tmdbId}/external_ids?api_key={apiKey}";
            try
            {
                var extResp = await _httpClient.GetAsync(extUrl);
                if (extResp.IsSuccessStatusCode)
                {
                    var extJson = await JsonSerializer.DeserializeAsync<JsonElement>(await extResp.Content.ReadAsStreamAsync());
                    imdbId = extJson.TryGetProperty("imdb_id", out var eid) ? eid.GetString() : null;
                }
            }
            catch { }

            // Get main credit (networks/creator)
            string? mainCredit = null;
            string? mainCreditRole = null;
            if (json.TryGetProperty("networks", out var networks) && networks.GetArrayLength() > 0)
            {
                var firstNetwork = networks[0];
                mainCredit = firstNetwork.TryGetProperty("name", out var n) ? n.GetString() : null;
                mainCreditRole = "Network";
            }
            else if (json.TryGetProperty("created_by", out var creators) && creators.GetArrayLength() > 0)
            {
                var firstCreator = creators[0];
                mainCredit = firstCreator.TryGetProperty("name", out var c) ? c.GetString() : null;
                mainCreditRole = "Creator";
            }

            return new TmdbTvShowResult
            {
                PosterUrl = posterPath != null ? $"https://image.tmdb.org/t/p/original{posterPath}" : null,
                BackdropUrl = backdropPath != null ? $"https://image.tmdb.org/t/p/original{backdropPath}" : null,
                ImdbId = imdbId,
                MainCredit = mainCredit,
                MainCreditRole = mainCreditRole,
                FirstAirDate = firstAirDate
            };
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching TMDB TV data for {TmdbId}", tmdbId); }
        return null;
    }

    public async Task<TmdbSeasonResult?> FetchTmdbSeasonAsync(string tmdbId, int seasonNumber, string apiKey)
    {
        var url = $"https://api.themoviedb.org/3/tv/{tmdbId}/season/{seasonNumber}?api_key={apiKey}";
        try
        {
            var resp = await _httpClient.GetAsync(url);
            if (!resp.IsSuccessStatusCode) return null;

            var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
            var episodeCount = json.TryGetProperty("episodes", out var episodes) ? episodes.GetArrayLength() : 0;
            var result = new TmdbSeasonResult { EpisodeCount = episodeCount };

            if (episodeCount > 0 && episodes.GetArrayLength() > 0)
            {
                foreach (var ep in episodes.EnumerateArray())
                {
                    var epNum = ep.TryGetProperty("episode_number", out var en) ? en.GetInt32() : 0;
                    var airDate = ep.TryGetProperty("air_date", out var ad) ? ad.GetString() : null;
                    result.Episodes.Add(new TmdbEpisodeInfo { EpisodeNumber = epNum, AirDate = airDate });
                }
            }

            return result;
        }
        catch (Exception ex) { _logger.LogError(ex, "Error fetching TMDB season data"); }
        return null;
    }

    // ======================== MOVIES ========================

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

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        // Upload poster blob if we have a TMDB image URL
        Dictionary<string, object>? posterBlob = null;
        if (!string.IsNullOrEmpty(posterUrl))
            posterBlob = (await UploadBlobAsync(config, posterUrl))?.Blob;

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
        if (posterBlob != null) item["poster"] = posterBlob;

        var itemOk = await PostRecordAsync(config, "social.popfeed.feed.listItem", item);

        if (itemOk)
            _logger.LogInformation("Logged watch for {Title} to Popfeed", movieTitle);

        return itemOk;
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

                // Filter to only watched_movies listType
                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("value", out var value)) continue;

                    // Check listType is watched_movies
                    if (!value.TryGetProperty("listType", out var lt) || lt.GetString() != "watched_movies")
                        continue;

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

    // ======================== TV SHOWS ========================

    public async Task<string?> FindTvShowInListAsync(PluginConfiguration config, string tmdbId, string listUri)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return null;

        // Extract rkey from the end of listUri to find items, but we search by tmdbId
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
                    return null;
                }

                var json = await JsonSerializer.DeserializeAsync<JsonElement>(await resp.Content.ReadAsStreamAsync());
                if (!json.TryGetProperty("records", out var records)) return null;

                foreach (var record in records.EnumerateArray())
                {
                    if (!record.TryGetProperty("value", out var value)) continue;

                    // Must match both listUri AND tmdbId
                    if (!value.TryGetProperty("listUri", out var lu) || lu.GetString() != listUri)
                        continue;

                    if (value.TryGetProperty("identifiers", out var ids) &&
                        ids.TryGetProperty("tmdbId", out var tid))
                    {
                        if (string.Equals(tid.GetString(), tmdbId, StringComparison.OrdinalIgnoreCase))
                        {
                            var uri = record.TryGetProperty("uri", out var u) ? u.GetString() : null;
                            _logger.LogInformation("Found TV show {TmdbId} in list, record URI: {Uri}", tmdbId, uri);
                            return uri; // Returns the record URI (which includes rkey) for deletion
                        }
                    }
                }
                return null;
            }
            catch (Exception ex)
            {
                if (attempt == 0 && await TryRefreshTokenAsync(config))
                    continue;
                _logger.LogError(ex, "Error finding TV show in list");
                return null;
            }
        }
        return null;
    }

    public async Task<bool> CreateTvShowListItemAsync(
        PluginConfiguration config,
        string listUri,
        string listType,
        string title,
        string? tmdbId,
        string? imdbId,
        string? releaseDate,
        List<string>? genres,
        string? mainCredit,
        string? mainCreditRole,
        string? posterUrl,
        string? backdropUrl,
        Dictionary<string, object>? posterBlob)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;

        var identifiers = new Dictionary<string, object>();
        if (!string.IsNullOrEmpty(tmdbId)) identifiers["tmdbId"] = tmdbId;
        if (!string.IsNullOrEmpty(imdbId)) identifiers["imdbId"] = imdbId;

        var now = DateTime.UtcNow.ToString("o", CultureInfo.InvariantCulture);

        var item = new Dictionary<string, object>
        {
            ["$type"] = "social.popfeed.feed.listItem",
            ["identifiers"] = identifiers,
            ["creativeWorkType"] = "tv_show",
            ["title"] = title,
            ["listUri"] = listUri,
            ["listType"] = listType,
            ["addedAt"] = now
        };
        if (!string.IsNullOrEmpty(releaseDate)) item["releaseDate"] = releaseDate;
        if (genres != null && genres.Count > 0) item["genres"] = genres;
        if (!string.IsNullOrEmpty(mainCredit)) item["mainCredit"] = mainCredit;
        if (!string.IsNullOrEmpty(mainCreditRole)) item["mainCreditRole"] = mainCreditRole;
        if (!string.IsNullOrEmpty(posterUrl)) item["posterUrl"] = posterUrl;
        if (!string.IsNullOrEmpty(backdropUrl)) item["backdropUrl"] = backdropUrl;
        if (posterBlob != null) item["poster"] = posterBlob;

        var ok = await PostRecordAsync(config, "social.popfeed.feed.listItem", item);
        if (ok)
            _logger.LogInformation("Created TV show listItem for {Title} in {ListType}", title, listType);

        return ok;
    }

    public async Task<bool> DeleteListItemAsync(PluginConfiguration config, string recordUri)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken)) return false;
        if (string.IsNullOrEmpty(recordUri)) return false;

        // Extract rkey from AT URI: at://did:plc:xxx/social.popfeed.feed.listItem/rkey
        var rkey = recordUri.Substring(recordUri.LastIndexOf('/') + 1);
        if (string.IsNullOrEmpty(rkey)) return false;

        var body = new { repo = config.AtProtocolDid, collection = "social.popfeed.feed.listItem", rkey };
        var url = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.deleteRecord";

        try
        {
            var jsonContent = new StringContent(JsonSerializer.Serialize(body), Encoding.UTF8, "application/json");
            using var req = new HttpRequestMessage(HttpMethod.Post, url);
            req.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            req.Content = jsonContent;
            var resp = await _httpClient.SendAsync(req);
            return resp.IsSuccessStatusCode;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting listItem {Rkey}", rkey);
            return false;
        }
    }

    // ======================== BLOB UPLOAD ========================

    public async Task<BlobResult?> UploadBlobAsync(PluginConfiguration config, string imageUrl)
    {
        if (string.IsNullOrEmpty(config.AtProtocolAccessToken) || string.IsNullOrEmpty(imageUrl))
            return null;

        try
        {
            // Step 1: Download the image
            var imageBytes = await _httpClient.GetByteArrayAsync(imageUrl);
            if (imageBytes == null || imageBytes.Length == 0) return null;

            // Step 2: Determine MIME type from URL
            var mimeType = "image/jpeg";
            if (imageUrl.EndsWith(".png", StringComparison.OrdinalIgnoreCase))
                mimeType = "image/png";
            else if (imageUrl.EndsWith(".webp", StringComparison.OrdinalIgnoreCase))
                mimeType = "image/webp";

            // Step 3: Upload to PDS
            var uploadUrl = $"https://{config.AtProtocolPdsHost}/xrpc/com.atproto.repo.uploadBlob";
            using var uploadReq = new HttpRequestMessage(HttpMethod.Post, uploadUrl);
            uploadReq.Headers.Authorization = new AuthenticationHeaderValue("Bearer", config.AtProtocolAccessToken);
            uploadReq.Content = new ByteArrayContent(imageBytes);
            uploadReq.Content.Headers.ContentType = new MediaTypeHeaderValue(mimeType);

            var uploadResp = await _httpClient.SendAsync(uploadReq);
            if (!uploadResp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Blob upload failed: {Status}", uploadResp.StatusCode);
                return null;
            }

            var uploadJson = await JsonSerializer.DeserializeAsync<JsonElement>(await uploadResp.Content.ReadAsStreamAsync());
            _logger.LogInformation("Blob uploaded successfully");

            // Convert the response to a Dictionary matching the AT Protocol blob format
            var blobDict = JsonSerializer.Deserialize<Dictionary<string, object>>(uploadJson.GetRawText());
            return new BlobResult { Blob = blobDict };
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Blob upload failed (non-critical, continuing without blob)");
            return null;
        }
    }

    // ======================== TOKEN REFRESH ========================

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

    // ======================== SHARED HELPERS ========================

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

                if (respBody.Contains("ExpiredToken") && attempt == 0)
                {
                    if (await TryRefreshTokenAsync(config))
                        continue;
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
}