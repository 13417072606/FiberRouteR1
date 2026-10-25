$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.1'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.1/'
$artifactRef = '8b1eb9177ca764aa57da9fd2188706ede7c2c81a'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$curl = Get-Command curl.exe -CommandType Application -ErrorAction SilentlyContinue
function Test-Part($path, $size, $sha) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne $size) { return $false }
    return ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha)
}
$parts = @(
    @{ Name = 'FiberRouteR1-win-x64-R1.1.zip.part001'; Size = 30000000; Sha = '78d83aa3d49527f4dabede63ac5116aad03d562c0f55275141f5d2a23fb3a667' },
    @{ Name = 'FiberRouteR1-win-x64-R1.1.zip.part002'; Size = 30000000; Sha = '75167f2be62349df4e57caf24c86eb68038675ad079c125c71f887f4d5b9672a' },
    @{ Name = 'FiberRouteR1-win-x64-R1.1.zip.part003'; Size = 30000000; Sha = '6cdcd476e523f060e12a85d3987598c17f41c36de503926d630bd6a9e2445873' },
    @{ Name = 'FiberRouteR1-win-x64-R1.1.zip.part004'; Size = 4075979; Sha = 'cf3708d57a8d0d803cacdd26bf013383784e6af8b92bae7fcaf32c207cb1ca8d' }
)
foreach ($part in $parts) {
    $dest = Join-Path $folder $part.Name
    if (Test-Part $dest $part.Size $part.Sha) {
        Write-Host ('Already verified: ' + $part.Name)
        continue
    }
    $temporary = $dest + '.partial'
    $uri = $api + $part.Name + '?ref=' + $artifactRef
    $verified = $false
    for ($attempt = 1; $attempt -le 4; $attempt++) {
        try {
            Write-Host ('Downloading {0}, attempt {1}/4' -f $part.Name, $attempt)
            if ($curl) {
                & $curl.Source --fail --location --http1.1 --tlsv1.2 --connect-timeout 30 --max-time 600 --header 'Accept: application/vnd.github.raw+json' --user-agent 'FiberRouteR1-Downloader' --output $temporary $uri
                if ($LASTEXITCODE -ne 0) { throw ('curl failed, exit code ' + $LASTEXITCODE) }
            } else {
                Invoke-WebRequest -UseBasicParsing -Uri $uri -Headers $headers -OutFile $temporary -TimeoutSec 600
            }
            if (!(Test-Part $temporary $part.Size $part.Sha)) { throw 'Part size or SHA256 verification failed.' }
            Move-Item -LiteralPath $temporary -Destination $dest -Force
            $verified = $true
            Write-Host ('Verified: ' + $part.Name)
            break
        } catch {
            Write-Warning $_.Exception.Message
            if ($attempt -lt 4) { Start-Sleep -Seconds 3 }
        }
    }
    if (!$verified) {
        Remove-Item -LiteralPath $temporary -ErrorAction SilentlyContinue
        throw ('Download stopped. Verified parts are preserved; rerun to continue. Failed part: ' + $part.Name)
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
