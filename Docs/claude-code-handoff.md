# Claude Code handoff: Bullet-Hell

> 2026-07-03 追記: 自律セッション(特に Opus 運用)は、まずリポジトリ直下の `OPUS-HANDOFF.md` を読むこと。残タスク・検証手順・凍結リストが最新の状態で整理されている。

Date: 2026-07-01
Repo: `D:\Unity\Bullet-Hell`
Branch: `marron/claude-codex`
Latest pushed commit seen: `455851c Implement stone stage and timing tools`
Remote: `origin https://github.com/TK75Attractions/Bullet-Hell.git`
Unity: `6000.3.9f1`

## Purpose

The user plans to delegate about one week of work on this project to Claude Code. The immediate focus is the stone stage, especially music timing, visual readability, and tile/conveyor polish.

Read these first:

- `CLAUDE.md`
- `Docs/stone-stage-handoff.md`
- `Docs/BulletBufferContext.md`
- `Docs/claude-code-handoff.md`
- `Recordings/stone_first_gemini_advice.md` if present locally

## Current working state

UnityMCP status during this handoff:

- Instance: `Bullet-Hell@484e2546`
- Active scene: `Assets/Scenes/Base.unity`
- Play Mode: stopped
- Compile/domain reload: idle
- UnityMCP is available from Codex, but can be unstable. Avoid parallel UnityMCP calls when mutating or entering Play Mode.

Important current dirty/untracked state:

- Modified stone BulletBuffer JSONs under `Assets/BulletBuffers/stone/`
- Modified `Assets/Scripts/Bullets/BulletRenderSystem.cs`
- Modified `Packages/manifest.json` and `Packages/packages-lock.json` because Unity Recorder `com.unity.recorder@5.1.6` was installed
- Modified debug audio `.meta` files under `Assets/StageData/debug/`
- Untracked screenshots under `Assets/Screenshots/`
- Untracked `Docs/stone-stage-handoff.md`
- Local Oracle output under `.oracle-output/` is ignored and should not be committed
- `Recordings/` is ignored; the local stone recording and Gemini advice may exist there but are not meant for normal commits

Do not revert dirty files unless the user explicitly asks. Some dirty files are deliberate work-in-progress from prior Codex/Claude passes.

## Project architecture

Stage flow:

- Stage list/load order is in `Assets/Scripts/Stages/StageDataManager.cs`.
- Official stage order currently includes `stone` after `debug(nature)` and before `mirror`.
- Runtime stage execution is in `Assets/Scripts/Stages/StageReader.cs`.
- `StageReader.Init()` loads runtime media, enemy visuals, stage-specific bullet buffers, schedules BGM, resolves `bulletSpawners`, and sorts spawn events.
- `StageReader.UpdateStage()` advances from scheduled DSP/audio time when BGM exists, then emits enemies and bullet buffers at scheduled times.

BulletBuffer flow:

- Read `Docs/BulletBufferContext.md` before editing BulletBuffer JSON.
- BulletBuffer JSONs live under `Assets/BulletBuffers/<stage>/`.
- `BulletBufferManager` loads the JSON buffers and maps `name` to clip indices.
- Stage spawner `clipName` must match the JSON top-level `name`.
- Play area is `32 x 18`, bottom-left `(0,0)`, top-right `(32,18)`.
- Stage spawner angles are degrees; JSON polar angles are radians.

Rendering:

- `Assets/Scripts/Bullets/BulletRenderSystem.cs` builds GPU instance data.
- Recent local change skips the 0.1s disappear fade for bullet type `stone_block` to avoid stone tiles vanishing during state transitions.
- This is targeted but broad for every `stone_block`; check non-stone uses before generalizing.

UI:

- `Assets/Scripts/UI/StageBox.cs` and related stage select UI were recently changed so the Japanese stage name `石工` remains visible and centered.

## Stone stage

Main files:

- `Assets/StageData/stone/stone.json`
- `Assets/StageData/stone/stone.m4a`
- `Assets/StageData/stone/stone.mp4`
- `Assets/StageData/stone/Visuals/`
- `Assets/BulletBuffers/stone/`
- `Assets/Scripts/Bullets/BulletTypes/stone_block/`
- `Assets/Scripts/Bullets/BulletTypes/stone_conveyor_belt/`
- `Assets/Scripts/Bullets/BulletTypes/stone_warning/`
- `Assets/Editor/StoneStageDebugMenu.cs`

