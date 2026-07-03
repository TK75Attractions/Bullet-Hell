# PROGRESS

## 2026-07-03 夜(自律セッション・Opus ラウンド1やり直し: 仕掛かり確定+Task B 検証)

### 今回やったこと

前回(14:34 session limit で中断)以降の状態を精査し、推奨順 B→A→C を検証第一で進めた。

1. **仕掛かり状態の確定(中途半端な未コミットは無し)**
   - `git status` は完全に clean。チェックポイント `b4bc4ba` 以降に2コミット追加済み: `b4bc4ba`(dirty だった CLAUDE.md / LiberationSans SDF を確定)→ `28eb47c`(**Task C = NativeArray リーク修正**)
   - つまり **Task C は前セッション終盤に既に実装・コミット済み**。内容を精読して健全性を確認: `OnDestroy` の破棄を `DisposeNativeContainers()` に抽出し、`AwakeSetting` で `AssemblyReloadEvents.beforeAssemblyReload` に登録(二重登録防止フラグ `reloadDisposeHooked`)、`OnDestroy` でフック解除、`IsCreated` ガードで二重破棄も安全。すべて `#if UNITY_EDITOR`。差分は QuadOrder.cs 単一ファイル +34行。破棄漏れ経路(Play 中強制リロードで OnDestroy が飛ぶ)を正しく塞いでおり、ゲーム挙動は不変。→ **検証済み・破棄不要**
2. **Task B(ドット拡大 scale 0.45 の Oracle 再レビュー)= 検証タスクを完了**
   - Unity ブリッジが落ちていた(下記)ため、前セッションが 14:16 に撮影済みの `Assets/Screenshots/capture_stone_66.50.png` / `73.00.png`(いずれも scale 0.45・color 0.52/0.63/0.98 のコミット `40e3f81` より後の撮影 = 現状を反映)を使用
   - Oracle(browser、`browserModelStrategy:"ignore"` で現在選択中モデル使用)に2枚を添付レビュー。画像描写がスクショと一致し実視聴を確認
   - **結果: scale 0.45 は「合格」**(暗背景で危険予告として読める。四角枠の形・ドット間隔・角の閉じ、いずれも問題なし)。Oracle の任意推奨は **scale 0.48**(73.0s で左右の大きな丸ノコに視線を取られる際の周辺視野を考慮した安全寄り。0.50 以上は不要)
3. **Oracle 運用知見をメモリに反映**: `browserModelLabel:"High"` は MCP 経由で効かず config 既定の gpt-5.5-pro(Plus に無い Pro)を掴む。`browserModelStrategy:"ignore"`(または `current`)で現在選択中モデルをそのまま使うのが確実、と `feedback_oracle_chatgpt_model.md` に追記

### 検証結果

- **Unity ブリッジは本セッション中ずっと利用不可**。`mcpforunity://instances` は instance_count=0。Unity プロセスは3つ生存するが RAM が異常に低く(main 0.63GB)、14:34 のクラッシュ由来でハングと推測。**ユーザー離席中の kill/再起動は危険なので実施せず**(GUI 操作も不可)
- そのため Task B の「合格」判定は既存スクショ+Oracle 客観評価で確定できたが、コンパイル/EditMode/golden 再生成を要する変更は一切行っていない
- Task C は git 上のコミット済み実装をコード精読で確認(静的検証)。Play 中リロードの実挙動再検証は Unity 復帰後に可能

### 未解決と次の一手(Unity 復帰が前提)

- **Task B 任意フォローアップ(scale 0.45→0.48)**: Oracle 推奨の安全寄り微調整。実行レシピ(次セッションで数分):
  1. `Assets/BulletBuffers/stone/lower_burst_warn_1.json` と `lower_burst_warn_2.json` の scale 値 `0.45` を全て `0.48` に置換(各ファイル 64 ドット。`0.45` は scale の x/y にのみ出現し color/appearTime とは非重複なので安全に一括置換可)
  2. `Tools/Bullet Hell/Golden/Dump All Stages` → `git diff Tests/Golden/stone.golden.json` で対象2バッファの sha1 のみ変化を確認
  3. `run_tests`(EditMode)で 49/49 緑
  4. Play Mode → Start Stone Stage → Capture At Times 66.5/73.0s で拡大を目視 → 1コミット
  - **判断**: 0.45 で既に合格のため必須ではない。安全側に倒すなら実施。alpha/色は触らない(凍結リスト)
