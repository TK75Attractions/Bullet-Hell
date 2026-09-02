// choreo/stone3.js — 新・石工（stone3）冒頭パート A/B プロトタイプ v3
//
// 参考: Just Shapes & Beats "Tokyo Skies" の冒頭（画面全体の正方グリッドにピンクのタイルが
// 拍ごとに点滅→実体化し、プレイヤーは残った隙間を縫って避ける）。
// 曲は石工（BPM144・4/4・offset 0）。細かい音ハメは後調整の前提で、全イベントを拍グリッドに乗せている。
//
// stone2.js（レビュー済みの全編プロトタイプ）と Assets/StageData/stone/ は変更禁止のため、
// 本ファイルは独立したステージ ID "stone3" として新規に作っている。
//
// ── 構成（8拍 = 2小節）────────────────────────────────────────────────
//   bar1        : イントロ（何も出さない）。1拍目の予告を t<0 に置けないため実体は bar2 から。
//   bar2 (4拍)  : パートA タイル配置
//                 各拍頭で画面全体のグリッドにタイルを敷き詰める。中央寄りのタイルはその拍の
//                 終わりで消え、画面端（外周フレーム）のタイルは残って溜まっていく。
//                 毎拍、ランダム位置に 3x3 の「逃げ場」を2箇所必ず空ける（＋確率的な穴も多数）。
//   bar3 (4拍)  : パートB 爆破
//                 端に残ったタイルを毎拍2枚ずつ爆破し、その位置から自転する四角い弾を放射する。
//                 この間、パートA最終拍のタイル配置は画面に残ったまま（bar4 の1拍目で一斉に消える）。
//
// ── v2 での変更（ユーザー指示7点）────────────────────────────────────
//   (1) セルを正方形に: 32x18 を 16列x9行（1マス 2x2）へ組み直した（16:9 にちょうど一致）
//   (2) 密度を大幅増: 1拍あたり 40〜80 枚（難易度依存）。逃げ場は毎拍2箇所を保証
//   (3) 着色: 予告=薄いピンク(warn_box・無害)、実体=濃いピンク(stone_block)。
//       BulletType 側の baseColor が白なので、bullet の color（乗算tint）がそのまま見た目の色になる
//       （color.w>0 = 乗算tint・w が alpha を兼ねる、の規約）
//   (4) 最下段を空ける処置を廃止。最下段を含む画面全体にタイルが出る
//   (5) 放射弾は life:0（寿命による消滅なし）＝画面外へ出て cull されるまで飛ぶ。速度も約2倍
//   (6) 放射の開始角度は爆破ごとにシード乱数で別オフセット
//   (7) 弾の見た目を四角に: BulletType "box"（正方スプライト＋verts 4点の正方当たり判定）を使用。
//       ※ 従来使っていた stone_burst は verts:[] ＝ Unity 側では当たり判定が無い弾だった
//
// ── v3 での変更（ユーザー指示3点）────────────────────────────────────
//   (1) 四角い弾を自転させる: DSL の ring() に自転指定が無いため、cutter()/spiral() と同じ
//       手法（useVelocityAngle:false + polarForm + thetaVlc）を使う spinBurst() を本ファイル内に
//       用意して置き換えた。polarForm は startPos/polynomial/speed が既定値の弾では位置に
//       一切影響しないので、originVlc による等速直線（無重力）はそのままに描画角だけが回る。
//   (2) タイルを減らす: fillRate を 0.38/0.50/0.60 → 0.25/0.33/0.40 に下げ、
//       1拍あたり概ね 30/40/50 枚（v2 の約 65%）にした。逃げ場 3x3 x2 箇所の保証は据え置き。
//   (3) 爆破中もタイルを残す: パートA最終拍のタイルを拍末で消さず、パートBの4発目の爆破が
//       終わった次の拍頭（bar4 の1拍目）まで画面に残す。爆破対象のタイルだけが爆破の瞬間に消える。
//       ・爆破対象タイルの life から継ぎ目余白を外し、BLAST_LEAD_OUT だけ手前で消す
//         （v2 で爆破中心にタイルが1フレーム残って見えたため）
//       ・最終拍のタイルは過去に積んだ端タイル（＝爆破対象）のセルを避けて置く。
//         重なっていると爆破後もそのセルにタイルが残ってしまうため
//
// ── 使用した DSL 語彙 ────────────────────────────────────────────────
//   raw() 相当（bulletDefaults を直接組む tileField ヘルパー）
//          … 「静止した矩形タイル」の専用プリミティブが DSL に無いための代用。
//            1バッファに複数弾を入れられるので、1拍ぶんのタイル群をまとめて1クリップにしている
//            （タイル1枚1クリップにするとクリップ数が数百になるため）。
//   ring() … 爆破の放射弾。originVlc による等速直線なので gravity は掛からない（無重力）。
//
// ── 乱数 ──────────────────────────────────────────────────────────────
//   stage() は難易度の数だけビルド関数を再実行するため Math.random() は使えない。
//   mulberry32 を固定シードでビルド開始時に初期化し、再現可能かつ難易度間で整合する配置にしている。

