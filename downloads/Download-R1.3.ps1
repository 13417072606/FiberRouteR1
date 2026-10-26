$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.3'
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/R1.3/'
$artifactRef = 'r1-revised-20261025'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$curl = Get-Command curl.exe -CommandType Application -ErrorAction SilentlyContinue
function Test-Part($path, $size, $sha) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne $size) { return $false }
    return ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha)
}
$parts = @(
    @{ Name = 'FiberRouteR1-win-x64-R1.3.zip.part001'; Size = 20000000; Sha = '34526bd3ec7336ddd283b3b5b20b6a3dfd83fb02a2f1186f0741723fa16378b1' },
    @{ Name = 'FiberRouteR1-win-x64-R1.3.zip.part002'; Size = 20000000; Sha = 'daa7b25594c053a28411dc7e313412bb28b2ca397dddcd69d3b419d664245d13' },
    @{ Name = 'FiberRouteR1-win-x64-R1.3.zip.part003'; Size = 20000000; Sha = '8a45b2cb8574775fba1278c7b0c2074a724747158b9fc7638bfa30748c3010c2' },
    @{ Name = 'FiberRouteR1-win-x64-R1.3.zip.part004'; Size = 20000000; Sha = '197f7bcedc0de517eaf1ae5386fd32d141789ecbb101dfdc18ec2f4b043e0b7b' },
    @{ Name = 'FiberRouteR1-win-x64-R1.3.zip.part005'; Size = 15156629; Sha = '72064b0b2748ecfe4890af3828de8d8fc631d8fc8816d007cb2e80c57b48da31' },
    @{ Name = 'FiberRouteR1-data-R1.3.zip.part001'; Size = 20000000; Sha = '2715de03c5e226865db36b66d73ef16801863063428dc947bc83a9f162898c07' },
    @{ Name = 'FiberRouteR1-data-R1.3.zip.part002'; Size = 20000000; Sha = '1e300f7da866d70ba672455b2fb3652d02c9067daec4df97a509c63f7a6f092f' },
    @{ Name = 'FiberRouteR1-data-R1.3.zip.part003'; Size = 20000000; Sha = '7b9b7b6d35a7ef3ab69487aa68be954fed476b2711c27d7ce6acf9fc8bcf4d01' },
    @{ Name = 'FiberRouteR1-data-R1.3.zip.part004'; Size = 11164824; Sha = '8c01a3a32de6fdf24592fce3a78270ab18dcd7cf3ad1e25a0ebd25aba4b036f2' }
)
foreach ($part in $parts) {
    $dest = Join-Path $folder $part.Name
    if (Test-Part $dest $part.Size $part.Sha) {
        Write-Host ('Already verified: ' + $part.Name)
        continue
    }
    $temporary = $dest + '.partial'
    $uri = $api + $part.Name + '?ref=' + $artifactRef
    $rawUri = 'https://raw.githubusercontent.com/13417072606/FiberRouteR1/' + $artifactRef + '/downloads/R1.3/' + $part.Name
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
$packages = @(
    @{Name='FiberRouteR1-win-x64-R1.3.zip'; Size=95156629; Sha='25469cbbc6c908f6a2daeffcf135577b631141ba873d44c08a2fd362e4cca167'; Parts=@('FiberRouteR1-win-x64-R1.3.zip.part001','FiberRouteR1-win-x64-R1.3.zip.part002','FiberRouteR1-win-x64-R1.3.zip.part003','FiberRouteR1-win-x64-R1.3.zip.part004','FiberRouteR1-win-x64-R1.3.zip.part005')},
    @{Name='FiberRouteR1-data-R1.3.zip'; Size=71164824; Sha='f7f44b2c4d5fc0b176760e3fa42d914ca7bcd552d16b726188dc078d506f1e6b'; Parts=@('FiberRouteR1-data-R1.3.zip.part001','FiberRouteR1-data-R1.3.zip.part002','FiberRouteR1-data-R1.3.zip.part003','FiberRouteR1-data-R1.3.zip.part004')}
)
foreach ($package in $packages) {
    $zip = Join-Path $folder $package.Name
    if (Test-Part $zip $package.Size $package.Sha) { continue }
    $temporaryZip = $zip + '.partial'
    $zipStream = [IO.File]::Create($temporaryZip)
    try {
        foreach ($name in $package.Parts) {
            $partStream = [IO.File]::OpenRead((Join-Path $folder $name))
            try { $partStream.CopyTo($zipStream) } finally { $partStream.Dispose() }
        }
    } finally { $zipStream.Dispose() }
    if (!(Test-Part $temporaryZip $package.Size $package.Sha)) { throw 'Merged ZIP verification failed.' }
    Move-Item -LiteralPath $temporaryZip -Destination $zip -Force
    Write-Host ('ZIP verified: ' + $package.Name)
}
$run = Join-Path $folder '运行目录'
if (!(Test-Path -LiteralPath $run)) {
    $stage = Join-Path $folder ('解压中-' + [Guid]::NewGuid().ToString('N'))
    New-Item -ItemType Directory -Path $stage | Out-Null
    try {
        foreach ($package in $packages) {
            Expand-Archive -LiteralPath (Join-Path $folder $package.Name) -DestinationPath $stage -Force
        }
        Move-Item -LiteralPath $stage -Destination $run
    } catch {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
} else {
    Write-Host 'Existing run directory preserved; existing databases were not overwritten.'
}
$target = Join-Path $run '光路调度智能分析R1.3'
Write-Host ('SUCCESS: R1.3 program and data packages verified. Folder: ' + $target) -ForegroundColor Green
Invoke-Item $target
