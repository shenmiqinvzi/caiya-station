"""Before/after sheet: the full sticker (marker.png) vs the minimap art (marker-map.png).

Renders both sprites at the sizes the minimap actually draws a POI icon (24-64 px)
on a map-like backdrop, upscaled 3x with NEAREST so the pixels are visible.
"""
from __future__ import annotations

import argparse
from pathlib import Path

from PIL import Image

SIZES = (24, 32, 40, 48, 64)
ZOOM = 3
GAP = 14
LABEL_H = 26
CELL = max(SIZES) * ZOOM
BACKDROP = (58, 64, 54, 255)
COLUMNS = ("marker.png (full sticker, panel + receipt)", "marker-map.png (minimap, new)")


def load(path: Path) -> Image.Image:
    art = Image.open(path).convert("RGBA")
    box = art.getbbox()
    return art.crop(box) if box else art


def strip(art: Image.Image) -> Image.Image:
    width = len(SIZES) * (CELL + GAP) + GAP
    height = CELL + GAP * 2
    sheet = Image.new("RGBA", (width, height), BACKDROP)
    for index, size in enumerate(SIZES):
        scaled = art.copy()
        scaled.thumbnail((size, size), Image.LANCZOS)
        big = scaled.resize((scaled.width * ZOOM, scaled.height * ZOOM), Image.NEAREST)
        x = GAP + index * (CELL + GAP) + (CELL - big.width) // 2
        y = GAP + (CELL - big.height) // 2
        sheet.alpha_composite(big, (x, y))
    return sheet


def main() -> None:
    parser = argparse.ArgumentParser()
    parser.add_argument("--full", default="mod/marker.png")
    parser.add_argument("--map", default="mod/marker-map.png")
    parser.add_argument("--out", default="artifacts/inspection/marker-before-after.png")
    args = parser.parse_args()

    from PIL import ImageDraw

    rows = []
    for column, path in zip(COLUMNS, (args.full, args.map)):
        art = load(Path(path))
        band = Image.new("RGBA", (len(SIZES) * (CELL + GAP) + GAP, LABEL_H), BACKDROP)
        ImageDraw.Draw(band).text((GAP, 7), f"{column}   {art.width}x{art.height} source", fill=(226, 232, 240, 255))
        rows.append(band)
        rows.append(strip(art))

    width = max(row.width for row in rows)
    height = sum(row.height for row in rows) + GAP
    sheet = Image.new("RGBA", (width, height), BACKDROP)
    y = 0
    for row in rows:
        sheet.alpha_composite(row, (0, y))
        y += row.height + GAP

    out = Path(args.out)
    out.parent.mkdir(parents=True, exist_ok=True)
    sheet.save(out)
    print(f"wrote {out} {sheet.width}x{sheet.height}")


if __name__ == "__main__":
    main()