import { stage, D, bulletDefaults, normalizeNegativeZero } from '../js/dsl.js';

// --- 拍グリッド ------------------------------------------------------------
const BPM = 144;
const BEAT = 60 / BPM;   // 0.41666667s
const BAR = BEAT * 4;    // 1.66666667s

// T(小節, 拍): 小節1拍1 = t=0（どちらも1始まり）。
function T(bar, beat = 1) {
  return (bar - 1) * BAR + (beat - 1) * BEAT;
}
function beats(n) {
  return n * BEAT;
}

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

// 端（外周フレーム）= 左右2列 + 上下1行。ここに置いたタイルはパートAで消さずに溜める。
function isEdgeCell(col, row) {
  return col <= 1 || col >= COLS - 2 || row === 0 || row === ROWS - 1;
}

const ALL_CELLS = [];
const EDGE_LEFT = [];
const EDGE_RIGHT = [];
for (let row = 0; row < ROWS; row++) {
  for (let col = 0; col < COLS; col++) {
    ALL_CELLS.push([col, row]);
    if (!isEdgeCell(col, row)) continue;
    (col < COLS / 2 ? EDGE_LEFT : EDGE_RIGHT).push([col, row]);
  }
}

// --- 色（JSaB 風ピンク）------------------------------------------------------
const PINK_WARN = [1.0, 0.45, 0.72, 0.40];   // 予告: 薄いピンク・半透明（warn_box は verts:[] で無害）
const PINK_SOLID = [1.0, 0.16, 0.52, 1.0];   // 実体: 濃いピンク
const PINK_BULLET = [1.0, 0.42, 0.72, 1.0];  // 放射弾: 中間のピンク

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

// --- パラメータ ------------------------------------------------------------
const PART_A_BAR = 2;      // パートA（タイル配置）の小節
const PART_B_BAR = 3;      // パートB（爆破）の小節
const WARN_BEATS = 0.5;    // タイル出現の予告リード（拍）
const EDGE_PER_BEAT = 2;   // 1拍あたりに端へ積む「残留タイル」数（パートBの消化数と対応）
const BLAST_PER_BEAT = 2;  // 1拍あたりの爆破数（8枚 / 4拍 = 2枚）
const GAPS_PER_BEAT = 2;   // 毎拍必ず空ける 3x3 の逃げ場の数
const SPIN_RATE = 5.0;     // 放射弾の自転速度(rad/s)。約0.8回転/秒＝2秒の飛行で1.6回転

const MARKERS = {
  1: T(PART_A_BAR, 1),     // パートA 開始
  2: T(PART_B_BAR, 1),     // パートB 開始
  3: T(PART_B_BAR + 1, 1), // パートB 終わり
};

