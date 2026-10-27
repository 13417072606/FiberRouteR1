param([string]$Folder = '', [switch]$NoOpen)
$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
if (!$Folder) { $Folder = Join-Path ([Environment]::GetFolderPath('UserProfile')) 'Downloads\FiberRouteR1-R1.3.2' }
$folder = [IO.Path]::GetFullPath($Folder)
New-Item -ItemType Directory -Force -Path $folder | Out-Null
$api = 'https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/'
$artifactRef = 'aecf19e5a2e1e3530de35b9e5051e4779d71d51d'
$headers = @{ Accept = 'application/vnd.github.raw+json'; 'User-Agent' = 'FiberRouteR1-Downloader' }
$curl = Get-Command curl.exe -CommandType Application -ErrorAction SilentlyContinue
function Test-Part($path, $size, $sha) {
    if (!(Test-Path -LiteralPath $path -PathType Leaf)) { return $false }
    if ((Get-Item -LiteralPath $path).Length -ne $size) { return $false }
    return ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -eq $sha)
}
$parts = @(
    @{ Folder = 'R1.3.2'; Name = 'FiberRouteR1-win-x64-R1.3.2.zip.part001'; Size = 20000000; Sha = 'b903b96f29d8d79256b1f7051244d0188e5ad070f28c7571d5ec3b161cc0acbf' },
    @{ Folder = 'R1.3.2'; Name = 'FiberRouteR1-win-x64-R1.3.2.zip.part002'; Size = 20000000; Sha = 'ce93530692154359b265b7091b7e049e1def64b08c220a6e122bb2efc4d973a4' },
    @{ Folder = 'R1.3.2'; Name = 'FiberRouteR1-win-x64-R1.3.2.zip.part003'; Size = 20000000; Sha = 'c49dfb395913e5b56c2b501a28c8c12f2e4823b0d100900b06541e749b92d214' },
    @{ Folder = 'R1.3.2'; Name = 'FiberRouteR1-win-x64-R1.3.2.zip.part004'; Size = 20000000; Sha = '1ea121be624e437a6e71efc1eebac867148da8535c2cb7d1d0af141117c12c24' },
    @{ Folder = 'R1.3.2'; Name = 'FiberRouteR1-win-x64-R1.3.2.zip.part005'; Size = 16065519; Sha = '55ded76213ec401c443d136403cb8c7e0d16bd475248aed6af77cb84b9ce1047' },
    @{ Folder = 'R1.3'; Name = 'FiberRouteR1-data-R1.3.zip.part001'; Size = 20000000; Sha = '2715de03c5e226865db36b66d73ef16801863063428dc947bc83a9f162898c07' },
    @{ Folder = 'R1.3'; Name = 'FiberRouteR1-data-R1.3.zip.part002'; Size = 20000000; Sha = '1e300f7da866d70ba672455b2fb3652d02c9067daec4df97a509c63f7a6f092f' },
    @{ Folder = 'R1.3'; Name = 'FiberRouteR1-data-R1.3.zip.part003'; Size = 20000000; Sha = '7b9b7b6d35a7ef3ab69487aa68be954fed476b2711c27d7ce6acf9fc8bcf4d01' },
    @{ Folder = 'R1.3'; Name = 'FiberRouteR1-data-R1.3.zip.part004'; Size = 11164824; Sha = '8c01a3a32de6fdf24592fce3a78270ab18dcd7cf3ad1e25a0ebd25aba4b036f2' }
)
foreach ($part in $parts) {
    $dest = Join-Path $folder $part.Name
    if (Test-Part $dest $part.Size $part.Sha) {
        Write-Host ('Already verified: ' + $part.Name)
        continue
    }
    $temporary = $dest + '.partial'
    $uri = $api + $part.Folder + '/' + $part.Name + '?ref=' + $artifactRef
    $rawUri = 'https://raw.githubusercontent.com/13417072606/FiberRouteR1/' + $artifactRef + '/downloads/' + $part.Folder + '/' + $part.Name
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
    @{Name='FiberRouteR1-win-x64-R1.3.2.zip'; Size=96065519; Sha='b68a5d76c02c75e200ae79d9e7ec5d30b52968692965a194504f5d6222cb0f63'; Parts=@('FiberRouteR1-win-x64-R1.3.2.zip.part001','FiberRouteR1-win-x64-R1.3.2.zip.part002','FiberRouteR1-win-x64-R1.3.2.zip.part003','FiberRouteR1-win-x64-R1.3.2.zip.part004','FiberRouteR1-win-x64-R1.3.2.zip.part005')},
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
        $oldRoot = Join-Path $stage '光路调度智能分析R1.3'
        $newRoot = Join-Path $stage '光路调度智能分析R1.3.2'
        foreach ($directory in @('数据','数据报告')) {
            $source = Join-Path $oldRoot $directory
            $destination = Join-Path $newRoot $directory
            New-Item -ItemType Directory -Path $destination -Force | Out-Null
            foreach ($file in Get-ChildItem -LiteralPath $source -Force) { Copy-Item -LiteralPath $file.FullName -Destination $destination -Recurse -Force }
        }
        Remove-Item -LiteralPath $oldRoot -Recurse -Force
        Move-Item -LiteralPath $stage -Destination $run
    } catch {
        Remove-Item -LiteralPath $stage -Recurse -Force -ErrorAction SilentlyContinue
        throw
    }
} else {
    Write-Host 'Existing run directory preserved; existing databases were not overwritten.'
}
$target = Join-Path $run '光路调度智能分析R1.3.2'
Write-Host ('SUCCESS: R1.3.2 program and data packages verified. Folder: ' + $target) -ForegroundColor Green
if (!$NoOpen) { Invoke-Item $target }
