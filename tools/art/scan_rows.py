from PIL import Image
p=r"C:\Users\LENOVO\.local\state\billion-context\retrieve\img\session-34ed1754-025d-43f7-9065-ae47c2250e9c\m02299-0963086d.png"
im=Image.open(p).convert("RGBA")
print("size",im.size)
for i in range(13):
    cy=149+40*i+18
    if cy>=im.size[1]-60: break
    row=[im.getpixel((x,cy)) for x in (30,32,33,36,40,46,120,200,300)]
    print(i,"y=",cy,[("%02x%02x%02x"%r[:3]) for r in row])
