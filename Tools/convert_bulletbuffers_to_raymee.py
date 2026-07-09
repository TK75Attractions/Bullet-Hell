#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
marron 形式の BulletBuffer JSON を raymee ランタイムのスキーマへ変換する（冪等・--dry-run）。

raymee の BulletDataJson との差分(現物確認済み):
  - gravity: marron は number(下向き加速度の大きさ) / raymee は Vector2 極形式(大きさ, 角度rad)。
      → {"x": G, "y": -1.5707963}  (下向き = -pi/2)。G は符号ごと通る(負=上向き)。
      JsonUtility は number を Vector2 に読めず {0,0} になる=落下弾が全滅するため必須変換。
  - size: marron のスカラ / raymee には無い(scale のみ)。
      → scale が未設定 or {0,0} のときだけ scale={size,size} にし、size を削除。
  - lockRotation: marron の「velocity 角へ回さない」/ raymee は useVelocityAngle(既定 true)。
      → lockRotation:true は useVelocityAngle:false を明示、lockRotation:false は削除のみ
        (raymee ローダが未指定を true に補完するため)。いずれも lockRotation を削除。
  - radiusAccel/thetaAccel/warpCooldown/useVelocityAngle(未指定) は JsonUtility/ローダが 0/true 補完
    するので明示追加は不要。

冪等マーカー: gravity が object かつ size キー不在かつ lockRotation キー不在なら変換済み。
各ファイルの BOM 有無・改行(CRLF/LF)は保持する(text-mode 書き込みの CRLF 破壊を避け binary 書込)。

使い方:
  python Tools/convert_bulletbuffers_to_raymee.py [--path Assets/BulletBuffers] [--apply]
デフォルトは --dry-run(書き込まない)。--apply で実書込。
"""
import argparse
import glob
import json
import os
import sys

DOWN_ANGLE = -1.5707963  # -pi/2, 下向き


def convert_bullet(b):
    """1弾を変換した dict と、変わったかの bool を返す。フィールド順は保持。"""
    changed = False
    had_size = 'size' in b
    size_val = b.get('size')
    scale_val = b.get('scale')

    out = {}
    for k, v in b.items():
        if k == 'size':
            # scale 側でまとめて処理するのでここでは出さない
            changed = True
            continue
        if k == 'gravity' and not isinstance(v, dict):
            out['gravity'] = {"x": v, "y": DOWN_ANGLE}
            changed = True
            continue
        if k == 'lockRotation':
            if v is True:
                out['useVelocityAngle'] = False  # 「回さない」を明示
            # false は削除のみ(ローダが未指定を true=回す に補完)
            changed = True
            continue
        out[k] = v

    # size -> scale (scale 未設定 or {0,0} のときだけ size を採用)
    if had_size:
        def is_zero(s):
            return s is None or (float(s.get('x', 0) or 0) == 0 and float(s.get('y', 0) or 0) == 0)
        if is_zero(scale_val):
            out['scale'] = {"x": size_val, "y": size_val}
        # scale が既に非ゼロなら既存 scale を尊重(out に既にコピー済み)

    return out, changed


def read_file(path):
    raw = open(path, 'rb').read()
    bom = raw.startswith(b'\xef\xbb\xbf')
    body = raw[3:] if bom else raw
    crlf = b'\r\n' in body
    text = body.decode('utf-8')
    return json.loads(text), bom, crlf


def write_file(path, data, bom, crlf):
    text = json.dumps(data, ensure_ascii=False, indent=4)
    if crlf:
        text = text.replace('\n', '\r\n')
    payload = text.encode('utf-8')
    if bom:
        payload = b'\xef\xbb\xbf' + payload
    open(path, 'wb').write(payload)


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument('--path', default='Assets/BulletBuffers')
    ap.add_argument('--apply', action='store_true', help='実際に書き込む(既定は dry-run)')
    ap.add_argument('--sample', type=int, default=1, help='dry-run 時に差分サンプルを出すファイル数')
    args = ap.parse_args()

    files = sorted(glob.glob(os.path.join(args.path, '**', '*.json'), recursive=True))
    tot_files = tot_bullets = ch_files = ch_bullets = 0
    grav_c = size_c = lock_true_c = lock_false_c = 0
    samples_shown = 0

    for f in files:
        try:
            data, bom, crlf = read_file(f)
        except Exception as e:
            print(f"SKIP(parse error): {f}: {e}", file=sys.stderr)
            continue
        bullets = data.get('bullets')
        if not isinstance(bullets, list):
            continue
        tot_files += 1
        file_changed = False
        new_bullets = []
        first_diff = None
        for b in bullets:
            tot_bullets += 1
            # 集計(変換前状態)
            if 'gravity' in b and not isinstance(b['gravity'], dict):
                grav_c += 1
            if 'size' in b:
                size_c += 1
            if 'lockRotation' in b:
                if b['lockRotation'] is True:
                    lock_true_c += 1
                else:
                    lock_false_c += 1
            nb, changed = convert_bullet(b)
            if changed:
                ch_bullets += 1
                file_changed = True
                if first_diff is None:
                    first_diff = (b, nb)
            new_bullets.append(nb)
        if file_changed:
            ch_files += 1
            data['bullets'] = new_bullets
            if args.apply:
                write_file(f, data, bom, crlf)
            elif samples_shown < args.sample and first_diff is not None:
                samples_shown += 1
                print(f"\n--- sample diff: {f} (bullet[0]) ---")
                print("  before:", json.dumps(first_diff[0], ensure_ascii=False))
                print("  after :", json.dumps(first_diff[1], ensure_ascii=False))

    mode = "APPLIED" if args.apply else "DRY-RUN"
    print(f"\n[{mode}] files={tot_files} bullets={tot_bullets} | changed files={ch_files} bullets={ch_bullets}")
    print(f"  gravity(number)->polar: {grav_c}   size->scale: {size_c}   "
          f"lockRotation true->useVelocityAngle:false: {lock_true_c}   lockRotation false->removed: {lock_false_c}")
    if not args.apply:
        print("  (dry-run: no files written. re-run with --apply to write.)")


if __name__ == '__main__':
    main()
