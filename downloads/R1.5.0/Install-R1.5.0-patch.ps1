param([string]$Target = '', [string]$OfflineZip = '', [switch]$NoOpen)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
function Test-RunDirectory([string]$path) {
    return ($path -and (Test-Path -LiteralPath (Join-Path $path '程序\FiberRouteR1.dll') -PathType Leaf))
}
if ([string]::IsNullOrWhiteSpace($Target)) {
    $current = (Get-Location).ProviderPath
    if (Test-RunDirectory $current) { $Target = $current }
    else {
        if ($env:OS -ne 'Windows_NT') { throw '请使用-Target指定包含程序和数据的原运行目录。' }
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择要升级的原运行目录，里面有程序和数据；不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; exit 2 }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
}

$Target = [IO.Path]::GetFullPath($Target)
if (!(Test-RunDirectory $Target)) { throw ('所选文件夹不是运行目录：' + $Target) }
if (Get-Process -Name FiberRouteR1,FiberRoute.Publisher,FiberRoute.Brand,FiberRoute.Updater,FiberRoute.Admin,FiberRoute.Online -ErrorAction SilentlyContinue) { throw '请关闭主程序、管理方工具和图标工具，再重新运行本补丁。' }
$work = Join-Path ([IO.Path]::GetDirectoryName($Target)) 'R1.5.0补丁文件'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$expectedSize = 17340648
$expectedSha = 'f57aca07ce94024f64b278dacd81b8ebd3f24a5791dc0c738f74c9f739c295cc'
function Test-Patch($path) {
    return ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length -eq $expectedSize -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $expectedSha)
}
if ($OfflineZip) {
    $zip = [IO.Path]::GetFullPath($OfflineZip)
    if (!(Test-Patch $zip)) { throw ('补丁缺失、下载不完整或SHA256不符，未修改现有程序。路径：' + $zip + '；预期大小：' + $expectedSize + '。请使用完整安装包，勿再次解压内部补丁ZIP。') }
} else { throw '请解压完整的 R1.5.0 安装包并双击安装补丁.cmd。' }
$stage = Join-Path $work ('校验中-' + [Guid]::NewGuid().ToString('N'))

