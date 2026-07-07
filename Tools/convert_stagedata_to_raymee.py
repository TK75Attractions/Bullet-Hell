#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
marron 形式の StageData JSON(stone.json / captain.json 等)を raymee ランタイムの
top-level スキーマへ変換する（冪等・--dry-run）。BulletBuffer 側は別ツール
convert_bulletbuffers_to_raymee.py で変換すること。

変換内容(現物確認済み: StageDataManager.StageDataJson / BossSpawnerJson):
  - endTime を追加(欠落が Init "endTime must be greater than zero" 失敗の直接原因)。
      値 = max(最終 bulletSpawner time, max(enemy appearTime+life)) + tail。
  - enemySpawners → bossSpawners:
      appearTime=enemyAppearTime, lifeTime=life, startPos=orbit.originPos,
      scale=orbit.scale, visualId, animation(initialClip/events/triggers), maxHp=100。
      orbit.originVlc が非ゼロなら MoveTo(startPos → startPos+originVlc*life, life, linear)。
  - difficulties は空のまま(ローダが legacy top-level を Lunatic 自動ラップ)。
  - _generatedFrom / enemySpawners / patternEvents を削除。
  - bulletSpawners / MusicEvents / enemyVisuals / delayTime / stageName / stageDescription は維持。

未対応(warn を出す):
  - enemySpawner.bulletCount>0 の敵弾emit(captain 等)。raymee の BossSpawner は弾を撃たない。
    別途 bulletSpawners/multiBulletSpawners 化が必要(このツールでは視覚 bossSpawner のみ変換)。

冪等マーカー: enemySpawners キー不在 かつ endTime 有り なら変換済みとみなしスキップ。
BOM/改行は保持。
"""
import argparse
import json
import os
import sys

DOWN_ANGLE = -1.5707963
TAIL = 2.0


def boss_from_enemy(e, warns, stage):
    orb = e.get('orbit', {}) or {}
    opos = orb.get('originPos', {"x": 0, "y": 0})
    oscl = orb.get('scale', {"x": 1, "y": 1})
    life = e.get('life', orb.get('life', -1))
    anim = e.get('animation', {}) or {}
    sp = {
        "bossId": "",
        "bossName": e.get('enemyName', ""),
        "visualId": e.get('visualId', ""),
        "appearTime": e.get('enemyAppearTime', 0.0),
        "lifeTime": life,
        "maxHp": 100.0,
        "startPos": {"x": opos.get('x', 0), "y": opos.get('y', 0)},
        "scale": {"x": oscl.get('x', 1), "y": oscl.get('y', 1)},
        "angle": 0.0,
        "sortingOrder": e.get('sortingOrder', 0),
        "fadeInSec": e.get('fadeInSec', 0.0),
        "fadeOutSec": e.get('fadeOutSec', 0.0),
        "animation": {
            "initialClip": anim.get('initialClip', 'idle'),
            "events": anim.get('events', []),
            "triggers": anim.get('triggers', []),
        },
        "moves": []
    }
    ov = orb.get('originVlc', {"x": 0, "y": 0})
    if ov.get('x', 0) != 0 or ov.get('y', 0) != 0:
        sp['moves'] = [{
            "time": 0.0, "duration": life, "type": "moveto",
            "to": {"x": round(opos.get('x', 0) + ov['x'] * life, 4),
                   "y": round(opos.get('y', 0) + ov['y'] * life, 4)},
            "control": {"x": 0, "y": 0}, "easing": "linear", "relative": False
        }]
    if e.get('bulletCount', 0):
        warns.append(f"{stage}: enemy visualId={e.get('visualId')} appear={e.get('enemyAppearTime')} "
                     f"has bulletCount={e.get('bulletCount')} (tekidan emit) -> bossSpawner は弾を撃たないため未変換")
    return sp


def convert_stage(path, apply):
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    d = json.loads((raw[3:] if bom else raw).decode('utf-8'))
    crlf = b'\r\n' in (raw[3:] if bom else raw)
    stage = os.path.basename(os.path.dirname(path))

    already = ('enemySpawners' not in d) and (d.get('endTime') not in (None, 0))
    warns = []

    bspawn = d.get('bulletSpawners', []) or []
    enemies = d.get('enemySpawners', []) or []
    last_bullet = max((b.get('time', 0) for b in bspawn), default=0)
    last_enemy = max((e.get('enemyAppearTime', 0) + e.get('life', 0) for e in enemies), default=0)
    end_time = d.get('endTime') or round(max(last_bullet, last_enemy) + TAIL, 3)

    boss = [boss_from_enemy(e, warns, stage) for e in enemies]
    # 既存 bossSpawners があれば温存(将来の手動追加)
    boss = (d.get('bossSpawners', []) or []) + boss

    out = {
        "stageName": d.get('stageName', ''),
        "MusicEvents": d.get('MusicEvents', []),
        "delayTime": d.get('delayTime', 0.0),
        "endTime": end_time,
        "stageDescription": d.get('stageDescription', ''),
        "enemyVisuals": d.get('enemyVisuals', []),
        "difficulties": d.get('difficulties', []) or [],
        "multiBulletSpawners": d.get('multiBulletSpawners', []) or [],
        "bossSpawners": boss,
        "bulletSpawners": bspawn,
    }

    changed = (not already) or (d.get('endTime') != end_time)
    if apply and not already:
        text = json.dumps(out, ensure_ascii=False, indent=4).replace('\n', '\r\n' if crlf else '\n')
        payload = text.encode('utf-8')
        if bom:
            payload = b'\xef\xbb\xbf' + payload
        open(path, 'wb').write(payload)

    return {
        "stage": stage, "already": already, "endTime": end_time,
        "bulletSpawners": len(bspawn), "bossSpawners": len(boss), "warns": warns
    }


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--stage', help='ステージ名(Assets/StageData/<stage>/<stage>.json)。省略で全ステージ')
    ap.add_argument('--apply', action='store_true')
    args = ap.parse_args()

    base = 'Assets/StageData'
    stages = [args.stage] if args.stage else sorted(
        n for n in os.listdir(base) if os.path.isdir(os.path.join(base, n)))
    for s in stages:
        p = os.path.join(base, s, s + '.json')
        if not os.path.exists(p):
            cands = [f for f in os.listdir(os.path.join(base, s)) if f.endswith('.json')]
            if not cands:
                continue
            p = os.path.join(base, s, cands[0])
        try:
            r = convert_stage(p, args.apply)
        except Exception as e:
            print(f"SKIP {s}: {e}", file=sys.stderr)
            continue
        tag = "already-converted" if r['already'] else ("APPLIED" if args.apply else "DRY-RUN")
        print(f"[{tag}] {r['stage']:14} endTime={r['endTime']} bulletSpawners={r['bulletSpawners']} bossSpawners={r['bossSpawners']}")
        for w in r['warns']:
            print("   WARN:", w)


if __name__ == '__main__':
    main()
