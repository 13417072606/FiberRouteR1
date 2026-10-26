import hashlib,json,pathlib,shutil,zipfile
import xml.etree.ElementTree as ET
root=pathlib.Path(__file__).resolve().parents[1]
version=ET.parse(root/'src'/'FiberRoute.App'/'FiberRoute.App.csproj').findtext('.//Version')
delivery_name='光路调度智能分析R'+version
out=root/'artifacts'/'delivery'/delivery_name
if out.exists(): shutil.rmtree(out)
(out/'程序').mkdir(parents=True)
(out/'导出结果').mkdir()
for part in ['app']:
    for f in (root/'artifacts'/part).iterdir():
        if f.is_file():shutil.copy2(f,out/'程序'/f.name)
shutil.copytree(root/'assets',out/'资源')
shutil.copy2(root/'artifacts'/'launcher'/'光路调度智能分析.exe',out/'光路调度智能分析.exe')
shutil.copy2(root/'docs'/'使用说明.txt',out/'使用说明.txt')
shutil.copy2(root/'docs'/'验证报告.md',out/'验证报告.md')
licenses=out/'第三方许可';licenses.mkdir()
for pattern in ['microsoft.data.sqlite.core/8.0.21','sqlitepclraw.lib.e_sqlite3/2.1.6','sqlitepclraw.core/2.1.6','sqlitepclraw.provider.e_sqlite3/2.1.6','sqlitepclraw.bundle_e_sqlite3/2.1.6']:
    package=pathlib.Path('/workspace/.nuget/packages')/pattern
    for f in package.rglob('*'):
        if f.is_file() and any(x in f.name.lower() for x in ['license','notice','copyright']):
            shutil.copy2(f,licenses/(pattern.replace('/','-')+'-'+f.name))
shutil.copy2(root/'docs'/'第三方组件.txt',licenses/'第三方组件.txt')
for part in ['microsoft.netcore.app.runtime.win-x64/8.0.32','microsoft.windowsdesktop.app.runtime.win-x64/8.0.32']:
    for f in (pathlib.Path('/workspace/.nuget/packages')/part).glob('*'):
        if f.is_file() and any(x in f.name.lower() for x in ['license','notice']):shutil.copy2(f,licenses/(part.replace('/','-')+'-'+f.name))
for f in (root/'docs'/'licenses').glob('*'):shutil.copy2(f,licenses/f.name)
for name in ['FiberRouteR1.exe','FiberRouteR1.dll','FiberRouteR1.runtimeconfig.json','coreclr.dll','hostfxr.dll','hostpolicy.dll','e_sqlite3.dll','System.Windows.Forms.dll']:
    assert (out/'程序'/name).is_file(),name
# Confirm native PE architecture of each EXE and native SQLite DLL.
for f in [out/'光路调度智能分析.exe',out/'程序'/'FiberRouteR1.exe',out/'程序'/out/'程序'/'e_sqlite3.dll']:
    import struct
    with f.open('rb') as stream:
        assert stream.read(2)==b'MZ'
        stream.seek(0x3c);pe=struct.unpack('<I',stream.read(4))[0];stream.seek(pe)
        assert stream.read(4)==b'PE\0\0' and stream.read(2)==b'\x64\x86',str(f)
config=json.loads((out/'程序'/'FiberRouteR1.runtimeconfig.json').read_text())
assert 'includedFrameworks' in config['runtimeOptions'] and 'frameworks' not in config['runtimeOptions']
manifest={str(f.relative_to(out)):hashlib.sha256(f.read_bytes()).hexdigest() for f in sorted(out.rglob('*')) if f.is_file()}
(out/'文件校验SHA256.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
archive=root/'artifacts'/('FiberRouteR1-win-x64-R'+version+'.zip')
with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
    for f in sorted(out.rglob('*')):
        if f.is_file():z.write(f,delivery_name+'/'+str(f.relative_to(out)))
    z.writestr(delivery_name+'/导出结果/','')
with zipfile.ZipFile(archive) as z:
    assert z.testzip() is None
    assert not any(f.endswith(('.py','.pyd','.cs')) for f in z.namelist())
    print('ZIP files:',len(z.namelist()),'size:',archive.stat().st_size)
sha=hashlib.sha256(archive.read_bytes()).hexdigest()
archive.with_suffix('.zip.sha256').write_text(f'{sha}  {archive.name}\n')
print('SHA256:',sha)
