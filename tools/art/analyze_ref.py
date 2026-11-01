#!/usr/bin/env python
"""Measure the reference's small-size icon: class bboxes and per-row extents.

The reference sheet's "小尺寸效果" sample is the authority for the marker's
geometry. Earlier attempts failed because the flat card background got classified
as one of the art colours, which made the content box equal the whole canvas and
turned the per-row table into noise. Here the background is taken from the corner
pixel and matched first, then every other pixel is assigned to the nearest palette
entry; only then are extents computed, normalised to the art bbox.
"""

import os
import sys

from PIL import Image

# Palette sampled from the reference itself (see sample_marker.py output and the
# earlier per-band sampling in the task notes).
PALETTE = [
    ("halo", (252, 240, 178)),
    ("outline", (41, 28, 25)),
    ("cap", (134, 88, 58)),
    ("cap_patch", (222, 168, 96)),
    ("cap_bill", (104, 66, 46)),
    ("face", (247, 228, 202)),
    ("eye", (32, 21, 20)),
    ("beak", (246, 152, 34)),
    ("collar", (168, 122, 86)),
    ("wing", (226, 160, 92)),
    ("awning_orange", (233, 140, 64)),
    ("awning_cream", (246, 215, 186)),
    ("shop", (54, 35, 25)),
    ("window", (201, 161, 124)),
    ("mullion", (163, 110, 71)),
    ("door", (86, 57, 43)),
    ("step", (240, 158, 58)),
    ("pointer", (246, 191, 66)),
]


def classify(pixel, background):
    r, g, b = pixel[0], pixel[1], pixel[2]
    if abs(r - background[0]) <= 10 and abs(g - background[1]) <= 10 and abs(b - background[2]) <= 10:
        return "background"
    best = None
    best_distance = 1 << 30
    for name, colour in PALETTE:
        distance = (r - colour[0]) ** 2 + (g - colour[1]) ** 2 + (b - colour[2]) ** 2
        if distance < best_distance:
            best_distance = distance
            best = name
    return best


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "artifacts/inspection/ref-icon-zoom.png"
    if not os.path.exists(path):
        raise SystemExit("missing " + path)
    image = Image.open(path).convert("RGB")
    width, height = image.size
    pixels = image.load()
    background = pixels[0, 0]
    print("image", width, height, "background", background)

    labels = [[classify(pixels[x, y], background) for x in range(width)] for y in range(height)]

    min_x, min_y, max_x, max_y = width, height, -1, -1
    for y in range(height):
        row = labels[y]
        for x in range(width):
            if row[x] != "background":
                if x < min_x:
                    min_x = x
                if y < min_y:
                    min_y = y
                if x > max_x:
                    max_x = x
                if y > max_y:
                    max_y = y
    box = (min_x, min_y, max_x + 1, max_y + 1)
    art_w = max_x + 1 - min_x
    art_h = max_y + 1 - min_y
    print("art bbox", box, "size", art_w, art_h, "aspect %.4f" % (art_w / art_h))

    print()
    print("class extents (normalised to the art bbox)")
    for name, _ in PALETTE:
        cx0 = cy0 = 1 << 30
        cx1 = cy1 = -1
        count = 0
        for y in range(min_y, max_y + 1):
            row = labels[y]
            for x in range(min_x, max_x + 1):
                if row[x] == name:
                    count += 1
                    if x < cx0:
                        cx0 = x
                    if y < cy0:
                        cy0 = y
                    if x > cx1:
                        cx1 = x
                    if y > cy1:
                        cy1 = y
        if count == 0:
            print("%-14s  absent" % name)
            continue
        print("%-14s  count=%6d  x %.3f..%.3f  y %.3f..%.3f"
              % (name, count,
                 (cx0 - min_x) / art_w, (cx1 + 1 - min_x) / art_w,
                 (cy0 - min_y) / art_h, (cy1 + 1 - min_y) / art_h))

    print()
    print("row extents (normalised; every 0.02)")
    step = max(1, int(round(art_h * 0.02)))
    for offset in range(0, art_h, step):
        y = min_y + offset
        row = labels[y]
        xs = [x for x in range(min_x, max_x + 1) if row[x] != "background"]
        if not xs:
            continue
        ny = offset / art_h
        names = sorted({row[x] for x in xs})
        print("y %.2f  x %.3f..%.3f  width %.3f  %s"
              % (ny, (min(xs) - min_x) / art_w, (max(xs) + 1 - min_x) / art_w,
                 (max(xs) + 1 - min(xs)) / art_w, ",".join(names)))


if __name__ == "__main__":
    main()
