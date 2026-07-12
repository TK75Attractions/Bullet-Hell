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
            speed=fall_speed, polarForm={"x": 1, "y": DOWN},
            # 原典の縦棒は 200°/s で自転する(vangle=200)。エンジンの angleSpeed は
            # ラジアン/s、かつ描画角に効くのは useVelocityAngle=True の経路のみ
            # (False だと polarForm.y 固定で自転しない)。両方を正して初めて回る。
            # 落下は polarForm(真下)のままなので軌道・音ハメは不変。
            angleSpeed=round(math.radians(200), 5), useVelocityAngle=True,
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
#   壁(原典 Main.cpp 1353-1362): サブ波 time1={0,1.5,3}拍 × 本数 co1、
#     各壁は 20x25px セグメント(ピッチ25px)の破線帯。セグメントは 0.00375拍/個
#     ずつ左→右へ走って出現。予告(点滅)4拍 → 致死1拍。
#   レーザー(原典 1363-1366): 拍96+16i+j に co2[i][j] 本(4拍のクレッシェンド)。
#     予告(実寸の点滅帯+中心ガイド線)4拍 → 致死0.5拍 → 残光0.5拍。
#   y 配置: 原典の一様乱数は重なり・偏りが出るため、ブロック単位のレーン分割
#     +レーン内ランダム(タイル状ランダム配置)で「均一だが整列しない」に変更
#     (2026-07-12 ビジュアル改善便。ユーザー承認済みの原典乖離)。
#   非致死の予告は「appearTime を寿命より後ろに置く」ことで実現する:
#     当たり判定(BulletCollisionJob)と完全表示は time>=appearTime で始まるため、
#     生涯 [appearStart, life) をビート点滅のまま終える弾は一度も致死にならない。
#   オーブ: x=270/530 に拍8iで落下(4拍で着弾)、着弾点で 24方向リング爆発(拍8i+4)。
#   バッファは拍88で発火、内部は拍88=相対0。
# ---------------------------------------------------------------------------
def _tiled_ys(count, margin):
    """原典y域[20,579]を count レーンに均等分割し、各レーン内ランダム位置を
    シャッフル順で返す(タイル状ランダム配置: 全体は均一に埋まるが整列しない)。
    margin はレーン端からの最小距離(px)で、隣接帯の重なりを防ぐ。"""
    lo, hi = 20.0, 579.0
    lane_h = (hi - lo) / count
    assert lane_h > margin * 2, "レーンが margin に対して狭すぎる"
    ys = [lo + lane_h * i + random.uniform(margin, lane_h - margin) for i in range(count)]
    random.shuffle(ys)
    return ys


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
    # --- 水平壁(破線セグメント帯, サブ波 time1 × 本数 co1) ---
    #   旧実装は「全行一斉+単一の全幅箱」で、しかも appearTime=予告開始に
    #   置いていたため第1ブロックが予告ゼロで出現と同時に致死だった(修正済み)。
    seg_xs = [round(-1.5 + 0.75 * k, 3) for k in range(48)]   # 全幅[-1.8,33.75+0.3]を覆う
    time1 = [0.0, 1.5, 3.0]
    co1 = [[1, 2, 2], [1, 2, 2], [2, 3, 3], [2, 3, 3]]
    for bi in range(4):
        ys = _tiled_ys(sum(co1[bi]), margin=14)               # 帯半厚12.5px+余白
        yi = 0
        for j, cnt in enumerate(co1[bi]):
            for _ in range(cnt):
                y = my(ys[yi]); yi += 1
                wt = (bi * 16 + time1[j]) * BEAT              # 予告開始(原典 emerge)
                for k, sx in enumerate(seg_xs):
                    at = wt + 4 * BEAT + k * 0.00375 * BEAT   # 致死開始(判定4拍後+走り)
                    bullets.append(bullet(
                        originPos={"x": sx, "y": y}, speed=0.0, useVelocityAngle=False,
                        typeName="box", scale={"x": ms(20), "y": ms(25)}, color=dict(PINK),
                        appearTime=round(at, 3), appearDuration=round(4 * BEAT, 3),
                        life=round(at + 1 * BEAT, 3)))        # 判定1拍(原典 duration=1)
    # --- 水平レーザー(拍96+16i+j に co2[i][j] 本ずつのクレッシェンド) ---
    #   予告=実寸の点滅帯+中心ガイド線(非致死)。発射の瞬間に予告を消す(d=0)。
    co2 = [[1, 1, 2, 2], [1, 1, 2, 2], [2, 2, 3, 3], [2, 2, 3, 3]]
    LTH = ms(20)                                              # レーザー太さ 0.6u
    for bi in range(4):
        ys = _tiled_ys(sum(co2[bi]), margin=12)               # 帯半厚10px+余白
        yi = 0
        for j, cnt in enumerate(co2[bi]):
            for _ in range(cnt):
                y = my(ys[yi]); yi += 1
                wt = (8 + bi * 16 + j) * BEAT                 # 予告開始(拍96+16i+j)
                ft = wt + 4 * BEAT                            # 致死開始(原典 timelag2=4)
                never = ft + 2.0                              # 寿命より後=永遠に非致死
                # 予告はブラケット型(上下エッジ線+中心ガイド線)。塗り帯の予告は
                # 点滅の山で発射帯と輝度が並び静止画で誤読するため、シルエットで
                # 区別する。上下線が実寸(太さLTH)の端を示す(実測検証 2026-07-12)。
                edge_off = round((LTH - ms(3)) / 2, 4)
                for ey in (y + edge_off, y - edge_off):
                    bullets.append(bullet(   # 予告エッジ線(実寸の上端/下端)
                        originPos={"x": 16, "y": round(ey, 4)}, speed=0.0, useVelocityAngle=False,
                        typeName="box", scale={"x": 34.0, "y": ms(3)}, color=dict(PINK),
                        appearTime=round(never, 3), appearDuration=round(never - wt, 3),
                        life=round(ft, 3)))
                bullets.append(bullet(   # 予告の中心ガイド線(狙いの芯)
                    originPos={"x": 16, "y": y}, speed=0.0, useVelocityAngle=False,
                    typeName="box", scale={"x": 34.0, "y": ms(4)}, color=dict(PINK),
                    appearTime=round(never, 3), appearDuration=round(never - wt, 3),
                    life=round(ft, 3)))
                bullets.append(bullet(   # 発射: 実寸帯(致死0.5拍)
                    originPos={"x": 16, "y": y}, speed=0.0, useVelocityAngle=False,
                    typeName="box", scale={"x": 34.0, "y": LTH}, color=dict(PINK),
                    appearTime=round(ft, 3), appearDuration=0.05,
                    life=round(ft + 0.5 * BEAT, 3)))
                bullets.append(bullet(   # 残光: 0.5拍(非致死・点滅減衰。原典 timelag3=0.5)
                    originPos={"x": 16, "y": y}, speed=0.0, useVelocityAngle=False,
                    typeName="box", scale={"x": 34.0, "y": LTH}, color=dict(PINK),
                    appearTime=round(never + 2.0, 3),
                    appearDuration=round(never + 2.0 - (ft + 0.5 * BEAT), 3),
                    life=round(ft + 1.0 * BEAT, 3)))
    return buf("vagrant_main", bullets)


