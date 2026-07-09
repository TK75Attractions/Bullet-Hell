import json,io,sys
def load(f):
    b=open(f,'rb').read()
    bom=b[:3]==b'\xef\xbb\xbf'
    crlf=b'\r\n' in b
    txt=b.decode('utf-8-sig')
    return json.loads(txt),bom,crlf
def save(f,obj,bom,crlf):
    txt=json.dumps(obj,indent=4,ensure_ascii=False)
    if crlf: txt=txt.replace('\n','\r\n')
    data=txt.encode('utf-8')
    if bom: data=b'\xef\xbb\xbf'+data
    open(f,'wb').write(data)
