# OPUS-HANDOFF — Opus 自律セッション向け引き継ぎ書

作成: 2026-07-03(Fable セッション)。以後このプロジェクトの自律作業は Opus(claude-opus-4-8)が担う前提で、迷わず実行できる粒度で残タスクを整理した。

**読む順**: このファイル → `CLAUDE.md`(プロジェクトルール) → `PROGRESS.md` 先頭2セクション → 必要に応じて `Docs/claude-code-handoff.md` / `Docs/stone-stage-handoff.md`。BulletBuffer JSON を編集する前に必ず `Docs/BulletBufferContext.md`。

---

## 1. 現状スナップショット(2026-07-03 時点・検証済み)

- ブランチ: `marron/claude-codex`(origin より **30 コミット先行**。push はユーザー確認が必要 → §4)
- 最新コミット: `40e3f81` 石工: 破片寿命20%短縮+下部破裂予告の点線を明るく・大きく
- **EditMode テスト 49/49 緑**(2026-07-03 にこの引き継ぎ書作成時点で再実行して確認)
- ワークツリーの dirty(意図的・**revert 禁止**):
  - `CLAUDE.md`(+12行: 「短期的な指示」セクション。ユーザー管理)
  - `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`(過去セッション由来)
- 石工ステージは通しで動作良好。直近の Gemini 動画レビューで「音と映像のズレ指摘ゼロ」、序盤の音ハメとクライマックス(1:03 ドロップ)は高評価
- 既知の無害な警告: obsolete 警告3件、mp4 color primaries 警告、Play 中ドメインリロード時の NativeArray リーク(→ タスク H)

## 2. 絶対に守るルール(CLAUDE.md の要点+このプロジェクト固有)

1. **push・force push・公開操作は禁止**(ユーザー確認が取れるまで)。ローカル commit は節目ごとに意味単位で行ってよい(許可済み)
2. dirty ファイルを revert しない。`git add .` を使わず対象ファイルだけ狭く add
3. `Assets/Screenshots/`、`Assets/_Recovery/`、`Recordings/`、`.oracle-output/` はコミットしない
4. UnityMCP: 変更系呼び出しと Play Mode 突入を並列にしない。スクリプト編集後はコンパイル完了+エラー無しを確認してから次へ
5. §5 の「凍結リスト」の値・方針を変更しない
6. BulletBuffer JSON: 極座標角は**ラジアン**、ステージ spawner の角度は**度**。プレイエリアは 32x18、左下 (0,0)
7. 石工の全弾は unCounterable(リファクタ後の標準)。`stage.json` は生成物なので直接編集しない(`stone.chart.json` とパターンが正)

## 3. 標準検証手順(全タスク共通のテンプレ)

### 3.1 コンパイル確認
スクリプトを触ったら `read_console` でエラー確認。`editor_state` の `isCompiling` が false になるのを待つ。

### 3.2 EditMode テスト
UnityMCP `run_tests`(mode=EditMode)→ `get_test_job` で 49/49 を確認。
- chart のイベント数を増減させた場合は `ChartCompileParityTest` の期待値更新が必要(前例: 104→105)
- テスト数 49 自体も期待値変更で変わりうる。緑であることが基準

### 3.3 ゴールデン再生成(BulletBuffer / chart を変えたら必須)
1. Unity メニュー `Tools/Bullet Hell/Golden/Dump All Stages`
2. `git diff Tests/Golden/stone.golden.json` で**意図した差分だけ**か確認(変更したバッファの sha1 と count のみ変わるのが正常。無関係なクリップの差分が出たら何かがおかしい)
3. 意図どおりなら golden も同じコミットに含める

### 3.4 Play Mode スクショ検証
1. **先に Play Mode に入る** → `Tools/Bullet Hell/Debug/Start Stone Stage`
2. `Tools/Bullet Hell/Debug/Capture At Times...` でステージ秒を指定して自動撮影(演出変更の前後比較に使う)
3. 状態確認は `Tools/Bullet Hell/Debug/Dump Stone Debug State`
4. Console にエラーが出ていないか確認

### 3.5 録画 → 動画レビュー(音ハメ系の変更のみ)
- `StageRecorderMenu`(Assets/Editor)で録画。**CapFrameRate=true が必須**(false だと音ズレ)。弾数0が5秒続くと自動停止する
- Gemini/Oracle に動画を渡すときはプロンプトに「視聴できた証拠として冒頭3秒を describe せよ」を必ず入れる(添付無視事故の再発防止)
- Oracle は `mcp__oracle__consult`。既定は GPT-5.5 High(browser エンジン)。見た目の良し悪しは自己評価で済ませず Oracle の画像レビューを挟む

### 3.6 戻し方(共通)
- 未コミットなら: `git checkout -- <変更したファイルだけ>`(golden も忘れず戻す)
- コミット済みなら: `git revert <sha>`。**reset --hard は使わない**(dirty を巻き込むため)

