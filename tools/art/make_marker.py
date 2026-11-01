#!/usr/bin/env python
"""Generate the minimap marker sticker, drawn for small sizes.

`mod/marker.png` is the full illustration: duck, striped awning, a wooden sign
carrying the four characters "菜鸭驿站", two crates and a suitcase. It reads
beautifully at 256 px, but the minimap POI slot draws it at roughly 24-48 px, and
at that size the sign lettering collapses into an orange smear, the crates vanish,
and the whole icon turns into one bright blob. That is what "the marker looks
ugly" actually meant.

This generator draws the reduced silhouette the reference sheet labels
"小尺寸效果": duck head under a cap, a collar on the left and a wing on the right,
a five-stripe scalloped awning, a shop front with two windows and a centre door,
and the pointer tip. No lettering, no crates, no suitcase.

Bands are normalised to a 214x242 content box (aspect 0.884, the same box the
reference's own small-size sample occupies), so each element sits where the
reference puts it:

    cap crown   y 0.000..0.158
    cap bill    y 0.128..0.242
    face        y 0.108..0.424
    beak        y 0.252..0.400, reaching x 0.905
    collar      y 0.272..0.448 (left)
    wing        y 0.272..0.442 (right)
    awning      y 0.440..0.665, scalloped bottom, full content width
    shop        y 0.665..0.882
    pointer     y 0.872..1.000

The icon is drawn the way the reference is drawn: one dark silhouette for the
whole sticker with each element's colour inset from it, so neighbouring elements
keep a thin dark seam. The union silhouette also gets a cream halo, so the icon
stays crisp on both dark and light minimaps.

The duck sits entirely above the awning -- its beak stays visible -- and the awning
is drawn last only to trim the bottoms of the collar and the wing.

Output is a separate file (`marker-map.png`) so the full sticker stays the logo for
the shipment panel and the delivery receipt.

Usage:
    python tools/art/make_marker.py                  # writes mod/marker-map.png
    python tools/art/make_marker.py --size-test      # also writes the size proof strips
"""

import argparse
import os

from PIL import Image, ImageDraw, ImageFilter

S = 4                       # supersample factor: draw at 4x, reduce with LANCZOS
BASE = 256                  # shipped texture size (the minimap slot is square)
N = BASE * S

CREAM_HALO = (252, 240, 178, 255)
DARK = (41, 28, 25, 255)
CAP = (134, 88, 58, 255)
CAP_HIGHLIGHT = (204, 152, 94, 255)
CAP_BILL = (104, 66, 46, 255)
COLLAR = (152, 106, 74, 255)
FACE = (250, 234, 209, 255)
BEAK = (246, 152, 34, 255)
WING = (216, 146, 74, 255)
AWNING_ORANGE = (233, 138, 64, 255)
AWNING_CREAM = (246, 215, 187, 255)
SHOP = (72, 49, 39, 255)
WINDOW = (201, 161, 124, 255)
WINDOW_MULLION = (146, 104, 74, 255)
DOOR = (86, 57, 43, 255)
DOOR_STEP = (240, 158, 58, 255)
POINTER = (247, 190, 66, 255)

HALO_PX = 7                 # cream band thickness, in 256-space pixels
INSET_PX = 4                # colour inset from the dark silhouette, in 256-space pixels

# Content box 214x242 (aspect 0.884) centred in the 256 square.
W, H = 214, 242
X0, Y0 = 21, 7


def nx(value):
    return X0 + value * W


def ny(value):
    return Y0 + value * H


def px(*values):
    """Scale a 256-space coordinate tuple up to the supersampled canvas."""
    return tuple(int(round(v * S)) for v in values)


def nbox(x0, y0, x1, y1):
    return px(nx(x0), ny(y0), nx(x1), ny(y1))


def npoly(points):
    return [px(nx(x), ny(y)) for x, y in points]


def nrad_x(value):
    """Scale a normalised radius (in content-width units) to canvas pixels."""
    return int(round(value * W * S))


def new_mask():
    return Image.new("L", (N, N), 0)


def add_ellipse(mask, box):
    ImageDraw.Draw(mask).ellipse(box, fill=255)


def add_rrect(mask, box, radius_x):
    ImageDraw.Draw(mask).rounded_rectangle(box, radius=nrad_x(radius_x), fill=255)


def add_rect(mask, box):
    ImageDraw.Draw(mask).rectangle(box, fill=255)


def add_polygon(mask, points):
    ImageDraw.Draw(mask).polygon(points, fill=255)


def grow(mask, radius_px):
    """Dilate by radius_px (256-space) with repeated 3x3 max filters."""
    out = mask
    for _ in range(int(radius_px * S)):
        out = out.filter(ImageFilter.MaxFilter(3))
    return out


def shrink(mask, radius_px):
    """Erode by radius_px (256-space) with repeated 3x3 min filters."""
    out = mask
    for _ in range(int(radius_px * S)):
        out = out.filter(ImageFilter.MinFilter(3))
    return out


