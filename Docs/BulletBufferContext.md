# BulletBuffer Context

This is the recall note for creating BulletBuffer JSON assets in this project.
Read this file first when the user asks to create or reason about BulletBuffer danmaku assets.
After creating a BulletBuffer asset, also edit `Assets/StageData/debug/debug.json` so the new buffer runs in Unity debug playback.
The visible play area is from bottom-left `(0,0)` to top-right `(32,18)`. When editing debug spawners, choose `pos` with the BulletBuffer's internal offsets and trajectory bounds included; `(16,9)` is the safe center default.

Generator project coupling:

- Breaking changes to StageData or BulletBuffer JSON/runtime contracts must also be checked in:
  - `C:\Users\tsuka\ドキュメント\GitHub\BulletBufferMaker`
  - `C:\Users\tsuka\ドキュメント\GitHub\Bullet-Hell-StageDataMaker`
- `BulletBufferMaker` emits BulletBuffer JSON. `Bullet-Hell-StageDataMaker` emits StageData JSON. Keep their DTOs and sample generators aligned with the Unity loaders before considering schema work complete.

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
- `homing` makes `BulletBufferManager.CreateSpawnedBullets()` aim the whole buffer at the current player position from the spawn position.
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
  "unCounterable": false
}
```

Known usable `typeName` values:

- `attention`
- `anchor`
- `box`
- `chain`
- `hummer`
- `knife`
- `normal`
- `simpleline`
- `slash`
- `splash_0`
- `splash_1`
- `splash_2`
- `sword`
- `tear`

Avoid `rocket` for JSON assets because its asset exists but its `typeName` field is blank.

Important load quirks:

- `BulletDataJson.ToBulletData()` directly assigns fields and does not call the main `BulletData` constructor.
- Because of that, JSON should explicitly set `startPos`. Usually set it to `{ "x": startX, "y": polynomial(startX) }`, or `{ "x": 0, "y": 0 }` when `startX` and polynomial are zero.
- `scale` is required for bullet size. If omitted or `{0,0}`, runtime falls back to `{1,1}`.
- `appearDuration` defaults to `0` when omitted. During `appearTime`, the bullet receives almost-zero trajectory updates and then appears immediately.
- `life <= 0` means no life timeout. Use positive life values for cleanup.
- Spawn color is multiplied with JSON color. Keep either JSON color or spawner color at white `{1,1,1,1}` unless multiplication is intentional.

## 2. Spawn Flow

Stage bullet spawners:

1. `StageReader.Init()` scans `stageData.bulletSpawners`.
2. It resolves `clipName` with `BulletBufferManager.TryGetBulletBufferIndex()`.
3. It expands `count`, `interval`, `time`, and `angleInterval` into scheduled `BulletSpawnEvent` entries.
4. `StageReader.UpdateStage()` calls `QuadOrder.AddEnemyBullets(index, pos, originVlc, angle, color)` when an event reaches its time.
5. `BulletBufferManager.CreateSpawnedBullets()` clones the template BulletData entries into spawned BulletData entries.
6. Normal buffers append to `enemyBullets`; laser buffers go to `LaserEmitter`.

Enemy bullet emission:

1. `StageReader.Init()` resolves `multiBulletSpawners[].bulletEmission.clipName` and every `bulletBufferTriggers[].clipName`.
2. `StageReader.UpdateStage()` calls `QuadOrder.AddMultiBullet()` when the spawner reaches `time`.
3. `MultiBullet` is a plain class for derived bullet drawing, not a `MonoBehaviour`.
4. The initial `bulletEmission` is emitted once from `multiBulletSpawners[].pos`.
5. The emitted normal bullet indexes are cached. After each trigger `time`, `MultiBullet` uses each live source bullet's current position as the next BulletBuffer origin.
6. Trigger angle defaults to the source bullet angle plus `angleOffset` degrees. Set `angleMode` to `absolute` or `fixed` to use `angleOffset` as an absolute degree angle.
7. `inheritSourceVelocity` adds the source bullet's per-second velocity to trigger `originVlc`; `applyBulletOrbit` applies the referenced BulletBuffer orbit to the cached source bullets; `deactivateSource` disables the source bullet after the trigger emits.
8. MultiBullet patterns are defined only by `pos`, `time`, `bulletEmission`, and `bulletBufferTriggers`. Legacy fields `count`, `enemyInterval`, `bulletEmitTime`, `bulletCount`, and `orbit` are removed.

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

- If `position.x < 0`, `position.y < 0`, or the Morton cell index is out of range, the bullet becomes inactive.
- Avoid spawn/trajectory setups that immediately move into negative coordinates unless that is intended.

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

## 5. Asset Design Patterns

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

## 6. Creation Checklist

- Top-level `name` exactly matches the StageData `clipName`.
- `bullets` is not empty.
- `typeName` exists in `BulletTypeDataBase`.
- JSON angles are radians; StageData spawner angles are degrees.
- Normal bullet `startPos` matches `startX` and `polynomial`.
- Spawn position and trajectory stay in the valid play area unless intentional.
- Use positive `life` for cleanup.
- Set `scale` explicitly; `size` is not supported.
- For lasers, use `isLaser: true`, `scale` as length, and `appearTime` as width.
