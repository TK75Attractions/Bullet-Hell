// choreo/jsab_study.js — SPEC-PHASE3 §5 実証デモ
//
// JSAB「Born Survivor」(Shirobon) 0:09〜0:40（約31秒）の模写。
// reference-analysis/born-survivor-inventory.md のパターン#1(ロータコーン→コメット)・
// #3(巨大玉 膨張)・#7(S字ビーズ掃引)・#8(扇状バースト)・#9(ビーム予告→本体) に対応。
// 音源合わせは行わず、inventory のタイムスタンプをそのままマーカーの相対秒として
// 使う拍グリッド相当の代用（SPEC-PHASE3 §5 が認める範囲）。振り付けの巧拙は本便の
// 合否に含めない（DSL で書けることの実証が目的）。
//
// index.html?stage=jsab_study で再生できる。

import { stage, marker, D, spiral, comet, chargeOrb, volley, beam, fan } from '../js/dsl.js';

// mm:ss はコメントに残す born-survivor-inventory.md 上の実時刻（0:09を相対0.0とする）。
const MARKERS = {
  1: 0.0, // 0:09 ロータコーン→コメット (#1)
  2: 5.0, // 0:14 巨大玉 膨張 (#3)
  3: 7.0, // 0:16 S字ビーズ掃引 (#7)
  4: 10.0, // 0:19 ビーム予告(白)→本体 (#9)
  5: 11.0, // 0:20 巨大玉 膨張(2) (#3)
  6: 15.0, // 0:24 S字ビーズ掃引(2) (#7)
  7: 23.0, // 0:32 巨大玉 膨張(3) (#3)
  8: 27.0, // 0:36 大きなS字ビーズ掃引 (#7)
  9: 31.0, // 0:40 扇状バースト (#8)
};

export default stage(
  {
    name: 'jsab_study',
    music: '',
    bpm: 132, // born-survivor-inventory.md の目視推定「BPM 130〜170」の下寄りを仮採用
    offset: 0,
    markers: MARKERS,
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
  },
  (s) => {
    // 1) ロータコーン→コメット（#1）: spiral でコーンの重なりを見せてから2方向へ comet 射出
    s.at(marker(1), spiral({ pos: [8, 14], count: 10, turns: 1.2, radiusStart: 0.4, radiusEnd: 1.8, spin: D(2, 3, 4), life: 1.0, scale: [0.6, 0.6] }));
    s.at(MARKERS[1] + 0.4, comet({ pos: [8, 14], angleDeg: 200, speed: D(6, 7, 8), trailCount: 3, life: 2.0 }));
    s.at(MARKERS[1] + 0.4, comet({ pos: [24, 5], angleDeg: 20, speed: D(6, 7, 8), trailCount: 3, life: 2.0 }));

    // 2) 巨大玉 膨張（#3）
    s.at(marker(2), chargeOrb({ pos: [22, 13], growDur: 0.35, holdDur: D(1.0, 1.3, 1.6), steps: 4, finalScale: 2.3 }));

    // 3) S字ビーズ掃引（#7）
    s.at(marker(3), volley({ points: [[2, 15], [5, 13], [7, 10], [8, 7], [7, 4]], intervalSec: 0.12, speed: D(4, 5, 6), life: 1.4 }));

    // 4) ビーム予告→本体（#9）: 上下2本
    s.at(marker(4), beam({ pos: [16, 3], angleDeg: 0, length: 26, thickness: 0.5, telegraphDur: 0.5, pulseCount: D(3, 4, 5), pulseIntervalSec: 0.22 }));
    s.at(marker(4), beam({ pos: [16, 15], angleDeg: 0, length: 26, thickness: 0.5, telegraphDur: 0.5, pulseCount: D(3, 4, 5), pulseIntervalSec: 0.22 }));

    // 5) 巨大玉 膨張(2)（#3）
    s.at(marker(5), chargeOrb({ pos: [9, 9], growDur: 0.35, holdDur: D(1.0, 1.3, 1.6), steps: 4, finalScale: 2.3 }));

    // 6) S字ビーズ掃引(2、逆向き)（#7）
    s.at(marker(6), volley({ points: [[30, 4], [27, 6], [25, 9], [24, 12], [25, 15]], intervalSec: 0.12, speed: D(4, 5, 6), life: 1.4 }));

    // 7) 巨大玉 膨張(3)（#3）
    s.at(marker(7), chargeOrb({ pos: [16, 9], growDur: 0.35, holdDur: D(1.0, 1.3, 1.6), steps: 4, finalScale: 2.6 }));

    // 8) 大きなS字ビーズ掃引（#7）
    s.at(marker(8), volley({ points: [[3, 3], [7, 5], [10, 9], [10, 13], [7, 16], [3, 17]], intervalSec: 0.12, speed: D(4, 5, 6), life: 1.8 }));

    // 9) 扇状バースト（#8）
    s.at(marker(9), fan({ pos: [16, 9], count: D(9, 12, 15), spreadDeg: 140, centerDeg: 90, speed: D(4, 5, 6), staggerSec: 0.08, life: 2.0 }));
  }
);
