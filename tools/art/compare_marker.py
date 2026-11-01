#!/usr/bin/env python
"""Side-by-side proof: the reference sheet's icon vs the generated marker."""

import os

from PIL import Image

ROOT = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))

REF = os.path.join(ROOT, "artifacts", "inspection", "ref-icon-zoom.png")
MINE = os.path.join(ROOT, "mod", "marker-map.png")
OUT = os.path.join(ROOT, "artifacts", "inspection", "marker-compare.png")


def trim(image):
    background = image.getpixel((0, 0))
    mask = Image.new("L", image.size, 0)
    source = image.load()
    target = mask.load()
    for y in range(image.height):
        for x in range(image.width):
            pixel = source[x, y]
            if max(abs(pixel[i] - background[i]) for i in range(3)) > 10:
                target[x, y] = 255
    return image.crop(mask.getbbox())


def square(image, side=256):
    canvas = Image.new("RGBA", (side, side), (0, 0, 0, 0))
    scaled = image.copy()
    scaled.thumbnail((side, side), Image.LANCZOS)
    canvas.alpha_composite(scaled, ((side - scaled.width) // 2, (side - scaled.height) // 2))
    return canvas


def main():
    ref = square(trim(Image.open(REF).convert("RGBA")))
    mine = Image.open(MINE).convert("RGBA")

    sizes = (48, 64, 96, 160)
    gap = 12
    width = sum(sizes) * 2 + gap * (len(sizes) + 1)
    height = max(sizes) + gap * 2
    strip = Image.new("RGBA", (width, height), (28, 32, 38, 255))

    x = gap
    for size in sizes:
        strip.alpha_composite(ref.resize((size, size), Image.LANCZOS), (x, (height - size) // 2))
        x += size + gap
    for size in sizes:
        strip.alpha_composite(mine.resize((size, size), Image.LANCZOS), (x, (height - size) // 2))
        x += size + gap

    strip = strip.resize((strip.width * 2, strip.height * 2), Image.NEAREST)
    strip.save(OUT)
    print("wrote", OUT, strip.size)


if __name__ == "__main__":
    main()
