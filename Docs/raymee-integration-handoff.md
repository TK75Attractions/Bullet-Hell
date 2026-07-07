# raymee/Result 統合 — 引き継ぎ（残タスク）

別端末で継続するための引き継ぎ。ブランチ `integration/raymee-runtime` に同梱。

## 現状（2026-07-07 時点）
- ブランチ: `integration/raymee-runtime`（base `marron/claude-codex` @`3ad8256`）
- WIP commit `ec08dd4`: **raymee 弾/ステージ/敵ランタイムを theirs 取り込み + obsolete 削除（未コンパイル）**
- ローカルの font `.asset` churn（Oxanium/LiberationSans Fallback）は Unity 自動再生成なので **未commit・無視**
- 承認済みプラン: `~/.claude/plans/peaceful-exploring-avalanche.md`（このドキュメントが Stage0 実査を反映した最新版）

## 確定した決定事項
- **実行系・スキーマ = raymee 基準**。**UI = marron 維持**。**Result画面/スコアHUD = 不採用**。**Boss/難易度 = 採用**。**marron データは自動変換**。
- 難易度 = **Easy / Normal / Lunatic** の3段。`DifficultyResolver`(marron) は raymee `Difficulty` に寄せる（削除済）。

## 済み（Stage 0 実査 + Stage 1 機械部）
Stage 0 でランタイム結合を実査済み。核心:
- `QuadOrder` は marron=`partial class`(6分割) vs raymee=単一 monolith＋`QuadGrid` struct で**相互排他** → raymee 版採用。
- raymee `QuadGrid` は Burst 用 broadphase struct。`QuadOrder`(driver) が毎フレーム `CreateQuadGrid()` して各Job(`BulletDataUpdateJob`/`WarpBulletJob`/`LASERQuadJob`)へ渡す。
- `EnemySpawner` 削除 → 責務は `BossSpawner/BossManager/BossMover`（動く/HPあり/**弾撃たない**）と `MultiBulletSpawner/MultiBullet`（**静的posの弾emitter**）に分割。fade/sortingOrder/動く発射源はギャップ（Stage4）。

機械的スワップ（commit `ec08dd4`）:
- **take-raymee**: `Assets/Scripts/{Bullets,Enemies,Stages}` 丸ごと + `Managers/{QuadOrder.cs,QuadGrid.cs,CManager.cs,PlayerController.cs}` + `Rendering/ScreenNoiseBlurFeature.cs` + `Difficulty.cs`
- **削除**: `Bullets/{BulletClip,BulletChangeClip,BulletEvent,BulletHandle}`, `Enemies/EnemySpawner`, `Stages/{ChartTimeExpr,DifficultyResolver,StageScheduleExpander}`, `Managers/QuadOrder.{Collision,Enemies,Laser,Patterns,Seek}`, `Patterns/*`
- **keep（重要な例外）**: `Stages/StoneBeltScrollDriver.cs`（石工コンベア背景ドライバ。marron-only だが削除しない）, `UI/*`, `InputManager.cs`, `Debug/*`

---

## 残 Stage 1: コンパイルを通す（linchpin = GManager）
現在のコンパイルエラーは **marron↔raymee の境界の食い違いのみ**（弾ランタイム自体は綺麗に入った）。2系統:

### A. GManager に raymee ランタイム要求メンバが無い
- `GManager.BulletBuffers`（raymee `StageReader`/`QuadOrder` が多数参照）
- `GManager.playerColor`（raymee `PlayerController`/`BulletRenderSystem` が参照）

**方針**: **GManager は raymee 版を土台に3-wayマージ**し、marron の `title/pause/stone-landing-shake/入力(IManager)/カメラshake` フローを再適用するのが最短。
- 参照 diff: `git diff 9582662 origin/raymee/Result -- Assets/Scripts/Managers/GManager.cs` と `git diff 9582662 marron/claude-codex -- Assets/Scripts/Managers/GManager.cs` を突き合わせる。

### B. marron keep コードが raymee クラスに無い API を呼ぶ
| 呼び出し元(keep marron) | 欠落 API | 対応 |
| --- | --- | --- |
| StageTimeOverlay, StoneBeltScrollDriver, GManager | `StageReader.IsReady / CurrentStage / CurrentTime` | raymee `StageReader` に薄い互換プロパティを足す（内部状態にマップ）のが安全 |
| GManager | `StageReader.ResetStageClockToScheduledStart` | raymee 等価があれば呼び出し更新、無ければ移植 |
| GManager, StageSelectManager | `PlayerController.ResetToCenter` | raymee `PlayerController` へシム追加 or 呼び出し更新 |
| GManager | `CManager.SetMenuBlur` | raymee `CManager` へ移植 or 呼び出し更新 |
| GManager | `CounterBullet.ResolveTypeId` | raymee 等価確認して更新 |

→ コンパイルが通ったら **Stage 1 完了 commit**。石工はまだ旧データなので壊れている＝この時点では正常。
※ `AudioManager.StopBGM`/`BeatManager.StopBeat` を raymee コードが呼ぶ場合も同様にブリッジ（marron版 keep のため）。

---

## 残 Stage 2: 難易度UI配線 / Result UI ドロップ
- marron `DefficultyBar`/`StageSelectManager` を raymee `Difficulty`/`DifficultySelection` に接続。`GManager.selectedDifficulty` 相当を raymee 難易度モデルへ橋渡し。
- `ResultUIManager`/`ScoreUIManager`/`GameResultData` は **import しない**。raymee GManager がスコア集計フックを持つ場合、UI へ出さず内部保持のみで参照解決。

---

## 残 Stage 3: marron データ自動変換（`Tools/` に Python、冪等・--dry-run）
raymee `StageReader` が要求する schema（Stage0 で確定）へ変換。

### 3a. BulletBuffer 弾クリップ JSON（`Assets/BulletBuffers/**/*.json`）
各弾要素に対し:
- `gravity: G`(number) → `gravity: { "x": G, "y": -1.5707963 }`
- `size: S`(scalar) → `scale` が `{0,0}` の時のみ `scale: {"x":S,"y":S}` にし、`size` 削除
- `lockRotation` → 基本削除（`true` があれば `useVelocityAngle:false`）
- 付与: `radiusAccel:0`, `thetaAccel:0`, `useVelocityAngle:true`
- 他フィールドは温存: `originPos,originVlc,startX,speed,initialAngle,angleSpeed,polarForm,radiusVlc,thetaVlc,startPos,polynomial,typeName(string),color,appearTime,appearDuration,life,random,unCounterable`
- 対象実データ: marron 148ファイル中 `size` 使用87 / `lockRotation` 保持98（`true` の実データは無い＝基本削除で可）。raymee は既に `25/`・`_archive/` を削除済なので、統合後 `25/` は不要データとして別途整理可。

### 3b. StageData JSON（`Assets/StageData/**`、例 `stone.json`）
raymee top-level `StageDataJson`: `stageName, endTime(>0必須), delayTime, stageDescription, MusicEvents[], enemyVisuals[], difficulties[]`（推奨） ｜ legacy top-level `multiBulletSpawners/bossSpawners/bulletSpawners`（difficulties空なら Lunatic に自動ラップ）。
- marron top-level `bulletSpawners` → `difficulties[].bulletSpawners`（Easy/Normal/Lunatic 各）
- inline 難易度の事前展開: `minDifficulty` → 各難易度リストから該当 spawner を除外。`thinEasy`/`thinNormal` → 難易度別に BulletBuffer を複製し弾配列を間引き、`clipName` 差し替え。
- `enemySpawners`（石工は `bulletCount:0` のビジュアル専用）→ `bossSpawners`: `enemyAppearTime→appearTime`, `orbit.life→lifeTime`, `orbit.originPos→startPos`, `orbit.scale→scale`, `visualId`, `animation`。ゴーレム降下 `originVlc.y=-7.2` → `BossMover` の `AddVelocity`/`Stop`（`moves[]`）。
- `patternEvents`: 石工は空 → skip。

raymee 変換ターゲット DTO（フィールド）:
- **BulletSpawnerJson**: `clipName, count, interval, time, pos(Vector2), originVlc(Vector2), angle, angleInterval, color(Vector4)`（`index` は JSON に無く clipName から解決。`"Clear"`→特殊クリア）
- **BossSpawnerJson**: `bossId, bossName, visualId, appearTime, lifeTime(-1=無限), maxHp(100,≥0.01), startPos(Vector2), scale(Vector2,(0,0)→(1,1)), angle, animation(BossAnimationPlan), moves[](BossMoveEventJson)`
  - **BossMoveEventJson**: `time, duration, type(setposition|moveto|bezierto|addvelocity|stop), to(Vector2), control(Vector2), easing(linear|easeincubic|easeoutcubic|easeinoutcubic|easeinoutsine), relative(bool)`
  - **BossAnimationPlan**: `initialClip("idle"), events[]{time,clip,next,overrideLoop,loop}, triggers[]{trigger,clip,next,overrideLoop,loop}`
- **StageDifficultyDataJson**: `difficulty("Easy|Normal|Lunatic", int も可), displayName, multiBulletSpawners[], bossSpawners[], bulletSpawners[]`
- **MultiBulletSpawnerJson**: `pos(Vector2), time, bulletEmission(BulletBufferEmissionJson), bulletBufferTriggers[](BulletBufferEmissionJson)`
- **BulletBufferEmissionJson**: `clipName, time, originVlc(Vector2), angleOffset, angleMode("absolute|fixed|none"⇒絶対 / 他⇒inheritSourceAngle), inheritSourceVelocity(bool), applyBulletOrbit(bool), deactivateSource(bool), color(Vector4, 全0⇒白)`

---

## 残 Stage 4: raymee 側 最小スペック追加（データで吸収不可）
1. `Enemies/Visuals/EnemyVisualDefinition.cs` に `transparentBackground`(bool), `transparentTolerance`(int) を、`EnemyVisualClipDefinition` に `maxFrames`(int) を **復活**（marron 版に存在）。raymee のビジュアルローダ(`EnemyVisualLoader`/`GifAnimationDecoder`)がこれを読むよう配線（石工 GIF の透過/フレーム制限維持）。
2. `Enemies/BossSpawner.cs` ＋ `BossManager.cs`(Spawn時に `SpriteRenderer.sortingOrder` 設定) ＋ `Boss.cs`(αランプ) に `fadeInSec`, `fadeOutSec`, `sortingOrder` を追加（石工の形態変化・前後関係維持）。
3. （任意・低優先）`lockRotation`/`fixedAngle` 完全互換 / 動く発射源 MultiBullet / `patternEvents` authoring 復活は将来。

---

## 検証（各 Stage 末、Unity MCP）
- **コンパイル**: `read_console(types=error)` が 0。`EditorApplication.isCompiling==false`。大規模変更後は `refresh_unity(mode=force, scope=all, compile=request)` で AssetDatabase 再スキャン必須（scripts スコープだと削除を拾い切れず `CS2001` が出る）。
- **EditMode テスト**: `Assets/Tests/EditMode/` の `Buffer*Tests`,`Stage*LintTests`,`DifficultyResolverTests` は **旧スキーマ前提で落ちる** → raymee スキーマへ更新/差し替えが必要（棚卸し要）。
- **実挙動**: Play → `Tools/Bullet Hell/Debug/Start Stone Stage` → `Dump Stone Debug State`。石工の弾軌道・敵/ボス・難易度切替・透過GIF・前後関係を目視。録画→Oracle/Gemini。
- **完了条件**: S1=コンパイル通る / S2=難易度UI切替可 / S3=石工が新データで起動・弾が旧軌道相当 / S4=石工ビジュアル(透過/前後/フェード)が旧marron同等。

## 別端末での再開手順
1. `git fetch origin && git checkout integration/raymee-runtime`（`git pull` で `ec08dd4`＋本doc取得）
2. Unity で開く（大規模再コンパイル。`refresh_unity force/all` を1回）
3. `read_console(error)` で上記 A/B エラー確認 → **GManager マージから着手**
4. 参照差分: `git diff 9582662 origin/raymee/Result -- <file>` / `git diff 9582662 marron/claude-codex -- <file>`
5. Stage ごとに local commit（チェックポイント）。`marron/claude-codex` へのマージは全検証後・ユーザー確認後。

---

## 進捗ログ（2026-07-07 更新 / Opus 第37便）

### Stage 1 完了（コンパイル通過）
- `6445593` ランタイム境界シム: GManager に `BulletBuffers`(=`BClipManager` エイリアス)/`playerColor`、`CounterBullet.ResolveTypeId` 削除(raymee は struct 定数 `TypeId=18`)。StageReader に `IsReady`/`CurrentStage`/`CurrentTime` エイリアス。PlayerController.`ResetToCenter`(=`ResetForStage`)、CManager.`SetMenuBlur`(スタブ, 実ブラー復元は Stage2)。
- `dde1929` 棚卸し削除: 旧 marron の chart/pattern/golden authoring(StageChartCompiler/StageValidation/StageGoldenDumper/StageLinterMenu/StageSeekSupport旧/BulletTypeSyncMenu/StageDebugLauncherWindow)＋ EditMode テスト16個。`PlayHistoryCodeTests` のみ保持。`StageSeekSupport` は非結合ヘルパのみの最小版に再作成。録画/撮影/デバッグメニューは維持。

### Stage 2.5 完了（Fable レビューで発見した Stage3 前提の3ブロッカー）
`a5744c3`:
1. **音ハメクロック移植** — raymee StageReader は `time+=dt` のフレーム加算で BGM が2s後発音のため弾が音楽より約2s+ずれていた。marron の DSP 同期(`time = dspTime - scheduledDspTime - delayTime`)を移植(stageBgmSource/scheduledDspTime を Init で保持、UpdateStage で同期、`state != Playing` ガード復元)。
2. **BulletTypeDataBase に石工の弾タイプ10種を末尾追加** — raymee版 DB(19)から stone_block/warning/cutter/shard/burst/dust/flash/shovel/conveyor_belt/warn_box が欠落していた(.asset は現存)。`counter_star=index18` 維持。起動時に `TypeId=18==counter_star` を assert。`bPower/bVerts` は `Init()` が再構築。**18番より前への挿入は厳禁**。
3. **GoGameAsync ブリッジ** — `CreateRuntimeCopy(Lunatic)` + Init 戻り値チェック。共有 StageData の直接 mutate を防止。
+ デッドフィールド `GManager.MultiBulletObj` 削除。

### Stage 3 完了（石工データ変換 → Lunatic 起動を実機確認）
`6ffe0c9`:
- **BulletBuffer 変換ツール** `Tools/convert_bulletbuffers_to_raymee.py`(冪等・`--dry-run`): **gravity number→極形式 `{x:G, y:-1.5707963}`**(JsonUtility が number を Vector2 に読めず {0,0}=落下弾全滅になるため必須)、size→scale、lockRotation:true→useVelocityAngle:false。stone/ 118ファイル1940弾に適用済み。**他ステージ(25/captain/mirror 等)のバッファは未変換**(必要になったら `--path` 無指定で全実行)。
- **stone.json** を raymee top-level へ: `endTime=82` 追加(欠落が Init 失敗の直接原因)、`difficulties:[]` 空のまま(ローダが legacy top-level を Lunatic 自動ラップ: `StageDataManager.ApplyStageDataJson`/`HasLegacySpawnerData`)。`enemySpawners`(ゴーレム降下/スラム/老人)→ `bossSpawners`(降下 originVlc.y=-7.2 は `MoveTo(19.64→13.64, 0.833s, linear)` へ)。`_generatedFrom/enemySpawners/patternEvents` 削除。
- **実機検証(runInBackground=true で通し)**: `Started Stage: 石工 (Lunatic)`、"Bullet clip not found"/endTime/NullRef **なし**。弾数が全区間で健全(9.7s:102, 60s:69, 63.5s:20, 77.8s:71)。落下ブロックが gravity で降下、老人ボス・**ゴーレムの形態変化(降下→出現、頭上に老人)**が正しく描画。DSP クロックでマーカー時刻と整合。

### 残タスク / 既知の差分
- **Stage 4(raymee 最小スペック追加)**: `EnemyVisualDefinition` に `transparentBackground`/`transparentTolerance`、`EnemyVisualClipDefinition` に `maxFrames` を復活(GIF 透過/フレーム制限)。`BossSpawner/BossManager/Boss` に `fadeInSec`/`fadeOutSec`/`sortingOrder` を追加(現状 stone.json 変換で一旦落としている。老人 sortingOrder=9/fadeOut=0.4 の前後関係・フェードが未反映)。
- **弾スプライトの見え方の差**: raymee 描画で **ブロックがひび模様なしの平坦ネイビー**、**縁カッターが marron の大鋸刃より小さく**描画される。BulletRenderSystem/mask シェーダの挙動差か color.w=0 の扱い差の可能性。要調査(Stage4 か描画側)。
- **Stage 2(難易度UI配線)**: `DefficultyBar`/`StageSelectManager` を raymee `Difficulty` に接続。Result UI は不採用。
- **他ステージのデータ変換**: stone 以外の StageData/BulletBuffer は旧スキーマのまま(起動しない)。文化祭の主対象は石工。
- **Fable 5 は利用枠上限に達した**(2026-07-07)。以降のレビューは Unity 実機検証を判断基準に。
- **検証の罠**: エディタ非フォーカスだとゲームが一時停止(`Application.runInBackground=true` で回避)。UICamera 指定スクショは白画像トラップ→ `ScreenCapture.CaptureScreenshot` を使う。
