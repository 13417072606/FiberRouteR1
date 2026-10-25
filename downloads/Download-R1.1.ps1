$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.1'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.1/'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$names = @('FiberRouteR1-win-x64-R1.1.zip.part001','FiberRouteR1-win-x64-R1.1.zip.part002','FiberRouteR1-win-x64-R1.1.zip.part003','FiberRouteR1-win-x64-R1.1.zip.part004')
foreach ($name in $names) {
    $dest = Join-Path $folder $name
    Write-Host ('Downloading ' + $name)
    Invoke-WebRequest -UseBasicParsing -Uri ($api + $name + '?ref=r1-revised-20261025') -Headers $headers -OutFile $dest
}
$zip = Join-Path $folder 'FiberRouteR1-win-x64-R1.1.zip'
$output = [IO.File]::Create($zip)
try {
    foreach ($name in $names) {
        $partStream = [IO.File]::OpenRead((Join-Path $folder $name))
        try { $partStream.CopyTo($output) } finally { $partStream.Dispose() }
    }
} finally { $output.Dispose() }
$expected = '1011ea4aac733c1491630adf092305f62ebd0ab49a2eb8fce2ada010188aa173'
if ((Get-FileHash -LiteralPath $zip -Algorithm SHA256).Hash -ne $expected) { throw 'SHA256 mismatch; do not use this ZIP.' }
Write-Host ('SHA256 OK: ' + $zip)
Expand-Archive -LiteralPath $zip -DestinationPath (Join-Path $folder 'unpacked') -Force
Invoke-Item (Join-Path $folder 'unpacked')
