"""Static mock of the shipment panel layout for visual review (not a runtime screenshot)."""
from pathlib import Path

from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[1]
OUT = ROOT / "artifacts" / "inspection" / "panel-mock.png"


def font(size, bold=False):
    for candidate in (r"C:\Windows\Fonts\msyhbd.ttc" if bold else r"C:\Windows\Fonts\msyh.ttc",
                      r"C:\Windows\Fonts\msyhbd.ttc", r"C:\Windows\Fonts\msyh.ttc", r"C:\Windows\Fonts\simhei.ttf"):
        if Path(candidate).exists():
            return ImageFont.truetype(candidate, size)
    return ImageFont.load_default()


W, H = 920, 700
BG = (9, 14, 12, 255)
PANEL = (14, 22, 19, 255)
HEADER = (17, 36, 27, 255)
CARD = (19, 27, 24, 255)
ACCENT = (82, 230, 148, 255)
TEXT = (235, 250, 240, 255)
DIM = (168, 194, 178, 255)
DANGER = (255, 122, 107, 255)
GOLD = (255, 209, 92, 255)

image = Image.new("RGBA", (W, H), BG)
draw = ImageDraw.Draw(image)


def rounded(rect, radius, fill, outline=None, width=1):
    draw.rounded_rectangle(rect, radius=radius, fill=fill, outline=outline, width=width)


rounded((0, 0, W, H), 22, PANEL, (28, 82, 58), 2)
rounded((0, 0, W, 68), 22, HEADER)
draw.rectangle((0, 40, W, 68), fill=HEADER)
draw.rectangle((0, 0, 4, 68), fill=ACCENT)

# Header
rounded((18, 16, 54, 52), 10, (26, 58, 42))
draw.text((36, 34), "菜", font=font(20, True), fill=TEXT, anchor="mm")
draw.text((64, 9), "菜鸭驿站", font=font(26, True), fill=TEXT)
rounded((190, 16, 256, 40), 12, (12, 28, 22), (40, 100, 72))
draw.text((223, 28), "营业中", font=font(11), fill=ACCENT, anchor="mm")
draw.text((64, 41), "仓库区　·　送达：地堡马蜂自提点　·　每站每局一次", font=font(13), fill=DIM)
rounded((838, 16, 876, 52), 10, (26, 40, 34))
draw.text((857, 34), "×", font=font(20), fill=DIM, anchor="mm")

# Left column
LX, LY, LW = 20, 84, 560
draw.text((LX, LY), "背包物品", font=font(13, True), fill=ACCENT)
draw.text((LX + 300, LY), "已选 3/12 格　4.2/25 kg", font=font(12), fill=DIM)
rounded((LX, LY + 30, LX + LW, LY + 542), 12, CARD, (22, 44, 34))

# List header
cols = [LX + 70, LX + LW - 296, LX + LW - 250, LX + LW - 184, LX + LW - 104]
headers = ["名称", "数量", "单件", "总价", "重量"]
for x, text in zip(cols, headers):
    draw.text((x, LY + 36), text, font=font(12), fill=DIM)

rows = [
    ("罐头", "x3", 120, 360, "1.2kg", (82, 230, 148)),
    ("布条", "x5", 45, 225, "0.5kg", (82, 230, 148)),
    ("电路板", "x1", 900, 900, "0.8kg", (120, 180, 255)),
    ("止痛药", "x2", 320, 640, "0.2kg", (82, 230, 148)),
    ("Rifle Ammo", "x40", 25, 1000, "1.0kg", (168, 194, 178)),
    ("任务信件", "1", 0, 1, "0.1kg", (255, 122, 107)),
]
row_y = LY + 60
for i, (name, count, unit, total, weight, quality) in enumerate(rows):
    selected = i < 3
    if selected:
        rounded((LX + 4, row_y - 2, LX + LW - 4, row_y + 32), 8, (26, 46, 36))
    draw.rectangle((LX + 5, row_y + 7, LX + 8, row_y + 27), fill=quality)
    rounded((LX + 12, row_y + 9, LX + 30, row_y + 27), 5, ACCENT if selected else (20, 30, 26),
            None if selected else (80, 100, 90))
    if selected:
        draw.text((LX + 21, row_y + 18), "✓", font=font(11, True), fill=(6, 30, 18), anchor="mm")
    rounded((LX + 36, row_y + 4, LX + 64, row_y + 32), 6, (30, 50, 40))
    draw.text((LX + 50, row_y + 18), name[0], font=font(14), fill=TEXT, anchor="mm")
    blocked = i == 5
    draw.text((LX + 70, row_y + 8), name, font=font(15), fill=(120, 132, 126) if blocked else TEXT)
    if blocked:
        draw.text((LX + 70, row_y + 23), "不可寄", font=font(11), fill=DANGER)
    draw.text((LX + LW - 296 + 42, row_y + 9), count, font=font(13), fill=DIM, anchor="ra")
    draw.text((LX + LW - 250 + 62, row_y + 9), str(unit), font=font(13), fill=DIM, anchor="ra")
    draw.text((LX + LW - 184 + 76, row_y + 9), str(total), font=font(13), fill=DIM, anchor="ra")
    draw.text((LX + LW - 104 + 54, row_y + 9), weight, font=font(13), fill=DIM, anchor="ra")
    row_y += 36
