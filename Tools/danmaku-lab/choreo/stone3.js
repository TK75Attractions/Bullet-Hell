// choreo/stone3.js — 新・石工（stone3）v19
//
// ── v19 での変更（指示書のマーカー 9〜17 = 40.0〜50.8s の実装。〜39.6s は v18 と完全に同一）──
//   指示書: Instructions/石工/stage-timing-instructions_20260903_chain_v2.md（17 マーカー）
//   マーカー 1〜8（〜39.2s）と 39.583s の爆破 1 発は v18 のまま 1 つも触っていない。
//   40.0s 以降にあった v13b 由来の仮区間⑪⑫⑬⑭（横断シャベル 2 本 / 落下シャベル 5 本 /
//   タイル表示④ 8 拍 / 爆破④ 3 回）は、新しい内容と時間帯が丸ごと重なるので削除した。
//   仮区間⑮（53.333s〜 横断シャベルの往復）と⑯（56.667s〜 落下シャベル＋放射弾増量）は
//   マーカー 17 の終わり（50.833s）より後なので、そのまま残している（供給源の帯だけ差し替え）。
//
//   採用時刻（.tmp_v19/onset_markers.py・onset_derived.py。丸めルールは v17 と同じで、
//   16 分音符（0.1041667s）へ丸めたうえで ±80ms 以内に強いオンセット
//   ＝ 40〜52s のスペクトルフラックスの 95 パーセンタイル 59.6 を超える局所ピーク
//   があればそちらを優先する）:
//     #   | ラベル            | 指示書  | 16分丸め | 採用    | 根拠
//     ----+-------------------+---------+---------+---------+--------------------------
//     9   | 鎖予告①           | 40.036  | 40.0000 | 40.0080 | オンセット flux 100.8
//     10  | 鎖予告②           | 40.436  | 40.4167 | 40.4167 | 窓内最大 24.7 < 閾値 → 丸め
//     11  | 鎖攻撃            | 41.262  | 41.2500 | 41.2619 | オンセット flux 66.7
//     12  | タイル予告開始      | 42.587  | 42.6042 | 42.5099 | オンセット flux 81.1
//     13  | タイル①           | 43.394  | 43.4375 | 43.3459 | オンセット flux 78.5
//     14  | タイル②           | 43.949  | 43.9583 | 43.9670 | オンセット flux 69.2
//     15  | タイル爆破 x4      | 44.986  | 45.0000 | 45.0061 | オンセット flux 141.2（区間最大）
//     16  | 鎖予告①（3 回目）  | 47.255  | 47.2917 | 47.2990 | オンセット flux 62.5
//     16b | 鎖予告②           | (+0.261)| 47.6042 | 47.5080 | オンセット flux 95.9
//     16c | 鎖攻撃            | (+0.979)| 48.2292 | 48.3439 | オンセット flux 71.6
//     17  | タイル予告開始      | 49.338  | 49.3750 | 49.3750 | 窓内最大 34.1 < 閾値 → 丸め
//     17b | タイル①           | (+0.836)| 50.2083 | 50.2083 | 窓内最大 29.3 < 閾値 → 丸め
//     17c | タイル②           | (+1.457)| 50.8333 | 50.8333 | 窓内最大 23.4 < 閾値 → 丸め
//   16b/16c は指示書に時刻が無い（「また鎖予告→攻撃」の 1 行だけ）ので、1 回目の鎖
//   （33.3961 / 33.6573 / 34.3750）の相対間隔 +0.2612 / +0.9789 を写してから同じ丸めを当てた。
//   17b/17c も同様に 12→13→14 の相対間隔 +0.8360 / +1.4571 を写してある。
//
//   実装:
//     ・鎖攻撃 2 回目（9-11）と 3 回目（16）は 1 回目と同じ構造（予告 2 段階 → 下から上へ
//       這う 3 本・1 段 1.15 ユニット・16 段・1.500s）。中心列だけ変えた。
//         1 回目 列 2 / 7 / 12（v17 から不変） → 2 回目 列 4 / 9 / 14 → 3 回目 列 1 / 6 / 11
//       2 回目は上下の帯（マーカー 5-8 で積んだ bandC）を、3 回目は上下＋左右の帯
//       （bandC ＋ マーカー 13/14 の bandD）を掃くので、どちらも残留タイルの破壊が起きる。
//     ・マーカー 12-14 … 予告 → タイル①② の出現ポップ（マーカー 4-8 と同じ作り）。
//       積む先は **左右の帯**（列 0-1 / 14-15）にして、上下（bandC）と合わせて外周が揃う。
//     ・マーカー 15 … 45.0061s に残留タイル 4 枚を同時爆破。blastPhase の 4 辺めぐりで
//       左 → 上 → 右 → 下 を 1 枚ずつ選ぶので、帯の左右上下に散る。1 拍前から点滅
//       （blinkWarn）、半拍前にリング予告、爆破と同時に放射弾。
//     ・マーカー 17 … 3 回目の鎖で砕けたセルとその 4 近傍を優先して外周へ積み直す
//       （マーカー 5-8 の補充と同じ作り）。50.833s 以降は次の仮区間⑮まで静止。
//
// ── v18 での変更（色だけ。弾の配置・時刻・当たり判定は v17 と完全に同じ）──
//   ユーザーが gpt-image-2 で作ったタイルテクスチャを stone3_tile のスプライトに採用し、
//   他のスプライトと予告色をその色相へ合わせた。
//     ・スプライト: Tools/gen_stone3_pixel.py が .tmp_tex/5d98ed95….png を 64 ドットへ縮小し、
//       面 (77,67,93) / 縁明 (116,102,134) / 縁暗 (55,48,67) / 欠片 (40,34,48) の 4 色へ量子化。
//       鎖・シャベル・放射弾はこのパレットで塗り直した（形とドット数は v16/v17 のまま）。
//     ・本ファイルの変更は下の「色」セクションの 6 定数だけ。明度の段は据え置き、
//       色相を H=240 度 → 264 度へ寄せた（STONE_TILE_END だけは面色に合わせて暗くした）。
//   タイミング・座標・弾数は 1 つも触っていない。
//
// ── v17 での変更（鎖の段の間隔と、33.4〜39.2s のタイミングだけ。スプライトは v16 のまま）──
//   指示書: Instructions/石工/stage-timing-instructions_20260903_chain.md（8 マーカー）
//     1. 鎖の段の間隔を CELL(2.0) から鎖の 1 辺 SNAKE_TILE(1.15) に詰め、元動画どおり
//        **段どうしが隙間なく接する**ようにした。段数は 9 → 16（画面高 18 ユニットを覆う）。
//        速度は元動画の比率に合わせ、16 段 × 0.09375s = 1.500s で画面高を縦断。
//        横揺れの振幅は鎖サイズ基準に読み替え（0.83 段 = 0.95 ユニット）、周期 1 拍・
//        1 段 60 度の位相差・3 本構成・先頭の出現フラッシュは v15/v16 のまま。
//     2. 8 マーカーへタイミングを合わせ直した（各マーカーは 16 分音符へ丸め、±80ms 以内に
//        強いオンセットがあればそちらを採用。採用値は MK1_WARN1 付近の表）。
//        鎖予告① 33.3961 / 鎖予告② 33.6573 / 鎖攻撃 34.3750 / タイル予告開始 35.9375 /
//        タイル① 36.8750 / ② 37.5000 / ③ 38.3420 / ④ 39.1721。
//        v16 の「35.0〜36.25s の 4 拍の補充」はこの 4 回のタイル出現に置き換えた。
//     3. 区間⑩（v13b 由来の仮の爆破）のうちタイル①〜④と重なる 3 発（36.667 / 37.917 /
//        38.542s）を削除し、39.583s の 1 発だけ残した。40.0s 以降は v13b のまま。
//   区間①〜⑧（〜33.3s）と 40.0s 以降は変更していない。
//
// ── v16 での変更（見た目だけ。弾の配置・タイミング・破壊判定は v15 と完全に同じ）──
//   ユーザー指示: 「テクスチャが詳細すぎる。もっとシンプルに（JSaB と Undertale の融合）」
//   「鎖はシンプルかつ他のタイルより小さめの正方形で描画」。
//     1. スプライト 4 種を平坦な 1 色の面 + 太い明るい輪郭だけに描き直した
//        （Tools/gen_stone3_pixel.py v16）。目地・ひび・欠け・リベット・影は全廃。
//     2. 鎖に専用 type stone3_chain を追加し、サイズを タイルの 0.625 倍（1.15 ユニット・
//        40 ドット）にした。当たり判定も同じサイズ（verts は正方の全面）。
//        出現フラッシュも同じサイズに合わせた。
//
// ── v15 での変更（32 秒からの攻撃だけ作り直し。区間①〜⑧（〜33.3s）と 36.7s 以降は不変）──
//   参考動画を 30fps で見直したところ、v14 の「画面幅のサイン波前線を上から下へ 2 本」は
//   誤読だった。実際は「1 タイル幅の鎖が下から上へ這い上がり、各段は高さを変えずに
//   横へサインで往復する」攻撃。実測値と根拠は Captures/ref_tokyoskies_014_frames.md。
//
// ── v14 での変更（32 秒からの新しい指示。区間①〜⑧＝〜33.3s は不変）──────────
//   ユーザー指示:
//     1. 「00:32 あたりの 2 個の大きな音」に合わせて予告を 2 回出し、
//        「その後の大きな音」で攻撃を出す
//     2. 攻撃は Just Shapes & Beats "Tokyo Skies" 00:14 の攻撃の強化版。
//        画面を縦断する波を出し、波の形に沿ったタイルを破壊する
//     3. その後の 4 拍くらいは、拍に合わせて区間①のようにタイルを出現させる。
//        破壊された場所を中心に補充し、中央にも出しつつすぐ消す
//
//   (V1) 音の特定（Tools/danmaku-lab/.tmp 解析。STFT N=1024 / hop=128 ≒ 5.8ms のフラックス）
//        31.5〜37.0s の局所ピークのうち、2〜6kHz の立ち上がりが 130 を超える「大きな音」は
//        32.917s（344.6）・33.333s（349.3）・35.833s（340.9）・36.250s（304.4）の 4 点で、
//        1 拍差の 2 個ずつ・2 組に分かれている。00:32 台（32.0〜33.0s）に入るピークは
//        32.083s（290.9）と 32.917s（344.6）の 2 個だけ。よって
//          予告① = 32.083s（拍 77・小節20 の 2 拍目）
//          予告② = 32.917s（拍 79・小節20 の 4 拍目）
//          攻撃   = 33.333s（拍 80・小節21 の 1 拍目）＝解析範囲で最強のオンセットで、
//                   曲の 8 小節周期の頭（stone3_section_analysis_20260903.md の境界）
//        予告①→②が 2 拍・②→攻撃が 1 拍と詰まるので、そのまま「溜めて撃つ」形になる。
//
//   (V2) Tokyo Skies 00:14 の観察（アプリ内ブラウザでコマ送り。
//        Captures/ref_tokyoskies_014_{t1355_head,t1385_wave,t1420_full}.png）
//        ・タイル 1 枚幅の鎖が画面の上から下へ「描かれて」いく。鎖はまっすぐではなく
//          左右に蛇行した曲線（＝波の形）で、画面の高さいっぱいを縦断する
//        ・先頭には一回り明るい白のタイルが 1 枚だけ出て、その後ろが実体色になる
//          （t=13.55s の白い 1 枚が予告兼ヘッド。t=13.85 では薄ピンクに落ちている）
//        ・描き終わると後ろから消えていき、t=14.55 には上半分だけ、14.90 には消えている
//        ・所要はおよそ 13.5→14.6s の 1.1 秒
//        v14 はこれの強化版として **2 本・1 拍差・振幅大きめ・帯状（複数行が同時に危険）**
//        にした。細かい設計は下の「── v14 の波 ──」を参照。
//
// ── v13 での変更 ──────────────────────────────────────────────────────
//   (A) 冒頭 8 区間への修正（3 点）と、ドット絵の造形改善（Tools/gen_stone3_pixel.py 側）
//     A1/A2 スプライトの描き直し（グラデーションをやめて輪郭と線のディテールで見せる。
//          シャベルは JSaB 参考画像の比率 握り 15% / 柄 45% / 刃 40% に合わせ直した）。
//          本ファイルの弾データには影響しない（scale・型・色の指定は不変）。
//     A3   タイル爆破①②（区間②④）の頻度を半分に。毎拍 2 枚 → 各小節の 1・3 拍目に 2 枚。
//          blastPhase の beatPhase を参照（既定 [0,2]）。
//     A4   横断シャベル（区間⑥⑧）の速度を 26 → 15.6（6 割）。前の拍のシャベルが画面に
//          残るので、shovelSweepPhase で直前の拍と同じ行を避けるようにした。
//   (B) 33.333s（拍 80・21 小節目）以降を 60.0s（拍 144・37 小節目）まで延長した。
//       区間⑨〜⑯。曲の解析は Instructions/石工/stone3_section_analysis_20260903.md。
//       既存モチーフ（タイル帯・爆破・落下シャベル・横断シャベル・放射弾）の変奏だけで
//       構成し、新要素は入れていない。詳細は下の「── (B) 延長の設計 ──」を参照。
//
// 参考: Just Shapes & Beats "Tokyo Skies" の冒頭（画面全体の正方グリッドにピンクのタイルが
// 拍ごとに点滅→実体化し、プレイヤーは残った隙間を縫って避ける）。
// 曲は石工（BPM144・4/4・offset 0）。細かい音ハメは後調整の前提で、全イベントを拍グリッドに乗せている。
//
// stone2.js（レビュー済みの全編プロトタイプ）と Assets/StageData/stone/ は変更禁止のため、
// 本ファイルは独立したステージ ID "stone3" として新規に作っている。
//
// ── v4 の構成（指示書 Instructions/石工/stage-timing-instructions_20260902.md の 8 マーカー）──
//   指示書のマーカーは拍グリッドから僅かにずれているので、最寄りの拍頭（BPM144・offset 0）へ
//   スナップした。B(n) = n * 0.4166667s（n は t=0 起点の拍番号）。
//
//   # | 指示書   | スナップ後       | 区間長 | 内容
//   --+----------+------------------+--------+--------------------------------------------
//   1 |  6.629s  | B(16) =  6.667s  | 7拍    | タイル表示①（毎拍更新。1回の配置の枚数は v3 のまま）
//   2 |  9.694s  | B(23) =  9.583s  | 9拍    | タイル爆破①（毎拍2枚。v10 では帯の残留から選ぶので9拍とも出る）
//   3 | 13.380s  | B(32) = 13.333s  | 8拍    | タイル表示②（①と同構造・別シード）
//   4 | 16.783s  | B(40) = 16.667s  | 8拍    | タイル爆破②（毎拍2枚 × 8拍。区間末でタイルを消さず残す）
//   5 | 20.001s  | B(48) = 20.000s  | 8拍    | シャベル爆破①（2拍おきに4回。上から落として当たった瞬間に放射）
//   6 | 23.189s  | B(56) = 23.333s  | 8拍    | シャベル飛ばし①（先頭4拍・毎拍2本を左右から。残4拍は静止）
//   7 | 26.591s  | B(64) = 26.667s  | 8拍    | シャベル爆破②（⑤と同構造・別のタイルを選ぶ）
//   8 | 30.161s  | B(72) = 30.000s  | 8拍    | シャベル飛ばし②（⑥と同構造）。区間末 B(80)=33.333s で残タイル消去
//
// ── v2/v3 から引き継いでいる決定（変更していない）──────────────────────
//   ・セルは正方形（playArea 32x18 を 16列 x 9行＝1マス 2x2）
//   ・予告=薄いピンク(warn_box・無害) / 実体=濃いピンク(stone_block)
//   ・放射弾は BulletType "box"（正方スプライト＋verts 4点の正方当たり判定）。life:0 で
//     画面外へ抜けるまで飛ぶ。無重力（originVlc による等速直線）
//   ・放射弾は飛行中に自転する（useVelocityAngle:false + polarForm + thetaVlc の spinBurst）
//   ・逃げ場 3x3 を毎拍 GAPS_PER_BEAT 箇所ぶん必ず空ける
//
// ── v4 での変更 ────────────────────────────────────────────────────────
//   (1) 8 区間へ拡張（上表）。タイル表示/爆破を tilePhase()/blastPhase() に一般化した
//   (2) 弾を小さく: 放射弾の scale を 0.6 → 0.3（半分）。BulletType "box" の verts は
//       BulletCollisionJob.cs:68 で bullet.scale と乗算されるため、当たり判定も同じ比率で縮む
//       （BulletType アセット側を触る必要はない）
//   (3) シャベル（BulletType "stone_shovel"・128x128 のドット絵スコップ）を新規に使用。
//       verts が空＝当たり判定なしの演出用オブジェクトで、色は color.w=0 でスプライト自色のまま。
//       スプライトは刃を下に向けた向きなので、描画角は initialAngle で明示する（落下=0 / 右=+90° / 左=-90°）。
//       DSL/ランタイムに衝突判定は無いので、到達時刻を逆算して「当たった瞬間」に
//       ・シャベルの life を尽きさせる
//       ・対象タイルの life を尽きさせる（BLAST_LEAD_OUT だけ手前）
//       ・同時刻に spinBurst() を出す
//       の 3 つを同じ時刻に置いて衝突を表現している
//   (4) 区間④の末尾でタイルを消さない（指示書 5 の前提）。残ったタイルが
//       ⑤⑦の爆破対象と ⑥⑧のシャベル y 座標（行）の供給源になる
//
// ── v5 での変更（JSaB 風の予告演出の追加のみ。弾幕内容は v4 から一切変えない）──
//   JSaB では危険物が出る前に「同じ形の薄いシルエット」を同じ場所へ出し、実体化の瞬間に
//   濃い色へ切り替わる。経路を通る攻撃は細い光の帯で経路を先に見せ、爆発するものは
//   直前に点滅する。これを 5 種の予告として実装した。**全て warn_box（verts 空＝当たり判定なし）**。
//     (1) タイル出現の予告  … v4 から据え置き（薄ピンク・半拍リード＝WARN_BEATS 0.5）
//     (2) 爆破タイルの点滅  … blinkWarn()。爆破の1拍前から明るいピンクが明滅（v7 で山型に作り直し）
//     (3) 落下シャベルの経路… dropPathWarn()。発射の1拍前から、通る列に縦帯（到達で消える。v7 で太くした）
//     (4) 横断シャベルの経路… shovelSweepPhase() 内。通る行に横一杯の帯＋来る側の端に方向マーク（v7 で太くした）
//     (5) 放射弾の予告      … ringWarn()。爆破の半拍前に中心のまわりへ小点のリング
//   弾数・配置・サイズ・タイミング（v4 の弾幕本体）は変更していない。
//
// ── v6 での変更（見た目のみ。v5 の弾幕・予告の配置/枚数/タイミングは一切変えない）──
//   (1) シャベルの BulletType を stone_shovel（青いドット絵）から stone3_shovel
//       （JSaB 風ホットピンクの新スプライト・128x128・verts 空＝当たり判定なし）へ差し替えた。
//       既存の stone_shovel アセットは他ステージが使うので触っていない。
//   (2) タイル実体化の瞬間に tilePop() を追加。JSaB "Milky Ways" のタイル出現を実測して
//       「拍頭に純白フラッシュ＋スケール 1.67倍 → 約0.10秒で等倍・濃ピンクへ収束」を再現した。
//       予告側は実測でも「実体と同じ大きさ・枠なし」だったため v5 のまま据え置き。
//       近似した点は tilePop() のコメントを参照。
//
// ── v7 での変更（見た目の 2 点のみ。弾幕本体の配置・枚数・タイミング・弾サイズは v4 のまま）─
//   (1) 爆破対象タイルの点滅を「タイル自体が呼吸する」見た目にした（blinkWarn）。
//       v5/v6 はタイルより一回り大きい warn_box（renderPriority 0）を 1 拍に 3 回重ねていたが、
//       warn_box は stone_block（renderPriority 1）より奥に描かれるのでタイルに隠れ、
//       「はみ出した縁だけが光る」→ 録画では灰色っぽい枠に見えていた（v6 録画 t=9.44s で確認）。
//       v7 では重ねる側を stone_flash（renderPriority 4・verts 空＝当たり判定なし。
//       baseSprite/maskSprite は stone_block と同じ）へ変え、タイルと同じ大きさ・同じ位置に
//       重ねる。拡大しないので枠にならず、タイルの面全体が明るいピンクへ変わる。
//       明滅の形は矩形波ではなく山型にした。寿命の異なる短命のコマを 1/60 秒刻みで
//       6 枚重ね、life 末尾 0.1 秒の減衰を合成して sin² の山を作る（solveBlinkPulse）。
//       1 拍に 4 山、山の間隔は爆破へ向かって BLINK_ACCEL 倍ずつ詰まる。
//   (1b) v7b: タイル実体化ポップ（tilePop）も同じ原因で「白い枠」に見えていたので、
//       同様に stone_flash へ切り替えて前面に描き、拍頭の白から 0.1 秒で地色へ滑らかに
//       減衰する構成にした（拡大は灰色ににじむため外し、タイル同寸のみ）。
//   (2) シャベルの経路予告を太くした（PATH_WIDTH）。中心線ではなく「掃かれる幅」を見せる。
//       落下シャベルの縦帯は画面上端から対象タイルの下端までを覆う。横断シャベルの横帯は
//       高さを同じ値にし、画面の横幅いっぱいのまま。来る側の方向マークは残している。
//
// ── v8 での変更（見た目の 2 点のみ。弾幕本体の配置・枚数・タイミング・弾サイズは v4 のまま）─
//   (1) 横断シャベル（区間⑥⑧）の経路予告を全部やめた。v5(4) で足していた
//       「通る行の横帯（PINK_PATH）」と「来る側の端の方向マーク（PINK_MARK）」を削除する。
//       落下シャベル（区間⑤⑦）の縦帯（dropPathWarn）と爆破対象タイルの点滅（blinkWarn）は
//       そのまま残す。ユーザー指摘「横断のほうは帯が邪魔」への対応。
//   (2) タイル実体化ポップ（tilePop）を JSaB "Milky Ways" の実測どおりに作り直した。
//       v7b は「タイル同寸の白 → 0.1 秒で地色」だけで拡大が無く、しかも各コマの
//       life が短すぎて（hold 0.10/0.067/0.033 秒 ＝ 出た瞬間の α が 1.0/0.67/0.33）
//       2 枚目以降が半透明の灰色に見えていた。v8 では
//         ・実測どおり 1.714 倍まで膨らませ、6 コマ（1/60 秒刻み・計 0.100 秒）で等倍へ収束
//         ・各コマの life を「appearTime + 1/60 + FADE_OUT_SEC」にして、そのコマが写る
//           1 フレームの間は α = 1（完全不透明）を保証する。減衰が始まるのは次のコマが
//           手前を覆ったあとなので、白が灰色に見えることは無い
//       ことで動画の見た目へ寄せた。詳細な実測値は POP_FRAMES のコメントを参照。
//   (3) タイル出現の予告色を PINK_TILE_WARN（＝実体と同じ色相の暗い版）に変えた。
//       動画の予告は「実体と同じ色の暗いタイル」で、明るいピンクではない（実測値も
//       PINK_TILE_WARN のコメントに残した）。予告→白フラッシュ→濃ピンク の色の流れが
//       動画と揃う。爆破予告・経路帯・リング予告の PINK_WARN は従来のまま。
//
// ── v9 での変更（見た目 1 点のみ。弾幕本体・横断予告の削除・爆破点滅は v8 のまま）────
//   タイル実体化ポップの作り方を変えた。v8 の「静止した 6 コマの重ね」は、各コマが自分の
//   1 フレームのあと life 末尾 0.1 秒の減衰に入るため、はみ出した部分が白〜灰色の輪郭として
//   0.06〜0.10 秒残っていた（Captures/stone3_v8_pop_compare.png。ユーザー評価「雑」）。
//   v9 ではランタイムに出現アニメ（BulletDataJson の scaleEnd / colorEnd / animDuration。
//   既定 0 で従来動作＝既存ステージへの影響なし）を追加し、1 タイル 1 発の stone_flash が
//   拍頭の白・1.714 倍から 0.100 秒で実体色・等倍へ滑らかに縮むようにした。重なるコマが
//   無いので白い輪郭は原理的に出ない。詳細は POP_SCALE_START 付近のコメントを参照。
//
// ── v10 での変更（タイルの配置ルールと爆破対象の選び方。演出は v9 のまま）────────
//   動機はユーザー評価「ちょっとうるさい」。毎拍 40 枚前後が一斉に点滅→消滅を繰り返すのを
//   やめ、「新しく出る枚数を減らし、出たタイルは縁に溜めて中央を空ける」構成へ変えた。
//   出現ポップ・爆破点滅・縦帯予告・シャベルの見た目・放射弾は v9 から一切変えていない。
//
//   (1) 縁の帯（BAND=3。上下左右すべて 3 セルぶん＝外周 3 周・114 セル）に出たタイルは
//       拍末で消さずに残す。帯の内側（中央 10x3 = 30 セル）に出たタイルは拍末で必ず消す。
//       v9 の「端に 1 拍 2 枚だけ積む」残留ルールは廃止した。
//   (2) 1 拍に新しく出す枚数を v9 の約半分にした。中央の密度 CENTER_RATE は v9 の半分、
//       帯は「区間の拍数をかけて BAND_TARGET まで埋める」等比の刻みで積む。
//       実測（normal）: 区間①は 20→12 枚/拍（平均 15.4・v9 は毎拍 40 前後）。
//   (3) 表示区間の終わりに帯が BAND_TARGET まで埋まり、中央は空になる。
//       実測の帯の埋まり率（表示区間末）: easy 51% / normal 64% / lunatic 72%。
//   (4) 自機の閉じ込め防止。タイルを 1 枚置くごとに「タイルの無いセルが 4 近傍で
//       1 つに繋がっているか」を検査し（emptyIsConnected）、崩す置き方は捨てる。
//       タイルは 1 セルのほぼ全体を覆うので斜めは通れない＝4 近傍で判定するのが正しい。
//       検証（Tools/danmaku-lab/check_stone3_safepath.mjs・0.05 秒刻み）: 安全域の主連結成分は最小でも
//       easy 82 / normal 66 / lunatic 56 セル。主成分以外は爆破跡の 1〜2 セルの袋小路だけ。
//   (5) 爆破①②（区間②④）の対象は、帯の 4 辺（左→上→右→下）を順に回りながら選ぶ。
//       1 拍 2 枚なので隣り合う 2 枚は必ず別の辺になり、区間を通して左右上下へ散る。
//   (6) 落下シャベル（区間⑤⑦）の対象は「下側の帯（行 0〜2）にあるタイルのうち、
//       その列でいちばん上にあるもの」。上側の帯のタイルは（衝突判定が無いので）貫通して
//       通り過ぎ、対象に当たった瞬間だけ爆破する。16 列を 2 列ずつ 8 ゾーンに割って
//       1 ゾーン 1 列だけ選び、偶数ゾーンを⑤・奇数ゾーンを⑦へ振り分ける（4 回とも別の列）。
//   (7) 横断シャベル（区間⑥⑧）の y は「残留タイルの行から選ぶ」現状ルールのまま。
//       残留タイルが帯になったので、行は上下の帯（0〜2 / 6〜8）に加えて、左右の帯にタイルが
//       残っている中央の行（3〜5）も候補に入る。
//
// ── v12 での変更（見た目だけ。区間の割り・音ハメ・配置・枚数・タイミングは v11 のまま）──
//
//   狙い: タイル・シャベル・放射弾を **ボスと同じドット密度のドット絵** にし、
//        色を 1 系統（青紫）へ統一する。
//
//   (1) ドット密度をボスに合わせた（Tools/gen_stone3_pixel.py）
//       ボスは pixelsPerUnit=100・scale=2.8 で出るので、GIF の 1 ドット = 0.0280 ユニット。
//       （実測: Recordings/stone_20260715_121633.mp4 t=22s でボスの明部が縦 73px、
//         stone_idle.gif の同条件が 68 ドット → 1.074 画面px、額装 38.4px/ユニットで割ると 0.0280）
//       弾は 128x128 のテクスチャ全面が scale ぶんのワールドサイズになるので、
//       1 テクスチャ px = scale/128 ユニット。ここから各スプライトのドット数を決めた。
//         タイル   scale 1.84 → 64 ドット（1 ドット = 2 テクスチャ px = 0.02875 ユニット）
//         シャベル scale 3.68 → 128 ドット（1 ドット = 1 テクスチャ px = 0.02875 ユニット）
//         放射弾   scale 0.30 → 10 ドット（1 ドット = 0.0300 ユニット）
//       ★シャベルだけ scale を 2.6 → 3.68 に上げている。2.6 のままだと 1 ドットが 1.38
//         テクスチャ px という半端な値になるため。代わりにスプライト内のシャベル本体を
//         128 ドット中 76 ドットに縮めたので、**画面上の大きさは v11 と同じ**
//         （v11: 縦 2.194 ユニット / v12: 縦 2.185 ユニット）。当たり判定は元から verts 空。
//
//   (2) 色を 1 系統に統一した。ボス本体色 sRGB(89,89,117) を少し明るくした青紫だけを使い、
//       赤・他の色相は一切使わない（v11 は無彩色の石テクスチャ＋純白のポップだった）。
//         輪郭 (20,22,36) / 影 (72,72,104) / 主色 (104,104,140) /
//         ハイライト (150,150,190) / 強ハイライト (190,190,224)
//
//   (3) stone3 専用の BulletType を 3 つ追加した（既存 type とスプライトは無変更）。
//         stone3_tile   … stone_block の複製（renderPriority 1・verts ±0.5・counterPower 1）
//         stone3_flash  … stone_flash の複製（renderPriority 4・verts 空）。スプライトは stone3_tile と同じ
//         stone3_bullet … box の複製（verts ±0.5・counterPower 1.6384）
//       stone3_tile / stone3_flash のマスクは輪郭ドットを白から外してあるので、
//       予告・点滅・ポップで着色されたタイルでも輪郭が黒く残りドット絵に見える。
//
// ── v11 での変更（3 点。区間の割り・音ハメ・弾サイズ・ポップ/点滅の作り方は v10 のまま）──
//   (1) 帯を 2 列に細くした（BAND 3 → 2）。残留域は「縁から 2 セル」＝列 0-1/14-15・行 0-1/7-8 の
//       84 セル（v10 は 114 セル）。中央 12x5 = 60 セルは v10 と同じく毎拍消える一時タイル。
//       ・帯の埋まり率の目標（BAND_TARGET）は v10 と同じ（easy 0.50 / normal 0.64 / lunatic 0.72）。
//         帯へ積む枚数は「残りの空きセル x bandRate」なので、帯が 114→84 に減れば新規枚数も
//         自動的に 84/114 = 0.737 倍になる。
//       ・中央の一時タイルは、セル数が 30→60 に増えるぶん CENTER_RATE を半分にし、さらに
//         同じ 0.737 倍を掛けた（＝v10 の 0.368 倍）。1 拍あたりの中央の枚数は v10 の約 0.74 倍。
//       ・空きセルの 4 近傍連結の検査（emptyIsConnected）、爆破対象の 4 辺めぐり、落下シャベルの
//         「下側の帯でその列のいちばん上」のルールは変更なし（BAND を参照しているので、
//         下側の帯は行 0-1 の 2 行になる）。
//   (2) タイル出現の予告リードを半拍 → 1 拍に延ばした（WARN_BEATS 0.5 → 1.0）。
//       予告は v10 と同じサイズ・同じ「実体と同じ色相の暗い版」のまま、見えている時間だけが倍。
//       爆破対象の点滅（BLINK_LEAD＝1拍前）と縦帯（SWEEP_LEAD＝1拍前）は変更していない。
//       リードが 1 拍になると「前の拍の中央タイル（life = 1拍 + 継ぎ目余白）」と「次の拍の予告」が
//       同じセルに重なる時間帯ができるが、予告は warn_box（renderPriority 0）・実体は
//       stone_block（renderPriority 1）なので予告が必ず奥に描かれ、実体を汚さない
//       （BulletRenderSystem.SortRenderData が renderPriority 昇順に描く。ラボの render.js も同じ）。
//       帯のセルは一度埋まると再抽選されない（bandOccupied）ので、帯側にこの重なりは起きない。
//       爆破予告の点滅は BLAST_MIN_AGE=2 拍のままなので、出現の予告窓とは重ならない。
//   (3) 配色を石工のテクスチャへ統一した（v6〜v10 の JSaB 風ホットピンクをやめた）。
//       基準は stone_block.png（タイルのスプライト）＝無彩色の石テクスチャで、不透明部の
//       平均 sRGB は (171,171,171)・分布 70〜245。既存の石工ステージ（Assets/BulletBuffers/stone/）も
//       stone_block を color.w=0（無着色）で 467 発使っており、これが石工の「石の色」の正。
//       ・タイル実体 … color.w=0（SPRITE_AS_IS）＝ スプライトのテクスチャそのまま。ピンク乗算をやめた
//       ・ポップ    … 拍頭の純白は据え置き。収束先を「無着色（w=0）・石の平均灰色」にしたので、
//                     収束した瞬間に実体タイルのテクスチャへそのまま切り替わる（tilePop 参照）
//       ・点滅/予告/縦帯/爆破予告/リング … 石の色の明版・暗版（STONE_* 定数）
//       ・放射弾（box）… 石の色のやや明るい版（小さいので視認性ぶん明るくしてある）
//       ・シャベル  … stone3_shovel.png を石色で再生成（Tools/gen_stone3_shovel.py）。color.w=0 のまま
//       色はいずれも背景（ほぼ黒 + ブルーム）に対して十分な明度差を確保している。
//
// ── 乱数 ──────────────────────────────────────────────────────────────
//   stage() は難易度の数だけビルド関数を再実行するため Math.random() は使えない。
//   mulberry32 を固定シードでビルド開始時に初期化し、再現可能かつ難易度間で整合する配置にしている。