- **Task A(起動リングの因果明確化)**: 未着手。JSON+chart 変更→golden 再生成→テスト期待値更新→Play Mode 検証が必要で、Unity 必須のため本セッションでは着手せず(OPUS-HANDOFF §4-A に手順あり)
- Unity ブリッジ復帰の確認が最優先。復帰しない場合はユーザーに Unity 再起動を依頼

## 2026-07-03 昼過ぎ(自律セッション・Opus 引き継ぎ整備)

### 今回やったこと

Fable の使用制限が近いため、今後の自律作業を Opus(claude-opus-4-8)へ引き継ぐ準備に徹した(実装変更なし):

1. **`OPUS-HANDOFF.md` を新規作成**(リポジトリ直下)
   - 現状スナップショット(EditMode 49/49・origin より 30 コミット先行・dirty 2ファイル)
   - 標準検証テンプレ(コンパイル → EditMode → golden 再生成メニュー → Capture At Times → 録画+動画レビュー)と共通の戻し方
   - 残タスク A〜I を、対象ファイル・手順・検証・戻し方・難易度・リスク付きで整理。Opus 向き(★)は B(ドット拡大の Oracle 再レビュー)→ A(起動リングの因果明確化)→ C(NativeArray リーク)の順を推奨
   - 凍結リスト(appearBeatBaseAlpha、破片アルファ、カッター当たり判定、形態変化の演出方向、点線の色、push 禁止など)を表で明記
2. `Docs/claude-code-handoff.md` の冒頭に OPUS-HANDOFF.md への導線を追記

### 検証結果

- EditMode テスト 49/49 緑を本セッションでも再実行して確認(1.84s、失敗0)
- git 状態を実測: `marron/claude-codex` は origin より 30 コミット先行(引き継ぎ指示にあった「4件」は旧情報)。dirty は CLAUDE.md(+12行)と LiberationSans SDF asset の2件のみ
- ドキュメントのみの変更のため Play Mode 検証は該当なし

### 未解決と次の一手

- 残タスクの実体は OPUS-HANDOFF.md §4 に集約(このファイルの過去ラウンドの未解決欄はそちらに転記済み)
- 次セッション(Opus)は OPUS-HANDOFF.md の推奨順 B → A → C から着手するのが安全
- push はユーザー確認待ちのまま(30 コミット溜まっているので、朝の確認時に push 可否の判断を推奨)

## 2026-07-03 昼(自律セッション・改善継続ラウンド4: 破片寿命短縮+下部破裂予告の視認性向上)

### 今回やったこと

前ラウンドまでの Oracle/Gemini 指摘のうち、JSON のみで入る安全な演出改善2点(+Oracle 再レビューの反映1点)を実装:

1. **中央粉砕の破片(`shatter_shard.json`)の寿命を一律20%短縮**
   - 全54発の飛翔時間 1.2s → 0.96s(life = appearTime + 0.96 に更新)。65秒台の画面占有を緩和
   - Oracle 指摘「寿命 15〜25% 短縮案」の範囲内。アルファ変更は過去のユーザー指示(半透明表示をやめる)と干渉するため不採用のまま
2. **下部破裂予告の点線(`lower_burst_warn_1/2.json`)を約15%明るく**
   - color RGB 0.45/0.55/0.85 → 0.52/0.63/0.98(青系の色相・アルファは維持)
3. **点線ドットを 12.5% 拡大(scale 0.4 → 0.45)**
   - Oracle 画像レビューの新規提案「明るさを上げずにドットを 10〜15% 大きくすると視認性が上がる」を反映
