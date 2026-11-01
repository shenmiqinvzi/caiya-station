"""Scan the live in-game panel screenshot for the selected-row accent colour."""
import sys
from PIL import Image

PATH = r"C:\Users\LENOVO\.local\state\billion-context\retrieve\img\session-34ed1754-025d-43f7-9065-ae47c2250e9c\m02299-0963086d.png"

ACCENT = (24, 131, 67)
ACCENT_DEEP = (50, 106, 79)
ON_PRIMARY = (255, 255, 255)
ROW_SELECTED_OVER_CARD = (211, 229, 224)


def near(px, target, tol):
    return all(abs(px[i] - target[i]) <= tol for i in range(3))


img = Image.open(PATH).convert("RGB")
w, h = img.size
px = img.load()
print("size", w, h)

# 1. every pixel close to the accent green inside the left list card
hits = []
for y in range(90, 640):
    for x in range(20, 480):
        if near(px[x, y], ACCENT, 12):
            hits.append((x, y))
print("accent-near pixels in left card:", len(hits))
if hits:
    xs = [p[0] for p in hits]
    ys = [p[1] for p in hits]
    print("bbox", min(xs), min(ys), max(xs), max(ys))
    # cluster rows
    rows = {}
    for x, y in hits:
        rows.setdefault(y, []).append(x)
    ys_sorted = sorted(rows)
    groups = []
    start = prev = ys_sorted[0]
    for y in ys_sorted[1:]:
        if y - prev > 2:
            groups.append((start, prev))
            start = y
        prev = y
    groups.append((start, prev))
    for g in groups:
        xs_g = [x for y in range(g[0], g[1] + 1) for x in rows.get(y, [])]
        print("  y-run", g, "x", min(xs_g), max(xs_g), "count", len(xs_g))

# 2. the exact selected-tint colour anywhere
hits2 = [(x, y) for y in range(60, 690) for x in range(0, w) if near(px[x, y], ROW_SELECTED_OVER_CARD, 3)]
print("row-selected tint pixels:", len(hits2))

# 3. column profile at one row band, to see what is at rect.x
for band in (170, 210, 250, 290, 330, 370, 410, 450):
    line = []
    for x in range(28, 50):
        line.append("%02x%02x%02x" % px[x, band])
    print("y=%d" % band, " ".join(line))