---

## 4. 残タスク一覧

難易度: 低/中/高。★ = **Opus 向き**(機械的・検証容易・JSON のみ等)。
出典: PROGRESS.md 未解決欄+`Recordings/stone_v6_gemini_advice.md`(Gemini 動画レビュー詳細)。

### A. ★ 起動リングの因果の明確化(Gemini 優先度1の前半)【難易度: 中 / リスク: 低】
- **内容**: 63.33s(M21)の赤い起動リングが「カッター射出を起動した」と読めるよう、リングの波紋をカッター出現位置(画面左右端)まで届かせる、またはリングがベルト帯に触れた瞬間の小フラッシュを追加
- **対象**: `Assets/BulletBuffers/stone/golem_core_ring.json`(既存4枚: scale 1.2→3.4、胸コア (16,13.75)、stone_burst 型・被弾なし)を拡張、または同型の新バッファ+`Assets/StageData/stone/stone.chart.json` に 39:1 付近のイベント追加
- **手順**: BulletBufferContext.md を読む → 既存 golem_core_ring.json を雛形にコピー → 位置/スケール/寿命を調整 → chart にイベント追加(clipName と JSON の name を一致させる)→ イベント数が変わるので ChartCompileParityTest の期待値更新
- **検証**: 3.2 → 3.3 → 3.4(63.3〜63.6s 前後を Capture At Times、過去の証跡は 63.38/63.46/63.50s)→ Oracle 画像レビュー
- **戻し方**: 追加した JSON と chart の差分、golden、テスト期待値を revert

### B. ★ ドット拡大(scale 0.45)後の Oracle 再レビュー【難易度: 低 / リスク: なし】
- **内容**: 前回、下部破裂予告のドットを 0.4→0.45 に拡大したが、拡大後の Oracle 再レビューが未実施のまま確定している。検証だけのタスク
- **手順**: 3.4 で 66.5 / 73.0s のスクショを撮る → Oracle 画像レビュー「点線予告の視認性はこれで十分か。明るさの上限は RGB 0.58/0.70/1.00 とされている」
- **注意**: Oracle が変更を提案しても、色は「現状値が適正」と一度確定済み。scale の微調整のみ許容。alpha は触らない(§5)

### C. ★ Play 中ドメインリロード時の NativeArray リーク修正【難易度: 中 / リスク: 低】
- **内容**: リロード時に QuadOrder の NativeArray が Dispose されず「Leak Detected: Persistent allocates 21 allocations」が出る(実害小・既存事象)
- **対象**: `Assets/Scripts/Bullets/BulletRenderSystem.cs` 周辺(QuadOrder の NativeArray 確保箇所)
- **手順**: 確保箇所を特定 → `AppDomain.CurrentDomain.DomainUnload` か `AssemblyReloadEvents.beforeAssemblyReload` で Dispose を追加(ゲーム挙動を変えないこと)
- **検証**: 3.1 → 3.2 → Play Mode で石工起動 → `EditorUtility.RequestScriptReload()` を Play 中に実行(前セッションの再現手順)→ リーク警告が消えること+NRE が出ないこと(GManager の ready=[NonSerialized] ガードは 596ef09 で対応済み)
- **戻し方**: 単一ファイルの revert

### D. 点線予告のビート同期出現(Gemini 優先度5)【難易度: 中 / リスク: 中】
- **内容**: 0:28 以降の点線枠がフワッと出るのを、ハイハット等の細ビートに合わせてパラパラ出現させる
- **対象**: `Assets/BulletBuffers/stone/` の warn 系 JSON(`beat_block_warn_1/2`、`big_block_warn_1〜3`、`block_warn_e/f`、`lower_burst_warn_1/2` 等)の各弾 `appearTime` をビート格子(BPM から算出。beat 157=65.417s が既知の基準点)に載せ替える
- **注意**: ファイル数が多く、音ハメの検証は静的スクショでは不可能 → 3.5 の録画+動画レビューまで完走できる場合のみ着手。片手間でやらない
- **検証**: 3.2 → 3.3 → 3.5(録画して Gemini 動画レビュー、冒頭 describe 必須)
- **戻し方**: warn 系 JSON+golden の revert

### E. 破壊エフェクトのビート同期(Gemini 優先度4)【難易度: 中〜高 / リスク: 中】
- **内容**: 65s 前後でカッターがブロックを砕くタイミングを BGM のキック/スネアに一致させる(カッター速度とブロック間隔の微調整)
- **対象**: `edge_cutter_1/2.json`、残置ブロック・`shatter_shard.json` の発生タイミング、必要なら chart
- **注意**: 65.417s(beat 157)の地面消滅同期を壊さないこと。破片寿命 0.96s は Oracle 確定値なので変えない
- **検証**: D と同じく録画+動画レビュー必須
- **戻し方**: 対象 JSON+golden の revert

