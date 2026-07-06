# -*- coding: utf-8 -*-
"""石工の破片(stone_shard)クリップを機械監査し、life が exit(画面外/着地)より
早く切れて「途中で消える」ものを検出する。

トラジェクトリ(polynomial=0, polarForm.x=1 前提):
  x_local = speed * lapse                       (polynomial'=0 → nowCalculateVlc=(speed,0))
  disVector = (x_local, 0)
  rotated   = rotate(disVector, polarForm.y)    = (x*cos a, x*sin a)
  world     = rotated + originPos
  world.y  -= gravity * lapse^2 / 2              (lapse>0)
lapse = time - appearTime。life は clip 絶対時刻 (time>=life で消滅)。
bounds cull: -2 <= x < 36 かつ -2 <= y < 36。範囲を出た最初のフレームで消滅。
"""
import json, sys, math, glob, os

CULL_MIN = -2.0
CULL_MAX = 36.0
DT = 1.0 / 60.0
MARGIN = 0.15  # life を exit よりこれだけ後ろに置く(bounds cull を先に効かせる)

def load(path):
    with open(path, "rb") as f:
        raw = f.read()
    return json.loads(raw.decode("utf-8-sig"))

def exit_lapse(b):
    """appearTime からの相対 lapse で、範囲を最初に出る時刻を返す。"""
    ox, oy = b["originPos"]["x"], b["originPos"]["y"]
    ovx = b.get("originVlc", {}).get("x", 0.0)
    ovy = b.get("originVlc", {}).get("y", 0.0)
    speed = b.get("speed", 0.0)
    grav = b.get("gravity", 0.0)
    ang = b.get("polarForm", {}).get("y", 0.0)
    ca, sa = math.cos(ang), math.sin(ang)
    t = 0.0
    # 上限 20s まで探索
    while t < 20.0:
        # originPos も originVlc で動く
        cx = ox + ovx * t
        cy = oy + ovy * t
        x_local = speed * t
        wx = cx + x_local * ca
        wy = cy + x_local * sa - grav * t * t / 2.0
        if not (CULL_MIN <= wx < CULL_MAX and CULL_MIN <= wy < CULL_MAX):
            return t
        t += DT
    return 20.0

def main():
    files = sorted(glob.glob("Assets/BulletBuffers/stone/*.json"))
    any_bad = False
    for path in files:
        data = load(path)
        bullets = data.get("bullets", [])
        shards = [b for b in bullets if b.get("typeName") == "stone_shard"]
        if not shards:
            continue
        bad = []
        for i, b in enumerate(shards):
            at = b.get("appearTime", 0.0)
            life = b.get("life", 0.0)
            tex = exit_lapse(b)
            exit_abs = at + tex
            # life<=0 は無限life(消えない)→OK
            if life > 0 and life < exit_abs - 0.02:
                bad.append((i, at, life, exit_abs, tex))
        tag = "OK " if not bad else "BAD"
        print(f"[{tag}] {os.path.basename(path):30s} shards={len(shards):3d} premature={len(bad)}")
        for (i, at, life, exit_abs, tex) in bad[:6]:
            print(f"       #{i:3d} appear={at:6.3f} life={life:6.3f} exit_abs={exit_abs:6.3f} (fly {tex:5.2f}s)  short by {exit_abs-life:5.2f}s")
        if bad:
            any_bad = True
    print("\nRESULT:", "PREMATURE SHARDS FOUND" if any_bad else "all shards survive to exit")

if __name__ == "__main__":
    main()
