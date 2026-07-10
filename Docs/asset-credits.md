# 外部アセットのクレジット・ライセンス記録

ゲーム内クレジット表記の作成時はこのファイルを参照する。
新しい外部素材を追加したら必ずここに追記する。

## アイコン

### Google Material Symbols（リザルト画面のパラメータアイコン 4 種）

- 用途: リザルト画面のパラメータカード（スコア/被弾回数/カウンター回数/時間）の
  チップ内アイコン。SVG を白シルエット PNG 化して
  `Assets/Resources/UI/result_icon_{score,hit,counter,time}.png` に配置。
- 出典リポジトリ: https://github.com/google/material-design-icons
- 使用アイコン（いずれも Material Symbols Outlined / wght500 / 48px 版 SVG）:
  - `my_location` → result_icon_score.png
    https://raw.githubusercontent.com/google/material-design-icons/master/symbols/web/my_location/materialsymbolsoutlined/my_location_wght500_48px.svg
  - `shield` → result_icon_hit.png
    https://raw.githubusercontent.com/google/material-design-icons/master/symbols/web/shield/materialsymbolsoutlined/shield_wght500_48px.svg
  - `swords` → result_icon_counter.png
    https://raw.githubusercontent.com/google/material-design-icons/master/symbols/web/swords/materialsymbolsoutlined/swords_wght500_48px.svg
  - `schedule` → result_icon_time.png
    https://raw.githubusercontent.com/google/material-design-icons/master/symbols/web/schedule/materialsymbolsoutlined/schedule_wght500_48px.svg
- ライセンス: **Apache License 2.0**（2026-07-10 取得時点）
  https://github.com/google/material-design-icons/blob/master/LICENSE
- 帰属表記: 不要（任意）。商用利用・改変可。
- 加工内容: Chromium で 512px ラスタライズ → 輝度をアルファへ変換した
  白シルエット RGBA → LANCZOS で 96px へ縮小。色はゲーム側で頂点色ティント。

## フォント

### Playfair Display Medium（リザルト画面のランク文字）

- 用途: リザルト画面のランク文字（S/A/B/C/F）のセリフ体。
  `Assets/Resources/Fonts/PlayfairDisplay-Medium SDF`
- 出典: Google Fonts https://fonts.google.com/specimen/Playfair+Display
- ライセンス: SIL Open Font License 1.1

## 効果音

### 効果音ラボ（リザルト画面の SE 6 種）

- 用途: `Assets/Resources/SE/result_*.wav`（スタンプ着地 2 種・カウント駆動/確定・
  カードスライド・決定）
- 出典: https://soundeffect-lab.info/sound/battle/ / https://soundeffect-lab.info/sound/button/
  - 打撃4「岩を砕く」(blow4) / 怪獣の足音「ズシン」(monster-footstep1) /
    剣の素振り2 (sword-gesture2) / データ表示2 (data-display2) / 決定29 / 決定1
- 利用規約（2026-07-10 確認）: https://soundeffect-lab.info/agreement/
  クレジット表記不要（任意）・商用無料・加工可（素材そのものの再配布は禁止・
  ゲーム組み込みは可）。
- 加工内容: 先頭無音トリム + wav 化。