def element(canvas, mask, art):
    """Draw one element's colours inset from the dark silhouette.

    The dark silhouette underneath supplies the sticker outline, and the inset
    leaves a thin dark seam wherever two elements meet -- the same construction the
    reference uses, which is what keeps the bill, the beak, the collar and the wing
    readable as separate shapes at 24 px.
    """
    inner = shrink(mask, INSET_PX)
    art.putalpha(Image.composite(art.getchannel("A"), Image.new("L", (N, N), 0), inner))
    canvas.alpha_composite(art)


# ----------------------------------------------------------------------------- bands

POINTER_SHAPE = [(0.375, 0.800), (0.625, 0.800), (0.500, 0.985)]

SHOP_BOX = (0.075, 0.600, 0.925, 0.800)
SHOP_RADIUS = 0.024
WINDOWS = ((0.135, 0.645, 0.405, 0.775), (0.595, 0.645, 0.865, 0.775))
WINDOW_MULLION_WIDTH = 0.013
DOOR_BOX = (0.445, 0.645, 0.555, 0.800)
DOOR_STEP_BOX = (0.445, 0.755, 0.555, 0.800)

CAP_CROWN = (0.220, 0.000, 0.660, 0.165)
CAP_CROWN_RADIUS = 0.090
# A single tan patch sits on the crown's left, the way the reference cap is patched.
CAP_HIGHLIGHT_BOX = (0.258, 0.016, 0.400, 0.092)
CAP_HIGHLIGHT_RADIUS = 0.026
# The bill is a tilted parallelogram: it sits over the forehead and dips to the right.
CAP_BILL_SHAPE = [(0.225, 0.150), (0.700, 0.126), (0.708, 0.202), (0.225, 0.236)]

FACE_BOX = (0.270, 0.110, 0.660, 0.420)

# The beak stays inside the head silhouette: a wedge from the face's right edge out
# to a rounded tip, not a bar sticking past the shoulder.
BEAK_SHAPE = [(0.600, 0.245), (0.690, 0.258), (0.690, 0.385), (0.600, 0.398)]
BEAK_TIP_BOX = (0.660, 0.250, 0.780, 0.392)

EYE_BOX = (0.500, 0.185, 0.552, 0.237)

# Collar and wing are shoulders, not columns: short horizontal bars at chin height.
COLLAR_BOX = (0.040, 0.258, 0.240, 0.400)
COLLAR_RADIUS = 0.050
WING_BOX = (0.720, 0.250, 0.920, 0.390)
WING_RADIUS = 0.048

AWNING_TOP = 0.415
AWNING_BOTTOM = 0.615
AWNING_STRIPES = 5
AWNING_STRIPE_W = 1.0 / AWNING_STRIPES
AWNING_SCALLOP_OVERLAP = 0.56      # scallop radius as a fraction of a stripe's width

AWNING_SCALLOP_R = AWNING_STRIPE_W * AWNING_SCALLOP_OVERLAP      # in content-width units
AWNING_SCALLOP_RY = AWNING_SCALLOP_R * W / H                     # same circle, height units
AWNING_SCALLOP_CY = AWNING_BOTTOM - AWNING_SCALLOP_RY


def pointer_mask():
    mask = new_mask()
    add_polygon(mask, npoly(POINTER_SHAPE))
    return mask


def shop_mask():
    mask = new_mask()
    add_rrect(mask, nbox(*SHOP_BOX), SHOP_RADIUS)
    return mask


def collar_mask():
    mask = new_mask()
    add_rrect(mask, nbox(*COLLAR_BOX), COLLAR_RADIUS)
    return mask


def wing_mask():
    mask = new_mask()
    add_rrect(mask, nbox(*WING_BOX), WING_RADIUS)
    return mask


def face_mask():
    mask = new_mask()
    add_ellipse(mask, nbox(*FACE_BOX))
    return mask


def cap_mask():
    mask = new_mask()
    add_rrect(mask, nbox(*CAP_CROWN), CAP_CROWN_RADIUS)
    add_polygon(mask, npoly(CAP_BILL_SHAPE))
    return mask


def beak_mask():
    mask = new_mask()
    add_polygon(mask, npoly(BEAK_SHAPE))
    add_ellipse(mask, nbox(*BEAK_TIP_BOX))
    return mask


def awning_mask():
    mask = new_mask()
    add_rect(mask, nbox(0.0, AWNING_TOP, 1.0, AWNING_SCALLOP_CY))
    for index in range(AWNING_STRIPES):
        centre = index * AWNING_STRIPE_W + AWNING_STRIPE_W * 0.5
        add_ellipse(mask, nbox(centre - AWNING_SCALLOP_R, AWNING_SCALLOP_CY - AWNING_SCALLOP_RY,
                               centre + AWNING_SCALLOP_R, AWNING_SCALLOP_CY + AWNING_SCALLOP_RY))
    return mask


