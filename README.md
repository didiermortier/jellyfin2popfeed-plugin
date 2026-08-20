# Jellyfin2 PopFeed Plugin

**Automatically post your watched movies from Jellyfin to PopFeed via AT Protocol**

---

## ⭐ Features

- ✅ Auto-post movies to PopFeed when you finish watching
- ✅ Per-user configuration (each user connects their own PopFeed account)
- ✅ Secure AT Protocol authentication
- ✅ Prevents duplicate posts (checks if movie already exists)
- ✅ Dashboard configuration in Jellyfin
- ✅ Works with TMDB and IMDb movie IDs

---

## 📥 Installation (Easy Method)

### Step 1: Add Repository to Jellyfin

1. Open your **Jellyfin Dashboard** (web interface)
2. Go to **Plugins** (in the left sidebar)
3. Click the **Repositories** tab
4. Click **+ Add Repository**
5. **Paste this URL:**
   ```
   https://raw.githubusercontent.com/didiermortier/jellyfin2popfeed-plugin/main/manifest.json
   ```
6. Click **Save**

### Step 2: Install the Plugin

1. Still in **Plugins**, go to the **Catalog** tab
2. Find **"Jellyfin2 PopFeed"** in the list
3. Click **Install**
4. **Restart Jellyfin** (important!)

### Step 3: Configure Your Account

1. After restart, go back to **Plugins**
2. Find **Jellyfin2 PopFeed** in the **Installed Plugins** list
3. Click **Configure** (the gear icon)
4. Enter your **AT Protocol Handle** (e.g., `username.popfeed.social`)
5. Enter your **AT Protocol Password**
6. Click **Connect to PopFeed**
7. If successful, you'll see "Connected" status
8. Make sure **"Automatically post watched movies"** is checked
9. Click **Save Settings**

---

## 🎬 How It Works

1. You watch a movie in Jellyfin
2. When you finish watching, Jellyfin fires a "playback stopped" event
3. The plugin checks if you've already posted this movie to PopFeed
4. If not, it creates a new post on your PopFeed with:
   - Movie title
   - Release year
   - Director (if available)
   - TMDB/IMDb IDs for identification

---

## 🔧 Requirements

- **Jellyfin Server**: 10.11.0 or higher
- **AT Protocol Account**: You need a PopFeed account (or any AT Protocol service)
- **.NET 9.0**: Required to build from source (not needed if installing from repository)

---

## 🐛 Troubleshooting

### Plugin not showing up?
- Make sure you **restarted Jellyfin** after installation
- Check the **Jellyfin server logs** for errors
- Verify the manifest URL is correct

### Connection failed?
- Double-check your **AT Protocol handle** (should include `.popfeed.social` or your PDS)
- Verify your **password** is correct
- Try changing the **PDS Host** to `popfeed.social` if using PopFeed

### Posts not appearing?
- Make sure **"Automatically post watched movies"** is enabled
- Check that you're **connected** to PopFeed
- Look in **Jellyfin server logs** for errors
- The plugin only posts when you **fully finish** watching (not if you stop midway)

---

## 🛠️ Manual Installation (Advanced)

If the repository method doesn't work, you can manually install:

1. **Download the DLL**: Get the latest release from [GitHub Releases](https://github.com/didiermortier/jellyfin2popfeed-plugin/releases)
2. **Create plugin folder**:
   ```bash
   mkdir -p /var/lib/jellyfin/plugins/Jellyfin2PopFeed/
   ```
3. **Copy the DLL**: Place `Jellyfin.Plugin.Jellyfin2PopFeed.dll` in that folder
4. **Set permissions**:
   ```bash
   chown jellyfin:jellyfin /var/lib/jellyfin/plugins/Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.dll
   chmod 644 /var/lib/jellyfin/plugins/Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.dll
   ```
5. **Restart Jellyfin**:
   ```bash
   sudo systemctl restart jellyfin
   ```

---

## 📦 Building from Source

### Prerequisites
- .NET 9.0 SDK
- Git

### Build Steps

```bash
# Clone the repository
git clone https://github.com/didiermortier/jellyfin2popfeed-plugin.git
cd jellyfin2popfeed-plugin

# Build the plugin
dotnet build Jellyfin.Plugin.Jellyfin2PopFeed/Jellyfin.Plugin.Jellyfin2PopFeed.csproj --configuration Release

# The DLL will be at:
# Jellyfin.Plugin.Jellyfin2PopFeed/bin/Release/net9.0/Jellyfin.Plugin.Jellyfin2PopFeed.dll
```

Then follow the **Manual Installation** steps above.

---

## 📄 License

MIT License - Copyright (c) 2026 Didier Mortier

---

## 🤝 Contributing

Pull requests are welcome! Please open an issue first for major changes.

---

## 📞 Support

- **GitHub Issues**: [Report a bug](https://github.com/didiermortier/jellyfin2popfeed-plugin/issues)
- **Jellyfin Forum**: Check the plugins section

---

**Plugin GUID**: `5f3e8a1c-2b7d-4e9f-a6c3-d1e4f8a2b9c5`
