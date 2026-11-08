# Jellyfin2PopFeed branding

The plugin mark: a diagonal mashup of the two real brand icons it bridges,
with a "2" straddling the seam (Jellyfin **2** PopFeed).

## Files

| File | Use |
|---|---|
| `jellyfin2popfeed-mark.svg` / `.png` | full color, square - used as `icon.svg` (repo root) and the plugin's catalog thumbnail (`thumb.png`) |
| `jellyfin2popfeed-mark-black.svg` / `.png` | single-ink black, for light backgrounds (favicons, print) |
| `jellyfin2popfeed-mark-white.svg` / `.png` | single-ink white, for dark backgrounds |
| `generate.py` | regenerates all of the above from `sources/` - single source of truth |
| `sources/` | the real upstream artwork the mark is built from |

Regenerate with:

```
cd branding
uv run --with resvg-py --with pillow python3 generate.py
```

## Sources and licensing

- `sources/jellyfin-icon-transparent.svg` - Jellyfin's official icon, pulled
  verbatim from `github.com/jellyfin/jellyfin-ux` (`branding/SVG/icon-transparent.svg`),
  licensed **CC BY-SA 4.0**. Real gradient (`#AA5CC3` -> `#00A4DC`), not re-created by hand.
- `sources/popfeed-icon-traced.svg` - traced from PopFeed's real App Store icon
  (`sources/popfeed-icon-original.jpg`, via the iTunes Search API) using `potrace`.
  Background color `#533EE5` sampled directly from the icon's pixels.

Jellyfin's branding guide asks that their logo shape not be reused inside another
logo without permission (gradient use is explicitly permitted). This mark does
reuse the actual triangle shape - a deliberate choice for this small fan plugin,
made knowingly after checking the guideline, not an oversight.
