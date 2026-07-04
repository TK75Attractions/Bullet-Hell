# OPUS-HANDOFF — Opus 自律セッション向け引き継ぎ書

作成: 2026-07-03(Fable セッション)。以後このプロジェクトの自律作業は Opus(claude-opus-4-8)が担う前提で、迷わず実行できる粒度で残タスクを整理した。

**読む順**: このファイル → `CLAUDE.md`(プロジェクトルール) → `PROGRESS.md` 先頭2セクション → 必要に応じて `Docs/claude-code-handoff.md` / `Docs/stone-stage-handoff.md`。BulletBuffer JSON を編集する前に必ず `Docs/BulletBufferContext.md`。

---

## 1. 現状スナップショット(2026-07-04 夕方 更新・検証済み)

- ブランチ: `marron/claude-codex`(origin より **82 コミット先行**。push はユーザー確認が必要 → §4)
- 2026-07-04 夕方時点の直近3コミット: `75be3d5` M21着地カメラシェイク(§4-G 画面揺れ完了)、`f483add` lower_burst 破片ネイビー統一、`5cf2411` StageTimeOverlay の Input System 修正(毎フレーム例外の解消)。EditMode 49/49 緑・Unity ブリッジ正常
- 以下は 2026-07-03 時点の記録(履歴として保持):
- 最新コミット: **R9(ユーザー実確認フィードバック4件)完了** — `f4bc132` 形態変化ブロックの重なり解消/ジッター(Task3)、`d9ce6ea` 色統一(Task4)、`3942a98` 点線予告の簡素化(Task1)、`8275960` tile/mass_drop 減速(Task2)。その前は R8 `814765d` 自機視認性(§4-F)
- **タスク進捗**: **Task C(NativeArray リーク)は `28eb47c` で実装・コミット済み**(コード精読で健全性確認済み)。**Task B(ドット拡大の Oracle 再レビュー)は完了**: scale 0.45 は Oracle「合格」、任意で 0.48 推奨(PROGRESS 2026-07-03 夜の節参照)
- **EditMode テスト 49/49 緑**(引き継ぎ書作成時点で確認。以後 Unity ブリッジ不通のため未再実行)
- ⚠ **2026-07-03 夜時点で Unity ブリッジ不通**(instance_count=0、プロセスはハング疑い)。JSON/コード変更を伴うタスクは Unity 復帰後に
- ワークツリーの dirty(意図的・**revert 禁止**):
  - `CLAUDE.md`(+12行: 「短期的な指示」セクション。ユーザー管理)
  - `Assets/TextMesh Pro/Resources/Fonts & Materials/LiberationSans SDF - Fallback.asset`(過去セッション由来)
- 石工ステージは通しで動作良好。直近の Gemini 動画レビューで「音と映像のズレ指摘ゼロ」、序盤の音ハメとクライマックス(1:03 ドロップ)は高評価
- 既知の無害な警告: obsolete 警告3件、mp4 color primaries 警告、Play 中ドメインリロード時の NativeArray リーク(→ タスク H)

## 2. 絶対に守るルール(CLAUDE.md の要点+このプロジェクト固有)

1. **push・force push・公開操作は禁止**(ユーザー確認が取れるまで)。ローカル commit は節目ごとに意味単位で行ってよい(許可済み)
2. dirty ファイルを revert しない。`git add .` を使わず対象ファイルだけ狭く add
3. `Assets/Screenshots/`、`Assets/_Recovery/`、`Recordings/`、`.oracle-output/` はコミットしない。**`Instructions/` も基本コミットしない**(2026-07-04 確定・.gitignore 済み。ユーザーが置く指示ファイル(mp4 等)は作業入力であってリポジトリ成果物ではない。既にトラック済みの5ファイルはそのまま管理し削除しない)
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

