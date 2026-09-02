// choreo/stone2.js — 新・石工（stone2）全編プロトタイプ v1
//
// 正典: Instructions/danmaku-lab/STONE2-DESIGN-DRAFT.md（7幕構成・パターン割付・難易度軸）。
// 本ファイルはその叩き台をそのまま choreo DSL に落とした最初の全編プロトタイプ。
// ユーザーレビュー後に数値・境界を直す前提（正典 §5 制作フロー 手順2）。
//
// 【正典 §6 未回答4点・本便での仮置き（要レビュー）】
//   1. 幕境界⚠: 正典の推定バー数（Act0=1-8, Act1=9-32, Act2=33-48, Act4=60-71, Act6=80-90）を
//      そのまま採用。Act3/Act5 開始は既知アンカー(79.961s / 118.307s)を優先。
//   2. Act1「積んだブロックが地形として残る」案を採用。Act2 で下から順（bandごと）に破砕する。
//   3. 拍手のタイミングは小節頭（強拍・beat1）に仮置き。
//   4. 全弾 unCounterable:true（旧石工の流儀踏襲）。
//
// 【拍グリッド規律】strike 時刻は 0.25拍グリッド（=BEAT/4）に量子化するか、正典の5アンカーの
// いずれかに厳密一致させる（tools/beat-quantization-check.mjs で機械確認・PROGRESS.md参照）。
// 複数 part を持つプリミティブ（wall/gravitySeq/fallBlock/chargeOrb/beam/volley/chase/drizzle等）
// は、内部オフセットもすべてグリッド倍数にする必要があるため、生アンカー値をベース時刻に
// 使わない（アンカーはグリッドに乗らないため、アンカー+グリッド量 はグリッドから外れる）。
// 5アンカーは「単一 part プリミティブ（ring/spiral/comet/telegraph等）」の演出キューにのみ、
// 正確な値で使う。付随する多 part 演出は最寄りのグリッド時刻で代用する。
//
// index.html?stage=stone2 で再生できる。マーカーボタンで Act0〜Act6 + 5アンカーへジャンプ可能。
//
// 【v2 磨き便（2026-07-16・同日）】親の検収所見4点に対応した差分修正。詳細・before/after数値は
// stone2-review/REVIEW-GUIDE.md の「v2 での変更点」節を参照。
//   (1) Act5フィナーレ: spiralCluster()を新設し6クラスタへ多重化(最重要)
//   (2) Act2拍手: ringを二重化して衝撃波感を強化
//   (3) Act1: LUNATIC限定でband3(最上段)に6本追加(E/N不変)
//   (4) Act4: check_safe_path.mjsで詰みチェック→検出なし・変更なし

import {
  stage, marker, D,
  ring, fan, spiral, comet, volley, chase, drizzle,
  gravitySeq, fallBlock, wall, cutter, telegraph, beam, chargeOrb, clear,
} from '../js/dsl.js';

// --- 拍グリッド ------------------------------------------------------------
const BPM = 144;
const BEAT = 60 / BPM;      // 0.41666667s
const BAR = BEAT * 4;       // 1.66666667s
const Q = BEAT / 4;         // 0.10416667s = 0.25拍グリッド刻み

// T(小節, 拍=1): 小節1拍1 = t=0（1始まり）。beat は 0.25 刻みで指定するとグリッドに厳密に乗る。
function T(bar, beat = 1) {
  return (bar - 1) * BAR + (beat - 1) * BEAT;
}
// beats(n): n拍ぶんの秒数（n は 0.25 刻み推奨。全ての内部オフセットに使う）。
function beats(n) {
  return n * BEAT;
}

// --- 曲の節目（正典 §1 既知アンカー・秒） -----------------------------------
const ANCHOR = {
  sectionChange: 79.961,   // Act3 開始（重力反転）
  wallClash: 95.239,       // へこみ壁 激突
  finaleBeam: 118.307,     // Act5 開始（chargeOrb→beam）
  finaleAvalanche: 128.364, // ブロック雪崩
  finaleSpiral: 131.805,   // spiral多重展開
};

