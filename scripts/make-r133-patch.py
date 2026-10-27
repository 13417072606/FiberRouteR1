"""Small R1.3.3 patch; retain all published older artifacts unchanged."""
import hashlib
import json
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/'downloads/R1.3.3'
out.mkdir(parents=True,exist_ok=True)
app=root/'artifacts/ui-checks-r133'
sha=lambda data:hashlib.sha256(data).hexdigest()
old=zipfile.ZipFile(root/'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip')
p131=zipfile.ZipFile(root/'downloads/R1.3.1/FiberRouteR1-R1.3.1-patch.zip')
p132=zipfile.ZipFile(root/'downloads/R1.3.2/FiberRouteR1-R1.3.2-patch.zip')
files={}
manifest=[]
for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json']:
    path='程序/'+name
    data=(app/name).read_bytes()
    previous=[sha(old.read('光路调度智能分析R1.3/'+path)),sha(p131.read(path)),sha(p132.read(path))]
    files[path]=data
    manifest.append(dict(Path=path,Length=len(data),Sha256=sha(data),PreviousSha256=sorted(set(previous))))
files['数据/机房归属基线.json']=p132.read('数据/机房归属基线.json')
files['R1.3.3更新说明.md']=(root/'docs/R1.3.3修改清单.md').read_bytes()
for path in ['数据/机房归属基线.json','R1.3.3更新说明.md']:
    data=files[path]
    manifest.append(dict(Path=path,Length=len(data),Sha256=sha(data),PreviousSha256=[]))
files['补丁校验.json']=json.dumps(dict(Version='1.3.3',Files=manifest),ensure_ascii=False,indent=2).encode()
target=out/'FiberRouteR1-R1.3.3-patch.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for path,data in sorted(files.items()):
        info=zipfile.ZipInfo(path,(2026,10,27,0,0,0))
        info.compress_type=zipfile.ZIP_DEFLATED
        info.external_attr=0o644<<16
        archive.writestr(info,data)
with zipfile.ZipFile(target) as archive:assert archive.testzip() is None
script=(root/'scripts/install-r132-patch.ps1').read_text().replace('R1.3.2','R1.3.3')
script=script.replace("'光路调度智能分析R1.3.3',''", "'光路调度智能分析R1.3.3','FiberRouteR1-R1.3.2\\运行目录\\光路调度智能分析R1.3.2','光路调度智能分析R1.3.2',''")
script=script.replace('已发布的 R1.3 / R1.3.1 不符','已发布的 R1.3 / R1.3.1 / R1.3.2 不符')
script=script.replace('__ZIP_SIZE__',str(target.stat().st_size)).replace('__ZIP_SHA__',sha(target.read_bytes()))
script=script.replace('?ref=r1-revised-20261025','?ref=__ARTIFACT_COMMIT__').replace('/r1-revised-20261025/downloads/','/__ARTIFACT_COMMIT__/downloads/')
(out/'Install-R1.3.3-patch.ps1').write_text(script,encoding='utf-8-sig')
metadata={target.name:dict(Size=target.stat().st_size,Sha256=sha(target.read_bytes()))}
(out/'SHA256.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(metadata,ensure_ascii=False))
