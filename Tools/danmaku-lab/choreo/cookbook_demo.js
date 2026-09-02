// choreo/cookbook_demo.js — PATTERN-COOKBOOK.md 用のスクリーンショット撮影台本
//
// 全プリミティブ（Phase2既存 + Phase3拡充）を1つずつ、4秒間隔で独立して発火する。
// 振り付けとしての意味はなく、各パターンを単独で見やすく撮影するためだけの並び。
// PATTERN-COOKBOOK.md の「最小例」はここに書いた引数をそのまま転記している。
// index.html?stage=cookbook_demo&t=<秒> で該当パターンの直後を確認できる。

import {
  stage,
  ring,
  fan,
  fallBlock,
  gravitySeq,
  wall,
  cutter,
  telegraph,
  laser,
  spiral,
  comet,
  volley,
  chase,
  pulseOnBeat,
  drizzle,
  beam,
  chargeOrb,
} from '../js/dsl.js';

export default stage(
  {
    name: 'cookbook_demo',
    music: '',
    bpm: 120,
    offset: 0,
    markers: {},
    playArea: [32, 18],
    difficulties: ['easy', 'normal', 'lunatic'],
  },
  (s) => {
    s.at(0.0, ring({ pos: [16, 9], count: 10, speed: 5, type: 'stone_burst', life: 3 }));
    s.at(4.0, fan({ pos: [16, 9], count: 7, spreadDeg: 90, centerDeg: 90, speed: 5, type: 'stone_burst', life: 3 }));
    s.at(8.0, fallBlock({ x: 16, fromY: 17, toY: 9, dur: 0.8, holdDur: 1.5, type: 'stone_block', scale: [1.6, 1.6] }));
    s.at(
      12.0,
      gravitySeq({ pos: [10, 16], type: 'stone_block', scale: [1.4, 1.4], appearDuration: 0.3 }, [
        { until: 0.6, accel: [22, -Math.PI / 2] },
        { until: 1.2, accel: [30, Math.PI / 2] },
        { until: 2.2, moveTo: [24, 11] },
        { until: 3.0, moveTo: [24, 11] },
      ])
    );
    s.at(16.0, wall({ side: 'left', gapY: 9, gapH: 6, speed: 8, warnLead: 1.0 }));
    s.at(20.0, cutter({ pos: [16, 9], spin: 3, life: 3 }));
    s.at(24.0, telegraph({ pos: [16, 9], scale: [4, 4], from: 0, until: 1.0 }));
    s.at(28.0, laser({ pos: [16, 9], angleDeg: 0, length: 20, thickness: 0.5, life: 1.0 }));
    s.at(32.0, spiral({ pos: [16, 9], count: 14, turns: 1.5, radiusStart: 0.5, radiusEnd: 3.5, expandSpeed: 2.5, spin: 1.5, life: 2.5, scale: [0.8, 0.8] }));
    s.at(36.0, comet({ pos: [4, 9], angleDeg: 0, speed: 6, trailCount: 4, life: 2.0 }));
    s.at(40.0, volley({ points: [[6, 4], [9, 6], [12, 8], [15, 10], [18, 12]], intervalSec: 0.15, speed: 5, life: 1.5 }));
    s.at(44.0, chase({ pos: [16, 16], count: 4, intervalSec: 0.25, speed: 5, life: 2.0 }));
    s.at(48.0, pulseOnBeat({ count: 4, intervalSec: 0.3, build: (i) => ring({ pos: [16, 9], count: 6, speed: 4, type: 'stone_burst', life: 0.8 }) }));
    s.at(52.0, drizzle({ area: [3, 3, 29, 15], count: 8, speedMin: 0.4, speedMax: 1.0, life: 3, seed: 7 }));
    s.at(56.0, beam({ pos: [16, 9], angleDeg: 0, length: 22, thickness: 0.5, telegraphDur: 0.5, pulseCount: 4, pulseIntervalSec: 0.25 }));
    s.at(60.0, chargeOrb({ pos: [16, 9], growDur: 0.4, holdDur: 1.2, steps: 4, startScale: 0.3, finalScale: 2.5 }));
  }
);
