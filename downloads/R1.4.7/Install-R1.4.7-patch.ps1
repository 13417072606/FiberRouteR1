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
$work = Join-Path ([IO.Path]::GetDirectoryName($Target)) 'R1.4.7补丁文件'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$expectedSize = 12808416
$expectedSha = 'ba901ed27be2b20aa0859344654abbb68bc499357d384aac835ce783d312391c'
function Test-Patch($path) {
    return ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length -eq $expectedSize -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $expectedSha)
}
if ($OfflineZip) {
    $zip = [IO.Path]::GetFullPath($OfflineZip)
    if (!(Test-Patch $zip)) { throw ('补丁缺失、下载不完整或SHA256不符，未修改现有程序。路径：' + $zip + '；预期大小：' + $expectedSize + '。请使用完整安装包，勿再次解压内部补丁ZIP。') }
} else {
    $zip = Join-Path $work 'FiberRouteR1-R1.4.7-patch.zip'
    if (!(Test-Patch $zip)) {
        $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
        $part = $zip + '.partial'
        $urls = @(
            'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4.7/FiberRouteR1-R1.4.7-patch.zip?ref=6527cddb5a7353b352dd91eb3f6d6d19b760f5aa',
            'https://raw.githubusercontent.com/13417072606/FiberRouteR1/6527cddb5a7353b352dd91eb3f6d6d19b760f5aa/downloads/R1.4.7/FiberRouteR1-R1.4.7-patch.zip'
        )
        for ($attempt = 0; $attempt -lt 8; $attempt++) {
            if (Test-Path -LiteralPath $part) {
                if ((Get-Item -LiteralPath $part).Length -ge $expectedSize -and !(Test-Patch $part)) { Remove-Item -LiteralPath $part }
            }
            if (Test-Patch $part) { break }
            Write-Host ('Downloading R1.4.7 patch, attempt {0}/8' -f ($attempt + 1))
            & $curl.Source --fail --location --http1.1 --tlsv1.2 --continue-at - --connect-timeout 30 --max-time 300 --header 'Accept: application/vnd.github.raw+json' --output $part $urls[$attempt % 2]
            if (Test-Patch $part) { break }
            Write-Warning '下载尚未完成，保留已下载字节继续重试。'
        }
        if (!(Test-Patch $part)) { throw '补丁下载未完成，请重跑续传。现有程序未修改。' }
        Move-Item -LiteralPath $part -Destination $zip -Force
    }
}
$stage = Join-Path $work ('校验中-' + [Guid]::NewGuid().ToString('N'))
$backup = Join-Path $Target '补丁备份\R1.4.7'
$changes = New-Object 'System.Collections.Generic.List[object]'
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $allowed = @('R1.4.7更新说明.md','光路调度智能分析.exe','光路调度路由查询.exe','在线升级辅助/Continue-Online-Update.ps1','在线升级辅助/使用说明.md','在线升级辅助/工具/FiberRoute.Online.deps.json','在线升级辅助/工具/FiberRoute.Online.dll','在线升级辅助/工具/FiberRoute.Online.exe','在线升级辅助/工具/FiberRoute.Online.runtimeconfig.json','数据/机房归属基线.json','数据/设施来源基线.db','更换品牌图标.exe','服务器部署/Setup-IIS-UpdateDirectory.ps1','服务器部署/Windows服务器部署说明.md','服务器部署/web.config','程序/FiberRoute.Admin.deps.json','程序/FiberRoute.Admin.dll','程序/FiberRoute.Admin.exe','程序/FiberRoute.Admin.runtimeconfig.json','程序/FiberRoute.Brand.deps.json','程序/FiberRoute.Brand.dll','程序/FiberRoute.Brand.exe','程序/FiberRoute.Brand.runtimeconfig.json','程序/FiberRoute.Core.dll','程序/FiberRoute.Publisher.deps.json','程序/FiberRoute.Publisher.dll','程序/FiberRoute.Publisher.exe','程序/FiberRoute.Publisher.runtimeconfig.json','程序/FiberRoute.Updater.deps.json','程序/FiberRoute.Updater.dll','程序/FiberRoute.Updater.exe','程序/FiberRoute.Updater.runtimeconfig.json','程序/FiberRouteR1.deps.json','程序/FiberRouteR1.dll','程序/FiberRouteR1.exe','程序/FiberRouteR1.runtimeconfig.json','程序/app.ico','管理方发布工具.cmd','管理方发布工具.exe','管理方在线更新发布指南.md','补丁校验.json','资源/update-service.example.json','资源/集群站点基线-R1.4.5.db')
    $archive = [IO.Compression.ZipFile]::OpenRead($zip)
    try {
        $names = @($archive.Entries | ForEach-Object { $_.FullName })
        if ($names.Count -ne $allowed.Count -or @($names | Select-Object -Unique).Count -ne $allowed.Count) { throw '补丁目录不符合预期。' }
        foreach ($entry in $archive.Entries) { if ($allowed -cnotcontains $entry.FullName) { throw ('补丁含不允许的文件：' + $entry.FullName) } }
    } finally { $archive.Dispose() }
    Expand-Archive -LiteralPath $zip -DestinationPath $stage
    $manifest = Get-Content -LiteralPath (Join-Path $stage '补丁校验.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    foreach ($file in $manifest.Files) {
        if ($allowed -cnotcontains $file.Path) { throw '补丁清单路径错误。' }
        $source = Join-Path $stage $file.Path
        if ((Get-Item -LiteralPath $source).Length -ne $file.Length -or (Get-FileHash -LiteralPath $source -Algorithm SHA256).Hash -ne $file.Sha256) { throw ('补丁内容校验失败：' + $file.Path) }
        if (@($file.PreviousSha256).Count -gt 0) {
            $destination = Join-Path $Target $file.Path
            if (!(Test-Path -LiteralPath $destination -PathType Leaf)) { throw ('现有程序缺少文件：' + $file.Path) }
            $current = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            if (@($file.PreviousSha256) -notcontains $current.ToLowerInvariant() -and $current -ne $file.Sha256) { throw ('现有文件与已发布的 R1.3 至 R1.4.6 不符，请勿混用补丁：' + $file.Path) }
        }
    }
    # Keep the update backup once. Databases and original tables are never included in the patch.
    foreach ($file in $manifest.Files) {
        $destination = Join-Path $Target $file.Path
        $existed = Test-Path -LiteralPath $destination -PathType Leaf
        if ((Test-Path -LiteralPath $destination) -and !$existed) { throw ('目标文件被同名目录占用：' + $file.Path) }
        if ($existed -and (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash -eq $file.Sha256) { continue }
        $saved = Join-Path $backup $file.Path
        if ($existed) {
            New-Item -ItemType Directory -Path ([IO.Path]::GetDirectoryName($saved)) -Force | Out-Null
            if (!(Test-Path -LiteralPath $saved)) { Copy-Item -LiteralPath $destination -Destination $saved }
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
    Write-Host ('SUCCESS: R1.4.7 patch verified and installed. Existing databases preserved: ' + $Target) -ForegroundColor Green
    Write-Host '请从“光路调度路由查询.exe”启动，窗口标题应为 R1.4.7。数据库、人工确认和数据目录保持。'
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