def silhouette():
    """Union of every element -- the dark sticker body, and the halo's source."""
    mask = new_mask()
    for part in (pointer_mask(), shop_mask(), face_mask(), cap_mask(), beak_mask(),
                 collar_mask(), wing_mask(), awning_mask()):
        mask = Image.composite(part, mask, part)
    return mask


# ------------------------------------------------------------------------------- art

def art_pointer():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(art).polygon(npoly(POINTER_SHAPE), fill=POINTER)
    return art


def art_shop():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    d.rounded_rectangle(nbox(*SHOP_BOX), radius=nrad_x(SHOP_RADIUS), fill=SHOP)
    for box in WINDOWS:
        d.rectangle(nbox(*box), fill=WINDOW)
        centre = (box[0] + box[2]) * 0.5
        d.rectangle(nbox(centre - WINDOW_MULLION_WIDTH * 0.5, box[1],
                         centre + WINDOW_MULLION_WIDTH * 0.5, box[3]), fill=WINDOW_MULLION)
    d.rectangle(nbox(*DOOR_BOX), fill=DOOR)
    d.rectangle(nbox(*DOOR_STEP_BOX), fill=DOOR_STEP)
    return art


def art_collar():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(art).rounded_rectangle(nbox(*COLLAR_BOX), radius=nrad_x(COLLAR_RADIUS), fill=COLLAR)
    return art


def art_wing():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    ImageDraw.Draw(art).rounded_rectangle(nbox(*WING_BOX), radius=nrad_x(WING_RADIUS), fill=WING)
    return art


def art_face():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    d.ellipse(nbox(*FACE_BOX), fill=FACE)
    d.rectangle(nbox(*EYE_BOX), fill=DARK)
    return art


def art_cap():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    d.rounded_rectangle(nbox(*CAP_CROWN), radius=nrad_x(CAP_CROWN_RADIUS), fill=CAP)
    d.rounded_rectangle(nbox(*CAP_HIGHLIGHT_BOX), radius=nrad_x(CAP_HIGHLIGHT_RADIUS), fill=CAP_HIGHLIGHT)
    d.polygon(npoly(CAP_BILL_SHAPE), fill=CAP_BILL)
    return art


def art_beak():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    d.polygon(npoly(BEAK_SHAPE), fill=BEAK)
    d.ellipse(nbox(*BEAK_TIP_BOX), fill=BEAK)
    return art


def art_awning():
    art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    d = ImageDraw.Draw(art)
    for index in range(AWNING_STRIPES):
        colour = AWNING_ORANGE if index % 2 == 0 else AWNING_CREAM
        left = index * AWNING_STRIPE_W
        centre = left + AWNING_STRIPE_W * 0.5
        d.rectangle(nbox(left, AWNING_TOP, left + AWNING_STRIPE_W, AWNING_SCALLOP_CY), fill=colour)
        d.ellipse(nbox(centre - AWNING_SCALLOP_R, AWNING_SCALLOP_CY - AWNING_SCALLOP_RY,
                       centre + AWNING_SCALLOP_R, AWNING_SCALLOP_CY + AWNING_SCALLOP_RY), fill=colour)
    return art


def compose():
    canvas = Image.new("RGBA", (N, N), (0, 0, 0, 0))

    body = silhouette()

    halo = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    halo.paste(CREAM_HALO, (0, 0), grow(body, HALO_PX))
    canvas.alpha_composite(halo)

    silhouette_art = Image.new("RGBA", (N, N), (0, 0, 0, 0))
    silhouette_art.paste(DARK, (0, 0), body)
    canvas.alpha_composite(silhouette_art)

    # Back to front: pointer, shop front, duck, awning. The awning goes last only to
    # trim the bottoms of the collar and the wing; the duck's face and beak sit clear
    # above it, as in the reference.
    element(canvas, pointer_mask(), art_pointer())
    element(canvas, shop_mask(), art_shop())
    element(canvas, face_mask(), art_face())
    element(canvas, cap_mask(), art_cap())
    element(canvas, beak_mask(), art_beak())
    element(canvas, collar_mask(), art_collar())
    element(canvas, wing_mask(), art_wing())
    element(canvas, awning_mask(), art_awning())

    return canvas.resize((BASE, BASE), Image.LANCZOS)


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
    parser = argparse.ArgumentParser(description="Generate the minimap marker sticker.")
    parser.add_argument("--out", default=None, help="output png (default mod/marker-map.png)")
    parser.add_argument("--size-test", action="store_true", help="also write the size proof strips")
    args = parser.parse_args()

    root = os.path.dirname(os.path.dirname(os.path.dirname(os.path.abspath(__file__))))
    out = args.out or os.path.join(root, "mod", "marker-map.png")

    marker = compose()
    marker.save(out)
    print("wrote", out, marker.size, os.path.getsize(out), "bytes")

    if args.size_test:
        out_dir = os.path.join(root, "artifacts", "inspection")
        os.makedirs(out_dir, exist_ok=True)
        size_test(marker, out_dir)


if __name__ == "__main__":
    main()
