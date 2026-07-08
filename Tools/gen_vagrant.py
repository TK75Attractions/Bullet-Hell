# -*- coding: utf-8 -*-
"""浮浪者(vagrant)ステージの弾幕バッファと vagrant.json を生成する。
原典(Siv3D, MUSIC=vagrant, BPM=199.5)+ PDF 仕様 + 動画タイムライン解析に基づく。

タイムライン(曲=t0 基準。イントロ後に曲頭を t=0 とする):
  0.0-7.2    イントロ(弾なし)
  7.2-16.8   1サビ前半: 骨の雨(まばら)
  16.8-26.5  1サビ後半: 骨の雨 + 両端の骸骨(上下移動しつつ内側へ骨を一斉射撃)
  26.5-45.7  間奏: 墓石列 + レーザー + 死体破裂(中央から放射)
  45.7-50.5  2サビ前半: 幽霊×1(回転リング+放射)
  50.5-65.0  2サビ後半: 幽霊×2
  65.0-69.2  アウトロ(消化)
座標系 32x18、左下原点。角度: spawner=度、buffer 極座標=ラジアン。
gravity={x:大きさ, y:-1.5708(下)}。polarForm={r, theta_rad}=発射方向, speed=発射速度。
"""
import os, json, math, random

random.seed(6606)
BPM = 199.5
BEAT = 60.0 / BPM          # 0.30075s
BUFDIR = "Assets/BulletBuffers/vagrant"
STAGE = "Assets/StageData/vagrant/vagrant.json"
W, H = 32.0, 18.0

def w_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "wb").write(json.dumps(obj, ensure_ascii=False, indent=4).encode("utf-8"))

def bullet(**kw):
    b = dict(
        originPos={"x": 0, "y": 0}, originVlc={"x": 0, "y": 0},
        speed=0.0, gravity={"x": 0.0, "y": -1.5707963},
        initialAngle=0, angleSpeed=0.0, useVelocityAngle=False,
        polarForm={"x": 1, "y": 0.0}, radiusVlc=0, radiusAccel=0, thetaVlc=0, thetaAccel=0,
        polynomial={"x": 0, "y": 0, "z": 0, "w": 0},
        typeName="bone", scale={"x": 1.0, "y": 1.0},
        color={"x": 0, "y": 0, "z": 0, "w": 0},
        appearTime=0.0, appearDuration=0.05, life=4.0, random=0, unCounterable=True)
    b.update(kw)
    return b

def buf(name, bullets):
    return {"name": name, "bullets": bullets, "homing": False, "isLaser": False}

DOWN = -math.pi / 2

# ---------------------------------------------------------------------------
# 1サビ: 骨の雨(まばら)。幅を 8 セルに分け、拍ごとに一部のセルに1本ずつ落とす。
#   タイル状ランダム: 各セルに1本、セル内 x はランダム。前半 1本/拍、後半 2本/拍。
# ---------------------------------------------------------------------------
def bone_rain():
    bullets = []
    cells = 8
    cw = W / cells
    dur_beats = 64            # 1サビ全体 ~19.3s ≈ 64拍
    top = H + 1.2
    for beat in range(dur_beats):
        t = beat * BEAT
        second_half = beat >= 32
        # 後半は骸骨の一斉射撃が加わるので雨は 1本/拍に抑える(前半と同じ)
        n = 1
        cols = random.sample(range(cells), n)
        for c in cols:
            x = c * cw + random.uniform(0.15, 0.85) * cw
            spd = random.uniform(4.6, 6.2)
            spin = random.choice([-1, 1]) * random.uniform(70, 150)
            fall_t = (top + 2.0) / spd + 1.0
            bullets.append(bullet(
                originPos={"x": round(x, 2), "y": round(top, 2)},
                speed=round(spd, 3), polarForm={"x": 1, "y": DOWN},
                gravity={"x": 2.0, "y": -1.5707963},
                angleSpeed=round(spin, 1), useVelocityAngle=False,
                typeName="bone", scale={"x": 1.15, "y": 1.15},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=round(t, 3), life=round(t + fall_t, 3)))
    return buf("vagrant_bone_rain", bullets)


