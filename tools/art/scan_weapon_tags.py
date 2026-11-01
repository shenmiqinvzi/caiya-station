import re
p=r"E:\Steam\steamapps\common\Escape from Duckov\Duckov_Data\Managed\TeamSoda.Duckov.Core.dll"
data=open(p,"rb").read()
# extract ascii strings
strs=re.findall(rb"[\x20-\x7e]{4,60}", data)
cands=[s.decode() for s in strs]
keys=["Weapon","Gun","Firearm","Rifle","Pistol","Shotgun","Sniper","Launcher","Melee","Caliber","Ammo","Continer","Backpack","Armor","Helmet","Attachment","Accessor","Consumable","Food","Medic","Valuable","Quest","Key","Cash","Character"]
for k in keys:
    hits=[c for c in cands if c.lower()==k.lower() or (len(c)<20 and c.lower().startswith(k.lower()))]
    print(k, "->", hits[:8])
