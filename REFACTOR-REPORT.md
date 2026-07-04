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
| 消滅境界は x<0 ではなく **x<-2 ‖ y<-2**(CullingMargin=2)。~~上/右は「遠方でのみ」~~ **→ ラウンド3で精密化: 生存域は [-2, 36)²**(§8.2) | `BulletDataUpdateJob.cs:12,145` |
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

1. ~~**stage.json 側 enemy 構造(orbit/bulletClip/bulletChangeClips)が無検証**(コンパイラは verbatim 通過)。typeName 不正や存在しないフィールド(playerInfluence 等)を書いても誰も気づかない。→ ValidateStageLinks 系に enemy 内 typeName 解決チェックを足すのが次の最小ステップ~~ **→ ラウンド2で解消(§7)**
2. ~~**pattern イベントの静的リンターがない**(patternType の存在・positions の域内・fallBeats 符号は実行時 drop 頼み)。→ StageChartCompiler のコンパイル時警告に昇格する余地~~ **→ ラウンド2で解消(§7)**
3. **originPos 域外・appearTime>life 等が warning 止まり**。約500件の既存 advisory を棚卸しして「意図的(画面外スポーン等)」を仕分けしないと error 昇格できない。半日仕事
4. **startPos ≠ (startX, poly(startX)) の整合検査は未実装**(ドキュメント推奨止まり)。多項式ゼロの弾が大半のため、非ゼロ多項式に限定すれば低ノイズで入る見込み
5. レーザーバッファのフィールド再解釈(appearTime=幅等)に特化した妥当性検査はない(§4 の isLaser 抑止で誤検知だけ排除した状態)
6. known-unresolved-links.tsv の既知債務(ステージ25の文字化け名)は P1 以来据え置きのまま

## 6. 遵守事項の確認

- 凍結リスト(OPUS-HANDOFF §5)非接触・ゲームデータ/ランタイム非接触・挙動不変
- push 禁止遵守(origin より 88 コミット先行、push はユーザー確認待ち)
- dirty ファイルなし・`git add` は対象ファイルのみ・`.claude/worktrees` の残骸3件は不触で残置

## 7. ラウンド2(2026-07-04 深夜): stage.json 構造の静的検証

対象コミット: `7f0654b` → `cc4a136` → `9ba7a3f`(いずれも挙動不変・独立 revert 可)。§5 の優先度 1・2 を解消した続きのラウンド。実装は Opus サブエージェント3本に切り出し、設計・実測・監査・検証は Fable メインセッションで実施。

### 7.1 実測(着手前・チェック強度の根拠)

- enemy 構造があるのは **captain(6 spawner)+ stone(3 spawner)のみ**(全7ステージ走査)
- **orbit.typeName は実データ全9件で空** — enemy の軌道は typeId を使わないため空が正常。「空=error」にすると全滅するので不問とする
- 発射条件は `count>0 && bulletCount>0 && bulletClip.number>0`(MultiBullet.cs:91-93 / QuadOrder.cs:608-611 現物確認)。bulletChangeClips は発射された弾にのみ適用
- **captain の全6 spawner に `bulletInterval` キーが実在するが、DTO(EnemySpawnerJson)に該当フィールドが無い** — runtime は `bulletEmitTime / bulletCount` で再計算するため、この JSON キーを編集しても何も起きない(§5-1 で予言した事故クラスの実物)
- patternEvents の実使用は pattern_demo の5件のみ(5登録タイプ各1)。positions 全て域内・beats 全て非負・shardType/cutterType は未指定(既定値運用)。他5ステージは patternEvents キー自体が無い
- Patterns.cs は固定型名を9箇所リテラル直書き(stone_shard×2 / stone_cutter / stone_block / stone_warning×2 / stone_dust / stone_burst×2)。6種全て現 BTDB に実在(16/18/19/21/22/23)
- 不正 typeName の実行時挙動: `GetTypeId` が warning ログ+`-1` を返すのみ(BulletTypeDataBase.cs:88)。pattern 側は3つの失敗が全てサイレント(空 patternType は Normalize で drop、未登録は Expand=false、未解決 emission は typeId<0 フィルタで除去)

### 7.2 実施内容(コミット別・各々独立)

#### `7f0654b` リンター: enemy 構造の typeName 解決チェック

