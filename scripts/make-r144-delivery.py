"""Create an immutable cumulative patch, then pin the bundle and downloader to commits.

Usage: --payload; --bundle <payload commit>; --bootstrap <bundle commit>.
Earlier published artifacts are read only.
"""
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile

root=Path(__file__).resolve().parents[1]
out=root/'downloads/R1.4.4'
out.mkdir(parents=True,exist_ok=True)
digest=lambda b:hashlib.sha256(b).hexdigest()
meta=lambda p:dict(Size=p.stat().st_size,Sha256=digest(p.read_bytes()))

def archive(path,files):
    with zipfile.ZipFile(path,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for name,data in sorted(files.items()):
            info=zipfile.ZipInfo(name,(2026,10,28,18,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
    with zipfile.ZipFile(path) as z:assert z.testzip() is None

def save_meta():
    files=[p for p in out.iterdir() if p.suffix in ('.zip','.ps1')]
    (out/'SHA256.json').write_text(json.dumps({p.name:meta(p) for p in sorted(files)},indent=2)+'\n')

patch=out/'FiberRouteR1-R1.4.4-patch.zip'
installer=out/'Install-R1.4.4-patch.ps1'
bundle=out/'FiberRouteR1-R1.4.4-install.zip'
stage=sys.argv[1]
if stage=='--payload':
    with zipfile.ZipFile(root/'downloads/R1.4.3/FiberRouteR1-R1.4.3-patch.zip') as z:files={n:z.read(n) for n in z.namelist()}
    previous=json.loads(files.pop('补丁校验.json'))
    files.pop('R1.4.3更新说明.md')
    for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json','FiberRouteR1.exe','FiberRouteR1.runtimeconfig.json','app.ico']:
        files['程序/'+name]=(root/'artifacts/app-r144'/name).read_bytes()
    for project,folder in [('FiberRoute.Publisher','publisher-r144'),('FiberRoute.Brand','brand-r144'),('FiberRoute.Admin','admin-r144')]:
        for ext in ['.exe','.dll','.deps.json','.runtimeconfig.json']:files['程序/'+project+ext]=(root/'artifacts'/folder/(project+ext)).read_bytes()
    files['R1.4.4更新说明.md']=(root/'docs/R1.4.4/R1.4.4修改清单.md').read_bytes()
    old={f['Path']:f for f in previous['Files']}
    manifest=[]
    for name,data in sorted(files.items()):
        hashes=old.get(name,{}).get('PreviousSha256',[])
        if hashes:hashes=sorted(set(hashes+[old[name]['Sha256']]))
        manifest.append(dict(Path=name,Length=len(data),Sha256=digest(data),PreviousSha256=hashes))
    files['补丁校验.json']=json.dumps(dict(Version='1.4.4',Files=manifest),ensure_ascii=False,indent=2).encode()
    archive(patch,files);save_meta()
elif stage=='--bundle':
    revision=sys.argv[2];assert re.fullmatch('[0-9a-f]{40}',revision)
    script=(root/'downloads/R1.4.3/Install-R1.4.3-patch.ps1').read_text(encoding='utf-8-sig').replace('R1.4.3','R1.4.4')
    script=re.sub(r'\$expectedSize = \d+',f'$expectedSize = {patch.stat().st_size}',script)
    script=re.sub(r"\$expectedSha = '[0-9a-f]+'",f"$expectedSha = '{digest(patch.read_bytes())}'",script)
    script=re.sub(r'(?<=ref=)[0-9a-f]{40}',revision,script)
    script=re.sub(r'(?<=FiberRouteR1/)[0-9a-f]{40}(?=/downloads/)',revision,script)
    script=script.replace('已发布的 R1.3 至 R1.4.2 不符','已发布的 R1.3 至 R1.4.3 不符')
    installer.write_text(script,encoding='utf-8-sig')
    with zipfile.ZipFile(root/'downloads/R1.4.3/FiberRouteR1-R1.4.3-install.zip') as z:command=z.read('R1.4.3补丁/安装补丁.cmd').replace(b'R1.4.3',b'R1.4.4')
    notes='关闭所有程序，完整解压后双击安装补丁.cmd，选择原来的运行目录（含程序和数据）。\n适用于R1.3至R1.4.3，成功后标题R1.4.4。数据库和人工记录保留。\n区域归并在打开原数据库时生效，无需重导中继段。原值和判定依据可在详情和导出中查看。\n'
    archive(bundle,{'R1.4.4补丁/安装补丁.cmd':command,'R1.4.4补丁/安装说明.txt':notes.encode('utf-8-sig'),'R1.4.4补丁/'+installer.name:installer.read_bytes(),'R1.4.4补丁/'+patch.name:patch.read_bytes()});save_meta()
elif stage=='--bootstrap':
    revision=sys.argv[2];assert re.fullmatch('[0-9a-f]{40}',revision)
    script=(root/'downloads/R1.4.3/Download-Install-R1.4.3.ps1').read_text(encoding='utf-8-sig').replace('R1.4.3','R1.4.4')
    old=json.loads((root/'downloads/R1.4.3/SHA256.json').read_text())
    for path in [patch,installer,bundle]:
        prior=old[path.name.replace('1.4.4','1.4.3')];current=meta(path)
        script=script.replace(str(prior['Size']),str(current['Size'])).replace(prior['Sha256'],current['Sha256'])
    script=re.sub(r'(?<=ref=)[0-9a-f]{40}',revision,script)
    script=re.sub(r'(?<=FiberRouteR1/)[0-9a-f]{40}(?=/downloads/)',revision,script)
    script=script.replace('本下载是公开代码包，未包含您的专用站点数据；请在节点类型页重新导入两张设施表，再在集群管理重新导入集群表。','本次区域归并打开原数据库即生效，无需重导中继段；人工记录保留。')
    (out/'Download-Install-R1.4.4.ps1').write_text(script,encoding='utf-8-sig');save_meta()
else:raise SystemExit('Unknown stage')
print((out/'SHA256.json').read_text())
