# Jellyfin2PopFeed plugin - repo rules

C# / .NET 9 Jellyfin plugin (targets Jellyfin 10.11.x). Writes watched movies and TV shows to PopFeed over AT Protocol. Public repo, owner `didiermortier`.

## Work

- Never work on `main`. Branch `feature/<task>` from `origin/main`, open a PR, wait for Didier's go before merging.
- Build: `dotnet build -c Release` in `Jellyfin.Plugin.Jellyfin2PopFeed/` (dotnet 9 from Homebrew: `brew install dotnet@9`, then `export DOTNET_ROOT=/opt/homebrew/opt/dotnet@9/libexec PATH=/opt/homebrew/opt/dotnet@9/bin:$PATH`). There are no unit tests; CI (`.github/workflows/build.yml`) builds every push and PR.
- Do not release unless asked. A release is a tag, a zip on GitHub and a new top entry in `manifest.json`.

## Hard rules

- `manifest.json`: newest version first in `versions[]`, never drop old entries, every release raises the version, timestamp is the real GitHub `published_at`, checksum is the MD5 of the released zip.
- The csproj `<Version>` and the top manifest entry must match, and the manifest must only describe code that is on `main`.
- Plugin name is one word: `Jellyfin2PopFeed`.
- Every PDS network call needs the two-attempt `ExpiredToken` refresh loop, with a fresh `HttpContent` per attempt (see `PostRecordAsync`).
- The config page is an HTML fragment, never a full document. One `Authenticate & Save` button.
- Create before delete when moving records between lists.
- `icon.svg` and `Jellyfin.Plugin.Jellyfin2PopFeed/thumb.png` are generated from `branding/` (see `branding/README.md`). `thumb.png` is the catalog image the manifest links to, via `main`.
- Secrets never go in the repo. The plugin stores its own credentials in Jellyfin's plugin config, not here.
