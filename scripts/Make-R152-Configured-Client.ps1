param(
    [Parameter(Mandatory=$true)][string]$SoftwareSource,
    [string]$Workspace = 'F:\管线资源发布区',
    [string]$Output = ''
)
$ErrorActionPreference = 'Stop'
$source = (Resolve-Path -LiteralPath $SoftwareSource).Path
$config = Join-Path $Workspace 'client-config\update-service.json'
if (-not (Test-Path -LiteralPath $config)) { throw "找不到公开更新配置：$config" }
$value = Get-Content -LiteralPath $config -Raw -Encoding UTF8 | ConvertFrom-Json
if ($value.ManifestUrl -ne 'https://guangludiaodu.pages.dev/manifest.json' -or $value.PublicKeyPem -notmatch '-----BEGIN PUBLIC KEY-----') { throw '更新地址或公钥不符合当前发布渠道' }
if (-not (Test-Path -LiteralPath (Join-Path $source '光路调度路由查询.exe')) -or -not (Test-Path -LiteralPath (Join-Path $source '程序\FiberRouteR1.dll'))) { throw '请选择解压后的光路调度路由分析R1.5.2文件夹' }
if (-not $Output) { $Output = Join-Path (Split-Path -Parent $source) '光路调度路由分析R1.5.2-已配置.zip' }
if (Test-Path -LiteralPath $Output) { throw "输出文件已存在：$Output" }
$stage = Join-Path $env:TEMP ('FiberRoute-client-' + [guid]::NewGuid().ToString('N'))
try {
    $client = Join-Path $stage '光路调度路由分析R1.5.2'
    New-Item -ItemType Directory -Path $client -Force | Out-Null
    Copy-Item -LiteralPath (Join-Path $source '光路调度路由查询.exe') -Destination $client
    Copy-Item -LiteralPath (Join-Path $source '程序') -Destination $client -Recurse
    $resources = Join-Path $client '资源'
    New-Item -ItemType Directory -Path $resources | Out-Null
    Copy-Item -LiteralPath $config -Destination (Join-Path $resources 'update-service.json')
    $unexpected = Get-ChildItem -LiteralPath $stage -File -Recurse | Where-Object { $_.Extension -in @('.db','.pem','.xlsx','.csv','.pdb') -or $_.Name -match 'private-key|发布工具' }
    if ($unexpected) { throw '客户端包中发现业务数据或管理方文件，已停止打包。' }
    Compress-Archive -Path $client -DestinationPath $Output -CompressionLevel Optimal
    Write-Host "已生成：$Output"
    Write-Host ('SHA256：' + (Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash)
}
finally { if (Test-Path -LiteralPath $stage) { Remove-Item -LiteralPath $stage -Recurse -Force } }
