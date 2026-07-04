# BulletBuffer Context

This is the recall note for creating BulletBuffer JSON assets in this project.
Read this file first when the user asks to create or reason about BulletBuffer danmaku assets.
After creating a BulletBuffer asset, also edit `Assets/StageData/debug/debug.json` so the new buffer runs in Unity debug playback.
The visible play area is from bottom-left `(0,0)` to top-right `(32,18)`. When editing debug spawners, choose `pos` with the BulletBuffer's internal offsets and trajectory bounds included; `(16,9)` is the safe center default.

Relevant flow:

- JSON load: `BulletBufferManager`
- Spawn scheduling: `StageReader`
- Bullet insertion and enemy emission: `QuadOrder`
- Normal bullet trajectory: `BulletDataUpdateJob`
- Laser trajectory: `LaserEmitter` and `LASER`

## 1. JSON Load

Load source:

- `Assets/BulletBuffers/**/*.json`
- `BulletBufferManager.Init()` first registers built-in buffers: `Rumia_0`, `Rumia_1`, `Line`, `LineLaser`, `Circle`.
- It then recursively reads all JSON files under `Assets/BulletBuffers`.
- If JSON `name` matches an existing buffer name, it replaces that buffer.
- If JSON `name` is blank, the file name without extension is used.

Top-level shape:

```json
{
  "name": "BufferName",
  "bullets": [],
  "homing": false,
  "isLaser": false
}
```

Rules:

- `bullets` is required.
- `homing` makes `BulletBufferManager.GetBulletClip()` aim the whole buffer at the current player position from the spawn position.
- `isLaser` routes the spawned data to `LaserEmitter.EmitLASER()` instead of appending normal bullets to `enemyBullets`.

BulletDataJson shape:

```json
{
  "originPos": { "x": 0, "y": 0 },
  "originVlc": { "x": 0, "y": 0 },
  "playerInfluence": { "x": 0, "y": 0 },
  "startX": 0,
  "speed": 4,
  "gravity": 0,
  "initialAngle": 0,
  "angleSpeed": 0,
  "polarForm": { "x": 1, "y": 0 },
  "radiusVlc": 0,
  "thetaVlc": 0,
  "startPos": { "x": 0, "y": 0 },
  "polynomial": { "x": 0, "y": 0, "z": 0, "w": 0 },
  "typeName": "normal",
  "scale": { "x": 1, "y": 1 },
  "color": { "x": 1, "y": 1, "z": 1, "w": 1 },
  "appearTime": 0,
  "appearDuration": 0,
  "life": 10,
  "random": 0,
  "warpCooldown": 0,
  "unCounterable": false,
  "lockRotation": false
}
```

Fields easy to miss (all optional, default to 0/false when omitted):

- `warpCooldown` (seconds): suppresses re-warping after a warp-zone teleport (`WarpBulletJob`). Only meaningful when warp zones are active.
- `unCounterable`: when true the bullet cannot be erased by the player dash counter (`BulletCollisionJob.cs:42`). It still damages the player.
- `lockRotation`: when true the render angle stops following the velocity vector (`BulletDataUpdateJob`); use for sprites that must not spin.
- `size` (legacy scalar): used only when `scale` is `{0,0}`. Prefer `scale`.

`typeName` values registered in `BulletTypeDataBase` (snapshot 2026-07-04; the
authoritative source is the asset itself — `Tools/Bullet Hell/Validate All Stages`
errors on any unknown name, and `Sync Bullet Types` registers new assets):

- generic: `anchor`, `attention`, `box`, `chain`, `hummer`, `knife`, `normal`,
  `simpleline`, `slash`, `splash_0`, `splash_1`, `splash_2`, `sword`, `tear`
- special-behaviour: `warp_zone` (teleport zones via `WarpBulletJob`),
  `ScreenNoise` (not an asset; resolves to reserved id -1000, `BulletData.cs:12`)
- stone stage: `stone_block`, `stone_burst`, `stone_conveyor_belt`,
  `stone_cutter`, `stone_dust`, `stone_flash`, `stone_shard`, `stone_shovel`,
  `stone_warning`

Avoid `rocket` for JSON assets because its asset exists but its `typeName` field is blank (resolves to -1 = dropped).

Important load quirks:

