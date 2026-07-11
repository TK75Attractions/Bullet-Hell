# 石工タイル予告を点線(stone_warning ドット輪郭)から半透明矩形(warn_box)へ
# 差し替える(REVIEW-NOTES 2026-07-11「最初の攻撃の予告を warn_box 様式に」)。
#
# 位置ロックの確立手法に従い、予告の位置/サイズは対応する出現バッファ
# (stone_tile_spawn_*.json の stone_block 5 タイル)を単一ソースとして生成する。
# タイミング(appearTime/appearDuration/life)は既存予告ドットの値をそのまま
# 引き継ぐので、表示/消滅時刻は従来の点線と同一(本体出現と同時に消える)。
# 弾スキーマは既存 warn_box(石工ランダム落下予告)と同一フィールド構成。
import json
import os
import sys

sys.stdout.reconfigure(encoding="utf-8")

BASE = r"D:\unity\Bullet-Hell\Assets\BulletBuffers\stone"

PAIRS = [
    ("stone_tile_warn_1_a.json", "stone_tile_spawn_1_a.json"),
    ("stone_tile_warn_1_b.json", "stone_tile_spawn_1_b.json"),
    ("stone_tile_warn_1_c.json", "stone_tile_spawn_1_c.json"),
    ("stone_tile_warn_1_d.json", "stone_tile_spawn_1_d.json"),
    ("stone_tile_warn_2_a.json", "stone_tile_spawn_2_a.json"),
    ("stone_tile_warn_2_b.json", "stone_tile_spawn_2_b.json"),
    ("stone_tile_warn_2_c.json", "stone_tile_spawn_2_c.json"),
    ("stone_tile_warn_2_d.json", "stone_tile_spawn_2_d.json"),
]


def warn_box(x, y, w, h, appear, appear_dur, life):
    return {
        "originPos": {"x": round(x, 4), "y": round(y, 4)},
        "originVlc": {"x": 0, "y": 0},
        "playerInfluence": {"x": 0, "y": 0},
        "startX": 0,
        "speed": 0,
        "gravity": {"x": 0, "y": -1.5707963},
        "angleSpeed": 0,
        "initialAngle": 0,
        "polarForm": {"x": 1, "y": 0},
        "radiusVlc": 0,
        "thetaVlc": 0,
        "startPos": {"x": 0, "y": 0},
        "polynomial": {"x": 0, "y": 0, "z": 0, "w": 0},
        "typeName": "warn_box",
        "scale": {"x": w, "y": h},
        "color": {"x": 1.0, "y": 1.0, "z": 1.0, "w": 0.0},
        "appearTime": appear,
        "appearDuration": appear_dur,
        "life": life,
        "random": 0,
        "unCounterable": True,
        "useVelocityAngle": False,
    }


def load(path):
    with open(path, encoding="utf-8-sig") as f:
        return json.load(f)


def write(path, obj):
    text = json.dumps(obj, ensure_ascii=False, indent=4)
    text = text.replace("\n", "\r\n")
    data = b"\xef\xbb\xbf" + text.encode("utf-8")
    with open(path, "wb") as f:
        f.write(data)


def main():
    for warn_name, spawn_name in PAIRS:
        warn_path = os.path.join(BASE, warn_name)
        spawn_path = os.path.join(BASE, spawn_name)
        warn = load(warn_path)
        spawn = load(spawn_path)

        # 既存ドットのタイミングを引き継ぐ(クリップ内で一様である前提を検査)
        appears = {b["appearTime"] for b in warn["bullets"]}
        durs = {b["appearDuration"] for b in warn["bullets"]}
        lifes = {b["life"] for b in warn["bullets"]}
        assert len(appears) == 1 and len(durs) == 1 and len(lifes) == 1, (
            warn_name + " timing not uniform: " + str((appears, durs, lifes)))
        appear, dur, life = appears.pop(), durs.pop(), lifes.pop()

        tiles = [b for b in spawn["bullets"] if b["typeName"] == "stone_block"]
        assert tiles, spawn_name + " has no stone_block"
        bullets = [
            warn_box(t["originPos"]["x"], t["originPos"]["y"],
                     t["scale"]["x"], t["scale"]["y"], appear, dur, life)
            for t in tiles
        ]
        out = {"name": warn["name"], "bullets": bullets,
               "homing": warn.get("homing", False),
               "isLaser": warn.get("isLaser", False)}
        write(warn_path, out)
        print("wrote", warn_name, "tiles=", len(bullets),
              "appear=", appear, "life=", life)


if __name__ == "__main__":
    main()