$previousVersion = [System.Reflection.AssemblyName]::GetAssemblyName((Join-Path $Target '程序\FiberRouteR1.dll')).Version.ToString(3)
$backupRoot = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FiberRouteR1\补丁备份'
$backup = Join-Path $backupRoot ('R' + $previousVersion)
$changes = New-Object 'System.Collections.Generic.List[object]'
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $allowed = @('光路调度路由查询.exe','在线升级辅助/补丁校验.json','程序/FiberRoute.Admin.deps.json','程序/FiberRoute.Admin.dll','程序/FiberRoute.Admin.exe','程序/FiberRoute.Admin.runtimeconfig.json','程序/FiberRoute.Brand.deps.json','程序/FiberRoute.Brand.dll','程序/FiberRoute.Brand.exe','程序/FiberRoute.Brand.runtimeconfig.json','程序/FiberRoute.Core.dll','程序/FiberRoute.Publisher.deps.json','程序/FiberRoute.Publisher.dll','程序/FiberRoute.Publisher.exe','程序/FiberRoute.Publisher.runtimeconfig.json','程序/FiberRoute.Updater.deps.json','程序/FiberRoute.Updater.dll','程序/FiberRoute.Updater.exe','程序/FiberRoute.Updater.runtimeconfig.json','程序/FiberRouteR1.deps.json','程序/FiberRouteR1.dll','程序/FiberRouteR1.exe','程序/FiberRouteR1.runtimeconfig.json','程序/app.ico','管理方发布工具.exe','资源/机房设施ID基线.db','资源/集群站点基线-R1.4.5.db')
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
        if ($names.Count -ne $allowed.Count -or @($names | Select-Object -Unique).Count -ne $allowed.Count) { throw '补丁目录不符合预期。' }
        foreach ($entry in $archive.Entries) { if ($allowed -cnotcontains $entry.FullName) { throw ('补丁含不允许的文件：' + $entry.FullName) } }
    } finally { $archive.Dispose() }
    Expand-Archive -LiteralPath $zip -DestinationPath $stage
    $manifest = Get-Content -LiteralPath (Join-Path $stage '在线升级辅助\补丁校验.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($file in $manifest.Files) {
        if ($allowed -cnotcontains $file.Path) { throw '补丁清单路径错误。' }
        $source = Join-Path $stage $file.Path
        if ((Get-Item -LiteralPath $source).Length -ne $file.Length -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $file.Sha256) { throw ('补丁内容校验失败：' + $file.Path) }
        if (@($file.PreviousSha256).Count -gt 0) {
            $destination = Join-Path $Target $file.Path
            if (!(Test-Path -LiteralPath $destination -PathType Leaf)) { throw ('现有程序缺少文件：' + $file.Path) }
            $current = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            if (@($file.PreviousSha256) -notcontains $current.ToLowerInvariant() -and $current -ne $file.Sha256) { throw ('现有文件与已发布的 已发布版本不符，请勿混用补丁：' + $file.Path) }
        }
    }

    $location = Join-Path ([Environment]::GetFolderPath('LocalApplicationData')) 'FiberRouteR1\data-location.txt'
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
    foreach ($file in $manifest.Files) {
        $destination = Join-Path $Target $file.Path
        $existed = Test-Path -LiteralPath $destination -PathType Leaf
        if ((Test-Path -LiteralPath $destination) -and !$existed) { throw ('目标文件被同名目录占用：' + $file.Path) }
        if ($existed -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $file.Sha256) { continue }
        if ($existed -and $file.Path -notmatch '(?i)(^|[/\\])(app\.ico|.*\.ico|品牌原图\.png)$') {
            $saved = Join-Path $backup $file.Path
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($saved)) -Force | Out-Null
            Copy-Item -LiteralPath $destination -Destination $saved -Force
        }
        $snapshot = Join-Path (Join-Path $stage 'rollback') $file.Path
        if ($existed) { New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($snapshot)) -Force | Out-Null; Copy-Item -LiteralPath $destination -Destination $snapshot }
        $changes.Add(@{ Destination=$destination; Existed=$existed; Saved=$snapshot })
        New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($destination)) -Force | Out-Null
        Copy-Item -LiteralPath (Join-Path $stage $file.Path) -Destination $destination -Force
        if ((Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -ne $file.Sha256) { throw ('写入后校验失败：' + $file.Path) }
    }
    $original = Join-Path $Target '资源\品牌原图.png'
    if ((Test-Path -LiteralPath $original -PathType Leaf) -and $env:OS -eq 'Windows_NT') {
        $brand = Start-Process -FilePath (Join-Path $Target '程序\FiberRoute.Brand.exe') -ArgumentList '--apply-existing' -Wait -PassThru
        if ($brand.ExitCode -ne 0) { throw '已有品牌原图应用失败，安装回退。请查看运行目录的品牌替换失败.txt。' }
    }
    # Remove backups older than the version replaced by this upgrade.
    if (Test-Path -LiteralPath $backupRoot) {
        Get-ChildItem -LiteralPath $backupRoot -Directory | Where-Object { $_.FullName -ne $backup } | Remove-Item -Recurse -Force
    }
    $legacyBackup = Join-Path $Target '补丁备份'
    if (Test-Path -LiteralPath $legacyBackup -PathType Container) { Remove-Item -LiteralPath $legacyBackup -Recurse -Force }
    foreach ($obsolete in @('光路调度智能分析.exe','管理方发布工具.cmd','管理方在线更新发布指南.md','验证报告.md','文件校验SHA256.json','R1.4.7更新说明.md','R1.4.8更新说明.md','R1.4.9更新说明.md','在线升级辅助/Continue-Online-Update.ps1','在线升级辅助/使用说明.md','更换品牌图标.exe','服务器部署/Windows服务器部署说明.md','服务器部署/Setup-IIS-UpdateDirectory.ps1')) {
        $oldFile = Join-Path $Target $obsolete
        if (Test-Path -LiteralPath $oldFile -PathType Leaf) { Remove-Item -LiteralPath $oldFile -Force }
    }
    Write-Host ('SUCCESS: R1.5.0 patch verified and installed. Existing databases preserved: ' + $Target) -ForegroundColor Green
    Write-Host '请从“光路调度路由查询.exe”启动，窗口标题应为 R1.5.0。数据库、人工确认和数据目录保持。'
} catch {
    for ($i=$changes.Count-1; $i -ge 0; $i--) {
        $change = $changes[$i]
        if ($change.Existed) { Copy-Item -LiteralPath $change.Saved -Destination $change.Destination -Force }
        elseif (Test-Path -LiteralPath $change.Destination -PathType Leaf) { Remove-Item -LiteralPath $change.Destination }
    }
    throw
} finally {
    if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force }
}
if (!$NoOpen) { try { Invoke-Item -LiteralPath $Target } catch { Write-Warning ('补丁已应用，文件夹未能自动打开：' + $Target) } }