- `BulletDataJson.ToBulletData()` directly assigns fields and does not call the main `BulletData` constructor.
- Because of that, JSON should explicitly set `startPos`. Usually set it to `{ "x": startX, "y": polynomial(startX) }`, or `{ "x": 0, "y": 0 }` when `startX` and polynomial are zero.
- New assets should use `scale`. Legacy `size` is only used if `scale` is `{0,0}`.
- `appearDuration` defaults to `0` when omitted, but a **negative** value is silently replaced by `BulletData.DefaultAppearDuration = 1.2` (`BulletDataJson.cs:65`). Always write the intended value explicitly (the linter warns on negatives).
- `appearDuration` is **render-only** (see "Render and Collision Semantics" below); collision always starts exactly at `appearTime` regardless of it.
- `life <= 0` means no life timeout. Use positive life values for cleanup.
- Spawn color is multiplied component-wise (including `w`) with JSON color (`BulletData.cs:181`). Keep either JSON color or spawner color at white `{1,1,1,1}` unless multiplication is intentional.

## 2. Spawn Flow

Stage bullet spawners:

1. `StageReader.Init()` scans `stageData.bulletSpawners`.
2. It resolves `clipName` with `BulletBufferManager.TryGetBulletClipIndex()`.
3. It expands `count`, `interval`, `time`, and `angleInterval` into scheduled `BulletSpawnEvent` entries.
4. `StageReader.UpdateStage()` calls `QuadOrder.AddEnemyBullets(index, pos, originVlc, angle, color)` when an event reaches its time.
5. `BulletBufferManager.GetBulletClip()` clones the template BulletData entries into spawned BulletData entries.
6. Normal buffers append to `enemyBullets`; laser buffers go to `LaserEmitter`.

Enemy bullet emission:

1. `StageReader` passes enemy spawners to `QuadOrder.AddEnemy()`.
2. Enemy `orbit` is added to `enemiesOrbitBullets` and updated by `BulletDataUpdateJob`.
3. `Enemy.Shot()` passes `bulletClip` to `QuadOrder.EmitEnemyBullet()`.
4. `bulletClip.number` and `disRad` create a fan. `disRad` is treated as degrees and converted with `math.radians()`.
5. `bulletChangeClips` can later call `QuadOrder.UpdateBulletData()` to replace a single bullet or deactivate old bullets and emit a new clip.

Angle conventions:

- Stage spawner `angle` is degrees.
- JSON `polarForm.y`, `thetaVlc`, `angleSpeed`, and `initialAngle` are radians.
- For non-homing BulletBuffers, spawner degrees are converted to radians and added into `polarForm.y` during clone construction.
- For homing BulletBuffers, spawner `angle` is ignored and the buffer is aimed at the player.

## 3. Normal Bullet Trajectory

Every frame, `BulletDataUpdateJob.Execute()` updates active bullets.

Lifetime:

- `time += dt`
- `originPos += originVlc * dt`
- `originPos += playerVelocity * playerInfluence * dt`
- `lapse = time - appearTime`
- If `appearTime > time`, the bullet receives only `dt * 0.00001` trajectory progress and returns.
- If `life > 0 && time >= life`, the bullet becomes inactive.

Local polynomial motion:

- `nowCalculateX` is the current local x.
- `nowCalculateVlc = normalize((1, polynomial'(x))) * speed`
- `x += nowCalculateVlc.x * dt`
- `y = a*x + b*x^2 + c*x^3 + d*x^4`
- `disVector = (x,y) - startPos`

Polar transform:

- `polarForm.x += radiusVlc * dt`
- `polarForm.y += thetaVlc * dt`
- `rotatedVector = polarForm.x * rotate(disVector, polarForm.y)`
- `position = rotatedVector + originPos + smoothNoise`

Player influence:

- `playerInfluence.x` scales how much the bullet origin follows the player's x velocity.
- `playerInfluence.y` scales how much the bullet origin follows the player's y velocity.
- `{ "x": 1, "y": 0 }` makes the pattern drift horizontally with the player; `{ "x": 0, "y": 1 }` makes it drift vertically; omitted values default to `{0,0}`.

Gravity:

- If `gravity != 0 && lapse > 0`, subtract `gravity * lapse^2 / 2` from y.
- Positive gravity bends downward.

