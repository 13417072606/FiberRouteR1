"""Build the local R1.5.0 full program and cumulative offline installer."""
import hashlib
import json
from pathlib import Path
import re
import sys
import zipfile

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.5.0'
out.mkdir(parents=True, exist_ok=True)
sha = lambda b: hashlib.sha256(b).hexdigest()
patch = out / 'FiberRouteR1-R1.5.0-patch.zip'
full = out / 'FiberRouteR1-win-x64-R1.5.0.zip'
installer = out / 'Install-R1.5.0-patch.ps1'
bundle = out / 'FiberRouteR1-R1.5.0-install.zip'

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
    with zipfile.ZipFile(root / 'downloads/R1.4.7/FiberRouteR1-R1.4.7-patch.zip') as z:
        files = {n: z.read(n) for n in z.namelist()}
    previous = json.loads(files.pop('补丁校验.json'))
    files.pop('R1.4.7更新说明.md')
    for name in list(files):
        if name.startswith(('在线升级辅助/','服务器部署/','数据/','导出结果/')) or name.endswith(('更新说明.md','验证报告.md','文件校验SHA256.json')) or name in ('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','使用说明.txt','资源/update-service.example.json'):
            files.pop(name)
    for name in ['FiberRouteR1.dll', 'FiberRoute.Core.dll', 'FiberRouteR1.deps.json',
                 'FiberRouteR1.exe', 'FiberRouteR1.runtimeconfig.json', 'app.ico']:
        files['程序/' + name] = (root / 'artifacts/app-r150' / name).read_bytes()
    for project, folder in [('FiberRoute.Publisher', 'publisher-r150'),
                            ('FiberRoute.Brand', 'brand-r149'), ('FiberRoute.Admin', 'admin-r149'), ('FiberRoute.Updater', 'updater-r150')]:
        for ext in ['.exe', '.dll', '.deps.json', '.runtimeconfig.json']:
            files['程序/' + project + ext] = (root / 'artifacts' / folder / (project + ext)).read_bytes()
    files['资源/机房设施ID基线.db'] = (root / 'artifacts/room-id-r148.db').read_bytes()



    old = {f['Path']: f for f in previous['Files']}
    with zipfile.ZipFile(root / 'downloads/R1.4.9/FiberRouteR1-R1.4.9-patch.zip') as prior_zip:
        prior149 = {f['Path']: f['Sha256'] for f in json.loads(prior_zip.read('补丁校验.json'))['Files']}
    legacy = {}
    for version in ('R1.4.6','R1.4.7'):
        with zipfile.ZipFile(root / f'downloads/{version}/FiberRouteR1-{version}-patch.zip') as legacy_zip:
            for legacy_name in legacy_zip.namelist():
                legacy.setdefault(legacy_name, set()).add(sha(legacy_zip.read(legacy_name)))
    prior150 = {}
    if patch.exists():
        with zipfile.ZipFile(patch) as earlier:
            prior150 = {f['Path']: f['Sha256'] for f in json.loads(earlier.read('在线升级辅助/补丁校验.json'))['Files']}
    manifest = []
    for name, data in sorted(files.items()):
        hashes = old.get(name, {}).get('PreviousSha256', [])
        if hashes:
            hashes = sorted(set(hashes + [old[name]['Sha256']]))
        if name in prior149:
            hashes = sorted(set(hashes + [prior149[name]]))
        if name in prior150:
            hashes = sorted(set(hashes + [prior150[name]]))
        hashes = sorted(set(hashes).union(legacy.get(name, set())))
        if name == '资源/机房设施ID基线.db':
            hashes = [] # New in later releases; absent in R1.4.6 is valid.
        manifest.append(dict(Path=name, Length=len(data), Sha256=sha(data), PreviousSha256=hashes))
    files['在线升级辅助/补丁校验.json'] = json.dumps(dict(Version='1.5.0', Files=manifest), ensure_ascii=False, indent=2).encode()
    archive(patch, files)

    baseprefix = '光路调度路由查询R1.4.7/'
    with zipfile.ZipFile(root / 'downloads/R1.4.7/FiberRouteR1-win-x64-R1.4.7.zip') as z:
        program = {n[len(baseprefix):]: z.read(n) for n in z.namelist() if not n.endswith('/')}
    # Retain the established runtime and license files, overlay current binaries.
    for p in (root / 'artifacts/app-r150').iterdir():
        if p.is_file() and p.suffix != '.pdb':
            program['程序/' + p.name] = p.read_bytes()
    program.update({name: data for name, data in files.items() if name != '在线升级辅助/补丁校验.json'})


    # Remove obsolete and redundant customer-facing files; keep runtime and legal notices.
    for name in list(program):
        if name.startswith(('在线升级辅助/', '服务器部署/', '数据/', '导出结果/')) or name.endswith(('更新说明.md', '验证报告.md', '文件校验SHA256.json')) or name in ('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','使用说明.txt','资源/update-service.example.json','资源/中继段导入模板.csv','资源/节点类型模板.csv','资源/调测中继段.csv'):
            program.pop(name)
    program['在线升级辅助/补丁校验.json'] = files['在线升级辅助/补丁校验.json']
    # One-time migration baselines remain until applied on existing installations.
    program['程序/FiberRoute.Publisher.dll'] = (root/'artifacts/publisher-r150/FiberRoute.Publisher.dll').read_bytes()
    program['程序/FiberRoute.Publisher.deps.json'] = (root/'artifacts/publisher-r150/FiberRoute.Publisher.deps.json').read_bytes()
    program['程序/FiberRoute.Publisher.runtimeconfig.json'] = (root/'artifacts/publisher-r150/FiberRoute.Publisher.runtimeconfig.json').read_bytes()
    assert not any(n.endswith(('personal.db', 'resources.db', 'private-key.pem')) for n in program)

    prefix = '光路调度路由查询R1.5.0/'
    archive(full, {prefix + n: data for n, data in program.items()})
    assert full.stat().st_size < 100 * 1024 * 1024
    content=full.read_bytes(); partsize=15*1024*1024
    for i,start in enumerate(range(0,len(content),partsize),1):
        (out / f'{full.name}.{i:03d}').write_bytes(content[start:start+partsize])