import { stage, D, bulletDefaults, normalizeNegativeZero, gravitySeq } from '../js/dsl.js';

// --- 拍グリッド ------------------------------------------------------------
const BPM = 144;
const BEAT = 60 / BPM;   // 0.41666667s

// B(n): t=0 起点の拍番号 n の拍頭時刻（0 始まり）。
function B(n) {
  return n * BEAT;
}
function beats(n) {
  return n * BEAT;
}

// --- 区間（拍番号）----------------------------------------------------------
const S1_BEAT = 16, S1_LEN = 7;   // タイル表示①
const S2_BEAT = 23, S2_LEN = 9;   // タイル爆破①
const S3_BEAT = 32, S3_LEN = 8;   // タイル表示②
const S4_BEAT = 40, S4_LEN = 8;   // タイル爆破②
const S5_BEAT = 48;               // シャベル爆破①
const S6_BEAT = 56;               // シャベル飛ばし①
const S7_BEAT = 64;               // シャベル爆破②
const S8_BEAT = 72;               // シャベル飛ばし②
const END_BEAT = 80;              // 区間⑧の末（33.333s）＝残タイル消去

// --- v13 (B): 延長区間（拍番号）------------------------------------------------
// 根拠は Instructions/石工/stone3_section_analysis_20260903.md。
// この範囲は 8 小節周期のループ（小節 21〜28 と 29〜36 の類似度 0.94〜0.999）で、
// 周期の内訳は「1〜4 小節目=主リフ / 5〜6 小節目=休符 / 7〜8 小節目=ビルドアップ」。
// 次の本当のセクション境界は 60.000s（小節 37・拍 144）なので、そこで延長を終える。
//   拍番号 n の時刻 = n * 0.4166667s。小節 m の頭 = 拍 (m-1)*4。
// v14: 区間⑨（v13 は「小節21-22 でタイル表示③を 8 拍」）は削除し、
//   32.083s からの 予告2回 → 波の攻撃 → タイル補充 に置き換えた。
//   拍 80（33.333s）が波の攻撃、拍 84-87（35.000-36.250s）がタイル補充。
//   補充で積んだ帯が v13 の bandC の役割（区間⑩⑫⑯の供給源）をそのまま引き継ぐので、
//   区間⑩以降（36.667s〜）は v13 から一切変えていない。
// v17: 指示書 Instructions/石工/stage-timing-instructions_20260903_chain.md の 8 マーカー。
//   手打ちのマーカーを 16 分音符（0.1041667s）へ丸め、±80ms 以内に音源のオンセット
//   （STFT N=1024 / hop=128 のスペクトルフラックスの局所ピークで、33〜40s の
//   95 パーセンタイル 53.9 を超えるもの）があればそちらを優先した。
//   採用の一覧（.tmp_v17/onset_markers2.py の出力）:
//     # | ラベル         | 指示書   | 16分丸め  | 採用     | 根拠
//     --+----------------+---------+----------+---------+---------------------------
//     1 | 鎖予告①        | 33.440  | 33.4375  | 33.3961 | オンセット flux 54.1
//     2 | 鎖予告②        | 33.702  | 33.7500  | 33.6573 | オンセット flux 65.7
//     3 | 鎖攻撃          | 34.426  | 34.3750  | 34.3750 | 窓内最大 flux 24.3 < 閾値 → 丸め
//     4 | タイル予告開始   | 35.968  | 35.9375  | 35.9375 | 窓内最大 flux 32.9 < 閾値 → 丸め
//     5 | タイル①        | 36.824  | 36.8750  | 36.8750 | 窓内最大 flux 27.8 < 閾値 → 丸め
//     6 | タイル②        | 37.483  | 37.5000  | 37.5000 | 窓内最大 flux 21.9 < 閾値 → 丸め
//     7 | タイル③        | 38.326  | 38.3333  | 38.3420 | オンセット flux 55.2
//     8 | タイル④        | 39.209  | 39.1667  | 39.1721 | オンセット flux 101.2（区間最大）
const MK1_WARN1 = 33.3961;         // 鎖予告①（薄い縦帯）
const MK2_WARN2 = 33.6573;         // 鎖予告②（同じ縦帯を濃く）
const MK3_CHAIN = 34.3750;         // 鎖攻撃＝鎖が下から上へ這い始める
const MK4_TILEWARN = 35.9375;      // タイル予告開始（＝鎖を生き延びた残タイルが崩れる時刻）
const MK5_TILE1 = 36.8750;         // タイル①
const MK6_TILE2 = 37.5000;         // タイル②
const MK7_TILE3 = 38.3420;         // タイル③
const MK8_TILE4 = 39.1721;         // タイル④
const REFILL_TIMES = [MK5_TILE1, MK6_TILE2, MK7_TILE3, MK8_TILE4];
// 予告は各タイルの実体化まで出しっぱなし。①は MK4 から、②〜④は前のタイルの実体化直後から。
const REFILL_LEADS = [
  MK5_TILE1 - MK4_TILEWARN,
  MK6_TILE2 - MK5_TILE1,
  MK7_TILE3 - MK6_TILE2,
  MK8_TILE4 - MK7_TILE3,
];

