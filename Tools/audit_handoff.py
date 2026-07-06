# -*- coding: utf-8 -*-
"""石工タイル/レイン区間の spawn->drop->settle->belt ハンドオフを機械監査する。
各ブロックの appear/disappear 絶対時刻と、ハンドオフ時刻での位置(multiset)を照合し、
gap(消える)/overlap(二重像)/位置ズレ を検出する。"""
import json, glob, os, math

BEAT = 60.0 / 144.0  # 0.416667

MARKERS = {}
def bt(s):
    s = str(s).strip()
    # marker expression like "M21" or "M21+1beat"
    if s.startswith("M"):
        base = s; add = 0.0
        for op in ("+", "-"):
            if op in s[1:]:
                base, rest = s.split(op, 1)
                rest = rest.strip()
                sign = 1 if op == "+" else -1
                if "beat" in rest:
                    add = sign * float(rest.replace("beat", "").strip()) * BEAT
                elif "bar" in rest:
                    add = sign * float(rest.replace("bar", "").strip()) * 4 * BEAT
                break
        return bt(MARKERS[base.strip()]) + add
    b, t = s.split(":")
    return ((int(b) - 1) * 4 + (float(t) - 1)) * BEAT

def load(p):
    return json.load(open(p, encoding="utf-8-sig"))

# name -> file  and  basename -> name
name2file = {}
base2name = {}
for p in glob.glob("Assets/BulletBuffers/stone/*.json"):
    try:
        o = load(p)
    except Exception:
        continue
    base = os.path.splitext(os.path.basename(p))[0]
    nm = o.get("name") or base
    name2file[nm] = p
    base2name[base] = nm

def N(base):
    """resolve buffer file basename -> chart clip name"""
    return base2name[base]

chart = load("Assets/StageData/stone/stone.chart.json")
MARKERS.update(chart.get("markers", {}))
# clip name -> list of fire times
fires = {}
for e in chart["events"]:
    if "clip" not in e or "at" not in e:
        continue
    try:
        fires.setdefault(e["clip"], []).append(bt(e["at"]))
    except Exception:
        pass

def pos_at(b, lapse):
    """straight/gravity block position at given lapse (>=0)."""
    op = b["originPos"]; ov = b.get("originVlc", {"x":0,"y":0})
    x = op["x"] + ov["x"] * lapse
    y = op["y"] + ov["y"] * lapse
    g = b.get("gravity", 0)
    if g and lapse > 0:
        y -= g * lapse * lapse / 2.0
    return (x, y)

def clip_blocks(name):
    """return list of (fire, appear_abs, disappear_abs, appearT, life, bullet)."""
    f = name2file.get(name)
    if not f:
        return None
    o = load(f)
    out = []
    for ft in fires.get(name, []):
        for b in o["bullets"]:
            at = b.get("appearTime", 0); life = b.get("life", 0)
            appear = ft + at
            disappear = ft + life if life > 0 else None
            out.append((ft, appear, disappear, at, life, b))
    return out

def report_handoff(a_name, b_name, label):
    print(f"\n### {label}: {a_name}  ->  {b_name}")
    A = clip_blocks(a_name); B = clip_blocks(b_name)
    if A is None or B is None:
        print("  (clip missing)"); return
    af = A[0][0]; bf = B[0][0]
    print(f"  fireA={af:.3f}  fireB={bf:.3f}")
    a_dis = sorted(set(round(x[2],4) for x in A if x[2] is not None))
    b_app = sorted(set(round(x[1],4) for x in B))
    print(f"  A disappear times: {a_dis}")
    print(f"  B appear    times: {b_app}")
    # handoff time = B first appear
    th = min(x[1] for x in B)
    # A blocks alive just before th, position at th
    def posset(clip, t):
        s = []
        for ft, appear, disappear, at, life, b in clip:
            if appear <= t and (disappear is None or t < disappear + 1e-6):
                s.append(pos_at(b, t - ft))
        return s
    aset = posset(A, th - 1e-4)
    bset = posset(B, th + 1e-4)
    def rnd(s): return sorted((round(x,2), round(y,2)) for x,y in s)
    ra, rb = rnd(aset), rnd(bset)
    print(f"  @handoff t={th:.3f}: |A|={len(ra)} |B|={len(rb)}  match={ra==rb}")
    if ra != rb:
        onlyA = [p for p in ra if p not in rb]
        onlyB = [p for p in rb if p not in ra]
        print(f"    onlyA={onlyA}")
        print(f"    onlyB={onlyB}")
    # gap/overlap per block: check continuity across the whole chain endpoints
    return af, bf, th

# Section 1 & 2 tile chain
for sec in ("1", "2"):
    print(f"\n========== TILE SECTION {sec} ==========")
    spawn_names = [N(f"stone_tile_spawn_{sec}_{s}") for s in "abcd"]
    # verify spawn subclip disappear vs drop appear
    drop = N(f"stone_tile_drop_{sec}")
    settle = N(f"stone_tile_settle_{sec}")
    belt = N(f"stone_belt_flow_{sec}")
    # spawn -> drop
    dropf = fires.get(drop, [None])[0]
    print(f"drop fires at {dropf:.3f}" if dropf else "no drop")
    for sn in spawn_names:
        sb = clip_blocks(sn)
        if not sb:
            print(f"  {sn}: MISSING"); continue
        ft = sb[0][0]; dis = sb[0][2]
        gap = (dropf - dis) if (dis and dropf) else None
        print(f"  {sn}: fire={ft:.3f} disappear={dis:.3f} -> drop gap={gap:+.3f}s "
              + ("<-- GAP(消え)" if gap and gap>1e-3 else ("<-- OVERLAP(二重)" if gap and gap<-1e-3 else "OK")))
    report_handoff(drop, settle, f"drop->settle sec{sec}")
    report_handoff(settle, belt, f"settle->belt sec{sec}")