- `StageValidation.ValidateStageEnemyTypeNames`: 全 `StageData/<dir>/<dir>.json` を probe 不要で静的検査。発射される弾(clip/changeClip)の空・未解決 typeName=**error**、休眠 spawner の typo=warn、orbit の空=不問(上記実測より)。`bulletCount>0 なのに number=0` の死に設定も warn
- JsonUtility のサブセットミラー DTO で「runtime が見るものだけを見る」。`EnumerateStageJsonFiles()` を公開(後続チェックが再利用)
- `StageEnemyLinkTests` 7本: 実データ緑(error/warn とも 0)+合成 JSON で全分岐の発火を実証

#### `cc4a136` リンター: enemy 構造の未知キー検出

- `StageValidation.ValidateStageEnemySchema`: enemySpawners サブツリーの全キーを、**StageDataManager の実 DTO 4種からリフレクションで得た許可集合**と照合(ハードコピーしないためスキーマ変更に自動追従)。パースは Newtonsoft(未知キーを観測するため。JsonUtility では原理的に不可能)
- JsonUtility が黙って捨てるキー=死にデータを **warn** で可視化。playerInfluence/warpCooldown には「buffer 専用フィールド」の専用ヒント。animation 木は対象外
- **実データの真陽性6件**(captain の bulletInterval)を新規に可視化。`StageEnemySchemaTests` の実データテストは「全 warning が bulletInterval を含む」ラチェット(新たな死にキーの混入はテスト失敗になる)
- captain 側データの修正(chart 再生成 or キー除去)はデータ接触になるため本ラウンドでは見送り

#### `9ba7a3f` リンター: pattern イベントの静的検証+PatternDefaults

- `StageValidation.ValidateStagePatternEvents`: 未登録 patternType=**error**、explicit な shardType/cutterType 未解決=**error**(いずれも実行時はサイレント消滅のため)、空 patternType/負 time/positions 域外/負 beats(warn/hold/fall/ghost)=warn
- `PatternDefaults` 新設(PatternData.cs): パターンが常に解決する固定型名6種を Patterns.cs のリテラル9箇所から const に集約(挙動不変の置換)。pattern イベントを使うステージでは `RequiredTypeNames` 6種全ての BTDB 存在を error 検査 — 型アセットの削除/リネームで演出が黙って消える事故を静的検出
- `StagePatternLintTests` 7本: 実データ緑+合成 JSON で全分岐の発火を実証

### 7.3 検証結果

- **EditMode: 55→62→67→74、各コミット時点で全緑**。golden 6ステージ・chart パリティ(110イベント)は runtime を触った `9ba7a3f`(const 置換)後も不変=挙動不変を機械確認
- **Validate All Stages: 0 error / 531 warn**。増分 +6 は captain bulletInterval の真陽性のみで、typeName/pattern チェックの追加ノイズは 0
- 前回 §4 の記録(524)との差分 1 件は [Types]/[Buffer] 側の環境ドリフト: prefix 内訳([Types]32 + [Buffer]492 + [Link]1 + [Enemy]0 + [EnemySchema]6 + [Pattern]0)で本ラウンドの diff 由来でないことを確認
- コンパイルエラーなし(各コミット前に refresh+console 確認)。Play Mode: 該当なし(runtime 変更は const 置換のみで golden が同一性を保証)。Oracle レビュー: 該当なし(視覚成果物なし)

### 7.4 残ギャップ(§5 の更新)

1. §5-1(enemy typeName)・§5-2(pattern リンター)は本ラウンドで解消。§5-3〜6 は据え置き
2. ~~**新規**: captain の bulletInterval 死にキー6件の実データ修正(chart 再生成 or キー除去)。warn+ラチェットで固定済みなので急がない~~ **→ `4915c94` で解消(キー除去+ラチェットをゼロに強化)**
3. ~~**新規**: `visualId` → 同 JSON 内 enemyVisuals の対応検査は未実装(enemy の見た目リンク)。~~ **→ ラウンド3で解消(§8.1)**。`enemyName` → EDB 解決は probe が要るため未実装のまま
4. **新規**: 未知キー検査は enemySpawners サブツリーのみ。トップレベル(MusicEvents 等)と animation 木は対象外
5. **新規**: chart 側(compile 前)の同等検査は無し — stage.json は生成物なので生成後検査で実害は覆えているが、コンパイル時に落とせれば作者へのフィードバックがより早い

## 8. ラウンド3(2026-07-04 深夜〜早朝): visualId リンク検査+advisory 524件の棚卸し

