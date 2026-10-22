"""Read-only inspection of the game's item catalogue.

Never writes to the game directory. Uses UnityPy only to read serialized
MonoBehaviour data from resources.assets so the mod can avoid guessing
item ids for escort costs and protected item tags.
"""
import json
import sys
from pathlib import Path

PY_TOOLS = Path(r"E:\javaproject\duckov-shared-bike\.python-tools")
sys.path.insert(0, str(PY_TOOLS))
import UnityPy

GAME = Path(r"E:\Steam\steamapps\common\Escape from Duckov\Duckov_Data")
OUT = Path(__file__).resolve().parents[1] / "artifacts" / "inspection" / "items.json"


def main():
    env = UnityPy.load(str(GAME / "resources.assets"))
    collections = []
    for obj in env.objects:
        if obj.type.name != "MonoBehaviour":
            continue
        try:
            base = obj.parse_as_object(check_read=False)
            if not base.m_Script.m_PathID:
                continue
            script = base.m_Script.deref_parse_as_object()
            if script.m_ClassName != "ItemAssetsCollection":
                continue
            collections.append(obj)
        except Exception as exc:
            print("scan error", obj.path_id, exc, file=sys.stderr)
    print("ItemAssetsCollection objects:", len(collections))
    report = {"objects": []}
    for obj in collections:
        entry = {"file": obj.assets_file.name, "path_id": obj.path_id, "byte_size": obj.byte_size}
        try:
            entry["dict"] = obj.parse_as_dict()
        except Exception as exc:
            entry["dict_error"] = str(exc)
            entry["raw_hex"] = obj.get_raw_data()[:2000].hex()
        report["objects"].append(entry)
    OUT.parent.mkdir(parents=True, exist_ok=True)
    OUT.write_text(json.dumps(report, ensure_ascii=False, indent=2, default=str), encoding="utf-8")
    print("wrote", OUT)


if __name__ == "__main__":
    main()
