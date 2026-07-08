# -*- coding: utf-8 -*-
"""浮浪者(vagrant)ステージを原典(Siv3D Main.cpp)に忠実に再現する生成器。
仕様は Instructions/浮浪者/vagrant-danmaku-spec.md 参照。

原典: 画面800x600(原点左上, y下向き正)、全弾ピンク HSV(330)=RGB(1,0,0.5)、BPM199.5。
Unity: 32x18(y上向き正)。円が歪まないよう等倍(×SCALE)で高さ基準・横中央寄せ+ y反転。
  x_u = x_orig*SCALE + XOFF,  y_u = 18 - y_orig*SCALE,  size_u = size_orig*SCALE
  速度も px/s → u/s へ ×SCALE。角度(度)はそのまま(Unity spawnerは度)。

段階実装: ①イントロ(回転縦棒の雨) 済 / ②骨柱 ③主部 ④幽霊 は順次追加。
"""
import os, json, math, random

random.seed(1600)
BPM = 199.5
BEAT = 60.0 / BPM              # 0.30075s
SCALE = 0.03                   # 600px→18u(高さ基準)。800px→24u(中央寄せ)
XOFF = 4.0                     # 横中央寄せ(800*0.03=24 を 32 の中央へ: (32-24)/2)
BUFDIR = "Assets/BulletBuffers/vagrant"
STAGE = "Assets/StageData/vagrant/vagrant.json"
PINK = {"x": 1.0, "y": 0.0, "z": 0.5, "w": 1.0}   # HSV(330,1,1)


def mx(x):  # 原典x(px) → Unity x
    return round(x * SCALE + XOFF, 3)


def my(y):  # 原典y(px, 下向き) → Unity y(上向き)
    return round(18.0 - y * SCALE, 3)


def ms(s):  # 原典サイズ/速度(px) → Unity(u)
    return round(s * SCALE, 4)


def w_json(path, obj):
    os.makedirs(os.path.dirname(path), exist_ok=True)
    open(path, "wb").write(json.dumps(obj, ensure_ascii=False, indent=4).encode("utf-8"))


def bullet(**kw):
    b = dict(
        originPos={"x": 0, "y": 0}, originVlc={"x": 0, "y": 0}, startX=0,
        speed=0.0, gravity={"x": 0.0, "y": 0.0},
        initialAngle=0, angleSpeed=0.0, useVelocityAngle=False,
        polarForm={"x": 1, "y": 0.0}, radiusVlc=0, radiusAccel=0, thetaVlc=0, thetaAccel=0,
        startPos={"x": 0, "y": 0}, polynomial={"x": 0, "y": 0, "z": 0, "w": 0},
        typeName="box", scale={"x": 1.0, "y": 1.0}, color=dict(PINK),
        appearTime=0.0, appearDuration=0.05, life=4.0, random=0, unCounterable=True)
    b.update(kw)
    return b


def buf(name, bullets, isLaser=False):
    return {"name": name, "bullets": bullets, "homing": False, "isLaser": isLaser}


DOWN = -math.pi / 2

# ---------------------------------------------------------------------------
# ① イントロ「回転縦棒の雨」(rect 10x50, 拍23–82 毎拍1本=60本)
#   px=rand(50,749), py=50(上端), vy=300(下), vangle=200°/s(自転)。
#   原典は warn0.5拍/判定開始1拍/duration15拍。Unity は静止予告が無いので、
#   落下開始をその拍に合わせ、短いフェードインで近似する。
# ---------------------------------------------------------------------------
def intro_bars():
    bullets = []
    start_beat, end_beat = 23, 82
    fall_speed = ms(300)                       # 9 u/s
    top_y = 50
    for beat in range(start_beat, end_beat + 1):
        t = (beat - start_beat) * BEAT         # バッファ発火(拍23)からの相対秒
        px = random.uniform(50, 749)
        bullets.append(bullet(
            originPos={"x": mx(px), "y": my(top_y)},
            speed=fall_speed, polarForm={"x": 1, "y": DOWN}, useVelocityAngle=True,
            # Unity は弾が速度方向を向く。速度=下なので、長軸(50)を速度方向(scale.x)に、
            # 幅(10)を scale.y に置く → 画面上は 0.3幅 x 1.5高 の縦棒として落ちる。
            angleSpeed=0.0,
            typeName="box", scale={"x": ms(50), "y": ms(10)},
            color=dict(PINK),
            appearTime=round(t, 3), appearDuration=0.12,
            life=round(t + (18.0 + 2.0) / fall_speed + 0.3, 3)))   # 画面下へ抜けるまで
    return buf("vagrant_intro_bars", bullets)


