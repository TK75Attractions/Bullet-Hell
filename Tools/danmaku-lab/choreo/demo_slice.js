// choreo/demo_slice.js — SPEC-PHASE2 §4 合格ゲート4 のデモ振り付け
//
// 「石工の一部（落下→重力反転上昇→右流れ→爆破 + へこみ壁1回 + リング）」を
// choreo DSL で書けることの実証。振り付けの良し悪しはこの便の合否に含めない
// （SPEC-PHASE2 §4-4）。数値は説明用の作例で、本物の石工チャートの値ではない。
//
// index.html?stage=demo_slice で再生できる（player.js が choreo/ モジュールを
// 優先的に import し、無ければ Assets/StageData の JSON にフォールバックする）。

import { stage, marker, D, gravitySeq, ring, wall } from '../js/dsl.js';

const MARKERS = { 1: 1.0, 2: 7.0, 3: 12.0 };

// gravitySeq 呼び出し全体の長さ（区間 until の最終値）。爆破の発火時刻を
// 「マーカー1 + この長さ」として組み立てるための定数。
const FALL_SEQUENCE_DURATION = 3.6;

export default stage(
  {
    name: 'demo_slice',
    music: 'Assets/StageData/stone/stone.mp3',
    bpm: 144,
    offset: 0,
    markers: MARKERS,
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
  },
  (s) => {
    // 1) 落下 → 重力反転上昇 → 右流れ → 保持（gravitySeq 1呼び出しで完結）
    s.at(
      marker(1),
      gravitySeq(
        {
          pos: [16, 14],
          vel: [0, 0],
          type: 'stone_block',
          scale: D([1.4, 1.4], [1.6, 1.6], [1.8, 1.8]),
          color: [1, 1, 1, 1],
          appearDuration: 0.4,
        },
        [
          { until: 0.8, accel: [D(20, 25, 30), -Math.PI / 2] }, // 落下
          { until: 1.6, accel: [D(30, 37, 50), Math.PI / 2] }, // 重力反転して急上昇
          { until: 2.8, moveTo: [26, 12] }, // 右流れ
          { until: FALL_SEQUENCE_DURATION, moveTo: [26, 12] }, // 保持（同点=速度ゼロ）
        ]
      )
    );

    // 2) 爆破: 保持地点(26,12)で破片リング
    s.at(
      MARKERS[1] + FALL_SEQUENCE_DURATION,
      ring({ pos: [26, 12], count: D(8, 12, 16), speed: D(5, 6, 7), type: 'stone_shard', life: 2.0, scale: [0.8, 0.8] })
    );

    // 3) へこみ壁（左から1回・中央付近に隙間）
    s.at(marker(2), wall({ side: 'left', gapY: 9, gapH: D(6, 5, 4), speed: 8, warnLead: 1.0 }));

    // 4) リング1回（単独プリミティブの実証）
    s.at(marker(3), ring({ pos: [16, 9], count: D(10, 14, 18), speed: 5, type: 'stone_burst', life: 3 }));
  }
);