// --- v19: 指示書マーカー 9〜17（40.0〜50.8s）の採用時刻 ------------------------
//   丸めルールと根拠はファイル冒頭の v19 の表を参照。
const MK9_WARN1 = 40.0080;      //  9 鎖予告①（2 回目・薄い縦帯）
const MK10_WARN2 = 40.4167;     // 10 鎖予告②（同じ帯を濃く）
const MK11_CHAIN = 41.2619;     // 11 鎖攻撃（2 回目）
const MK12_TILEWARN = 42.5099;  // 12 タイル予告開始（＝鎖を生き延びた残タイルが崩れる時刻）
const MK13_TILE1 = 43.3459;     // 13 タイル①
const MK14_TILE2 = 43.9670;     // 14 タイル②
const MK15_BLAST4 = 45.0061;    // 15 タイル爆破 x4（帯の左右上下から 1 枚ずつ）
const MK16_WARN1 = 47.2990;     // 16 鎖予告①（3 回目）
const MK16_WARN2 = 47.5080;     // 16b 鎖予告②（1 回目の相対間隔 +0.2612 を写して丸め）
const MK16_CHAIN = 48.3439;     // 16c 鎖攻撃（3 回目。同 +0.9789）
const MK17_TILEWARN = 49.3750;  // 17 タイル予告開始（2 回目）
const MK17_TILE1 = 50.2083;     // 17b タイル①（12→13 の相対間隔 +0.8360 を写して丸め）
const MK17_TILE2 = 50.8333;     // 17c タイル②（12→14 の +1.4571 を写して丸め）

// マーカー 13/14（左右の帯へ積む）と 17b/17c（外周へ積み直す）の出現時刻と予告リード。
// 予告は「前のイベントから次のタイルの実体化まで出しっぱなし」＝ マーカー 4〜8 と同じ扱い。
const TILE2_TIMES = [MK13_TILE1, MK14_TILE2];
const TILE2_LEADS = [MK13_TILE1 - MK12_TILEWARN, MK14_TILE2 - MK13_TILE1];
const TILE3_TIMES = [MK17_TILE1, MK17_TILE2];
const TILE3_LEADS = [MK17_TILE1 - MK17_TILEWARN, MK17_TILE2 - MK17_TILE1];

const S10_BEAT = 88;               // 小節23-24 36.667s タイル爆破③（アクセントに乗せる）
// v19: 小節25〜32（40.000〜53.333s）の仮区間⑪⑫⑬⑭は指示書のマーカー 9〜17 に
//   置き換えたので、拍番号の定数も削除した（時刻は MK9_WARN1 以降を参照）。
const S15_BEAT = 128;              // 小節33-34 53.333s 休符（横断シャベルの往復・行ずらし）
const S16_BEAT = 136;              // 小節35-36 56.667s 落下シャベル④＋放射弾増量（頂点）
const EXT_END_BEAT = 144;          // 60.000s 次のセクションの頭＝残タイル消去

// --- 画面グリッド（正方セル）------------------------------------------------
// playArea 32x18 を 16列 x 9行に割ると 1マス 2x2 の正方形になる。
const COLS = 16;
const ROWS = 9;
const CELL = 2;              // 正方セル（幅=高さ）
const TILE = CELL * 0.92;    // タイル本体（目地ぶん少し小さい正方形）

function cellCenter(col, row) {
  return [CELL * (col + 0.5), CELL * (row + 0.5)];
}
function key(col, row) {
  return col + ',' + row;
}

// v10: 縁の帯（上下左右すべて BAND セルぶん）＝ここに出たタイルは拍末で消さず溜める。
// v11: BAND 3 → 2。16x9・BAND=2 なら 帯 = 列 0-1 / 14-15 と 行 0-1 / 7-8 の 84 セル、
// 内側（中央）= 列 2-13 x 行 2-6 の 12x5 = 60 セル。中央のタイルは拍末で必ず消す。
const BAND = 2;
function isBandCell(col, row) {
  return col < BAND || col >= COLS - BAND || row < BAND || row >= ROWS - BAND;
}

const BAND_CELLS = [];
const CENTER_CELLS = [];
for (let row = 0; row < ROWS; row++) {
  for (let col = 0; col < COLS; col++) {
    (isBandCell(col, row) ? BAND_CELLS : CENTER_CELLS).push([col, row]);
  }
}

// v13 (B): 延長区間で使う「帯」の変奏。区間⑨は上下だけ、区間⑬は左右だけに溜める。
//   セルの総数は 上下 = 16列 x 4行 = 64 / 左右 = 4列 x 9行 = 36。
//   帯の埋まり率の目標（BAND_TARGET）は同じなので、新規タイルの枚数はセル数に比例して減る。
const BAND_CELLS_TB = [];   // 上下だけ（行 0-1 と 7-8 の全列）
const BAND_CELLS_LR = [];   // 左右だけ（列 0-1 と 14-15 の全行）
for (let row = 0; row < ROWS; row++) {
  for (let col = 0; col < COLS; col++) {
    if (row < BAND || row >= ROWS - BAND) BAND_CELLS_TB.push([col, row]);
    if (col < BAND || col >= COLS - BAND) BAND_CELLS_LR.push([col, row]);
  }
}

// v10: 自機が通れることを保証するための連結判定。
// タイルは 1 セル（2x2）のほぼ全体（TILE=1.84）を占めるので、斜め隣は通れない。
// 「空きセルが 4 近傍で 1 つに繋がっている」を保てば、画面のどこからでもどこへでも
// 移動できる＝閉じ込めが起きない。タイルを 1 枚置くたびにこれを検査して、
// 崩す置き方は捨てる（帯が 6〜7 割埋まっても迷路状の通路が必ず残る）。
const NEIGHBOR_DC = [1, -1, 0, 0];
const NEIGHBOR_DR = [0, 0, 1, -1];
function emptyIsConnected(blocked) {
  const total = COLS * ROWS - blocked.size;
  if (total <= 0) return false;
  let start = null;
  for (let row = 0; row < ROWS && start === null; row++) {
    for (let col = 0; col < COLS; col++) {
      if (!blocked.has(key(col, row))) { start = [col, row]; break; }
    }
  }
  const seen = new Set([key(start[0], start[1])]);
  const stack = [start];
  while (stack.length > 0) {
    const cur = stack.pop();
    for (let d = 0; d < 4; d++) {
      const nc = cur[0] + NEIGHBOR_DC[d];
      const nr = cur[1] + NEIGHBOR_DR[d];
      if (nc < 0 || nc >= COLS || nr < 0 || nr >= ROWS) continue;
      const k = key(nc, nr);
      if (blocked.has(k) || seen.has(k)) continue;
      seen.add(k);
      stack.push([nc, nr]);
    }
  }
  return seen.size === total;
}

// v10: 帯タイルを 4 辺のどれに属するとみなすか（爆破対象を左右上下へ散らすために使う）。
function sideOf(col, row) {
  const dl = col;
  const dr = COLS - 1 - col;
  const db = row;
  const dt = ROWS - 1 - row;
  if (Math.min(dl, dr) <= Math.min(db, dt)) return dl <= dr ? 'left' : 'right';
  return db <= dt ? 'bottom' : 'top';
}
const SIDE_ORDER = ['left', 'top', 'right', 'bottom'];

// --- 色（v18: タイルテクスチャの色相に統一。v11 の値を色相だけ寄せたもの）------------
//
// ★v18: タイルのスプライトがユーザー生成のドット絵テクスチャ（.tmp_tex/5d98ed95….png を
//   64 ドットへ縮小したもの。Tools/gen_stone3_pixel.py）に置き換わった。抽出した 4 色は
//     面 (77,67,93) / 縁明 (116,102,134) / 縁暗 (55,48,67) / 欠片 (40,34,48)
//   で、色相は HSV H≈263〜266 度（平均 264 度）の青紫。v11 の予告色は H=240 度の
//   無彩色寄りだったので、**明度（HSV の V）と彩度は据え置いたまま色相だけ H=264 度へ寄せた**。
//   予告の明暗の段（強ハイライト→ハイライト→影→最暗）は v17 とまったく同じ順序・同じ V なので、
//   予告の読みやすさと拍の見え方は変わらず、色味だけがタイルと揃う。
//   例外は STONE_TILE_END（ポップの収束先）で、これはタイルの面色そのものに合わせる必要が
//   あるため (104,104,140) → (77,67,93) と暗くした。ここがずれると収束の瞬間に色が飛ぶ。
//
// 基準色 = stone_block.png（タイルのスプライト）。無彩色の石テクスチャで、不透明部の平均 sRGB は
// (171,171,171)・分布 70〜245。既存の石工ステージも stone_block を color.w=0 で 467 発使っている。
//
// ★色空間: BulletBuffer の color は **linear 値**で、画面に出るのは sRGB へ変換された色。
//   下の各定数はまず「出したい sRGB」を決め、sRGB→linear で変換した値を書いてある
//   （コメントの (r,g,b) が実際に見える sRGB）。
//
// ★alpha（w）の意味: BulletIndirectURP.shader:283-291 で color.w は「着色するかどうか」の
//   フラグでしかなく（w>0 なら RGB を完全適用・描画アルファは 1）、値の大小は見た目に出ない。
//   w=0 だけが特別で「スプライトのテクスチャそのまま」を意味する（hummer/cutter/warn_box の規約）。
//   予告が暗く見えるのは色ではなく appearDuration の予告窓（α 0.2〜0.5 の拍同期）による。
const SPRITE_AS_IS = [1, 1, 1, 0];                  // 無着色＝スプライトのテクスチャそのまま
const STONE_BRIGHT = [0.8070, 0.7305, 0.9387, 0.92];// (232,222,248) 強ハイライト: 爆破対象の点滅
const STONE_MID = [0.3813, 0.3050, 0.5149, 0.40];   // (166,150,190) ハイライト: 爆破予告・リング予告
const STONE_WARN = [0.0908, 0.0648, 0.1384, 0.40];  // ( 85, 72,104) 影: タイル出現の予告
const STONE_PATH = [0.0395, 0.0273, 0.0648, 0.25];  // ( 56, 46, 72) 最暗: 落下シャベルの縦帯
// ポップの収束先。v18 でタイルの面色 sRGB(77,67,93) に相当する linear 値へ更新（旧 104,104,140）。
// 補間の終端で w が 0 になった瞬間に「実体タイルのテクスチャそのまま」へ入れ替わる（tilePop 参照）。
const STONE_TILE_END = [0.0742, 0.0561, 0.1095, 0];

// --- 決定論的乱数（mulberry32）--------------------------------------------
function makeRng(seed) {
  let a = seed >>> 0;
  return function next() {
    a = (a + 0x6d2b79f5) >>> 0;
    let t = a;
    t = Math.imul(t ^ (t >>> 15), t | 1);
    t ^= t + Math.imul(t ^ (t >>> 7), t | 61);
    return ((t ^ (t >>> 14)) >>> 0) / 4294967296;
  };
}

// 配列をシャッフルした新しい配列を返す（rng は呼び出し側の mulberry32）。
function shuffled(arr, rng) {
  const a = arr.slice();
  for (let i = a.length - 1; i > 0; i--) {
    const j = Math.floor(rng() * (i + 1));
    const t = a[i];
    a[i] = a[j];
    a[j] = t;
  }
  return a;
}

// --- タイル群 --------------------------------------------------------------
// 1フレーム継ぎ目対策（OPUS-DEV-KNOWLEDGE §2）: 予告→実体の切替で1フレーム欠けないよう、
// 退場する側（予告）の life に微小余白を足して実体側と重ねる。予告は無害なので重なってよい。
const SEAM_MARGIN = 0.034;
// 爆破対象タイルは放射開始と同時かわずかに前に消す（v2 で爆破中心にタイルが残るフレームがあった）。
const BLAST_LEAD_OUT = 0.02;

const NEUTRAL_SPAWNER = () => ({
  pos: { x: 0, y: 0 },
  originVlc: { x: 0, y: 0 },
  angle: 0,
  angleInterval: 0,
  color: { x: 1, y: 1, z: 1, w: 1 },
  count: 1,
  interval: 0,
});

