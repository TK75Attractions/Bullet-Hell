# ステージ制作ガイド(リファクタ後の標準フロー)

対象: 新しくステージ(弾幕)を作る人。2026-07-02 のリファクタリング(P0〜P5)後の手順。

## 全体フロー

```
1. timing-editor で音源にマーカーを打つ
   → 「AI用指示を保存」(人/AI向け指示書) + 「チャート雛形を保存」(<stage>.chart.json)
2. chart.json にイベントを書く(拍・マーカー相対、Lunatic全量で)
3. Tools/Bullet Hell/Compile Stage Charts → <stage>.json 生成
4. Play Mode + Tools/Bullet Hell/Debug/Start Stage... で起動
   → シークバー/マーカーボタンで任意時刻へジャンプして確認
5. 難易度修飾(thin/minDifficulty/diffScale)で EASY/NORMAL を減算生成
6. Record ボタンで録画してレビュー
```

## 1. チャート(<stage>.chart.json)

置き場所: `Assets/StageData/<stageDir>/<stageDir>.chart.json`。**stage.json は生成物**(`_generatedFrom` 付き)なので直接編集しない。

```jsonc
{
  "meta": { "stageName": "石工", "bpm": 144, "measure": 4, "barCount": 91, "beatOffsetSec": 0 },
  "markers": { "M1": "5:1", "M20": "37:1" },        // 小節:拍(1始まり)
  "events": [
    { "at": "M1", "clip": "既存バッファ名" },          // 旧来のバッファ参照
    { "at": "M20 - 1beat", "clip": "..." },           // マーカー相対(beat/bar単位)
    { "at": "12.5", "clip": "..." },                  // 生の秒も可
    { "at": "M10", "pattern": "FallingBlock",         // パターン(バッファ不要)
      "params": { "positions": [{"x":10,"y":14}], "scale": 3,
                  "warnBeats": 2, "fallBeats": 1, "untilSec": 3, "dust": true } },
    { "at": "M31", "kind": "clear" }
  ],
  "enemies": [ ... ]                                   // enemySpawner 相当(appearAt に時刻式)
}
```

**リタイム**は markers の値を書き換えて再コンパイルするだけ。

チャート編集の注意:

- `at` は必須。裸の数値は秒、`"小節:拍"` は1始まり、単位は `beat`/`bar`/`sec`(乗除算は不可)
- スポナーの `color` 既定は `(1,1,1,1)`(白)。バッファ側 color と乗算される
- `diffScale` は pattern イベント専用(clip イベントでは無視される)
- enemies の `orbit` / `bulletClip` / `bulletChangeClips` は**コンパイラが検証せずそのまま通す**。この中の弾には `playerInfluence` / `warpCooldown` が存在しない(stage.json 側は別デシリアライザ)。必要なら BulletBuffer JSON 側で書く
- chart.json は Newtonsoft パース(コメント可)、生成物 stage.json と BulletBuffer JSON は JsonUtility(コメント不可・未知キー無視)

enemies のオプションフィールド:

- `fadeInSec` / `fadeOutSec`: 敵スプライトのαを出現直後 / 寿命(orbit.life)末尾で補間(0 なら無効)
- `sortingOrder`: SpriteRenderer.sortingOrder の明示指定(0 なら既定の 10)。敵同士の前後関係用
- `animation.events[].atAbs`: アニメ発火をマーカー式(例 `"M21 + 2beat"`)で絶対時刻指定
- 使用例: 石工の形態変化(老人を sortingOrder 12 + fadeOutSec 1.4 で手前から消し、ゴーレムを fadeInSec 1.0 で背後に出す)

## 2. パターン(バッファJSONを書かずに弾幕を出す)

| パターン | 用途 | 主パラメータ |
|---|---|---|
| `FallingBlock` | 予告→ブロック→落下→着地→ダスト(+バースト)の一式 | positions/scale/warnBeats/fallBeats/untilSec/dust/burst |
| `RadialBurst` | n方向破片+3段フラッシュ | positions/shardCount/speed/gravity/tumble/seed |
| `CutterSweep` | ゴースト予告付き直進カッター | cutters[]{pos,dirDeg,speed,ghostBeats} |
| `BeatPulseWarn` | ビート明滅予告のみ | positions/scale/warnBeats |
| `GhostPreview` | 事前明滅→直進弾 | positions/dirDeg/speed/ghostBeats |

- 落下は「着地が拍ジャスト」になるよう重力を自動逆算
- 予告の明滅・出現前の無敵ゴーストは appearTime/appearDuration 法で自動付与
- 詳細仕様: `Docs/stone-stage-design-v2.md` §5/§6(色パレット・接地式もここ)
- 動作サンプル: `Assets/StageData/pattern_demo/pattern_demo.chart.json`

## 3. 難易度(Lunatic-first 減算)

チャートは **LUNATIC の全量**で書く。下位はイベント単位の修飾で間引く:

```jsonc
{ "at": "23:1", "clip": "破片クリップ", "thin": { "easy": 2 } }          // EASYで半減
{ "at": "47:3", "clip": "縁カッター_2", "minDifficulty": "normal" }      // EASYでは出ない
{ "at": 6.0, "pattern": "RadialBurst", "params": {...},
  "diffScale": { "easy": { "speed": 0.7, "count": 0.5 } } }              // パターンのみ
```

- `thin: N` = n発ごとに1発除外(決定的)。ブロック/予告など構造弾は間引かれない(破片・カッター等のみ)
- デバッグウィンドウの難易度セレクタで実行中に切替可能

## 4. 検証ツール(Tools/Bullet Hell/)

- `Debug/Start Stage...`: 全ステージ対応ランチャ。シークバー+**チャートマーカー一覧からワンクリックジャンプ**+難易度セレクタ+スクショ+Start&Record
- `Debug/Record Current Stage`: 実行中ステージの録画(720p/30fps/音声)
- `Debug/Capture At Times...`: 指定秒リストで自動スクショ
- `Compile Stage Charts`: 全チャート再コンパイル(リンター込み)
- `Validate All Stages`: データ検証(clipName解決/スキーマ/タイプDB/テクスチャ設定/ファイル形式 UTF-8・改行/バッファ登録名の重複)
- `Sync Bullet Types`: BulletTypes フォルダの .asset を DB に自動登録

シークは「弾なし頭出し」(その時刻を跨いで生存中の弾は出ない)。BGM・ビート・敵出現は同期する。

## 5. テスト(壊していないかの確認)

Unity Test Runner(EditMode)で `BulletHell.Tests` を実行(2026-07-04 時点 51本)。
ゴールデンテストは全ステージの展開スケジュールを固定しており、意図した変更でゴールデンが変わる場合は `Tools/Bullet Hell/Golden/Dump All Stages` で再生成してコミットする。

## 6. 弾タイプの追加

1. `Assets/Scripts/Bullets/BulletTypes/<name>/` に png+mask+asset(既存フォルダを雛形に)
2. テクスチャは Read/Write 有効+Uncompressed(リンターが検証)
3. `Tools/Bullet Hell/Sync Bullet Types` で登録
4. 被弾させたくない演出弾は `verts: []`(完全無害)
