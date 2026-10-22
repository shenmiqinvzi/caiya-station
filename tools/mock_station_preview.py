"""Concept previews for the CaiYaStation kiosk and the region-entry banner.

This is a deterministic isometric reconstruction of the primitives in
src/CaiYaStation/StationVisuals.cs plus the notice banner in StationWorld.cs.
It is a design mock, not a runtime screenshot.
"""
from __future__ import annotations

import math
import random
from pathlib import Path

from PIL import Image, ImageDraw, ImageFilter, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "docs" / "images"
OUT.mkdir(parents=True, exist_ok=True)

W, H = 1600, 1000
SS = 2
CW, CH = W * SS, H * SS
SCALE = 200.0
CX = CW * 0.5
CY = CH * 0.70
ISO_ANGLE = math.radians(24)
COS30 = math.cos(ISO_ANGLE)
SIN30 = math.sin(ISO_ANGLE)


def set_angle(degrees):
    global ISO_ANGLE, COS30, SIN30
    ISO_ANGLE = math.radians(degrees)
    COS30 = math.cos(ISO_ANGLE)
    SIN30 = math.sin(ISO_ANGLE)

# Palette from StationVisuals / UiTheme (linear-ish sRGB values).
FRAME = (41, 51, 59)
DARK = (20, 26, 31)
METAL = (148, 163, 173)
SIGN = (61, 219, 133)
USED = (97, 102, 107)
BEAK = (247, 168, 46)
GLOW = (255, 209, 107)
FLAG = (51, 184, 117)
WOOD = (84, 54, 33)
CREAM = (224, 209, 168)
ROOF = (23, 38, 36)
PAPER = (240, 230, 199)
ROPE = (107, 77, 41)
RED = (184, 46, 36)
GRASS = (39, 58, 38)
EARTH = (48, 38, 28)
DIRT = (72, 58, 40)
BURLAP = (148, 117, 79)

TEXT = (235, 250, 240)
DIM = (168, 194, 178)
ACCENT = (82, 230, 148)
WARN = (255, 194, 92)
DANGER = (255, 122, 107)


def font(size: int, bold: bool = False) -> ImageFont.FreeTypeFont:
    candidates = [
        r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc",
        r"C:\Windows\Fonts\msyhbd.ttc",
        r"C:\Windows\Fonts\msyh.ttc",
        r"C:\Windows\Fonts\simhei.ttf",
    ]
    for candidate in candidates:
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def mul(color, factor):
    return tuple(max(0, min(255, int(c * factor))) for c in color[:3])


def mix(a, b, t):
    t = max(0.0, min(1.0, t))
    return tuple(int(a[i] + (b[i] - a[i]) * t) for i in range(3))


def over(base: Image.Image, layer: Image.Image):
    base.alpha_composite(layer)


def project(x, y, z):
    return (
        CX + (x - z) * COS30 * SCALE,
        CY + ((x + z) * SIN30 - y) * SCALE,
    )


def normal3(a, b, c):
    ux, uy, uz = b[0] - a[0], b[1] - a[1], b[2] - a[2]
    vx, vy, vz = c[0] - a[0], c[1] - a[1], c[2] - a[2]
    nx, ny, nz = uy * vz - uz * vy, uz * vx - ux * vz, ux * vy - uy * vx
    length = math.sqrt(nx * nx + ny * ny + nz * nz) or 1.0
    return nx / length, ny / length, nz / length


LIGHT = (-0.42, 0.86, 0.33)
LIGHT_LEN = math.sqrt(sum(v * v for v in LIGHT))
LIGHT = tuple(v / LIGHT_LEN for v in LIGHT)
VIEW = (0.72, 0.62, 0.64)


def shade(color, normal, emissive=False):
    diff = max(0.0, sum(normal[i] * LIGHT[i] for i in range(3)))
    factor = (1.12 if emissive else 0.52) + (0.55 if not emissive else 0.20) * diff
    return mul(color, factor)


def rotate_point(point, center, rx=0.0, ry=0.0, rz=0.0):
    x, y, z = point[0] - center[0], point[1] - center[1], point[2] - center[2]
    if rx:
        c, s = math.cos(rx), math.sin(rx)
        y, z = y * c - z * s, y * s + z * c
    if ry:
        c, s = math.cos(ry), math.sin(ry)
        x, z = x * c + z * s, -x * s + z * c
    if rz:
        c, s = math.cos(rz), math.sin(rz)
        x, y = x * c - y * s, x * s + y * c
    return x + center[0], y + center[1], z + center[2]


