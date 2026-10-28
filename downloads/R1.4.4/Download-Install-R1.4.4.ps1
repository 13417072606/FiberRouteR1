param([string]$Target = '', [string]$DownloadDirectory = '')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$DownloadDirectory) { $DownloadDirectory = Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.4.4-setup' }
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$outer = Join-Path $DownloadDirectory 'FiberRouteR1-R1.4.4-install.zip'
function Test-ExpectedFile([string]$Path, [long]$Bytes, [string]$Sha) {
    return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq $Bytes -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha)
}
$outerSize = 7469093
$outerSha = 'cbbaab8119aa02b40ec1533a318734aa60017d9889f603c894f14432be648d16'
if (!(Test-ExpectedFile $outer $outerSize $outerSha)) {
    $part = $outer + '.partial'
    $urls = @(
        'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4.4/FiberRouteR1-R1.4.4-install.zip?ref=9aacc7918f3014a1d8a30f997a4bbb6120c9ea22',
        'https://raw.githubusercontent.com/13417072606/FiberRouteR1/9aacc7918f3014a1d8a30f997a4bbb6120c9ea22/downloads/R1.4.4/FiberRouteR1-R1.4.4-install.zip'
    )
    $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Write-Host ('正在下载 R1.4.4 安装包（{0}/6）…' -f ($attempt + 1))
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
    $folder = Join-Path $expanded 'R1.4.4补丁'
    $inner = Join-Path $folder 'FiberRouteR1-R1.4.4-patch.zip'
    if (!(Test-ExpectedFile $inner 7466722 'b5916ce69f3b8fe455aa000c5668969eef247f980a56ef692d65d12a3bc2626e')) { throw '内部补丁ZIP校验失败，未修改现有程序。请删除安装包后重新下载。' }
    $installer = Join-Path $folder 'Install-R1.4.4-patch.ps1'
    if (!(Test-ExpectedFile $installer 9579 '260c8578cc8a9fca0da9059c3e5b881b1d8f788a372e707ecbd55cf17b4602a2')) { throw '安装脚本校验失败，未修改现有程序。' }
    if (!$Target) {
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择原来的运行目录：里面应包含“程序”“数据”和“光路调度智能分析.exe”，不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; return }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $installer -Target $Target -OfflineZip $inner -NoOpen
    if ($LASTEXITCODE -ne 0) { throw '安装未完成，请查看上面的具体错误。' }
    Write-Host 'R1.4.4 已安装，数据库及人工记录保留。请启动光路调度路由查询.exe，原快捷方式也可继续使用。' -ForegroundColor Green
    Write-Host ('管理方图形工具：' + (Join-Path $Target '管理方发布工具.exe'))
    Write-Host ('品牌原图替换工具：' + (Join-Path $Target '更换品牌图标.exe'))
    Write-Host '发布工具和客户端均已支持24MiB自动分片、合并及校验。Cloudflare仍需上传正式签名清单及分片。'
    Write-Host '本次区域归并打开原数据库即生效，无需重导中继段；人工记录保留。'
    Write-Host ('已校验的离线安装包保留在：' + $outer)
} finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}