// --- 幕頭（小節グリッド。⚠=正典推定値をそのまま採用） -----------------------
const ACT_START = {
  act0: T(1, 1),                // 0.000
  act1: T(9, 1),                 // 13.333 ⚠
  act2: T(33, 1),                // 53.333 ⚠
  act3: ANCHOR.sectionChange,    // 79.961（既知アンカー優先）
  act4: T(60, 1),                // 98.333 ⚠
  act5: ANCHOR.finaleBeam,       // 118.307（既知アンカー優先）
  act6: T(80, 1),                // 131.667 ⚠（アンカーfinaleSpiral 131.805とほぼ同時）
};

const STAGE_END = 150.022; // 音源長 02:30.022（stage-timing-instructions_20260715.md）

// --- ボス定位置（杖/手の発生源。拍手ringの原点はここに統一） -----------------
const BOSS_POS = [16, 15];

// --- Act1「地形として残す」ブロックの高さバンド＆Act2破砕タイミング ----------
// Act1 のブロックは band(0=低い〜3=高い) を持ち、Act2 でband昇順に破砕する。
const BAND_Y = [3.6, 6.4, 9.2, 12.0];
const BAND_SHATTER_TIME = [T(35, 1), T(39, 1), T(43, 1), T(47, 1)];

// ============================================================================
// 共通ヘルパー
// ============================================================================

// strike(): 単一partプリミティブの appearTime/appearDuration ネイティブ点滅を使った
// telegraph→strike。strikeTime に実体化・grid/anchor厳密一致。build(lead) は
// appearTime:lead, appearDuration:lead を含むプリミティブを返す関数。
function strike(s, strikeTime, leadBeats, build) {
  const lead = beats(leadBeats);
  s.at(strikeTime - lead, build(lead));
}

// dropBlock(): telegraph(着地点予告) → fallBlock(地形として残る)。
// holdDur は Act2 の band 破砕時刻から逆算し、Act2 の shard ring と同時に消滅させる
// （＝見た目上「破砕」される）。
function dropBlock(s, terrainBlocks, landBar, landBeat, x, band, opts = {}) {
  const {
    fromY = 17.5,
    fallBeats = 2,
    leadBeats = 1,
    scale = D([1.5, 1.5], [1.6, 1.6], [1.7, 1.7]),
  } = opts;
  const toY = BAND_Y[band];
  const land = T(landBar, landBeat);
  const dur = beats(fallBeats);
  const lead = beats(leadBeats);
  const fallStart = land - dur;
  const warnStart = fallStart - lead;
  const holdDur = BAND_SHATTER_TIME[band] - land;
  s.at(
    warnStart,
    telegraph({ pos: [x, toY], scale: [scale[0] * 1.15, scale[1] * 1.15], from: 0, until: lead + dur })
  );
  s.at(
    fallStart,
    fallBlock({ x, fromY, toY, dur, type: 'stone_block', scale, appearDuration: 0, holdDur, unCounterable: true })
  );
  terrainBlocks.push({ x, toY, band });
}

// updraftColumn(): Act3 反重力の「落下→静止→反転上昇→上端で砕ける」1本ぶん。
// 前半は accel で自然な重力反転の"間"を作り、後半は moveTo で正確に上端到達点へ運ぶ
// （accel だけで狙った終着点に合わせるのは過剰調整になるため moveTo に切替える設計）。
function updraftColumn(s, spawnTime, x, opts = {}) {
  const {
    fromY = 9,
    topY = 16.5,
    fallBeats = 1,
    riseBeats = 3,
    gDown = 16,
    scale = D([1.4, 1.4], [1.5, 1.5], [1.6, 1.6]),
  } = opts;
  const result = gravitySeq(
    { pos: [x, fromY], vel: [0, 0], type: 'stone_block', scale, appearDuration: beats(0.5), unCounterable: true },
    [
      { until: beats(fallBeats), accel: [gDown, -Math.PI / 2] },
      { until: beats(fallBeats) + beats(riseBeats), moveTo: [x, topY] },
    ]
  );
  s.at(spawnTime, result);
  const burstTime = spawnTime + beats(fallBeats) + beats(riseBeats);
  s.at(
    burstTime,
    ring({ pos: [x, topY], count: D(6, 8, 10), speed: D(3, 4, 5), type: 'stone_shard', life: beats(2), scale: [0.7, 0.7], unCounterable: true })
  );
}

