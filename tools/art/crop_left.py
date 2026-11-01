from PIL import Image
src=r"C:\Users\LENOVO\.local\state\billion-context\retrieve\img\session-34ed1754-025d-43f7-9065-ae47c2250e9c\m02299-0963086d.png"
im=Image.open(src).convert("RGB")
# left list rows crop
crop=im.crop((20,140,360,400)).resize((340*3,260*3), Image.NEAREST)
crop.save(r"artifacts\inspection\zoom-left-rows.png")
print("saved", crop.size)
# column of x=36..70 for row0..row3 vertical profile
for y in range(148,200):
    px=[im.getpixel((x,y)) for x in (36,38,40,42,46,50,56,62,68)]
    print(y, " ".join(f"{r:02x}{g:02x}{b:02x}" for r,g,b in px))
