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
