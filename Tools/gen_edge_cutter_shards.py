# -*- coding: utf-8 -*-
"""縁カッター破片を刃の掃引に追従(trailing)させて再生成する。
edge_cutter_1(edge_cutter_shard) と逆方向の edge_cutter_2(edge_cutter_shard_2) を作る。
各辺で刃が通過した位置・時刻に破片を発生させ、内向き+後方(trailing)へ射出する。"""
import sys, math, copy, json
sys.path.insert(0, "Tools")
from stone_edit_lib import load, dump, exit_lapse

SPEED = 5.0
SCALE = 0.55
PER_EDGE = 10
BLADE_SPEED = 24.0

# edge geometry: name -> (fixed axis value, inward normal unit, along-edge sample range)
# top/bottom sample x in [2,30]; left/right sample y in [2,16]
EDGES = {
    "top":    dict(y=17.3, n=(0, -1), axis="x", lo=2.0, hi=30.0),
    "bottom": dict(y=1.2,  n=(0,  1), axis="x", lo=2.0, hi=30.0),
    "right":  dict(x=31.3, n=(-1, 0), axis="y", lo=2.0, hi=16.0),
    "left":   dict(x=0.7,  n=(1,  0), axis="y", lo=2.0, hi=16.0),
}

# blade parameters per variant: edge -> (appearTime, dir_unit, passage(pos)->time)
def blades(variant):
    if variant == 1:
        return {
            "top":    dict(appT=0.833333, d=(1, 0),  start=-1.95),   # L->R
            "bottom": dict(appT=0.983333, d=(-1, 0), start=33.95),   # R->L
            "right":  dict(appT=1.283333, d=(0, 1),  start=-1.95),   # B->T
            "left":   dict(appT=1.133333, d=(0, -1), start=19.95),   # T->B
        }
    else:
        return {
            "top":    dict(appT=0.833333, d=(-1, 0), start=33.95),   # R->L
            "bottom": dict(appT=0.983333, d=(1, 0),  start=-1.95),   # L->R
            "right":  dict(appT=1.283333, d=(0, -1), start=19.95),   # T->B
            "left":   dict(appT=1.133333, d=(0, 1),  start=-1.95),   # B->T
        }

def passage_time(edge, blade, pos):
    """blade reaches along-edge coordinate pos at this time (relative to fire)."""
    d = blade["d"]; start = blade["start"]
    if edge in ("top", "bottom"):
        travelled = abs(pos - start)
    else:
        travelled = abs(pos - start)
    return blade["appT"] + travelled / BLADE_SPEED

def make_bullet(template, edge, variant, i):
    e = EDGES[edge]; b = blades(variant)[edge]
    frac = i / (PER_EDGE - 1)
    pos = e["lo"] + (e["hi"] - e["lo"]) * frac
    if edge in ("top", "bottom"):
        ox, oy = pos, e["y"]
    else:
        ox, oy = e["x"], pos
    # velocity direction = inward normal + trailing(-blade dir)*0.5
    n = e["n"]; d = b["d"]
    vx = n[0] * 1.0 + (-d[0]) * 0.5
    vy = n[1] * 1.0 + (-d[1]) * 0.5
    # deterministic small jitter (+/-8deg) for natural look
    jit = ((i * 37 + (0 if variant == 1 else 19)) % 17 - 8) * math.radians(1.0)
    ang = math.atan2(vy, vx) + jit
    appT = passage_time(edge, b, pos)
    life = exit_lapse(ox, oy, SPEED, ang, 0.0) + 0.15
    spin = 6 + (i % 5)
    if i % 2 == 0:
        spin = -spin
    bl = copy.deepcopy(template)
    bl["originPos"] = {"x": round(ox, 3), "y": round(oy, 3)}
    bl["originVlc"] = {"x": 0, "y": 0}
    bl["speed"] = SPEED
    bl["gravity"] = 0.0
    bl["angleSpeed"] = spin
    bl["polarForm"] = {"x": 1, "y": round(ang % (2 * math.pi), 6)}
    bl["startPos"] = {"x": 0, "y": 0}
    bl["startX"] = 0
    bl["scale"] = {"x": SCALE, "y": SCALE}
    bl["appearTime"] = round(appT, 6)
    bl["appearDuration"] = 0
    bl["life"] = round(life, 4)
    return bl

def build(variant, out_name, out_path):
    src, bom, crlf, tnl = load("Assets/BulletBuffers/stone/edge_cutter_shard.json")
    template = src["bullets"][0]
    bullets = []
    for edge in ("top", "bottom", "right", "left"):
        for i in range(PER_EDGE):
            bullets.append(make_bullet(template, edge, variant, i))
    obj = {"name": out_name, "bullets": bullets, "homing": False, "isLaser": False}
    dump(out_path, obj, bom, crlf, tnl)
    print(f"wrote {out_path}: {len(bullets)} shards (variant {variant})")

# variant 1 -> overwrite existing edge_cutter_shard (keep its name)
n1 = json.load(open("Assets/BulletBuffers/stone/edge_cutter_shard.json", encoding="utf-8-sig"))["name"]
build(1, n1, "Assets/BulletBuffers/stone/edge_cutter_shard.json")
# variant 2 -> new edge_cutter_shard_2 ; name = n1 + "_2"
build(2, n1 + "_2", "Assets/BulletBuffers/stone/edge_cutter_shard_2.json")
print("name1 =", n1)
