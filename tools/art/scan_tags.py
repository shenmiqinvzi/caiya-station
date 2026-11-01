import os,sys
roots=[r"E:\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Managed"]
needles=[b"Continer",b"Backpack",b"Weapon",b"Caliber"]
for root in roots:
    for dp,dn,fn in os.walk(root):
        for f in fn:
            if not f.lower().endswith((".dll",".exe")): continue
            p=os.path.join(dp,f)
            try: data=open(p,"rb").read()
            except Exception: continue
            for n in needles:
                if n in data:
                    print(f"{os.path.basename(p)} :: {n.decode()}")