// spiralCluster(): Act5 フィナーレ「銀河状スパイラルクラスタ多重展開」用の共通呼び出し。
// tokyo-skies inventory #15（複数の銀河状スパイラルが同時多発し画面ほぼ全域を覆う）に寄せて、
// pos違いの複数クラスタを近い時刻にstaggerして重ねる（v2で追加。既存spiral呼び出しはこの
// ヘルパー経由に統一しただけで挙動は変えていない）。
function spiralCluster(s, t, pos, opts) {
  const {
    count, turns, radiusStart = 0.5, radiusEnd, expandSpeed, spin,
    life = beats(6), type = 'stone_burst', scale = [0.6, 0.6], angleOffset = 0,
  } = opts;
  s.at(t, spiral({ pos, count, turns, radiusStart, radiusEnd, expandSpeed, spin, type, life, scale, angleOffset, unCounterable: true }));
}

// avalancheBlock(): Act5 雪崩用の速いdropBlock（地形化しない・holdDur短命）。
function avalancheBlock(s, landBar, landBeat, x, opts = {}) {
  const {
    fromY = 17.5,
    toY = 3.2,
    fallBeats = 1,
    leadBeats = 0.5,
    scale = D([1.5, 1.5], [1.7, 1.7], [1.9, 1.9]),
  } = opts;
  const land = T(landBar, landBeat);
  const dur = beats(fallBeats);
  const lead = beats(leadBeats);
  const fallStart = land - dur;
  const warnStart = fallStart - lead;
  s.at(warnStart, telegraph({ pos: [x, toY], scale: [scale[0] * 1.15, scale[1] * 1.15], from: 0, until: lead + dur }));
  s.at(
    fallStart,
    fallBlock({ x, fromY, toY, dur, type: 'stone_block', scale, appearDuration: 0, holdDur: beats(1.5), unCounterable: true })
  );
}

// ============================================================================
// マーカー: Act0〜Act6 幕頭 + 正典5アンカー（プレイグラウンドのボタンから幕単位レビュー可）
// ============================================================================
const MARKERS = {
  1: ACT_START.act0,
  2: ACT_START.act1,
  3: ACT_START.act2,
  4: ACT_START.act3,
  5: ACT_START.act4,
  6: ACT_START.act5,
  7: ACT_START.act6,
  8: ANCHOR.sectionChange,
  9: ANCHOR.wallClash,
  10: ANCHOR.finaleBeam,
  11: ANCHOR.finaleAvalanche,
  12: ANCHOR.finaleSpiral,
};