# ---------------------------------------------------------------------------
# ② 骨柱 skeleton(拍56–88攻撃, 8体)
#   30x100の縦長骨柱。左4(x=50,v=+100下)/右4(x=750,v=-100上)。y∈[100,500]で完全反射。
#   2拍ごとに放物線の骨(50x10, 速さ400, gravity400)を内側へ。左at_first=0/右=1で交互。
#   engine仕様: 本体もemergeするので、バウンドを事前計算し「反射ごとの線形区間」を弾として出す。
# ---------------------------------------------------------------------------
def _bounce_segs(py0, v0, lower, upper, t1):
    """原典座標(px, 下向き)で t∈[0,t1] のバウンド経路を線形区間 (t0,y0,te,ye) の列で返す。v は px/s。"""
    segs = []; y = py0; v = v0; t = 0.0
    while t < t1 - 1e-6 and v != 0:
        bound = upper if v > 0 else lower
        dt = (bound - y) / v
        te = min(t + dt, t1)
        ye = y + v * (te - t)
        segs.append((t, y, te, ye))
        if te >= t1 - 1e-6:
            break
        y = ye; v = -v; t = te
    return segs


def _y_at(segs, t):
    for (t0, y0, te, ye) in segs:
        if t0 <= t <= te + 1e-6:
            return y0 + (ye - y0) * (t - t0) / (te - t0) if te > t0 else y0
    return segs[-1][3]


def skeletons():
    bullets = []
    dur = 32 * BEAT                      # 拍56–88 = 9.6s
    lower, upper = 100, 500
    pillars = ([(50, py, +100, 0) for py in (225, 275, 325, 375)] +
               [(750, py, -100, 1) for py in (375, 325, 275, 225)])
    for (x, py0, v, atf) in pillars:
        segs = _bounce_segs(py0, v, lower, upper, dur)
        # 骨柱本体: 反射ごとの線形区間を縦移動の弾で(useVelocityAngle=速度=縦→長軸を縦に)
        for (t0, y0, te, ye) in segs:
            vy_u = (my(ye) - my(y0)) / (te - t0) if te > t0 else 0.0
            bullets.append(bullet(
                originPos={"x": mx(x), "y": my(y0)}, originVlc={"x": 0, "y": round(vy_u, 4)},
                speed=0.0, gravity={"x": 0, "y": 0}, useVelocityAngle=True,
                typeName="box", scale={"x": ms(100), "y": ms(30)},   # 長軸(100)を速度方向へ=縦3 x 幅0.9
                color=dict(PINK), appearTime=round(t0, 3), appearDuration=0.0,
                life=round(te + 0.05, 3)))
        # 骨: 2拍ごとに現在のyから放物線射出
        angle = 0.0 if x < 400 else math.pi
        fb = atf
        while fb < 32:
            ft = fb * BEAT
            y = _y_at(segs, ft)
            bullets.append(bullet(
                originPos={"x": mx(x), "y": my(y)}, speed=ms(400),
                polarForm={"x": 1, "y": round(angle, 5)}, gravity={"x": ms(400), "y": DOWN},
                useVelocityAngle=True, typeName="box", scale={"x": ms(50), "y": ms(10)},
                color=dict(PINK), appearTime=round(ft, 3), appearDuration=0.05,
                life=round(ft + 3.0, 3)))
            fb += 2
    return buf("vagrant_skeletons", bullets)


