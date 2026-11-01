#!/usr/bin/env python
"""ASCII map of the reference sheet's small-size icon, so the layout is readable.

Crops the icon out of artifacts/inspection/ref-small-sample.png (the reference
sheet's own "小尺寸效果" sample, upscaled 4x), downsamples it to a coarse grid, and
prints one character per cell for the nearest flat colour. Reading that grid is how
the band geometry in tools/art/make_marker.py was set.

    python tools/art/map_marker.py            # 44 columns
    python tools/art/map_marker.py --cols 88  # finer
"""

import argparse
import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
SRC = os.path.join(ROOT, "artifacts", "inspection", "ref-small-sample.png")

# The icon's content box inside ref-small-sample.png (measured, see the summary):
# 532x598, aspect 0.889, including the cream halo.
CROP = (258, 361, 790, 959)

CLASSES = [
    ("H", (252, 240, 178)),   # cream halo
    ("@", (41, 28, 25)),      # outline / dark
    ("c", (134, 88, 58)),     # cap
    ("C", (204, 152, 94)),    # cap highlight
    ("b", (104, 66, 46)),     # cap bill
    ("k", (152, 106, 74)),    # collar
    ("w", (216, 146, 74)),    # wing
    ("f", (250, 234, 209)),   # face
    ("o", (246, 152, 34)),    # beak
    ("O", (233, 138, 64)),    # awning orange
    ("p", (246, 215, 187)),   # awning cream
    ("s", (72, 49, 39)),      # shop front
    ("t", (201, 161, 124)),   # window
    ("m", (146, 104, 74)),    # mullion
    ("d", (86, 57, 43)),      # door
    ("S", (240, 158, 58)),    # door step
    ("y", (247, 190, 66)),    # pointer
]


def nearest(rgb):
    best, best_d = ".", 1e9
    for char, target in CLASSES:
        d = sum((rgb[i] - target[i]) ** 2 for i in range(3)) ** 0.5
        if d < best_d:
            best, best_d = char, d
    return best


def main():
    parser = argparse.ArgumentParser()
    parser.add_argument("--cols", type=int, default=44)
    args = parser.parse_args()

    image = Image.open(SRC).convert("RGBA").crop(CROP)
    width, height = image.size
    rows = int(round(args.cols * height / width))
    small = image.resize((args.cols, rows), Image.LANCZOS)

    print("crop", CROP, "->", (width, height), "aspect", round(width / height, 4),
          "grid", args.cols, "x", rows)
    print("legend H halo  @ outline  c cap  C cap-hi  b bill  k collar  w wing  f face  o beak")
    print("       O awning-orange  p awning-cream  s shop  t window  m mullion  d door  S step  y pointer")
    print()

    cells = small.load()
    for y in range(rows):
        row = "".join(nearest(cells[x, y][:3]) if cells[x, y][3] > 24 else " "
                      for x in range(args.cols))
        print(f"{y / rows:0.3f} {row}")


if __name__ == "__main__":
    main()