export default stage(
  {
    name: 'stone3',
    music: 'Assets/StageData/stone/stone.mp3',
    bpm: BPM,
    offset: 0,
    markers: MARKERS,
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
    endTime: 8.0, // 冒頭パートのみの断片ステージ（放射弾が画面外へ抜けてから終わる）
  },
  (s) => {
    const rng = makeRng(20260902);
    const lead = beats(WARN_BEATS);
    const usedEdge = new Set();
    const stackedEdge = []; // 端に溜まったタイル（積んだ順＝爆破する順）
    // v3: パートA最終拍のタイルを残す期限。パートB最後の爆破の次の拍頭（bar4 の1拍目）。
    const HOLD_UNTIL = T(PART_B_BAR + 1, 1);

    // 自機の初期位置(16,2.4) が入るセル。1拍目だけは必ずここを逃げ場にする（初見の詰み防止）。
    const START_GAP = [Math.floor(16 / CELL), Math.floor(2.4 / CELL)];

    // ========================================================================
    // パートA（bar2・4拍）— タイル配置
    //   各拍頭で
    //     ・逃げ場 3x3 を GAPS_PER_BEAT 箇所ぶん確保（ここには絶対にタイルを置かない）
    //     ・残りのセルへ確率 fillRate でタイルを敷き詰める（→ 確率的な穴も多数残る）
    //     ・敷いたタイルはその拍の終わりで消滅
    //     ・端（外周フレーム）には別途「残留タイル」を2枚積み、パートBの爆破まで残す
    // ========================================================================
    const fillRate = D(0.25, 0.33, 0.40); // v3: 1拍あたり概ね 30/40/50 枚（v2 の約65%）

    for (let i = 0; i < 4; i++) {
      const strike = T(PART_A_BAR, 1 + i);
      const blastTime = T(PART_B_BAR, 1 + i); // この拍に積んだ残留タイルが爆破される時刻

      // (a) 逃げ場: 3x3 の空きセル領域。1拍目の1箇所目は自機の初期位置に固定。
      const gapCells = new Set();
      for (let gi = 0; gi < GAPS_PER_BEAT; gi++) {
        const anchor =
          i === 0 && gi === 0
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

      // (b) 残留タイル: 端から左右1枚ずつ（左右対称に溜める）。逃げ場とは重ねない。
      const persist = [];
      [EDGE_LEFT, EDGE_RIGHT].forEach(function (pool) {
        const cand = pool.filter((c) => !usedEdge.has(key(c[0], c[1])) && !gapCells.has(key(c[0], c[1])));
        for (let n = 0; n < EDGE_PER_BEAT / 2 && cand.length > 0; n++) {
          const picked = cand.splice(Math.floor(rng() * cand.length), 1)[0];
          usedEdge.add(key(picked[0], picked[1]));
          persist.push(picked);
          stackedEdge.push({ col: picked[0], row: picked[1], strike });
        }
      });
      // (c) タイル（逃げ場と端の残留タイル以外を確率で埋める）
      //     v3: これまでに積んだ端タイル（usedEdge）を全部除外する。最終拍のタイルは
      //     パートBの間ずっと残るため、爆破対象のセルと重なると爆破後もタイルが残ってしまう。
      const transient = ALL_CELLS.filter(
        (c) => !gapCells.has(key(c[0], c[1])) && !usedEdge.has(key(c[0], c[1])) && rng() < fillRate
      );

      // (d) 予告（薄いピンク・無害）: この拍に出る全タイルぶんを1クリップにまとめる
      s.at(
        strike - lead,
        tileField(transient.concat(persist), {
          type: 'warn_box',
          color: PINK_WARN,
          appearTime: lead,
          appearDuration: lead,
          life: lead + SEAM_MARGIN,
          kind: 'tilewarn',
        })
      );

      // (e) 実体（濃いピンク）
      //     v3: 最終拍（i===3）のタイルだけは拍末で消さず HOLD_UNTIL まで残す。
      //     パートB中の画面は、この「パートA終了時点の配置」がそのまま残った状態になる。
      const isLastA = i === 3;
      s.at(
        strike,
        tileField(transient, {
          type: 'stone_block',
          color: PINK_SOLID,
          life: isLastA ? HOLD_UNTIL - strike : beats(1) + SEAM_MARGIN,
          kind: isLastA ? 'tilehold' : 'tile',
        })
      );

      // (f) 実体（濃いピンク）: 端の残留タイル。パートBの対応する拍で爆破されるまで残る。
      s.at(
        strike,
        tileField(persist, {
          type: 'stone_block',
          color: PINK_SOLID,
          // v3: 継ぎ目余白を足さず BLAST_LEAD_OUT ぶん手前で消す（爆破中心にタイルを残さない）
          life: blastTime - strike - BLAST_LEAD_OUT,
          kind: 'tilekeep',
        })
      );
    }

    // ========================================================================
    // パートB（bar3・4拍）— 爆破
    //   各拍で、積んだ順に端のタイルを2枚ずつ爆破する。
    //   ・爆破の半拍前に薄いピンクの予告（1拍ぶん2枚を1クリップに）
    //   ・爆破時刻ちょうどでタイルの life が尽き、同時に ring（放射弾）が出る
    //   ・放射弾(spinBurst)は無重力の等速直線。life:0 なので画面外へ抜けるまで消えない
    //   ・開始角度は爆破ごとにシード乱数で別オフセット
    //   ・v3: 各弾は飛行中に SPIN_RATE(rad/s) で自転する（拍ごとに回転方向を反転）
    //   ・v3: 爆破対象以外のタイル（パートA最終拍ぶん）は画面に残ったまま。bar4 の1拍目で一斉に消える
    // ========================================================================
    for (let b = 0; b < 4; b++) {
      const blastTime = T(PART_B_BAR, 1 + b);
      const group = stackedEdge.slice(b * BLAST_PER_BEAT, (b + 1) * BLAST_PER_BEAT);

      s.at(
        blastTime - beats(0.5),
        tileField(
          group.map((t) => [t.col, t.row]),
          {
            type: 'warn_box',
            color: PINK_WARN,
            appearTime: beats(0.5),
            appearDuration: beats(0.5),
            // v3: 継ぎ目余白を足さず爆破の瞬間ちょうどで消す（爆破中心に何も残さない）
            life: beats(0.5),
            kind: 'blastwarn',
          }
        )
      );

      group.forEach(function (t) {
        const center = cellCenter(t.col, t.row);
        s.at(
          blastTime,
          spinBurst({
            pos: [center[0], center[1]],
            count: D(10, 12, 14),
            speed: D(7, 9, 11),        // v1（3.5/4.5/5.5）の約2倍
            type: 'box',               // 正方スプライト＋正方当たり判定の弾種
            life: 0,                   // 寿命なし＝画面外へ出て cull されるまで飛ぶ
            scale: [0.6, 0.6],
            color: PINK_BULLET,
            angleOffset: rng() * 2 * Math.PI,        // 爆破ごとに別オフセット角（rad）
            spin: SPIN_RATE * (b % 2 === 0 ? 1 : -1), // v3: 飛行中の自転。拍ごとに回転方向を反転
            kind: 'blast',
            unCounterable: true,
          })
        );
      });
    }
  }
);
