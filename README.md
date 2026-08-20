# Jellyfin2 PopFeed Plugin

A Jellyfin plugin that automatically posts watched movies to PopFeed via AT Protocol.

## Features
- Auto-post movies to PopFeed when watched
- Per-user configuration
- AT Protocol authentication (OAuth)
- Deduplication - checks PopFeed before posting

## Installation
1. Download DLL from Releases
2. Place in `/var/lib/jellyfin/plugins/Jellyfin2PopFeed/`
3. Restart Jellyfin
4. Configure in Dashboard > Plugins > Jellyfin2 PopFeed

## Requirements
- Jellyfin Server 10.11.0+
- .NET 9.0
- AT Protocol account on PopFeed

## Configuration
Each user can configure their own PopFeed account:
- AT Protocol Handle (e.g., @username.popfeed.social)
- PDS Host (defaults to popfeed.social)
- Auto-post movies (enable/disable)

## Technical Details
- Uses `social.popfeed.feed.note` collection
- Checks for existing posts before creating new ones
- Persistent user settings via Jellyfin's IUserDataManager