# ---------------------------------------------------------------------------
# ③ 主部(拍88–152)：水平壁 + 水平レーザー + オーブ落下→24方向リング
#   壁: ブロック(拍88,104,120,136)ごとに 5/5/8/8 行、ランダムY の全幅横帯。予告後フラッシュ。
#   レーザー: ブロック(拍96,112,128,144)ごとに 6/6/10/10 本、ランダムY の全幅細帯。
#   オーブ: x=270/530 に拍8iで落下(4拍で着弾)、着弾点で 24方向リング爆発(拍8i+4)。
#   バッファは拍88で発火、内部は拍88=相対0。
# ---------------------------------------------------------------------------
def main_section():
    bullets = []
    # --- オーブ落下 + 24方向リング ---
    for i in range(8):
        ob = 8 * i
        for ox in (270, 530):
            fall_t = ob * BEAT
            land_t = (ob + 4) * BEAT
            bullets.append(bullet(   # オーブ(大きい円が降ってくる)
                originPos={"x": mx(ox), "y": my(-25)}, speed=ms(270), polarForm={"x": 1, "y": DOWN},
                useVelocityAngle=False, typeName="vcirc", scale={"x": ms(60), "y": ms(60)},
                color=dict(PINK), appearTime=round(fall_t, 3), appearDuration=0.1,
                life=round(land_t + 0.12, 3)))
            for k in range(24):      # 着弾点で24方向リング
                ang = k * (2 * math.pi / 24)
                bullets.append(bullet(
                    originPos={"x": mx(ox), "y": my(300)}, speed=ms(400),
                    polarForm={"x": 1, "y": round(ang, 5)}, useVelocityAngle=False,
                    typeName="vcirc", scale={"x": ms(16), "y": ms(16)}, color=dict(PINK),
                    appearTime=round(land_t, 3), appearDuration=0.03, life=round(land_t + 3.0, 3)))
    # --- 水平壁(全幅横帯, ランダムY) ---
    for bi, nrows in enumerate([5, 5, 8, 8]):
        bt = (bi * 16) * BEAT
        for _ in range(nrows):
            py = random.uniform(20, 579)
            bullets.append(bullet(
                originPos={"x": 16, "y": my(py)}, speed=0.0, useVelocityAngle=False,
                typeName="box", scale={"x": 32.0, "y": ms(25)}, color=dict(PINK),
                appearTime=round(bt, 3), appearDuration=round(4 * BEAT, 3),   # 4拍予告
                life=round(bt + 4 * BEAT + 1 * BEAT + 0.2, 3)))               # 判定1拍+余韻
    # --- 水平レーザー(全幅細帯, ランダムY) ---
    for bi, ncnt in enumerate([6, 6, 10, 10]):
        bt = (8 + bi * 16) * BEAT
        for _ in range(ncnt):
            py = random.uniform(20, 579)
            bullets.append(bullet(
                originPos={"x": 16, "y": my(py)}, speed=0.0, useVelocityAngle=False,
                typeName="box", scale={"x": 34.0, "y": ms(18)}, color=dict(PINK),
                appearTime=round(bt, 3), appearDuration=round(4 * BEAT, 3),
                life=round(bt + 4 * BEAT + 0.5 * BEAT + 0.15, 3)))
    return buf("vagrant_main", bullets)


# ④幽霊 は次段で追加。


def build():
    os.makedirs(BUFDIR, exist_ok=True)
    w_json(f"{BUFDIR}/intro_bars.json", intro_bars())
    w_json(f"{BUFDIR}/skeletons.json", skeletons())
    w_json(f"{BUFDIR}/main_section.json", main_section())

    WHITE = {"x": 1, "y": 1, "z": 1, "w": 1}

    def spawner(clip, count, interval, time, pos, angle=0.0, ainterval=0.0):
        return {"clipName": clip, "count": count, "interval": interval, "time": time,
                "pos": pos, "originVlc": {"x": 0, "y": 0}, "angle": angle,
                "angleInterval": ainterval, "color": WHITE}

    B = BEAT
    sp = [
        # ① イントロ(拍23=6.917s で発火、バッファ内で拍23–82 を展開)
        spawner("vagrant_intro_bars", 1, 0.0, round(23 * B, 3), {"x": 0, "y": 0}),
        # ② 骨柱(拍56=16.84s で発火、バッファ内で拍56–88 を展開)
        spawner("vagrant_skeletons", 1, 0.0, round(56 * B, 3), {"x": 0, "y": 0}),
        # ③ 主部(拍88=26.47s で発火、バッファ内で拍88–152 を展開)
        spawner("vagrant_main", 1, 0.0, round(88 * B, 3), {"x": 0, "y": 0}),
    ]

    stage = {
        "stageName": "浮浪者",
        "MusicEvents": [{"barCount": 260, "BPM": BPM, "beatTimings": [0, 1, 2, 3], "measure": 4, "barStartOffsetBeats": 0}],
        "delayTime": 0.0, "endTime": 70.0, "stageDescription": "不死者・屍霊魔法",
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
            "startPos": {"x": 16, "y": 15.5}, "scale": {"x": 2.2, "y": 2.2}, "angle": 0,
            "sortingOrder": 5, "fadeInSec": 1.2, "fadeOutSec": 1.0
        }],
        "bulletSpawners": sp
    }
    w_json(STAGE, stage)
    print("生成: intro_bars.json + vagrant.json (①イントロのみ)")
    print(f"  intro_bars 弾数 = {len(intro_bars()['bullets'])}")


if __name__ == "__main__":
    build()