対象コミット: `6484271`(挙動不変・独立 revert 可)+ 本レポート/ドキュメント追記。§7.4-3 の解消と §5-3(advisory 棚卸し)の第一歩。

### 8.1 `6484271` リンター: visualId → enemyVisuals リンク検査

- `StageValidation.ValidateStageEnemyVisuals`: 全 stage.json を probe 不要で静的検査。severity はランタイムのサイレント失敗実測に準拠:
  - `Boss.ResolveVisualSet` は visualId のカタログミスを**無言で** enemyName visual → EDB スプライトへフォールバック(Boss.cs:84-90)→ 発射 spawner(count>0)の未解決/never-loads visualId = **error**、休眠 spawner = warn
  - カタログは `visualsById[id] = visual` の後勝ち上書き(EnemyVisualCatalog.cs:19)→ 登録 id 重複 = **error**
  - 定義側の根本原因(blank id / addressable の address 空 / 未知 source / 未参照の死にデータ / GIF クリップ欠落・ファイル不在)= warn。登録ゲートは `EnemyVisualLoader.LoadCatalogAsync` の実装(blank id skip / externalGif は常に登録 / addressable は address 必須)をミラー
  - GIF パス解決は `ResolveExternalPath / ResolveExternalBaseDirectory` と同一規則(rooted 優先 → basePath → stage フォルダ)
- 実測: enemyVisuals を持つのは stone(externalGif×2, クリップ13本全て実在)と captain(addressable×1)のみで、実データは error/warn ともゼロ
- `StageVisualLintTests` 12本: 実データ緑1本+合成 JSON negative 11本で全分岐の発火を実証

### 8.2 advisory 524件の棚卸し(分類と昇格候補リストまで。昇格は未実施)

メッセージ正規化で全524件を分類した(集計は Temp/lint-warnings.txt、再現は §8.2 の手順どおり)。

**[Buffer] originPos 域外 = 492件(94%)— 静的単体では error 昇格不可(構造的理由)**

- JSON の originPos は **clip ローカル座標**。実行時は `world = spawnerPos + Rotate(originPos, spawner角度)`(BulletBufferManager.cs:668 → BulletData.cs:167)で平行移動+回転されるため、生の originPos を world 境界と比較しても真陽性/偽陽性を区別できない
- 参照実測: 492件のうち **466件は stage.json の bulletSpawners から参照される現役バッファ**(captain 208 / stone 217 / mirror 27 / debug(nature) 14)、**26件は未参照**(debug/HexagonClockwise 12・debug/LightMagic 6・_archive 8)
- **生存域の精密化(コード+実測)**: 弾が生き残る条件は `position ∈ [-2, 36)²`。左/下は CullingMargin=2、右/上は Morton グリッド(separateLevel=6 → 64セル × cellSize=0.5625 = 36)。カルは **appearTime 後の毎フレーム**のみ適用(BulletDataUpdateJob.cs:36-58,63-67,143-153)。§2 の表の「上/右は遠方でのみ」は不正確だった(是正済み)
- **合成実測(bulletSpawners は pos/angle が静的なので合成可能)**: 全ステージ×全 bulletSpawner×全弾 = 7,977 スポーン実体のうち **172 発が生存域外 = 出現前に即死**:
  - `shellsplash`(captain)126発: スプラッシュ左裾が x<-2。可視域(x∈[0,32])外を垂直上昇するだけの弾のため**視覚影響なし(良性の無駄データ)**
  - `石工ベルトダッシュ`(stone)45発 = 9発×5 spawner: x=38〜70 起点の「右から流れ込む」弾が**全滅。Play Mode 実測で確認済みの実バグ** — sp[4](t=10.83 発射)の age=2.72 時点の生存弾を逆算すると起点は 14/18/22/26/30/34 のみで、38 以降は一度も出現しない(Temp/belt-probe2.txt)。授権意図(life 6.35 秒間ベルトが右から補充され続ける)に対し、実際は約 0.4 秒で補充が止まり右から涸れる
  - `mirror_LASER_SUB`(mirror)1発
- 未参照26件は昇格対象外(スコープ分離が正道)

**[Types] = 32件**

- 圧縮テクスチャ 30件(15 BulletType × base/mask): renderer は Uncompressed 前提。修正は import settings 変更=視覚に触るため、視覚差分レビュー付きの別タスク
- index 8 の空 typeName + baseSprite 無し 2件: BTDB の穴。typeId=index のため除去は全後続 id のシフトを伴い危険。JSON から参照不能なだけの穴として据え置き(文書化のみ)

