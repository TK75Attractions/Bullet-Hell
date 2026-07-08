# -*- coding: utf-8 -*-
"""浮浪者ステージの弾スプライト(bone/skull/ghost/tombstone)とボス GIF を生成する。
PIL は非アンチエイリアス(ハードエッジ)なので、GIF はマゼンタ背景を透過キーにできる。
弾は 128x128(BTDB の texArray サイズと一致)、自己彩色(color.w=0 で本来色表示)。
出力先はプロジェクト内。実行後 Unity で再インポート、新規型なら BTDB 登録が必要
(既存なら png 差し替えのみで反映)。ボス GIF は Assets/StageData/vagrant/Visuals/。
"""
import os, math
from PIL import Image, ImageDraw

BT = "Assets/Scripts/Bullets/BulletTypes"
VIS = "Assets/StageData/vagrant/Visuals"
S = 128
cream = (232, 226, 205, 255); outl = (120, 112, 92, 255); dark = (38, 34, 44, 255)


def save_with_mask(im, name):
    d = f"{BT}/{name}"
    os.makedirs(d, exist_ok=True)
    im.save(f"{d}/{name}.png")
    a = im.split()[3]
    mask = Image.new("RGBA", (S, S), (0, 0, 0, 0))
    mask.paste((255, 255, 255, 255), (0, 0), a)
    mask.save(f"{d}/{name}_mask.png")


def bone():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    cy = S // 2; r = 22
    for cx in (30, 98):
        for oy in (-16, 16):
            d.ellipse([cx - r, cy + oy - r, cx + r, cy + oy + r], fill=cream, outline=outl, width=3)
    d.rectangle([30, cy - 9, 98, cy + 9], fill=cream)
    d.line([30, cy - 9, 98, cy - 9], fill=outl, width=3)
    d.line([30, cy + 9, 98, cy + 9], fill=outl, width=3)
    save_with_mask(im, "bone")


def skull():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.ellipse([28, 20, 100, 88], fill=cream, outline=outl, width=3)
    d.rectangle([44, 78, 84, 104], fill=cream, outline=outl, width=3)
    d.ellipse([40, 44, 60, 66], fill=dark); d.ellipse([68, 44, 88, 66], fill=dark)
    d.polygon([(64, 62), (58, 76), (70, 76)], fill=dark)
    for tx in (52, 64, 76):
        d.line([tx, 80, tx, 100], fill=outl, width=2)
    save_with_mask(im, "skull")


def ghost():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    pale = (206, 232, 240, 235)
    d.pieslice([24, 18, 104, 98], 180, 360, fill=pale)
    d.rectangle([24, 58, 104, 96], fill=pale)
    w = (104 - 24) / 4
    for i in range(4):
        x0 = 24 + i * w
        d.pieslice([x0, 86, x0 + w, 86 + w], 0, 180, fill=pale)
    d.ellipse([44, 46, 60, 68], fill=dark); d.ellipse([70, 46, 86, 68], fill=dark)
    d.ellipse([58, 72, 72, 90], fill=dark)
    save_with_mask(im, "ghost")


def tombstone():
    im = Image.new("RGBA", (S, S), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    gray = (150, 150, 160, 255); gout = (92, 92, 104, 255)
    d.rounded_rectangle([34, 24, 94, 112], radius=26, fill=gray, outline=gout, width=4)
    d.line([64, 40, 64, 64], fill=gout, width=5); d.line([52, 50, 76, 50], fill=gout, width=5)
    save_with_mask(im, "tombstone")


# 屍霊術師の固定パレット(idx0=マゼンタ=透過)。全フレーム共通で透過 idx を安定させる。
KEY = (255, 0, 255)
ROBE = (46, 36, 64); ROBE_D = (30, 22, 44); BONE_C = (226, 222, 206)
GLOW = (120, 240, 150); GLOW2 = (180, 255, 205); EYEW = (220, 255, 230)
DARKEYE = (20, 20, 28); STAFF = (90, 70, 50)
NECRO_PAL = [KEY, ROBE, ROBE_D, BONE_C, GLOW, GLOW2, EYEW, DARKEYE, STAFF]


def _pal_image():
    flat = []
    for c in NECRO_PAL:
        flat += list(c)
    flat += [0, 0, 0] * (256 - len(NECRO_PAL))
    pi = Image.new("P", (1, 1)); pi.putpalette(flat)
    return pi


def _draw_necromancer(orb_r, orb_col, eye_col):
    """1フレーム分を RGB(マゼンタ背景)で返す。orb_r=杖玉半径, 目/杖玉の色で詠唱脈動。"""
    N = 256
    im = Image.new("RGBA", (N, N), (0, 0, 0, 0)); d = ImageDraw.Draw(im)
    d.polygon([(128, 40), (70, 120), (52, 236), (204, 236), (186, 120)], fill=ROBE + (255,))
    d.ellipse([80, 24, 176, 120], fill=ROBE + (255,))
    d.ellipse([98, 44, 158, 116], fill=ROBE_D + (255,))
    d.ellipse([104, 54, 152, 104], fill=BONE_C + (255,)); d.rectangle([116, 98, 140, 116], fill=BONE_C + (255,))
    for ex in (112, 134):
        d.ellipse([ex, 68, ex + 14, 84], fill=eye_col + (255,)); d.ellipse([ex + 3, 71, ex + 11, 81], fill=EYEW + (255,))
    d.polygon([(128, 84), (123, 94), (133, 94)], fill=DARKEYE + (255,))
    d.ellipse([44, 150, 70, 176], fill=BONE_C + (255,)); d.ellipse([186, 150, 212, 176], fill=BONE_C + (255,))
    d.line([200, 60, 200, 180], fill=STAFF + (255,), width=7)
    oc = (200, 56)  # 杖玉の中心
    d.ellipse([oc[0] - orb_r, oc[1] - orb_r, oc[0] + orb_r, oc[1] + orb_r], fill=orb_col + (255,))
    d.line([(70, 120), (52, 236)], fill=ROBE_D + (255,), width=4); d.line([(186, 120), (204, 236)], fill=ROBE_D + (255,), width=4)
    bg = Image.new("RGB", (N, N), KEY)
    bg.paste(im.convert("RGB"), (0, 0), im.split()[3])
    return bg


def necromancer_gif():
    """フード付き屍霊術師。杖玉が脈動する2フレーム idle。固定パレットで透過 idx=0 安定。"""
    palimg = _pal_image()
    frames = []
    for orb_r, orb_col, eye_col in [(12, GLOW, GLOW), (16, GLOW2, GLOW2)]:
        rgb = _draw_necromancer(orb_r, orb_col, eye_col)
        frames.append(rgb.quantize(palette=palimg, dither=Image.NONE))
    os.makedirs(VIS, exist_ok=True)
    frames[0].save(f"{VIS}/vagrant_idle.gif", save_all=True, append_images=frames[1:],
                   transparency=0, duration=560, loop=0, disposal=2)


if __name__ == "__main__":
    bone(); skull(); ghost(); tombstone(); necromancer_gif()
    print("生成: bone/skull/ghost/tombstone(+mask) と vagrant_idle.gif")
