$ErrorActionPreference = 'Stop'
$outputName = 'FiberRouteR1-win-x64-R1.4.9.zip'
$expectedSha = '4d23752e7e04675f1c93fddafad9fd0ce5e054b7d202d9b59ceb62b54ee9dfd4'
$parts = @(
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.001'; Size = 15728640; Sha256 = '689ec28bc053e1fc9230ac04c328a6bbd1cf73f8ba2af86643cec8794bac2301' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.002'; Size = 15728640; Sha256 = 'c606a3cbfbb887d9c537d6d277e095495c516f1e15ba1ac808789cac1f86f676' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.003'; Size = 15728640; Sha256 = 'f37c298327656c35ffdf06509f782139f7b7f3600b0fa0c8cbc3f44e692f4710' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.004'; Size = 15728640; Sha256 = '3f1cfb569441b7515e92da57d399159531aa450e961e0079a96852d609248528' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.005'; Size = 15728640; Sha256 = 'a7912f8c8b89d9fd29492b8940f759350933b215eae9fca49947593818879345' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.006'; Size = 10591885; Sha256 = '1d6aa936d316163cc1ed550f52932283eb5af22e5bd0f29d7730f11366ea409f' }
)
$folder = Split-Path -Parent $MyInvocation.MyCommand.Path
$output = Join-Path $folder $outputName
$temporary = $output + '.partial'
if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }
try {
    $writer = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)
    try {
        foreach ($item in $parts) {
            $path = Join-Path $folder $item.Name
            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw ('缺少分包：' + $item.Name) }
            if ((Get-Item -LiteralPath $path).Length -ne $item.Size) { throw ('分包大小不符：' + $item.Name) }
            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $item.Sha256) { throw ('分包校验失败：' + $item.Name) }
            $reader = [IO.File]::OpenRead($path)
            try { $reader.CopyTo($writer) } finally { $reader.Dispose() }
        }
    } finally { $writer.Dispose() }
    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedSha) { throw '合并后的完整程序校验失败' }
    Move-Item -LiteralPath $temporary -Destination $output -Force
    Write-Host ('合并完成：' + $output) -ForegroundColor Green
} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }
