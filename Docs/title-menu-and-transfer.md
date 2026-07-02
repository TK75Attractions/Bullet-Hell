# タイトルメニュー+プレイ履歴引き継ぎ 設計

目的: 文化祭展示(ゲームセンター的な短時間プレイ)で、リピーターが**引き継ぎコード**でプレイ履歴を持ち越せるようにする。タイトルに「スタート」「設定」「引き継ぎ」メニューを追加する。

## 現状(調査済み)

- タイトル: `GManager.Update` の `GameState.Title` 分岐で「任意ボタン → ChoosingStage」直行(`GManager.cs:211-230`)。`TitleManager` は演出のみ
- 設定画面: `Canvases/StageCanvas/OptionScreen` + `OptionMenu`。`GManager.SetPaused` がプレイ中ポーズとして開閉
- プレイ履歴: **存在しない**。「Clear」スポナー(index -3)は弾消去のみ(`StageReader.cs:223-227`)

## 1. プレイ履歴(新設: `Assets/Scripts/Managers/PlayHistory.cs`)

静的クラス+PlayerPrefs 永続化(キー `playHistory.v1`、JSON)。

データモデル(ステージは `stageDirectoryName` で識別、最大8ステージ想定):
```json
{ "v": 1, "stages": { "stone": { "p": 3, "c": 1 }, ... } }
```
- `p` = プレイ回数(0〜15でクランプ)、`c` = クリア回数(0〜15)

記録フック(最小限の追記):
- プレイ開始: `GManager.GoGameAsync` 成功時に `PlayHistory.RecordPlay(stageDirectoryName)`
- クリア: `StageReader.UpdateStage` の index -3(Clear)発火時に `PlayHistory.RecordClear(...)`。同一プレイで複数回 Clear が来ても1回だけ記録(ステージ Init でフラグリセット)

## 2. 引き継ぎコード

サーバ無し・コードそのものがセーブデータ。**Crockford Base32**(I/L/O/U 除外、大文字)で人が書き写しやすい形式。

エンコード(ビット列):
- version 4bit(=1)
- ステージ8枠 × (p 4bit + c 4bit) = 64bit(枠順は `StageDataManager` のステージ順。存在しない枠は0)
- CRC8(多項式 0x07、version+データに対して)8bit
- 計 76bit → Base32 で 16文字 → 表示 `XXXX-XXXX-XXXX-XXXX`

デコード: ハイフン/空白除去 → 大文字化 → 紛らわしい文字の正規化(I→1, L→1, O→0)→ Base32 復号 → CRC 検証 → 失敗時は「コードが正しくありません」

実装: `PlayHistory.ExportCode()` / `PlayHistory.TryImportCode(string, out error)`。コード適用は既存履歴を**置き換え**(マージしない。シンプル優先)

## 3. タイトルメニュー UI

`TitleManager` に縦メニュー3項目を追加(Prompt「PRESS ANY BUTTON」を置き換え):
- **スタート** / **設定** / **引き継ぎ**
- 上下キーで選択(選択中はシアン+ビートパルス、非選択は暗色)、決定キーで実行
- スタート: 従来のフロー(Dismiss → ChoosingStage)
- 設定: OptionScreen を開く(`optionScreenObj.SetActive(true) + optionMenu.Open()`。タイトルでは `Time.timeScale` は触らない)。閉じる(ESC/戻る)でタイトルへ復帰(`PlayReturnEntrance` 活用可)。OptionMenu 内の「プレイを終了」等プレイ専用項目はタイトル起動時は非表示にできれば理想、困難なら現状のままで可(押されたらタイトルへ戻す)
- 引き継ぎ: 専用パネル(ランタイム生成で可、タイトルと同トーンの黒+シアン+濃紺):
  - 上段「あなたの引き継ぎコード」: `ExportCode()` を大きな等幅表示(履歴ゼロなら「まだプレイ履歴がありません」)
  - 下段「コードを入力」: TMP_InputField(キーボード入力、自動大文字化・ハイフン自由)+ 適用ボタン。成功→「引き継ぎました(プレイ n 回 / クリア m 回)」、失敗→エラーメッセージ
  - ESC/戻る でタイトルメニューへ
- GManager の Title 分岐を「メニュー操作へ委譲」に変更(titleArmed のガードは維持)。設定/引き継ぎ表示中はメニュー入力を止める

## 4. 入力

`IManager` の up/down/button/back を使用(キー実態は IManager を参照)。引き継ぎパネルのコード入力中はゲーム側キー処理を抑止(InputField フォーカス中ガード)。

## 5. 検証

1. コンパイル+コンソールエラー0
2. Play Mode: タイトル→スタート→従来どおりステージ選択
3. タイトル→設定→開閉してタイトル復帰
4. タイトル→引き継ぎ→コード表示。石工を1プレイ+Clear 到達後、コードが変化。コードをコピー→PlayerPrefs 消去(execute_code)→コード入力→履歴復元を確認
5. 不正コード入力でエラー表示