### F. プレイヤーの視認性向上(Oracle 優先度1)【難易度: 中〜高 / リスク: 中】
- **内容**: 65.5s 前後で自機が破片・カッターに埋もれる。自機の描画順を前面化+1px 暗色アウトライン
- **対象**: 自機の描画(SpriteRenderer の sortingOrder ないしプレイヤー用マテリアル)。描画コード変更が必要
- **注意**: 全ステージに効く変更になりやすい。石工以外(過去ステージ)のスクショも1枚撮って副作用がないか見る
- **検証**: 3.1 → 3.2 → 3.4(64.3〜65.7s)→ Oracle 画像レビュー
- **戻し方**: 変更ファイルの revert

### G. 床消滅の演出強化: 亀裂ライン+画面揺れ(Oracle 案)【難易度: 高 / リスク: 中】
- **内容**: 65.417s の地面消滅時に亀裂ライン+0.05〜0.08s の画面揺れ。画面揺れは新機能実装(カメラ制御)が必要
- **判断**: 亀裂ラインだけなら JSON(点線バッファの流用)で可能 → そこだけ切り出すのは Opus 向き。画面揺れはコード新設なので、時間が限られるならやらない
- **検証**: 3.2 → 3.3 → 3.4(65.3〜65.7s)→ Oracle 画像レビュー

### H. カッターのコマ送り移動/ビート明滅(Gemini 優先度2)【難易度: 高 / 着手前にユーザー相談推奨】
- **内容**: カッター移動を等速からビート同期のコマ送りへ、または回転/火花のビート明滅
- **理由**: コンテンツ再設計を伴い、避け心地(難易度)が変わる。PROGRESS でも一貫して「ユーザー判断待ち」扱い
- **代替**: 移動は等速のまま「ビートごとの火花明滅」だけなら JSON 追加で可能性あり。やるならそちらを先に提案として実装し、ユーザーレビューに委ねる

### I. 地面をカッター通過順に崩壊(Gemini 優先度1の後半)【難易度: 高 / 着手前にユーザー相談推奨】
- **内容**: ベルト帯を一括消滅ではなくカッター通過位置から順に崩す
- **理由**: `stone_belt_bottom_2.json` を複数分割する再設計。65.417s 同期(直近2ラウンドで作り込んだ因果)を壊すリスクが高い。現状でも Oracle は「粉砕→床消滅→次攻撃の因果が読める」と評価済みで、緊急度は低い

**推奨着手順(Opus 単独で完結できる順)**: B → A → C → (余力があれば) G の亀裂のみ → D。E/F は録画レビューまで完走できるときのみ。H/I は実装せず提案止まり。

---

## 5. 凍結リスト(ユーザー判断待ち・変更禁止)

| 項目 | 場所 | 理由 |
|---|---|---|
| `appearBeatBaseAlpha = 0.2f` | `Assets/Scripts/Bullets/BulletRenderSystem.cs:14` | 全ステージの予告フェード共通定数。Oracle は 0.3 を提案したが全体の見た目に効くためユーザー判断 |
| 破片(shatter_shard)のアルファ | `shatter_shard.json` | 「半透明で最初表示するのをやめて」というユーザー指示と干渉。寿命 0.96s も Oracle 確定値 |
| カッターの最終 Y / スケール | `edge_cutter_1/2.json` | 当たり判定=難易度に直結。Oracle 案(Y を 0.3〜0.5 下げ or scale 0.9)は保留中 |
| 形態変化の演出方向 | ゴーレム/老人のクロスフェード一式 | Gemini は「粒子吸い込み/グリッチ」を提案したが、「老人がゴーレムに重なって消える」は**ユーザー指示そのもの**。変更は要相談 |
| 下部破裂予告の点線の色 | `lower_burst_warn_1/2.json` | RGB 0.52/0.63/0.98 で Oracle と確定済み(上限 0.58/0.70/1.00) |
| Script Changes While Playing | Unity Preferences | ユーザー環境設定。変更しない(推奨値の伝達のみ) |
| CLAUDE.md「短期的な指示」 | `CLAUDE.md`(dirty) | 全項目対応済みのはずだが、セクション整理はユーザー確認後 |
| push / 公開 / force push | — | ユーザー確認必須 |

## 6. その他の申し送り

- `Recordings/` の中間 mp4(`stone_20260703_*.mp4`)と `_frames/` は削除可(gitignore 済み)。ただし `stone_v6_capfix_20260703.mp4` と `stone_v6_gemini_advice.md` は参照元なので残す
- Oracle の Chrome ウィンドウが最小化/画面外に行くことがある。復旧手順は memory(`project_oracle_chrome_window_behavior.md`)にあり
- セッション終了時は必ず `PROGRESS.md` の先頭に (1)やったこと (2)検証結果 (3)未解決と次の一手 を追記し、このファイルの §1/§4 も実態に合わせて更新すること