# ---------------------------------------------------------------------------
# 1サビ後半: 両端の骸骨。左右端に骸骨(skull)を並べ、内側へ骨を段状に一斉射撃。
#   骸骨は上下に緩やかに揺れる(sine で y をずらした位置から発射)。volley を反復。
# ---------------------------------------------------------------------------
def skeleton_volley():
    bullets = []
    rows = 4
    n_volley = 9
    vol_interval = 3.0 * BEAT          # ~0.9s ごとに一斉射撃(密度を下げる)
    ys = [3.6 + r * 3.7 for r in range(rows)]   # y=3.6,7.3,11.0,14.7(隙間を広く)
    for v in range(n_volley):
        t = v * vol_interval
        yshift = 1.4 * math.sin(v * 0.9)        # 骸骨の上下揺れ
        skip_row = v % rows                     # 毎回1行だけ空ける=安全レーン(拍ごとに下→上へ移動)
        for r in range(rows):
            if r == skip_row:                   # 安全レーンは撃たない
                continue
            y = ys[r] + yshift
            spd = 8.2
            cross = (W + 3.0) / spd + 0.4
            # 左端 → 右へ
            bullets.append(bullet(
                originPos={"x": 1.2, "y": round(y, 2)}, speed=spd,
                polarForm={"x": 1, "y": 0.0}, gravity={"x": 0.0, "y": -1.5707963},
                typeName="bone", scale={"x": 0.95, "y": 0.95},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=round(t, 3), life=round(t + cross, 3)))
            # 右端 → 左へ
            bullets.append(bullet(
                originPos={"x": W - 1.2, "y": round(y, 2)}, speed=spd,
                polarForm={"x": 1, "y": math.pi}, gravity={"x": 0.0, "y": -1.5707963},
                typeName="bone", scale={"x": 0.95, "y": 0.95},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=round(t, 3), life=round(t + cross, 3)))
    return buf("vagrant_skeleton_volley", bullets)


def skeleton_markers():
    """左右端に骸骨(skull)の目印を置く。緩やかに上下ドリフト。"""
    bullets = []
    life = 10.0
    for edge_x in (0.7, W - 0.7):
        for i, y in enumerate([4.0, 9.0, 14.0]):
            vy = 0.5 if (i % 2 == 0) else -0.5
            bullets.append(bullet(
                originPos={"x": edge_x, "y": y}, originVlc={"x": 0, "y": vy},
                speed=0.0, polarForm={"x": 1, "y": math.pi / 2}, gravity={"x": 0.0, "y": 0.0},
                typeName="skull", scale={"x": 1.3, "y": 1.3},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=0.0, appearDuration=0.3, life=life))
    return buf("vagrant_skeleton_markers", bullets)


# ---------------------------------------------------------------------------
# 間奏: 死体破裂(中央から skull を全周放射)。2箇所で反復。
# ---------------------------------------------------------------------------
def corpse_burst():
    """間奏: 中央付近の様々な位置で死体が破裂し skull を全周放射。位置・角度・弾数をずらして反復。
    1バッファに全破裂(appearTime で展開)を入れ、t=26.5 で1回発火する。"""
    bullets = []
    n_burst = 11                             # 最後の破裂を t=41.5 に(2サビ 45.7 前に消えるよう)
    interval = 1.5
    for bi in range(n_burst):
        t = bi * interval
        cx = round(random.uniform(8.0, 24.0), 2)
        cy = round(random.uniform(8.5, 13.0), 2)
        offset = random.uniform(0, math.pi / 8)
        rings = 18 + (bi % 3) * 3            # 18/21/24 と弾数を変える
        spd = 4.4 + (bi % 2) * 0.6
        for i in range(rings):
            ang = offset + i * (2 * math.pi / rings)
            bullets.append(bullet(
                originPos={"x": cx, "y": cy}, speed=spd,
                polarForm={"x": 1, "y": round(ang, 5)}, gravity={"x": 0.0, "y": -1.5707963},
                angleSpeed=60, typeName="skull", scale={"x": 0.8, "y": 0.8},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=round(t, 3), life=round(t + 4.0, 3)))  # 残弾を早めに消す
    return buf("vagrant_corpse_burst", bullets)