Visual angle:

- `angle = atan2(velocity.y, velocity.x)`, normalized to `0..2*pi`.
- Then add `angleSpeed * lapse`.

Bounds:

- The exact survival region is `-2 <= position.x < 36 && -2 <= position.y < 36`. Left/bottom is `CullingMargin = 2` (`BulletDataUpdateJob.cs:12,145`); right/top is the Morton cell grid: `separateLevel = 6` -> 64x64 cells x `cellSize = 0.5625` = 36 world units (both values live on the GManager scene object, `QuadOrder.AwakeSetting`). A bullet whose position leaves that square is silently set inactive on that frame.
- The check only runs after `appearTime` (`BulletDataUpdateJob.cs:43-58` returns early before it), so a bullet may WAIT outside the region, but on its first post-appear frame it must already be inside — a spawn at `x >= 36` intended to "fly in from the right" never appears at all (measured 2026-07-04: `石工ベルトダッシュ` loses its 9 bullets authored at x=38..70 this way; see REFACTOR-REPORT §8).
- Entry margins are therefore: 2 units on the left/bottom, 4 units right of the visible 32, 18 units above the visible 18. Design trajectories around the visible `0..32 / 0..18` area and spawn inside `[-2,36)` on both axes.
- Pitfall: `polarForm.x == 0` collapses the rotated vector to the origin (`rotatedVector = polarForm.x * rotate(...)`), so a "straight" bullet with `polarForm.x = 0` renders stuck at `originPos`. Straight bullets need `polarForm = { 1, angleRad }`.

## 4. Laser Trajectory

`isLaser: true` BulletBuffers use `LaserEmitter.EmitLASER(List<BulletData>, pos)`.

JSON field mapping:

- spawner `pos` -> laser origin position
- `originVlc` -> laser origin velocity
- `thetaVlc` -> angular velocity
- `speed` -> extension/travel speed
- `polarForm.y` -> initial angle
- `startX` -> initial polynomial x
- `startPos` -> polynomial baseline
- `polynomial` -> path curve
- `max(abs(scale.x), abs(scale.y))` -> laser length
- `appearTime` -> laser width
- `life` -> laser lifetime
- `color` -> material color

Important laser quirk:

- For laser buffers, `appearTime` is not a spawn delay. It is passed as width.
- To make visible lasers, set `isLaser: true`, use `scale` for length, set `appearTime` to a positive width, and set `life` to a positive lifetime.

## 5. Render and Collision Semantics

These are the rules that most often surprise editors because they live in the
renderer/collision jobs, not in the trajectory math.

`appearDuration` (render-only, `BulletRenderSystem.cs:269-288`):

- `appearDuration > 0`: invisible before `appearTime - appearDuration`; during the window `[appearTime - appearDuration, appearTime]` the bullet is drawn with a beat-pulsing alpha (`saturate(appearBeatBaseAlpha + coeff * beatValueSin)` — the music-synced shimmer used by warn telegraphs); fully drawn after `appearTime`.
- `appearDuration == 0`: invisible before `appearTime`, pops in at `appearTime`.
- Collision is independent of all of this: `BulletCollisionJob.cs:37` skips hits only while `appearTime > time`. Changing `appearDuration` never changes difficulty or timing.
- `appearDuration > appearTime` is common and legal (the window is simply clipped at clip spawn); the repo has ~340 such bullets.

`color` (tint model, `BulletIndirectURP.shader:120-124`):

- `tintStrength = saturate(mask * color.w)`; `rgb = lerp(spriteRGB, color.rgb, tintStrength)`; `alpha = max(spriteAlpha, tintStrength) * fade`.
- So `color.w = 0` means "show the sprite's own colors and shape" (used by cutters/hammers) — the bullet is NOT transparent. Note the mask covers the full quad on some types, making the visual a solid rectangle.
- With `color.w = 0` the screen pixels come from the sprite, so pixel sampling cannot be compared against JSON tint values; compare against other bullets using the same setup instead.
- Collision never reads `color`; an alpha-0-everything bullet would still hit. The linter warns on components outside `0..1`.

Counter (dash) interaction:

