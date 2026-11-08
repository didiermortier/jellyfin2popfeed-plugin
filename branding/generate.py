#!/usr/bin/env python3
"""
Jellyfin2PopFeed mark generator.

Rebuilds the approved square mark (color, black, white) from the real source
logos in sources/:
  - sources/jellyfin-icon-transparent.svg   (Jellyfin's official icon, CC BY-SA 4.0,
    from github.com/jellyfin/jellyfin-ux)
  - sources/popfeed-icon-traced.svg         (PopFeed's real app-icon artwork,
    traced from their App Store icon with potrace)

The mark: a rounded organic "blob" badge, diagonally split into Jellyfin's
gradient (top-left) and PopFeed's indigo (bottom-right), with a simplified
solid silhouette of each brand's icon on top (white), and a "2" straddling the
seam (standing in for "Jellyfin2PopFeed" / "to").

Run with:
    uv run --with resvg-py --with pillow python3 generate.py

Outputs (this directory):
    jellyfin2popfeed-mark.svg / .png        - full color
    jellyfin2popfeed-mark-black.svg / .png  - single-ink, for light backgrounds
    jellyfin2popfeed-mark-white.svg / .png  - single-ink, for dark backgrounds

Do not hand-edit the path data below - it is copied verbatim from the traced
source files. Change SOURCE SVGs instead, or edit only the layout constants
(JF1, JF2, PF, blob_path, shift/scale values).
"""
import os
import re

HERE = os.path.dirname(os.path.abspath(__file__))
SOURCES = os.path.join(HERE, "sources")

# ---- real brand colors (sourced, not guessed) -----------------------------
JF1, JF2 = "#AA5CC3", "#00A4DC"   # Jellyfin's official gradient (jellyfin.org/docs/general/contributing/branding)
PF = "#533EE5"                     # PopFeed's real app-icon background, sampled from their iTunes artwork

# ---- approved layout constants (frozen - matches the signed-off draft) ----
BLOB_PATH = ("M150,10 C218,8 280,48 290,118 C298,178 292,252 218,284 "
             "C150,314 60,298 22,232 C-12,170 -2,84 52,42 "
             "C84,16 116,12 150,10 Z")
JF_SHIFT = (-30, -20)
JF_SCALE = 0.62
JF_BASE_T = (22, 22)  # centers the raw triangle bbox on the 300x300 canvas before shift
PF_SHIFT = (35, 35)
PF_SCALE = 0.75
PF_BASE_T = (-45.0, -48.1)  # centers the raw traced-pin bbox on the 300x300 canvas before shift
TWO_SIZE, TWO_Y = 170, 195


def _load_jf_solid_outer():
    """Extract just the TRUE outer boundary of Jellyfin's triangle (drop the
    inner ring-hole subpath and the center dot) - a solid silhouette reads
    cleaner than the full hollow ring once only half of it is visible."""
    svg = open(os.path.join(SOURCES, "jellyfin-icon-transparent.svg")).read()
    paths = re.findall(r'<path[^>]*d="([^"]+)"[^>]*/>', svg)
    outer = paths[1]  # id="outer-shape" (paths[0] is id="inner-shape", the small center dot - unused)
    subpaths = re.findall(r'M[^M]+?z', outer, re.IGNORECASE)
    return subpaths[0]


def _load_pf():
    svg = open(os.path.join(SOURCES, "popfeed-icon-traced.svg")).read()
    group_transform = re.search(r'<g transform="([^"]+)"', svg).group(1)
    path_d = re.search(r'<path d="([^"]+)"', svg).group(1)
    return group_transform, path_d


JF_SOLID_OUTER = _load_jf_solid_outer()
PF_GROUP_TRANSFORM, PF_PATH_D = _load_pf()


def build_mark(mode="color", size=1024):
    """mode: 'color' | 'black' (for light bg) | 'white' (for dark bg)"""
    jf_cx, jf_cy = (JF_BASE_T[0] + JF_SHIFT[0], JF_BASE_T[1] + JF_SHIFT[1])
    pf_cx, pf_cy = (PF_BASE_T[0] + PF_SHIFT[0], PF_BASE_T[1] + PF_SHIFT[1])

    if mode == "color":
        bg_jf, bg_pf = "url(#jfGrad)", PF
        fg_fill = "#ffffff"
        two_fill, two_stroke = "#0C0E1D", "#ffffff"
    elif mode == "black":
        bg_jf = bg_pf = "#000000"
        fg_fill = "#ffffff"
        two_fill, two_stroke = "#ffffff", "#000000"
    elif mode == "white":
        bg_jf = bg_pf = "#ffffff"
        fg_fill = "#000000"
        two_fill, two_stroke = "#000000", "#ffffff"
    else:
        raise ValueError(mode)

    jf_group = f'<path d="{JF_SOLID_OUTER}" fill="{fg_fill}"/>'
    pf_path = f'<path d="{PF_PATH_D}" fill="{fg_fill}"/>'

    two = (f'<text x="150" y="{TWO_Y}" font-family="Arial, Helvetica, sans-serif" font-size="{TWO_SIZE}" '
           f'font-weight="800" fill="{two_fill}" text-anchor="middle" '
           f'paint-order="stroke" stroke="{two_stroke}" stroke-width="5">2</text>')

    defs_grad = ""
    if mode == "color":
        defs_grad = (f'<linearGradient id="jfGrad" x1="0" y1="0" x2="300" y2="300" gradientUnits="userSpaceOnUse">'
                     f'<stop offset="0" stop-color="{JF1}"/><stop offset="1" stop-color="{JF2}"/></linearGradient>')

    svg = f'''<svg width="{size}" height="{size}" viewBox="0 0 300 300" xmlns="http://www.w3.org/2000/svg">
  <defs>
    {defs_grad}
    <clipPath id="blobClip"><path d="{BLOB_PATH}"/></clipPath>
    <clipPath id="upperLeft"><polygon points="0,0 300,0 0,300"/></clipPath>
    <clipPath id="lowerRight"><polygon points="300,0 300,300 0,300"/></clipPath>
  </defs>
  <g clip-path="url(#blobClip)">
    <polygon points="0,0 300,0 0,300" fill="{bg_jf}"/>
    <polygon points="300,0 300,300 0,300" fill="{bg_pf}"/>
    <g clip-path="url(#upperLeft)">
      <g transform="translate({jf_cx},{jf_cy}) scale({JF_SCALE})">{jf_group}</g>
    </g>
    <g clip-path="url(#lowerRight)">
      <g transform="translate({pf_cx},{pf_cy}) scale({PF_SCALE}) {PF_GROUP_TRANSFORM}">{pf_path}</g>
    </g>
    {two}
  </g>
</svg>'''
    return svg


def main():
    from resvg_py import svg_to_bytes

    names = {"color": "jellyfin2popfeed-mark", "black": "jellyfin2popfeed-mark-black", "white": "jellyfin2popfeed-mark-white"}
    for mode, name in names.items():
        svg = build_mark(mode, size=1024)
        svg_path = os.path.join(HERE, f"{name}.svg")
        with open(svg_path, "w") as f:
            f.write(svg)
        png_bytes = bytes(svg_to_bytes(svg_string=svg))
        png_path = os.path.join(HERE, f"{name}.png")
        with open(png_path, "wb") as f:
            f.write(png_bytes)
        print("wrote", svg_path, "and", png_path)


if __name__ == "__main__":
    main()