- `Tests/Golden/stone.golden.json` を再生成(対象3バッファの sha1 のみ更新、count 不変)

### 検証結果

- EditMode テスト 49/49 緑(変更前後2回とも)
- Play Mode(Start Stone Stage + Capture At Times)でステージ秒同期スクショ検証:
  - 64.3 / 65.5s: 破片は粉砕の因果が読める量を維持しつつ、収束時の密度が低下。Console エラーなし
  - 66.5 / 73.0s: 点線矩形の予告が明るく・大きくなり、暗背景ではっきり読める(scale 変更後に再撮影して確認)
- Oracle(gpt-5.5-pro、browser)画像レビュー(5枚、実視聴を冒頭 describe で確認):
  - 破片: 「20%短縮でちょうどよい。0.96s 維持、これ以上の短縮は不要」→ 現状値で確定
  - 点線の色: 「現状値が適正。上げる場合の上限は RGB 0.58/0.70/1.00」→ 色は据え置き
  - ドット拡大は Oracle 提案どおり反映(拡大後の再レビューは未実施)
- 音ハメへの影響なし(発生タイミングは未変更、見た目のみの差分)

### 未解決と次の一手(いずれもユーザー判断推奨)

- **予告のフェードイン明滅の底上げ**: Oracle 提案「フェード中の最低 alpha 0.2 → 0.3」は `BulletRenderSystem.appearBeatBaseAlpha`(全ステージの予告共通の定数)の変更になるため見送り。全体の見た目に効くのでユーザー判断向き
- **プレイヤーの視認性**(Oracle 優先度1): 65.5s 前後で破片・カッターと重なる。描画順の前面化+1px 暗色アウトライン案。描画コード変更が必要
- 前回からの持ち越し: カッターと中央プレイヤー域の重なり(当たり判定)、床消滅の演出強化(亀裂+画面揺れ)、カッターのビート同期、Play 中ドメインリロード時の NativeArray リーク

## 2026-07-03 朝(自律セッション・改善継続ラウンド3: シーク後 NRE ループの根本修正)

### 今回やったこと

前ラウンド未解決筆頭「シークで終端(73s〜)を跨ぐと GManager.Control が null になり PlayerController.Move が NRE ループ」を調査・修正:

1. **真因の特定(シークは無実)**
   - Play Mode で 6s→75s シーク、70s へ巻き戻して終端 80s(Clear イベント+ゴーレム寿命)を2回跨いでもエラーゼロ。シーク・終端跨ぎ単体では再現しない
   - 前セッションの Editor.log(113,378行目〜)を精査すると、NRE スパムの直前に「Reloading assemblies after forced synchronous recompile」= **Play Mode 中のドメインリロード**があった
   - 機序: リロードで static `GManager.Control` と非シリアライズ状態が消える一方、シリアライズされる `ready=true` / `state=Playing` は生き残る → `GManager.Update` が動き続け `PlayerController.Move` の `GManager.Control.IManager` が毎フレーム NRE
   - Play 中に `EditorUtility.RequestScriptReload()` を実行して同一の NRE ループを意図的に再現し、機序を確定
2. **修正(`GManager.cs` のみ、差分最小)**
   - `ready` を `[NonSerialized]` 化: リロード後は必ず false に戻り、Update/LateUpdate が安全に停止(NRE の連鎖を根元で遮断)
   - `OnEnable` で `Control == null` なら再ラッチ: エディタ拡張の null ガードが「未初期化」として正しく機能する
   - `ready=false` かつ `state != Title` を検出したら一度だけ警告ログ(「Play を止めてステージを再起動して」)を出す

### 検証結果

- コンパイルエラーなし、EditMode テスト 49/49 緑
- 修正後の Play Mode で再テスト: 石工ステージ起動 → 75s シーク → Play 中に強制ドメインリロード → **NRE ゼロ**、警告1行のみ、Control 再ラッチ・ready=false を確認
- 通常起動(Play → タイトル → ステージ開始 → シーク)への影響なしを同セッションで確認
- リロード時の「Leak Detected: Persistent allocates 21 allocations」は修正前から出ている既存事象(リロードで QuadOrder の NativeArray が Dispose されないため)。今回のスコープ外