// tileField(): cells に一括でタイルを置く（1バッファ = 1クリップ）。
function tileField(cells, opts) {
  const { type, color, appearTime = 0, appearDuration = 0, life, kind } = opts;
  const bullets = cells.map(function (cell) {
    const center = cellCenter(cell[0], cell[1]);
    return bulletDefaults({
      originPos: { x: center[0], y: center[1] },
      typeName: type,
      scale: { x: TILE, y: TILE },
      color: { x: color[0], y: color[1], z: color[2], w: color[3] },
      appearTime,
      appearDuration,
      life,
      unCounterable: true,
    });
  });
  return {
    parts: [{ offsetSec: 0, kind, buffer: { bullets, homing: false, isLaser: false }, spawner: NEUTRAL_SPAWNER() }],
  };
}

// spinBurst(): 自転する弾の全周放射バースト。
// ring() は spawner.count/angleInterval で複製するぶん自転パラメータを持てないので、
// spiral() と同じく弾を1発ずつ Cartesian で組み、cutter()/spiral() の視覚回転トリック
// （useVelocityAngle:false + polarForm + thetaVlc）を各弾に付ける。
// startPos/polynomial/speed が既定値の弾では polarForm は位置に影響しないため、
// 移動は originVlc による等速直線（無重力）のまま、描画角だけが spin(rad/s) で回り続ける。
function spinBurst(opts) {
  const {
    pos, count, speed, spin,
    type = 'box', life = 0, scale = [1, 1], color = [1, 1, 1, 1],
    angleOffset = 0, unCounterable = true, kind = 'spinburst',
  } = opts;
  const bullets = [];
  for (let i = 0; i < count; i++) {
    const angle = angleOffset + (i * 2 * Math.PI) / count;
    const dx = normalizeNegativeZero(Math.cos(angle));
    const dy = normalizeNegativeZero(Math.sin(angle));
    bullets.push(
      bulletDefaults({
        originPos: { x: pos[0], y: pos[1] },
        originVlc: { x: dx * speed, y: dy * speed },
        typeName: type,
        scale: { x: scale[0], y: scale[1] },
        color: { x: color[0], y: color[1], z: color[2], w: color[3] },
        life,
        unCounterable,
        useVelocityAngle: false,
        polarForm: { x: 1, y: normalizeNegativeZero(angle) },
        thetaVlc: spin,
      })
    );
  }
  return {
    parts: [{ offsetSec: 0, kind, buffer: { bullets, homing: false, isLaser: false }, spawner: NEUTRAL_SPAWNER() }],
  };
}

// --- v5: 予告表示（当たり判定なし）の共通クリップ ---------------------------
// warnClip(): warn_box の予告を1クリップにまとめる。
//   items = [{ pos:[x,y], scale:[w,h], color:[r,g,b,a], appearTime, appearDuration, life }]
//   時刻は全てクリップ発火時刻からの相対秒。
//   appearDuration>0 … その区間は「予告点滅」表示（BulletRenderSystem.cs:270 の fadeIn 分岐）
//   appearDuration=0 … appearTime までは完全に非表示（同 :286）。この性質を使うと
//     1クリップの中に時間差の点滅を全部詰め込めるので、点滅のためにクリップを量産せずに済む。
//   life は必ず正の値にする（life<=0 は「寿命なし」扱いで消えなくなる）。
//   v7: it.type で弾のタイプを選べるようにした（既定は従来どおり warn_box）。爆破タイルの
//   点滅だけは stone_flash（renderPriority 4）を使い、タイルより手前に重ねる必要がある。
//   v9: it.scaleEnd / it.colorEnd / it.animDuration を渡すと、ランタイムの出現アニメ
//   （BulletRenderSystem の線形補間）に乗る。指定した弾だけがこのキーを持つ。
function warnClip(items, kind) {
  const bullets = items.map(function (it) {
    const spec = {
      originPos: { x: it.pos[0], y: it.pos[1] },
      typeName: it.type || 'warn_box',
      scale: { x: it.scale[0], y: it.scale[1] },
      color: { x: it.color[0], y: it.color[1], z: it.color[2], w: it.color[3] },
      appearTime: it.appearTime || 0,
      appearDuration: it.appearDuration || 0,
      life: it.life,
      unCounterable: true,
    };
    if (it.animDuration > 0) {
      spec.scaleEnd = { x: it.scaleEnd[0], y: it.scaleEnd[1] };
      spec.colorEnd = { x: it.colorEnd[0], y: it.colorEnd[1], z: it.colorEnd[2], w: it.colorEnd[3] };
      spec.animDuration = it.animDuration;
    }
    return bulletDefaults(spec);
  });
  return {
    parts: [{ offsetSec: 0, kind, buffer: { bullets, homing: false, isLaser: false }, spawner: NEUTRAL_SPAWNER() }],
  };
}

// --- パラメータ ------------------------------------------------------------
// v11: タイル出現の予告リードを半拍 → 1 拍に延ばした（見えている時間が倍。サイズ・色は据え置き）。
// 前の拍の中央タイルと次の拍の予告が同じセルで重なる時間帯ができるが、予告は warn_box
// （renderPriority 0）＝ stone_block（同 1）より必ず奥に描かれるので実体を汚さない。
const WARN_BEATS = 1.0;    // タイル出現の予告リード（拍）
const BLAST_PER_BEAT = 2;  // 1拍あたりの爆破数
const GAPS_PER_BEAT = 2;   // 毎拍必ず空ける 3x3 の逃げ場の数
// v10: 爆破対象は「置かれてから BLAST_MIN_AGE 拍以上たったタイル」から選ぶ。
// 出現ポップと爆破予告の点滅（1拍前から）が同じ拍に重なるのを避けるため。
const BLAST_MIN_AGE = 2;
const SPIN_RATE = 5.0;     // 放射弾の自転速度(rad/s)
const BULLET_SCALE = 0.3;  // v4: v3 の 0.6 の半分。verts も scale 倍されるので当たり判定も半分

// v5: 予告のパラメータ
const BLINK_LEAD = beats(1);       // 点滅を始める時刻（爆破の1拍前）

// v7 (1): 爆破タイルの点滅（山型の明滅）。
// 重ねる弾は stone_flash（renderPriority 4 > stone_block の 1・verts 空＝当たり判定なし）。
// baseSprite/maskSprite が stone_block と同じなので、同じ scale・同じ座標に置くとタイルの
// 面にぴったり重なる（角丸の形まで一致する）。拡大しないので v6 のような「枠」にならない。
const BLINK_TYPE = 'stone3_flash';   // v12: stone_flash の複製（スプライトが stone3_tile）
const FADE_OUT_SEC = 0.1;     // BulletRenderSystem.cs:13 disappearDuration（life 末尾の減衰時間）
const BLINK_SUB = 1 / 60;     // 山を作るコマの間隔（1ゲームフレーム相当）
const BLINK_STEPS = 6;        // 1山あたりのコマ数（hold=0 に解けたコマは出さない）
const BLINK_PEAK = 0.92;      // 山の頂点で狙う合成アルファ
const BLINK_PULSES = 4;       // 1拍あたりの山の数
const BLINK_ACCEL = 0.7;      // 山の間隔の縮み率（爆破へ向けて詰まる＝JSaB の加速する明滅）

// v7 (2): シャベルの経路予告の太さ。中心線ではなく「掃かれる幅」を予告する。
// stone3_shovel.png の刃の最大幅は実測 68px / 128px ＝ scale の 0.531 倍で、
// SHOVEL_SCALE 2.6 では 1.38（タイル 1.84 の 0.75 倍）。ただし実際に壊れるのはタイル1枚ぶん
// なので、帯は刃の実測幅を丸めてタイルと同じ幅にする（v5 の 0.6 から約3倍）。
const PATH_WIDTH = TILE;
const RING_LEAD = beats(0.5);      // 放射弾リング予告のリード（爆破の半拍前）
const RING_COUNT = 8;              // リングの点の数
const RING_RADIUS = 1.7;           // リングの半径
const RING_DOT = 0.42;             // リングの点の一辺
const SWEEP_LEAD = beats(1);       // 横断シャベルの経路予告のリード（発射の1拍前）
// MARK_SIZE / MARK_INSET / MARK_HOLD（横断シャベルの方向マーク）は v8 で予告ごと削除した。

// v9: タイル実体化ポップのパラメータ（JSaB "Milky Ways" の実測 + ランタイムの出現アニメ）。
//
// ── 実測（参考動画 https://www.youtube.com/watch?v=UVpbc1aCJjU・60fps・1276x720）──
//   t=91.067s と t=90.367s の 2 枚のタイルで同一の値を確認した（実測画像は
//   Captures/ref_milkyways_pop_strip.png と ref_milkyways_pop_{f0,f2,settled}.png）。
//   1 枚のタイルは 119x119px（等倍）で、拍頭から 6 フレーム＝ちょうど 0.100 秒で収束する。
//   中心は動かず、同じ中心のまま縮む。
//     コマ | 経過      | 実測サイズ | 倍率  | 実測RGB
//     -----+-----------+-----------+-------+----------------
//      f0  | +0/60     | 204px     | 1.714 | (255,255,255) ＝ 完全な白（半透明ではない）
//      f1  | +1/60     | 177px     | 1.487 | (255,214,255)
//      f2  | +2/60     | 157px     | 1.319 | (255,159,224)
//      f3  | +3/60     | 142px     | 1.193 | (255,116,176)
//      f4  | +4/60     | 129px     | 1.084 | (255, 83,140)
//      f5  | +5/60     | 122px     | 1.025 | (255, 63,118)
//      --  | +6/60     | 119px     | 1.000 | (255, 58,111) ＝ 実体色（以後静止）
//   予告は同サイズ・枠なしで、実体と同じ色相の暗いタイル（v11 では STONE_WARN）。
//
// ── v8 の作り方と、その問題 ──
//   v8 は「静止した 6 枚のコマを 1/60 秒ずつずらして重ねる」方式だった。各コマは自分の
//   1 フレームぶんは α=1（不透明）だったが、そのあと life 末尾の 0.1 秒減衰に入るため、
//   一回り小さい次のコマからはみ出した部分が白〜灰色の輪郭として 0.06〜0.10 秒残った
//   （Captures/stone3_v8_pop_compare.png）。動画には無い見え方で、ユーザー評価も「雑」。
//
// ── v9 の作り方 ──
//   ランタイムに出現アニメ（scaleEnd / colorEnd / animDuration・既定 0 で従来動作）を足し、
//   1 タイルにつき stone_flash 1 発だけで拍頭〜収束までを表現する。
//     ・appearTime=0（＝拍頭）に scale 1.714 倍・純白で出る
//     ・animDuration=0.100 秒かけて scale 1.0 倍・実体色（v11 は STONE_TILE_END）へ線形補間される
//     ・補間が終わった時点で実体タイル（stone_block）と完全に同じ大きさ・同じ色になる
//     ・life = 0.100 + FADE_OUT_SEC(0.1)。減衰する 0.1 秒の間、この弾は実体タイルと
//       寸分違わず重なっているので、消えていく過程は画面上まったく見えない（継ぎ目なし）
//   重なるコマが 1 枚も無いので、v8 の白い輪郭は原理的に発生しない。
//   弾数も 1 タイル 6 発 → 1 発になり、tilepop の JSON は 17MB → 約 3MB に減る。
const POP_SCALE_START = 1.714;                      // 拍頭の倍率（実測 204/119）
const POP_COLOR_START = [0.8879, 0.8388, 0.9734, 1.0]; // 拍頭の色 sRGB(242,236,252)。v12 で純白から色相を持つ明色へ／v18 で H=264 度へ
const POP_DURATION = 0.100;                         // 収束までの秒数（実測どおり）

// v12: 2.6 → 3.68。スプライトのドット間隔をタイル（0.02875 ユニット）に揃えるため
// 1 ドット = 1 テクスチャ px にした結果（3.68/128 = 0.02875）。スプライト内の
// シャベル本体を 128 ドット中 76 ドットに縮めてあるので、画面上の大きさは v11 と同じ。
const SHOVEL_SCALE = 3.68;
const SHOVEL_SPAWN_Y = 26;     // 画面上端(18)より上・カリング境界(36)より内側
const SHOVEL_FALL_SPEED = 24;  // 落下速度（一定）。飛来時間はタイルの高さで変わる
// v13 (A4): 横断シャベルの速度を 26 → 15.6（6 割）へ下げた。
//   画面幅 35 ユニット（-1.5 → 33.5）を渡る時間は 1.346s → 2.244s に伸びるので、毎拍
//   （0.417s）発射だと前の拍の 2 本がまだ画面に残っている状態で次の 2 本が出る。
//   ユーザー了承済み（「次の拍の発射と重なってよい（別の行なので）」）。行が本当に別に
//   なるよう、shovelSweepPhase では直前の拍で使った 2 行を候補から外している。予告は無しのまま。
const SHOVEL_SIDE_SPEED = 15.6;  // 横断速度
const SHOVEL_LEFT_X = -1.5;    // カリング境界 x>=-2 の内側から出す
const SHOVEL_RIGHT_X = 33.5;

// shovel(): 等速直線で飛ぶシャベル1本（無重力・当たり判定なしの演出物）。
// v6: BulletType を stone3_shovel（JSaB 風ホットピンクの新スプライト・128x128・verts 空）へ差し替えた。
//     既存の stone_shovel（青いドット絵）は他ステージが使うので触っていない。
// 描画角は useVelocityAngle:false + initialAngle(rad) で明示指定する。
// stone3_shovel.png は「刃を下に向けた」向きで描かれている＝回転0で下向きなので、
// useVelocityAngle:true にすると落下時（速度角 -90°）に横倒しになってしまう。
// initialAngle は GetRotationAngle()（BulletData.cs:366）でしか使われず、軌道には影響しない。
const SHOVEL_ANGLE_DOWN = 0;                  // 下向き（スプライトそのままの向き）
const SHOVEL_ANGLE_RIGHT = Math.PI / 2;       // 右向き
const SHOVEL_ANGLE_LEFT = -Math.PI / 2;       // 左向き
function shovel(opts) {
  const { pos, vel, angle = SHOVEL_ANGLE_DOWN, life = 0, scale = SHOVEL_SCALE, kind = 'shovel' } = opts;
  return {
    parts: [{
      offsetSec: 0,
      kind,
      buffer: {
        bullets: [bulletDefaults({
          originPos: { x: pos[0], y: pos[1] },
          originVlc: { x: vel[0], y: vel[1] },
          typeName: 'stone3_shovel',
          scale: { x: scale, y: scale },
          color: { x: SPRITE_AS_IS[0], y: SPRITE_AS_IS[1], z: SPRITE_AS_IS[2], w: SPRITE_AS_IS[3] },
          life,
          unCounterable: true,
          useVelocityAngle: false,
          initialAngle: normalizeNegativeZero(angle),
        })],
        homing: false,
        isLaser: false,
      },
      spawner: NEUTRAL_SPAWNER(),
    }],
  };
}

// --- v5: 予告ビルダー（3種。いずれも warnClip＝当たり判定なし）----------------

// (2) 爆破されるタイルの点滅（v7 で作り直し）。
//     クリップ発火 = 爆破の BLINK_LEAD（1拍）前。タイルと同じ大きさ・同じ位置の stone_flash を
//     重ね、タイルの面そのものが 明→暗→明 と脈打って見えるようにする。
//
//     アルファを直接指定する手段は無い（BulletIndirectURP.shader:283-291。color.w は
//     「着色するか否か」のフラグで、w>0 なら描画アルファは 1）。作者が動かせる透明度は
//       ・appearDuration>0 の予告窓 … α = 0.2〜0.5 の拍同期の明滅（値は選べない）
//       ・life の最後の FADE_OUT_SEC(0.1) 秒 … α が 1→0 へ直線減衰
//     の 2 つだけなので、後者を使って山を作る。1 コマは
//       「t0 に出て hold 秒後に消える」＝ 出た瞬間 α = hold/0.1 → hold 秒で 0 へ直線減衰
//     という三角波になる。これを BLINK_SUB(1/60) 秒ずつずらして重ね、合成 α（重ね塗りなので
//     1 - Π(1-α_i)）が sin² の山を通るように各コマの hold を前から順に逆算する。
function solveBlinkPulse() {
  const holds = [];
  function alphaAt(hold, start, t) {
    if (t < start) return 0;
    return Math.max(0, Math.min(1, (hold - (t - start)) / FADE_OUT_SEC));
  }
  for (let j = 0; j < BLINK_STEPS; j++) {
    // 山の目標形（sin² の 1 山。両端は 0 に近く、中央で BLINK_PEAK）
    const target = BLINK_PEAK * Math.pow(Math.sin((Math.PI * (j + 1)) / (BLINK_STEPS + 1)), 2);
    const now = j * BLINK_SUB;
    let rest = 1;
    for (let m = 0; m < j; m++) rest *= 1 - alphaAt(holds[m], m * BLINK_SUB, now);
    // 既に残っているぶん rest の上に target を作るのに必要な自分のアルファ
    const need = rest > 1e-9 ? 1 - (1 - target) / rest : 0;
    holds.push(Math.max(0, Math.min(1, need)) * FADE_OUT_SEC);
  }
  return holds;
}
const BLINK_HOLDS = solveBlinkPulse();
// 1 山が完全に消えるまでの長さ（＝最後まで残るコマの終了時刻）
const BLINK_PULSE_DUR = BLINK_HOLDS.reduce(function (acc, hold, i) {
  return hold > 0 ? Math.max(acc, i * BLINK_SUB + hold) : acc;
}, 0);
// 山の開始時刻。間隔は BLINK_ACCEL 倍ずつ詰まり、最後の山が爆破の瞬間ちょうどに消え終わる。
const BLINK_PULSE_STARTS = (function () {
  const span = BLINK_LEAD - BLINK_PULSE_DUR;
  const weights = [];
  for (let i = 0; i < BLINK_PULSES - 1; i++) weights.push(Math.pow(BLINK_ACCEL, i));
  const total = weights.reduce(function (a, b) { return a + b; }, 0);
  const out = [0];
  weights.forEach(function (w) { out.push(out[out.length - 1] + (span * w) / total); });
  return out;
})();

