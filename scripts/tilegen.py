"""Generate branded gradient tile images for menu items.

Real food photography can't be synthesised and third-party web images are
copyrighted, so each item gets a generated tile instead: a diagonal gradient in
the item's category colour with the item name typeset over it. Outlet admins can
replace any tile with a real photo later from the app's gallery upload, which
overwrites the same PostgreSQL row.

Writes PNG files to <outDir>/<slug>.png sized for the POS/public menu cards.
"""
from __future__ import annotations

import os
import sys
import textwrap

from PIL import Image, ImageDraw, ImageFilter, ImageFont

# Tiles are landscape: every surface crops with `object-fit: cover`/`BoxFit.cover`
# (web POS ~1.8:1, public QR menu ~1.3:1, Android POS ~2.5:1, catalogue thumb
# 1:1), so the artwork is kept in a centre band that survives all of them.
WIDTH, HEIGHT = 1200, 800
MAX_TEXT_W = 660      # survives the square catalogue thumbnail crop
SAFE_TOP, SAFE_BOTTOM = 190, 620  # survives the 2.5:1 Android POS crop


def format_inr(value: float) -> str:
    """₹ with Indian lakh/crore grouping, matching the app's formatter.

    1234 -> ₹1,234; 123456 -> ₹1,23,456; 12345678 -> ₹1,23,45,678.
    """
    sign = "-" if value < 0 else ""
    whole = int(round(abs(value)))
    text = str(whole)
    if len(text) > 3:
        head, tail = text[:-3], text[-3:]
        pairs = []
        while len(head) > 2:
            pairs.insert(0, head[-2:])
            head = head[:-2]
        if head:
            pairs.insert(0, head)
        text = ",".join(pairs) + "," + tail
    return f"{sign}₹{text}"


def _font(size: int, bold: bool = True) -> ImageFont.FreeTypeFont | ImageFont.ImageFont:
    # Segoe UI ships with Windows; fall back to Pillow's bitmap font otherwise.
    candidates = [
        "C:/Windows/Fonts/segoeuib.ttf" if bold else "C:/Windows/Fonts/segoeui.ttf",
        "C:/Windows/Fonts/arialbd.ttf" if bold else "C:/Windows/Fonts/arial.ttf",
    ]
    for path in candidates:
        if os.path.exists(path):
            return ImageFont.truetype(path, size)
    return ImageFont.load_default()


def _lerp(a: tuple[int, int, int], b: tuple[int, int, int], t: float) -> tuple[int, int, int]:
    return tuple(round(a[i] + (b[i] - a[i]) * t) for i in range(3))


def tile(name: str, top: tuple[int, int, int], bottom: tuple[int, int, int],
         price: float | None = None) -> Image.Image:
    """A landscape gradient tile with the item name typeset over it."""
    small_w, small_h = 300, 200
    grad = Image.new("RGB", (small_w, small_h))
    px = grad.load()
    for y in range(small_h):
        for x in range(small_w):
            # Diagonal ramp so the light falls from the top-left corner.
            t = (x / small_w + y / small_h) / 2
            px[x, y] = _lerp(top, bottom, t)
    img = grad.resize((WIDTH, HEIGHT), Image.LANCZOS).filter(ImageFilter.GaussianBlur(1.2))

    draw = ImageDraw.Draw(img, "RGBA")

    # Soft highlight blob behind the text for legibility on any gradient.
    glow = Image.new("RGBA", (WIDTH, HEIGHT), (0, 0, 0, 0))
    gdraw = ImageDraw.Draw(glow)
    gdraw.ellipse([WIDTH * 0.06, HEIGHT * 0.16, WIDTH * 0.94, HEIGHT * 0.84], fill=(0, 0, 0, 95))
    glow = glow.filter(ImageFilter.GaussianBlur(70))
    img = Image.alpha_composite(img.convert("RGBA"), glow)
    draw = ImageDraw.Draw(img, "RGBA")

    # Thin gold keyline inset from the edge.
    inset = 26
    draw.rounded_rectangle(
        [inset, inset, WIDTH - inset, HEIGHT - inset],
        radius=28,
        outline=(224, 182, 74, 210),
        width=4,
    )

    # Spaces inside brackets are non-breaking so a portion count stays with
    # its item name. Pick the largest size whose lines all fit the safe band.
    import re as _re
    _nb = _re.sub(r"\(([^)]*)\)", lambda m: "(" + m.group(1).replace(" ", "\u00a0") + ")", name)

    lines, font_size = [], 0
    for size in (80, 70, 62, 54, 46):
        cols = max(8, int(MAX_TEXT_W / (0.56 * size)))
        candidate = textwrap.wrap(_nb, width=cols) or [_nb]
        font = _font(size)
        if len(candidate) <= 3 and all(draw.textlength(ln, font=font) <= MAX_TEXT_W for ln in candidate):
            lines, font_size = candidate, size
            break
    if not lines:  # pathologically long name: shrink until it fits
        for size in (46, 40, 34):
            font_size = size
            font = _font(size)
            cols = max(8, int(MAX_TEXT_W / (0.56 * size)))
            lines = textwrap.wrap(_nb, width=cols) or [_nb]
            if len(lines) <= 3 and all(draw.textlength(ln, font=font) <= MAX_TEXT_W for ln in lines):
                break

    font = _font(font_size)
    line_h = int(font_size * 1.22)
    block_h = line_h * len(lines) + (70 if price is not None else 0)
    # Centre the block in the safe band, not the raw canvas.
    y = (SAFE_TOP + SAFE_BOTTOM) / 2 - block_h / 2
    for line in lines:
        w = draw.textlength(line, font=font)
        x = (WIDTH - w) / 2
        draw.text((x + 3, y + 3), line, font=font, fill=(0, 0, 0, 130))  # drop shadow
        draw.text((x, y), line, font=font, fill=(255, 251, 240))
        y += line_h

    if price is not None:
        # INR with lakh/crore grouping, matching the app's formatter.
        pfont = _font(46)
        label = format_inr(price)
        w = draw.textlength(label, font=pfont)
        x = (WIDTH - w) / 2
        py = y + 8
        draw.text((x + 2, py + 2), label, font=pfont, fill=(0, 0, 0, 130))
        draw.text((x, py), label, font=pfont, fill=(240, 205, 118))

    return img.convert("RGB")


def main(out_dir: str, spec_path: str) -> None:
    """Read `slug\\tname\\trgb_top\\trgb_bottom` lines and write tiles."""
    os.makedirs(out_dir, exist_ok=True)
    made = 0
    with open(spec_path, encoding="utf-8") as fh:
        for raw in fh:
            line = raw.strip()
            if not line or line.startswith("#"):
                continue
            slug, name, top, bottom = line.split("\t")
            top_rgb = tuple(int(v) for v in top.split(","))
            bottom_rgb = tuple(int(v) for v in bottom.split(","))
            img = tile(name, top_rgb, bottom_rgb)
            img.save(os.path.join(out_dir, f"{slug}.png"), optimize=True)
            made += 1
    print(f"generated {made} tiles in {out_dir}")


if __name__ == "__main__":
    main(sys.argv[1], sys.argv[2])