### 未解決と次の一手

- 恒久対策の推奨: Unity の Preferences > General > Script Changes While Playing を「Recompile After Finished Playing」にすると Play 中リロード自体が起きなくなる(ユーザー環境設定のため未変更)
- Play 中ドメインリロード時の NativeArray リーク(既存)。実害は小さいがいつか OnApplicationQuit/リロード前 Dispose を検討
- 演出系(Gemini/Oracle 指摘の反映、カッターのビート同期、形態変化強化など)は引き続きユーザー判断待ち。今回は手を付けていない

## 2026-07-03 朝(自律セッション・改善継続ラウンド2: 録画→検証→動画レビューの完走)

### 今回やったこと

1. **前セッション切断後も回り続けていた録画を回収**
   - 07:52 開始の録画が Unity 内で走り続けていた(gameTime 309s、ステージ本編はとっくに終了)のをフォアグラウンドで停止・finalize(363.4s / 29.4MB / H.264+AAC 正常)
2. **CapFrameRate=true 修正(前セッションの仕掛かり)の効果を実測で確認**
   - 旧録画: 実時間 133s → 動画尺 805.9s(ゲーム時間が約6倍で走り全ビートがズレる)
   - 新録画: 実時間 365s → 動画尺 363.4s(比率 1.00)。音ズレの根本原因が解消
3. **本編 76s をトリムしてレビュー用クリップ作成**
   - `Recordings/stone_v6_capfix_20260703.mp4`(19MB)。音声レベル正常(mean -11dB / max 0dB)、フレーム目視で開始・終盤・終了後を確認
4. **Gemini(gemini-3-pro、oracle CLI browser + remote-chrome)動画レビューを完走**
   - 1回目は添付が無視され一般論だけ返る事故 → プロンプトに「視聴できた証拠として冒頭3秒を describe せよ」を追加して再実行、実視聴を確認(冒頭描写がフレームと一致)
   - 結果: **音と映像のズレ指摘ゼロ**。序盤のブロック着地は「BGMのキックに正確に合っている」、1:03 のドロップとゴーレム起動〜破壊の一致は「クライマックス感を見事に演出」と高評価
   - 改善指摘(優先度順、`Recordings/stone_v6_gemini_advice.md`): ①カッター粉砕と地面消滅の因果の明確化 ②カッター移動が等速で音ハメ不足 ③形態変化の演出強化 ④破壊エフェクトのビート同期 ⑤点線予告の細かいビート同期
5. **録画の自動停止をステージ内容終了に同期(再発防止)**
   - `StageRecorderMenu.cs`: 敵弾を一度観測した後、弾数 0 が 5 秒続いたら自動停止。debug 起動ステージは GameState が Playing のまま終わらず、BGM クリップ(150s)は本編(~83s)より長いため、既存の停止条件では止まらなかった
   - `Docs/claude-code-handoff.md` の Recorder 節に CapFrameRate 必須・auto-stop・Gemini 添付事故の知見を追記

### 検証結果

- コンパイルエラーなし(既存の obsolete 警告3件のみ)
- Play Mode 通し(Start Stone Stage + StartRecording)で auto-stop の実挙動を確認: 弾数 0(stageTime 83.3s)→ 5秒後に「Stage content finished; auto-stopping recording.」ログとともに停止、78.7s の正常な mp4 が finalize された
- 通し実行では Unity Console にエラーなし
- EditMode テストは未実行(今回の変更は Editor スクリプトとドキュメントのみで、ゲームコード・データは未変更)

### 未解決と次の一手

