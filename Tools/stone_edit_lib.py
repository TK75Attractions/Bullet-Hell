# -*- coding: utf-8 -*-
import json, math

def load(path):
    raw = open(path,"rb").read()
    bom = raw[:3]==b'\xef\xbb\xbf'
    crlf = b'\r\n' in raw
    txt = raw.decode("utf-8-sig")
    trailing_nl = txt.endswith("\n")
    obj = json.loads(txt)
    return obj, bom, crlf, trailing_nl

def dump(path, obj, bom, crlf, trailing_nl):
    txt = json.dumps(obj, ensure_ascii=False, indent=4)
    if trailing_nl:
        txt += "\n"
    if crlf:
        txt = txt.replace("\r\n","\n").replace("\n","\r\n")
    data = txt.encode("utf-8")
    if bom:
        data = b'\xef\xbb\xbf'+data
    open(path,"wb").write(data)

def exit_lapse(ox, oy, speed, theta, gravity, dt=0.002, maxt=12.0):
    # straight bullet: local motion along theta, gravity reduces y by g*lapse^2/2
    t=0.0
    while t<maxt:
        t+=dt
        x = ox + speed*math.cos(theta)*t
        y = oy + speed*math.sin(theta)*t - gravity*t*t/2.0
        if not (-2.0 <= x < 36.0 and -2.0 <= y < 36.0):
            return t
    return maxt
