"""Build R1.5.4 full client and software-publishing source with a single clean icon."""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import json

root=Path(__file__).resolve().parents[1]
out=root/'downloads/R1.5.4';out.mkdir(parents=True,exist_ok=True)
app=root/'artifacts/app-r154';updater=root/'artifacts/updater-r154';publisher=root/'artifacts/publisher-r154'
launcher=root/'artifacts/native-r141/app.exe'
assert launcher.is_file() and (app/'FiberRouteR1.dll').is_file()
program={'光路调度路由查询.exe':launcher.read_bytes()}
for path in app.rglob('*'):
    if path.is_file() and path.suffix.lower()!='.pdb' and path.name!='createdump.exe':
        program['程序/'+path.relative_to(app).as_posix()]=path.read_bytes()
for name in ('FiberRoute.Updater.exe','FiberRoute.Updater.dll','FiberRoute.Updater.deps.json','FiberRoute.Updater.runtimeconfig.json'):
    program['程序/'+name]=(updater/name).read_bytes()
assert '程序/app.ico' in program
full=dict(program)
for path in (root/'assets').glob('*.csv'):
    full['资源/'+path.name]=path.read_bytes()
with ZipFile(root/'downloads/R1.4.6/FiberRouteR1-win-x64-R1.4.6.zip') as prior:
    for name in prior.namelist():
        marker='光路调度路由查询R1.4.6/第三方许可/'
        if name.startswith(marker) and not name.endswith('/'):
            full['第三方许可/'+name[len(marker):]]=prior.read(name)
assert len([n for n in full if n.startswith('资源/')])==3
assert any(n.startswith('第三方许可/') for n in full)
assert not any(n.endswith(('使用说明.txt','.db','.pem','.xlsx','.pdb')) for n in full)
manifest={name:sha256(data).hexdigest().upper() for name,data in sorted(full.items())}
full['程序/补丁校验.json']=json.dumps(manifest,ensure_ascii=False,indent=2).encode('utf-8')
def write(name,items,empty=()):
    target=out/name;prefix='光路调度路由分析R1.5.4/'
    with ZipFile(target,'w',ZIP_DEFLATED,compresslevel=6) as z:
        for path,data in sorted(items.items()):
            e=ZipInfo(prefix+path,(2026,11,2,0,0,0));e.compress_type=ZIP_DEFLATED;z.writestr(e,data)
        for path in empty:z.writestr(prefix+path,b'')
    with ZipFile(target) as z:assert z.testzip() is None
    print(name,target.stat().st_size,sha256(target.read_bytes()).hexdigest())
write('FiberRouteR1-R1.5.4-program-source.zip',program)
write('FiberRouteR1-R1.5.4-full-client.zip',full,['数据/'])
assert (publisher/'FiberRoute.Publisher.exe').is_file()
admin={path.relative_to(publisher).as_posix():path.read_bytes() for path in publisher.rglob('*') if path.is_file() and path.suffix.lower()!='.pdb' and path.name!='createdump.exe'}
assert not any(name.endswith(('.db','.pem','.xlsx')) for name in admin)
target=out/'FiberRouteR1-Publisher-R1.5.4.zip'
with ZipFile(target,'w',ZIP_DEFLATED,compresslevel=6) as z:
    for path,data in sorted(admin.items()):
        e=ZipInfo('管理方发布工具R1.5.4/'+path,(2026,11,2,0,0,0));e.compress_type=ZIP_DEFLATED;z.writestr(e,data)
with ZipFile(target) as z:assert z.testzip() is None
print(target.name,target.stat().st_size,sha256(target.read_bytes()).hexdigest())
