// choreo/stone3.js — 新・石工（stone3）冒頭 8 区間 v5
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
//   2 |  9.694s  | B(23) =  9.583s  | 9拍    | タイル爆破①（毎拍2枚 × 7拍で残留14枚を使い切り、残2拍は静止）
//   3 | 13.380s  | B(32) = 13.333s  | 8拍    | タイル表示②（①と同構造・別シード）
//   4 | 16.783s  | B(40) = 16.667s  | 8拍    | タイル爆破②（毎拍2枚 × 8拍で残留16枚。区間末でタイルを消さず残す）
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
//     (2) 爆破タイルの点滅  … blinkWarn()。爆破の1拍前から 1拍の間に3回、明るいピンクが明滅
//     (3) 落下シャベルの経路… dropPathWarn()。発射の1拍前から、通る列に細い縦帯（到達で消える）
//     (4) 横断シャベルの経路… shovelSweepPhase() 内。通る行に横一杯の細い帯＋来る側の端に方向マーク
//     (5) 放射弾の予告      … ringWarn()。爆破の半拍前に中心のまわりへ小点のリング
//   弾数・配置・サイズ・タイミング（v4 の弾幕本体）は変更していない。
//
// ── 乱数 ──────────────────────────────────────────────────────────────
//   stage() は難易度の数だけビルド関数を再実行するため Math.random() は使えない。
//   mulberry32 を固定シードでビルド開始時に初期化し、再現可能かつ難易度間で整合する配置にしている。

import { stage, D, bulletDefaults, normalizeNegativeZero } from '../js/dsl.js';

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

// 端（外周フレーム）= 左右2列 + 上下1行。ここに置いたタイルは表示区間で消さずに溜める。
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
// シャベルは color.w=0 ＝ スプライト自色そのまま（hummer/cutter と同じ規約）。
const SPRITE_AS_IS = [1, 1, 1, 0];
// v5 の予告色。いずれも warn_box（当たり判定なし）に使う。
const PINK_FLASH = [1.0, 0.80, 0.92, 0.85];  // 点滅の「明」側（通常ピンクより明るい）
const PINK_PATH = [1.0, 0.45, 0.72, 0.25];   // 経路の帯（薄ピンク・alpha 0.25）
const PINK_MARK = [1.0, 0.28, 0.60, 0.85];   // 横断シャベルの方向マーク（濃いめ）

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

// --- v5: 予告表示（当たり判定なし）の共通クリップ ---------------------------
// warnClip(): warn_box の予告を1クリップにまとめる。
//   items = [{ pos:[x,y], scale:[w,h], color:[r,g,b,a], appearTime, appearDuration, life }]
//   時刻は全てクリップ発火時刻からの相対秒。
//   appearDuration>0 … その区間は「予告点滅」表示（BulletRenderSystem.cs:270 の fadeIn 分岐）
//   appearDuration=0 … appearTime までは完全に非表示（同 :286）。この性質を使うと
//     1クリップの中に時間差の点滅を全部詰め込めるので、点滅のためにクリップを量産せずに済む。
//   life は必ず正の値にする（life<=0 は「寿命なし」扱いで消えなくなる）。
function warnClip(items, kind) {
  const bullets = items.map(function (it) {
    return bulletDefaults({
      originPos: { x: it.pos[0], y: it.pos[1] },
      typeName: 'warn_box',
      scale: { x: it.scale[0], y: it.scale[1] },
      color: { x: it.color[0], y: it.color[1], z: it.color[2], w: it.color[3] },
      appearTime: it.appearTime || 0,
      appearDuration: it.appearDuration || 0,
      life: it.life,
      unCounterable: true,
    });
  });
  return {
    parts: [{ offsetSec: 0, kind, buffer: { bullets, homing: false, isLaser: false }, spawner: NEUTRAL_SPAWNER() }],
  };
}

// --- パラメータ ------------------------------------------------------------
const WARN_BEATS = 0.5;    // タイル出現の予告リード（拍）＝半拍（v5 仕様の下限を満たす）
const EDGE_PER_BEAT = 2;   // 1拍あたりに端へ積む「残留タイル」数（爆破区間の消化数と対応）
const BLAST_PER_BEAT = 2;  // 1拍あたりの爆破数
const GAPS_PER_BEAT = 2;   // 毎拍必ず空ける 3x3 の逃げ場の数
const SPIN_RATE = 5.0;     // 放射弾の自転速度(rad/s)
const BULLET_SCALE = 0.3;  // v4: v3 の 0.6 の半分。verts も scale 倍されるので当たり判定も半分