def rotate_normal(normal, rx=0.0, ry=0.0, rz=0.0):
    x, y, z = normal
    if rx:
        c, s = math.cos(rx), math.sin(rx)
        y, z = y * c - z * s, y * s + z * c
    if ry:
        c, s = math.cos(ry), math.sin(ry)
        x, z = x * c + z * s, -x * s + z * c
    if rz:
        c, s = math.cos(rz), math.sin(rz)
        x, y = x * c - y * s, x * s + y * c
    return x, y, z


FACES = [
    ((4, 5, 6, 7), (0, 0, 1)),
    ((1, 0, 3, 2), (0, 0, -1)),
    ((5, 1, 2, 6), (1, 0, 0)),
    ((0, 4, 7, 3), (-1, 0, 0)),
    ((7, 6, 2, 3), (0, 1, 0)),
    ((0, 1, 5, 4), (0, -1, 0)),
]


def box(center, size, color, rx=0.0, ry=0.0, rz=0.0, emissive=False):
    cx, cy, cz = center
    sx, sy, sz = (s / 2 for s in size)
    verts = [
        (-sx, -sy, -sz), (sx, -sy, -sz), (sx, sy, -sz), (-sx, sy, -sz),
        (-sx, -sy, sz), (sx, -sy, sz), (sx, sy, sz), (-sx, sy, sz),
    ]
    verts = [rotate_point((v[0] + cx, v[1] + cy, v[2] + cz), center, rx, ry, rz) for v in verts]
    out = []
    for indices, normal in FACES:
        world_normal = rotate_normal(normal, rx, ry, rz)
        if sum(world_normal[i] * VIEW[i] for i in range(3)) <= 0.001:
            continue
        points = [project(*verts[i]) for i in indices]
        depth = sum(verts[i][0] + verts[i][1] * 0.9 + verts[i][2] for i in indices) / len(indices)
        out.append(("face", depth, points, shade(color, world_normal, emissive), world_normal))
    return out


