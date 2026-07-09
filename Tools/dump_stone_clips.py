# -*- coding: utf-8 -*-
import json, glob, os
d = "Assets/BulletBuffers/stone"
targets = ["prefall_blink_1","prefall_blink_2","prefall_blink_3",
           "big_block_hammer_1","big_block_hammer_2","big_block_hammer_3",
           "shatter_shard","big_block_shard_1","big_block_shard_2","big_block_shard_3",
           "lower_burst_1","lower_burst_2","edge_cutter_shard",
           "run_cutter_warn","run_cutter_1","run_cutter_2","run_cutter_3",
           "stone_belt_bottom","stone_belt_bottom_2",
           "erase_flash_1","erase_flash_2","big_block_flash_1","big_block_flash_2","big_block_flash_3",
           "lower_burst_flash_1","lower_burst_flash_2"]
for name in targets:
    p = os.path.join(d, name+".json")
    if not os.path.exists(p):
        print(name, "MISSING"); continue
    raw = open(p,"rb").read()
    bom = raw[:3]==b'\xef\xbb\xbf'
    crlf = b'\r\n' in raw
    j = json.loads(raw.decode("utf-8-sig"))
    bl = j["bullets"]
    n = len(bl)
    b0 = bl[0]
    # summarize unique gravity, life, appearTime, scale, angleSpeed, speed
    def uniq(key):
        vals = set()
        for b in bl:
            v = b.get(key)
            if isinstance(v, dict): v = (v.get("x"),v.get("y"))
            vals.add(json.dumps(v))
        return list(vals)[:6]
    print(f"=== {name} n={n} BOM={bom} CRLF={crlf}")
    for k in ["gravity","life","appearTime","appearDuration","angleSpeed","speed","initialAngle","typeName"]:
        print(f"    {k}: {uniq(k)}")
    # origin range
    ox=[b['originPos']['x'] for b in bl]; oy=[b['originPos']['y'] for b in bl]
    print(f"    originX[{min(ox):.2f},{max(ox):.2f}] originY[{min(oy):.2f},{max(oy):.2f}]")
    sc=[(b['scale']['x'],b['scale']['y']) for b in bl]
    print(f"    scale samples: {sc[:3]}")
