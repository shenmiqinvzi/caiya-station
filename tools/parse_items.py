"""Parse the item catalogue and tag names from resources.assets (read-only).

Output: artifacts/inspection/item-catalogue.json with typeID, internal name,
display name, price, stack size and tag names for every item the game ships.
"""
import json
import struct
import sys
from pathlib import Path

sys.path.insert(0, r"E:\javaproject\duckov-shared-bike\.python-tools")
import UnityPy

GAME = Path(r"E:\Steam\steamapps\common\Escape from Duckov\Duckov_Data")
OUT = Path(__file__).resolve().parents[1] / "artifacts" / "inspection" / "item-catalogue.json"


def read_string(raw, off):
    n = struct.unpack_from("<i", raw, off)[0]
    off += 4
    if n < 0 or n > 200000:
        raise ValueError(f"bad string length {n} at {off - 4}")
    s = raw[off:off + n].decode("utf-8", "replace")
    off += n
    off = (off + 3) & ~3
    return s, off


def read_pptr(raw, off):
    file_id, path_id = struct.unpack_from("<iq", raw, off)
    return (file_id, path_id), off + 12


def main():
    env = UnityPy.load(str(GAME / "resources.assets"))
    collection = None
    tag_names = {}
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            base = obj.parse_as_object(check_read=False)
        except Exception:
            continue
        if not base.m_Script.m_PathID:
            continue
        try:
            script = base.m_Script.deref_parse_as_object()
        except Exception:
            continue
        if script.m_ClassName == "Tag":
            tag_names[obj.path_id] = base.m_Name
        elif script.m_ClassName == "ItemAssetsCollection":
            collection = obj
    if collection is None:
        raise SystemExit("ItemAssetsCollection not found")

    raw = collection.get_raw_data()
    off = 52
    next_type_id = struct.unpack_from("<i", raw, off)[0]; off += 4
    count = struct.unpack_from("<i", raw, off)[0]; off += 4
    items = []
    for _ in range(count):
        type_id = struct.unpack_from("<i", raw, off)[0]; off += 4
        (_, prefab_id), off = read_pptr(raw, off)
        meta_id = struct.unpack_from("<i", raw, off)[0]; off += 4
        quality = struct.unpack_from("<i", raw, off)[0]; off += 4
        tag_count = struct.unpack_from("<i", raw, off)[0]; off += 4
        tags = []
        for _tag in range(tag_count):
            (_, tag_id), off = read_pptr(raw, off)
            tags.append(tag_names.get(tag_id, str(tag_id)))
        name, off = read_string(raw, off)
        display_name, off = read_string(raw, off)
        description, off = read_string(raw, off)
        max_stack, default_stack = struct.unpack_from("<ii", raw, off); off += 8
        (_, icon_id), off = read_pptr(raw, off)
        display_quality = struct.unpack_from("<i", raw, off)[0]; off += 4
        price = struct.unpack_from("<i", raw, off)[0]; off += 4
        caliber, off = read_string(raw, off)
        items.append({
            "typeID": type_id,
            "metaID": meta_id,
            "prefabPathID": prefab_id,
            "name": name,
            "displayName": display_name,
            "description": description,
            "tags": tags,
            "maxStack": max_stack,
            "defaultStack": default_stack,
            "quality": quality,
            "displayQuality": display_quality,
            "priceEach": price,
        })
    if off != len(raw):
        print(f"warning: parsed {off} of {len(raw)} bytes", file=sys.stderr)

    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps({
        "nextTypeID": next_type_id,
        "count": count,
        "tags": sorted(set(tag_names.values())),
        "items": items,
    }, ensure_ascii=False, indent=2), encoding="utf-8")
    print("items", count, "tags", len(tag_names), "bytes", off, "/", len(raw))
    print("wrote", OUT)
    for item in items:
        if any(key in item["name"] for key in ("Feather", "DogTag", "Dog_Tag", "Cash", "Money")):
            print(item["typeID"], item["name"], item["displayName"], item["tags"], item["priceEach"])


if __name__ == "__main__":
    main()
