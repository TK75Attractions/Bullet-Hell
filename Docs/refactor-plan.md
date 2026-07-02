# 弾幕制作効率化リファクタリング計画

作成: 2026-07-02 / 対象コミット: 8886a99 / **計画のみ・実装未着手**

## 0. 復帰管理(確保済み)

- タグ **`pre-refactor-20260702`** とブランチ **`backup/pre-refactor-20260702`** が現状(8886a99、全作業コミット済み)を指す
- 戻し方: 閲覧は `git checkout backup/pre-refactor-20260702`、作業ブランチの巻き戻しは `git reset --hard pre-refactor-20260702`(実行前にユーザー確認)
- 実装時は **フェーズごとに専用ブランチ+完了時マージ**。各フェーズ開始時にもタグを打つ(`refactor-p1-start` 等)
- 挙動同一性は「ゴールデンマスター」(P0参照)で機械的に確認してからマージする

## 1. 目的

石工ステージ制作(2026-07-02)で実測された工数を削減する:

| 痛点(実測) | 現状 | 目標 |
|---|---|---|
| リタイム(指示書の秒変更) | 全スポナー時刻+バッファ内 life/appearTime を手再計算(専任エージェント1回分) | マーカー値の書き換えのみ(再コンパイル) |
| 1ギミック追加 | warn/spawn/drop/settle/dust/flash の**約6ファイル**を規約頼みで整合 | パターン1エントリ+パラメータ |
| 検証1回 | 実時間再生のみ(約90秒/周) | 任意時刻シークで数秒 |
| 弾タイプ追加 | .asset+DB手動登録+並列配列+テクスチャ設定の地雷(isReadable事故が実際に発生) | フォルダに置くだけ(自動登録+検証) |
| データ不整合 | clipName↔name の突合をその都度スクリプトで実施 | エディタ内リンター常設 |

## 2. 非目標(やらないこと)

- Burst ジョブ/GPU インスタンシング等**ホットパスの再設計**(現性能で十分。触るのは所有構造の整理のみ)
- ゲームプレイ仕様・見た目の変更(挙動同一が原則。P5の難易度実効化のみ新機能)
- 旧ステージ(25/captain/mirror)の作り直し(動くものは動くまま。新レイヤは併存)
- 入力機構(COM3シリアル)の変更(筐体依存のため凍結)

## 3. 現状アーキテクチャの要点(調査 2026-07-02)

- 経路: `GManager.GoGameAsync` → `StageReader.Init/UpdateStage`(dspTime同期・単調増加のみ) → `QuadOrder`(1374行、弾/敵/レーザー/衝突/描画データの一極集中) → Burst Jobs → `BulletRenderSystem`
- **index結合が最脆弱**: ①MultiBullet三重並列リスト(`multiBullets`/`multiBulletOrbitBullets`/`bossDisplays` が同一i前提) ②`BulletTypeDataBase` の types/bVerts/bPower 並列配列=typeId ③`MultiBullet.BulletChache`/`BulletEvent` が enemyBullets の生indexを保持(`ClearManagedEnemyDanmaku` 後に不整合し得る) ④warpZone の `zoneIndex^1` ペア ⑤`CounterBullet.TypeId=5` ハードコード
- マジック値: `"Clear"`→`-3`、`ScreenNoiseTypeId=-1000`、`"stone_block"` 文字列比較でフェードスキップ、度/ラジアン混在、`grazeRange=10` を距離二乗と比較(実効√10)
- データ: バッファ登録キーは JSON内 `name`(ファイル名と別)。stone は 480 ファイル中**約387が未参照**。25 の一部バッファ name に文字化け疑い
- 難易度: `selectedDifficulty` は**書くだけで誰も読まない**(完全に飾り)
- テスト0件・asmdef 0件(単一 Assembly-CSharp)。シーク機構なし(先頭リセットのみ)

## 4. フェーズ計画

### P0: 安全網とテスト土台(挙動変更なし)

