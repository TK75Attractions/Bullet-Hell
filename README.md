# Bullet-Hell

Unity 6 (URP 2D) の弾幕ゲーム試作です。  
大量弾を `Job/Burst + DrawMeshInstancedIndirect` で更新・描画します。

## 現在の実装メモ

- タイトル画面
  - 背景: 青みがかった黒
  - 中央: ロゴ（Image）をビート風にバウンス
  - 背景演出: 半透明の図形（四角 / 三角 / ひし形）がゆっくりスクロール
  - フラッシュ: ビートに合わせて白フラッシュ
  - 下部テキスト: `ボタンを押してスタート`
  - 任意キー入力でステージ選択へ遷移

- ステージ選択
  - 上下キーでステージ選択ボックスをスクロール

## 主要ファイル

- タイトルUI/ステージ選択UI: `Assets/Scripts/UI/UIManager.cs`
- 入力: `Assets/Scripts/Managers/InputManager.cs`
- 全体ゲームループ: `Assets/Scripts/Managers/GManager.cs`

## タイトル演出の調整ポイント（Inspector）

`UIManager` の以下を調整すると見た目が変わります。

- `Logo Sprite`
- `Title Background Color`
- `Prompt Color`
- `Bpm`
- `Logo Bounce Amount`
- `Flash Alpha`
- `Shape Count`
- `Shape Size Range`
- `Shape Speed Range`

## 共同編集ルール（簡易）

- タイトル演出ロジックは `UIManager.cs` に集約
- 入力仕様は `InputManager.cs` に集約
- ゲーム状態遷移（Title / ChoosingStage / Playing）は `GManager.cs` を基準に変更
