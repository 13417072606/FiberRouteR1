$ErrorActionPreference = 'Stop'
$outputName = 'FiberRouteR1-win-x64-R1.4.9.zip'
$expectedSha = 'daf49dc03096410aba5b81177c9492647edacf5197993cbcdf5a182f6c19b6e0'
$parts = @(
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.001'; Size = 15728640; Sha256 = 'd2597e1577043a454beab654828aed54b5886cee2ee43bb2680df74f46ed9456' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.002'; Size = 15728640; Sha256 = '0e41c1d1371db7e61e66133f2095ed7f9736fbe4bdfb5c80f0bf0293976d72a6' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.003'; Size = 15728640; Sha256 = '70937328dd679fb3ad0c2207bf8dbea409124d5f6c7138ce4c9afcc31fe4be34' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.004'; Size = 15728640; Sha256 = '6baa68ae87d3eee0bf8a3bc8ae5ef335f64965b1e5b30f085a3a5152ee29b828' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.005'; Size = 15728640; Sha256 = '18ea0b1ba56ae7912fd2aa68c6c6f54c0b9b00a6361d548b1d150b94eb2f255a' }
    @{ Name = 'FiberRouteR1-win-x64-R1.4.9.zip.006'; Size = 10591871; Sha256 = '1314dcabdce698fc073cf9658c66046dc8cd121f70c75c35b913712d15939e37' }
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
