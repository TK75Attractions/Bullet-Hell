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

## BGM（背景音楽）

DOVA-SYNDROME（https://dova-s.jp/ ）のフリー BGM。
利用規約（2026-07-13 確認・ユーザー確認済み）: 連絡不要・商用/非商用/学校利用可・
クレジット表記は必須ではない（任意）。素材そのものの再配布は禁止だがゲーム組み込みは可。
※本ファイルへの記載は礼儀としての帰属メモ。

### Discotheque（タイトル画面 BGM）

- 用途: タイトル画面のループ BGM。`Assets/Resources/BGM/title_discotheque.mp3`
- 出典: DOVA-SYNDROME
- 作曲者: **不明**（mp3 メタデータにタグなし・DOVA 検索でも曲名から特定できず。
  判明したら追記する）
- BPM: 130.00（実測。Tools/measure_bpm.py のオンセット自己相関＋位相最適化櫛
  フィルタ）。タイトルロゴの振動・図形フラッシュ周期をこの BPM に同期。
- 加工内容: 原曲 3:20 のうち先頭ダウンビート 2.763s〜末尾フェード前 184.0s を
  切り出し（VBR ~190k・先頭/末尾に 5〜10ms のクリック防止フェード）。ループ用。
  音量は再生時に 0.55 倍で減衰（ステージ BGM 帯 -12LUFS へラウドネス整合）。

### Killing Party（リザルト画面 BGM）

- 用途: リザルト画面のループ BGM。`Assets/Resources/BGM/result_killing_party.mp3`
- 出典: DOVA-SYNDROME
- 作曲者: **MFP**（mp3 メタデータ TAG:artist=MFP / album=DOVA / 2017）
- BPM: 128.00（実測。1:50 以降の区間）。入場アニメ（溜め→開放）と音ハメ:
  clip 頭の build-up が「溜め」、1.44s のドロップがランクスタンプ着地に一致。
- 加工内容: ユーザー指示「1:50 以降を使い」に従い、原曲 110.113s（ドロップが
  スタンプ着地に一致する位置）〜本編終了 171.0s を切り出し（VBR ~190k・
  クリック防止フェード）。ループ用。音量は再生時に 0.78 倍で減衰（-12LUFS 整合）。

## 効果音

（2026-07-10 に追加したリザルト画面の効果音ラボ SE 6 種は、2026-07-13 に
ユーザー指示で削除済み。）

### 決定音（UI 確定 SE）

- 用途: タイトル/ステージ選択/難易度/リザルトの確定ボタン押下時の効果音。
  `Assets/Resources/SE/ui_decide.mp3`（元ファイル名「決定ボタンを押す16.mp3」）。
  `AudioManager.PlayDecisionSE()` が Resources から遅延ロードして常駐 SE プールで
  PlayOneShot する（BGM とは独立）。
- 出典: **効果音ラボ**（https://soundeffect-lab.info/ ）
- ライセンス（2026-07-15 確認）: 利用規約に従い、ゲーム・動画等での利用は無料・
  クレジット表記は任意。素材の再配布・素材そのものの販売は禁止。
