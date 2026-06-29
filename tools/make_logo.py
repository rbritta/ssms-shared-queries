#!/usr/bin/env python3
"""
Generate the SSMS Shared Queries logo and icons from code - no external design tool.

The mark: a database cylinder drawn as three stacked discs in an "ocean" blue
gradient (only the top disc shows its lid; the bottom is rounded), with the git
"branch" glyph in white sitting on the body. Blue = the database (SQL); the
branch = git version control. The whole product in one mark: "SQL, in git".

Palette and geometry are intentionally simple so the same mark reads at 480px
(README hero) and at 16px (the Tools-menu command and the panel's repo node).

Usage:
    pip install pillow
    python tools/make_logo.py

Outputs (overwritten in place):
    docs/logo.png                               480px - README hero
    src/SsmsSharedQueries/Resources/Icon.png    16px  - Tools-menu command icon
    src/SsmsSharedQueries/Resources/repo.png    32px  - repository node in the tree
"""
import os
from PIL import Image, ImageDraw

# ---- palette ---------------------------------------------------------------
TOP = (46, 111, 176, 255)    # #2E6FB0  top disc (darkest)
MID = (79, 138, 192, 255)    # #4F8AC0  middle disc (the app's DB blue)
BOT = (134, 182, 224, 255)   # #86B6E0  bottom disc (lightest)
EDGE = (33, 66, 95, 255)     # navy outline
WHITE = (250, 252, 255, 255)  # git glyph
HALO = (24, 44, 64, 165)     # subtle dark halo behind the glyph, for contrast

ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
RES = os.path.join(ROOT, "src", "SsmsSharedQueries", "Resources")
DOCS = os.path.join(ROOT, "docs")


def lighten(c, f=0.24):
    r, g, b, a = c
    return (int(r + (255 - r) * f), int(g + (255 - g) * f), int(b + (255 - b) * f), a)


def _dot(d, cx, cy, r, fill):
    d.ellipse([cx - r, cy - r, cx + r, cy + r], fill=fill)


def _disc(d, cx, rx, ry, lidy, bot, body, cap, ow, bry=None):
    """One disc: a short cylinder. Only the visible bottom arc curves (deeper when bry set)."""
    by = bry if bry else ry
    d.ellipse([cx - rx, bot - by, cx + rx, bot + by], fill=body, outline=EDGE, width=ow)
    d.rectangle([cx - rx, lidy, cx + rx, bot], fill=body)
    d.line([(cx - rx, lidy), (cx - rx, bot)], fill=EDGE, width=ow)
    d.line([(cx + rx, lidy), (cx + rx, bot)], fill=EDGE, width=ow)
    d.ellipse([cx - rx, lidy - ry, cx + rx, lidy + ry], fill=cap, outline=EDGE, width=ow)


def render(px):
    """Draw the mark at px*px. Coordinates are authored in a 256 grid and scaled by k."""
    k = px / 256.0
    im = Image.new("RGBA", (px, px), (0, 0, 0, 0))
    d = ImageDraw.Draw(im)
    cx, rx, ry = 128 * k, 84 * k, 19 * k
    ow = max(2, int(round(3.2 * k)))

    # three stacked discs (bottom drawn first so the upper discs overlap their tops)
    _disc(d, cx, rx, ry, 150 * k, 196 * k, BOT, lighten(BOT), ow, bry=28 * k)  # deeper rounded bottom
    _disc(d, cx, rx, ry, 116 * k, 150 * k, MID, lighten(MID), ow)
    _disc(d, cx, rx, ry, 82 * k, 116 * k, TOP, lighten(TOP), ow)
    # subtle rim highlight on the top lid
    d.arc([cx - rx + 10 * k, 82 * k - ry + 4 * k, cx + rx - 10 * k, 82 * k + ry - 4 * k],
          start=185, end=355, fill=lighten(TOP, 0.55), width=max(1, int(round(2 * k))))

    # git-branch glyph, sitting low on the body, just left of centre
    bx = 112 * k
    A = (bx, 194 * k); B = (bx, 124 * k); C = (bx + 46 * k, 148 * k); fork = (bx, 162 * k)

    def branch(color, w, r):
        d.line([A, B], fill=color, width=w)
        d.line([fork, (bx + 17 * k, 160 * k), (bx + 35 * k, 152 * k), C], fill=color, width=w, joint="curve")
        for p in (A, B, C):
            _dot(d, p[0], p[1], r, color)

    branch(HALO, int(19 * k), int(18 * k))
    branch(WHITE, int(12 * k), int(13 * k))
    return im


def to_icon(master, px, margin_frac):
    """Crop to the mark's bounding box, centre it on a square with a small margin, then
    resize. This makes the mark fill the icon (full-bleed) so it matches the visual size
    of the neighbouring menu/tree icons instead of looking small with empty padding."""
    box = master.getbbox()
    cropped = master.crop(box)
    w, h = cropped.size
    side = int(round(max(w, h) * (1 + 2 * margin_frac)))
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    canvas.alpha_composite(cropped, ((side - w) // 2, (side - h) // 2))
    return canvas.resize((px, px), Image.LANCZOS)


def main():
    master = render(1024)  # large supersample, then crop + downscale for crisp, full-bleed output
    to_icon(master, 512, 0.06).save(os.path.join(DOCS, "logo.png"))    # README hero
    to_icon(master, 128, 0.05).save(os.path.join(RES, "repo.png"))     # tree node (WPF downscales w/ HighQuality)
    to_icon(master, 16, 0.05).save(os.path.join(RES, "Icon.png"))      # Tools-menu command bitmap (16x16 format)
    print("wrote docs/logo.png (512), Resources/repo.png (128), Resources/Icon.png (16)")


if __name__ == "__main__":
    main()
