"""Create reproducible R1.3.2 upgrade and full-program deliveries without source tables."""
from pathlib import Path
import hashlib,json,zipfile,re
root=Path(__file__).resolve().parents[1]
out=root/'downloads'/'R1.3.2';out.mkdir(parents=True,exist_ok=True)
app=root/'artifacts'/'app-r132'
base=zipfile.ZipFile(root/'downloads'/'R1.3'/'FiberRouteR1-win-x64-R1.3.zip')
oldpatch=zipfile.ZipFile(root/'downloads'/'R1.3.1'/'FiberRouteR1-R1.3.1-patch.zip')
baseprefix='光路调度智能分析R1.3/'
newprefix='光路调度智能分析R1.3.2/'
sha=lambda b:hashlib.sha256(b).hexdigest()
external=lambda d:{k:v for k,v in d['libraries'].items() if v['type']!='project'}
assert base.read(baseprefix+'程序/FiberRouteR1.runtimeconfig.json')==(app/'FiberRouteR1.runtimeconfig.json').read_bytes()
assert external(json.loads(base.read(baseprefix+'程序/FiberRouteR1.deps.json')))==external(json.loads((app/'FiberRouteR1.deps.json').read_text()))

def archive(path,files):
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for name,data in sorted(files.items()):
            info=zipfile.ZipInfo(name,(2026,10,27,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16
            z.writestr(info,data)
    with zipfile.ZipFile(path) as z:assert z.testzip() is None
    return dict(Size=path.stat().st_size,Sha256=sha(path.read_bytes()))

files={};manifest=[]
for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json']:
    path='程序/'+name;data=(app/name).read_bytes();files[path]=data
    previous=[sha(base.read(baseprefix+path)),sha(oldpatch.read(path))]
    manifest.append(dict(Path=path,Length=len(data),Sha256=sha(data),PreviousSha256=sorted(set(previous))))
files['数据/机房归属基线.json']=oldpatch.read('数据/机房归属基线.json')
files['R1.3.2更新说明.md']=(root/'docs'/'R1.3.2修改清单.md').read_bytes()
for name in ['数据/机房归属基线.json','R1.3.2更新说明.md']:
    data=files[name];manifest.append(dict(Path=name,Length=len(data),Sha256=sha(data),PreviousSha256=[]))
files['补丁校验.json']=json.dumps(dict(Version='1.3.2',Files=manifest),ensure_ascii=False,indent=2).encode()
patch=out/'FiberRouteR1-R1.3.2-patch.zip';metadata={patch.name:archive(patch,files)}
installer=(root/'scripts'/'install-r132-patch.ps1').read_text().replace('__ZIP_SIZE__',str(metadata[patch.name]['Size'])).replace('__ZIP_SHA__',metadata[patch.name]['Sha256'])
# This placeholder is pinned after the artifact commit exists; never pin a mutable URL in a bootstrap.
installer=installer.replace('?ref=r1-revised-20261025','?ref=__ARTIFACT_COMMIT__').replace('/r1-revised-20261025/downloads/','/__ARTIFACT_COMMIT__/downloads/')
(out/'Install-R1.3.2-patch.ps1').write_text(installer,encoding='utf-8-sig')

full={}
for name in base.namelist():
    if name.endswith('/'):continue
    relative=name[len(baseprefix):]
    if relative in ['文件校验SHA256.json','使用说明.txt','验证报告.md']:continue
    if relative.startswith('程序/'):
        candidate=app/relative.removeprefix('程序/')
        full[newprefix+relative]=candidate.read_bytes() if candidate.is_file() else base.read(name)
    else:full[newprefix+relative]=base.read(name)
for f in app.iterdir():
    if f.is_file() and f.suffix not in ['.pdb']:full[newprefix+'程序/'+f.name]=f.read_bytes()
full[newprefix+'光路调度智能分析.exe']=(root/'artifacts'/'launcher-r132'/'光路调度智能分析.exe').read_bytes()
full[newprefix+'数据/机房归属基线.json']=files['数据/机房归属基线.json']
full[newprefix+'R1.3.2更新说明.md']=files['R1.3.2更新说明.md']
full[newprefix+'使用说明.txt']=('R1.3.2完整程序包，不包含用户原始资源表和全量数据库。\n已有R1.3或R1.3.1推荐使用小补丁保留原数据。全新下载使用Download-R1.3.2.ps1，自动合并既有全量数据包。\n详见R1.3.2更新说明.md。指定品牌原图PNG尚未作为文件收到，本包暂保留既有图标；原图支持等比显示与多尺寸ICO。\n').encode('utf-8')
full[newprefix+'验证报告.md']=(out/'验证结果.txt').read_bytes()
full[newprefix+'文件校验SHA256.json']=json.dumps({name[len(newprefix):]:sha(data) for name,data in full.items()},ensure_ascii=False,indent=2).encode()
program=out/'FiberRouteR1-win-x64-R1.3.2.zip';metadata[program.name]=archive(program,full)
with program.open('rb') as stream:
    part=1
    while chunk:=stream.read(20_000_000):
        target=out/(program.name+f'.part{part:03}');target.write_bytes(chunk);metadata[target.name]=dict(Size=len(chunk),Sha256=sha(chunk));part+=1
(out/'SHA256.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(metadata,ensure_ascii=False,indent=2))
