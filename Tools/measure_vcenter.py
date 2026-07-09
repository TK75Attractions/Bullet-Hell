# タイトルメニュー等のテキスト上下中央を、スクリーンショットの実ピクセルで測る。
# 各バンド(バナーの上端/下端の image-y)内で、背景より明るいピクセルを
# テキストのインクとみなし、上余白/下余白を数える。
# 使い方: python Tools/measure_vcenter.py <png> <name:x0:x1:ytop:ybot:thr> ...
#   thr = バンド内中央値に足す輝度閾値(既定 45)
import sys
from PIL import Image
import statistics


def measure(img, name, x0, x1, ytop, ybot, thr):
    px = img.load()
    lum_rows = []  # (y, count of ink pixels)
    all_lums = []
    for y in range(ytop, ybot + 1):
        for x in range(x0, x1 + 1):
            r, g, b = px[x, y][:3]
            all_lums.append(0.299 * r + 0.587 * g + 0.114 * b)
    med = statistics.median(all_lums)
    cutoff = med + thr
    ink_ys = []
    for y in range(ytop, ybot + 1):
        cnt = 0
        for x in range(x0, x1 + 1):
            r, g, b = px[x, y][:3]
            if 0.299 * r + 0.587 * g + 0.114 * b > cutoff:
                cnt += 1
        # ノイズ除去: 3px 以上インクがある行だけテキスト行とみなす
        if cnt >= 3:
            ink_ys.append(y)
    if not ink_ys:
        print(f"{name}: no ink found (median={med:.0f} cutoff={cutoff:.0f})")
        return
    top_margin = ink_ys[0] - ytop
    bot_margin = ybot - ink_ys[-1]
    print(
        f"{name}: band=[{ytop},{ybot}] ink=[{ink_ys[0]},{ink_ys[-1]}] "
        f"topMargin={top_margin}px botMargin={bot_margin}px diff(top-bot)={top_margin - bot_margin}px "
        f"(median={med:.0f} cutoff={cutoff:.0f})"
    )


def main():
    img = Image.open(sys.argv[1]).convert("RGB")
    for spec in sys.argv[2:]:
        parts = spec.split(":")
        name, x0, x1, ytop, ybot = parts[0], int(parts[1]), int(parts[2]), int(parts[3]), int(parts[4])
        thr = int(parts[5]) if len(parts) > 5 else 45
        measure(img, name, x0, x1, ytop, ybot, thr)


if __name__ == "__main__":
    main()