1. **ゴールデンマスター採取ツール**(Editor): stage.json+バッファ群 → 展開後スポーンイベント列(時刻/クリップ/弾数/位置/寿命)を決定的にダンプ(JSON)。全6ステージ分を `Tests/Golden/` に保存
2. **asmdef 分割**: `BulletHell.Core`(Scripts) / `BulletHell.Editor` / `BulletHell.Tests(EditMode)`。コンパイル時間短縮とテスト受け皿
3. **EditMode テスト最初の一式**: ゴールデン一致テスト、BulletBuffer JSONスキーマ検証、clipName↔name 突合、BulletTypeDataBase 整合(型数/テクスチャ解決/verts)
4. **ステージリンター**(メニュー常設): 上記検証+単位・範囲の警告(座標が32x18外、負lifeなど)

成果物基準: `Tools/Bullet Hell/Validate All Stages` が全緑。以降の全フェーズはこの緑を維持したままマージ。
規模: 小〜中。リスク: 低(製品コード無変更)。

### P1: 危険箇所の無害化(挙動同一)

1. マジック値の型化: `"Clear"` → スポナーの `kind` enum(後方互換で文字列も受理)、`-3`/`-1000` 定数化、`CounterBullet.TypeId` を DB から解決、`"stone_block"` → BulletType に `skipDisappearFade` フラグ
2. **BulletTypeDataBase 自動化**: フォルダスキャンで types 自動登録、bVerts/bPower は SO から常時導出(並列配列の serialize 廃止)。テクスチャインポート検証(isReadable/サイズ)をリンターに追加。壊れた index8 空エントリの除去(ゴールデンで typeId 変化がないことを確認、変わる場合は互換マップ)
3. **MultiBullet 三重リスト → 単一 `EnemyEntry` 構造**(orbit index への参照は1箇所に集約)。enemyBullets 生index保持 → 世代カウンタ付きハンドル
4. 単位の境界明示: 公開フィールド/引数名を `angleDeg`/`angleRad` に改名(JSONキーは互換維持)、変換は境界1箇所に
5. 棚卸し: 未参照バッファを `Assets/BulletBuffers/_archive/`(非ロード対象)へ移動(削除しない)、25 の name 文字化けを調査・修正、死コード削除(`BulletEvent` 死コード部・`BulletObjectPool`・`NewMonoBehaviourScript`)
6. `QuadOrder` の**責務分割(移譲のみ)**: 衝突・レーザー・敵管理を partial/サブクラスへ切り出し(データ所有は不変。API 据え置き)

成果物基準: ゴールデン完全一致+全ステージ実プレイのスクショ監査一致。
規模: 中。リスク: 中(index結合の触り換え。ハンドル化は特に慎重に)。

### P2: ステージ制作レイヤ「StageChart」(核心)

**設計方針: ランタイムは変えず、その上に“コンパイルされる楽譜”を載せる。**

1. **StageChart フォーマット**(JSON/YAML、拍ドメイン):
   - ヘッダ: BPM/拍子/オフセット、**名前付きマーカー**(timing-editor の出力そのまま)
   - イベント時刻は `4:1`(小節:拍)/`marker.M20`/`marker.M20 - 1beat` 等の式で記述
   - 寿命は `until: marker.M16` / `for: 2beat` の相対指定(絶対秒の手計算を根絶)
   - 座標グループ(名前付き位置セット)と参照
2. **StageChartCompiler**(Editorのみ): StageChart → 既存の stage.json + BulletBuffer JSON 群を生成。**ランタイム無改修**なので互換リスクが最小。コンパイル時にリンター実行
3. **timing-editor 連携**: 「AI用指示保存」に加え「StageChart雛形を保存」を追加(マーカー→named markers)
4. **実証**: 石工ステージを StageChart に逆移植し、コンパイル結果がゴールデンと一致(または差分が説明可能)であることを確認。以後 stone は StageChart が正、生成物はビルド成果物扱い

規模: 大。リスク: 低〜中(生成物比較で担保できる)。**弾幕制作の工数削減の本丸。**

### P3: パターンライブラリ

