param(
    [string]$Destination = (Join-Path $env:USERPROFILE 'Downloads\FiberRouteR1-R1.5.1-软件源'),
    [switch]$NoOpen
)

$ErrorActionPreference = 'Stop'
[Net.ServicePointManager]::SecurityProtocol = [Net.SecurityProtocolType]::Tls12
$base = 'https://github.com/13417072606/FiberRouteR1/releases/download/r1.5.1-download'
$stem = 'FiberRouteR1-R1.5.1-program-source.zip'
New-Item -ItemType Directory -Path $Destination -Force | Out-Null

function Test-Hash([string]$Path, [string]$Expected) {
    return ((Test-Path -LiteralPath $Path) -and ((Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq $Expected))
}

function Get-VerifiedAsset([string]$Name, [string]$Expected) {
    $path = Join-Path $Destination $Name
    if (Test-Hash $path $Expected) {
        Write-Host "已校验，跳过：$Name"
        return $path
    }
    for ($attempt = 1; $attempt -le 8; $attempt++) {
        try {
            Write-Host "下载 $Name，第 $attempt 次..."
            Invoke-WebRequest -Uri ($base + '/' + $Name) -OutFile $path -UseBasicParsing -TimeoutSec 300
            if (-not (Test-Hash $path $Expected)) { throw "文件校验失败：$Name" }
            return $path
        }
        catch {
            if ($attempt -eq 8) { throw }
            Write-Host "本次未成功：$($_.Exception.Message)"
            Start-Sleep -Seconds 3
        }
    }
    throw "无法下载：$Name"
}

function Join-VerifiedFiles([string[]]$Sources, [string]$Target, [string]$Expected) {
    if (Test-Hash $Target $Expected) { return }
    $output = [IO.File]::Create($Target)
    try {
        foreach ($source in $Sources) {
            $inputFile = [IO.File]::OpenRead($source)
            try { $inputFile.CopyTo($output) }
            finally { $inputFile.Dispose() }
        }
    }
    finally { $output.Dispose() }
    if (-not (Test-Hash $Target $Expected)) { throw "合并文件校验失败：$Target" }
}

$partHashes = @(
    '73B12B385A0400F45BB017ADF695CBA4E3B366CCC08E8639AE4AC5FAB38ACA45',
    '88ECC28A54751C015FA14F27BF6F9F928F3AF22DBD9A8CDF2DF0BCC4A717309A',
    '29B9E05A73933C253B9958EA8F0587AF71F5CCA934D379DEA13C370A214199CC',
    'A8D62B61B417C5F657A97C36370D060F5A82BF91D2C78BEC8D2DDE73C4BB3B5',
    '5A95B1E0F31D99BAE19B79DE66DC96AC8F84B8B7146A23AAEFFC3F4331824996'
)
for ($index = 1; $index -le 3; $index++) {
    $name = '{0}.part{1:D3}' -f $stem, $index
    Get-VerifiedAsset $name $partHashes[$index - 1] | Out-Null
}

$smallHashes = @{}
$smallHashes[4] = @(
    '1366D3F812D58294ECA4E5F7C7729674DB9A5268CC93C45D865BB0F6590BA62A',
    '55EFCDAE028B25318D6294E5D2AC25DA618ACC31ED97799B1C49A98C8651F40A',
    '7955548F6656A9823BA3E4BED9EC1D07E3CB956620A02842A7EC319F6AD292A6',
    'D80C3A4EB608BDFCA0324E50CB5A8097C24AD0F8F86E8FEB8CBEEA2D54473E6'
)
$smallHashes[5] = @(
    '20AA497E8FB7075C85BF43BB073C0BFA8A445B2C8B688B2BBB868599CB4B0476',
    'C0FD155F7C87008FC7E65356E1C01E41685DAD62FF451C87500DEB447A5B0C14'
)
foreach ($partNumber in @(4, 5)) {
    $sources = [System.Collections.Generic.List[string]]::new()
    $hashes = $smallHashes[$partNumber]
    for ($sliceNumber = 1; $sliceNumber -le $hashes.Count; $sliceNumber++) {
        $name = '{0}.part{1:D3}.slice{2:D3}' -f $stem, $partNumber, $sliceNumber
        $sources.Add((Get-VerifiedAsset $name $hashes[$sliceNumber - 1]))
    }
    $part = Join-Path $Destination ('{0}.part{1:D3}' -f $stem, $partNumber)
    Join-VerifiedFiles $sources.ToArray() $part $partHashes[$partNumber - 1]
}

$parts = [System.Collections.Generic.List[string]]::new()
for ($index = 1; $index -le 5; $index++) {
    $part = Join-Path $Destination ('{0}.part{1:D3}' -f $stem, $index)
    if (-not (Test-Hash $part $partHashes[$index - 1])) { throw "分片校验失败：$part" }
    $parts.Add($part)
}
$zip = Join-Path $Destination $stem
Join-VerifiedFiles $parts.ToArray() $zip '1FFBD345C855C544E23BBDEC477FB0BCFAECBFBABA6722EA951C96950CAD8F92'
$folder = Join-Path $Destination '已解压-成功'
Expand-Archive -LiteralPath $zip -DestinationPath $folder -Force
if (-not $NoOpen) { Start-Process explorer.exe -ArgumentList ('"' + $folder + '"') }
Write-Host "软件发布源已校验并解压：$folder"