Stage facts from `stone.json`:

- Stage name: `石工`
- BPM: `144`
- Beat length: about `0.4166667s`
- Enemy visuals: `stone`, `golem`
- Enemy spawners: `2`
- Bullet spawners: `50`

Important timing windows:

- `5.416667` to `8.75`: first warning/spawn/drop sequence
- `8.75`: bottom conveyor and first all-at-once tile drop/settle
- `9.833333`: first conveyor flow
- `10.416667` to `13.75`: second warning/spawn/drop sequence
- `14.833333`: second conveyor flow
- `19.166667` to `22.916667`: first random rain + belt transition
- `25.833334` to `29.583333`: second random rain + belt transition
- `33.75`: `Clear`

Debug menu:

- `Tools/Bullet Hell/Debug/Start Stone Stage`
- Enter Play Mode first; this menu no longer enters Play Mode automatically.
- It starts `石工`, hides selection canvases, and resets the stage clock with a shorter debug BGM lead time.
- `Tools/Bullet Hell/Debug/Dump Stone Debug State` logs manager/stage/active bullet state.

## Gemini/Oracle review result

A Unity Recorder capture was made:

- `Recordings/stone_first_test_20260701_1446.mp4`
- `25.13s`, `1280x720`, `30fps`, H.264 + AAC stereo 48kHz

Gemini via Oracle succeeded using local Oracle `0.15.0`:

- Output: `Recordings/stone_first_gemini_advice.md`
- Session: `.oracle-output/sessions/stone-stage-video-review/`
- The CLI finished the Gemini response but then failed desktop notification with `spawn EPERM`; the answer was already saved.

Gemini's main advice:

- Conveyor motion feels too linear. Try beat-stepped or pulsed conveyor motion rather than constant velocity.
- Warning-to-impact windows should be quantized to exact beat intervals.
- Large warning grids at about `00:06` and `00:10` are hard to read; stagger or animate warning urgency.
- Random rain near `00:20` should preserve warning time but make the actual fall faster/punchier.
- Add impact feedback for stone weight: short scale squash/stretch and optional dust/debris.
- Use non-linear fall easing, e.g. hang briefly then accelerate.
- Consider driving conveyor phase from current song beat rather than pure time-linear movement.

## Verification workflow

Unity:

1. Check UnityMCP/resources first: editor state, project info, console.
2. If UnityMCP was explicitly required and cannot connect, stop and report that. Do not silently replace it with a file-only workflow.
3. Enter Play Mode.
4. Wait until `GManager.Control != null && GManager.Control.ready`.
5. Run `Tools/Bullet Hell/Debug/Start Stone Stage`.
6. Inspect Game View or record with Unity Recorder.
7. Stop Play Mode when done.
8. Check Unity Console for errors/warnings.

Recorder:

- Unity Recorder package installed: `com.unity.recorder@5.1.6`.
- Recorder captures Unity internal audio, not Windows desktop mix.
- Project audio speaker mode was checked as `Stereo / 48000`.
- For movie recording, use MP4/H.264 with `Include Audio`; audio requires Mono/Stereo.
- If using Oracle/Gemini video input, keep clips short and prefer 720p/30fps.
- `RecorderControllerSettings.CapFrameRate` must stay `true`: with it off, game time
  outruns the realtime BGM clock and every beat-timed event desyncs in the recording
  (observed as a 133s realtime run producing an 806s video).
- Recording auto-stops a few seconds after the stage content ends (enemy bullet count
  stays 0 after bullets were seen). Debug-started stages never leave `GameState.Playing`
  and the BGM clip (150s) is longer than the stone content (~83s), so neither of those
  is a usable end signal.
- Gemini video review can silently answer without the attachment (it then says it
  cannot watch videos). Put an instruction in the prompt to first confirm the video is
  viewable by describing the opening seconds, and retry if that proof is missing.

JSON sanity check:

```powershell
Get-ChildItem Assets\BulletBuffers\stone\*.json | ForEach-Object {
  Get-Content -Raw $_.FullName | ConvertFrom-Json | Out-Null
}
Get-Content -Raw Assets\StageData\stone\stone.json | ConvertFrom-Json | Out-Null
```

## Oracle setup for this PC

Do not use bare `npx -y @steipete/oracle` on this PC for serious work; it fetched older Oracle `0.9.0` in this session.

