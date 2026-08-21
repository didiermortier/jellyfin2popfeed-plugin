using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Jellyfin.Plugin.Jellyfin2PopFeed.Configuration;

namespace Jellyfin.Plugin.Jellyfin2PopFeed.Services;

public class AuthResult
{
    public string? Did { get; set; }
    public string? Handle { get; set; }
    public string? AccessToken { get; set; }
    public string? RefreshToken { get; set; }
    public DateTime? Expiry { get; set; }
}

public interface IAtProtocolService
{
    Task<AuthResult?> AuthenticateAsync(string handle, string password, string pdsHost);
    Task<bool> LogMovieWatchAsync(
        PluginConfiguration config,
        string movieTitle,
        int? movieYear,
        string? tmdbId,
        string? releaseDate,
        List<string>? genres,
        string? director);
    Task<bool> MovieWatchExistsAsync(PluginConfiguration config, string? tmdbId, string movieTitle);
    Task<bool> TestConnectionAsync(PluginConfiguration config);
}