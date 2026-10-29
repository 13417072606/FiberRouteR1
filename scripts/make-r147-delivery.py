"""Create immutable R1.4.7 program and cumulative upgrade deliveries.

--payload builds the two payloads; --bundle <payload commit> pins the installer.
Published R1.4.5 and earlier packages are only read, never rewritten.
"""
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.4.7'
out.mkdir(parents=True, exist_ok=True)
sha = lambda b: hashlib.sha256(b).hexdigest()
patch = out / 'FiberRouteR1-R1.4.7-patch.zip'
full = out / 'FiberRouteR1-win-x64-R1.4.7.zip'
installer = out / 'Install-R1.4.7-patch.ps1'
bundle = out / 'FiberRouteR1-R1.4.7-install.zip'

def archive(path, files):
    with zipfile.ZipFile(path, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
        for name, data in sorted(files.items()):
            info = zipfile.ZipInfo(name, (2026, 10, 28, 22, 0, 0))
            info.compress_type = zipfile.ZIP_DEFLATED
            info.external_attr = 0o644 << 16
            z.writestr(info, data)
    with zipfile.ZipFile(path) as z:
        assert z.testzip() is None

def metadata():
    values = {p.name: dict(Size=p.stat().st_size, Sha256=sha(p.read_bytes()))
              for p in sorted(out.iterdir()) if p.suffix in ('.zip', '.ps1')}
    (out / 'SHA256.json').write_text(json.dumps(values, indent=2) + '\n')
    print(json.dumps(values, indent=2))

if sys.argv[1] == '--payload':
    with zipfile.ZipFile(root / 'downloads/R1.4.6/FiberRouteR1-R1.4.6-patch.zip') as z:
        files = {n: z.read(n) for n in z.namelist()}
    previous = json.loads(files.pop('补丁校验.json'))
    files.pop('R1.4.6更新说明.md')
    for name in ['FiberRouteR1.dll', 'FiberRoute.Core.dll', 'FiberRouteR1.deps.json',
                 'FiberRouteR1.exe', 'FiberRouteR1.runtimeconfig.json', 'app.ico']:
        files['程序/' + name] = (root / 'artifacts/app-r147' / name).read_bytes()
    for project, folder in [('FiberRoute.Publisher', 'publisher-r147'),
                            ('FiberRoute.Brand', 'brand-r147'), ('FiberRoute.Admin', 'admin-r147')]:
        for ext in ['.exe', '.dll', '.deps.json', '.runtimeconfig.json']:
            files['程序/' + project + ext] = (root / 'artifacts' / folder / (project + ext)).read_bytes()
    # A resource asset is included by the software publisher. This lets an
    # online software upgrade carry the same station repair as an offline patch.
    baseline = Path(sys.argv[2]) if len(sys.argv) > 2 else Path('/workspace/fiber-r145-full/集群站点基线-R1.4.5.db')
    files['资源/集群站点基线-R1.4.5.db'] = baseline.read_bytes()
    files['R1.4.7更新说明.md'] = (root / 'docs/R1.4.7/更新说明.md').read_bytes()
    files['在线升级辅助/Continue-Online-Update.ps1'] = (root / 'scripts/Continue-Online-Update.ps1').read_text().encode('utf-8-sig')
    files['在线升级辅助/使用说明.md'] = (root / 'docs/online-setup/使用说明.md').read_bytes()
    for ext in ['.exe', '.dll', '.deps.json', '.runtimeconfig.json']:
        files['在线升级辅助/工具/FiberRoute.Online' + ext] = (root / 'artifacts/online-r147' / ('FiberRoute.Online' + ext)).read_bytes()
    old = {f['Path']: f for f in previous['Files']}
    manifest = []
    for name, data in sorted(files.items()):
        hashes = old.get(name, {}).get('PreviousSha256', [])
        if hashes:
            hashes = sorted(set(hashes + [old[name]['Sha256']]))
        manifest.append(dict(Path=name, Length=len(data), Sha256=sha(data), PreviousSha256=hashes))
    files['补丁校验.json'] = json.dumps(dict(Version='1.4.7', Files=manifest), ensure_ascii=False, indent=2).encode()
    archive(patch, files)

    baseprefix = '光路调度智能分析R1.3/'
    with zipfile.ZipFile(root / 'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip') as z:
        program = {n[len(baseprefix):]: z.read(n) for n in z.namelist() if not n.endswith('/')}
    # Retain the established runtime and license files, overlay current binaries.
    for p in (root / 'artifacts/app-r147').iterdir():
        if p.is_file() and p.suffix != '.pdb':
            program['程序/' + p.name] = p.read_bytes()
    program.update({name: data for name, data in files.items() if name != '补丁校验.json'})
    program['使用说明.txt'] = (out / '使用说明.txt').read_bytes()
    program['验证报告.md'] = (root / 'docs/R1.4.7/验证报告.md').read_bytes()
    program.pop('文件校验SHA256.json', None)
    assert not any(n.endswith(('personal.db', 'resources.db', 'private-key.pem')) for n in program)
    program['文件校验SHA256.json'] = json.dumps({n: sha(d) for n, d in sorted(program.items())}, ensure_ascii=False, indent=2).encode()
    prefix = '光路调度路由查询R1.4.7/'
    archive(full, {prefix + n: data for n, data in program.items()})
    assert full.stat().st_size < 100 * 1024 * 1024, 'split full ZIP before publishing oversized Git blobs'
    metadata()
elif sys.argv[1] == '--bundle':
    revision = sys.argv[2]
    assert re.fullmatch('[0-9a-f]{40}', revision)
    script = (root / 'downloads/R1.4.6/Install-R1.4.6-patch.ps1').read_text(encoding='utf-8-sig').replace('R1.4.6', 'R1.4.7')
    script = re.sub(r'\$expectedSize = \d+', f'$expectedSize = {patch.stat().st_size}', script)
    script = re.sub(r"\$expectedSha = '[0-9a-f]+'", f"$expectedSha = '{sha(patch.read_bytes())}'", script)
    script = re.sub(r'(?<=ref=)[0-9a-f]{40}', revision, script)
    script = re.sub(r'(?<=FiberRouteR1/)[0-9a-f]{40}(?=/downloads/)', revision, script)
    with zipfile.ZipFile(patch) as z:
        allowed = sorted(z.namelist())
    script = re.sub(r'    \$allowed = @\([^\n]+\)', '    $allowed = @(' + ','.join("'" + n + "'" for n in allowed) + ')', script)
    script = script.replace('R1.3 至 R1.4.5 不符', 'R1.3 至 R1.4.6 不符')
    script = script.replace('FiberRoute.Updater,FiberRoute.Admin -ErrorAction', 'FiberRoute.Updater,FiberRoute.Admin,FiberRoute.Online -ErrorAction')
    installer.write_text(script, encoding='utf-8-sig')
    with zipfile.ZipFile(root / 'downloads/R1.4.6/FiberRouteR1-R1.4.6-install.zip') as z:
        command = z.read('R1.4.6补丁/安装补丁.cmd').replace(b'R1.4.6', b'R1.4.7')
    archive(bundle, {'R1.4.7补丁/安装补丁.cmd': command,
                     'R1.4.7补丁/安装说明.txt': (out / '使用说明.txt').read_bytes(),
                     'R1.4.7补丁/' + installer.name: installer.read_bytes(),
                     'R1.4.7补丁/' + patch.name: patch.read_bytes()})
    metadata()
else:
    raise SystemExit('Use --payload or --bundle <payload commit>')