elif sys.argv[1] == '--bundle':
    script = (root / 'downloads/R1.4.7/Install-R1.4.7-patch.ps1').read_text(encoding='utf-8-sig').replace('R1.4.7', 'R1.5.0')
    script = re.sub(r'\$expectedSize = \d+', f'$expectedSize = {patch.stat().st_size}', script)
    script = re.sub(r"\$expectedSha = '[0-9a-f]+'", f"$expectedSha = '{sha(patch.read_bytes())}'", script)
    script = re.sub(r"(?s)\} else \{\n    \$zip = Join-Path \$work.*?\n\}\n\$stage", "} else { throw '请解压完整的 R1.5.0 安装包并双击安装补丁.cmd。' }\n$stage", script, count=1)
    with zipfile.ZipFile(patch) as z:
        allowed = sorted(z.namelist())
    script = re.sub(r'    \$allowed = @\([^\n]+\)', '    $allowed = @(' + ','.join("'" + n + "'" for n in allowed) + ')', script)
    script = script.replace('R1.3 至 R1.4.6 不符', 'R1.3 至 R1.4.7 不符')
    script = script.replace("$backup = Join-Path $Target '补丁备份\\R1.5.0'", '')
    script = script.replace("    $manifest = Get-Content -LiteralPath (Join-Path $stage '补丁校验.json')", "    $manifest = Get-Content -LiteralPath (Join-Path $stage '在线升级辅助\\补丁校验.json')")
    script = re.sub(r"(?s)        \$saved = Join-Path \$backup \$file.Path\n        if \(\$existed\) \{.*?\n        \}\n        \$snapshot", '        $snapshot', script)
    # Keep only the immediately preceding program version; installation rollback stays temporary.
    backup_setup = """$previousVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $Target '程序\\FiberRouteR1.dll')).Version.ToString(3)
$backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FiberRouteR1\\补丁备份'
$backup = Join-Path $backupRoot ('R' + $previousVersion)
"""
    script = script.replace('$changes = New-Object', backup_setup + '$changes = New-Object')
    script = script.replace("        $snapshot = Join-Path (Join-Path $stage 'rollback') $file.Path", """        if ($existed -and $file.Path -notmatch '(?i)(^|[/\\\\])(app\\.ico|.*\\.ico|品牌原图\\.png)$') {
            $saved = Join-Path $backup $file.Path
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($saved)) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved -Force
        }
        $snapshot = Join-Path (Join-Path $stage 'rollback') $file.Path""")
    script = script.replace("    # Keep the update backup once.", "    # Keep the update backup once.")
    script = script.replace("    # Keep the update backup once. Databases and original tables are never included in the patch.", "")
    snapshot = """    $location = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FiberRouteR1\\data-location.txt'
    $dataRoot = if (Test-Path -LiteralPath $location -PathType Leaf) { (Get-Content -LiteralPath $location -Raw).Trim() } else { $Target }
    if (!(Test-Path -LiteralPath $dataRoot -PathType Container)) { throw ('业务数据目录不存在：' + $dataRoot) }
    foreach ($name in @('resources.db','resources.db-wal','resources.db-shm','personal.db','personal.db-wal','personal.db-shm')) {
        $existingDb = Join-Path $dataRoot $name
        if (Test-Path -LiteralPath $existingDb -PathType Leaf) {
            $dataBackup = Join-Path (Join-Path $backup '业务数据') $name
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($dataBackup)) -Force | Out-Null
            Copy-Item -LiteralPath $existingDb -Destination $dataBackup -Force
        }
    }
"""
    script = script.replace("    # Keep the update backup once. Databases and original tables are never included in the patch.", "")
    script = script.replace("    foreach ($file in $manifest.Files) {\n        $destination", snapshot + "    foreach ($file in $manifest.Files) {\n        $destination", 1)
    script = script.replace("    Write-Host ('SUCCESS:", """    # Remove backups older than the version replaced by this upgrade.
    if (Test-Path -LiteralPath $backupRoot) {
        Get-ChildItem -LiteralPath $backupRoot -Directory | Where-Object { $_.FullName -ne $backup } | Remove-Item -Recurse -Force
    }
    $legacyBackup = Join-Path $Target '补丁备份'
    if (Test-Path -LiteralPath $legacyBackup -PathType Container) { Remove-Item -LiteralPath $legacyBackup -Recurse -Force }
    foreach ($obsolete in @('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','验证报告.md','文件校验SHA256.json','R1.4.7更新说明.md','R1.4.8更新说明.md','R1.4.9更新说明.md','在线升级辅助/Continue-Online-Update.ps1','在线升级辅助/使用说明.md','服务器部署/Windows服务器部署说明.md','服务器部署/Setup-IIS-UpdateDirectory.ps1')) {
        $oldFile = Join-Path $Target $obsolete
        if (Test-Path -LiteralPath $oldFile -PathType Leaf) { Remove-Item -LiteralPath $oldFile -Force }
    }
    Write-Host ('SUCCESS:""")
    installer.write_text(script, encoding='utf-8-sig')
    with zipfile.ZipFile(root / 'downloads/R1.4.7/FiberRouteR1-R1.4.7-install.zip') as z:
        command = z.read('R1.4.7补丁/安装补丁.cmd').replace(b'R1.4.7', b'R1.5.0')
    archive(bundle, {'R1.5.0补丁/安装补丁.cmd': command,
                     'R1.5.0补丁/' + installer.name: installer.read_bytes(),
                     'R1.5.0补丁/' + patch.name: patch.read_bytes()})

else:
    raise SystemExit('Use --payload or --bundle')
