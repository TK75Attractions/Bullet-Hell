# -*- coding: utf-8 -*-
"""縁カッター破片を刃の掃引に追従(trailing)して発生させ、最寄りの壁へ外向きに
「重力的な加速」で飛ばして画面外へ抜けさせる(第36便 @77.7 対応)。
edge_cutter_1(edge_cutter_shard) と逆方向の edge_cutter_2(edge_cutter_shard_2)。

engine の gravity は -Y のみで任意方向の加速に使えないため、radiusVlc を使う。
straight 弾の変位は offset(t) = (r0 + radiusVlc*t) * speed * t を theta 方向へ。
r0=初速係数、radiusVlc*speed が t^2 項(=外向き加速)を生む。各辺で theta を外向き
法線に向け、破片が壁方向へ加速して画面外(-2..36 の bounds)へ抜けるまで生かす。"""
import sys, math, copy, json
sys.path.insert(0, "Tools")
from stone_edit_lib import load, dump

# アーク軌道: 破片はまず内向き(arena 側)へ散り、外向き加速(重力的)で反転して
# 最寄りの壁へ抜ける。offset(t) = (R0 + RADIUS_VLC*t) * SPEED * t を theta(外向き)へ。
# R0<0 で初速は内向き、RADIUS_VLC>0 の t^2 項が外向き加速。turn 時刻 t*=-R0/(2*RVLC)、
# 内向き最大侵入 = SPEED*R0^2/(4*RVLC)。壁が近い(0.7u)ため一旦内側に見せてから外へ出す。
SPEED = 4.0
R0 = 1.5           # 初速係数(正=最初から射出方向へ)。刃の後方へ素直に噴出
RADIUS_VLC = 2.0   # 加速(t^2 項)。4.5→2.0 に下げて破片を遅くし滞留させる(密度up)
OUTWARD_MIX = 0.35 # 射出方向 = 刃の後方(-d) + 壁方向(外向き法線)*OUTWARD_MIX
SCALE = 0.55
PER_EDGE = 16   # 各辺の破片数(密度)。10→16 に増量(@79.3 後方噴出で疎になった分)
BLADE_SPEED = 24.0

def accel_exit_lapse(ox, oy, theta, dt=0.002, maxt=6.0):
    """radiusVlc 加速する straight 破片が bounds(-2..36) を出る lapse。"""
    cs, sn = math.cos(theta), math.sin(theta)
    t = 0.0
    while t < maxt:
        t += dt
        off = (R0 + RADIUS_VLC * t) * SPEED * t
        x = ox + off * cs
        y = oy + off * sn
        if not (-2.0 <= x < 36.0 and -2.0 <= y < 36.0):
            return t
    return maxt

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
    # deterministic な沿辺ジッタ(+/-0.7)で等間隔の弧を崩す(@77.6 規則的すぎ対策)
    pos += (((i * 53 + (0 if variant == 1 else 29)) % 21 - 10) / 10.0) * 0.7
    if edge in ("top", "bottom"):
        ox, oy = pos, e["y"]
    else:
        ox, oy = e["x"], pos
    # velocity direction = 刃の進行方向の逆(後方 -d)に、壁方向(外向き法線 -n)を少量ブレンド。
    # 破片が刃の後ろへ噴き出し(trailing)、徐々に壁へ寄って画面外へ抜ける。
    n = e["n"]
    d = b["d"]
    back = (-d[0], -d[1])          # 刃の進行方向の逆 = 後方
    outward = (-n[0], -n[1])       # 壁方向(外向き法線)
    vx = back[0] + outward[0] * OUTWARD_MIX
    vy = back[1] + outward[1] * OUTWARD_MIX
    # deterministic small jitter (+/-12deg) で後方に軽く扇状
    jit = ((i * 37 + (0 if variant == 1 else 19)) % 25 - 12) * math.radians(1.0)
    ang = math.atan2(vy, vx) + jit
    appT = passage_time(edge, b, pos)
    # life は spawn からの絶対時刻。appearTime(刃の通過)+ 飛行時間(exit まで)+ 余白。
    # appT を足さないと life<appearTime になり appear 前に死ぬ(死亡判定は spawn からの time>=life)。
    life = appT + accel_exit_lapse(ox, oy, ang) + 0.15
    spin = 6 + (i % 5)
    if i % 2 == 0:
        spin = -spin
    bl = copy.deepcopy(template)
    bl["originPos"] = {"x": round(ox, 3), "y": round(oy, 3)}
    bl["originVlc"] = {"x": 0, "y": 0}
    bl["speed"] = SPEED
    bl["gravity"] = 0.0
    bl["radiusVlc"] = RADIUS_VLC      # 外向き加速(t^2 項)= 重力的な加速
    bl["thetaVlc"] = 0
    bl["angleSpeed"] = spin
    bl["polarForm"] = {"x": R0, "y": round(ang % (2 * math.pi), 6)}
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
