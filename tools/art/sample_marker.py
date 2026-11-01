#!/usr/bin/env python
"""Sample the reference's small-size icon at named normalised points.

The reference sheet's "小尺寸效果" sample is the authority for both geometry and
palette. This prints the RGB under a list of normalised points (0..1 across the
trimmed content box), so the generator can use measured colours instead of guesses,
and so a wrong point is obvious: it lands on the wrong element's colour.
"""

import os
import sys

from PIL import Image

POINTS = [
    # name, x, y
    ("cap-crown-left", 0.30, 0.10),
    ("cap-crown-right", 0.66, 0.08),
    ("cap-band", 0.44, 0.13),
    ("cap-bill", 0.72, 0.18),
    ("face-left", 0.40, 0.30),
    ("face-right", 0.70, 0.22),
    ("eye", 0.48, 0.26),
    ("beak-mid", 0.70, 0.31),
    ("beak-tip", 0.79, 0.30),
    ("collar", 0.22, 0.37),
    ("wing", 0.80, 0.36),
    ("awning-orange-1", 0.10, 0.50),
    ("awning-cream-1", 0.22, 0.50),
    ("awning-orange-2", 0.40, 0.50),
    ("awning-cream-2", 0.52, 0.50),
    ("awning-orange-3", 0.72, 0.50),
    ("awning-scallop", 0.10, 0.585),
    ("shop-band", 0.50, 0.625),
    ("window-left", 0.26, 0.71),
    ("window-mullion", 0.30, 0.70),
    ("door", 0.50, 0.66),
    ("step", 0.50, 0.775),
    ("pointer", 0.51, 0.90),
    ("halo", 0.06, 0.50),
    ("outline", 0.02, 0.50),
]


def content_box(image):
    """Trim the flat card background, leaving halo + art."""
    width, height = image.size
    pixels = image.convert("RGBA").load()
    background = pixels[0, 0]
    min_x, min_y, max_x, max_y = width, height, -1, -1
    for y in range(height):
        for x in range(width):
            r, g, b, a = pixels[x, y]
            if a < 8:
                continue
            if abs(r - background[0]) <= 6 and abs(g - background[1]) <= 6 and abs(b - background[2]) <= 6:
                continue
            if x < min_x:
                min_x = x
            if y < min_y:
                min_y = y
            if x > max_x:
                max_x = x
            if y > max_y:
                max_y = y
    return (min_x, min_y, max_x + 1, max_y + 1)


def main():
    path = sys.argv[1] if len(sys.argv) > 1 else "artifacts/inspection/ref-icon-zoom.png"
    if not os.path.exists(path):
        raise SystemExit("missing " + path)
    image = Image.open(path).convert("RGBA")
    box = content_box(image)
    print("content box", box, "size", box[2] - box[0], box[3] - box[1])
    width = box[2] - box[0]
    height = box[3] - box[1]
    pixels = image.load()
    for name, nx, ny in POINTS:
        x = box[0] + int(round(nx * width))
        y = box[1] + int(round(ny * height))
        x = min(max(x, 0), image.size[0] - 1)
        y = min(max(y, 0), image.size[1] - 1)
        r, g, b, a = pixels[x, y]
        print("%-16s (%5.3f,%5.3f) px(%3d,%3d) rgb(%3d,%3d,%3d) a=%d" % (name, nx, ny, x, y, r, g, b, a))


if __name__ == "__main__":
    main()
