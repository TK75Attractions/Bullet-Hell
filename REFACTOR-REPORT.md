# REFACTOR-REPORT: 弾幕生成データ層の安全化(スキーマ明文化+不変条件検証)

作成: 2026-07-04(自律セッション・Fable「弾幕生成リファクタ・再開2」)
対象コミット: `759733a` → `92cbf8e` → `028c134` → `c8d3058`(いずれも挙動不変・独立 revert 可)

## 1. 目的と背景

大規模リファクタ P0〜P5(2026-07-02 完了、`Docs/refactor-plan.md`)で「StageChart+パターン+シーク+減算難易度」の制作フローと golden/EditMode テストの安全網は完成済み。本ラウンドの目的はその上に、**Opus(または任意の後続エージェント)が単体で BulletBuffer JSON / chart を正確・安全に編集できる**ようにする層を足すこと:

1. **スキーマ明文化** — ドキュメントとコード実態の乖離を現物突き合わせで是正
2. **バリデーションツール** — 過去に実際に起きた事故クラスを機械検出に昇格
3. **不変条件テスト** — 「現データが緑」と「検出器が本当に発火する」の両方を EditMode で固定

ゲームの挙動・データは一切変更していない(golden 完全一致を各ステップで維持)。

## 2. 現状棚卸し(2026-07-04 実測)

### 検証基盤(着手前)

- リンター `Tools/Bullet Hell/Validate All Stages` = `StageValidation` の3関数(バッファスキーマ/タイプDB/clipName リンク)。テストとリンターは同一実装を共有する設計
- EditMode テスト **49本**(golden 6ステージ・chart パリティ(イベント数110)・時刻式・難易度・パターン・ハンドル)
- ドキュメント: `Docs/BulletBufferContext.md`(弾道系は正確)、`Docs/stage-authoring-guide.md`(リファクタ後フロー)

### データ実測(Assets/BulletBuffers、626 JSON)

- ファイル形式は混在: **472 BOM+CRLF / 143 noBOM+CRLF / 10 BOM+LF / 1 noBOM+LF**。\r\r\n 破壊・改行混在・不正UTF-8・登録名重複・空 name は現状ゼロ
- `appearDuration > appearTime` は **343 弾**が実在する正常パターン(表示窓がクリップ発生時点で切られるだけ)— 警告対象にしてはならないことを確認
- 静的角度(polarForm.y / initialAngle)で 4π 超はゼロ = 度/ラジアン取り違え検出をノイズゼロで導入可能
- 既存リンター警告 525 件(originPos 域外 advisory 等。error は 0)

### 調査で確定したコード真実(ドキュメント未記載だったもの)

| 事実 | 根拠 |
|---|---|
| `appearDuration` は省略時 0、**負値のみ** DefaultAppearDuration=1.2 に暗黙置換 | `BulletDataJson.cs:65`, `BulletData.cs:9` |
| `appearDuration` は描画専用。当たり判定は常に `appearTime` 開始 | `BulletRenderSystem.cs:269-288`, `BulletCollisionJob.cs:37` |
| `color.w` は tint 強度。**w=0 はスプライト本来の色で表示**(透明ではない)。衝突は color を一切見ない | `BulletIndirectURP.shader:120-124` |
| 消滅境界は x<0 ではなく **x<-2 ‖ y<-2**(CullingMargin=2) | `BulletDataUpdateJob.cs:12,145` |
| `counterPower` は JSON フィールドではなく BulletType.verts の面積から自動算出(verts<3 で 0=無害) | `BulletType.cs:32-50`, `BulletCollisionJob.cs:48` |
| `polarForm.x=0` は回転ベクトルが 0 倍になり弾が originPos に潰れる(直進弾は {1, angleRad} 必須) | `BulletDataUpdateJob.cs:115` |
| **DTO が二系統**: stage.json 内 orbit/bulletClip の弾には `playerInfluence`/`warpCooldown` が存在しない | `StageDataManager.cs:35-95` |
| パーサも二系統: chart=Newtonsoft(コメント可)、buffer/stage.json=JsonUtility(厳密) | `StageChartCompiler.cs:7-8` |
| 登録名 = JSON `name`(空ならファイル名)。1ロードスコープ = built-in 5種 + common/ + debug/ + ステージフォルダ。重複は黙って置換 | `BulletBufferManager.cs:19,395-397,409-419` |

### 外部調査(Explore サブエージェント)の誤認を現物棄却した2件

- 「color.w=0 は完全透明で当たり判定だけ残る」→ **誤り**。シェーダは `lerp(spriteRGB, tint.rgb, mask×w)` で、w=0 はスプライト本来の色・形・αで描画される(過去コミット `404bebf` の実証とも一致)
- 「EditMode テストは48本」→ **誤り**。実行実測 49本(着手前)→ 55本(今回終了時)

