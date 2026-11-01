#!/usr/bin/env python
"""Lift the reference sheet's small-size icon straight out of the source image.

Hand-redrawing the duck kept drifting away from the reference: the head came out
too wide, the beak turned into a lump, and the cap flattened into a band. The
reference sheet already contains the exact icon we want, labelled "小尺寸效果
(如地图缩放时)", so the faithful move is to extract that artwork instead of
approximating it.

Steps: crop the icon from the sheet, key the flat card background out to
transparency with a flood fill from the border, square it into a 256 canvas with
the reference's own aspect, and save. A light unsharp pass compensates for the
upscale so the texture stays crisp at 48 px.

Usage:
    python tools/art/extract_marker.py            # writes mod/marker-map.png
    python tools/art/extract_marker.py --preview  # also writes inspection crops
"""

import argparse
import os

from PIL import Image, ImageDraw, ImageFilter

# The sheet is 1536x1024. The "小尺寸效果" sample sits in the right column; its art
# occupies (874,690)-(1006,839) in sheet coordinates (measured from the 4x zoom crop).
CROP = (874, 689, 1007, 840)

BACKGROUND_TOLERANCE = 26


def key_background(image):
    """Flood-fill the flat card background from the border into transparency."""
    image = image.convert("RGBA")
    width, height = image.size
    pixels = image.load()
    seed = pixels[0, 0]
    stack = []
    for x in range(width):
        stack.append((x, 0))
        stack.append((x, height - 1))
    for y in range(height):
        stack.append((0, y))
        stack.append((width - 1, y))

    seen = [[False] * width for _ in range(height)]
    while stack:
        x, y = stack.pop()
        if x < 0 or y < 0 or x >= width or y >= height:
            continue
        if seen[y][x]:
            continue
        seen[y][x] = True
        r, g, b, a = pixels[x, y]
        if abs(r - seed[0]) > BACKGROUND_TOLERANCE:
            continue
        if abs(g - seed[1]) > BACKGROUND_TOLERANCE:
            continue
        if abs(b - seed[2]) > BACKGROUND_TOLERANCE:
            continue
        pixels[x, y] = (0, 0, 0, 0)
        stack.append((x + 1, y))
        stack.append((x - 1, y))
        stack.append((x, y + 1))
        stack.append((x, y - 1))
    return image


def content_box(image):
    alpha = image.getchannel("A")
    return alpha.getbbox()


def size_test(marker, out_dir):
    """Proof strip: the shipped texture at the sizes the minimap actually uses."""
    sizes = (16, 24, 32, 40, 48, 64, 96)
    gap = 16
    for name, background in (("dark", (26, 29, 34, 255)), ("light", (206, 217, 232, 255))):
        width = sum(sizes) + gap * (len(sizes) + 1)
        strip = Image.new("RGBA", (width, 128), background)
        x = gap
        for size in sizes:
            strip.alpha_composite(marker.resize((size, size), Image.LANCZOS), (x, (128 - size) // 2))
            x += size + gap
        strip = strip.resize((strip.width * 2, strip.height * 2), Image.NEAREST)
        path = os.path.join(out_dir, "marker-size-test-" + name + ".png")
        strip.save(path)
        print("wrote", path)


def main():
    parser = argparse.ArgumentParser(description="Extract the reference's small-size marker icon.")
    parser.add_argument("--source", default=None, help="reference sheet png/webp")
    parser.add_argument("--out", default=None, help="output png (default mod/marker-map.png)")
    parser.add_argument("--preview", action="store_true", help="also write inspection crops")
    args = parser.parse_args()

    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    source = args.source or os.path.join(
        os.path.expanduser("~"), ".local", "state", "billion-context", "retrieve", "img",
        "session-34ed1754-025d-43f7-9065-ae47c2250e9c", "m00002-ba793f89.webp")
    if not os.path.exists(source):
        raise SystemExit("missing reference sheet: " + source)

    out = args.out or os.path.join(root, "mod", "marker-map.png")

    sheet = Image.open(source).convert("RGBA")
    icon = sheet.crop(CROP)
    icon = key_background(icon)
    box = content_box(icon)
    if box is None:
        raise SystemExit("nothing left after keying the background")
    icon = icon.crop(box)
    print("extracted", icon.size, "from", CROP, "content", box)

    # Square it into a 256 canvas, keeping the reference's own aspect so the pointer
    # tip is not stretched.
    base = 256
    art_w, art_h = icon.size
    scale = min(base / art_w, base / art_h)
    size = (max(1, int(round(art_w * scale))), max(1, int(round(art_h * scale))))
    icon = icon.resize(size, Image.LANCZOS)
    icon = icon.filter(ImageFilter.UnsharpMask(radius=1.4, percent=38, threshold=2))

    canvas = Image.new("RGBA", (base, base), (0, 0, 0, 0))
    canvas.alpha_composite(icon, ((base - size[0]) // 2, (base - size[1]) // 2))
    canvas.save(out)
    print("wrote", out, canvas.size, os.path.getsize(out), "bytes")

    out_dir = os.path.join(root, "artifacts", "inspection")
    os.makedirs(out_dir, exist_ok=True)
    size_test(canvas, out_dir)

    if args.preview:
        out_dir = os.path.join(root, "artifacts", "inspection")
        os.makedirs(out_dir, exist_ok=True)
        raw = sheet.crop(CROP)
        raw = raw.resize((raw.width * 6, raw.height * 6), Image.NEAREST)
        raw_path = os.path.join(out_dir, "marker-extract-source.png")
        raw.save(raw_path)
        print("wrote", raw_path)
        canvas.resize((base * 3, base * 3), Image.NEAREST).save(
            os.path.join(out_dir, "marker-extract-3x.png"))
        print("wrote marker-extract-3x.png")


if __name__ == "__main__":
    main()