- `counterPower` is NOT a JSON field. It is derived from the `BulletType` asset's `verts` polygon area at load (`BulletType.cs:32-50`); `verts` shorter than 3 gives power 0.
- On a dash-counter kill the player gains `bPowers[typeId] * uniformScale` attack (`BulletCollisionJob.cs:48`). From JSON you control only `unCounterable`.
- Telegraph/effect bullets should use types with empty `verts` (fully harmless) — that is why warn buffers are safe regardless of their other fields.

Two DTO schemas exist (divergence to know about):

- BulletBuffer JSON uses `BulletDataJson` (this document). Inline bullets inside `stage.json` enemy `orbit` / `bulletClip` / `bulletChangeClips` go through a separate deserializer in `StageDataManager` that has NO `playerInfluence` and NO `warpCooldown` fields — those two features work only in BulletBuffer JSON files.
- `chart.json` is parsed with Newtonsoft (comments tolerated), while BulletBuffer JSON and `stage.json` use `JsonUtility` (strict: no comments, no NaN/Infinity, unknown keys ignored, missing keys become 0/false/null).

## 6. File Format and Registration Rules

Enforced by `Tools/Bullet Hell/Validate All Stages` and the EditMode test
`BufferFormatInvariantTests` (errors fail the suite):

- Files must be valid UTF-8 and must use ONE line-ending style per file. A `\r\r\n` sequence is the signature of a text-mode double conversion (a real incident on 2026-07-04) and is a hard error.
- Repo convention is UTF-8 **BOM + CRLF** for most buffers (some are LF; both are accepted — just never mix within a file). Safe edit recipe: read the current bytes (or `git show HEAD:<path>` if the working copy is suspect), modify, and write back in **binary** mode preserving BOM and line endings. Do not use text-mode Python writes from Git Bash.
- Registration name = JSON `name`, or the file name without extension when `name` is blank. One runtime load scope = built-ins (`Rumia_0`, `Rumia_1`, `Line`, `LineLaser`, `Circle`) + everything under `common/` and `debug/` + the stage's own folder. Duplicate names inside one scope silently replace each other, so the linter errors on duplicates (and warns when a stage buffer shadows a common/built-in one).
- `Assets/BulletBuffers/_archive/` is never loaded by the per-stage loader; it is excluded from name checks but still schema-checked.

## 7. Asset Design Patterns

Straight bullet:

- `polynomial = {0,0,0,0}`
- `startX = 0`
- `startPos = {0,0}`
- `polarForm.x = 1`
- `polarForm.y = angleRad`
- `speed > 0`
- `radiusVlc = 0`
- `thetaVlc = 0`

Radial ring or spiral:

- Use many bullets with `polarForm.y = 2*pi*i/n`.
- Add `thetaVlc` for rotation.
- Positive `radiusVlc` expands outward; negative `radiusVlc` contracts inward.
- Stagger `appearTime` for waves or delayed volleys.

Curved bullet:

- Use `polynomial.x/y/z/w` for local curve shape.
- `speed` follows the curve tangent.
- `polarForm.y` rotates the whole local curve.
- Keep `startPos` consistent with `startX` and the polynomial.

Falling bullet:

- Use `gravity > 0`.
- Initial movement direction still comes from `polarForm.y`.
- Use `originVlc` if the whole emission origin should drift.

Snake or wave:

- Combine non-zero `thetaVlc` with a second or higher polynomial term.
- Small `random` adds smooth per-bullet noise.

## 8. Creation Checklist

- Top-level `name` exactly matches the StageData `clipName`, and is unique within its load scope (stage folder + common/debug + built-ins).
- `bullets` is not empty.
- `typeName` exists in `BulletTypeDataBase`.
- JSON angles are radians; StageData spawner angles are degrees.
- Normal bullet `startPos` matches `startX` and `polynomial`; straight bullets use `polarForm = { 1, angleRad }` (never `polarForm.x = 0`).
- Spawn position and trajectory stay in the valid play area unless intentional.
- Use positive `life` for cleanup; write `appearDuration` explicitly (never negative).
- Prefer `scale` over legacy `size`.
- For lasers, use `isLaser: true`, `scale` as length, and `appearTime` as width.
- Preserve the file's BOM and line-ending style when editing (binary write).
- After editing: `Tools/Bullet Hell/Validate All Stages` = 0 errors, then run the EditMode tests (golden catches unintended behaviour change).
