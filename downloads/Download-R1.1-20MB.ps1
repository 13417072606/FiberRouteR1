$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.1-20MB'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.1-20MB/'
$artifactRef = 'r1-revised-20261025'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$curl = Get-Command curl.exe -CommandType Application -ErrorAction SilentlyContinue
function Test-Part($path, $size, $sha) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne $size) { return $false }
    return ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha)
}
$parts = @(
    @{ Name = 'FiberRouteR1-R1.1.zip.part001'; Size = 20000000; Sha = '96098eab36d4e0a78d53f32be81313bbb9cbf803e1591b9b9190d7d5035f53f2' },
    @{ Name = 'FiberRouteR1-R1.1.zip.part002'; Size = 20000000; Sha = '967a073f0815c983e917fe58de9467d52958e263bdf786edb535c3f177bd37d8' },
    @{ Name = 'FiberRouteR1-R1.1.zip.part003'; Size = 20000000; Sha = '1bb4a1aba50bbd8e893ade6873708ac198b31be4b3e23dd1302320bfedc3e563' },
    @{ Name = 'FiberRouteR1-R1.1.zip.part004'; Size = 20000000; Sha = 'cc2a6db4b59a3e235c941951c6509023478adf90792b8e56c91f45631e86ddc8' },
    @{ Name = 'FiberRouteR1-R1.1.zip.part005'; Size = 14075979; Sha = 'ec03805d4375987f8937285c31d85d108698e6db2f708d9e8dccc3ee593aa6f3' }
)
foreach ($part in $parts) {
    $dest = Join-Path $folder $part.Name
    if (Test-Part $dest $part.Size $part.Sha) {
        Write-Host ('Already verified: ' + $part.Name)
        continue
    }
    $temporary = $dest + '.partial'
    $uri = $api + $part.Name + '?ref=' + $artifactRef
    $rawUri = 'https://raw.githubusercontent.com/13417072606/FiberRouteR1/' + $artifactRef + '/downloads/R1.1-20MB/' + $part.Name
    $useRaw = $false
    $verified = $false
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Write-Host ('Downloading {0}, attempt {1}/8' -f $part.Name, $attempt)
            if (Test-Path -LiteralPath $temporary) {
                $length = (Get-Item -LiteralPath $temporary).Length
                if ($length -gt $part.Size -or ($length -eq $part.Size -and !(Test-Part $temporary $part.Size $part.Sha))) {
                    Remove-Item -LiteralPath $temporary
                }
            }
            if (!(Test-Part $temporary $part.Size $part.Sha)) {
                if (!$curl) { throw 'curl.exe is required for resumable download.' }
                $targetUri = if ($useRaw) { $rawUri } else { $uri }
                & $curl.Source --fail --location --http1.1 --tlsv1.2 --continue-at - --connect-timeout 30 --max-time 900 --header 'Accept: application/vnd.github.raw+json' --user-agent 'FiberRouteR1-Downloader' --output $temporary $targetUri
                $code = $LASTEXITCODE
                if (!(Test-Part $temporary $part.Size $part.Sha)) {
                    if ($code -eq 33 -or $code -eq 22) {
                        $useRaw = $true
                        Write-Warning 'Trying the original-file endpoint for byte-range resume.'
                    }
                    throw ('Transfer incomplete (curl ' + $code + '). Partial bytes are preserved for resume.')
                }
            }
            Move-Item -LiteralPath $temporary -Destination $dest -Force
            $verified = $true
            Write-Host ('Verified: ' + $part.Name)
            break
        } catch {
            Write-Warning $_.Exception.Message
            if ($attempt -lt 8) { Start-Sleep -Seconds 3 }
        }
    }
    if (!$verified) {
        throw ('Download stopped. Verified parts and unfinished bytes are preserved. Rerun this downloader to resume. Failed 20MB part: ' + $part.Name)
    }
}
$zip = Join-Path $folder 'FiberRouteR1-win-x64-R1.1.zip'
$merged = $zip + '.partial'
$outputStream = [IO.File]::Create($merged)
try {
    foreach ($part in $parts) {
        $partStream = [IO.File]::OpenRead((Join-Path $folder $part.Name))
        try { $partStream.CopyTo($outputStream) } finally { $partStream.Dispose() }
    }
} finally { $outputStream.Dispose() }
$expected = '1011ea4aac733c1491630adf092305f62ebd0ab49a2eb8fce2ada010188aa173'
if (!(Test-Part $merged 94075979 $expected)) { throw 'Merged ZIP verification failed. Do not use it.' }
Move-Item -LiteralPath $merged -Destination $zip -Force
Write-Host ''
Write-Host ('SUCCESS: ZIP downloaded and SHA256 verified: ' + $zip) -ForegroundColor Green
Invoke-Item $folder