// v5: 予告のパラメータ
const BLINK_COUNT = 3;             // 爆破の1拍前から光る回数（1拍の間に3回＝明暗が交互）
const BLINK_LEAD = beats(1);       // 点滅を始める時刻（爆破の1拍前）
const BLINK_DUTY = 0.5;            // 1周期のうち「明」の割合
const BLINK_SCALE = TILE * 1.25;   // タイルより一回り大きくして、描画順に依らず縁が光って見えるようにする
const PATH_WIDTH = CELL * 0.3;     // 経路帯の幅＝タイル幅の3割
const RING_LEAD = beats(0.5);      // 放射弾リング予告のリード（爆破の半拍前）
const RING_COUNT = 8;              // リングの点の数
const RING_RADIUS = 1.7;           // リングの半径
const RING_DOT = 0.42;             // リングの点の一辺
const SWEEP_LEAD = beats(1);       // 横断シャベルの経路予告のリード（発射の1拍前）
const MARK_SIZE = [1.1, 0.55];     // 方向マーク（来る側の端に置く短い四角）
const MARK_INSET = 0.75;           // マークの端からの距離
const MARK_HOLD = 0.25;            // シャベル発射後にマークが残る秒

const SHOVEL_SCALE = 2.6;      // 2x2 のタイルを叩くのにちょうどよい見た目（hummer の 3.5 より小さめ）
const SHOVEL_SPAWN_Y = 26;     // 画面上端(18)より上・カリング境界(36)より内側
const SHOVEL_FALL_SPEED = 24;  // 落下速度（一定）。飛来時間はタイルの高さで変わる
const SHOVEL_SIDE_SPEED = 26;  // 横断速度
const SHOVEL_LEFT_X = -1.5;    // カリング境界 x>=-2 の内側から出す
const SHOVEL_RIGHT_X = 33.5;

// shovel(): 等速直線で飛ぶシャベル1本（無重力・当たり判定なしの演出物）。
// 描画角は useVelocityAngle:false + initialAngle(rad) で明示指定する。
// stone_shovel.png は「刃を下に向けた」向きで描かれている＝回転0で下向きなので、
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
          typeName: 'stone_shovel',
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

// (2) 爆破されるタイルの点滅。
//     クリップ発火 = 爆破の BLINK_LEAD（1拍）前。1拍を BLINK_COUNT 等分し、各周期の前半だけ
//     明るいピンクを重ねる → 「明るいピンク⇔通常ピンク」が交互に見える。
//     色そのもののアニメは DSL に無いので、通常タイルの上に短寿命の明るい warn_box を
//     等間隔で重ねる方式（タイルより一回り大きくして縁が光る）。
function blinkWarn(cells, kind) {
  const step = BLINK_LEAD / BLINK_COUNT;
  const on = step * BLINK_DUTY;
  const items = [];
  cells.forEach(function (cell) {
    const c = cellCenter(cell[0], cell[1]);
    for (let k = 0; k < BLINK_COUNT; k++) {
      items.push({
        pos: c,
        scale: [BLINK_SCALE, BLINK_SCALE],
        color: PINK_FLASH,
        appearTime: k * step,     // ここまで非表示（appearDuration=0）
        appearDuration: 0,
        life: k * step + on,      // 明るいのは各周期の前半だけ
      });
    }
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
        color: PINK_WARN,
        appearTime: RING_LEAD,      // 予告点滅の窓に入れっぱなし（実体化しない）
        appearDuration: RING_LEAD,
        life: RING_LEAD,
      });
    }
  });
  return warnClip(items, kind);
}

