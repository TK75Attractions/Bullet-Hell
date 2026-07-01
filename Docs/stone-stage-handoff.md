# 引継ぎ書: Bullet-Hell 石工ステージ実装

## 現在の状態

- Repo: `D:\Unity\Bullet-Hell`
- Branch: `marron/claude-codex`
- Remote: `https://github.com/TK75Attractions/Bullet-Hell.git`
- Pushed commit: `455851c Implement stone stage and timing tools`
- Push 済み: `origin/marron/claude-codex`

PR は未作成。`gh` CLI が未インストールだったため。

## 重要なユーザールール

ユーザーが MCP / UnityMCP 連携を明示的に頼んだ場合、接続できなければ代替手段を探さず、接続できない旨を伝えて止まること。

UnityMCP は不安定になることがある。並列呼び出しは避け、単発で使う。Play Mode に入れたら最後に必ず止める。

## 未コミット・未追跡ファイル

以下は意図的に commit/push から外してある。

- `Assets/Screenshots/`
- `Assets/Screenshots.meta`
- `Assets/_Recovery/0 (5).unity` から `0 (9).unity`
- 各 `_Recovery` の `.meta`

スクショは確認用。Recovery は Unity 自動復旧ファイル。基本的に PR には入れない。

## 実装済みの主な内容

### 石工ステージ

5番目のステージとして `石工` を実装。曲は `Assets/StageData/stone/stone.m4a`。

主なファイル:

- `Assets/StageData/stone/stone.json`
- `Assets/StageData/stone/Visuals/`
- `Assets/BulletBuffers/stone/`
- `Assets/Scripts/Bullets/BulletTypes/stone_block/`
- `Assets/Scripts/Bullets/BulletTypes/stone_conveyor_belt/`
- `Assets/Scripts/Bullets/BulletTypes/stone_warning/`

攻撃の範囲:

- `00:06` 前後: 予告 -> タイル出現 -> 一斉落下 -> ベルトコンベアで左流し x2
- `00:20` 前後: ランダム落下 -> ベルトコンベアで左流し x2
- `00:34` 以降は基本未実装。余計な弾幕は出さない方針。

最近の修正:

- タイルの落下前後で一瞬消える問題を修正
- `life` が切替時刻ぴったりだと `BulletRenderSystem` の 0.1 秒フェードアウトで消えるため、石工用 BulletBuffer に 0.12 秒程度の表示ガードを入れた
- 着地・ベルト移行もフェード抜けしないよう調整済み

### ステージ選択 UI

対象ファイル:

- `Assets/Scripts/UI/StageBox.cs`

日本語ステージ名が中央に表示されない / 消える問題を修正。

現在の方針:

- StageName の RectTransform を中央固定 `520x92`
- TMP auto-size 有効
- alpha を 1 固定
- Play Mode で `石工` 表示確認済み

### デバッグ補助

対象ファイル:

- `Assets/Editor/StoneStageDebugMenu.cs`

石工ステージ確認用。以前 UnityMCP 経由のメニュー実行で StageReader 時刻が怪しい挙動を見せたことがあるので、信用しすぎないこと。

### 指示用タイミングツール

音楽を流しながらキーフレームとコメントを打つための簡易 Web ツール。

対象ファイル:

- `Tools/stone-timing-editor/index.html`
- `Tools/stone-timing-editor/DESIGN-figma.md`

## 検証済み

直近で確認したこと:

- `StageBox.cs` UnityMCP script validation: エラーなし
- Unity refresh/compile: 完了
- Unity console: C# / JSON 由来のエラーなし
- `Assets/BulletBuffers/stone/*.json`: JSON parse OK
- Play Mode 上でステージ選択の `石工` が表示されることを確認
- Play Mode は停止済み

Unity Console に動画の `Color primaries 1 is unknown or unsupported by WindowsMediaFoundation` が出ることがあるが、今回の変更由来ではない。

## 注意点・次に見るべきところ

ユーザーの主な関心は、石工ステージの見た目と音ハメ精度。

優先して見るなら:

1. `00:06` からの予告 -> 出現 -> 一斉落下 -> コンベア流しの音ハメ
2. `00:20` 以降のランダム落下の自然さ
3. コンベア速度・表示タイミング
4. タイルが左端まで行く前に消えないか
5. 石工本体が途中で消えないか
6. ステージ選択の日本語が崩れていないか

ユーザーは細部をかなり見ているので、スクショだけで「できている」と言わず、実際の Play Mode / Game View で確認してから報告すること。

## 実行環境の癖

PowerShell がたまに `CreateProcessAsUserW failed: 5` で失敗する。読み書きや JSON 確認は `node_repl` が安定していた。

ファイル編集は原則 `apply_patch`。JSON の機械的な一括修正は Node REPL で実施していた。

UnityMCP は一度切れても戻ることがあるが、ユーザーが明示的に UnityMCP を頼んでいる場面で接続不可なら、代替せず止めること。