function blinkWarn(cells, kind) {
  const items = [];
  cells.forEach(function (cell) {
    const c = cellCenter(cell[0], cell[1]);
    BLINK_PULSE_STARTS.forEach(function (pulse) {
      BLINK_HOLDS.forEach(function (hold, i) {
        if (hold <= 0) return;
        const t0 = pulse + i * BLINK_SUB;
        items.push({
          type: BLINK_TYPE,
          pos: c,
          scale: [TILE, TILE],      // タイルと同じ大きさ（拡大しない＝枠にならない）
          color: STONE_BRIGHT,
          appearTime: t0,           // ここまで非表示（appearDuration=0）
          appearDuration: 0,
          life: t0 + hold,
        });
      });
    });
  });
  return warnClip(items, kind);
}

// --- v9: タイル実体化ポップ（1 タイル 1 発。ランタイムの出現アニメを使う）------------
// 拍頭に純白・POP_SCALE_START 倍で出し、POP_DURATION 秒で実体タイルと同じ大きさ・同じ色へ
// 線形に収束させる。収束後は実体タイルと完全に一致するので、life 末尾の減衰は見えない。
// 描画は BLINK_TYPE（stone_flash・renderPriority 4）＝ stone_block(1) より手前。
function tilePop(cells, kind) {
  const items = [];
  cells.forEach(function (cell) {
    const c = cellCenter(cell[0], cell[1]);
    const big = TILE * POP_SCALE_START;
    items.push({
      type: BLINK_TYPE,
      pos: c,                       // 中心はタイルと同じ（拡大・縮小は中心対称）
      scale: [big, big],
      color: POP_COLOR_START,
      scaleEnd: [TILE, TILE],       // 収束後は実体タイルと同寸
      colorEnd: STONE_TILE_END,     // 収束後は無着色（w=0）＝実体タイルのテクスチャそのまま
      animDuration: POP_DURATION,
      appearTime: 0,                // 拍頭ちょうどに出る
      appearDuration: 0,
      // 収束後の 0.1 秒は実体タイルと完全に重なったまま減衰するので、消え際は見えない
      life: POP_DURATION + FADE_OUT_SEC,
    });
  });
  return warnClip(items, kind);
}

// (5) 放射弾の予告。爆破の RING_LEAD（半拍）前に、爆破中心のまわりへ小点をリング状に置き、
//     爆破の瞬間ちょうどで消す（life = RING_LEAD）。
function ringWarn(centers, kind) {
  const items = [];
  centers.forEach(function (c) {
    for (let i = 0; i < RING_COUNT; i++) {
      const a = (i * 2 * Math.PI) / RING_COUNT;
      items.push({
        pos: [c[0] + Math.cos(a) * RING_RADIUS, c[1] + Math.sin(a) * RING_RADIUS],
        scale: [RING_DOT, RING_DOT],
        color: STONE_MID,
        appearTime: RING_LEAD,      // 予告点滅の窓に入れっぱなし（実体化しない）
        appearDuration: RING_LEAD,
        life: RING_LEAD,
      });
    }
  });
  return warnClip(items, kind);
}

// (3) 落下シャベルの経路予告。通る列に、刃の幅ぶん（＝タイル1枚幅）の縦帯を出す。
//     v7: 帯の下端を対象タイルの中心から「タイルの下端」まで下げ、シャベルが掃く範囲を
//     まるごと覆うようにした（対象タイル自体の位置は blinkWarn の点滅でも示している）。
//     クリップ発火 = シャベル発射の1拍前、life = dur でシャベル到達と同時に消える。
function dropPathWarn(center, dur, kind) {
  const top = ROWS * CELL;             // 画面上端 y=18
  const bottom = center[1] - TILE / 2; // 対象タイルの下端
  const h = top - bottom;
  return warnClip(
    [{
      pos: [center[0], bottom + h / 2],
      scale: [PATH_WIDTH, h],
      color: STONE_PATH,
      appearTime: dur,
      appearDuration: dur,
      life: dur,
    }],
    kind
  );
}

// ── v15 の鎖（下から上へ這う蛇）─────────────────────────────────────────────
//
// 参考: Just Shapes & Beats "Tokyo Skies" 0:13.33〜0:14.87（曲 33.33〜34.87s に対応。
//   動画時刻 + 20.000s = stone3.mp3 の時刻。計測ログは Captures/ref_tokyoskies_014_frames.md）。
//   v14 は「画面幅いっぱいのサイン波前線を上から下へ 2 本」だったが、これは誤読だった。
//   実際は **1 タイル幅の鎖が 1 本、画面の下端から上端へ這い上がる** 攻撃で、
//   鎖の各タイルは自分の段（高さ）を変えずに **横方向へサインで往復** している。
//
//   実測値（1920x1080・タイル 75px・段の間隔 82.5px）:
//     ・先頭は 0.100s に 1 段ずつ上へ進む（10 段/秒）
//     ・横揺れは 中心 x=510 / 振幅 75px（= 0.83 セル）/ 周期 0.400s
//     ・隣の段との位相差は 60 度（＝波長 6 段）。模様自体は上から下へ流れる
//     ・同時に見えているのは 7〜8 段ぶん（全 11 段中）。先頭が伸び、末尾が消える
//     ・新しく生えたタイルはその 1〜2 コマだけ淡く光る（＝出現フラッシュ。白い頭ではない）
//     ・鎖は壁タイルを壊さず、最上段の 1 つ下で消える
//
//   本ステージへの移し替え（v17 で改訂）: 画面は 16 列 x 9 行（32x18 ユニット）。
//   v16 までは段の間隔をグリッドの 1 行（CELL = 2 ユニット）にしていたので、
//   1.15 ユニットの鎖のあいだに 0.85 ユニットの隙間が空き、参考の
//   「段どうしが隙間なく接した 1 本の蛇」に見えていなかった。
//   v17 では **段の間隔 = 鎖の 1 辺（SNAKE_TILE = 1.15 ユニット）** にして接するようにし、
//   そのぶん段数を 9 → 16 に増やして画面の高さ（18 ユニット）を覆う。
//   速度は参考の比率に合わせる: 参考は 0.1s/段 × 11 段 ≒ 1.1s で画面を縦断（末尾を
//   含めて約 1.5s）なので、こちらも **16 段 × 0.09375s = 1.500s** で縦断させる。
//   横揺れは鎖のサイズ基準に読み替え（振幅 = 0.83 段 = 0.83 × 1.15）、周期と
//   1 段あたりの位相差（60 度）は参考のまま。全長も参考の 7〜8 枚に合わせる。
//   1 本では 16 列の画面に対して薄いので **本数だけ 3 本**（v15 から不変）。
const SNAKE_ROW_STEP = 0.09375;          // 1 段ぶん進む時間（16 段で 1.500s = 画面高を縦断）
const SNAKE_LEN = 7;                     // 同時に見えるタイル数（参考の全長 7〜8 枚）
const SNAKE_PERIOD = beats(1);           // 0.41667s: 横揺れの周期（実測 0.400s ≒ 1 拍）
const SNAKE_ROW_PHASE = Math.PI / 3;     // 1 段あたりの位相差 60 度（＝波長 6 段）
const SNAKE_SEGMENTS = 16;               // moveTo の折れ線でサインを近似する分割数（1 周期あたり約 10.7 点）
// 3 本の中心列と位相。位相を 120 度ずつずらすと、3 本が同時に同じ向きへ寄らない。
const SNAKE_LANES = [
  { col: 2, phase: 0 },
  { col: 7, phase: (2 * Math.PI) / 3 },
  { col: 12, phase: (4 * Math.PI) / 3 },
];
// v19: 2 回目・3 回目の鎖は中心列を変える（同じ場所を 3 回掃かない）。
//   1 回目 2/7/12 → 2 回目 4/9/14 → 3 回目 1/6/11。どれも間隔 5 列で、
//   振幅 0.95 ユニットぶん往復しても画面（x = 0〜32）からはみ出さない。
//   位相は 3 本が同時に同じ向きへ寄らないよう 120 度差のまま、組ごとに初期位相をずらす。
const SNAKE_LANES2 = [
  { col: 4, phase: Math.PI / 3 },
  { col: 9, phase: Math.PI },
  { col: 14, phase: (5 * Math.PI) / 3 },
];
const SNAKE_LANES3 = [
  { col: 1, phase: (2 * Math.PI) / 3 },
  { col: 6, phase: (4 * Math.PI) / 3 },
  { col: 11, phase: 0 },
];
// v16: 鎖を専用スプライト stone3_chain（タイルより小さい正方形・一段明るい面）にした。
// v17 でも見た目（スプライト・サイズ 1.15 ユニット）は v16 のまま。
const SNAKE_TILE_SCALE = 0.625;
const SNAKE_TILE = Math.round(TILE * SNAKE_TILE_SCALE * 1e6) / 1e6;   // 1.15 ユニット（スプライトは 40 ドット）
// v17: 段の間隔 = 鎖の 1 辺。段どうしが隙間なく接する。
const SNAKE_STEP_Y = SNAKE_TILE;                           // 1.15 ユニット
// 画面（ROWS * CELL = 18 ユニット）は 1.15 で割り切れない（15.65 段）ので、16 段の梯子を
// 上下対称に置く。中心は 0.375 → 17.625、上下の端が 0.2 ユニットだけ画面外へはみ出す
// （鎖が画面外から生えて画面外へ抜ける形になる）。
const SNAKE_STEPS = 16;
const SNAKE_Y0 = (ROWS * CELL - (SNAKE_STEPS - 1) * SNAKE_STEP_Y) / 2;   // 0.375
const SNAKE_AMP = 0.83 * SNAKE_TILE;                       // 0.9545: 横揺れの振幅（参考の 0.83 段ぶん）
// 鎖 1 本が掃く幅の半分（振幅 + 鎖の半分）。予告の帯の幅に使う。
const SNAKE_HALF_W = SNAKE_AMP + SNAKE_TILE / 2;           // 1.53 ユニット
const SNAKE_BREAK_R = CELL * 0.9;                          // 残留タイルを砕く距離（x 方向・中心間）
const SNAKE_BREAK_DY = (CELL + SNAKE_TILE) / 2;            // 同（y 方向）。段がグリッド行と揃わなくなったので明示
const SNAKE_BURSTS = 8;                                    // 砕けたタイルのうち放射弾を出す枚数の上限
const SNAKE_POP_DURATION = 0.05;                           // 出現フラッシュ（実測 1〜2 コマ）
const SNAKE_POP_SCALE = 1.15;

// 段 k の中心 y。
function snakeY(k) {
  return SNAKE_Y0 + k * SNAKE_STEP_Y;
}

// 段 k・鎖 lane のタイルの、時刻 t における x 座標。
// t は曲の絶対時刻。全部の段・全部の本数が同じ時計を見るので、位相が揃う。
function snakeX(t, k, lane) {
  return (
    CELL * (lane.col + 0.5) +
    SNAKE_AMP * Math.sin((2 * Math.PI * t) / SNAKE_PERIOD + k * SNAKE_ROW_PHASE + lane.phase)
  );
}

// 段 k のタイルが画面に居る時間帯 [生える, 消える]（t0 = 攻撃の発火時刻）。
// 先頭は 1 段につき SNAKE_ROW_STEP で上がり、SNAKE_LEN 段ぶん後ろで末尾が消える。
function snakeRowWindow(t0, k) {
  const a = t0 + k * SNAKE_ROW_STEP;
  return [a, a + SNAKE_LEN * SNAKE_ROW_STEP];
}

// 鎖のタイル 1 枚。段（y）は動かさず、moveTo の折れ線だけで横に往復させる。
// v2 レーン（{v2:true}）なので 1 枚 = 弾 1 発。
function snakeTile(t0, k, lane) {
  const y = snakeY(k);
  const win = snakeRowWindow(t0, k);
  const dur = win[1] - win[0];
  const segs = [];
  for (let i = 1; i <= SNAKE_SEGMENTS; i++) {
    const rel = (dur * i) / SNAKE_SEGMENTS;
    segs.push({ until: rel, moveTo: [snakeX(win[0] + rel, k, lane), y] });
  }
  return gravitySeq(
    {
      pos: [snakeX(win[0], k, lane), y],
      vel: [0, 0],
      type: 'stone3_chain',
      scale: [SNAKE_TILE, SNAKE_TILE],
      color: SPRITE_AS_IS,
      unCounterable: true,
    },
    segs,
    'snake',
    { v2: true }
  );
}

// 出現フラッシュ。参考では新しいタイルが生えたコマだけ淡く光り、次のコマには通常色に戻る。
function snakePop(t0, k, lane) {
  const y = snakeY(k);
  const a = snakeRowWindow(t0, k)[0];
  const big = SNAKE_TILE * SNAKE_POP_SCALE;
  return warnClip(
    [{
      type: BLINK_TYPE,
      pos: [snakeX(a, k, lane), y],
      scale: [big, big],
      color: POP_COLOR_START,
      scaleEnd: [SNAKE_TILE, SNAKE_TILE],
      colorEnd: STONE_TILE_END,
      animDuration: SNAKE_POP_DURATION,
      appearTime: 0,
      appearDuration: 0,
      life: SNAKE_POP_DURATION + FADE_OUT_SEC,
    }],
    'snakepop'
  );
}

// 予告。参考では「鎖が掃く幅ぶんの縦帯が画面の上から下まで通しで出て、
// 約 1.8 秒かけて連続に濃くなり、攻撃の瞬間に閃光になって消える」。
// こちらは拍に乗せる都合で、32.083s（薄い）→ 32.917s（濃い）の 2 段階で濃くする。
function snakeWarn(lanes, color, dur, kind) {
  const items = lanes.map(function (lane) {
    return {
      pos: [CELL * (lane.col + 0.5), (ROWS * CELL) / 2],
      scale: [SNAKE_HALF_W * 2, ROWS * CELL],
      color: color,
      appearTime: dur,
      appearDuration: dur,   // 予告点滅の窓に入れっぱなし（実体化しない）
      life: dur,
    };
  });
  return warnClip(items, kind);
}

// セル(col,row) に残っているタイルを鎖が砕く時刻。どの鎖も通らなければ null。
// v17: 段がグリッド行と揃わなくなったので、そのセルと y が重なる段だけを見る。
// 各段が画面に居る間を 1/120 秒刻みで走査し、x が SNAKE_BREAK_R 以内に来た最初の時刻。
function snakeBreakTime(col, row, t0, lanes) {
  const center = cellCenter(col, row);
  const cx = center[0];
  const cy = center[1];
  const STEP = 1 / 120;
  let best = null;
  for (let k = 0; k < SNAKE_STEPS; k++) {
    if (Math.abs(snakeY(k) - cy) > SNAKE_BREAK_DY) continue;
    const win = snakeRowWindow(t0, k);
    lanes.forEach(function (lane) {
      for (let t = win[0]; t <= win[1]; t += STEP) {
        if (Math.abs(snakeX(t, k, lane) - cx) <= SNAKE_BREAK_R) {
          if (best === null || t < best) best = t;
          return;
        }
      }
    });
  }
  return best;
}


// 自機の初期位置(16,2.4) が入るセル。最初の拍だけは必ずここを逃げ場にする（初見の詰み防止）。
const START_GAP = [Math.floor(16 / CELL), Math.floor(2.4 / CELL)];

const MARKERS = {
  1: B(S1_BEAT),
  2: B(S2_BEAT),
  3: B(S3_BEAT),
  4: B(S4_BEAT),
  5: B(S5_BEAT),
  6: B(S6_BEAT),
  7: B(S7_BEAT),
  8: B(S8_BEAT),
  // 延長のジャンプ先（ラボのシークバー用。弾データには影響しない）。
  // v19: 仮区間⑪〜⑭を削除したので、9 以降は指示書のマーカーそのものへ貼り替えた。
  9: MK1_WARN1,       // 鎖攻撃 1 回目の予告①（33.396s）
  10: MK4_TILEWARN,   // タイル①〜④の予告開始（35.938s）
  11: MK9_WARN1,      // 鎖攻撃 2 回目の予告①（40.008s）
  12: MK11_CHAIN,     // 鎖攻撃 2 回目（41.262s）
  13: MK12_TILEWARN,  // タイル①②の予告開始（42.510s）
  14: MK15_BLAST4,    // タイル爆破 x4（45.006s）
  15: MK16_WARN1,     // 鎖攻撃 3 回目の予告①（47.299s）
  16: MK17_TILEWARN,  // タイル攻撃 2 回目の予告開始（49.375s）
  17: B(S15_BEAT),    // 仮区間⑮（53.333s）
  18: B(S16_BEAT),    // 仮区間⑯（56.667s）
};