StageChart から呼べるパラメータ化ジェネレータ(コンパイラ内蔵、C#):

- `FallingBlock`(warn→spawn→drop→settle→dust→flash を1定義に。落下時間=1拍固定の重力逆算を内蔵)
- `CutterSweep`(ゴースト予告+直進+粉砕連鎖)、`RadialBurst`(n方向+重力+フラッシュ段数)、`GhostPreview` 修飾子、`BeatPulseWarn`
- 石工で確立した実装知見(appearTime衝突無効・verts空=無害・拍ジャスト着地)をパターン側に封じ込め、**規約をコードに昇格**
- カタログ文書と最小サンプル(新メンバーが timing-editor → StageChart → パターン参照で1曲書ける状態)

規模: 中(パターン1個ずつ追加可能)。リスク: 低。

### P4: 検証イテレーション(シーク)

1. **ステージシーク**: 指定秒まで①スポーンイベントを消化済みに(bulletCount 頭出し)②BGM `timeSamples` を合わせ③「シーク時刻を跨いで生存する弾」をテンプレから再構成(appearTime/寿命残の再計算)。厳密再現が難しい弾(レーザー履歴等)は「弾なし頭出し」モードをまず提供(それだけで検証90秒→数秒)
2. デバッグメニュー汎用化: `Start Stage(任意)` / `Seek to…` / マーカー一覧からのジャンプ / スクショフック・Recorder 開始の標準メニュー化(今セッションで手作りした execute_code 群の製品化)
3. (任意)エディタ再生ウィンドウ: StageChart を Play Mode なしでプレビュー(弾のみシミュレート)。工数対効果を見て判断

規模: 中。リスク: 中(シークの再現精度はモード分けで管理)。

### P5: 難易度の実効化とボス演出タイムライン

1. **難易度オーバレイ**: StageChart にイベント/パラメータの難易度修飾(`difficulty: lunatic` ブロック、弾速・数の係数)。コンパイラが難易度別 stage.json を生成 → `selectedDifficulty` がついに読まれるようになる(StageDataManager のロード分岐を追加)
2. **ボスタイムライン統合**: アニメイベント+台詞(世界観の会話劇: 技師の飲み勝負等)を StageChart のマーカーに紐付け。`Clear` を正式イベント化
3. 引き継ぎコードのスロット順を stageDirectoryName 固定マップ化(難易度追加でズレないように)

規模: 中〜大。リスク: 中(新機能を含む唯一のフェーズ。最後に置く)。

## 5. 実施順序と依存

```
P0 ──▶ P1 ──▶ P2 ──▶ P3 ──▶ P5
              └───▶ P4(P2完了後ならいつでも)
```

- 各フェーズ完了時: リンター+ゴールデン+実プレイのスクショ監査 → コミット・タグ → 次フェーズへ
- 中断可能性: どのフェーズ境界でも中断・出荷可能(文化祭が近づいたら P2/P3 優先で打ち切り)

## 6. リスク一覧

| リスク | 影響 | 緩和 |
|---|---|---|
| typeId 再割当で既存JSONの typeName 解決が変化 | 弾の見た目/衝突が変わる | ゴールデン+互換マップ。P1で最も慎重に |
| ハンドル化(enemyBullets参照)の取りこぼし | 弾変化クリップの誤動作 | BulletChache/BulletEvent の全参照を先にテストで固定 |
| 25バッファの文字化け修正が挙動変更になる | 旧ステージの弾が変わる | 実行時マッチング実態を先に検証。一致していた物だけ直す |
| StageChart コンパイラのバグ | 生成データ破損 | 生成物は git 管理+ゴールデン比較。手書きJSONもいつでも併用可 |
| シークの再現不完全 | 誤った検証結論 | 「弾なし頭出し」を既定、完全再構成はモード明示 |
| 単一アセンブリ→asmdef 分割での参照エラー | 一時的ビルド不能 | P0 で独立実施、即日で緑に戻す |

## 7. 見積り(目安)

| フェーズ | 規模感 | 検証込みの目安 |
|---|---|---|
| P0 | 小〜中 | 半日 |
| P1 | 中 | 1日 |
| P2 | 大 | 1.5〜2日 |
| P3 | 中 | 1日(パターン4種) |
| P4 | 中 | 半日〜1日 |
| P5 | 中〜大 | 1日〜 |

(エージェント並列前提の実働目安。フェーズ単位で価値が出るため途中打ち切り可)