**error 昇格候補リスト(優先度順・本ラウンドでは実施しない)**

1. **新設「合成スポーン位置検査」**: bulletSpawners(pos/angle 静的)× クリップ originPos の合成位置が [-2,36)² 外なら warn(新規チェック。既存492件の originPos advisory の実質的な後継)。laser クリップ(カル経路が別)と enemySpawner 経由(オービット依存で emitPos が動的)は対象外。真陽性172件を新規可視化でき、既存の生 originPos 警告はノイズとして廃止候補になる
2. **石工ベルトダッシュのデータ修正**: x=38〜70 の9発を x<36 に収める(間隔詰め)か、授権意図どおり長時間ベルトにするなら別の実現手段(originVlc 起点を域内に+appearTime ずらし等)。stone.chart.json 側の authoring 判断が要るためユーザー確認案件
3. **未参照バッファ26件のスコープ分離**(debug/HexagonClockwise・LightMagic・_archive): per-stage ローダー対象かの確認の上で lint 対象から除外し、advisory を 492→466 に削減
4. **圧縮テクスチャ30件の import 修正**(視覚差分レビュー付き別タスク)

### 8.3 検証結果

- **EditMode: 74 → 86 で全緑**(新規12本を含む。golden 6ステージ・chart パリティ不変 = 挙動不変を機械確認)
- **Validate All Stages: 0 error / 525 warn** = ラウンド2の 531 − captain bulletInterval 6件(`4915c94` で掃除)。prefix 内訳 [Buffer]492 + [Types]32 + [Link]1、**新設 [Visual] は実データ 0 件**(追加ノイズなし)
- コンパイルエラーなし。Play Mode: belt dash の実測検証に使用(§8.2。update フックは自己解除・録画なし・終了後 stop 済み)
- Oracle レビュー: 該当なし(視覚成果物なし。棚卸しは数値実測で確定)

### 8.4 残ギャップ(§7.4 の更新)

1. §7.4-2(bulletInterval データ掃除)は `4915c94`、§7.4-3 前半(visualId 検査)は本ラウンドで解消。§7.4-4・5 は据え置き
2. ~~**次の最有力**: §8.2 の昇格候補 1(合成スポーン位置検査)。実装素材(生存域・合成式・除外条件・期待真陽性172件)は §8.2 に全て揃っている~~ **→ ラウンド4で解消(§9)。実測で 172 のうち1件が homing 由来の誤検知と判明し、真陽性は 171 に精緻化**
3. **ユーザー判断待ち**: 石工ベルトダッシュの授権意図の確認とデータ修正(§8.2 候補2)
4. `enemyName` → EDB 解決検査(probe 要)、startPos 整合検査(§5-4)、laser 特化検査(§5-5)は未実装のまま

## 9. ラウンド4(2026-07-04 早朝): 合成スポーン位置検査

対象コミット: `98492b6`(挙動不変・独立 revert 可)+ 本レポート/PROGRESS 追記。§8.2 の昇格候補 1 を実装し、advisory 棚卸しの数値実測を「出現前に即死する弾」を world 座標で名指しする静的チェックに昇格させた。

### 9.1 実測でチェック強度を確定(着手前)

`execute_code` で全公式ステージの bulletSpawners × クリップ弾を合成し(`world = spawnerPos + Rotate(originPos, angle deg→rad)`、BulletData.cs:167 と同一の Rotate)、生存域 [-2,36)² 外を数えた:

- **除外なし = 172 / laser 除外 = 172 / laser+homing 除外 = 171**。内訳は shellsplash(captain)126 + 石工ベルトダッシュ(stone)45 + mirror_LASER_SUB(mirror)1
- §8.2 の 172 を正確に再現。ただし **172 件目の mirror_LASER_SUB は homing 弾**(`isLaser=false, homing=true`。名前に LASER を含むが laser ではない)で、5 spawner から参照され、域外は spawner pos=(28,12)・originPos=(10,0) の1発だけ = 静的角度0で world=(38,12)(x≥36)
- **この1件は homing 由来の誤検知**と判定: homing は発射時に角度をプレイヤー方向へ再計算する(BulletBufferManager.GetBulletClip の homing 分岐)。|originPos|=10 で spawner (28,12) は生存域内(box 最近点まで距離0)なので、半径10の円は生存域と交差する = プレイヤーが画面内にいる通常時この弾は域内にスポーンする。静的角度での域外判定は実挙動と乖離する
- したがって正しい設計は **laser・enemySpawner(emitPos 動的)と同じ理由で homing(角度動的)も除外**し、真陽性 **171件**・誤検知ゼロにすること。§8.2 の 172 からの意図的な精緻化で、根拠を上記実測で確定した