### A. ✅ 起動リングの因果の明確化【完了・`cb7c611`(2026-07-03 深夜3)】
- **結果**: `golem_core_ring.json` を4層→8層に拡張(scale 0.7-14.0・寿命延長・赤→暗赤フェード)。Oracle 画像レビューで因果=改善・十字形=許容・サイズ=ちょうどよい寄りと確認、指摘の外側 alpha 微減も反映。golden 再生成・EditMode 49/49 緑・通し再生スクショ済み。詳細は PROGRESS 2026-07-03 深夜3
- **(参考)当初案**: 63.33s(M21)の赤い起動リングが「カッター射出を起動した」と読めるよう、リングの波紋をカッター出現位置(画面左右端)まで届かせる、またはリングがベルト帯に触れた瞬間の小フラッシュを追加
- **対象**: `Assets/BulletBuffers/stone/golem_core_ring.json`(既存4枚: scale 1.2→3.4、胸コア (16,13.75)、stone_burst 型・被弾なし)を拡張、または同型の新バッファ+`Assets/StageData/stone/stone.chart.json` に 39:1 付近のイベント追加
- **手順**: BulletBufferContext.md を読む → 既存 golem_core_ring.json を雛形にコピー → 位置/スケール/寿命を調整 → chart にイベント追加(clipName と JSON の name を一致させる)→ イベント数が変わるので ChartCompileParityTest の期待値更新
- **検証**: 3.2 → 3.3 → 3.4(63.3〜63.6s 前後を Capture At Times、過去の証跡は 63.38/63.46/63.50s)→ Oracle 画像レビュー
- **戻し方**: 追加した JSON と chart の差分、golden、テスト期待値を revert

### B. ✅ ドット拡大(scale 0.45)後の Oracle 再レビュー【完了・2026-07-03 夜】
- **結果**: 既存スクショ(66.5/73.0s、scale 0.45 反映)を Oracle 画像レビュー。**scale 0.45 は「合格」**(危険予告として読める・枠の形も良好)。任意推奨は **scale 0.48**(周辺視野で安全寄り。0.50 以上は不要)。色/alpha は現状維持で確定
- **残: 任意フォローアップ(0.45→0.48)** — 必須ではない。実施するなら PROGRESS 2026-07-03 夜の「実行レシピ」参照(warn_1/2.json の 0.45→0.48 一括置換 → golden 再生成 → テスト → Capture 目視)

### C. ✅ Play 中ドメインリロード時の NativeArray リーク修正【完了・`28eb47c`】
- **実装**: `Assets/Scripts/Managers/QuadOrder.cs` に `DisposeNativeContainers()` を抽出し、`AwakeSetting` で `beforeAssemblyReload` に登録(二重登録防止フラグ)、`OnDestroy` で解除+破棄、`IsCreated` ガードで二重破棄も安全。すべて `#if UNITY_EDITOR`。リーク 21→2 に減少(残2は LASER.vertsSet・別スコープ・実害小)
- **残検証**: Play 中 `EditorUtility.RequestScriptReload()` での実挙動再確認は Unity 復帰後に(コード精読では健全)

### D. ✅ 点線予告のビート同期出現(Gemini 優先度5)【完了・`1b3c69d`(2026-07-03 深夜5)】
- **結果**: ブロック枠予告7バッファ(`beat_block_warn_1/2`・`big_block_warn_1/2/3`・`block_warn_e/f`)を、グループ内の点が8分音符グリッド(0.208333s @144BPM)で四隅→散布の3波に順次ポップインする「パラパラ出現」に変更。**変更は `appearDuration` のみ**で appearTime/life(=消滅拍=音同期アンカー)は不変=ゲーム音ハメ本体(別の spawn バッファ)に非接触。四隅を第1波固定で落下範囲を先に読ませる(Oracle 指摘反映)。golden 再生成(7クリップ sha1 のみ)・EditMode 49/49 緑・Play撮影・**Oracle 画像レビュー=クリーンパス**・**Oracle 動画レビュー(104KB録画)=クリーンパス**。詳細は PROGRESS 2026-07-03 深夜5
- **未検証(次の一手)**: 実曲ハイハットとの同期は**音声付き録画での耳/動画レビューが要**(今回の録画は音声なしで拍グリッド感のみ判定)。engine の脈動は4分のみ(beatTimings=[0,1,2,3])。違和感が出るなら SUB を16分へ細分 or 4分に寄せる
- **対象外**: tile/rain 系(100発グリッド)と `lower_burst_warn`(§5凍結=色)は今回未着手。同手法(appearDuration のみ変更)で追加拡張は可能
- **戻し方**: `git revert 1b3c69d`(7 JSON+golden がまとまっている)

