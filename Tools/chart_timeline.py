# -*- coding: utf-8 -*-
import json, re
raw = open("Assets/StageData/stone/stone.chart.json","rb").read().decode("utf-8-sig")
# strip // comments and _comment keys are fine (newtonsoft). Use a lenient parse: remove // lines
lines = raw.split("\n")
clean = "\n".join(l for l in lines if not l.strip().startswith("//"))
j = json.loads(clean)
meta = j["meta"]; bpm=meta["bpm"]; measure=meta["measure"]
beat = 60.0/bpm  # 0.416667
bar = measure*beat
markers = j["markers"]
def barbeat_to_sec(s):
    s=s.strip()
    m=re.match(r'^(\d+):([\d.]+)$', s)
    if m:
        b=int(m.group(1)); bt=float(m.group(2))
        return (b-1)*bar + (bt-1)*beat
    # absolute float
    try: return float(s)
    except: return None
def resolve(at):
    at=at.strip()
    # marker expr like "M21 - 2beat"
    m=re.match(r'^(M\d+)\s*([+-])\s*([\d.]+)beat$', at)
    if m:
        base=barbeat_to_sec(markers[m.group(1)]); sign=1 if m.group(2)=='+' else -1
        return base + sign*float(m.group(3))*beat
    if at in markers: return barbeat_to_sec(markers[at])
    return barbeat_to_sec(at)
evs=[]
for e in j["events"]:
    t=resolve(e["at"])
    label=e.get("clip", e.get("kind","?"))
    evs.append((t,label))
evs.sort()
for t,l in evs:
    print(f"{t:7.3f}  {l}")
print("--- markers ---")
for k in sorted(markers, key=lambda x:int(x[1:])):
    print(f"{k}={markers[k]} -> {barbeat_to_sec(markers[k]):.3f}")