### 9.2 実施内容(`98492b6`)

- `StageValidation.ValidateStageSpawnPositions(stages, report)`: probe 依存(`ValidateStageLinks` と同型。buffer をロードして originPos と homing/laser フラグを読む)。全 bulletSpawner について clip を解決し、非 laser・非 homing の弾ごとに合成位置を計算、生存域外なら **warn**
- 純粋な幾何コアを分離してテスト可能に: `ComposeSpawnWorldPosition(pos, angleDeg, originPos)`(実行時の Rotate と一致)、`IsInsideSurvivalRegion(world)`(GetTreeNum のカル境界のミラー:左下 CullingMargin=2、右上 Morton グリッド separateLevel6→64セル×cellSize0.5625=36)、`CheckSpawnPositions(...)`(合成 originPos 列を受けて warn を積む)。probe 無しで detector の発火を実証できる
- **severity は warn のみ**(error にしない): shellsplash 126 は可視域外を上昇する意図的な演出裾(良性の死にデータ)、belt dash 45 は実バグ(x=38〜70 起点)で、両者が混在するため advisory が正しい。既存の生 originPos advisory(clip ローカル座標)の world 座標版の後継
- **除外**: enemySpawners(オービット原点が動的)/ laser(カル経路が別+フィールド再解釈)/ homing(角度動的)/ 未解決 clip(ValidateStageLinks 担当)。base 角度のみ合成(angleInterval のファンアウトは展開しない = 常に発射される k=0 の弾を検査。base が域外なら必ず実発射されるため偽陽性は増えない)
- `StageLinterMenu` の probe ブロックに結線。`StageSpawnPositionLintTests` 6本: 実データ ratchet 1本(0 error / 171 warn・全 warn が既知2クリップ限定・件数固定)+ 合成幾何 negative 5本(域外検出・域内通過・回転適用・並進+回転の合成値・境界の下側包含/上側排他)

### 9.3 検証結果

- **EditMode: 86 → 92 で全緑**(新規6本含む)。golden 6ステージ・chart パリティは同スイート内で緑 = 挙動不変を機械確認(Editor/Tests のみの変更・runtime/データ非接触)
- **Validate All Stages: 0 error / 696 warn** = ラウンド3の 525 + 新設 [Spawn] 171。prefix 内訳 [Buffer]492 + [Types]32 + [Link]1 + [Spawn]171。既存プレフィックスは不変 = 追加ノイズは 171 の真陽性のみ、誤検知ゼロ
- コンパイルエラーなし。Play Mode: 該当なし(棚卸しの数値実測は §9.1 の execute_code で確定)。Oracle レビュー: 該当なし(視覚成果物なし)

### 9.4 残ギャップ(§8.4 の更新)

1. §8.2 候補1(合成スポーン位置検査)は本ラウンドで解消。**既存の生 originPos advisory 492件の廃止/スコープ縮小は未実施**: [Spawn] と重複するが、生 advisory は未参照バッファ26件・enemy/laser/homing クリップも拾う(粗いが広い)カバレッジを持つため、差分追加優先で本ラウンドでは両立させた。廃止するなら未参照・enemy 経由のカバー手段を別途用意してからが安全
2. **ユーザー判断待ち(継続)**: 石工ベルトダッシュのデータ修正(§8.2 候補2)。[Spawn] が45件を world 座標で名指しするようになったので、修正後は ratchet の期待値(171→126)を golden と同様に更新する
3. **angleInterval ファンアウト未検査**: count>1 かつ angleInterval≠0 の spawner では k>0 の回転コピーは合成していない(base のみ)。base が域内でも回転コピーが域外に出るケースは false negative。必要なら Expand 相当の角度展開を足す余地
4. `enemyName` → EDB 解決検査(probe 要)、startPos 整合検査(§5-4)、laser 特化検査(§5-5)、未参照26件のスコープ分離、圧縮テクスチャ30件の import 修正は未実装のまま