### E. ✅ 破壊エフェクトのビート同期(Gemini 優先度4)【完了・`05729e9`(2026-07-04 早朝)】
- **結果**: `shatter_shard.json`(石工粉砕破片・chart 63.75s=beat 153 発生)の破片バーストを、外→内クランブルの各波がキック/スネアの4分拍に載るよう整列。相対 appearTime 0.520833/1.041667/1.5625(beat 154.25/155.5/156.75・拍ズレ)→ **0.416667/0.833333/1.25**(beat 154/155/156・拍ちょうど)。**変更は appearTime/life のみ**(54発中42発。life=新 appearTime+0.96 で破片寿命0.96s=Oracle 確定値を保持)。第1波(appearTime 0/life 0.96・最外周12発)と紺色は不変、**床消滅 65.417s(beat 157)も不変**。中央波は 65.0s=beat 156 で床崩落の1拍前に収束。golden は当該 sha1 のみ・count 54 不変・chart 未変更(テスト期待値変更なし)・EditMode 49/49 緑・通し再生撮影・**Oracle 動画レビュー(283KB録画)=リズム/因果/密度すべてクリーンパス**。詳細は PROGRESS 2026-07-04 早朝
- **未検証(次の一手)**: 実曲キック/スネアとの同期は音声付き録画での耳チェックが要(Oracle は音声なしで視覚リズムのみ判定)。ユーザー耳チェック用 `stone_taskE_break_audio.mp4` を用意済み
- **Oracle 任意の微調整(低優先)**: 中央波(65.0s の (16,2.3)×6)がノコギリに重なり密。spawn 位置を上/横に広げると最終拍がクリーンに(タイミング維持)。frozen 非該当で変更可
- **対象外**: `edge_cutter_1/2.json`(75s台=65s破壊と無関係、かつ最終Y/scale が §5凍結)は未着手
- **戻し方**: `git revert 05729e9`(shatter_shard.json+golden がまとまっている)

### F. プレイヤーの視認性向上(Oracle 優先度1)【難易度: 中〜高 / リスク: 中】✅完了(`814765d`)
- **内容**: 65.5s 前後で自機が破片・カッターに埋もれる。自機の描画順を前面化+1px 暗色アウトライン
- **実装(ラウンド8)**: URP 2D は弾(SRPDefaultUnlit の DrawMeshInstancedIndirect)を 2D スプライトパスの後に描くため、通常 SpriteRenderer の自機は常に弾の背後になっていた(=真因)。`Assets/Shaders/PlayerFrontOverlay.shader`(URP unlit・Queue=Transparent+100)+ `Assets/Scripts/Managers/PlayerFrontOverlay.cs`(自機スプライトを弾の直後に `Graphics.DrawMesh` で再描画、色を MPB 伝播)を追加。GManager が自機生成直後に付与。既存 SpriteRenderer 不変=追加のみ。**アウトラインは不採用**(Oracle が「暗い画面で浮くので不要」と明言)
- **対象**: 自機の描画(SpriteRenderer の sortingOrder ないしプレイヤー用マテリアル)。描画コード変更が必要
- **注意**: 全ステージに効く変更になりやすい。石工以外(過去ステージ)のスクショも1枚撮って副作用がないか見る
- **検証**: 3.1 → 3.2 → 3.4(64.3〜65.7s)→ Oracle 画像レビュー。→ EditMode 49/49・石工 before/after・Captain 回帰なし・Oracle 合格で完走
- **戻し方**: `git revert 814765d`(shader+component+GManager フックがまとまっている)
- **戻し方**: 変更ファイルの revert