# ---------------------------------------------------------------------------
# 拡張(第36便): fresh-fire ハンドオフのフレーム単位ギャップ点検
#
# 実測で判明した根本原因: incoming クリップが「そのハンドオフ時刻に新規発火」
# される対 (= incoming の appearTime==0) は、初回スポーンが約1ゲームフレーム
# 遅れて描画されるため、ハードカット型の outgoing が先に消えて 1 フレームの
# 継ぎ目(黒み/一瞬消え)が出る。数式上は gap=0 でも実レンダリングで欠ける。
# 一方 settle/warn のように appearTime>0 で「事前発火→appearTime で可視化」
# される incoming は初回スポーン済みなので gap は出ない(実測 drop->settle は clean)。
#
# よってブロックが持続する fresh-fire ハンドオフ(spawn->drop, settle->belt)は
# outgoing の life に微小オーバーラップ余白を持たせる必要がある。ここでは
# outgoing.disappear >= incoming_fire + MIN_OVERLAP を要求し、不足を FLAG する。
# MIN_OVERLAP は 1 ゲームフレーム+ジッタを吸収する 0.02s。
MIN_OVERLAP = 0.020

def first_bullet(name):
    f = name2file.get(name)
    if not f:
        return None
    o = load(f)
    return o["bullets"][0]

def fresh_check(out_bases, in_base, label):
    """out_bases: list of buffer basenames (persistent blocks) that must survive
    until the freshly-fired incoming clip's first render frame."""
    inn = base2name.get(in_base)
    if inn is None:
        print(f"  [{label}] incoming {in_base} MISSING"); return
    inf = fires.get(inn, [None])[0]
    ib = first_bullet(inn)
    if inf is None or ib is None:
        print(f"  [{label}] incoming {in_base} no-fire"); return
    in_at = ib.get("appearTime", 0)
    in_ov = ib.get("originVlc", {"x": 0, "y": 0}) or {"x": 0, "y": 0}
    fresh = abs(in_at) < 1e-6
    in_appear = inf + in_at
    moving = abs(in_ov.get("x", 0)) > 1e-6 or abs(in_ov.get("y", 0)) > 1e-6
    kind = f"belt/ov=({in_ov.get('x',0)},{in_ov.get('y',0)})" if moving else "gravity/static"
    tag = "FRESH(gap-risk)" if fresh else "pre-fired(safe)"
    print(f"  [{label}] incoming={in_base} appearTime={in_at} {tag} incoming={kind}")
    worst = None
    for ob in out_bases:
        on = base2name.get(ob)
        if on is None:
            print(f"     out {ob}: MISSING"); continue
        of = fires.get(on, [None])[0]
        b = first_bullet(on)
        if of is None or b is None:
            print(f"     out {ob}: no-fire"); continue
        life = b.get("life", 0)
        dis = of + life
        margin = dis - in_appear  # >0 = overlap(good), <0 = gap
        worst = margin if worst is None else min(worst, margin)
        state = "OK-overlap" if margin >= MIN_OVERLAP else ("!!GAP(消え)" if margin < 0 else "!!TIGHT(1F消え risk)")
        print(f"     out {ob:26s} disappear={dis:.4f} incoming_appear={in_appear:.4f} margin={margin:+.4f}s {state}")
    if fresh and worst is not None and worst < MIN_OVERLAP:
        need = MIN_OVERLAP - worst
        print(f"     ==> FLAG: fresh-fire かつ margin<{MIN_OVERLAP}s。outgoing life を +{need:.3f}s 以上延長せよ")

print("\n\n================= FRESH-FIRE HANDOFF AUDIT (第36便拡張) =================")
for sec in ("1", "2"):
    print(f"\n----- TILE section {sec} -----")
    fresh_check([f"stone_tile_spawn_{sec}_{s}" for s in "abcd"],
                f"stone_tile_drop_{sec}", f"tile spawn->drop {sec}")
    fresh_check([f"stone_tile_drop_{sec}"], f"stone_tile_settle_{sec}",
                f"tile drop->settle {sec}")
    fresh_check([f"stone_tile_settle_{sec}"], f"stone_belt_flow_{sec}",
                f"tile settle->belt {sec}")
for sec in ("1", "2"):
    print(f"\n----- RAIN section {sec} -----")
    fresh_check([f"stone_rain_drop_{sec}_{s}" for s in "ac"],
                f"stone_rain_settle_{sec}_a", f"rain drop->settle {sec} (a)")
    fresh_check([f"stone_rain_settle_{sec}_{s}" for s in "ac"],
                f"stone_rain_belt_flow_{sec}", f"rain settle->belt {sec}")
print(f"\n----- MASS drop -----")
fresh_check(["mass_drop_3"], "mass_settle_3", "mass drop->settle")