export default stage(
  {
    name: 'stone2',
    music: 'Assets/StageData/stone/stone.mp3',
    bpm: BPM,
    offset: 0,
    markers: MARKERS,
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
    endTime: STAGE_END,
  },
  (s) => {
    const terrainBlocks = [];

    // ========================================================================
    // Act0 開場（0:00〜0:13・bar1-8）— 静かな作業場・低密度・杖だけ
    // ========================================================================
    {
      // 石屑が漂う（decorative・harmless。type:stone_dust は player.js HARMLESS_TYPES）
      const dustBars = [1, 3, 5, 7];
      dustBars.forEach((bar, i) => {
        s.at(
          T(bar, 1),
          drizzle({
            area: [4, 9, 28, 17],
            count: D(3, 4, 5),
            speedMin: 0.3,
            speedMax: 0.6,
            angleMin: 210,
            angleMax: 330,
            type: 'stone_dust',
            life: beats(6),
            spawnSpread: 0,
            seed: i + 1,
            unCounterable: true,
          })
        );
      });

      // 杖タップ（4小節ごとの明滅・純粋に演出）
      s.at(T(1, 1), telegraph({ pos: BOSS_POS, scale: [2, 2], from: 0, until: beats(1.5) }));
      s.at(T(5, 1), telegraph({ pos: BOSS_POS, scale: [2, 2], from: 0, until: beats(1.5) }));

      // 練習ゾーンのfan（8拍=2小節ごと・遅い・当たっても許容）
      strike(s, T(3, 1), 4, (lead) =>
        fan({ pos: BOSS_POS, count: D(3, 4, 5), spreadDeg: 40, centerDeg: 260, speed: D(2.5, 3, 3.2), type: 'stone_burst', life: beats(6), appearTime: lead, appearDuration: lead, unCounterable: true })
      );
      strike(s, T(5, 3), 4, (lead) =>
        fan({ pos: BOSS_POS, count: D(3, 4, 5), spreadDeg: 40, centerDeg: 280, speed: D(2.5, 3, 3.2), type: 'stone_burst', life: beats(6), appearTime: lead, appearDuration: lead, unCounterable: true })
      );
      strike(s, T(7, 1), 4, (lead) =>
        fan({ pos: BOSS_POS, count: D(3, 4, 5), spreadDeg: 40, centerDeg: 270, speed: D(2.5, 3, 3.2), type: 'stone_burst', life: beats(6), appearTime: lead, appearDuration: lead, unCounterable: true })
      );
    }

    // ========================================================================
    // Act1 石積み（0:13〜0:53・bar9-32）— 拍同期fallBlock。地形として残す
    // ========================================================================
    {
      // group0 row @ band0（bar9-12）
      [7, 13, 19, 25].forEach((x, i) => dropBlock(s, terrainBlocks, 9 + i, 1, x, 0));
      [10, 16, 22, 28].forEach((x, i) => {
        if (D(false, false, true)) dropBlock(s, terrainBlocks, 9 + i, 3, x, 0);
      });

      // group1 stair @ band1→band2（bar13-16）
      const stair1X = [7, 13, 19, 25];
      const stair1Band = [1, 1, 2, 2];
      stair1X.forEach((x, i) => dropBlock(s, terrainBlocks, 13 + i, 1, x, stair1Band[i]));
      const stair1ExtraX = [10, 16, 22];
      const stair1ExtraBand = [1, 2, 2];
      stair1ExtraX.forEach((x, i) => {
        if (D(false, false, true)) dropBlock(s, terrainBlocks, 13 + i, 3, x, stair1ExtraBand[i]);
      });

      // group2 tower @ x=16（bar17-20・band0〜3 縦に積む）
      [0, 1, 2, 3].forEach((band, i) => dropBlock(s, terrainBlocks, 17 + i, 1, 16, band));
      // L: 両脇に控えめな双子タワー（band0-1）
      [13, 19].forEach((x) => {
        [0, 1].forEach((band, i) => {
          if (D(false, false, true)) dropBlock(s, terrainBlocks, 17 + i, 3, x, band);
        });
      });
      // L: band3(最上段)が手薄なので、beat2/4のオフビートに追加のガレキを畳みかけて
      // 積みの密度・リズムの見応えを一段強化する（E/Nは据え置き。他groupのband3使用位置
      // と重ならないx=10/22を選定）。
      if (D(false, false, true)) {
        dropBlock(s, terrainBlocks, 18, 2, 10, 3);
        dropBlock(s, terrainBlocks, 19, 4, 22, 3);
      }

      // group3 row @ band2（bar21-24）
      [9, 15, 21, 27].forEach((x, i) => dropBlock(s, terrainBlocks, 21 + i, 1, x, 2));
      [12, 18, 24, 6].forEach((x, i) => {
        if (D(false, false, true)) dropBlock(s, terrainBlocks, 21 + i, 3, x, 2);
      });
      // L: band3の追加ガレキ第2弾（group2の続き。オフビートの畳みかけ）
      if (D(false, false, true)) {
        dropBlock(s, terrainBlocks, 23, 2, 25, 3);
        dropBlock(s, terrainBlocks, 24, 4, 28, 3);
      }

      // group4 stair @ band3→band0（bar25-28・降りる階段）
      const stair2X = [7, 13, 19, 25];
      const stair2Band = [3, 2, 1, 0];
      stair2X.forEach((x, i) => dropBlock(s, terrainBlocks, 25 + i, 1, x, stair2Band[i]));
      const stair2ExtraX = [10, 16, 22];
      const stair2ExtraBand = [2, 1, 0];
      stair2ExtraX.forEach((x, i) => {
        if (D(false, false, true)) dropBlock(s, terrainBlocks, 25 + i, 3, x, stair2ExtraBand[i]);
      });
      // L: band3の追加ガレキ第3弾（画面端寄り。band3を7,10,13,16,19,22,25,28,31とほぼ埋める）
      if (D(false, false, true)) {
        dropBlock(s, terrainBlocks, 26, 2, 4, 3);
        dropBlock(s, terrainBlocks, 27, 4, 31, 3);
      }

      // group5 tower finale（bar29-32・双子タワー、Lで中央タワー追加）
      [13, 19].forEach((x) => {
        [0, 1, 2, 3].forEach((band, i) => dropBlock(s, terrainBlocks, 29 + i, 1, x, band));
      });
      [0, 1, 2, 3].forEach((band, i) => {
        if (D(false, false, true)) dropBlock(s, terrainBlocks, 29 + i, 3, 16, band);
      });

      // 幕の締め: wall（gap大きめ・Act4の伏線・脅威度は低い）
      s.at(
        T(31, 1),
        wall({ side: 'left', gapY: 9, gapH: D(11, 10, 9), speed: D(6, 7, 8), warnLead: beats(2), type: 'stone_block', unCounterable: true })
      );
    }

    // ========================================================================
    // Act2 開眼（0:53〜1:20・bar33-48→アンカー79.961）— 拍手=ring・地形を破砕
    // ========================================================================
    {
      // 拍手: 2小節に1回(bar33,35,37,39)→毎小節(bar41-48)へ加速
      // v2: 「衝撃波としての重さ」を出すため二重リング化（速い外周弾+遅く大きい後続リング）。
      const clapBars = [33, 35, 37, 39, 41, 42, 43, 44, 45, 46, 47, 48];
      clapBars.forEach((bar) => {
        strike(s, T(bar, 1), 2, (lead) => [
          ring({ pos: BOSS_POS, count: D(18, 24, 32), speed: D(5, 6, 7), type: 'stone_burst', life: beats(4), scale: [0.9, 0.9], appearTime: lead, appearDuration: lead, unCounterable: true }),
          ring({ pos: BOSS_POS, count: D(9, 12, 15), speed: D(3, 3.5, 4), type: 'stone_burst', life: beats(4.5), scale: [1.4, 1.4], angleOffset: 12, appearTime: lead, appearDuration: lead, unCounterable: true }),
        ]);
      });

      // 拍手の合間: comet(斜め投石)×2 + volley(3連投)×1
      strike(s, T(34, 1), 1, (lead) =>
        comet({ pos: BOSS_POS, angleDeg: 210, speed: D(5, 6, 7), trailCount: D(3, 4, 5), life: beats(4), appearTime: lead, appearDuration: lead, unCounterable: true })
      );
      strike(s, T(36, 1), 1, (lead) =>
        comet({ pos: BOSS_POS, angleDeg: 330, speed: D(5, 6, 7), trailCount: D(3, 4, 5), life: beats(4), appearTime: lead, appearDuration: lead, unCounterable: true })
      );
      strike(s, T(38, 1), 1, (lead) =>
        volley({
          points: [[13, 14], [15, 13], [17, 12]],
          intervalSec: beats(0.5),
          speed: D(4, 5, 6),
          life: beats(3),
          appearTime: lead,
          appearDuration: lead,
          type: 'stone_burst',
          unCounterable: true,
        })
      );

      // 地形破砕: band 昇順（下から順）に、band内の全ブロックを一斉に stone_shard 化
      for (let band = 0; band < BAND_Y.length; band++) {
        const t = BAND_SHATTER_TIME[band];
        terrainBlocks
          .filter((b) => b.band === band)
          .forEach((b) => {
            s.at(
              t,
              ring({ pos: [b.x, b.toY], count: D(4, 5, 6), speed: D(3, 4, 5), type: 'stone_shard', life: beats(3), scale: [0.7, 0.7], unCounterable: true })
            );
          });
      }

      // 62.5s付近の「手の登場」は Unity側ボススプライト演出（DSL対象外・正典§4）
    }

    // ========================================================================
    // Act3 反重力（79.961〜98.333）— 落下方向反転・へこみ壁
    // ========================================================================
    {
      // アンカー: 反転の瞬間（単一partの衝撃波キュー）
      s.at(
        ANCHOR.sectionChange,
        ring({ pos: [16, 9], count: D(12, 16, 20), speed: D(4, 5, 6), type: 'stone_burst', life: beats(2), unCounterable: true })
      );
      s.at(ANCHOR.sectionChange, clear());

      // 反重力の更新柱（落下→静止→反転上昇→上端で砕ける）4波
      const waves = [
        { bar: 49, xs: [8, 16, 24], extraXs: [12, 20] },
        { bar: 51, xs: [6, 16, 26], extraXs: [11, 21] },
        { bar: 53, xs: [10, 16, 22], extraXs: [13, 19] },
        { bar: 55, xs: [8, 14, 20, 26], extraXs: [11, 17, 23] },
      ];
      waves.forEach((w) => {
        w.xs.forEach((x, i) => updraftColumn(s, T(w.bar, 1 + i), x));
        w.extraXs.forEach((x, i) => {
          if (D(false, false, true)) updraftColumn(s, T(w.bar, 1.5 + i), x);
        });
      });

      // 上向きドリズル（重力反転の間、石屑が上に降る）
      [50, 54, 58].forEach((bar, i) => {
        s.at(
          T(bar, 1),
          drizzle({ area: [4, 2, 28, 10], count: D(4, 5, 6), speedMin: 0.3, speedMax: 0.6, angleMin: 70, angleMax: 110, type: 'stone_dust', life: beats(6), spawnSpread: 0, seed: 20 + i, unCounterable: true })
        );
      });

      // へこみ壁: 左右同時収束（予告→激突。アンカーwallClashは単一partの衝撃キューで流用）
      s.at(
        T(57, 1),
        [
          wall({ side: 'left', gapY: D(11, 8, 7), gapH: D(9, 6, 4.5), speed: D(7, 8, 9), warnLead: beats(3), type: 'stone_block', unCounterable: true }),
          wall({ side: 'right', gapY: D(11, 8, 7), gapH: D(9, 6, 4.5), speed: D(7, 8, 9), warnLead: beats(3), type: 'stone_block', unCounterable: true }),
        ]
      );
      s.at(
        ANCHOR.wallClash,
        ring({ pos: [16, 9], count: D(10, 14, 18), speed: D(5, 6, 7), type: 'stone_shard', life: beats(2), unCounterable: true })
      );
    }

    // ========================================================================
    // Act4 ゲートレット（98.333〜118.307）— wall(top/bottom/left/right)交互+cutter巡回
    // ========================================================================
    {
      const ACT4_GATES = [
        { bar: 60, side: 'top', center: 10 },
        { bar: 61, side: 'left', center: 12 },
        { bar: 62, side: 'bottom', center: 16 },
        { bar: 63, side: 'right', center: 6 },
        { bar: 64, side: 'top', center: 22 },
        { bar: 65, side: 'left', center: 9 },
        { bar: 66, side: 'bottom', center: 14 },
        { bar: 67, side: 'right', center: 11 },
        { bar: 68, side: 'top', center: 20 },
        { bar: 69, side: 'left', center: 7 },
        { bar: 70, side: 'bottom', center: 12 },
        { bar: 71, side: 'right', center: 13 },
      ];
      ACT4_GATES.forEach((g) => {
        const t = T(g.bar, 1);
        const warnLead = D(1.5 * BAR, BAR, BAR); // Eは間隔(=telegraphリード)1.5倍
        const gapSize = D(9, 6.5, 4.5);
        const horizontal = g.side === 'top' || g.side === 'bottom';
        const opts = { side: g.side, speed: D(6, 7.5, 9), warnLead, type: 'stone_block', unCounterable: true };
        if (horizontal) {
          opts.gapX = g.center;
          opts.gapW = gapSize;
        } else {
          opts.gapY = g.center;
          opts.gapH = gapSize;
        }
        s.at(t - warnLead, wall(opts));
      });

      // 固定巡回カッター（予告→出現。旧石工の「突然表示するな」指摘を踏襲）
      s.at(T(60, 1) - beats(1), telegraph({ pos: [16, 9], scale: [9, 9], from: 0, until: beats(1) }));
      s.at(
        T(60, 1),
        cutter({
          pos: [16, 9],
          spin: D(2, 3, 4),
          scale: D([7, 7], [8, 8], [9, 9]),
          unCounterable: true,
          path: [
            { until: 3 * BAR, moveTo: [8, 6] },
            { until: 6 * BAR, moveTo: [24, 12] },
            { until: 9 * BAR, moveTo: [8, 12] },
            { until: 11 * BAR, moveTo: [16, 9] },
          ],
        })
      );
    }

    // ========================================================================
    // Act5 フィナーレ（118.307〜131.805）— chargeOrb→beam / 雪崩 / spiral多重
    // ========================================================================
    {
      // 118.307: 単一partの「発動」キュー(アンカー厳密) + chargeOrb(最寄りグリッド)
      s.at(
        ANCHOR.finaleBeam,
        spiral({ pos: BOSS_POS, count: D(10, 14, 18), turns: 1, radiusStart: 0.6, radiusEnd: 0.6, expandSpeed: 0, spin: D(3, 4, 5), type: 'stone_burst', life: beats(2), scale: [0.5, 0.5], unCounterable: true })
      );
      s.at(
        T(72, 1),
        chargeOrb({ pos: BOSS_POS, growDur: beats(2), holdDur: beats(1), steps: 4, startScale: 0.3, finalScale: D(2.5, 3, 3.5), type: 'stone_flash', unCounterable: true })
      );
      [210, 270, 330].forEach((angleDeg) => {
        s.at(
          T(72, 4),
          beam({ pos: BOSS_POS, angleDeg, length: 18, thickness: 0.7, telegraphDur: beats(1), pulseCount: D(3, 4, 5), pulseIntervalSec: beats(0.5), type: 'stone_flash', unCounterable: true })
        );
      });

      // 128.364: ブロック雪崩 + 重力が元に戻る(下向きdrizzle再開)
      s.at(
        ANCHOR.finaleAvalanche,
        ring({ pos: [16, 16], count: D(8, 10, 12), speed: D(4, 5, 6), type: 'stone_shard', life: beats(2), unCounterable: true })
      );
      const avalancheXs = [6, 11, 16, 21, 26, 16];
      const avalancheBeats = [
        [78, 1], [78, 2], [78, 3], [78, 4], [79, 1], [79, 2],
      ];
      avalancheXs.forEach((x, i) => {
        avalancheBlock(s, avalancheBeats[i][0], avalancheBeats[i][1], x);
        if (D(false, false, true)) avalancheBlock(s, avalancheBeats[i][0], avalancheBeats[i][1], x + 2 > 29 ? x - 2 : x + 2);
      });
      s.at(
        T(79, 3),
        drizzle({ area: [4, 10, 28, 17], count: D(5, 6, 8), speedMin: 0.4, speedMax: 0.8, angleMin: 250, angleMax: 290, type: 'stone_dust', life: beats(4), spawnSpread: 0, seed: 41, unCounterable: true })
      );

      // 131.805: 銀河状スパイラルクラスタ多重展開の弾幕カーテン（v2で強化。tokyo-skies #15
      // 「複数の銀河状スパイラルが同時多発し画面ほぼ全域を覆う」に寄せ、pos違いの6クラスタを
      // 131.667(Act6 clear直後)〜132.2s の間でstaggerして展開する。各クラスタは単一partの
      // spiral()なのでアンカー/グリッドどちらの時刻でも良い（ここでは重ならないよう各クラスタ
      // 別々のグリッド刻みに置く＝1フレームあたりの新規スポーン数を抑えてスパイク判定を回避）。
      // life はクラスタごとに「curtainEnd(Act6のdrizzle開始 T(81,1)の半拍前)で揃って消える」
      // よう逆算する（旧v1の「spawn直後から短命→次の静けさへ自然消滅」という設計を保ち、
      // 全クラスタが同時に画面を覆いきった状態を作ってから一斉に晴れる）。
      // 自機の初期位置(16,2.4)付近の下部帯(y<8)にはクラスタ中心を置かず、逃げ道を残す
      // （check_safe_path.mjs で全難易度・全フレーム到達可能セル>0を確認済み。PROGRESS.md参照）。
      const curtainEnd = T(81, 1) - beats(0.5);
      const clusterSpawns = [
        { t: ANCHOR.finaleSpiral, pos: BOSS_POS },
        { t: T(80, 1.25), pos: [8, 13] },
        { t: T(80, 1.5), pos: [24, 13] },
        { t: T(80, 1.75), pos: BOSS_POS },
        { t: T(80, 2), pos: [5, 10] },
        { t: T(80, 2.25), pos: [27, 10] },
      ];
      spiralCluster(s, clusterSpawns[0].t, clusterSpawns[0].pos, {
        count: D(90, 135, 195), turns: D(2.5, 3, 3.5), radiusStart: 0.5, radiusEnd: D(9, 11, 13),
        expandSpeed: D(3, 4, 5), spin: D(1, 1.5, 2), life: curtainEnd - clusterSpawns[0].t, scale: [0.65, 0.65], angleOffset: 0,
      });
      spiralCluster(s, clusterSpawns[1].t, clusterSpawns[1].pos, {
        count: D(65, 95, 140), turns: D(2, 2.5, 3), radiusStart: 0.4, radiusEnd: D(6, 7, 8),
        expandSpeed: D(2.5, 3, 3.5), spin: D(1.2, 1.6, 2), life: curtainEnd - clusterSpawns[1].t, scale: [0.6, 0.6], angleOffset: 80,
      });
      spiralCluster(s, clusterSpawns[2].t, clusterSpawns[2].pos, {
        count: D(65, 95, 140), turns: D(2, 2.5, 3), radiusStart: 0.4, radiusEnd: D(6, 7, 8),
        expandSpeed: D(2.5, 3, 3.5), spin: D(-1.2, -1.6, -2), life: curtainEnd - clusterSpawns[2].t, scale: [0.6, 0.6], angleOffset: 160,
      });
      spiralCluster(s, clusterSpawns[3].t, clusterSpawns[3].pos, {
        count: D(75, 110, 165), turns: D(2, 2.5, 3), radiusStart: 1.2, radiusEnd: D(6, 7, 8),
        expandSpeed: D(2.5, 3, 3.5), spin: D(-1, -1.5, -2), life: curtainEnd - clusterSpawns[3].t, scale: [0.6, 0.6], angleOffset: 40,
      });
      spiralCluster(s, clusterSpawns[4].t, clusterSpawns[4].pos, {
        count: D(60, 85, 125), turns: D(1.5, 2, 2.5), radiusStart: 0.4, radiusEnd: D(5, 6, 7),
        expandSpeed: D(2, 2.5, 3), spin: D(1, 1.3, 1.6), life: curtainEnd - clusterSpawns[4].t, scale: [0.55, 0.55], angleOffset: 200,
      });
      spiralCluster(s, clusterSpawns[5].t, clusterSpawns[5].pos, {
        count: D(60, 85, 125), turns: D(1.5, 2, 2.5), radiusStart: 0.4, radiusEnd: D(5, 6, 7),
        expandSpeed: D(2, 2.5, 3), spin: D(-1, -1.3, -1.6), life: curtainEnd - clusterSpawns[5].t, scale: [0.55, 0.55], angleOffset: 280,
      });
      // 装飾用のdust渦（無害・当たり判定なし。全体のきらめきを足すだけ）。curtainEndより少し前で消える。
      s.at(
        T(80, 2.5),
        spiral({ pos: BOSS_POS, count: D(12, 16, 20), turns: 1, radiusStart: 0.5, radiusEnd: D(7, 8, 9), expandSpeed: D(3, 4, 5), spin: D(2, 2.5, 3), type: 'stone_dust', life: curtainEnd - T(80, 2.5) - beats(0.5), scale: [0.6, 0.6], unCounterable: true })
      );
    }

    // ========================================================================
    // Act6 閉場（131.667〜150.022）— 攻撃なし。石屑が舞い落ちる
    // ========================================================================
    {
      s.at(T(80, 1), clear());
      const tailBars = [81, 83, 85, 87, 89];
      tailBars.forEach((bar, i) => {
        const countBase = 5 - i; // だんだん静かになる
        s.at(
          T(bar, 1),
          drizzle({
            area: [4, 9, 28, 17],
            count: D(Math.max(2, countBase), Math.max(3, countBase + 1), Math.max(4, countBase + 2)),
            speedMin: 0.2,
            speedMax: 0.5,
            angleMin: 220,
            angleMax: 320,
            type: 'stone_dust',
            life: beats(8),
            spawnSpread: 0,
            seed: 60 + i,
            unCounterable: true,
          })
        );
      });
    }
  }
);