### G. ✅ 床消滅の演出強化: 亀裂ライン(JSON のみ)【完了・`309aae0`(2026-07-03 深夜4)】
- **結果**: 新バッファ `stone_floor_crack.json`(石工床亀裂・stone_warning 型 23発)を追加し、chart "40:1"(65.0s)に発生イベント追加。中央(16,0.85)→左右へ appearTime 0→0.28 でギザギザに伝播、life=0.59 で 65.59s に一斉消滅。床(65.417s 消滅)の直前に全長が読め、「床がヒビ割れる→砕けて消える→残光として消える」の因果を作る。Oracle 画像レビュー=条件付き合格、指摘(非均等密度・色 (0.55,0.68,1.0)・ギザギザ+20%・life 短縮)を反映済み。golden 再生成(count 23・eventCount 105→106)・ChartCompileParityTest 期待値 106・EditMode 49/49 緑・通し再生スクショ済み。詳細は PROGRESS 2026-07-03 深夜4
- **画面揺れは実装済み(2026-07-04・`75be3d5`)**: `Assets/Scripts/Managers/CameraShake.cs`(idle-by-default・トリガー時のみ transform に触れ厳密復元)+ GManager の石工 63.333s 交差検知で **M21 ゴーレム着地**にカメラシェイク(振幅0.22u≈画面高1.2%・0.16s 減衰・下方向初撃)。Oracle 動画レビュー=合格(重さ適正・酔いリスク低)。CameraShake.Trigger(amp, dur, freq) は汎用なので、床消滅(65.417s)にも揺れを足すなら GManager に同様の交差検知を1つ足すだけ(要レビュー)
- **戻し方**: `git revert 309aae0`(新 JSON+meta、chart、stone.json、golden、テスト期待値がまとまっている)

### H. カッターのコマ送り移動/ビート明滅(Gemini 優先度2)【難易度: 高 / 着手前にユーザー相談推奨】
- **内容**: カッター移動を等速からビート同期のコマ送りへ、または回転/火花のビート明滅
- **理由**: コンテンツ再設計を伴い、避け心地(難易度)が変わる。PROGRESS でも一貫して「ユーザー判断待ち」扱い
- **代替**: 移動は等速のまま「ビートごとの火花明滅」だけなら JSON 追加で可能性あり。やるならそちらを先に提案として実装し、ユーザーレビューに委ねる

### I. 地面をカッター通過順に崩壊(Gemini 優先度1の後半)【難易度: 高 / 着手前にユーザー相談推奨】
- **内容**: ベルト帯を一括消滅ではなくカッター通過位置から順に崩す
- **理由**: `stone_belt_bottom_2.json` を複数分割する再設計。65.417s 同期(直近2ラウンドで作り込んだ因果)を壊すリスクが高い。現状でも Oracle は「粉砕→床消滅→次攻撃の因果が読める」と評価済みで、緊急度は低い

**推奨着手順(Opus 単独で完結できる順)**: ~~B~~ → ~~A~~ → ~~C~~ → ~~G の亀裂のみ~~ → ~~D~~ → ~~E~~ → ~~F~~ → ~~G の画面揺れ(M21着地・`75be3d5`)~~(**§4 の A〜G すべて完了**)。H/I は実装せず提案止まり(要ユーザー相談)。Oracle 動画レビューのパイプライン確立済み(録画→ffmpeg で <1MB 圧縮→browser 添付→冒頭 describe で実視聴確認)。

---

## 5. 凍結リスト(ユーザー判断待ち・変更禁止)

