"""Render the paint-command JSON emitted by tools/PanelPreview into PNG previews.

The commands come from the real C# PanelRenderer, so the geometry in these images is the
geometry the game will draw. Only the font rasteriser differs (PIL/msyh instead of Unity).
"""
import json
import sys
from pathlib import Path

from PIL import Image, ImageChops, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "inspection"
FONT_REG = Path(r"C:\Windows\Fonts\msyh.ttc")
FONT_BOLD = Path(r"C:\Windows\Fonts\msyhbd.ttc")
FALLBACK = Path(r"C:\Windows\Fonts\simhei.ttf")

_fonts = {}


def font(size, bold):
    key = (size, bold)
    if key in _fonts:
        return _fonts[key]
    path = FONT_BOLD if bold and FONT_BOLD.exists() else FONT_REG
    if not path.exists():
        path = FALLBACK
    try:
        loaded = ImageFont.truetype(str(path), size)
    except OSError:
        loaded = ImageFont.load_default()
    _fonts[key] = loaded
    return loaded


def rgba(values):
    r, g, b, a = values
    return (
        max(0, min(255, round(r * 255))),
        max(0, min(255, round(g * 255))),
        max(0, min(255, round(b * 255))),
        max(0, min(255, round(a * 255))),
    )


def rect_of(values, scale):
    x, y, w, h = values
    return [x * scale, y * scale, (x + w) * scale, (y + h) * scale]


ANCHORS = {
    "ml": "lm",
    "mc": "mm",
    "mr": "rm",
    "ul": "la",
    "uc": "ma",
    "ur": "ra",
    "ll": "ld",
    "lc": "md",
    "lr": "rd",
}


def wrap_lines(text, fnt, max_width, draw):
    lines = []
    current = ""
    for ch in text:
        if ch == "\n":
            lines.append(current)
            current = ""
            continue
        candidate = current + ch
        width = draw.textlength(candidate, font=fnt)
        if width <= max_width or not current:
            current = candidate
        else:
            lines.append(current)
            current = ch
    if current:
        lines.append(current)
    return lines


def paint(draw, cmd, scale, canvas):
    kind = cmd["t"]
    if kind == "rect":
        box = rect_of(cmd["r"], scale)
        radius = max(0.0, cmd.get("radius", 0)) * scale
        fill = rgba(cmd["fill"])
        stroke = rgba(cmd["stroke"]) if cmd.get("stroke") else None
        width = max(1, int(round(cmd.get("sw", 1) * scale))) if stroke else 1
        if radius > 0.5:
            draw.rounded_rectangle(box, radius=radius, fill=fill, outline=stroke, width=width)
        else:
            draw.rectangle(box, fill=fill, outline=stroke, width=width)
        return

    if kind == "icon":
        box = rect_of(cmd["r"], scale)
        color = rgba(cmd["color"])
        fill = (color[0], color[1], color[2], 26)
        stroke = (color[0], color[1], color[2], 60)
        radius = max(3, 6 * scale)
        draw.rounded_rectangle(box, radius=radius, fill=fill, outline=stroke, width=1)
        size = max(8, round(cmd["size"] * scale))
        fnt = font(size, False)
        draw.text(
            ((box[0] + box[2]) / 2, (box[1] + box[3]) / 2),
            cmd.get("s", "?"),
            font=fnt,
            fill=color,
            anchor="mm",
        )
        return

    if kind == "text":
        box = rect_of(cmd["r"], scale)
        size = max(7, round(cmd["size"] * scale))
        fnt = font(size, bool(cmd.get("bold")))
        color = rgba(cmd["color"])
        text = cmd.get("s", "")
        if cmd.get("wrap"):
            lines = wrap_lines(text, fnt, box[2] - box[0], draw)
            line_height = size * 1.42
            y = box[1]
            for line in lines:
                draw.text((box[0], y), line, font=fnt, fill=color, anchor="la")
                y += line_height
            return
        anchor = ANCHORS.get(cmd.get("anchor", "ml"), "lm")
        x = box[0] if anchor[0] == "l" else (box[2] if anchor[0] == "r" else (box[0] + box[2]) / 2)
        y = box[1] if anchor[1] == "a" else (box[3] if anchor[1] == "d" else (box[1] + box[3]) / 2)
        draw.text((x, y), text, font=fnt, fill=color, anchor=anchor)


def needs_layer(cmd):
    if cmd["t"] == "rect":
        if cmd["fill"][3] < 0.995:
            return True
        stroke = cmd.get("stroke")
        return bool(stroke) and stroke[3] < 0.995
    if cmd["t"] == "icon":
        return True
    return cmd["color"][3] < 0.995


def draw_command(image, cmd, scale):
    clip = cmd.get("c")
    if not clip and not needs_layer(cmd):
        paint(ImageDraw.Draw(image), cmd, scale, image)
        return
    layer = Image.new("RGBA", image.size, (0, 0, 0, 0))
    paint(ImageDraw.Draw(layer), cmd, scale, layer)
    if clip:
        mask = Image.new("L", image.size, 0)
        ImageDraw.Draw(mask).rectangle(rect_of(clip, scale), fill=255)
        layer.putalpha(ImageChops.multiply(layer.getchannel("A"), mask))
    image.alpha_composite(layer)


def render_image(document, scale=1):
    width, height = document["size"]
    image = Image.new("RGBA", (int(width * scale), int(height * scale)), (5, 8, 6, 255))
    for command in document["commands"]:
        draw_command(image, command, scale)
    return image


def main():
    targets = sys.argv[1:]
    sources = sorted(OUT.glob("panel-*.json")) if not targets else [Path(t) for t in targets]
    for source in sources:
        name = source.stem
        document = json.loads(source.read_text(encoding="utf-8"))
        image = render_image(document, 1)
        image.save(OUT / f"{name}.png")
        render_image(document, 2).save(OUT / f"{name}@2x.png")
        if document.get("panel"):
            x, y, w, h = document["panel"]
            image.crop((round(x), round(y), round(x + w), round(y + h))).save(OUT / f"{name}-card.png")
            if name == "panel-default":
                image.crop((round(x), round(y), round(x + w), round(y + h))).save(OUT / "panel-card.png")
        print("wrote", OUT / f"{name}.png", "and", OUT / f"{name}@2x.png")


if __name__ == "__main__":
    main()
