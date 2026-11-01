#!/usr/bin/env python
"""Measure the reference sheet's small-size icon, band by band.

Reads artifacts/inspection/ref-icon-zoom.png (the reference sheet's own "小尺寸效果"
sample, upscaled), takes the most common colour as the card background, finds the
sticker's bounding box, and then walks down in rows reporting two things per row:

  * the silhouette extent (left..right) of non-background pixels, and
  * each flat colour's extent, in normalised content-box coordinates.

Those numbers replace the hand-placed guesses in tools/art/make_marker.py.
"""

import os
from collections import Counter

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "artifacts", "inspection", "ref-icon-zoom.png")

# Flat colours sampled off the reference sheet, with a tight per-channel tolerance
# so neighbouring shades (cap vs cap brim, awning cream vs face) stay distinct.
CLASSES = {
    "halo":   ((252, 239, 176), 14),
    "outline": ((38, 24, 22), 18),
    "cap":    ((140, 92, 60), 22),
    "capband": ((206, 154, 96), 22),
    "brim":   ((92, 60, 42), 20),
    "face":   ((249, 233, 208), 14),
    "beak":   ((246, 152, 34), 26),
    "wing":   ((226, 160, 96), 24),
    "awnor":  ((233, 138, 64), 22),
    "awncr":  ((246, 215, 187), 12),
    "shop":   ((74, 50, 40), 18),
    "window": ((201, 161, 124), 22),
    "mullion": ((150, 108, 78), 16),
    "door":   ((88, 58, 44), 16),
    "step":   ((240, 158, 58), 20),
    "pointer": ((247, 190, 66), 18),
}

STEP = 8          # rows between samples, in source pixels


def nearest(rgb):
    best, best_d = None, None
    for name, (target, tol) in CLASSES.items():
        d = max(abs(rgb[i] - target[i]) for i in range(3))
        if d <= tol and (best_d is None or d < best_d):
            best, best_d = name, d
    return best


def main():
    image = Image.open(SRC).convert("RGBA")
    width, height = image.size
    pixels = image.load()

    common = Counter(image.getdata()).most_common(1)[0][0]
    print("source", image.size, "background", common)

    def is_background(pixel):
        return all(abs(pixel[i] - common[i]) <= 10 for i in range(3)) and pixel[3] >= 200

    xs, ys = [], []
    for y in range(height):
        for x in range(width):
            if not is_background(pixels[x, y]):
                xs.append(x)
                ys.append(y)
    x0, x1, y0, y1 = min(xs), max(xs) + 1, min(ys), max(ys) + 1
    cw, ch = x1 - x0, y1 - y0
    print(f"content bbox {x0},{y0} -> {x1},{y1}  size {cw}x{ch}  aspect {cw / ch:.3f}")

    def nx(x):
        return (x - x0) / cw

    def ny(y):
        return (y - y0) / ch

    print()
    print("rows: normalised y | silhouette left-right | per-colour extents")
    for y in range(y0, y1, STEP):
        spans = {}
        for x in range(x0, x1):
            name = nearest(pixels[x, y][:3])
            if name:
                lo, hi = spans.get(name, (x, x))
                spans[name] = (min(lo, x), max(hi, x))
        if not spans:
            continue
        left = min(v[0] for v in spans.values())
        right = max(v[1] for v in spans.values())
        parts = " ".join(
            f"{name}:{nx(lo):.2f}-{nx(hi + 1):.2f}" for name, (lo, hi) in sorted(spans.items())
        )
        print(f"  y{ny(y):.3f} [{nx(left):.2f}-{nx(right + 1):.2f}] {parts}")

    print()
    print("vertical extents per colour (normalised):")
    for name in CLASSES:
        rows = []
        for y in range(y0, y1):
            for x in range(x0, x1):
                if nearest(pixels[x, y][:3]) == name:
                    rows.append(y)
                    break
        if rows:
            print(f"  {name}: y{ny(min(rows)):.3f}-{ny(max(rows) + 1):.3f}")
        else:
            print(f"  {name}: none")


if __name__ == "__main__":
    main()