- **Gemini 指摘の反映はユーザー判断待ち**: 特に「カッターをビートに合わせてコマ送り移動」「地面をカッター通過順に崩壊」はコンテンツ再設計を伴う。「形態変化の強化」は「老人がゴーレムに重なって消える」という既存のユーザー指示と方向が異なるため要相談
- **シークで終端(73s〜)を跨ぐと GManager.Control が null になり PlayerController.Move が NRE ループ**(既存問題らしい。通し実行では発生しない。SeekTo 起因のクリア遷移の破損と推測)
- Recordings/ に検証用の中間 mp4(stone_20260703_*.mp4)と _frames/ が残っている。不要なら削除可(gitignore 済み)
- 前回からの持ち越し: 中央粉砕時のカッターとプレイヤー域の重なり、破片の寿命短縮、床消滅の演出強化、点線予告の視認性

## 2026-07-03 深夜(自律セッション・改善継続ラウンド)

### 今回やったこと

前回の未解決筆頭「残置ブロックの浮遊」(地面が 63.33s に消えた後、粉砕完了 65.3s まで最大約2秒ブロックが宙に浮く)を解消:

1. **地面(ベルト帯・窓2)の撤去をカッター粉砕完了に同期**
   - `stone_belt_bottom_2.json` の `life` 5.0→7.083333(消滅 63.33s→65.417s = beat 157)
   - カッターが左右から残置ブロックを地面の上で順次粉砕し、中央の最後のブロックが砕けた直後に地面が消える構成に変更。0.1s フェードは中央の破片バーストに隠れる
   - 浮遊時間はゼロになった(Oracle 提案の「前倒し」「沈下演出」より小差分で完全解消)
   - `Docs/stone-stage-design-v2.md` の窓2記述(36:1〜Clear)を現状に合わせて更新
2. **ゴーレム起動インパクト(前回 Oracle 提案の積み残し)**
   - 新バッファ `golem_core_ring.json`(石工起動リング): M21(63.33s)に胸コア (16,13.75) から赤バースト4枚(scale 1.2→2.0→2.8→3.4、0.05s 刻み、計約0.24s)。stone_burst 型で被弾なし
   - 4枚目(scale 3.4)は Oracle 再レビュー「やや弱い、最終スケールを 3.3〜3.5 に」を反映して追加
   - `stone.chart.json` に 39:1 のイベント追加。イベント数 104→105 に伴い `ChartCompileParityTest` の期待値を更新

### 検証結果

- EditMode テスト 49/49 緑(stone golden は意図した差分: belt_bottom_2 sha1 更新+起動リング追加+全クリップ idx シフト)
- Play Mode 通し(Start Stone Stage)+ Capture At Times で確認:
  - 63.6 / 64.5 / 65.1s: 地面あり、ブロックは床上で順次粉砕。浮遊なし
  - 65.35s: 中央最後のブロック粉砕の瞬間、地面まだあり
  - 65.45 / 65.7s: 地面消滅。破片雲とカッターがフェードを隠す
  - 63.38 / 63.46 / 63.50s: 起動リングが胸コアで赤く3段→4段ポップ(seek 検証含む)
- Oracle(GPT-5.5、browser)画像レビュー2回:
  - 「浮遊はほぼ解消。地面を粉砕完了まで残す今回の構成は前回案(M21 と同時に消す)より明確に良い。粉砕→床消滅→次攻撃の因果が読める」
  - 起動リングは4枚化で「確実に読ませる」推奨値に合わせ済み
- 音ハメは静的スクショのため未検証(地面消滅 65.417s は beat 157 に載せてある)

### 未解決と次の一手(Oracle 新規指摘、いずれもユーザー判断推奨)

- **中央粉砕時(65.3s 前後)のカッターとプレイヤー域の重なり**: 丸ノコの最終 Y を 0.3〜0.5 下げる or スケール 0.9 倍の案。当たり判定(難易度)に関わるため保留
- **破片の画面占有(65.1〜65.7s)**: 寿命 15〜25% 短縮案。ただし「破片の半透明をやめる」という過去のユーザー指示と干渉するためアルファ変更は不採用。寿命短縮のみ検討余地
- **床消滅時の演出強化**: 亀裂ライン+画面揺れ(0.05〜0.08s)案。新機能実装が必要
- **下部破裂予告の視認性**: 点線を 10〜20% 明るく+初回 0.12s ポップ案
- 音ハメの動的確認(Unity Recorder 録画 → 動画レビュー)は未実施