# ---------------------------------------------------------------------------
# ④ 幽霊 ghost(拍152 と 184、計2体)
#   8オーブ(半径125)のリングが45°/s自転しつつ、中心が矩形(150,150)→(150,450)→
#   (650,450)→(650,150)を16拍/辺で周回。4拍ごとに各オーブ位置から内向きの平行三角弾。
#   本体orbs=辺ごとに originVlc(線形移動)+polarForm(startX=1,speed=0)で周回。
#   三角弾=各fire時刻のオーブ位置を事前計算(center(t)+R*(cos,sin)(θ_g+j*45°))。
# ---------------------------------------------------------------------------
GHOST_PATH = [(150, 150), (150, 450), (650, 450), (650, 150)]   # 矩形の角(原典座標)
GHOST_ATK = [0.0, math.pi / 2, math.pi, 3 * math.pi / 2]        # 各辺の内向きUnity角(右/上/左/下)


def _ghost(t0_rel, n_sides):
    bullets = []
    R = ms(125); n_orbs = 8; d_orb = 2 * math.pi / n_orbs   # 半径125px=3.75u(原典どおり)
    theta_rate = math.radians(45)          # 自転(rad/s)
    side_dur = 16 * BEAT
    orb_sc = ms(50)                        # size_radius25 → 直径50
    theta_g = 0.0
    # 本体オーブ(辺ごとに再spawn)
    for si in range(n_sides):
        p = si % 4
        c0 = GHOST_PATH[p]; c1 = GHOST_PATH[(p + 1) % 4]
        cs = (mx(c0[0]), my(c0[1])); ce = (mx(c1[0]), my(c1[1]))
        ovx = round((ce[0] - cs[0]) / side_dur, 4); ovy = round((ce[1] - cs[1]) / side_dur, 4)
        st = t0_rel + si * side_dur
        for j in range(n_orbs):
            a0 = (theta_g + j * d_orb) % (2 * math.pi)
            bullets.append(bullet(
                originPos={"x": cs[0], "y": cs[1]}, originVlc={"x": ovx, "y": ovy},
                # startX は spawn 経路で効かず nowCalculateX=0 になるため、startPos=(-1,0) で
                # disVector=(x=0)-(-1,0)=(1,0) を強制 → 半径 = polarForm.x になる。
                startX=0, startPos={"x": -1, "y": 0}, speed=0.0,
                polarForm={"x": R, "y": round(a0, 5)}, thetaVlc=round(theta_rate, 5),
                gravity={"x": 0, "y": 0}, useVelocityAngle=False,
                typeName="vcirc", scale={"x": orb_sc, "y": orb_sc}, color=dict(PINK),
                appearTime=round(st, 3), appearDuration=(0.3 if si == 0 else 0.0),
                life=round(st + side_dur + 0.03, 3)))
        theta_g += theta_rate * side_dur
    # 三角弾(4拍ごと、at_first=2拍)
    total = n_sides * side_dur
    ft = 2 * BEAT
    while ft < total - 1e-6:
        si = min(int(ft // side_dur), n_sides - 1); p = si % 4
        tt = ft - si * side_dur
        c0 = GHOST_PATH[p]; c1 = GHOST_PATH[(p + 1) % 4]
        cx = mx(c0[0]) + (mx(c1[0]) - mx(c0[0])) * tt / side_dur
        cy = my(c0[1]) + (my(c1[1]) - my(c0[1])) * tt / side_dur
        th = theta_rate * ft
        aang = GHOST_ATK[p]
        for j in range(n_orbs):
            a = th + j * d_orb
            ox = cx + R * math.cos(a); oy = cy + R * math.sin(a)
            bullets.append(bullet(
                originPos={"x": round(ox, 3), "y": round(oy, 3)}, speed=ms(300),
                # angleSpeed はラジアン/s(度で 120 を渡すと毎秒19回転のエイリアシング)。
                polarForm={"x": 1, "y": round(aang, 5)}, useVelocityAngle=True,
                angleSpeed=round(math.radians(120), 5),
                typeName="vtri", scale={"x": ms(30), "y": ms(30)}, color=dict(PINK),
                appearTime=round(t0_rel + ft, 3), appearDuration=0.05, life=round(t0_rel + ft + 4.0, 3)))
        ft += 4 * BEAT
    return bullets


def ghosts():
    return buf("vagrant_ghosts", _ghost(0.0, 4) + _ghost(32 * BEAT, 2))


def build():
    os.makedirs(BUFDIR, exist_ok=True)
    w_json(f"{BUFDIR}/intro_bars.json", intro_bars())
    w_json(f"{BUFDIR}/skeletons.json", skeletons())
    w_json(f"{BUFDIR}/main_section.json", main_section())
    w_json(f"{BUFDIR}/ghosts.json", ghosts())

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
        # ④ 幽霊(拍152=45.71s で発火、幽霊1=1周・幽霊2=相対32拍後2辺)
        spawner("vagrant_ghosts", 1, 0.0, round(152 * B, 3), {"x": 0, "y": 0}),
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
            "startPos": {"x": 16, "y": 15.0}, "scale": {"x": 5.0, "y": 5.0}, "angle": 0,
            "sortingOrder": 5, "fadeInSec": 1.2, "fadeOutSec": 1.0
        }],
        "bulletSpawners": sp
    }
    w_json(STAGE, stage)
    print("生成: intro_bars.json + vagrant.json (①イントロのみ)")
    print(f"  intro_bars 弾数 = {len(intro_bars()['bullets'])}")


if __name__ == "__main__":
    build()