| 項目 | 場所 | 理由 |
|---|---|---|
| `appearBeatBaseAlpha = 0.2f` | `Assets/Scripts/Bullets/BulletRenderSystem.cs:14` | 全ステージの予告フェード共通定数。Oracle は 0.3 を提案したが全体の見た目に効くためユーザー判断 |
| 破片(shatter_shard)のアルファ | `shatter_shard.json` | 「半透明で最初表示するのをやめて」というユーザー指示と干渉。寿命 0.96s も Oracle 確定値 |
| カッターの最終 Y / スケール | `edge_cutter_1/2.json` | 当たり判定=難易度に直結。Oracle 案(Y を 0.3〜0.5 下げ or scale 0.9)は保留中 |
| 形態変化の**キャラ演出**(召喚→落下→着地→騎乗) | `stone.chart.json` enemies 3エントリ一式 | 旧凍結対象だったクロスフェードは、ユーザー指示動画 `Instructions/石工弾幕形態変化.mp4`(2026-07-04 追加)に基づき「老人召喚→ゴーレム上空落下→M21着地→老人騎乗」へ刷新済み(`3cd3931`・Oracle 合格)。**この mp4 準拠の形が新基準**。さらに変える場合は要相談 |
| 下部破裂予告 | `lower_burst_warn_1/2.json` | ユーザー合流ラウンドで点線→**半透明の危険域ボックス1発・(0.3,0.34,0.55)** に再設計済み(`7e7f916`/`7d56dd1`)。旧記載「点線 RGB 0.52/0.63/0.98」は陳腐化。現行のボックス形式・色がユーザー由来の基準なので変更しない |
| 起動リングの赤 | `golem_core_ring.json` | 因果演出(赤=起動インパクト)で Oracle 確定。ネイビー統一の対象外 |
| Script Changes While Playing | Unity Preferences | ユーザー環境設定。変更しない(推奨値の伝達のみ) |
| CLAUDE.md「短期的な指示」 | `CLAUDE.md`(dirty) | 全項目対応済みのはずだが、セクション整理はユーザー確認後 |
| push / 公開 / force push | — | ユーザー確認必須 |

## 5.1 石工 統一ネイビーパレット(R9 `d9ce6ea` で確定・以後これに揃える)

| 役割 | RGBA | 対象 |
|---|---|---|
| ソリッドブロック/コンベア/床 | `0.025, 0.04, 0.095, 0.98` | block spawn/drop/settle・belt_flow・belt_bottom |
| エフェクト雲(ダスト/フラッシュ/明滅) | `0.42, 0.5, 0.8, (α各自)` | land_dust*・*_flash・erase_flash・prefall_blink・transform_dust |
| 予告(点線 warn) | `0.45, 0.55, 0.85, 0.962` | block/rain/tile warn・run_cutter_warn |
| カッター刃 | `0.12, 0.17, 0.42, 1.0` | edge/run/lower cutter(視認性で据置) |
| 破片/デブリ | `0.1, 0.14, 0.36, 1.0` | shard 系 + **lower_burst の破片(2026-07-04 `f483add` で統一・Oracle 合格)** |

- 例外(統一しない): `lower_burst_warn`(現行の半透明ボックスはユーザー由来)・`shatter_shard`(凍結α)・`golem_core_ring`(赤)。cutter/hammer は JSON tint でなく **color.w=0+ネイビースプライト表示**(`404bebf`)で統一済み。棚卸し詳細は PROGRESS 2026-07-04 昼の R9 節と同日夕の節。
- Oracle 任意メモ(2026-07-04): lower_burst のネイビー破片は視認性合格だが、実プレイで見落としが出る場合のみ「明度+10〜15%」または「1px の薄いリム」を検討(色相は戻さない)。

## 6. その他の申し送り

- `Recordings/` の中間 mp4(`stone_20260703_*.mp4`)と `_frames/` は削除可(gitignore 済み)。ただし `stone_v6_capfix_20260703.mp4` と `stone_v6_gemini_advice.md` は参照元なので残す
- Oracle の Chrome ウィンドウが最小化/画面外に行くことがある。復旧手順は memory(`project_oracle_chrome_window_behavior.md`)にあり
- セッション終了時は必ず `PROGRESS.md` の先頭に (1)やったこと (2)検証結果 (3)未解決と次の一手 を追記し、このファイルの §1/§4 も実態に合わせて更新すること