## 2026-07-03 未明(自律セッション)

### 今回やったこと

CLAUDE.md「短期的な指示」の未対応3件を実装(前夜 7b013f8 で他項目は対応済み):

1. **コンベア始動時のブロック二重表示を修正**
   - 原因: v2.3 でベルト流しを2拍前倒しした際、着地タイルの `life` が旧時刻のままで、静止タイルと移動タイルが約0.83秒重なっていた
   - 対応: 着地バッファ10ファイル(`stone_tile_settle_1/2`、`stone_rain_settle_1_a〜d`, `2_a〜d`)の `life` を「流し開始+0.04s」に短縮
2. **形態変化後の下の地面を撤去**
   - `stone_belt_bottom_2.json` の `life` 22→5.0。ベルト帯(窓2)は M21(63.33s、ゴーレム稼働)と同時に消滅
3. **形態変化をクロスフェード化(ゴーレムの上に老人が重なって消える)**
   - enemySpawner に `fadeInSec` / `fadeOutSec` / `sortingOrder` を新設(chart → StageChartCompiler → StageDataManager → EnemySpawner → Boss.cs、シーク起動も対応)
   - ゴーレム: M20(60s)に fadeInSec 1.0 で老人の背後(order 10)に出現、攻撃アニメは `atAbs`(M21 相対)化
   - 老人: life 56.4 に延長し 60.2〜61.6s に fadeOutSec 1.4 で手前(order 12)から消える
   - フェード秒数は Oracle(GPT-5.5 Pro)の画像レビューを反映(当初 3.33s → 貼り付いて見えるため 1.4s に短縮)
   - `Docs/stage-authoring-guide.md` に新フィールドを追記

コミット: `4eea28a`(本体)+ フェード調整コミット(このファイルと同時)。

### 検証結果

- EditMode テスト 49/49 緑(stone ゴールデンは意図した差分のため再生成してコミット)
- Play Mode 通し(Start Stone Stage)+ Capture At Times でステージ秒同期スクショ検証:
  - 17.9 / 24.2 / 30.9s: ベルト流し始動直後、二重表示なし(単一の列がきれいに流れる)
  - 60.5 / 61.2 / 62.0s: ゴーレムがフェードインし老人が手前で消えるクロスフェードを確認
  - 63.6 / 66.8s: 地面消滅、床なしで下部破裂・カッター攻撃が成立
- Unity Console エラーなし(既存の mp4 color primaries 警告のみ)
- 注意: 撮影アームがステージ開始に間に合わず 10.5/11.3s の2枚は実クロック15.7sの遅延撮影(異常ではない)。ベルト始動の証跡は 17.9s 以降で担保

### 未解決と次の一手

- **残置ブロックの浮遊**(Oracle 指摘・中程度): 63.33s に地面が消えた後、粉砕完了(65.3s)まで最大約2秒ブロックが宙に浮く。粉砕シーケンス(下部カッター+粉砕破片)の前倒し or ブロックの沈下演出で 0.5s 以内に短縮したい
- **地面の消え方**(Oracle 案A): 現在 0.1s フェードのみ。0.35s で沈み込む演出にするには per-type の disappearDuration 対応(BulletRenderSystem)が必要
- **63.33s の起動インパクト**(Oracle 提案): ゴーレム稼働の瞬間に胸コアから赤リング1〜2枚(scale 拡大は未サポートのため α 演出で代用)
- 音ハメ確認(スクショ検証は静的なため未検証)。必要なら Unity Recorder で録画 → Gemini/Oracle 動画レビュー
- CLAUDE.md の「短期的な指示」は全項目対応済みのはず。ユーザー確認後にセクション整理を推奨
