# Jellyfin2PopFeed

Automatically log watched movies to your [PopFeed](https://popfeed.social) Diary and Watched Movies library via AT Protocol. No manual steps -- finish a movie on Jellyfin, it shows up on PopFeed with poster art, director, and metadata.

*Vibe-coded with ❤️*

## Features

- **Auto-log movies** - Finish a movie (90%+), it logs to your PopFeed diary automatically
- **Dedup by TMDb ID** - Never double-posts. Checks existing listItem records before creating a new one
- **TMDB poster art** - Posters and backdrops are fetched from TMDb and stored as permanent CDN URLs (requires free TMDb API key)
- **Director credit** - Director name extracted from TMDb credits API
- **Full metadata** - Title, year, TMDb ID, IMDB ID, genres, release date, genres, main credit, poster, backdrop
- **Dashboard settings** - Configure handle, password, PDS host, TMDb API key from Jellyfin Dashboard
- **Persistent config** - Settings survive page refresh, back navigation, and server restarts
- **Global account** - One PopFeed account for your whole Jellyfin server

## What gets created

The plugin creates a single `social.popfeed.feed.listItem` record on your PDS with:

| Field | Example |
|-------|---------|
| identifiers.tmdbId | "980431" |
| creativeWorkType | "movie" |
| title | "Avatar Aang: The Last Airbender" |
| listType | "watched_movies" |
| posterUrl | https://image.tmdb.org/t/p/original/....jpg |
| mainCredit | "Matt Shakman" |
| mainCreditRole | "Directed by" |
| genres | ["Science Fiction", "Adventure"] |
| releaseDate | "2025-07-22" |
| addedAt | 2026-08-21T23:50:10Z |

The PopFeed AppView generates the Activity feed entry from the listItem record.

## Installation

### Via Plugin Repository (Recommended)

1. In Jellyfin, go to **Dashboard > Plugins > Repositories**
2. Click **+** and add this URL:
   ```
   https://raw.githubusercontent.com/didiermortier/jellyfin2popfeed-plugin/main/manifest.json
   ```
3. Go to **Catalog**, find **Jellyfin2PopFeed**, click **Install**
4. Restart Jellyfin

### Manual Installation

1. Download the DLL from the [latest release](https://github.com/didiermortier/jellyfin2popfeed-plugin/releases)
2. Copy it to your plugins folder:
   - Linux: `/var/lib/jellyfin/plugins/Jellyfin2PopFeed/`
   - Docker: mount or copy into `/jellyfin/jellyfin-web/plugins/Jellyfin2PopFeed/`
3. Restart Jellyfin

## Configuration

1. Go to **Dashboard > Plugins > Jellyfin2PopFeed**
2. Enter your **AT Protocol Handle** (e.g., `user.bsky.social` or `user.popfeed.social`)
3. Enter your **Password** (Bluesky App Password or PDS password)
4. Set **PDS Host** to `popfeed.social` (or your PDS)
5. **TMDB API Key** (optional but recommended): Get a free key at [themoviedb.org/settings/api](https://www.themoviedb.org/settings/api). Required for poster images and director names.
6. Click **Authenticate & Save** -- the plugin discovers your Watched Movies list automatically
7. Click **Test Connection** to verify
8. Toggle **Auto-log watched movies** on or off

## Troubleshooting

**"Error processing request" after update:**
Clear the plugin cache and reinstall:
```bash
sudo systemctl stop jellyfin
sudo rm -rf /var/lib/jellyfin/plugins/Jellyfin2PopFeed/
sudo systemctl start jellyfin
```
Then re-add the repository URL and install again.

**Plugin not showing up in Catalog:**
Remove the repository and add it again to refresh the manifest.

**Poster images not showing:**
Make sure you entered a valid TMDb API Key. The plugin fetches posters from TMDb's API.

## How It Works

1. A movie playback session ends in Jellyfin
2. The plugin checks if the movie was watched to **90%+** completion (matches Jellyfin's own max-resume threshold)
3. It looks up the movie's metadata (title, year, TMDb ID, genres)
4. If a TMDb API key is set, it fetches poster URL, backdrop URL, IMDB ID, and director from TMDb
5. It queries your PDS for existing `social.popfeed.feed.listItem` records with the same TMDb ID
6. If no match is found, it creates a `social.popfeed.feed.listItem` via `com.atproto.repo.createRecord`
7. The movie appears in your PopFeed Diary and Watched Movies library.

## Building from Source

```bash
# Install .NET 9.0 SDK, then:
git clone https://github.com/didiermortier/jellyfin2popfeed-plugin.git
cd jellyfin2popfeed-plugin
dotnet build Jellyfin.Plugin.Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.csproj
```

## PopFeed Lexicon

This plugin writes to the `social.popfeed.feed.listItem` collection. For more details, see the [PopFeed Community repo](https://github.com/Popfeed-Social/Popfeed-Community/tree/main/lexicons).

## Roadmap

- [x] Movie support -- Diary, Watched Movies, Activity
- [x] Poster image upload via TMDb API
- [x] Director credit from TMDb credits API
- [x] Duplicate detection by TMDb ID
- [x] Dashboard settings with persistent config
- [ ] TV show support (per-show, not per-episode)
- [ ] Music album support
- [ ] Customizable log text

## Version History

| Version | Changes |
|---------|---------|
| 1.1.0 | Final stable release. Removed review record (listItem only). 90% threshold. |
| 1.0.9 | Activity fix, thumbnail, threshold 95% -> 90% |
| 1.0.8 | Activity feed fix (listItem + review) |
| 1.0.7 | Full rewrite: listItem-based, TMDb API Key, auto-discover list |
| 1.0.6 | Review-based logging |
| 1.0.5 | Config persistence fix, renamed to one word |
| 1.0.4 - 1.0.2 | Initial releases |

## License

GPL-3.0 -- Because Jellyfin plugins link against GPLv3 code.