draw.text((LX, LY + 542 - 20), "悬停查看详情　·　灰色为不可寄　·　只收背包内物品", font=font(12), fill=DIM)

# Right column
RX, RY, RW = 600, 84, 320
rounded((RX, RY, RX + RW, RY + 232), 12, CARD, (22, 44, 34))
draw.text((RX + 16, RY + 10), "费用与风险", font=font(13, True), fill=ACCENT)
lines = [("货值", "2,126"), ("运费（10%）", "213"), ("基础损耗 5%", "约 106"),
         ("护送后损耗 3%", "拾荒者团"), ("护送费", "1,000")]
y = RY + 38
for key, value in lines:
    draw.text((RX + 16, y), key, font=font(12), fill=DIM)
    draw.text((RX + RW - 16, y), value, font=font(13), fill=TEXT, anchor="ra")
    y += 23
draw.line((RX + 16, y + 2, RX + RW - 16, y + 2), fill=(36, 56, 46))
draw.text((RX + 16, y + 8), "预计总成本", font=font(15), fill=TEXT)
draw.text((RX + RW - 16, y + 8), "1,319", font=font(24, True), fill=ACCENT, anchor="ra")
y += 38
rounded((RX + 16, y, RX + RW - 16, y + 10), 5, (14, 20, 17), (30, 46, 38))
draw.rectangle((RX + 18, y + 2, RX + 18 + (RW - 36) * 0.97, y + 8), fill=ACCENT)
draw.rectangle((RX + 18 + (RW - 36) * 0.97, y + 2, RX + RW - 18, y + 8), fill=DANGER)
draw.text((RX + 16, y + 14), "预计送达 97%　·　预计丢失 3%", font=font(12), fill=DIM)
draw.text((RX + 16, y + 32), "实际按堆叠随机；最多丢 30% 件数 / 50% 价值", font=font(12), fill=DIM)

# Escort cards
EY = RY + 244
draw.text((RX, EY), "护送队（每单最多一队）", font=font(13, True), fill=ACCENT)
cards = [("不请护送", "免费", "损耗 5%", False), ("拾荒者团", "1,000 + 4 羽毛", "损耗 3%", True),
         ("雇佣兵团", "2,000 + 4 狗牌", "损耗 1.5%", False)]
cy = EY + 26
for title, cost, effect, selected in cards:
    rounded((RX, cy, RX + RW, cy + 54), 10, (24, 38, 30) if selected else CARD,
            ACCENT if selected else (24, 42, 32))
    if selected:
        draw.rectangle((RX, cy + 8, RX + 3, cy + 46), fill=ACCENT)
    draw.text((RX + 14, cy + 6), title, font=font(14, selected), fill=TEXT)
    draw.text((RX + 14, cy + 27), cost, font=font(12), fill=DIM)
    draw.text((RX + RW - 14, cy + 27), effect, font=font(12), fill=DIM, anchor="ra")
    cy += 60

# Warnings
draw.text((RX, RY + 452), "选择物品后即可发货；发货后请回地堡马蜂自提点领取。", font=font(12), fill=DIM)

# Footer
draw.line((20, H - 68, W - 20, H - 68), fill=(36, 56, 46))
draw.text((20, H - 60), "金钱 12,400　现金 680", font=font(15), fill=TEXT)
draw.text((20, H - 39), "已选 3 件　·　3% 损耗　·　总成本 1,319", font=font(12), fill=DIM)
buttons = [("全选可寄", 466), ("清空", 582), ("确认发货", 672), ("关闭", 838)]
for text, x in buttons:
    solid = text == "确认发货" and False
    rounded((x, H - 56, x + (160 if text == "确认发货" else 82 if text == "关闭" else 84 if text == "清空" else 110), H - 24),
            10, (82, 230, 148) if text == "确认发货" else (28, 42, 35))
    draw.text((x + (160 if text == "确认发货" else 82 if text == "关闭" else 84 if text == "清空" else 110) / 2, H - 40),
              text, font=font(13, text == "确认发货"), fill=(6, 30, 18) if text == "确认发货" else TEXT, anchor="mm")

OUT.parent.mkdir(parents=True, exist_ok=True)
image.save(OUT)
print("wrote", OUT)