Use the local Windows wrapper:

```powershell
$env:ORACLE_HOME_DIR = "D:\Unity\Bullet-Hell\.oracle-output"
$env:ORACLE_MAX_FILE_SIZE_BYTES = "104857600"
& "D:\codex-projects\oracle\oracle.ps1" --version
```

Expected version during this handoff: `0.15.0`.

The successful Gemini video command shape was:

```powershell
$env:ORACLE_HOME_DIR = "D:\Unity\Bullet-Hell\.oracle-output"
$env:ORACLE_MAX_FILE_SIZE_BYTES = "104857600"
& "D:\codex-projects\oracle\oracle.ps1" `
  --engine browser `
  --model gemini-3-pro `
  --browser-attachments always `
  --remote-chrome 127.0.0.1:9222 `
  --browser-manual-login `
  --browser-manual-login-profile-dir "C:\Users\hitos\.oracle\browser-profile" `
  --browser-input-timeout 120000 `
  --slug "stone-stage-video-review" `
  --timeout 15m `
  --no-notify `
  --write-output "Recordings/stone_first_gemini_advice.md" `
  --force `
  --file "Recordings/stone_first_test_20260701_1446.mp4" `
  --file "Docs/stone-stage-handoff.md" `
  --file "Assets/StageData/stone/stone.json" `
  --file "Assets/Scripts/Bullets/BulletRenderSystem.cs" `
  -p "<prompt>"
```

Use `--no-notify` to avoid the desktop notification `spawn EPERM` seen after the successful Gemini response.

## Claude Code Oracle MCP status

Question: can Claude Code use Oracle?

Answer: yes in principle. Oracle includes an `oracle-mcp` stdio server with a `consult` tool and shared session store.

Setup performed for this project:

- Claude Code CLI installed: `2.1.193`
- Local MCP entry added with name `oracle`
- It launches Node 24 directly:
  - `D:\codex-projects\oracle\.oracle-tool\node_modules\node\bin\node.exe`
  - `D:\codex-projects\oracle\.oracle-tool\node_modules\@steipete\oracle\dist\bin\oracle-mcp.js`
- Env:
  - `ORACLE_ENGINE=browser`
  - `ORACLE_HOME_DIR=D:\Unity\Bullet-Hell\.oracle-output`
  - `ORACLE_BROWSER_PROFILE_DIR=C:\Users\hitos\.oracle\browser-profile`

Direct stdio smoke tests succeeded for:

- `initialize`
- `tools/list`
- `resources/list`
- `ping`

However, `claude mcp get oracle` and `claude mcp list` still reported `Failed to connect` during this handoff. This may be a Claude Code health-check/config issue rather than an Oracle MCP server issue, because the server responds correctly when driven directly. Do not assume Claude Code can call the `oracle` MCP tool until this health check is resolved in a subscribed/testable Claude Code session.

The user noted this PC is not yet paid/subscribed for the intended Claude Code use, so model-level Claude Code tests were not performed here.

## Current risks and next recommended work

1. Resolve Claude Code MCP health check for `oracle` before relying on Claude-to-Oracle delegation.
2. Decide whether to commit the Unity Recorder package changes. They are required for future video-based review.
3. Decide whether `Assets/Screenshots/` and `Docs/stone-stage-handoff.md` should be committed. Prior handoff says screenshots/recovery files were intentionally left out, but the doc itself may now be useful.
4. Improve stone stage timing using Gemini advice:
   - quantize warning/drop/impact windows,
   - make conveyor motion beat-pulsed,
   - add impact feedback/easing,
   - keep tile persistence stable after landing and before conveyor movement.
5. Re-record a short 720p/30fps clip after each timing pass and compare visually.
6. Before committing, run:
   - JSON parse checks above,
   - Unity Console check,
   - Play Mode stone-stage smoke test via debug menu,
   - optional Unity Recorder clip for visual regression.

## Commit hygiene

- Avoid committing `.oracle-output/` and `Recordings/`.
- Avoid committing `Assets/_Recovery/` and incidental screenshots unless the user asks.
- Preserve user/other-agent dirty changes; do not reset or checkout files.
- If committing package changes, include both `Packages/manifest.json` and `Packages/packages-lock.json`.
- If committing stage timing changes, group stage JSON/BulletBuffer changes with a concise Japanese or English commit message that states the visual/timing behavior changed.