# ---------------------------------------------------------------------------
# 間奏: 墓石の壁(横一列で下降、隙間で回避)。
# ---------------------------------------------------------------------------
def tombstone_wall():
    """間奏中に墓石の壁を複数回下降。各壁の隙間はランダム(同じ隙間の反復を避ける)。"""
    bullets = []
    cols = 8
    cw = W / cols
    n_walls = 5
    wall_interval = 3.8
    for wnum in range(n_walls):
        t = wnum * wall_interval
        gap = random.randint(1, cols - 3)
        for c in range(cols):
            if c in (gap, gap + 1):
                continue
            x = c * cw + cw / 2
            bullets.append(bullet(
                originPos={"x": round(x, 2), "y": H + 1.0}, speed=3.0,
                polarForm={"x": 1, "y": DOWN}, gravity={"x": 0.0, "y": -1.5707963},
                typeName="tombstone", scale={"x": 1.6, "y": 1.6},
                color={"x": 0, "y": 0, "z": 0, "w": 0},
                appearTime=round(t, 3), life=round(t + 7.5, 3)))
    return buf("vagrant_tombstone_wall", bullets)


# ---------------------------------------------------------------------------
# 2サビ: 幽霊。中心の周りを ghost 大玉8個が回転(polarForm+thetaVlc)。
#   別途、中心から緑の魔法弾(tear)を放射状に連射する。
# ---------------------------------------------------------------------------
def vagrant_laser():
    """間奏: 頭蓋骨(省略)が放つ横レーザー。墓石と交互に、ランダムな y に全幅ビーム。"""
    bullets = []
    n = 4
    interval = 3.8
    for i in range(n):
        t = i * interval + 1.9        # 墓石の壁の間に挟む
        y = round(random.uniform(4.0, 14.0), 2)
        bullets.append(dict(
            originPos={"x": -2, "y": y}, originVlc={"x": 0, "y": 0}, startX=0,
            speed=0, gravity=0, angleSpeed=0, polarForm={"x": 1, "y": 0.0},
            radiusVlc=0, thetaVlc=0, startPos={"x": 0, "y": 0},
            polynomial={"x": 0, "y": 0, "z": 0, "w": 0}, typeName="laser",
            size=220, color={"x": 0.4, "y": 0.95, "z": 0.55, "w": 1},
            appearTime=round(t, 3), life=round(t + 1.5, 3), random=0))
    return {"name": "vagrant_laser", "bullets": bullets, "homing": False, "isLaser": True}


def ghost_ring():
    # 固定半径の周回: disVector を単位ベクトルにするため startX=1・speed=0。
    # 位置 = origin + polarForm.x(半径) * rotate((1,0), theta)。thetaVlc で周回。
    bullets = []
    n = 8
    for i in range(n):
        ang = i * (2 * math.pi / n)
        bullets.append(bullet(
            originPos={"x": 0, "y": 0}, startPos={"x": 0, "y": 0}, startX=1, speed=0.0,
            polarForm={"x": 3.0, "y": round(ang, 5)}, thetaVlc=1.9,
            gravity={"x": 0.0, "y": 0.0},
            typeName="ghost", scale={"x": 1.4, "y": 1.4},
            color={"x": 0, "y": 0, "z": 0, "w": 0},
            appearTime=0.0, appearDuration=0.4, life=20.0))
    return buf("vagrant_ghost_ring", bullets)


def ghost_fire():
    """中心から緑の魔法弾を放射状に(1回分)。回転オフセット付きで反復発射。"""
    bullets = []
    n = 12
    for i in range(n):
        ang = i * (2 * math.pi / n)
        bullets.append(bullet(
            originPos={"x": 0, "y": 0}, speed=3.4,
            polarForm={"x": 1, "y": round(ang, 5)}, gravity={"x": 0.0, "y": -1.5707963},
            useVelocityAngle=True, typeName="tear", scale={"x": 0.9, "y": 0.9},
            color={"x": 0.45, "y": 0.95, "z": 0.55, "w": 1},
            appearTime=0.0, life=5.0))
    return buf("vagrant_ghost_fire", bullets)


