# Bullet-Hell プロジェクト指示書

Unity `6000.3.9f1` の弾幕ゲーム。現在の主対象は石工ステージで、音ハメ、タイル落下/着地、コンベア移動、動画レビューの流れが重要。

## セッション開始時に読むもの

- `Docs/claude-code-handoff.md` — 全体の引き継ぎ。作成日付が古い場合、リポジトリの現状と差分がある前提で読む
- `Docs/stone-stage-handoff.md` — 石工ステージの詳細
- BulletBuffer JSON を編集する前に、必ず `Docs/BulletBufferContext.md`

## モデル別の実装方針

Fable メイン運用時のサブエージェント切り出し方針、Opus メイン時の挙動は D: ドライブ共通ルール（`D:\CLAUDE.md`）に移動した。そちらを参照。

## 不変のルール

### 座標・データ仕様

- プレイエリアは `32 x 18`、左下 `(0,0)`、右上 `(32,18)`
- ステージ側 spawner の角度は度、BulletBuffer JSON の極座標角はラジアン
- spawner の `clipName` は JSON トップレベルの `name` と一致させる

### Unity MCP

- 変更系の呼び出しと Play Mode への突入を並列に行わない（UnityMCP が不安定になる）
- スクリプト編集後は、コンパイル完了とエラー有無を確認してから次の作業に進む
- 石工ステージの動作確認は、先に Play Mode に入ってから `Tools/Bullet Hell/Debug/Start Stone Stage` を実行する
- 状態確認には `Tools/Bullet Hell/Debug/Dump Stone Debug State` が使える

### Git

- 作業開始時に必ず `git status -sb`。ワークツリーには意図的な未コミット変更が混在していることが多い
- dirty なファイルを勝手に revert しない（過去セッションの仕掛かり作業の可能性がある）
- staging は狭く、対象ファイルだけを add する。`git add .` は使わない
- `Assets/Screenshots/`、`Assets/_Recovery/`、`Recordings/`、`.oracle-output/` はコミットしない
- 大きな変更の前には、基準点としてローカル commit を作ってよい
- push・公開・force push はユーザー確認を取ってから

### 検証

- 実装後は説明で終えず、コンパイル → Play Mode → デバッグメニューで実挙動を確認する
- 音ハメ・演出系の変更は、Unity Recorder で録画し Oracle/Gemini の動画レビューにかける流れがある
- 検証できなかった項目は、未検証であることを明記する

## 報告スタイル

- 日本語で報告する。技術名・ファイル名・コマンドは英語のまま
- 最後に、変更点・検証結果・未解決点を短くまとめる