def cylinder_y(center, radius, height, color, segments=14, emissive=False):
    cx, cy, cz = center
    top = cy + height / 2
    bottom = cy - height / 2
    ring_top = [(cx + radius * math.cos(2 * math.pi * i / segments), top,
                 cz + radius * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
    ring_bottom = [(p[0], bottom, p[2]) for p in ring_top]
    out = []
    for i in range(segments):
        j = (i + 1) % segments
        mid = 2 * math.pi * (i + 0.5) / segments
        normal = (math.cos(mid), 0.0, math.sin(mid))
        if sum(normal[k] * VIEW[k] for k in range(3)) <= 0.001:
            continue
        points = [project(*ring_bottom[i]), project(*ring_bottom[j]), project(*ring_top[j]), project(*ring_top[i])]
        depth = (ring_bottom[i][0] + ring_bottom[i][2] + ring_top[j][0] + ring_top[j][2]) / 2 + cy
        out.append(("face", depth, points, shade(color, normal, emissive), normal))
    out.append(("face", top + cy, [project(*p) for p in ring_top], shade(color, (0, 1, 0), emissive), (0, 1, 0)))
    return out


def cylinder_x(center, radius, length, color, segments=12, emissive=False):
    cx, cy, cz = center
    left = cx - length / 2
    right = cx + length / 2
    ring_left = [(left, cy + radius * math.cos(2 * math.pi * i / segments),
                  cz + radius * math.sin(2 * math.pi * i / segments)) for i in range(segments)]
    ring_right = [(right, p[1], p[2]) for p in ring_left]
    out = []
    for i in range(segments):
        j = (i + 1) % segments
        mid = 2 * math.pi * (i + 0.5) / segments
        normal = (0.0, math.cos(mid), math.sin(mid))
        if sum(normal[k] * VIEW[k] for k in range(3)) <= 0.001:
            continue
        points = [project(*ring_left[i]), project(*ring_left[j]), project(*ring_right[j]), project(*ring_right[i])]
        depth = (ring_left[i][1] + ring_left[i][2] + ring_right[j][1] + ring_right[j][2]) / 2 + cx
        out.append(("face", depth, points, shade(color, normal, emissive), normal))
    out.append(("face", right + cy, [project(*ring_right[i]) for i in range(segments)], shade(color, (1, 0, 0), emissive), (1, 0, 0)))
    return out


def sphere(center, radius, color, glow=False, flatten=1.0):
    depth = center[0] + center[1] * 0.9 + center[2]
    return [("sphere", depth, center, radius, color, glow, flatten)]


def build_items(closed=False):
    items = []
    add = items.extend

    # Diorama platform (grass top, earth sides) and a dirt approach.
    add(box((0, -0.38, 0), (10.8, 0.72, 9.0), EARTH))
    add(box((0, -0.02, 0), (10.8, 0.08, 9.0), GRASS))
    add(box((0, 0.035, 2.1), (2.4, 0.02, 5.0), DIRT))
    for index in range(7):
        add(box((0.15 * math.sin(index), 0.055, 2.6 - index * 0.62), (1.5 - index * 0.08, 0.015, 0.28),
                mul(DIRT, 1.12 + 0.02 * index)))

    # Station foundation and step.
    add(box((0, 0.06, 0), (3.8, 0.12, 2.9), DARK))
    add(box((0, 0.11, 1.55), (2.2, 0.10, 0.55), METAL))
    add(box((0, 0.13, 0), (3.35, 0.06, 2.45), WOOD))

    # Posts and back wall.
    for x in (-1.60, 1.60):
        for z in (-1.10, 1.10):
            add(cylinder_y((x, 1.60, z), 0.09, 3.20, WOOD))
            add(box((x, 3.16, z), (0.18, 0.08, 0.18), METAL))
    add(box((0, 1.58, -1.03), (3.10, 2.50, 0.14), FRAME))
    add(box((0, 0.36, -0.96), (3.00, 0.24, 0.10), DARK))
    for index in range(9):
        x = -1.36 + index * 0.34
        add(box((x, 1.58, -0.94), (0.055, 2.34, 0.035), mul(WOOD, 1.08)))

    # Counter and shelf.
    add(box((0, 0.58, 0.58), (2.50, 1.00, 0.80), WOOD))
    add(box((0, 1.10, 0.58), (2.70, 0.12, 0.95), METAL))
    add(box((0, 0.62, 0.995), (2.35, 0.86, 0.04), FRAME))
    add(box((0, 0.24, 1.02), (2.35, 0.05, 0.03), METAL))
    add(box((0, 1.00, 1.02), (2.35, 0.05, 0.03), METAL))
    add(box((0, 0.18, 0.58), (2.40, 0.06, 0.75), DARK))
    add(box((0, 1.86, -0.82), (2.25, 0.08, 0.36), WOOD))
    add(sphere((0.86, 1.18, 0.55), 0.06, GLOW, glow=True))

    # Parcels on the counter / shelf.
    for center, size, ry in [
        ((-0.78, 1.22, 0.55), (0.40, 0.27, 0.32), 8),
        ((-0.30, 1.20, 0.50), (0.30, 0.20, 0.26), -12),
        ((-0.72, 2.02, -0.82), (0.36, 0.24, 0.28), 4),
        ((-0.28, 2.00, -0.82), (0.28, 0.18, 0.24), -8),
    ]:
        add(box(center, size, PAPER, ry=math.radians(ry)))
        add(box((center[0], center[1] + size[1] / 2 + 0.012, center[2]),
                (size[0] * 1.03, 0.015, size[2] * 0.30), mul(PAPER, 0.72)))
    add(box((0.55, 2.02, -0.80), (0.50, 0.36, 0.04), PAPER))
    add(box((0.55, 2.08, -0.77), (0.38, 0.03, 0.02), RED))
    add(box((0.55, 2.00, -0.77), (0.38, 0.03, 0.02), DARK))

    # Striped awning.
    for index in range(8):
        x = -1.49 + index * 0.425
        material = SIGN if index % 2 == 0 else CREAM
        add(box((x, 2.60, 0.84), (0.425, 0.055, 1.42), material, rx=math.radians(-8)))
    add(box((0, 2.50, 1.54), (3.42, 0.22, 0.06), FRAME))
    add(box((-1.72, 2.50, 0.84), (0.06, 0.22, 1.42), FRAME))
    add(box((1.72, 2.50, 0.84), (0.06, 0.22, 1.42), FRAME))

    # Peaked roof, ridge and vent.
    add(box((0, 3.08, 0.72), (3.90, 0.10, 1.55), ROOF, rx=math.radians(-12)))
    add(box((0, 3.08, -0.72), (3.90, 0.10, 1.55), ROOF, rx=math.radians(12)))
    add(cylinder_x((0, 3.25, 0), 0.08, 3.90, ROOF))
    add(box((0, 2.99, 1.46), (3.90, 0.06, 0.08), METAL))
    add(box((0, 2.99, -1.46), (3.90, 0.06, 0.08), METAL))
    add(box((0, 2.93, 1.47), (3.90, 0.035, 0.03), SIGN))
    add(box((0.95, 3.28, -0.55), (0.28, 0.34, 0.28), DARK))
    add(box((0.95, 3.47, -0.55), (0.36, 0.06, 0.36), METAL))

    # Front fascia sign and duck mark.
    sign_color = USED if closed else SIGN
    add(box((0, 2.98, 1.46), (2.50, 0.78, 0.10), DARK))
    add(box((0, 2.98, 1.51), (2.30, 0.66, 0.06), sign_color, emissive=not closed))
    add(box((0, 3.32, 1.55), (2.30, 0.035, 0.03), METAL))
    add(box((0, 2.64, 1.55), (2.30, 0.035, 0.03), METAL))
    add(box((-1.15, 2.98, 1.55), (0.035, 0.66, 0.03), METAL))
    add(box((1.15, 2.98, 1.55), (0.035, 0.66, 0.03), METAL))
    add(box((-1.08, 3.24, 1.48), (0.05, 0.28, 0.08), METAL))
    add(box((1.08, 3.24, 1.48), (0.05, 0.28, 0.08), METAL))
    add(sphere((-0.80, 3.00, 1.555), 0.18, USED if closed else CREAM, flatten=0.55))
    add(box((-0.61, 2.96, 1.58), (0.22, 0.08, 0.08), BEAK if not closed else USED))
    add(sphere((-0.72, 3.08, 1.58), 0.024, DARK))

    # Back-wall notice board (no duplicate title).
    add(box((0, 2.30, -0.90), (1.30, 0.70, 0.08), DARK))
    add(box((0, 2.30, -0.84), (1.12, 0.52, 0.04), PAPER))
    add(box((-0.12, 2.40, -0.81), (0.66, 0.025, 0.02), DARK))
    add(box((-0.02, 2.28, -0.81), (0.82, 0.025, 0.02), DARK))
    add(box((-0.16, 2.16, -0.81), (0.58, 0.025, 0.02), RED))

    # Hanging sign.
    add(cylinder_y((-0.38, 2.38, 1.28), 0.018, 0.40, ROPE))
    add(cylinder_y((0.38, 2.38, 1.28), 0.018, 0.40, ROPE))
    add(box((0, 2.04, 1.30), (1.05, 0.68, 0.08), DARK))
    add(box((0, 2.04, 1.35), (0.92, 0.56, 0.04), sign_color, emissive=not closed))

    # Flag pole, cloth and little duck mark.
    add(cylinder_y((-1.78, 2.00, -0.95), 0.06, 4.00, METAL))
    add(sphere((-1.78, 4.02, -0.95), 0.06, METAL))
    flag_color = USED if closed else FLAG
    add(box((-1.30, 3.54, -0.95), (0.94, 0.62, 0.035), flag_color))
    add(sphere((-1.41, 3.57, -0.93), 0.10, CREAM, flatten=0.35))
    add(box((-1.30, 3.55, -0.91), (0.10, 0.04, 0.02), BEAK if not closed else USED))

    # Lanterns.
    for x in (-1.26, 1.26):
        add(cylinder_y((x, 2.39, 1.18), 0.018, 0.36, ROPE))
        add(cylinder_y((x, 2.23, 1.18), 0.06, 0.03, METAL))
        add(sphere((x, 2.18, 1.18), 0.12, GLOW if not closed else USED, glow=not closed))
        add(cylinder_y((x, 2.06, 1.18), 0.06, 0.03, METAL))

    # Closed shutter and its text plate.
    if closed:
        add(box((0, 1.30, 1.04), (2.30, 1.15, 0.06), USED))
        for index in range(4):
            add(box((0, 0.88 + index * 0.28, 1.08), (2.26, 0.025, 0.02), DARK))

    # Props.
    for center, size, yaw in [
        ((-2.15, 0.34, 1.62), (0.56, 0.56, 0.56), 16),
        ((-2.13, 0.84, 1.56), (0.42, 0.42, 0.42), -10),
    ]:
        add(box(center, size, WOOD, ry=math.radians(yaw)))
        add(box((center[0], center[1] + 0.02, center[2] + size[2] * 0.505),
                (size[0] * 0.94, size[1] * 0.12, 0.02), DARK))
        add(box((center[0] + size[0] * 0.505, center[1] + size[1] * 0.26, center[2]),
                (0.02, size[1] * 0.20, size[2] * 0.66), PAPER))
    add(cylinder_y((1.98, 0.48, 1.02), 0.38, 0.92, WOOD))
    add(cylinder_y((1.98, 0.78, 1.02), 0.395, 0.08, METAL))
    add(cylinder_y((1.98, 0.18, 1.02), 0.395, 0.08, METAL))
    add(sphere((1.38, 0.38, 1.72), 0.28, BURLAP, flatten=1.15))
    add(cylinder_y((1.38, 0.68, 1.72), 0.13, 0.06, ROPE))
    add(box((1.38, 0.48, 1.94), (0.24, 0.16, 0.02), PAPER))
    add(box((1.30, 0.48, 1.955), (0.09, 0.025, 0.01), RED))

    # A small fence along the back edge of the platform.
    for index in range(-3, 4):
        x = index * 1.35
        add(cylinder_y((x, 0.48, -4.25), 0.055, 0.96, ROPE))
    add(box((0, 0.72, -4.25), (9.2, 0.08, 0.08), ROPE))
    add(box((0, 0.42, -4.25), (9.2, 0.06, 0.06), mul(ROPE, 0.8)))

    # Two trees for scale, kept dark so the kiosk stays the focus.
    for x, z, r in [(-4.35, -2.5, 0.62), (4.18, -3.0, 0.50)]:
        add(cylinder_y((x, 0.85, z), 0.12, 1.70, (44, 34, 25)))
        add(sphere((x, 2.05, z), r, (26, 46, 34)))
        add(sphere((x - 0.2, 2.20, z + 0.1), r * 0.72, (31, 54, 39)))

    return items


def draw_background():
    image = Image.new("RGBA", (CW, CH), (6, 12, 10, 255))
    draw = ImageDraw.Draw(image)
    top = (6, 13, 11)
    bottom = (22, 43, 34)
    for y in range(CH):
        t = y / (CH - 1)
        draw.line((0, y, CW, y), fill=mix(top, bottom, t * 0.95))
    glow = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow)
    gd.ellipse((CX - 620, CY - 760, CX + 620, CY - 60), fill=(34, 84, 62, 90))
    gd.ellipse((CX - 360, CY - 540, CX + 360, CY + 40), fill=(56, 122, 82, 55))
    glow = glow.filter(ImageFilter.GaussianBlur(90))
    over(image, glow)
    rng = random.Random(11)
    for _ in range(90):
        x = rng.randint(0, CW)
        y = rng.randint(0, int(CH * 0.55))
        radius = rng.choice((1, 1, 2))
        alpha = rng.randint(18, 60)
        draw.ellipse((x, y, x + radius, y + radius), fill=(180, 230, 200, alpha))
    return image


def draw_ground(image, items):
    # Soft shadow under the diorama platform.
    shadow = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    corners = [project(-5.6, -0.72, -4.8), project(5.6, -0.72, -4.8),
               project(5.6, -0.72, 4.8), project(-5.6, -0.72, 4.8)]
    sd.polygon(corners, fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(30))
    over(image, shadow)

    # Footprint shadows for the kiosk and props, offset toward the light.
    shadow = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    for center, size, yaw in [
        ((0, 0.0, 0.35), (4.6, 0.0, 4.2), 0),
        ((-1.98, 0.0, 0.92), (0.9, 0, 0.9), 0),
        ((1.98, 0.0, 1.02), (0.9, 0, 0.9), 0),
        ((1.52, 0.0, 1.48), (0.7, 0, 0.7), 0),
    ]:
        cx, _, cz = center
        sx = size[0] / 2
        sz = size[2] / 2
        pts = [project(cx - sx + 0.35, 0.04, cz - sz + 0.25),
               project(cx + sx + 0.35, 0.04, cz - sz + 0.25),
               project(cx + sx + 0.35, 0.04, cz + sz + 0.25),
               project(cx - sx + 0.35, 0.04, cz + sz + 0.25)]
        sd.polygon(pts, fill=(0, 0, 0, 105))
    shadow = shadow.filter(ImageFilter.GaussianBlur(18))
    over(image, shadow)

    # Grass tufts along the platform top.
    rng = random.Random(5)
    layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    ld = ImageDraw.Draw(layer)
    for _ in range(260):
        x = rng.uniform(-5.2, 5.2)
        z = rng.uniform(-4.3, 4.3)
        if abs(x) < 2.8 and abs(z) < 2.4:
            continue
        px, py = project(x, 0.05, z)
        length = rng.uniform(4, 11) * SS
        ld.line((px, py, px + rng.uniform(-4, 4), py - length),
                fill=(28, 54, 38, rng.randint(90, 160)), width=max(1, SS // 2))
    over(image, layer)


def draw_items(image, items):
    draw = ImageDraw.Draw(image)
    objects = []
    for item in items:
        if item[0] == "face":
            _, depth, points, color, normal = item
            objects.append((depth, "face", points, color))
        else:
            _, depth, center, radius, color, glow, flatten = item
            objects.append((depth, "sphere", center, radius, color, glow, flatten))
    objects.sort(key=lambda value: value[0])

    glow_layer = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    gd = ImageDraw.Draw(glow_layer)
    for obj in objects:
        if obj[1] == "sphere" and obj[5]:
            _, _, center, radius, color, _, flatten = obj
            px, py = project(*center)
            r = radius * SCALE
            for factor, alpha in ((3.4, 26), (2.2, 45), (1.45, 80)):
                gd.ellipse((px - r * factor, py - r * factor * flatten,
                            px + r * factor, py + r * factor * flatten),
                           fill=(255, 198, 100, alpha))
    glow_layer = glow_layer.filter(ImageFilter.GaussianBlur(16))
    over(image, glow_layer)

    for obj in objects:
        if obj[1] == "face":
            _, _, points, color = obj
            draw.polygon(points, fill=color)
            if len(points) >= 3:
                draw.line(points + [points[0]], fill=mul(color, 0.74), width=max(1, SS // 2))
        else:
            _, _, center, radius, color, glow, flatten = obj
            px, py = project(*center)
            r = radius * SCALE
            # Soft radial shading for lanterns / duck heads / mail sacks.
            steps = max(8, int(r))
            for step in range(steps, 0, -1):
                t = step / steps
                shade_factor = 0.55 + 0.55 * (1.0 - t) ** 0.7
                colour = mul(color, shade_factor)
                draw.ellipse((px - r * t, py - r * t * flatten,
                              px + r * t, py + r * t * flatten), fill=colour)
            draw.ellipse((px - r * 0.30, py - r * 0.34 * flatten,
                          px + r * 0.30, py + r * 0.34 * flatten),
                         fill=mul(color, 1.28))


def sign_texture(closed=False):
    tw, th = 2048, 620
    layer = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    cream = (247, 237, 201, 255)
    # Duck emblem at the left.
    cx, cy, radius = 278, 298, 132
    draw.ellipse((cx - radius, cy - radius, cx + radius, cy + radius), fill=cream)
    draw.ellipse((cx + radius - 16, cy - 30, cx + radius + 88, cy + 56), fill=(247, 168, 46, 255))
    draw.ellipse((cx + radius + 62, cy - 22, cx + radius + 94, cy + 10), fill=(247, 168, 46, 255))
    draw.ellipse((cx + 38, cy - 54, cx + 72, cy - 20), fill=(31, 35, 34, 255))
    draw.arc((cx - radius + 22, cy - 98, cx + radius - 22, cy - 4), 205, 330, fill=(190, 174, 136, 255), width=9)
    # Text.
    title = "菜鸭驿站"
    f = font(316, True)
    bbox = draw.textbbox((0, 0), title, font=f)
    tx = 1160 - (bbox[2] - bbox[0]) / 2
    ty = 300 - (bbox[3] - bbox[1]) / 2 - bbox[1]
    draw.text((tx + 9, ty + 11), title, font=f, fill=(5, 36, 22, 180))
    draw.text((tx, ty), title, font=f, fill=cream)
    # A tiny English footer keeps the board from feeling empty without stealing focus.
    sf = font(46)
    sub = "CAI YA POST"
    sb = draw.textbbox((0, 0), sub, font=sf)
    draw.text((1160 - (sb[2] - sb[0]) / 2, 548), sub, font=sf, fill=(196, 240, 210, 210))
    if closed:
        overlay = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
        od = ImageDraw.Draw(overlay)
        od.rectangle((0, 0, tw, th), fill=(32, 34, 34, 140))
        layer = Image.alpha_composite(layer, overlay)
    return layer


def hanging_texture(closed=False):
    tw, th = 512, 320
    layer = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    f = font(220, True)
    bbox = draw.textbbox((0, 0), "寄", font=f)
    x = (tw - (bbox[2] - bbox[0])) / 2 - bbox[0]
    y = (th - (bbox[3] - bbox[1])) / 2 - bbox[1]
    draw.text((x + 8, y + 9), "寄", font=f, fill=(5, 36, 22, 170))
    draw.text((x, y), "寄", font=f, fill=(247, 237, 201, 255))
    if closed:
        overlay = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
        od = ImageDraw.Draw(overlay)
        od.rectangle((0, 0, tw, th), fill=(40, 42, 42, 120))
        layer = Image.alpha_composite(layer, overlay)
    return layer


def shutter_texture():
    tw, th = 1024, 512
    layer = Image.new("RGBA", (tw, th), (0, 0, 0, 0))
    draw = ImageDraw.Draw(layer)
    f = font(260, True)
    text = "停业"
    bbox = draw.textbbox((0, 0), text, font=f)
    x = (tw - (bbox[2] - bbox[0])) / 2 - bbox[0]
    y = (th - (bbox[3] - bbox[1])) / 2 - bbox[1]
    draw.text((x + 10, y + 12), text, font=f, fill=(15, 18, 17, 190))
    draw.text((x, y), text, font=f, fill=(221, 214, 189, 255))
    draw.line((tw * 0.22, th * 0.78, tw * 0.78, th * 0.78), fill=(154, 52, 42, 210), width=8)
    return layer


def paste_decal(image, texture, p0, p1, p2):
    tw, th = texture.size
    a = (p1[0] - p0[0]) / tw
    b = (p2[0] - p0[0]) / th
    c = p0[0]
    d = (p1[1] - p0[1]) / tw
    e = (p2[1] - p0[1]) / th
    f = p0[1]
    det = a * e - b * d
    if abs(det) < 1e-6:
        return
    ia, ib = e / det, -b / det
    ic = (b * f - e * c) / det
    id_, ie = -d / det, a / det
    if_ = (d * c - a * f) / det
    warped = texture.transform((CW, CH), Image.Transform.AFFINE,
                               (ia, ib, ic, id_, ie, if_), resample=Image.BICUBIC)
    over(image, warped)


def draw_decals(image, closed=False):
    # Front fascia sign face (top-left, top-right, bottom-left in local face space).
    p0 = project(-1.15, 3.31, 1.565)
    p1 = project(1.15, 3.31, 1.565)
    p2 = project(-1.15, 2.65, 1.565)
    paste_decal(image, sign_texture(closed), p0, p1, p2)

    # Hanging sign face (width .92, height .56, z=1.372).
    p0 = project(-0.46, 2.32, 1.372)
    p1 = project(0.46, 2.32, 1.372)
    p2 = project(-0.46, 1.76, 1.372)
    paste_decal(image, hanging_texture(closed), p0, p1, p2)

    if closed:
        p0 = project(-1.15, 1.875, 1.073)
        p1 = project(1.15, 1.875, 1.073)
        p2 = project(-1.15, 0.725, 1.073)
        paste_decal(image, shutter_texture(), p0, p1, p2)


def draw_banner(image, closed=False):
    draw = ImageDraw.Draw(image)
    bw = 1240
    bh = 192
    x = (CW - bw) / 2
    y = 52
    shadow = Image.new("RGBA", (CW, CH), (0, 0, 0, 0))
    sd = ImageDraw.Draw(shadow)
    sd.rounded_rectangle((x + 12, y + 16, x + bw + 12, y + bh + 16), radius=36, fill=(0, 0, 0, 150))
    shadow = shadow.filter(ImageFilter.GaussianBlur(24))
    over(image, shadow)

    accent = WARN if closed else ACCENT
    draw.rounded_rectangle((x, y, x + bw, y + bh), radius=34, fill=(14, 22, 19, 248),
                           outline=(28, 82, 58, 255), width=2 * SS)
    draw.rounded_rectangle((x + 22, y + 36, x + 30, y + bh - 36), radius=4, fill=accent)
    # Icon tile.
    ix, iy, isz = x + 52, y + 44, 104
    draw.rounded_rectangle((ix, iy, ix + isz, iy + isz), radius=26, fill=(12, 28, 22, 255),
                           outline=(40, 100, 72, 255), width=2 * SS)
    icx, icy = ix + isz / 2, iy + isz / 2
    draw.ellipse((icx - 38, icy - 38, icx + 38, icy + 38), fill=(37, 160, 94, 255))
    draw.ellipse((icx - 28, icy - 30, icx + 22, icy + 22), fill=(246, 237, 201, 255))
    draw.ellipse((icx + 12, icy - 10, icx + 44, icy + 14), fill=(247, 168, 46, 255))
    draw.ellipse((icx + 2, icy - 15, icx + 10, icy - 7), fill=(31, 35, 34, 255))
    # Text.
    title = "菜鸭驿站 · 本区域停业" if closed else "菜鸭驿站 · 本区域营业"
    body = "本局驿站已全部停业，下一局再来" if closed else "地图已标记 · 本局 2 个驿站，2 个可用"
    hint = "每站每局只能发货一次" if closed else "靠近后按交互键寄送 · 每站每局一次"
    draw.text((x + 184, y + 26), title, font=font(38, True), fill=TEXT)
    draw.text((x + 184, y + 84), body, font=font(26), fill=DIM)
    draw.text((x + 184, y + 132), hint, font=font(22), fill=accent)
    # Right badge.
    badge = "停业" if closed else "新区域"
    bx, by, bwd, bht = x + bw - 208, y + 40, 160, 46
    draw.rounded_rectangle((bx, by, bx + bwd, by + bht), radius=23, fill=(12, 28, 22, 255),
                           outline=accent, width=2 * SS)
    bb = draw.textbbox((0, 0), badge, font=font(22, True))
    draw.text((bx + bwd / 2 - (bb[2] - bb[0]) / 2, by + bht / 2 - (bb[3] - bb[1]) / 2 - bb[1]),
              badge, font=font(22, True), fill=accent)
    # Thin top light.
    draw.line((x + 34, y + 1, x + bw - 34, y + 1), fill=(105, 210, 150, 100), width=SS)


def add_frame_text(image, title, subtitle):
    draw = ImageDraw.Draw(image)
    x, y = 48 * SS, 38 * SS
    f = font(42, True)
    sf = font(23)
    draw.text((x + 2, y + 3), title, font=f, fill=(0, 0, 0, 150))
    draw.text((x, y), title, font=f, fill=(242, 252, 244))
    draw.text((x, y + 58 * SS), subtitle, font=sf, fill=(180, 204, 188, 230))
    note = "概念预览 · 按代码尺寸/材质重建 · 非游戏截图"
    nf = font(21)
    nb = draw.textbbox((0, 0), note, font=nf)
    draw.text((CW - 48 * SS - (nb[2] - nb[0]), CH - 50 * SS), note, font=nf, fill=(160, 188, 172, 210))


def render(path, closed=False, banner=False, blur_background=False, angle=24):
    set_angle(angle)
    image = draw_background()
    items = build_items(closed)
    draw_ground(image, items)
    draw_items(image, items)
    draw_decals(image, closed)
    if banner:
        draw_banner(image, closed)
    if blur_background:
        blurred = image.filter(ImageFilter.GaussianBlur(7 * SS))
        dark = Image.new("RGBA", (CW, CH), (3, 8, 6, 105))
        image = Image.alpha_composite(blurred, dark)
        if banner:
            draw_banner(image, closed)
    title = "菜鸭驿站 · 已停业" if closed else "菜鸭驿站 · 营业中"
    subtitle = "停业卷帘 / 灯光熄灭 / 招牌变灰" if closed else "双坡屋顶 · 条纹雨棚 · 发光招牌 · 双吊灯 · 包裹道具"
    add_frame_text(image, title, subtitle)
    final = image.convert("RGB").resize((W, H), Image.LANCZOS)
    final.save(path)
    print("wrote", path)


if __name__ == "__main__":
    render(OUT / "station-open.png", closed=False, banner=False, angle=24)
    render(OUT / "station-front.png", closed=False, banner=False, angle=12)
    render(OUT / "station-closed.png", closed=True, banner=False, angle=24)
    render(OUT / "notice-banner.png", closed=False, banner=True, blur_background=True, angle=24)
    render(OUT / "station-notice.png", closed=False, banner=True, angle=24)
