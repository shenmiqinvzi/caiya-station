from PIL import Image
im = Image.open(r"C:\Users\LENOVO\.local\state\billion-context\retrieve\img\session-34ed1754-025d-43f7-9065-ae47c2250e9c\m02299-0963086d.png").convert("RGB")
print(im.size)
# scan the list card area for any green-ish selected band
W,H = im.size
for y in range(120, 660, 4):
    row=[]
    for x in range(28, 340, 6):
        r,g,b = im.getpixel((x,y))
        row.append(f"{r:02x}{g:02x}{b:02x}")
    print(y, " ".join(row))
