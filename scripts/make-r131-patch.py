"""Build a small patch against the verified, unchanged R1.3 delivery."""
from pathlib import Path
import hashlib, json, sys, zipfile
root=Path(__file__).resolve().parents[1]
baseline=Path(sys.argv[1])
out=root/'downloads'/'R1.3.1';out.mkdir(parents=True,exist_ok=True)
original=zipfile.ZipFile(root/'downloads'/'R1.3'/'FiberRouteR1-win-x64-R1.3.zip')
assert original.read('光路调度智能分析R1.3/程序/FiberRouteR1.runtimeconfig.json')==(root/'artifacts'/'app-r131'/'FiberRouteR1.runtimeconfig.json').read_bytes(),'runtime requirements changed: a binary-only patch would be insufficient'
before=json.loads(original.read('光路调度智能分析R1.3/程序/FiberRouteR1.deps.json'))
after=json.loads((root/'artifacts'/'app-r131'/'FiberRouteR1.deps.json').read_text())
external=lambda doc:{k:v for k,v in doc['libraries'].items() if v['type']!='project'}
assert external(before)==external(after),'external dependencies changed: full delivery required'
files={};manifest=[]
for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json']:
    path='程序/'+name;data=(root/'artifacts'/'app-r131'/name).read_bytes();files[path]=data
    old=original.read('光路调度智能分析R1.3/'+path)
    manifest.append(dict(Path=path,Length=len(data),Sha256=hashlib.sha256(data).hexdigest(),OriginalSha256=hashlib.sha256(old).hexdigest()))
files['数据/机房归属基线.json']=baseline.read_bytes()
files['修正版说明.txt']=(root/'docs'/'R1.3.1修正说明.md').read_bytes()
for name in ['数据/机房归属基线.json','修正版说明.txt']:
    data=files[name];manifest.append(dict(Path=name,Length=len(data),Sha256=hashlib.sha256(data).hexdigest(),OriginalSha256=''))
files['补丁校验.json']=json.dumps(dict(Version='1.3.1',Files=manifest),ensure_ascii=False,indent=2).encode()
archive=out/'FiberRouteR1-R1.3.1-patch.zip'
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=9) as z:
    for path,data in files.items():z.writestr(path,data)
with zipfile.ZipFile(archive) as z:assert z.testzip() is None
sha=hashlib.sha256(archive.read_bytes()).hexdigest()
script=(root/'scripts'/'install-r131-patch.ps1').read_text().replace('__ZIP_SIZE__',str(archive.stat().st_size)).replace('__ZIP_SHA__',sha)
(out/'Install-R1.3.1-patch.ps1').write_text(script,encoding='utf-8-sig')
(out/'SHA256.json').write_text(json.dumps({f.name:dict(Size=f.stat().st_size,Sha256=hashlib.sha256(f.read_bytes()).hexdigest()) for f in [archive,out/'Install-R1.3.1-patch.ps1']},indent=2),encoding='utf-8')
print(json.dumps(dict(Size=archive.stat().st_size,Sha256=sha,Files=len(files)),indent=2))