// (3) 落下シャベルの経路予告。通る列（画面上端→対象タイル）に細い縦帯を出す。
//     クリップ発火 = シャベル発射の1拍前、life = dur でシャベル到達と同時に消える。
function dropPathWarn(center, dur, kind) {
  const top = ROWS * CELL;             // 画面上端 y=18
  const h = top - center[1];
  return warnClip(
    [{
      pos: [center[0], center[1] + h / 2],
      scale: [PATH_WIDTH, h],
      color: PINK_PATH,
      appearTime: dur,
      appearDuration: dur,
      life: dur,
    }],
    kind
  );
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
    // 区間⑧の末(33.333s)の直後。最後のシャベルが画面外へ抜けきる余裕を見て 34.5s。
    endTime: 34.5,
  },
  (s) => {
    const rng = makeRng(20260902);
    const lead = beats(WARN_BEATS);
    // 1拍あたり概ね 30/40/50 枚（v3 と同じ密度）。
    const FILL_RATE = D(0.25, 0.33, 0.40);

    // burst(): 爆破の放射弾。区間②④⑤⑦で共通に使う（弾数・速度の難易度比は v3 のまま）。
    function burst(center, index) {
      return spinBurst({
        pos: [center[0], center[1]],
        count: D(10, 12, 14),
        speed: D(7, 9, 11),
        type: 'box',
        life: 0,                          // 寿命なし＝画面外へ出て cull されるまで飛ぶ
        scale: [BULLET_SCALE, BULLET_SCALE],
        color: PINK_BULLET,
        angleOffset: rng() * 2 * Math.PI, // 爆破ごとに別オフセット角（rad）
        spin: SPIN_RATE * (index % 2 === 0 ? 1 : -1),
        kind: 'blast',
        unCounterable: true,
      });
    }

    // ======================================================================
    // タイル表示区間（区間①③）
    //   各拍頭で
    //     ・逃げ場 3x3 を GAPS_PER_BEAT 箇所ぶん確保（ここには絶対にタイルを置かない）
    //     ・残りのセルへ確率 fillRate でタイルを敷き詰める（→ 確率的な穴も多数残る）
    //     ・敷いたタイルはその拍の終わりで消滅（最終拍だけは holdUntil / onLastBeat で延命）
    //     ・端（外周フレーム）には別途「残留タイル」を積み、対応する爆破区間まで残す
    //   戻り値 stacked = 端に積んだ順（＝爆破する順）のタイル列
    // ======================================================================
    function tilePhase(cfg) {
      const usedEdge = new Set();
      const stacked = [];
      for (let i = 0; i < cfg.len; i++) {
        const strike = B(cfg.firstBeat + i);
        const blastTime = B(cfg.blastFirstBeat + i); // この拍に積んだ残留タイルが爆破される時刻
        const isLast = i === cfg.len - 1;

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

        // (b) 残留タイル: 端から左右1枚ずつ（左右対称に溜める）。逃げ場とは重ねない。
        const persist = [];
        [EDGE_LEFT, EDGE_RIGHT].forEach(function (pool) {
          const cand = pool.filter((c) => !usedEdge.has(key(c[0], c[1])) && !gapCells.has(key(c[0], c[1])));
          for (let n = 0; n < EDGE_PER_BEAT / 2 && cand.length > 0; n++) {
            const picked = cand.splice(Math.floor(rng() * cand.length), 1)[0];
            usedEdge.add(key(picked[0], picked[1]));
            persist.push(picked);
            stacked.push({ col: picked[0], row: picked[1] });
          }
        });

        // (c) タイル（逃げ場と端の残留タイル以外を確率で埋める）
        //     これまでに積んだ端タイル（usedEdge）を全部除外する。最終拍のタイルは
        //     爆破区間の間ずっと残るため、爆破対象のセルと重なると爆破後もタイルが残ってしまう。
        const transient = ALL_CELLS.filter(
          (c) => !gapCells.has(key(c[0], c[1])) && !usedEdge.has(key(c[0], c[1])) && rng() < cfg.fillRate
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

        // (e) 実体（濃いピンク）。最終拍だけは拍末で消さず holdUntil まで残す。
        //     cfg.onLastBeat がある場合（区間③）は、残すタイルの内訳を呼び出し側が決める。
        if (isLast && cfg.onLastBeat) {
          cfg.onLastBeat(transient, strike);
        } else {
          s.at(
            strike,
            tileField(transient, {
              type: 'stone_block',
              color: PINK_SOLID,
              life: isLast ? cfg.holdUntil - strike : beats(1) + SEAM_MARGIN,
              kind: isLast ? 'tilehold' : 'tile',
            })
          );
        }

        // (f) 実体（濃いピンク）: 端の残留タイル。対応する拍で爆破されるまで残る。
        //     継ぎ目余白を足さず BLAST_LEAD_OUT ぶん手前で消す（爆破中心にタイルを残さない）。
        s.at(
          strike,
          tileField(persist, {
            type: 'stone_block',
            color: PINK_SOLID,
            life: blastTime - strike - BLAST_LEAD_OUT,
            kind: 'tilekeep',
          })
        );
      }
      return stacked;
    }

    // ======================================================================
    // タイル爆破区間（区間②④）
    //   各拍で、積んだ順に端のタイルを BLAST_PER_BEAT 枚ずつ爆破する。
    //   ・爆破の半拍前に薄いピンクの予告
    //   ・爆破時刻ちょうどでタイルの life が尽き、同時に spinBurst（放射弾）が出る
    //   ・放射弾は無重力の等速直線。life:0 なので画面外へ抜けるまで消えない
    //   ・タイルを使い切ったあとの拍は静止（何も出さない）
    // ======================================================================
    function blastPhase(cfg) {
      const need = Math.ceil(cfg.stacked.length / BLAST_PER_BEAT);
      for (let b = 0; b < Math.min(cfg.len, need); b++) {
        const blastTime = B(cfg.firstBeat + b);
        const group = cfg.stacked.slice(b * BLAST_PER_BEAT, (b + 1) * BLAST_PER_BEAT);
        if (group.length === 0) continue;

        s.at(
          blastTime - beats(0.5),
          tileField(group.map((t) => [t.col, t.row]), {
            type: 'warn_box',
            color: PINK_WARN,
            appearTime: beats(0.5),
            appearDuration: beats(0.5),
            life: beats(0.5), // 爆破の瞬間ちょうどで消す（爆破中心に何も残さない）
            kind: 'blastwarn',
          })
        );

        // v5 (2): 爆破の1拍前から、対象タイルを1拍の間に3回点滅させる
        s.at(blastTime - BLINK_LEAD, blinkWarn(group.map((t) => [t.col, t.row]), 'blastblink'));

        const centers = group.map((t) => cellCenter(t.col, t.row));
        // v5 (5): 爆破の半拍前に、爆破中心へ放射弾のリング予告
        s.at(blastTime - RING_LEAD, ringWarn(centers, 'burstwarn'));

        centers.forEach(function (center) {
          s.at(blastTime, burst(center, b));
        });
      }
    }

    // ======================================================================
    // 1. 6.629s → B(16)=6.667s  タイル表示①（7拍）
    // 2. 9.694s → B(23)=9.583s  タイル爆破①（9拍。7拍で14枚を消化し2拍静止）
    //    区間末（＝区間③の頭 B(32)）で残ったタイルは消す（v3 どおり）。
    // ======================================================================
    const stackedA = tilePhase({
      firstBeat: S1_BEAT,
      len: S1_LEN,
      blastFirstBeat: S2_BEAT,
      // 「配置の総枚数は v3 のまま」＝1回の配置（1拍ぶんの敷き詰め）の枚数を v3 と同じに保つ。
      // 拍数だけ 4→7 に増やし、密度（fillRate）は v3 の値を据え置く。
      fillRate: FILL_RATE,
      holdUntil: B(S3_BEAT),
      pinStartGap: true,
    });
    blastPhase({ firstBeat: S2_BEAT, len: S2_LEN, stacked: stackedA });

    // ======================================================================
    // 3. 13.380s → B(32)=13.333s  タイル表示②（8拍・シードは rng の続き＝別配置）
    // 4. 16.783s → B(40)=16.667s  タイル爆破②（8拍で16枚）
    //    指示書 5 の前提により、区間末でタイルを消さず残す。残ったタイルが
    //    区間⑤⑦の爆破対象・区間⑥⑧のシャベル y 座標の供給源になる。
    // ======================================================================
    let heldCells = [];      // 区間⑤〜⑧の間ずっと画面に残るタイル
    let shovelTargets = [];  // ⑤⑦で爆破する 8 枚（先頭4枚=⑤ / 後半4枚=⑦）

    // ⑤⑦のシャベル到達（＝爆破）時刻。2拍おきに4回。
    const S5_IMPACTS = [0, 2, 4, 6].map((k) => B(S5_BEAT + k));
    const S7_IMPACTS = [0, 2, 4, 6].map((k) => B(S7_BEAT + k));
    const IMPACTS = S5_IMPACTS.concat(S7_IMPACTS);

    const stackedB = tilePhase({
      firstBeat: S3_BEAT,
      len: S3_LEN,
      blastFirstBeat: S4_BEAT,
      fillRate: FILL_RATE,
      pinStartGap: false,
      // 最終拍のタイルの内訳をここで決める（爆破対象8枚は個別クリップにして個別の life を持たせる）。
      onLastBeat: function (transient, strike) {
        // 爆破対象は「各列でいちばん上のタイル」から列が重複しないように選ぶ。
        // こうすると上から落とすシャベルが手前の別タイルを素通りして見えることがない。
        const topByCol = new Map();
        transient.forEach(function (c) {
          const cur = topByCol.get(c[0]);
          if (!cur || c[1] > cur[1]) topByCol.set(c[0], c);
        });
        const cand = Array.from(topByCol.values());
        const picked = [];
        for (let k = 0; k < IMPACTS.length && cand.length > 0; k++) {
          picked.push(cand.splice(Math.floor(rng() * cand.length), 1)[0]);
        }
        const pickedKeys = new Set(picked.map((c) => key(c[0], c[1])));
        const pool = transient.filter((c) => !pickedKeys.has(key(c[0], c[1])));
        shovelTargets = picked;
        heldCells = pool;

        // 爆破対象: 1枚1クリップ。シャベル到達の BLAST_LEAD_OUT 手前で消える。
        picked.forEach(function (cell, k) {
          s.at(
            strike,
            tileField([cell], {
              type: 'stone_block',
              color: PINK_SOLID,
              life: IMPACTS[k] - strike - BLAST_LEAD_OUT,
              kind: 'tiletarget',
            })
          );
        });

        // 残りのタイル: 区間⑧の末まで画面に残る。
        s.at(
          strike,
          tileField(pool, {
            type: 'stone_block',
            color: PINK_SOLID,
            life: B(END_BEAT) - strike,
            kind: 'tilehold',
          })
        );
      },
    });
    blastPhase({ firstBeat: S4_BEAT, len: S4_LEN, stacked: stackedB });

    // ======================================================================
    // シャベル爆破区間（区間⑤⑦）
    //   画面上からシャベルを1本ずつ落とし、対象タイルの中心に到達した瞬間に
    //   ・シャベルの life が尽きる（＝タイルの位置で消える）
    //   ・タイルの life が尽きる（tiletarget クリップ側で設定済み）
    //   ・spinBurst が出る
    //   DSL/ランタイムに衝突判定は無いため、到達時刻を逆算して3つを同時刻に置いている。
    // ======================================================================
    function shovelBlastPhase(impacts, targets, spinBase) {
      impacts.forEach(function (impact, k) {
        const cell = targets[k];
        if (!cell) return;
        const center = cellCenter(cell[0], cell[1]);
        const flight = (SHOVEL_SPAWN_Y - center[1]) / SHOVEL_FALL_SPEED;
        // v5 (3): 発射の1拍前から、シャベルが通る列に細い縦帯（到達＝爆破で消える）
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
        s.at(impact, burst(center, spinBase + k));
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

    function shovelSweepPhase(firstBeat) {
      for (let i = 0; i < 4; i++) {
        const t = B(firstBeat + i);
        if (heldRows.length === 0) continue;
        const leftRow = heldRows[Math.floor(rng() * heldRows.length)];
        const others = heldRows.filter((r) => r !== leftRow);
        const rightRow = others.length > 0 ? others[Math.floor(rng() * others.length)] : leftRow;

        // v5 (4): 横断シャベルの経路予告。
        //   ・通る行に横一杯の細い帯を、発射の1拍前からシャベルが画面外へ抜けるまで出す
        //   ・来る側の端に濃いめの短い四角（方向マーク）を置き、左右どちらから来るかを示す
        const sweepDur = SWEEP_LEAD + (SHOVEL_RIGHT_X - SHOVEL_LEFT_X) / SHOVEL_SIDE_SPEED;
        const sweepItems = [];
        [[leftRow, true], [rightRow, false]].forEach(function (entry) {
          const y = cellCenter(0, entry[0])[1];
          const fromLeft = entry[1];
          sweepItems.push({
            pos: [(COLS * CELL) / 2, y],
            scale: [COLS * CELL, PATH_WIDTH],
            color: PINK_PATH,
            appearTime: sweepDur,
            appearDuration: sweepDur,
            life: sweepDur,
          });
          sweepItems.push({
            pos: [fromLeft ? MARK_INSET : COLS * CELL - MARK_INSET, y],
            scale: MARK_SIZE,
            color: PINK_MARK,
            appearTime: SWEEP_LEAD,
            appearDuration: SWEEP_LEAD,
            life: SWEEP_LEAD + MARK_HOLD,  // シャベルが通り過ぎたら消える
          });
        });
        s.at(t - SWEEP_LEAD, warnClip(sweepItems, 'sweepwarn'));

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

    // 6. 23.189s → B(56)=23.333s  シャベル飛ばし①
    shovelSweepPhase(S6_BEAT);
    // 8. 30.161s → B(72)=30.000s  シャベル飛ばし②
    shovelSweepPhase(S8_BEAT);
  }
);
