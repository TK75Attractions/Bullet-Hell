# Bullet-Hell — 子セッション共通ルール

Unity `6000.3.9f1` の弾幕ゲーム。音ハメ・タイル落下/着地・コンベア移動・動画レビューの反復で作る。

## セッション開始時に読むもの（この順）

1. 本書（絶対ルール）と `Instructions/OPUS-PLAYBOOK.md`（デザインの正・実装パターン・便の組み方）
2. `Docs/OPUS-DEV-KNOWLEDGE.md` — 罠・検証テンプレ・ユーザー感覚値の一次資料
3. `Instructions/REVIEW-NOTES.md` の未処理 `- [ ]` — **最優先の作業対象**。処理したら `[x]` 化し対応コミット hash を書き添える
4. デザインを触るなら `Docs/result-design-language.md`（スタイル値の正）、ステージ制作は `Docs/stage-authoring-guide.md`

CLAUDE.md には古いタスク指示を書かない。個別指示は便の Goal と REVIEW-NOTES.md が唯一のキュー。

## 完了報告の証拠ゲート（最重要）

- **エディタ内の単フレームスクショと「コンパイルが通った」は検証ではない**。実プレイ（Play Mode 通し or 録画）の該当フレームで確認する
- 指摘項目ごとに録画から該当秒の実フレームを抽出し Before/After を並置してから「直った」と言う。主張は証拠形式で（「t=37.30 で○○を確認」）。**1項目でも未確認なら「未確認」と明記**する
- 曖昧な要求（「シンプルに」等）は全量を作り込む前に試作フレーム1枚で方向確認

## 弾データ不変（golden 検証）

- 弾幕は `chart.json → コンパイル → stage.json + BulletBuffer JSON` のデータ駆動。`stage.json` は生成物（直接編集しない）。JSON を編集する前に `Docs/BulletBufferContext.md`
- `Assets/Tests/EditMode/GoldenScheduleTest.cs` が公式ステージのバッファをロック。**UI・演出・リファクタ便は golden 完全不変が合格条件**。弾を意図的に変える便は「意図したバッファのみ再生成・count/sha 差分を報告」
- 弾を触ったら4段検証: Validate All Stages 0 error → EditMode 全緑 → golden 意図差分のみ → BOM/CRLF を byte 検査
- `color.w=0` 規約（marron/ハンマー/カッター/warn_box はスプライト色そのまま）をシェーダで壊すと弾が不可視化する

## 確立済みデザイン言語（勝手に崩さない・詳細は OPUS-PLAYBOOK.md §1）

- **額装①案**: Playing 中のみズームアウトしフィールドを額縁 PlayFrame で遮蔽。**弾データ・論理座標・rect・golden 完全不変、diff は .cs のみ**でUI改修を通すのが流儀
- **HUD 上部集約**（104px 帯）: 左=被弾→スコア / 中央=曲進捗バー（主）/ 右=曲名。曲名だけ大きく明るくしない
- **平行四辺形ボタン**: 斜辺・スラッシュは全て同一角 **19°**（平行が生命線）。触ったら 1080p 実フレームで角度実測
- **難易度色の維持**: 様式（銀枠・19°スラッシュ）は統一、ベース色だけ EASY=紺/NORMAL=青/LUNATIC=暗赤

## Unity MCP・検証・録画

- 変更系呼び出しと Play Mode 突入を並列にしない。スクリプト編集後はコンパイル完了とエラー有無を確認してから進む
- 石工の動作確認は Play Mode に入ってから `Tools/Bullet Hell/Debug/Start Stone Stage`。状態は `Dump Stone Debug State`
- **背景ジョブ/完了通知待ちをしない（同期実行）**。録画・ビルドは自前ポーリングで完了まで見届ける。重い EditMode 等の前に仮コミット（Unity クラッシュ実績）
- 録画は実装済み範囲全体を音声付きで `Recordings/` に、**stage 時計オーバーレイ（t=XX.XX）を焼き込む**

## リポジトリ・Discord

- 方針「**ゲームが動くのに必要なファイルだけ push**」（.gitignore に日付コメント）。`Instructions/`・`Tools/`・`Docs/`・`*.md`・`PROGRESS.md`・`Recordings/`・`Assets/Screenshots/` は非コミット＝ローカル台帳
- 日本語コミット・プレフィックス方式（`石工:` `リザルト画面:` 等）・意味単位・`git add .` 禁止で狭く add。**push はユーザー承認制**
- 作業開始時に `git status -sb`。dirty ファイルを勝手に revert しない（Fonts の SDF 等はセッション由来でない）。`git reset --hard` 禁止
- **Discord 投稿は `D:\claude\night-runner\v2\scripts\post-discord.ps1` のみ**。独自スクリプト作成・`env:DISCORD_WEBHOOK_URL` 参照は禁止。**秘密情報の値は表示・保存・ログ出力しない**
- oracle は夜間便では使わない（ユーザーのレビューループ優先）。session limit 時は記録してキリよく終了
