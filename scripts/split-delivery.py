"""Split the tested Windows delivery into download-sized binary parts."""
import hashlib
import json
from pathlib import Path
import shutil
import sys
import zipfile

root=Path(__file__).resolve().parents[1]
source=root/'artifacts/FiberRouteR1-win-x64-R1.zip'
target=Path(sys.argv[1]) if len(sys.argv)>1 else root/'artifacts/downloads-R1.0.1'
target.mkdir(parents=True,exist_ok=True)
base='FiberRouteR1-win-x64-R1.0.1.zip'
parts=[]
with source.open('rb') as stream:
    while data:=stream.read(30_000_000):
        name=f'{base}.{len(parts)+1:03d}'
        (target/name).write_bytes(data)
        parts.append({'name':name,'bytes':len(data),'sha256':hashlib.sha256(data).hexdigest()})
whole=hashlib.sha256()
for part in parts:
    with (target/part['name']).open('rb') as stream:
        for block in iter(lambda:stream.read(1024*1024),b''):whole.update(block)
assert whole.hexdigest()==hashlib.sha256(source.read_bytes()).hexdigest()
manifest={'name':base,'bytes':source.stat().st_size,'sha256':whole.hexdigest(),'parts':parts}
helpers=target/'merge-helper';helpers.mkdir(exist_ok=True)
(helpers/'manifest.json').write_text(json.dumps(manifest,indent=2),encoding='utf-8')
ps=r'''$ErrorActionPreference = 'Stop'
$Temporary = $null
try {
    $Manifest = Get-Content -LiteralPath (Join-Path $PSScriptRoot 'manifest.json') -Raw -Encoding UTF8 | ConvertFrom-Json
    $Output = Join-Path $PSScriptRoot $Manifest.name
    foreach ($Part in $Manifest.parts) {
        $File = Join-Path $PSScriptRoot $Part.name
        if (-not (Test-Path -LiteralPath $File -PathType Leaf)) { throw ('缺少分包：' + $Part.name + '。请把分包与 merge.ps1 放在同一文件夹。') }
        if ((Get-Item -LiteralPath $File).Length -ne $Part.bytes -or (Get-FileHash -LiteralPath $File -Algorithm SHA256).Hash -ne $Part.sha256) { throw ('分包不完整：' + $Part.name + '，请重新下载该分包。') }
    }
    if (Test-Path -LiteralPath $Output) {
        if ((Get-FileHash -LiteralPath $Output -Algorithm SHA256).Hash -eq $Manifest.sha256) { Write-Host ('完整 ZIP 已存在：' + $Output); exit 0 }
        throw '存在同名但内容不同的 ZIP；请先移动旧 ZIP，工具不会覆盖。'
    }
    $Temporary = $Output + '.' + [guid]::NewGuid().ToString('N') + '.partial'
    $Destination = [IO.File]::Open($Temporary, [IO.FileMode]::CreateNew)
    try {
        foreach ($Part in $Manifest.parts) {
            $Source = [IO.File]::OpenRead((Join-Path $PSScriptRoot $Part.name))
            try { $Source.CopyTo($Destination) } finally { $Source.Dispose() }
        }
    } finally { $Destination.Dispose() }
    if ((Get-Item -LiteralPath $Temporary).Length -ne $Manifest.bytes -or (Get-FileHash -LiteralPath $Temporary -Algorithm SHA256).Hash -ne $Manifest.sha256) { throw '合并后的完整性校验失败。' }
    Move-Item -LiteralPath $Temporary -Destination $Output
    $Temporary = $null
    Write-Host ('合并并校验成功：' + $Output) -ForegroundColor Green
    Write-Host '请完整解压 ZIP，双击解压目录中的“纤路R1.exe”。无需安装 .NET 或自行编译。'
} catch {
    Write-Host $_.Exception.Message -ForegroundColor Red
    exit 1
} finally {
    if ($Temporary -and (Test-Path -LiteralPath $Temporary)) { Remove-Item -LiteralPath $Temporary -Force }
}
'''
(helpers/'merge.ps1').write_bytes(b'\xef\xbb\xbf'+ps.replace('\n','\r\n').encode('utf-8'))
cmd='@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0merge.ps1"\r\npause\r\n'
(helpers/'合并下载包.cmd').write_bytes(cmd.encode('ascii'))
(helpers/'下载合并说明.txt').write_text('下载全部4个分包和合并工具 ZIP。解压合并工具，把其中所有文件与 .001 到 .004 分包放在同一文件夹，双击“合并下载包.cmd”。工具校验每份及完整 ZIP；成功后完整解压生成的 ZIP，再双击“纤路R1.exe”。分包不能单独解压。Windows x64 自包含，不需安装 .NET。个人数据保存在 %LOCALAPPDATA%\\FiberRouteR1，原数据不放在下载目录。Windows 桌面实际交互尚需实机验收，已完成核心65项测试、29万行合成数据、交叉发布和成品完整性检查。',encoding='utf-8-sig')
with zipfile.ZipFile(target/'合并工具.zip','w',zipfile.ZIP_DEFLATED) as archive:
    for path in sorted(helpers.iterdir()):archive.write(path,path.name)
with zipfile.ZipFile(target/'合并工具.zip') as archive:assert archive.testzip() is None
(target/'分包校验.json').write_text(json.dumps(manifest,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(manifest,ensure_ascii=False,indent=2))
