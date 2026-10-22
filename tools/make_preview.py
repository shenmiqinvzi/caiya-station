"""Generate the mod-menu preview image (no game assets used)."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

OUT = Path(__file__).resolve().parents[1] / "mod" / "preview.png"


def font(size):
    for candidate in (r"C:\Windows\Fonts\msyhbd.ttc", r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


def main():
    size = 512
    image = Image.new("RGBA", (size, size), (10, 22, 18, 255))
    draw = ImageDraw.Draw(image)
    for y in range(size):
        shade = int(16 + 20 * y / size)
        draw.line([(0, y), (size, y)], fill=(8, shade, int(shade * 0.8), 255))

    # Warm lantern glow behind the kiosk.
    glow = Image.new("RGBA", (size, size), (0, 0, 0, 0))
    glow_draw = ImageDraw.Draw(glow)
    for radius, alpha in ((110, 14), (80, 22), (52, 34), (28, 52)):
        glow_draw.ellipse((408 - radius, 250 - radius, 408 + radius, 250 + radius),
                          fill=(255, 190, 96, alpha))
    image = Image.alpha_composite(image, glow)
    draw = ImageDraw.Draw(image)

    # Ground.
    draw.rounded_rectangle((48, 362, 464, 452), radius=20, fill=(24, 44, 35, 255))
    draw.rounded_rectangle((70, 372, 442, 442), radius=14, fill=(20, 38, 30, 255))

    # Kiosk.
    draw.rounded_rectangle((126, 218, 386, 380), radius=10, fill=(28, 50, 42, 255),
                           outline=(70, 180, 120, 255), width=3)
    draw.rectangle((150, 300, 362, 318), fill=(70, 154, 110, 255))
    draw.rectangle((150, 330, 250, 360), fill=(52, 112, 84, 255))
    draw.rectangle((124, 206, 388, 222), fill=(46, 116, 78, 255))  # roof trim

    # Flag.
    draw.line((100, 150, 100, 380), fill=(150, 168, 158, 255), width=5)
    draw.polygon([(102, 158), (168, 176), (102, 206)], fill=(56, 196, 124, 255))
    draw.line((102, 158, 102, 206), fill=(40, 150, 92, 255), width=3)

    # Sign.
    draw.rounded_rectangle((142, 160, 370, 234), radius=12, fill=(18, 32, 26, 255))
    draw.rounded_rectangle((150, 168, 362, 226), radius=10, fill=(50, 196, 120, 255))

    # Duck head + beak + eyes.
    draw.ellipse((200, 100, 304, 196), fill=(244, 250, 238, 255))
    draw.ellipse((288, 126, 352, 162), fill=(250, 176, 48, 255))
    draw.ellipse((228, 128, 246, 146), fill=(18, 30, 26, 255))
    draw.ellipse((268, 128, 286, 146), fill=(18, 30, 26, 255))

    # Lantern.
    draw.line((410, 214, 410, 242), fill=(150, 168, 158, 255), width=4)
    draw.ellipse((392, 236, 428, 272), fill=(255, 208, 120, 255), outline=(255, 236, 180, 255), width=2)

    # Text.
    draw.text((256, 258), "菜鸭驿站", font=font(42), fill=(9, 32, 22, 255), anchor="mm")
    draw.text((256, 300), "CaiYaStation 0.1.0", font=font(19), fill=(222, 248, 232, 255), anchor="mm")
    draw.text((256, 414), "寄回地堡 · 可能丢件 · 可请护送", font=font(20), fill=(184, 232, 204, 255), anchor="mm")

    OUT.parent.mkdir(parents=True, exist_ok=True)
    image.save(OUT)
    print("wrote", OUT)


if __name__ == "__main__":
    main()
