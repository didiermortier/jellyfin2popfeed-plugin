# Jellyfin2 PopFeed Plugin

Automatically post watched movies to [PopFeed](https://popfeed.social) via the AT Protocol. When you finish watching a movie on Jellyfin (95%+ completion), the plugin checks if it already exists on your PopFeed feed and posts it if it's new.

Coded with love and a bit of AI magic. 

## Features

- **Automatic posting** - No manual steps. Finish a movie, it posts.
- **Duplicate detection** - Checks by TMDb ID (or title as fallback) before posting.
- **Enriched posts** - Posts include movie title, year, TMDb ID, release date, genres, and more (matching the `social.popfeed.feed.note` lexicon).
- **Settings page** - Configure credentials and toggle auto-posting from the Jellyfin Dashboard.
- **Global settings** - One PopFeed account for your whole server.

## Installation

### Via Plugin Repository (Recommended)

1. In Jellyfin, go to **Dashboard > Plugins > Repositories**
2. Click **+** and add this URL:
   ```
   https://raw.githubusercontent.com/didiermortier/jellyfin2popfeed-plugin/main/manifest.json
   ```
3. Go to **Catalog**, find **Jellyfin2 PopFeed**, click **Install**
4. Restart Jellyfin

### Manual Installation

1. Download the DLL from the [latest release](https://github.com/didiermortier/jellyfin2popfeed-plugin/releases)
2. Copy it to your plugins folder:
   - Linux: `/var/lib/jellyfin/plugins/Jellyfin2PopFeed/`
   - Docker: mount or copy into `/jellyfin/jellyfin-web/plugins/Jellyfin2PopFeed/`
3. Restart Jellyfin

## Configuration

1. Go to **Dashboard > Plugins > Jellyfin2 PopFeed**
2. Enter your **AT Protocol Handle** (e.g., `user.bsky.social` or `user.popfeed.social`)
3. Enter an **App Password** (generate one in your Bluesky/Atmosphere settings: Settings > App Passwords)
4. Leave **PDS Host** as `popfeed.social` (or change if using a different PDS)
5. Click **Authenticate & Save**
6. Click **Test Connection** to verify

That's it. Now when you finish a movie, it posts to your PopFeed feed.

## How It Works

1. A playback session ends in Jellyfin
2. The plugin checks if the movie was watched to 95%+ completion
3. It looks up the movie's metadata (title, year, TMDb ID, genres, release date)
4. It queries PopFeed for existing posts with the same TMDb ID
5. If no match is found, it creates a `social.popfeed.feed.note` record via `com.atproto.repo.createRecord`
6. The post appears on PopFeed and (if cross-posted) on Bluesky

## Building from Source

```bash
# Install .NET 9.0 SDK, then:
git clone https://github.com/didiermortier/jellyfin2popfeed-plugin.git
cd jellyfin2popfeed-plugin
dotnet build Jellyfin.Plugin.Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.csproj
```

The compiled DLL will be in:
```
Jellyfin.Plugin.Jellyfin2PopFeed/bin/Release/net9.0/Jellyfin.Plugin.Jellyfin2PopFeed.dll
```

## PopFeed Lexicon

This plugin writes to the `social.popfeed.feed.note` collection on AT Protocol. For more details, see the [PopFeed Community Lexicons](https://github.com/Popfeed-Social/Popfeed-Community/tree/main/lexicons).

## Roadmap

- [x] Movie support (MVP)
- [ ] TV show / episode support
- [ ] Customizable post text
- [ ] Per-user PopFeed accounts
- [ ] Poster image uploads

## License

GPL-3.0 - Because Jellyfin plugins link against GPLv3 code.