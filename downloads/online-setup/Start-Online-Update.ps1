param([ValidateSet('Prepare','Verify','Inspect')][string]$Stage='Prepare', [string]$RuntimeDirectory='', [string]$WorkspaceDirectory='F:\管线资源发布区', [string]$DataDirectory='')
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$directory=Join-Path $env:USERPROFILE 'Downloads\FiberRoute-Online-Setup'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$archive=Join-Path $directory 'FiberRoute-Online-Setup-20261028.zip'
function Test-Archive([string]$Path) { return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq 86298 -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq 'ab1cc52584f923bb13539dc4ad02351100358a5c66d1cdce92caebdd63550819') }
if (!(Test-Archive $archive)) {
    $partial=$archive+'.partial'
    $urls=@('https://raw.githubusercontent.com/13417072606/FiberRouteR1/6ade9b2276c7b3fecdc5f78fb8692d0a40668e8c/downloads/online-setup/FiberRoute-Online-Setup-20261028.zip','https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/online-setup/FiberRoute-Online-Setup-20261028.zip?ref=6ade9b2276c7b3fecdc5f78fb8692d0a40668e8c')
    for ($i=0;$i -lt 4;$i++) {
        curl.exe --fail --location --http1.1 --tlsv1.2 --retry 2 --connect-timeout 20 --max-time 120 --header 'Accept: application/vnd.github.raw+json' --output $partial $urls[$i % 2]
        if ($LASTEXITCODE -eq 0 -and (Test-Archive $partial)) { Move-Item -LiteralPath $partial -Destination $archive -Force; break }
    }
    if (!(Test-Archive $archive)) { throw '辅助工具下载或SHA256校验失败，未继续发布。' }
}
$expanded=Join-Path $directory ('工具-'+[Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $expanded
try { & (Join-Path $expanded 'Continue-Online-Update.ps1') -Stage $Stage -RuntimeDirectory $RuntimeDirectory -WorkspaceDirectory $WorkspaceDirectory -DataDirectory $DataDirectory }
finally { if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force } }
