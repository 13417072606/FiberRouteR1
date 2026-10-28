param([string]$Target = '', [string]$DownloadDirectory = '')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$DownloadDirectory) { $DownloadDirectory = Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.4.5-setup' }
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$outer = Join-Path $DownloadDirectory 'FiberRouteR1-R1.4.5-install.zip'
function Test-ExpectedFile([string]$Path, [long]$Bytes, [string]$Sha) {
    return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq $Bytes -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha)
}
$outerSize = 12810933
$outerSha = '93a98d17b579adeaba3d74a5eb3936c14747ab7930281f048857b79dc234b89a'
if (!(Test-ExpectedFile $outer $outerSize $outerSha)) {
    $part = $outer + '.partial'
    $urls = @(
        'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4.5/FiberRouteR1-R1.4.5-install.zip?ref=a22da097058c66b7123f023e253f81cd7fe31591',
        'https://raw.githubusercontent.com/13417072606/FiberRouteR1/a22da097058c66b7123f023e253f81cd7fe31591/downloads/R1.4.5/FiberRouteR1-R1.4.5-install.zip',
        'https://cdn.jsdelivr.net/gh/13417072606/FiberRouteR1@a93883260454908c37b81466cc312a7deec289da/downloads/R1.4.5/FiberRouteR1-R1.4.5-install.zip'
    )
    $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Write-Host ('正在下载 R1.4.5 安装包（{0}/6）…' -f ($attempt + 1))
        & $curl.Source --fail --location --http1.1 --tlsv1.2 --retry 2 --connect-timeout 30 --max-time 300 --header 'Accept: application/vnd.github.raw+json' --output $part $urls[$attempt % $urls.Count]
        if (Test-ExpectedFile $part $outerSize $outerSha) { break }
        if (Test-Path -LiteralPath $part) { Remove-Item -LiteralPath $part -Force }
    }
    if (!(Test-ExpectedFile $part $outerSize $outerSha)) { throw '下载未完成或校验失败，未修改现有程序。请重新运行本命令。' }
    Move-Item -LiteralPath $part -Destination $outer -Force
}
$expanded = Join-Path $DownloadDirectory ('安装临时-' + [Guid]::NewGuid().ToString('N'))
try {
    Expand-Archive -LiteralPath $outer -DestinationPath $expanded
    $folder = Join-Path $expanded 'R1.4.5补丁'
    $inner = Join-Path $folder 'FiberRouteR1-R1.4.5-patch.zip'
    if (!(Test-ExpectedFile $inner 12808437 '06bec7e4b76cd2ece10aff1c7a222a97d152e8376aebfd839331b448c5df9587')) { throw '内部补丁ZIP校验失败，未修改现有程序。请删除安装包后重新下载。' }
    $installer = Join-Path $folder 'Install-R1.4.5-patch.ps1'
    if (!(Test-ExpectedFile $installer 9942 '52a286983b8d4b5d008f4ca0534aa924db25e2b87277d3f9891247cc83e600f1')) { throw '安装脚本校验失败，未修改现有程序。' }
    if (!$Target) {
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择原来的运行目录：里面应包含“程序”“数据”和“光路调度智能分析.exe”，不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; return }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $installer -Target $Target -OfflineZip $inner -NoOpen
    if ($LASTEXITCODE -ne 0) { throw '安装未完成，请查看上面的具体错误。' }
    Write-Host 'R1.4.5 已安装，数据库及人工记录保留。请启动光路调度路由查询.exe，原快捷方式也可继续使用。' -ForegroundColor Green
    Write-Host ('管理方图形工具：' + (Join-Path $Target '管理方发布工具.exe'))
    Write-Host ('品牌原图替换工具：' + (Join-Path $Target '更换品牌图标.exe'))
    Write-Host '发布工具和客户端均已支持24MiB自动分片、合并及校验。Cloudflare仍需上传正式签名清单及分片。'
    Write-Host '本次集群修复打开原数据库即生效；后续导入新设施表和集群表会自动重新分析。'
    Write-Host ('已校验的离线安装包保留在：' + $outer)
} finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}