def build():
    os.makedirs(BUFDIR, exist_ok=True)
    w_json(f"{BUFDIR}/bone_rain.json", bone_rain())
    w_json(f"{BUFDIR}/skeleton_volley.json", skeleton_volley())
    w_json(f"{BUFDIR}/skeleton_markers.json", skeleton_markers())
    w_json(f"{BUFDIR}/corpse_burst.json", corpse_burst())
    w_json(f"{BUFDIR}/tombstone_wall.json", tombstone_wall())
    # w_json(f"{BUFDIR}/vagrant_laser.json", vagrant_laser())  # 保留(laser 型未登録)
    w_json(f"{BUFDIR}/ghost_ring.json", ghost_ring())
    w_json(f"{BUFDIR}/ghost_fire.json", ghost_fire())

    # ---- vagrant.json 組み立て ----
    WHITE = {"x": 1, "y": 1, "z": 1, "w": 1}

    def spawner(clip, count, interval, time, pos, angle=0.0, ainterval=0.0):
        return {"clipName": clip, "count": count, "interval": interval, "time": time,
                "pos": pos, "originVlc": {"x": 0, "y": 0}, "angle": angle,
                "angleInterval": ainterval, "color": WHITE}

    sp = [
        # 1サビ: 骨の雨
        spawner("vagrant_bone_rain", 1, 0.0, 7.2, {"x": 0, "y": 0}),
        # 1サビ後半: 両端の骸骨(目印 + 一斉射撃)
        spawner("vagrant_skeleton_markers", 1, 0.0, 16.8, {"x": 0, "y": 0}),
        spawner("vagrant_skeleton_volley", 1, 0.0, 16.8, {"x": 0, "y": 0}),
        # 間奏: 死体破裂(中央付近の様々な位置。バッファ内で12回展開) + 墓石の壁
        spawner("vagrant_corpse_burst", 1, 0.0, 26.5, {"x": 0, "y": 0}),
        spawner("vagrant_tombstone_wall", 1, 0.0, 28.0, {"x": 0, "y": 0}),
        # レーザーは保留(BTDB に laser 型が無く、LASER は独自 Mesh 系統。要 laser 型追加+初期化調査)
        # spawner("vagrant_laser", 1, 0.0, 30.0, {"x": 0, "y": 0}),
        # 2サビ前半: 幽霊×1(左) 回転リング + 放射(spiral)
        spawner("vagrant_ghost_ring", 1, 0.0, 45.7, {"x": 10, "y": 10.5}),
        spawner("vagrant_ghost_fire", 30, 0.62, 46.0, {"x": 10, "y": 10.5}, angle=0.0, ainterval=13.0),
        # 2サビ後半: 幽霊×2(右を追加)。左右で逆回転の螺旋にして鏡像のクライマックスに
        spawner("vagrant_ghost_ring", 1, 0.0, 50.5, {"x": 22, "y": 10.5}),
        spawner("vagrant_ghost_fire", 24, 0.62, 50.5, {"x": 22, "y": 10.5}, angle=7.0, ainterval=-13.0),
    ]
    stage = {
        "stageName": "浮浪者",
        "MusicEvents": [{"barCount": 260, "BPM": BPM, "beatTimings": [0, 1, 2, 3], "measure": 4, "barStartOffsetBeats": 0}],
        "delayTime": 0.0, "endTime": 71.0, "stageDescription": "不死者・屍霊魔法",
        "enemyVisuals": [{
            "id": "vagrant", "source": "externalGif", "basePath": "Visuals",
            "pixelsPerUnit": 100, "transparentBackground": True, "transparentTolerance": 0,
            "pivot": {"x": 0.5, "y": 0.5},
            "clips": [{"name": "idle", "path": "vagrant_idle.gif", "loop": True, "frameDuration": 0.56, "maxFrames": 0}]
        }],
        "difficulties": [], "multiBulletSpawners": [],
        "bossSpawners": [{
            "bossId": "vagrant", "bossName": "浮浪者", "visualId": "vagrant",
            "appearTime": 1.0, "lifeTime": 68.0, "maxHp": 1000,
            "startPos": {"x": 16, "y": 13.6}, "scale": {"x": 2.2, "y": 2.2}, "angle": 0,
            "sortingOrder": 5, "fadeInSec": 1.2, "fadeOutSec": 1.0
        }],
        "bulletSpawners": sp
    }
    w_json(STAGE, stage)
    print("生成: bone_rain.json + vagrant.json")
    print(f"  bone_rain 弾数 = {len(bone_rain()['bullets'])}")

if __name__ == "__main__":
    build()
