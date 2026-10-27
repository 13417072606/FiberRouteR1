param([string]$Target = '', [string]$DownloadDirectory = '')
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$DownloadDirectory) { $DownloadDirectory = Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.4-setup' }
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$outer = Join-Path $DownloadDirectory 'FiberRouteR1-R1.4-install.zip'
function Test-ExpectedFile([string]$Path, [long]$Bytes, [string]$Sha) {
    return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq $Bytes -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha)
}
$outerSize = 6907621
$outerSha = '8fbf63290068686120b0c792289be51a32265d02da1276e3c3c7d563018cc39a'
if (!(Test-ExpectedFile $outer $outerSize $outerSha)) {
    $part = $outer + '.partial'
    $urls = @(
        'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4/FiberRouteR1-R1.4-install.zip?ref=b8cb611bbe477af1ed5908ca36d264d46c9e918d',
        'https://raw.githubusercontent.com/13417072606/FiberRouteR1/b8cb611bbe477af1ed5908ca36d264d46c9e918d/downloads/R1.4/FiberRouteR1-R1.4-install.zip'
    )
    $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
    for ($attempt = 0; $attempt -lt 4; $attempt++) {
        Write-Host ('正在下载 R1.4 安装包（{0}/4）…' -f ($attempt + 1))
        & $curl.Source --fail --location --http1.1 --tlsv1.2 --retry 2 --connect-timeout 30 --max-time 300 --header 'Accept: application/vnd.github.raw+json' --output $part $urls[$attempt % 2]
        if (Test-ExpectedFile $part $outerSize $outerSha) { break }
        if (Test-Path -LiteralPath $part) { Remove-Item -LiteralPath $part -Force }
    }
    if (!(Test-ExpectedFile $part $outerSize $outerSha)) { throw '下载未完成或校验失败，未修改现有程序。请重新运行本命令。' }
    Move-Item -LiteralPath $part -Destination $outer -Force
}
$expanded = Join-Path $DownloadDirectory ('安装临时-' + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $outer -DestinationPath $expanded
    $folder = Join-Path $expanded 'R1.4补丁'
    $inner = Join-Path $folder 'FiberRouteR1-R1.4-patch.zip'
    if (!(Test-ExpectedFile $inner 6903781 '5f18937001474eeb5e4438e8439c8546ffcc6b9ceda4a4b2b8119fc2e8e4ba6b')) { throw '内部补丁ZIP校验失败，未修改现有程序。请删除安装包后重新下载。' }
    $installer = Join-Path $folder 'Install-R1.4-patch.ps1'
    if (!(Test-ExpectedFile $installer 9684 'f7a72b6409004671dc3230394a9ea63e9d32166b87976e58d0008661158d3d87')) { throw '安装脚本校验失败，未修改现有程序。' }
    if (!$Target) {
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择原来的运行目录：里面应包含“程序”“数据”和“光路调度智能分析.exe”，不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; return }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $installer -Target $Target -OfflineZip $inner -NoOpen
    if ($LASTEXITCODE -ne 0) { throw '安装未完成，请查看上面的具体错误。' }
    Write-Host 'R1.4 已安装。请从原快捷方式启动，首次启动会整理区域和来源信息。' -ForegroundColor Green
    Write-Host ('已校验的离线安装包保留在：' + $outer)
} finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}