export default stage(
  {
    name: 'stone3',
    music: 'Assets/StageData/stone3/stone3.mp3',
    bpm: BPM,
    offset: 0,
    markers: MARKERS,
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
    // v13 (B): 延長の末は 60.000s（拍 144・小節 37＝次のセクションの頭）で残タイルを消す。
    //   最後の放射弾（59.167s・速度 7〜11）が画面外へ抜け、55.833s に出た横断シャベルが
    //   渡りきる（2.24s）余裕を見て 62.0s。
    endTime: 62.0,
  },
  (s) => {
    const rng = makeRng(20260902);
    const lead = beats(WARN_BEATS);
    // v11: 中央（12x5 = 60 セル）に毎拍出す一時タイルの密度。
    //   v10 は 10x3 = 30 セルに D(0.125, 0.165, 0.20)＝毎拍 4/5/6 枚。セル数が倍になったので
    //   まず 1/2 にして枚数を据え置き、さらに帯の縮小と同じ比 84/114 = 0.7368 を掛けている。
    //   ＝ v10 の 0.3684 倍。毎拍の中央の枚数は easy 3 / normal 4 / lunatic 4 枚前後。
    const CENTER_RATE = D(0.0461, 0.0608, 0.0737);
    // v10: 表示区間の終わりに縁の帯が埋まっている割合の目安（連結判定で弾かれるぶん実測は少し下がる）。
    // v11 でも目標値は据え置き。帯が 114→84 セルに減るので、同じ割合なら新規枚数も比例して減る。
    const BAND_TARGET = D(0.50, 0.64, 0.72);

    // burst(): 爆破の放射弾。区間②④⑤⑦で共通に使う（弾数・速度の難易度比は v3 のまま）。
    // v13 (B): countMul で放射弾の数を増減できるようにした（曲の盛り上がりに合わせる）。
    //   既定 1.0＝区間①〜⑧と同じ。区間⑯（2 周目の頂点）だけ 1.3 にしている。
    function burst(center, index, countMul = 1) {
      return spinBurst({
        pos: [center[0], center[1]],
        count: Math.round(D(10, 12, 14) * countMul),
        speed: D(7, 9, 11),
        type: 'stone3_bullet',
        life: 0,                          // 寿命なし＝画面外へ出て cull されるまで飛ぶ
        scale: [BULLET_SCALE, BULLET_SCALE],
        color: SPRITE_AS_IS,   // v12: 弾スプライト自体が石色のドット絵なので無着色
        angleOffset: rng() * 2 * Math.PI, // 爆破ごとに別オフセット角（rad）
        spin: SPIN_RATE * (index % 2 === 0 ? 1 : -1),
        kind: 'blast',
        unCounterable: true,
      });
    }

    // ======================================================================
    // タイル表示区間（区間①③）— v10 で配置ルールを作り直した
    //   各拍頭で
    //     ・逃げ場 3x3 を GAPS_PER_BEAT 箇所ぶん確保（ここには絶対にタイルを置かない）
    //     ・縁の帯（v11: BAND=2 セルの外周・84 セル）へ新しいタイルを積む。**拍末で消さずに残る**
    //     ・中央（v11: 12x5 = 60 セル）へ一時タイルを置く。**拍末（次の拍頭）で必ず消える**
    //     ・タイルを 1 枚置くごとに「空きセルが 4 近傍で 1 つに繋がっている」ことを検査し、
    //       崩す置き方は捨てる（＝自機が閉じ込められない。emptyIsConnected 参照）
    //   帯の追加率は「len 拍かけて bandTarget まで埋める」等比の刻みにしてある。
    //   最初の拍がいちばん多く（帯の空きが多いので）、後半になるほど新規枚数は減る。
    //   戻り値 = 帯に積んだタイルの一覧 [{col,row,strike,end,lead}]。
    //   end（消える時刻）は爆破区間・シャベル区間で上書きしてから emitBandTiles() で出す。
    // ======================================================================
    //
    //   v13 (B): cfg.bandCells / cfg.centerCells で溜める場所の変奏を選べるようにした
    //   （区間⑨は上下だけ・区間⑬は左右だけ）。cfg.preBlocked には「この区間の前から
    //   画面に残っているタイル」のセルキーの集合を渡す。連結判定（emptyIsConnected）の
    //   初期状態に入れて候補からも外すので、前の区間の帯と合わせて自機が閉じ込められない。
    function tilePhase(cfg) {
      const bandCells = cfg.bandCells || BAND_CELLS;
      const centerCells = cfg.centerCells || CENTER_CELLS;
      const preBlocked = cfg.preBlocked || new Set();
      const bandOccupied = new Set(preBlocked);   // 既に埋まっている扱い（再抽選しない）
      const bandTiles = [];
      // v17: 拍等間隔（firstBeat + len）のほかに、任意の時刻列 cfg.times を渡せるようにした。
      //   指示書のマーカーは等間隔ではないので、補充（区間⑨' の (e)）はこちらを使う。
      //   cfg.leads を渡すと予告のリードも 1 回ずつ変えられる（前のタイルの実体化から
      //   次のタイルの実体化まで予告を出しっぱなしにするため）。
      const strikes =
        cfg.times ||
        Array.from({ length: cfg.len }, function (_, i) { return B(cfg.firstBeat + i); });
      const bandRate = 1 - Math.pow(1 - cfg.bandTarget, 1 / strikes.length);

      for (let i = 0; i < strikes.length; i++) {
        const strike = strikes[i];
        const myLead = cfg.leads ? cfg.leads[i] : lead;
        // 中央の一時タイルは「次のタイルが出るまで」に消す。等間隔（cfg.times 無し）の
        // 呼び出しでは従来どおり厳密に 1 拍にする（引き算で丸め誤差を出さないため）。
        const centerLife =
          (cfg.times && i + 1 < strikes.length ? strikes[i + 1] - strike : beats(1)) + SEAM_MARGIN;

        // (a) 逃げ場: 3x3 の空きセル領域。区間①の1拍目の1箇所目は自機の初期位置に固定。
        const gapCells = new Set();
        for (let gi = 0; gi < GAPS_PER_BEAT; gi++) {
          const anchor =
            cfg.pinStartGap && i === 0 && gi === 0
              ? START_GAP
              : [1 + Math.floor(rng() * (COLS - 2)), 1 + Math.floor(rng() * (ROWS - 2))];
          for (let dc = -1; dc <= 1; dc++) {
            for (let dr = -1; dr <= 1; dr++) {
              const c = anchor[0] + dc;
              const r = anchor[1] + dr;
              if (c < 0 || c >= COLS || r < 0 || r >= ROWS) continue;
              gapCells.add(key(c, r));
            }
          }
        }

        // この拍に画面へ出ているタイル（帯の既存ぶん＋これから置くぶん）を溜める集合。
        const blocked = new Set(bandOccupied);

        // (b) 帯へ積むタイル（残留）。連結を壊す候補は飛ばす。
        const free = bandCells.filter((c) => !bandOccupied.has(key(c[0], c[1])));
        const bandWant = Math.round(free.length * bandRate);
        const bandPicks = [];
        let bandCand = shuffled(free.filter((c) => !gapCells.has(key(c[0], c[1]))), rng);
        // v14: cfg.priority（セルキーの集合）を渡すと、その候補を先に試す。
        //   波で砕かれたセルとその 4 近傍を優先して積み直すために使う（補充）。
        if (cfg.priority) {
          const pri = cfg.priority;
          bandCand = bandCand
            .filter((c) => pri.has(key(c[0], c[1])))
            .concat(bandCand.filter((c) => !pri.has(key(c[0], c[1]))));
        }
        for (let n = 0; n < bandCand.length && bandPicks.length < bandWant; n++) {
          const cell = bandCand[n];
          const k = key(cell[0], cell[1]);
          blocked.add(k);
          if (!emptyIsConnected(blocked)) {
            blocked.delete(k);
            continue;
          }
          bandPicks.push(cell);
          bandOccupied.add(k);
          bandTiles.push({ col: cell[0], row: cell[1], strike, end: cfg.bandEnd, lead: 0, claimed: false });
        }

        // (c) 中央のタイル（拍末で消える一時タイル）。こちらも連結を壊さない範囲で置く。
        const centerPicks = [];
        const centerWant = Math.round(centerCells.length * cfg.centerRate);
        const centerCand = shuffled(
          centerCells.filter((c) => !gapCells.has(key(c[0], c[1])) && !preBlocked.has(key(c[0], c[1]))),
          rng
        );
        for (let n = 0; n < centerCand.length && centerPicks.length < centerWant; n++) {
          const cell = centerCand[n];
          const k = key(cell[0], cell[1]);
          blocked.add(k);
          if (!emptyIsConnected(blocked)) {
            blocked.delete(k);
            continue;
          }
          centerPicks.push(cell);
        }

        const appearing = centerPicks.concat(bandPicks);
        if (appearing.length === 0) continue;

        // (d) 予告（暗いピンク・無害）: この拍に出る全タイルぶんを1クリップにまとめる
        s.at(
          strike - myLead,
          tileField(appearing, {
            type: 'warn_box',
            color: STONE_WARN,
            appearTime: myLead,
            appearDuration: myLead,
            life: myLead + SEAM_MARGIN,
            kind: 'tilewarn',
          })
        );

        // (d2) 実体化の瞬間のポップ（v9 の出現アニメのまま）
        s.at(strike, tilePop(appearing, 'tilepop'));

        // (e) 中央の一時タイルの実体。次の拍頭で消える。
        if (centerPicks.length > 0) {
          s.at(
            strike,
            tileField(centerPicks, {
              type: 'stone3_tile',
              color: SPRITE_AS_IS,   // v11: 無着色＝石テクスチャそのまま
              life: centerLife,
              kind: 'tile',
            })
          );
        }

        // (f) 帯のタイルの実体は、消える時刻が確定してから emitBandTiles() でまとめて出す。
      }
      return bandTiles;
    }

    // v10: 帯タイルの実体クリップ。出た拍と消える時刻が同じものを 1 クリップにまとめる。
    //   lead>0（爆破される）のタイルは BLAST_LEAD_OUT ぶん手前で消す（爆破中心にタイルを残さない）。
    function emitBandTiles(tiles) {
      const groups = new Map();
      tiles.forEach(function (t) {
        const gk = t.strike + '|' + t.end + '|' + t.lead;
        if (!groups.has(gk)) groups.set(gk, { strike: t.strike, end: t.end, lead: t.lead, cells: [] });
        groups.get(gk).cells.push([t.col, t.row]);
      });
      groups.forEach(function (g) {
        if (g.cells.length === 0) return;
        s.at(
          g.strike,
          tileField(g.cells, {
            type: 'stone3_tile',
            color: SPRITE_AS_IS,   // v11: 無着色＝石テクスチャそのまま
            life: g.end - g.strike - g.lead,
            kind: 'tileband',
          })
        );
      });
    }

    // ======================================================================
    // タイル爆破区間（区間②④）— v10 で対象の選び方を変えた
    //   残留タイルが縁の帯になったので、帯の 4 辺（左・上・右・下）を順番に回りながら
    //   その辺のタイルを 1 枚ずつ選ぶ。1 拍 2 枚なので、隣り合う 2 枚は必ず別の辺になり、
    //   区間を通して左右上下へ均等に散る。
    //   ・爆破の1拍前から対象タイルが点滅、半拍前に薄い予告とリング予告
    //   ・爆破時刻ちょうどでタイルの life が尽き、同時に spinBurst（放射弾）が出る
    //
    //   v13 (A3): 頻度を半分にした。v12 は「毎拍 2 枚」だったが、
    //   **各小節の 1 拍目と 3 拍目だけ・1 回 2 枚**（= 2 拍に 1 回）にする。
    //   拍番号 n の小節内位置は n % 4（0 = 1 拍目 / 2 = 3 拍目）。BPM144・offset 0 で
    //   小節線は拍番号 4 の倍数に来るので、この判定がそのまま楽譜上の 1・3 拍目になる。
    //   点滅予告は従来どおり爆破の 1 拍前（BLINK_LEAD）から。
    //   残留タイルを使い切れなくなるが、余りは区間末の bandEnd で消える（cfg.bandEnd）。
    //     区間② 拍 23〜31 … 発火は 24 / 26 / 28 / 30 の 4 回（v12 は 9 回）
    //     区間④ 拍 40〜47 … 発火は 40 / 42 / 44 / 46 の 4 回（v12 は 8 回）
    //   cfg.beatPhase を渡すと 1・3 拍目の代わりに任意の拍位置集合を使える（区間⑩⑭で使用）。
    // ======================================================================
    function blastPhase(cfg) {
      const pools = {};
      SIDE_ORDER.forEach(function (nm) { pools[nm] = []; });
      shuffled(cfg.tiles, rng).forEach(function (t) { pools[sideOf(t.col, t.row)].push(t); });

      // v13 (A3): 発火する拍位置（小節内 0 起点）。既定は 1 拍目と 3 拍目＝2 拍に 1 回。
      const beatPhase = cfg.beatPhase || [0, 2];

      // v13 (B): cfg.shots を渡すと拍グリッドではなく明示した拍番号（8 分＝小数可）で撃つ。
      //   [{ beat, n }] の配列。n は 1 回に爆破する枚数（省略時 BLAST_PER_BEAT）。
      //   区間⑩で「曲のアクセントに乗せる」ために使う。
      //   v19: beat の代わりに time（秒）を渡せるようにした。指示書のマーカーは拍グリッドに
      //   乗らないので、マーカー 15（45.0061s）の同時 4 枚爆破はこちらを使う。
      const shots = cfg.shots
        ? cfg.shots.map((sh) => ({ beat: sh.beat, time: sh.time, n: sh.n || BLAST_PER_BEAT }))
        : (function () {
            const out = [];
            for (let i = 0; i < cfg.len; i++) {
              const beatNo = cfg.firstBeat + i;
              if (beatPhase.indexOf(beatNo % 4) < 0) continue;  // v13: 2 拍に 1 回だけ爆破する
              out.push({ beat: beatNo, n: BLAST_PER_BEAT });
            }
            return out;
          })();

      let sideIdx = 0;
      for (let b = 0; b < shots.length; b++) {
        const blastTime = shots[b].time !== undefined ? shots[b].time : B(shots[b].beat);
        const youngest = blastTime - beats(BLAST_MIN_AGE) + 1e-6;
        const group = [];
        for (let n = 0; n < shots[b].n; n++) {
          let picked = null;
          for (let tryIdx = 0; tryIdx < SIDE_ORDER.length && picked === null; tryIdx++) {
            const pool = pools[SIDE_ORDER[(sideIdx + tryIdx) % SIDE_ORDER.length]];
            for (let m = 0; m < pool.length; m++) {
              if (pool[m].claimed || pool[m].strike > youngest) continue;
              picked = pool[m];
              break;
            }
            if (picked !== null) sideIdx = (sideIdx + tryIdx + 1) % SIDE_ORDER.length;
          }
          if (picked === null) break;
          picked.claimed = true;
          picked.end = blastTime;
          picked.lead = BLAST_LEAD_OUT;
          group.push(picked);
        }
        if (group.length === 0) continue;

        const cells = group.map((t) => [t.col, t.row]);
        s.at(
          blastTime - beats(0.5),
          tileField(cells, {
            type: 'warn_box',
            color: STONE_MID,
            appearTime: beats(0.5),
            appearDuration: beats(0.5),
            life: beats(0.5), // 爆破の瞬間ちょうどで消す（爆破中心に何も残さない）
            kind: 'blastwarn',
          })
        );
        s.at(blastTime - BLINK_LEAD, blinkWarn(cells, 'blastblink'));

        const centers = cells.map((c) => cellCenter(c[0], c[1]));
        s.at(blastTime - RING_LEAD, ringWarn(centers, 'burstwarn'));
        centers.forEach(function (center) {
          s.at(blastTime, burst(center, b, cfg.burstMul || 1));
        });
      }
    }

    // ======================================================================
    // v10: 落下シャベルの対象タイルの選び方（区間⑤⑦）
    //   対象は「下側の帯（行 0..BAND-1。v11 では行 0〜1）にあるタイルのうち、その列でいちばん上にあるもの」。
    //   シャベルは画面上端から落ちるが、上側の帯のタイルは（衝突判定が無いので）貫通して
    //   通り過ぎ、この対象に当たった瞬間だけ爆破する。縦帯の予告は上端から対象タイルまで。
    //   4 回とも別の列にするため、16 列を 2 列ずつ 8 ゾーンに割って 1 ゾーン 1 列だけ選び、
    //   偶数ゾーンを区間⑤、奇数ゾーンを区間⑦へ振り分ける（どちらも左右に散る）。
    // ======================================================================
    function pickShovelTargets(tiles) {
      const topByCol = new Map();
      tiles.forEach(function (t) {
        if (t.claimed) return;      // 区間④で爆破済みのタイルは対象にしない
        if (t.row >= BAND) return;  // 下側の帯だけが対象
        const cur = topByCol.get(t.col);
        if (!cur || t.row > cur.row) topByCol.set(t.col, t);
      });

      const zones = [];
      for (let z = 0; z < 8; z++) zones.push([]);
      topByCol.forEach(function (t) { zones[Math.floor(t.col / 2)].push(t); });

      const first = [];   // 区間⑤
      const second = [];  // 区間⑦
      const usedCols = new Set();
      zones.forEach(function (pool, z) {
        if (pool.length === 0) return;
        const t = pool[Math.floor(rng() * pool.length)];
        usedCols.add(t.col);
        (z % 2 === 0 ? first : second).push(t);
      });
      // ゾーンが足りなかったときの補充（列は必ず別）
      const spare = shuffled(
        Array.from(topByCol.values()).filter((t) => !usedCols.has(t.col)),
        rng
      );
      [first, second].forEach(function (list) {
        while (list.length < 4 && spare.length > 0) {
          const t = spare.shift();
          usedCols.add(t.col);
          list.push(t);
        }
      });
      return shuffled(first, rng).slice(0, 4).concat(shuffled(second, rng).slice(0, 4));
    }

    // ======================================================================
    // 1. 6.629s → B(16)=6.667s  タイル表示①（7拍）
    // 2. 9.694s → B(23)=9.583s  タイル爆破①（9拍・毎拍2枚＝最大18枚）
    //    帯に残ったタイルは区間③の頭 B(32) で消える（v9 どおり）。
    // ======================================================================
    const bandA = tilePhase({
      firstBeat: S1_BEAT,
      len: S1_LEN,
      centerRate: CENTER_RATE,
      bandTarget: BAND_TARGET,
      bandEnd: B(S3_BEAT),
      pinStartGap: true,
    });
    blastPhase({ firstBeat: S2_BEAT, len: S2_LEN, tiles: bandA });
    emitBandTiles(bandA);

    // ======================================================================
    // 3. 13.380s → B(32)=13.333s  タイル表示②（8拍・rng の続き＝別配置）
    // 4. 16.783s → B(40)=16.667s  タイル爆破②（8拍・毎拍2枚＝最大16枚）
    //    帯に残ったタイルは区間⑧の末 B(80) まで消えず、⑤⑦の爆破対象と
    //    ⑥⑧のシャベル y 座標（行）の供給源になる。
    // ======================================================================

    // ⑤⑦のシャベル到達（＝爆破）時刻。2拍おきに4回。
    const S5_IMPACTS = [0, 2, 4, 6].map((k) => B(S5_BEAT + k));
    const S7_IMPACTS = [0, 2, 4, 6].map((k) => B(S7_BEAT + k));
    const IMPACTS = S5_IMPACTS.concat(S7_IMPACTS);

    const bandB = tilePhase({
      firstBeat: S3_BEAT,
      len: S3_LEN,
      centerRate: CENTER_RATE,
      bandTarget: BAND_TARGET,
      bandEnd: B(END_BEAT),
      pinStartGap: false,
    });
    blastPhase({ firstBeat: S4_BEAT, len: S4_LEN, tiles: bandB });

    // ⑤⑦で爆破する 8 枚（先頭4枚=⑤ / 後半4枚=⑦）を決め、到達時刻で消えるようにする。
    const shovelTiles = pickShovelTargets(bandB);
    shovelTiles.forEach(function (t, k) {
      t.claimed = true;
      t.end = IMPACTS[k];
      t.lead = BLAST_LEAD_OUT;
    });
    const shovelTargets = shovelTiles.map((t) => [t.col, t.row]);

    // 区間⑤〜⑧の間ずっと画面に残るタイル（⑥⑧の横断シャベルの行の供給源）
    // v14: 波の破壊判定より **前** に採る。ここを動かすと⑥⑧の行が変わってしまうため、
    //   v13 と同じ集合（＝爆破・落下シャベルで使われなかった帯タイル）を維持している。
    const heldCells = bandB.filter((t) => !t.claimed).map((t) => [t.col, t.row]);

    // ── v15: 33.333s の鎖が砕く残留タイルを確定させる ──────────────────────
    //   鎖が横に往復しながら通った先に残っているタイルは、鎖が重なった瞬間に life を尽きさせる。
    //   3 本の鎖はそれぞれ 1.53 ユニット幅を掃くので、掃かれない列に残ったタイルは
    //   鎖を生き延び、MK4_TILEWARN（35.9375s）でまとめて崩れる。
    //   emitBandTiles(bandB) より前に end を書き換える必要がある。
    const snakeBroken = [];
    bandB.forEach(function (t) {
      if (t.claimed) return;
      const hit = snakeBreakTime(t.col, t.row, MK3_CHAIN, SNAKE_LANES);
      if (hit === null) {
        t.end = MK4_TILEWARN;   // 鎖の通過後、マーカー④（タイル予告開始）でまとめて崩れる
        return;
      }
      t.claimed = true;
      t.end = hit;
      t.lead = BLAST_LEAD_OUT;
      snakeBroken.push(t);
    });


    emitBandTiles(bandB);

    // ======================================================================
    // シャベル爆破区間（区間⑤⑦）
    //   画面上からシャベルを1本ずつ落とし、対象タイルの中心に到達した瞬間に
    //   ・シャベルの life が尽きる（＝タイルの位置で消える）
    //   ・タイルの life が尽きる（tiletarget クリップ側で設定済み）
    //   ・spinBurst が出る
    //   DSL/ランタイムに衝突判定は無いため、到達時刻を逆算して3つを同時刻に置いている。
    // ======================================================================
    function shovelBlastPhase(impacts, targets, spinBase, burstMul = 1) {
      impacts.forEach(function (impact, k) {
        const cell = targets[k];
        if (!cell) return;
        const center = cellCenter(cell[0], cell[1]);
        const flight = (SHOVEL_SPAWN_Y - center[1]) / SHOVEL_FALL_SPEED;
        // v5 (3): 発射の1拍前から、シャベルが通る列に縦帯（到達＝爆破で消える）。v7 で刃の幅ぶんに拡幅
        s.at(impact - flight - SWEEP_LEAD, dropPathWarn(center, SWEEP_LEAD + flight, 'droppathwarn'));
        // v5 (2): 爆破の1拍前から対象タイルを点滅させる
        s.at(impact - BLINK_LEAD, blinkWarn([cell], 'blastblink'));
        // v5 (5): 爆破の半拍前に放射弾のリング予告
        s.at(impact - RING_LEAD, ringWarn([center], 'burstwarn'));
        s.at(
          impact - flight,
          shovel({
            pos: [center[0], SHOVEL_SPAWN_Y],
            vel: [0, -SHOVEL_FALL_SPEED],
            angle: SHOVEL_ANGLE_DOWN,
            life: flight, // 到達＝タイルの位置でちょうど消える
            kind: 'shoveldrop',
          })
        );
        s.at(impact, burst(center, spinBase + k, burstMul));
      });
    }

    // 5. 20.001s → B(48)=20.000s  シャベル爆破①
    shovelBlastPhase(S5_IMPACTS, shovelTargets.slice(0, 4), 0);
    // 7. 26.591s → B(64)=26.667s  シャベル爆破②（残っているタイルから別の4枚）
    shovelBlastPhase(S7_IMPACTS, shovelTargets.slice(4, 8), 1);

    // ======================================================================
    // シャベル飛ばし区間（区間⑥⑧）
    //   先頭4拍、毎拍2本。左端→右 と 右端→左 を1本ずつ。
    //   y は残っているタイルの行の中心に合わせ、左右で必ず別の行にする。
    //   タイルに当たっても何も起こらず、画面を横切って外へ抜ける（life:0＝カリング任せ）。
    // ======================================================================
    const heldRows = Array.from(new Set(heldCells.map((c) => c[1]))).sort((a, b) => a - b);

    // v13 (B): 横断シャベル 1 組（左→右・右→左）を時刻 t に出す。区間⑥⑧⑪⑮で共有する。
    //   予告は v8 で全廃したまま（ユーザー評価「帯が邪魔」）。当たり判定は元から無い。
    function sweepPair(t, leftRow, rightRow) {
      if (leftRow !== null) {
        s.at(
          t,
          shovel({
            pos: [SHOVEL_LEFT_X, cellCenter(0, leftRow)[1]],
            vel: [SHOVEL_SIDE_SPEED, 0],
            angle: SHOVEL_ANGLE_RIGHT,
            life: 0,
            kind: 'shovelsweep',
          })
        );
      }
      if (rightRow !== null) {
        s.at(
          t,
          shovel({
            pos: [SHOVEL_RIGHT_X, cellCenter(0, rightRow)[1]],
            vel: [-SHOVEL_SIDE_SPEED, 0],
            angle: SHOVEL_ANGLE_LEFT,
            life: 0,
            kind: 'shovelsweep',
          })
        );
      }
    }

    function shovelSweepPhase(firstBeat, count = 4) {
      // v13 (A4): 速度を落としたぶん前の拍のシャベルが画面に残るので、直前の拍で使った行は
      //   候補から外す（候補が尽きたときだけ全行に戻す）。同じ行で追いかけっこにならない。
      let prevRows = [];
      for (let i = 0; i < count; i++) {
        const t = B(firstBeat + i);
        if (heldRows.length === 0) continue;
        let pool = heldRows.filter((r) => prevRows.indexOf(r) < 0);
        if (pool.length < 2) pool = heldRows;
        const leftRow = pool[Math.floor(rng() * pool.length)];
        const others = pool.filter((r) => r !== leftRow);
        const rightRow = others.length > 0 ? others[Math.floor(rng() * others.length)] : leftRow;
        prevRows = [leftRow, rightRow];

        // v8 (1): 横断シャベルの経路予告（横帯 + 方向マーク）は削除した。
        //   v5(4)〜v7 では通る行に横一杯の PINK_PATH の帯と、来る側の端に PINK_MARK の
        //   方向マークを出していたが、ユーザー評価で「帯が邪魔」となったため全部外す。
        //   落下シャベル（区間⑤⑦）の縦帯 dropPathWarn と、爆破対象タイルの点滅 blinkWarn は
        //   そのまま残している。シャベル本体（下の2本）の配置・時刻・サイズは v4 のまま不変。
        sweepPair(t, leftRow, rightRow);
      }
    }

    // 6. 23.189s → B(56)=23.333s  シャベル飛ばし①
    shovelSweepPhase(S6_BEAT);
    // 8. 30.161s → B(72)=30.000s  シャベル飛ばし②
    shovelSweepPhase(S8_BEAT);

    // ======================================================================
    // ★ v13 (B) 延長: 区間⑨〜⑯（33.333s → 60.000s・小節 21〜36 の 16 小節）
    //
    //   解析: Instructions/石工/stone3_section_analysis_20260903.md
    //   この範囲は 8 小節周期のループで、周期の内訳は
    //     1〜4 小節目 = 主リフ（低域が毎拍鳴る。4 小節目に 8 分のフィル）
    //     5〜6 小節目 = 休符（低域が抜ける。フラックス最小）
    //     7〜8 小節目 = ビルドアップ（高域が上がり、8 小節目が最も密）
    //   これを 2 周ぶん（小節 21〜28 / 29〜36）そのまま構成に写した。次の本当の
    //   セクション境界は 60.000s（中域が 7.3 → 10.9 に跳ねる）なのでそこで終える。
    //
    //   新要素は入れていない。既存モチーフの変奏だけ:
    //     ・タイルの帯を「上下だけ」（⑨）と「左右だけ」（⑬）に変える
    //     ・爆破を曲のアクセント（8 分位置を含む）に乗せる（⑩⑭）
    //     ・落下シャベルを 2 本同時にする（⑫⑯）
    //     ・横断シャベルを行ずらしで往復させる（⑮）
    //     ・放射弾の数を盛り上がりで増やす（⑯だけ 1.3 倍）
    //     ・休符の小節（⑪⑮）では新規タイルも爆破も出さない
    // ======================================================================

    // v13 (B): 落下シャベルの対象を選ぶ汎用版（区間⑤⑦の pickShovelTargets の一般化）。
    //   区間⑤⑦は「下側の帯にあるタイル」に限っていたが、延長では帯が上下だけ／左右だけに
    //   変わるので下側の在庫が足りない（easy で 1 本しか出せない事象を実測した）。
    //   ここでは行の制限をやめ、**その列で最も上にあるタイル**を対象にする。上から落ちる
    //   シャベルが最初にぶつかる 1 枚なので、貫通が一切起きない点ではむしろ正しい。
    //   16 列を count 個のゾーンに割って 1 ゾーン 1 列だけ選ぶ（左右に散らす・列は必ず別）。
    //   usedCols は呼び出し側で持ち回り、同じ区間の中では同じ列を使わない。
    function pickDropTargets(tiles, count, usedCols) {
      const topByCol = new Map();
      tiles.forEach(function (t) {
        if (t.claimed || usedCols.has(t.col)) return;
        const cur = topByCol.get(t.col);
        if (!cur || t.row > cur.row) topByCol.set(t.col, t);
      });
      // 落下距離が長いほうが見栄えするので、下側の帯（行 0..BAND-1）にある列を先に使い、
      // 足りないぶんだけ上側の列で補う。
      const low = [], high = [];
      topByCol.forEach(function (t) { (t.row < BAND ? low : high).push(t); });
      const out = [];
      [low, high].forEach(function (group) {
        if (out.length >= count) return;
        const zones = [];
        for (let z = 0; z < count; z++) zones.push([]);
        group.forEach(function (t) {
          zones[Math.min(count - 1, Math.floor((t.col * count) / COLS))].push(t);
        });
        zones.forEach(function (pool) {
          if (out.length >= count || pool.length === 0) return;
          const cand = pool.filter((t) => !usedCols.has(t.col));
          if (cand.length === 0) return;
          const t = cand[Math.floor(rng() * cand.length)];
          usedCols.add(t.col);
          out.push(t);
        });
        const spare = shuffled(group.filter((t) => !usedCols.has(t.col)), rng);
        while (out.length < count && spare.length > 0) {
          const t = spare.shift();
          usedCols.add(t.col);
          out.push(t);
        }
      });
      return out.slice(0, count);
    }

    // 落下シャベルの対象タイルに到達時刻を書き込むヘルパ（区間⑤⑦と同じ扱い）。
    function claimDropTargets(tiles, impacts) {
      tiles.forEach(function (t, k) {
        t.claimed = true;
        t.end = impacts[k];
        t.lead = BLAST_LEAD_OUT;
      });
      return tiles.map((t) => [t.col, t.row]);
    }

    // ----------------------------------------------------------------------
    // ★ v17 ⑨' 33.396〜39.172s — 予告2連 → 下から上へ這う鎖 → タイル出現 4 回
    //   （指示書 Instructions/石工/stage-timing-instructions_20260903_chain.md の 8 マーカー。
    //    採用時刻の根拠は MK1_WARN1 付近の表を参照）
    //
    //   v14 の「画面幅のサイン波前線 2 本を上から下へ」は参考動画の読み違いだったので、
    //   ここだけ作り直した。区間①〜⑧（〜33.3s）と区間⑩以降（36.667s〜）は不変。
    //   拍 77/79/80 は区間⑧（拍 72-79）の中に入るが、⑧が弾を出しているのは拍 72-75
    //   （横断シャベル 4 拍ぶん）だけで拍 76-79 は空いているため、区間⑧の内容には
    //   一切触れていない（追加した予告は当たり判定のない warn_box）。
    //
    //   (a) 予告①（33.3961s / マーカー1）… 鎖が掃く幅の縦帯を、画面の下から上まで通しで。
    //       色は最暗の STONE_PATH。予告②まで出しっぱなし。
    //   (b) 予告②（33.6573s / マーカー2）… 同じ帯を STONE_WARN で濃くする。攻撃の瞬間に消える。
    //   (c) 攻撃（34.3750s / マーカー3）… 鎖 3 本が下から生える。
    //       v17: 段の間隔を鎖の 1 辺（1.15 ユニット）にして **段どうしが接する** ようにし、
    //       段数を 9 → 16 に増やした。1 段 0.09375s（16 段 = 1.500s で画面高を縦断）。
    //       同時に見えるのは 7 段（参考の全長 7〜8 枚）。段ごとに 60 度ずれた横揺れ
    //       （周期 1 拍・振幅 0.83 段 = 0.95 ユニット）で蛇行する。
    //       最後の段が消えるのは 34.375 + (15 + 7) × 0.09375 = 36.44s。
    //       鎖が重なった残留タイルはその瞬間に砕け（snakeBroken）、
    //       うち最大 SNAKE_BURSTS 枚から放射弾が出る。
    //   (d) 残タイル崩落（35.9375s / マーカー4）… 鎖に掃かれなかったタイルが崩れる
    //       （bandB の end。上の「鎖の破壊判定」で設定済み）。ここからタイルの予告が出始める。
    //   (e) タイル出現 4 回（36.8750 / 37.5000 / 38.3420 / 39.1721s / マーカー5-8）…
    //       区間①と同じ出現ポップ。予告は各タイルの実体化まで継続的に見せる
    //       （①はマーカー4 から、②〜④は前のタイルの実体化直後から＝REFILL_LEADS）。
    //       帯は v13 の区間⑨と同じ「上下だけ」で、候補の順番だけ「鎖で砕けたセルと
    //       その 4 近傍」を先頭に寄せてある。中央（12x5）にもタイルが出て、次のタイルで消える。
    //       ここで積んだ帯が v13 の bandC の役割をそのまま引き継ぐので、区間⑪以降は不変。
    // ----------------------------------------------------------------------
    // v13 (B): 延長では「上下の帯」と「左右の帯」が同時に画面へ乗る（⑬以降）ので、
    //   1 枚あたりの目標埋まり率を区間①③の 0.62 倍に下げてある。区間①③は外周ぐるり
    //   84 セルを D(0.50,0.64,0.72) まで埋めていたが、延長は 上下 64 + 左右 36（うち角 16 は
    //   重複）で、同じ率だと自機の通路（空きセルの 4 近傍連結）が確保できず
    //   emptyIsConnected が全候補を弾いてしまう（lunatic で区間⑬が 0 枚になる不具合を実測）。
    const EXT_BAND_TARGET = D(0.31, 0.40, 0.45);

    // (a)(b) 予告 2 連（同じ帯を 2 段階で濃くする）
    s.at(MK1_WARN1, snakeWarn(SNAKE_LANES, STONE_PATH, MK2_WARN2 - MK1_WARN1, 'snakewarn1'));
    s.at(MK2_WARN2, snakeWarn(SNAKE_LANES, STONE_WARN, MK3_CHAIN - MK2_WARN2, 'snakewarn2'));

    // (c) 鎖 3 本。1 段 = 弾 1 発（v2 レーンの折れ線）＋出現フラッシュ 1 発。
    //     v17: 段の間隔 1.15 ユニット（隙間なく接する）× 16 段 × 0.09375s = 1.500s で縦断。
    const SNAKE_T0 = MK3_CHAIN;
    SNAKE_LANES.forEach(function (lane) {
      for (let k = 0; k < SNAKE_STEPS; k++) {
        const at = snakeRowWindow(SNAKE_T0, k)[0];
        s.at(at, snakeTile(SNAKE_T0, k, lane));
        s.at(at, snakePop(SNAKE_T0, k, lane));
      }
    });

    // (c) 砕けたタイルからの放射弾。全部に付けると弾が溢れるので、列でばらけるように
    //     等間隔で間引いて最大 SNAKE_BURSTS 枚だけにする。弾数は通常の爆破の半分。
    const brokenSorted = snakeBroken.slice().sort(function (a, b) {
      return a.col - b.col || a.row - b.row;
    });
    if (brokenSorted.length > 0) {
      const step = Math.max(1, Math.ceil(brokenSorted.length / SNAKE_BURSTS));
      for (let i = 0, k = 0; i < brokenSorted.length; i += step, k++) {
        const t = brokenSorted[i];
        const center = cellCenter(t.col, t.row);
        s.at(t.end, burst(center, k, 0.5));
      }
    }


    // (e) 補充（4 拍）。鎖で砕けたセルとその 4 近傍を優先して積み直す。
    const refillPriority = new Set();
    snakeBroken.forEach(function (t) {
      refillPriority.add(key(t.col, t.row));
      for (let d = 0; d < 4; d++) {
        const nc = t.col + NEIGHBOR_DC[d];
        const nr = t.row + NEIGHBOR_DR[d];
        if (nc < 0 || nc >= COLS || nr < 0 || nr >= ROWS) continue;
        refillPriority.add(key(nc, nr));
      }
    });

    const bandC = tilePhase({
      times: REFILL_TIMES,
      leads: REFILL_LEADS,
      centerRate: CENTER_RATE,
      bandTarget: EXT_BAND_TARGET,
      bandCells: BAND_CELLS_TB,
      bandEnd: B(EXT_END_BEAT),
      priority: refillPriority,
      pinStartGap: false,
    });

    // ----------------------------------------------------------------------
    // ⑩ 小節23-24 / 36.667s / 拍 88-95 — タイル爆破③（曲のアクセントに乗せる）
    //   小節23 の強拍は 1 拍目(258)・2 拍裏(361)・4 拍目(602) → 1 拍目と 4 拍目を採る。
    //   小節24 は 8 分のフィルで 1 拍裏(655) が最大 → 1 拍裏と 4 拍目(405) を採る。
    //   区間②④と同じ「1 回 2 枚」なので密度は同じ（4 回 × 2 枚）。
    // ----------------------------------------------------------------------
    //   v17: 指示書のタイル①〜④（36.875〜39.172s）と重なる 3 発
    //   （36.667 / 37.917 / 38.542s）は削除した。タイルが出るそばから爆破していては
    //   指示書の「4 回のタイル出現」が見えないため。マーカー⑧（39.172s）より後の
    //   39.583s の 1 発だけを残し、40.0s 以降の区間⑪〜⑯は v13b のまま。
    blastPhase({
      tiles: bandC,
      shots: [
        { beat: S10_BEAT + 7 },      // 39.583s 小節24 4拍目
      ],
    });

    // ======================================================================
    // ★ v19  40.008〜50.833s — 指示書のマーカー 9〜17
    //   鎖攻撃 2 回目 → タイル①② → 同時 4 枚爆破 → 鎖攻撃 3 回目 → タイル攻撃 2 回目。
    //   採用時刻とその根拠はファイル冒頭の v19 の表。
    //   ここにあった v13b 由来の仮区間は、40.0〜51.9s が丸ごと重なるので削除した:
    //     ⑪ 40.000 / 42.500s の横断シャベル 2 本（鎖 2 回目と重なる）
    //     ⑫ 43.333 / 45.000 / 46.458s の落下シャベル 5 本（タイル①②・4 枚爆破・鎖 3 回目と重なる）
    //     ⑬ 46.667〜49.583s のタイル表示④ 8 拍（鎖 3 回目・タイル攻撃 2 回目と重なる）
    //     ⑭ 50.625 / 51.250 / 51.875s の爆破④ 3 回（指示書の「その後は静止」と噛み合わない）
    //   50.833s より後の仮区間⑮（53.333s〜）⑯（56.667s〜）はそのまま残し、
    //   対象タイルの供給源だけ新しい帯（bandC / bandD / bandE）へ差し替えている。
    // ======================================================================

    // 鎖攻撃 1 回分をまとめて出すヘルパ。1 回目（33.4〜36.4s・SNAKE_LANES）と同じ構造で、
    //   予告①（薄い縦帯）→ 予告②（同じ帯を濃く）→ 鎖 3 本（16 段 × 0.09375s = 1.500s で
    //   下から上へ縦断）→ 掃かれた残留タイルの破壊 → 砕けたタイルからの放射弾
    // を、中心列（lanes）と時刻だけ変えて出す。戻り値は砕けたタイルの一覧。
    function chainAttack(warn1, warn2, t0, lanes, tiles) {
      s.at(warn1, snakeWarn(lanes, STONE_PATH, warn2 - warn1, 'snakewarn1'));
      s.at(warn2, snakeWarn(lanes, STONE_WARN, t0 - warn2, 'snakewarn2'));
      lanes.forEach(function (lane) {
        for (let k = 0; k < SNAKE_STEPS; k++) {
          const at = snakeRowWindow(t0, k)[0];
          s.at(at, snakeTile(t0, k, lane));
          s.at(at, snakePop(t0, k, lane));
        }
      });
      const broken = [];
      tiles.forEach(function (t) {
        if (t.claimed) return;
        const hit = snakeBreakTime(t.col, t.row, t0, lanes);
        if (hit === null) return;   // 掃かれなかったタイルはそのまま残す
        t.claimed = true;
        t.end = hit;
        t.lead = BLAST_LEAD_OUT;
        broken.push(t);
      });
      // 放射弾は全部には付けない（弾が溢れる）。列でばらけるよう等間隔に間引いて
      // 最大 SNAKE_BURSTS 枚だけ、弾数も通常の爆破の半分にする（1 回目と同じ扱い）。
      const sorted = broken.slice().sort(function (a, b) {
        return a.col - b.col || a.row - b.row;
      });
      if (sorted.length > 0) {
        const step = Math.max(1, Math.ceil(sorted.length / SNAKE_BURSTS));
        for (let i = 0, k = 0; i < sorted.length; i += step, k++) {
          const t = sorted[i];
          s.at(t.end, burst(cellCenter(t.col, t.row), k, 0.5));
        }
      }
      return broken;
    }

    // ----------------------------------------------------------------------
    // マーカー 9-11: 鎖攻撃 2 回目（予告① 40.008 / 予告② 40.417 / 攻撃 41.262s）
    //   中心列は 4 / 9 / 14。1 回目（2 / 7 / 12）から 2 列ずらしてあるので、
    //   1 回目を生き延びて残っている上下の帯（bandC）のセルにも破壊が起きる。
    //   掃かれなかったタイルはそのまま画面に残り、マーカー 15 の爆破対象になる。
    // ----------------------------------------------------------------------
    chainAttack(MK9_WARN1, MK10_WARN2, MK11_CHAIN, SNAKE_LANES2, bandC);

    // ----------------------------------------------------------------------
    // マーカー 12-14: 予告表示（42.510s）→ タイル①（43.346s）→ タイル②（43.967s）
    //   マーカー 4-8 と同じ出現ポップ。積む先は **左右の帯**（列 0-1 / 14-15）にして、
    //   上下（bandC の残り）と合わせて外周の 4 辺が揃うようにする（マーカー 15 の前提）。
    //   予告は「前のイベントから実体化まで出しっぱなし」＝ TILE2_LEADS。
    //   上下の帯は既に画面にあるので preBlocked に渡し、連結判定（自機が閉じ込められない）
    //   を上下＋左右の合計で行う。中央（12x5）にも一時タイルが出て次のタイルで消える。
    // ----------------------------------------------------------------------
    const aliveC = new Set(bandC.filter((t) => !t.claimed).map((t) => key(t.col, t.row)));
    const bandD = tilePhase({
      times: TILE2_TIMES,
      leads: TILE2_LEADS,
      centerRate: CENTER_RATE,
      bandTarget: EXT_BAND_TARGET,
      bandCells: BAND_CELLS_LR,
      preBlocked: aliveC,
      bandEnd: B(EXT_END_BEAT),
      pinStartGap: false,
    });

    // ----------------------------------------------------------------------
    // マーカー 15: タイル爆破 x4（45.006s に 4 枚同時）
    //   blastPhase の 4 辺めぐり（左 → 上 → 右 → 下）で 1 枚ずつ選ぶので、
    //   4 枚は必ず帯の左右上下に散る。在庫は上下＝bandC・左右＝bandD。
    //   1 拍前から対象が点滅（blinkWarn）、半拍前に薄い予告とリング予告、
    //   45.006s ちょうどでタイルの life が尽き、同時に放射弾が 4 箇所から出る。
    // ----------------------------------------------------------------------
    blastPhase({
      tiles: bandC.concat(bandD),
      shots: [{ time: MK15_BLAST4, n: 4 }],
    });

    // ----------------------------------------------------------------------
    // マーカー 16: 鎖攻撃 3 回目（予告① 47.299 / 予告② 47.508 / 攻撃 48.344s）
    //   中心列は 1 / 6 / 11。1 回目・2 回目のどちらとも重ならない位置で、
    //   左端の列 0-1（bandD の左の帯）も掃く。対象は上下＋左右の残り全部。
    // ----------------------------------------------------------------------
    const brokenD = chainAttack(
      MK16_WARN1, MK16_WARN2, MK16_CHAIN, SNAKE_LANES3, bandC.concat(bandD)
    );

    // ----------------------------------------------------------------------
    // マーカー 17: タイル攻撃 2 回目（予告開始 49.375 → ① 50.208 → ② 50.833s）
    //   マーカー 5-8 の補充と同じ作りで、3 回目の鎖で砕けたセルとその 4 近傍を
    //   優先して外周へ積み直す。積む先は外周ぐるり（BAND_CELLS）。
    //   50.833s から仮区間⑮（53.333s）までは新規の弾を出さない＝静止。
    // ----------------------------------------------------------------------
    const refill3Priority = new Set();
    brokenD.forEach(function (t) {
      refill3Priority.add(key(t.col, t.row));
      for (let d = 0; d < 4; d++) {
        const nc = t.col + NEIGHBOR_DC[d];
        const nr = t.row + NEIGHBOR_DR[d];
        if (nc < 0 || nc >= COLS || nr < 0 || nr >= ROWS) continue;
        refill3Priority.add(key(nc, nr));
      }
    });
    const aliveCD = new Set(
      bandC.concat(bandD).filter((t) => !t.claimed).map((t) => key(t.col, t.row))
    );
    const bandE = tilePhase({
      times: TILE3_TIMES,
      leads: TILE3_LEADS,
      centerRate: CENTER_RATE,
      bandTarget: EXT_BAND_TARGET,
      bandCells: BAND_CELLS,
      preBlocked: aliveCD,
      priority: refill3Priority,
      bandEnd: B(EXT_END_BEAT),
      pinStartGap: false,
    });

    // ----------------------------------------------------------------------
    // ⑮ 小節33-34 / 53.333s / 拍 128-135 — 休符（横断シャベルの往復・行ずらし）
    //   低域が小節を通して 30 前後に落ちる 2 小節。爆破もタイル追加もせず、
    //   横断シャベルだけを 2 拍おきに 4 組流す。左→右は行を下から上へ、
    //   右→左は上から下へ 1 段ずつずらして「往復」に見せる。
    //   行は「その時点で画面に残っているタイルの行」から採る（⑨の上下＋⑬の左右）。
    // ----------------------------------------------------------------------
    //   v19: 供給源にマーカー 17 で積み直した bandE を足した（⑬ を削除したため）。
    const rowsCD = Array.from(
      new Set(
        bandC.filter((t) => !t.claimed).map((t) => t.row)
          .concat(bandD.filter((t) => !t.claimed).map((t) => t.row))
          .concat(bandE.filter((t) => !t.claimed).map((t) => t.row))
      )
    ).sort((a, b) => a - b);
    if (rowsCD.length > 0) {
      const nr = rowsCD.length;
      for (let i = 0; i < 4; i++) {
        const li = i % nr;                      // 左→右 は下の行から上へ 1 段ずつ
        let ri = (nr - 1 - i + nr) % nr;        // 右→左 は上の行から下へ 1 段ずつ
        if (ri === li && nr > 1) ri = (li + Math.floor(nr / 2)) % nr;  // 同じ行に重ならないよう避ける
        sweepPair(B(S15_BEAT + i * 2), rowsCD[li], nr > 1 ? rowsCD[ri] : null);
      }
    }

    // ----------------------------------------------------------------------
    // ⑯ 小節35-36 / 56.667s / 拍 136-143 — 落下シャベル④＋放射弾増量（2 周目の頂点）
    //   低域が戻り（rms 0.40）、小節36 は 4 分の均等なアクセント。⑫と同じ形で
    //   1 拍目に 2 本同時、3 拍目に 1 本。放射弾だけ 1.3 倍に増やして頂点を作る。
    //     56.667s ×2 / 57.500s ×1 / 58.333s ×2 / 59.167s ×1
    //   対象は⑨の上下の帯の残り（⑫で使った列は避ける）。
    // ----------------------------------------------------------------------
    const S16_IMPACTS = [
      B(S16_BEAT + 0), B(S16_BEAT + 0),     // 56.667s 小節35 1拍目・2 本同時
      B(S16_BEAT + 4), B(S16_BEAT + 4),     // 58.333s 小節36 1拍目・2 本同時
      B(S16_BEAT + 6),                      // 59.167s 小節36 3拍目・1 本で締める
    ];
    //   対象は⑨の上下の帯の残りに加えて⑬の左右の帯の下段（列 0-1/14-15 の行 0-1）も使う。
    //   ⑫からは 13 秒離れていて画面にも残っていないので、列の重複を避ける集合は分ける
    //   （共有すると Easy で候補が尽きて 1 本しか出せなくなるのを実測した）。
    //   v19: ⑫⑬ を削除したので、対象は bandC / bandD の残りとマーカー 17 の bandE から採る。
    const dropD = pickDropTargets(bandC.concat(bandD).concat(bandE), S16_IMPACTS.length, new Set());
    shovelBlastPhase(
      S16_IMPACTS.slice(0, dropD.length),
      claimDropTargets(dropD, S16_IMPACTS),
      1,
      1.3
    );

    // 残ったタイルの実体クリップを出す（消える時刻が全部確定したあと）。
    // 使い切らなかったタイルは bandEnd = B(144) = 60.000s＝次のセクションの頭で消える。
    emitBandTiles(bandC);
    emitBandTiles(bandD);
    emitBandTiles(bandE);
  }
);
