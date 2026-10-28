param([string]$Target = '', [string]$DownloadDirectory = '')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$DownloadDirectory) { $DownloadDirectory = Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.4.2-setup' }
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$outer = Join-Path $DownloadDirectory 'FiberRouteR1-R1.4.2-install.zip'
function Test-ExpectedFile([string]$Path, [long]$Bytes, [string]$Sha) {
    return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq $Bytes -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha)
}
$outerSize = 7422114
$outerSha = '5348aa6d9c40de92537f554d12c9da6f2585106250e076509928ce32b05867f4'
if (!(Test-ExpectedFile $outer $outerSize $outerSha)) {
    $part = $outer + '.partial'
    $urls = @(
        'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4.2/FiberRouteR1-R1.4.2-install.zip?ref=1bf559abfb1aa3a1757f0b3cd62094f27b5a0143',
        'https://raw.githubusercontent.com/13417072606/FiberRouteR1/1bf559abfb1aa3a1757f0b3cd62094f27b5a0143/downloads/R1.4.2/FiberRouteR1-R1.4.2-install.zip'
    )
    $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Write-Host ('正在下载 R1.4.2 安装包（{0}/6）…' -f ($attempt + 1))
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
    $folder = Join-Path $expanded 'R1.4.2补丁'
    $inner = Join-Path $folder 'FiberRouteR1-R1.4.2-patch.zip'
    if (!(Test-ExpectedFile $inner 7419176 '40db52fe7a4ba522dc7ecd181adba96bc0f68deaa4514bd7eaab4465828a3dd8')) { throw '内部补丁ZIP校验失败，未修改现有程序。请删除安装包后重新下载。' }
    $installer = Join-Path $folder 'Install-R1.4.2-patch.ps1'
    if (!(Test-ExpectedFile $installer 9557 '45d4d2053b896c01f98034c602a613957436f1c13a3fd8b1c93e9b707d4fa86c')) { throw '安装脚本校验失败，未修改现有程序。' }
    if (!$Target) {
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择原来的运行目录：里面应包含“程序”“数据”和“光路调度智能分析.exe”，不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; return }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $installer -Target $Target -OfflineZip $inner -NoOpen
    if ($LASTEXITCODE -ne 0) { throw '安装未完成，请查看上面的具体错误。' }
    Write-Host 'R1.4.2 已安装，数据库及人工记录保留。请从原快捷方式启动。' -ForegroundColor Green
    Write-Host ('管理方图形工具：' + (Join-Path $Target '管理方发布工具.exe'))
    Write-Host ('品牌原图替换工具：' + (Join-Path $Target '更换品牌图标.exe'))
    Write-Host '图标工具需选择本机保存的原始PNG一次。Cloudflare Pages站点已创建，签名清单、版本包和客户配置仍需完成。'
    Write-Host ('已校验的离线安装包保留在：' + $outer)
} finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}
