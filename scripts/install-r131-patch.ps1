param([string]$Target = '', [string]$OfflineZip = '', [switch]$NoOpen)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if ([string]::IsNullOrWhiteSpace($Target)) {
    $Target = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.3\运行目录\光路调度智能分析R1.3'
}
$Target = [IO.Path]::GetFullPath($Target)
if (!(Test-Path -LiteralPath (Join-Path $Target '程序\FiberRouteR1.dll') -PathType Leaf)) {
    throw ('未找到现有 R1.3 运行目录。请用 -Target 指定包含“程序”“数据”的文件夹：' + $Target)
}
if (Get-Process -Name FiberRouteR1 -ErrorAction SilentlyContinue) { throw '请关闭光路调度智能分析，再重新运行本补丁。' }
$work = Join-Path ([IO.Path]::GetDirectoryName($Target)) 'R1.3.1补丁文件'
New-Item -ItemType Directory -Path $work -Force | Out-Null
$expectedSize = __ZIP_SIZE__
$expectedSha = '__ZIP_SHA__'
function Test-Patch($path) {
    return ((Test-Path -LiteralPath $path -PathType Leaf) -and (Get-Item -LiteralPath $path).Length -eq $expectedSize -and (Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $expectedSha)
}
if ($OfflineZip) {
    $zip = [IO.Path]::GetFullPath($OfflineZip)
    if (!(Test-Patch $zip)) { throw '补丁文件大小或 SHA256 校验失败，未修改现有程序。' }
} else {
    $zip = Join-Path $work 'FiberRouteR1-R1.3.1-patch.zip'
    if (!(Test-Patch $zip)) {
        $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
        $part = $zip + '.partial'
        $urls = @(
            'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.3.1/FiberRouteR1-R1.3.1-patch.zip?ref=r1-revised-20261025',
            'https://raw.githubusercontent.com/13417072606/FiberRouteR1/r1-revised-20261025/downloads/R1.3.1/FiberRouteR1-R1.3.1-patch.zip'
        )
        for ($attempt = 0; $attempt -lt 8; $attempt++) {
            if (Test-Path -LiteralPath $part) {
                if ((Get-Item -LiteralPath $part).Length -ge $expectedSize -and !(Test-Patch $part)) { Remove-Item -LiteralPath $part }
            }
            if (Test-Patch $part) { break }
            Write-Host ('Downloading R1.3.1 patch, attempt {0}/8' -f ($attempt + 1))
            & $curl.Source --fail --location --http1.1 --tlsv1.2 --continue-at - --connect-timeout 30 --max-time 300 --header 'Accept: application/vnd.github.raw+json' --output $part $urls[$attempt % 2]
            if (Test-Patch $part) { break }
            Write-Warning '下载尚未完成，保留已下载字节继续重试。'
        }
        if (!(Test-Patch $part)) { throw '补丁下载未完成，请重跑续传。现有程序未修改。' }
        Move-Item -LiteralPath $part -Destination $zip -Force
    }
}
$stage = Join-Path $work ('校验中-' + [Guid]::NewGuid().ToString('N'))
$backup = Join-Path $Target '补丁备份\R1.3.1'
$changes = New-Object 'System.Collections.Generic.List[object]'
try {
    Add-Type -AssemblyName System.IO.Compression.FileSystem
    $allowed = @('程序/FiberRouteR1.dll','程序/FiberRoute.Core.dll','程序/FiberRouteR1.deps.json','数据/机房归属基线.json','修正版说明.txt','补丁校验.json')
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
        if ($file.OriginalSha256) {
            $destination = Join-Path $Target $file.Path
            if (!(Test-Path -LiteralPath $destination -PathType Leaf)) { throw ('现有程序缺少文件：' + $file.Path) }
            $current = (Get-FileHash -LiteralPath $destination -Algorithm SHA256).Hash
            if ($current -ne $file.OriginalSha256 -and $current -ne $file.Sha256) { throw ('现有版本与 R1.3 不符，请勿混用补丁：' + $file.Path) }
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
    Write-Host ('SUCCESS: R1.3.1 patch verified and installed. Existing databases preserved: ' + $Target) -ForegroundColor Green
    Write-Host '请从原来的“光路调度智能分析.exe”启动，窗口标题应为 R1.3.1。首次启动将整理机房归属。'
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