## 3. 実施内容(コミット別・各々独立)

### `759733a` リンター: ファイル形式+名前レジストリの不変条件を新設

- `StageValidation.ValidateBufferFileFormat`: 不正UTF-8・\r\r\n(**2026-07-04 に実際に起きた** Git Bash text-mode 二重変換事故の署名)・裸CR・CRLF/LF混在を **error**。BOM有無・CRLFかLFかの流儀は既存混在を尊重して不問
- `StageValidation.ValidateBufferNames`: 実行時ロードスコープ単位の登録名重複を **error**(黙って置換される事故の防止)。ステージ→共通/built-in の shadowing と空 name フォールバックは warn。`_archive/` は per-stage ローダー対象外のため除外
- `ValidateBuffers` に advisory 2件: 負 appearDuration(暗黙 1.2 置換)、color 成分の 0..1 範囲外
- 既存誤検知の修正: isLaser バッファでは appearTime=ビーム幅のため `appearTime>life` 警告を抑止
- 新テスト `BufferFormatInvariantTests`(2本)をリンターと同一実装に結線

### `92cbf8e` スキーマ明文化: BulletBufferContext.md をコード準拠に増補

- 誤記是正(appearDuration 負値の置換・消滅境界 -2)、§5「Render and Collision Semantics」・§6「File Format and Registration Rules」を新設(§2 の表の内容+binary 編集レシピ)、typeName 一覧を現 BTDB 25種に更新、warpCooldown/lockRotation/size を追記、チェックリスト増補
- stage-authoring-guide.md: chart 編集の注意5点(at 必須/color 既定白/diffScale は pattern 専用/orbit 等はコンパイラ未検証/パーサ差)を追加

### `028c134` リンター: 度/ラジアン取り違え検出

- 静的角度(polarForm.y/initialAngle)が 4π 超で warn。速度系(thetaVlc/angleSpeed)は高速回転が正当のため対象外。現データ警告ゼロ

### `c8d3058` テスト: 検出能力の実証(negative test)

- バイト検査を `ValidateBufferBytes(rel, bytes, report)` に public 抽出し、合成データで「\r\r\n を検出する/混在を検出する/不正UTF-8 を検出する/クリーンな BOM+CRLF と LF は通す」の4本を追加

## 4. 検証結果

- **EditMode: 各コミット時点で全緑**(49→51→51→55。既存49本は全ステップ不変で緑)
- **golden 完全一致**: データ非接触のため GoldenScheduleTest(6ステージ)・ChartCompileParityTest(イベント数110)が毎回そのまま合格
- **Validate All Stages: 0 error / 524 warn**(着手前 525。新チェックの追加ノイズ 0 件、isLaser 誤検知の抑止で 1 件減)
- コンパイルエラーなし(各編集後に refresh+console 確認)
- Play Mode: 該当なし(ランタイムコード非接触。Editor/Tests/Docs のみの変更)
- Oracle レビュー: 該当なし(視覚成果物なし。コード/文書のみのため自己検証+テストで確定)

## 5. 残ギャップと次の一手(優先度順)

1. **stage.json 側 enemy 構造(orbit/bulletClip/bulletChangeClips)が無検証**(コンパイラは verbatim 通過)。typeName 不正や存在しないフィールド(playerInfluence 等)を書いても誰も気づかない。→ ValidateStageLinks 系に enemy 内 typeName 解決チェックを足すのが次の最小ステップ
2. **pattern イベントの静的リンターがない**(patternType の存在・positions の域内・fallBeats 符号は実行時 drop 頼み)。→ StageChartCompiler のコンパイル時警告に昇格する余地
3. **originPos 域外・appearTime>life 等が warning 止まり**。約500件の既存 advisory を棚卸しして「意図的(画面外スポーン等)」を仕分けしないと error 昇格できない。半日仕事
4. **startPos ≠ (startX, poly(startX)) の整合検査は未実装**(ドキュメント推奨止まり)。多項式ゼロの弾が大半のため、非ゼロ多項式に限定すれば低ノイズで入る見込み
5. レーザーバッファのフィールド再解釈(appearTime=幅等)に特化した妥当性検査はない(§4 の isLaser 抑止で誤検知だけ排除した状態)
6. known-unresolved-links.tsv の既知債務(ステージ25の文字化け名)は P1 以来据え置きのまま

## 6. 遵守事項の確認

- 凍結リスト(OPUS-HANDOFF §5)非接触・ゲームデータ/ランタイム非接触・挙動不変
- push 禁止遵守(origin より 88 コミット先行、push はユーザー確認待ち)
- dirty ファイルなし・`git add` は対象ファイルのみ・`.claude/worktrees` の残骸3件は不触で残置
