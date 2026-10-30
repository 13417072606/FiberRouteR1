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
bundle = out / 'FiberRouteR1-R1.5.0-upgrade-corefix.zip'

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
    files.pop('补丁校验.json')
    files.pop('R1.4.7更新说明.md')
    for name in list(files):
        if name.startswith(('在线升级辅助/','服务器部署/','数据/','导出结果/')) or name.endswith(('更新说明.md','验证报告.md','文件校验SHA256.json')) or name in ('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','使用说明.txt','资源/update-service.example.json','更换品牌图标.exe'):
            files.pop(name)
    for name in ['FiberRouteR1.dll', 'FiberRoute.Core.dll', 'FiberRouteR1.deps.json',
                 'FiberRouteR1.exe', 'FiberRouteR1.runtimeconfig.json', 'app.ico']:
        files['程序/' + name] = (root / 'artifacts/app-r150' / name).read_bytes()
    for project, folder in [('FiberRoute.Publisher', 'publisher-r150'),
                            ('FiberRoute.Brand', 'brand-r149'), ('FiberRoute.Updater', 'updater-r150')]:
        for ext in ['.exe', '.dll', '.deps.json', '.runtimeconfig.json']:
            files['程序/' + project + ext] = (root / 'artifacts' / folder / (project + ext)).read_bytes()
    for name in list(files):
        if name == '管理方发布工具.exe' or name.startswith(('程序/FiberRoute.Publisher.', '程序/FiberRoute.Admin.')):
            files.pop(name)
    files['资源/机房设施ID基线.db'] = (root / 'artifacts/room-id-r148.db').read_bytes()



    manifest = []
    for name, data in sorted(files.items()):
        manifest.append(dict(Path=name, Length=len(data), Sha256=sha(data)))
    files['在线升级辅助/补丁校验.json'] = json.dumps(dict(Version='1.5.0', Files=manifest), ensure_ascii=False, indent=2).encode()
    archive(patch, files)

elif sys.argv[1] == '--bundle':
    script = (root / 'downloads/R1.4.7/Install-R1.4.7-patch.ps1').read_text(encoding='utf-8-sig').replace('R1.4.7', 'R1.5.0')
    script = re.sub(r'\$expectedSize = \d+', f'$expectedSize = {patch.stat().st_size}', script)
    script = re.sub(r"\$expectedSha = '[0-9a-f]+'", f"$expectedSha = '{sha(patch.read_bytes())}'", script)
    script = re.sub(r"(?s)\} else \{\n    \$zip = Join-Path \$work.*?\n\}\n\$stage", "} else { throw '请解压完整的 R1.5.0 安装包并双击安装补丁.cmd。' }\n$stage", script, count=1)
    with zipfile.ZipFile(patch) as z:
        allowed = sorted(z.namelist())
    script = re.sub(r'    \$allowed = @\([^\n]+\)', '    $allowed = @(' + ','.join("'" + n + "'" for n in allowed) + ')', script)
    script, removed = re.subn(r'(?s)        if \(@\(\$file\.PreviousSha256\)\.Count -gt 0\) \{\n.*?\n        \}\n', '', script, count=1)
    assert removed == 1
    script = script.replace("$backup = Join-Path $Target '补丁备份\\R1.5.0'", '')
    script = script.replace("    $manifest = Get-Content -LiteralPath (Join-Path $stage '补丁校验.json')", "    $manifestPath = Join-Path $stage '在线升级辅助\\补丁校验.json'\n    if (!(Test-Path -LiteralPath $manifestPath -PathType Leaf)) { throw '升级包缺少在线升级辅助\\补丁校验.json；请重新下载本版升级包并解压到新文件夹。' }\n    $manifest = Get-Content -LiteralPath $manifestPath")
    script = re.sub(r"(?s)        \$saved = Join-Path \$backup \$file.Path\n        if \(\$existed\) \{.*?\n        \}\n        \$snapshot", '        $snapshot', script)
    # Keep only the immediately preceding program version; installation rollback stays temporary.
    backup_setup = """$installedAssembly = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $Target '程序\\FiberRouteR1.dll'))
if ($installedAssembly.Name -ne 'FiberRouteR1') { throw '所选目录中的主程序程序集不正确，未修改现有程序。' }
$previousVersion = $installedAssembly.Version.ToString(3)
if (@('1.4.6','1.4.7','1.4.8','1.4.9','1.5.0') -notcontains $previousVersion) { throw ('不支持从 R' + $previousVersion + ' 升级，请选择 R1.4.6 至 R1.5.0 的原程序目录。') }
$backupRoot = Join-Path $Target '补丁备份'
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
    $oldBackgroundBackup = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FiberRouteR1\\补丁备份'
    if ((Test-Path -LiteralPath (Join-Path $backup '业务数据') -PathType Container) -and (Test-Path -LiteralPath $oldBackgroundBackup -PathType Container)) { Remove-Item -LiteralPath $oldBackgroundBackup -Recurse -Force }
    foreach ($obsolete in @('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','验证报告.md','文件校验SHA256.json','R1.4.6更新说明.md','机房归属来源复核.xlsx','R1.4.7更新说明.md','R1.4.8更新说明.md','R1.4.9更新说明.md','在线升级辅助/Continue-Online-Update.ps1','在线升级辅助/使用说明.md','更换品牌图标.exe','管理方发布工具.exe','服务器部署/Windows服务器部署说明.md','服务器部署/Setup-IIS-UpdateDirectory.ps1')) {
        $oldFile = Join-Path $Target $obsolete
        if (Test-Path -LiteralPath $oldFile -PathType Leaf) { Remove-Item -LiteralPath $oldFile -Force }
    }
    $programDir = Join-Path $Target '程序'
    if (Test-Path -LiteralPath $programDir -PathType Container) {
        Get-ChildItem -LiteralPath $programDir -File | Where-Object { $_.Name -like 'FiberRoute.Publisher.*' -or $_.Name -like 'FiberRoute.Admin.*' } | Remove-Item -Force
    }
    $oldExports = Join-Path $Target '导出结果'
    if (Test-Path -LiteralPath $oldExports -PathType Container) {
        $exportFiles = @(Get-ChildItem -LiteralPath $oldExports -Recurse -File)
        if ($exportFiles.Count -gt 0) {
            $archiveRoot = Join-Path ([Environment]::GetFolderPath('MyDocuments')) ('光路调度历史导出-' + (Get-Date -Format 'yyyyMMdd-HHmmss'))
            Move-Item -LiteralPath $oldExports -Destination $archiveRoot
            Write-Host ('旧导出文件已移至：' + $archiveRoot)
        } else { Remove-Item -LiteralPath $oldExports -Recurse -Force }
    }
    foreach ($folder in @('第三方许可','在线升级辅助','服务器部署')) {
        $oldFolder = Join-Path $Target $folder
        if ((Test-Path -LiteralPath $oldFolder -PathType Container) -and @(Get-ChildItem -LiteralPath $oldFolder -Force).Count -eq 0) { Remove-Item -LiteralPath $oldFolder -Force }
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
