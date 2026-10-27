param([string]$Target = '', [string]$DownloadDirectory = '')
$ErrorActionPreference = 'Stop'
$PSNativeCommandUseErrorActionPreference = $false
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$DownloadDirectory) { $DownloadDirectory = Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.4.1-setup' }
New-Item -ItemType Directory -Path $DownloadDirectory -Force | Out-Null
$outer = Join-Path $DownloadDirectory 'FiberRouteR1-R1.4.1-install.zip'
function Test-ExpectedFile([string]$Path, [long]$Bytes, [string]$Sha) {
    return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq $Bytes -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Sha)
}
$outerSize = 7421109
$outerSha = 'effe400ffa09c74cfc5573db0432d87bab014c96955296a79b904a128aed47f6'
if (!(Test-ExpectedFile $outer $outerSize $outerSha)) {
    $part = $outer + '.partial'
    $urls = @(
        'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.4.1/FiberRouteR1-R1.4.1-install.zip?ref=c2896efe3db24945fa29a393134981ac925b8cf6',
        'https://raw.githubusercontent.com/13417072606/FiberRouteR1/c2896efe3db24945fa29a393134981ac925b8cf6/downloads/R1.4.1/FiberRouteR1-R1.4.1-install.zip'
    )
    $curl = Get-Command curl.exe -CommandType Application -ErrorAction Stop
    for ($attempt = 0; $attempt -lt 6; $attempt++) {
        Write-Host ('正在下载 R1.4.1 安装包（{0}/6）…' -f ($attempt + 1))
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
    $folder = Join-Path $expanded 'R1.4.1补丁'
    $inner = Join-Path $folder 'FiberRouteR1-R1.4.1-patch.zip'
    if (!(Test-ExpectedFile $inner 7418274 '6bd58a109622c037f24c2f7fae685f82c3a823cfb1fe019c6cb73845fcd6d107')) { throw '内部补丁ZIP校验失败，未修改现有程序。请删除安装包后重新下载。' }
    $installer = Join-Path $folder 'Install-R1.4.1-patch.ps1'
    if (!(Test-ExpectedFile $installer 9555 '69fa86907b0eb432ad1bab4b2c935ebe4460157d50488b8a16c858c409a952d4')) { throw '安装脚本校验失败，未修改现有程序。' }
    if (!$Target) {
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择原来的运行目录：里面应包含“程序”“数据”和“光路调度智能分析.exe”，不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; return }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
    & powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $installer -Target $Target -OfflineZip $inner -NoOpen
    if ($LASTEXITCODE -ne 0) { throw '安装未完成，请查看上面的具体错误。' }
    Write-Host 'R1.4.1 已安装，数据库及人工记录保留。请从原快捷方式启动。' -ForegroundColor Green
    Write-Host ('管理方图形工具：' + (Join-Path $Target '管理方发布工具.exe'))
    Write-Host ('品牌原图替换工具：' + (Join-Path $Target '更换品牌图标.exe'))
    Write-Host '图标工具需选择本机保存的原始PNG一次。单位真实HTTPS地址尚未提供，在线更新服务暂未配置。'
    Write-Host ('已校验的离线安装包保留在：' + $outer)
} finally {
    if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force }
}
