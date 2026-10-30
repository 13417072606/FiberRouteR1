"""Split the verified full ZIP into small offline-download parts."""
import hashlib
import json
from pathlib import Path

root = Path(__file__).resolve().parents[1] / "downloads" / "R1.4.9"
archive = root / "FiberRouteR1-R1.4.9-install.zip"
part_size = 15 * 1024 * 1024
parts = []
with archive.open("rb") as source:
    while block := source.read(part_size):
        part = root / f"{archive.name}.{len(parts) + 1:03d}"
        part.write_bytes(block)
        parts.append({"Name": part.name, "Size": len(block), "Sha256": hashlib.sha256(block).hexdigest()})

expected = hashlib.sha256(archive.read_bytes()).hexdigest()
if expected != json.loads((root / "SHA256.json").read_text())[archive.name]["Sha256"]:
    raise SystemExit("The installer ZIP has changed since its delivery checksum was created")
ps_lines = [
    "$ErrorActionPreference = 'Stop'",
    f"$outputName = '{archive.name}'",
    f"$expectedSha = '{expected}'",
    "$parts = @(",
    *[f"    @{{ Name = '{item['Name']}'; Size = {item['Size']}; Sha256 = '{item['Sha256']}' }}" for item in parts],
    ")",
    "$folder = Split-Path -Parent $MyInvocation.MyCommand.Path",
    "$output = Join-Path $folder $outputName",
    "$temporary = $output + '.partial'",
    "if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force }",
    "try {",
    "    $writer = [IO.File]::Open($temporary, [IO.FileMode]::CreateNew, [IO.FileAccess]::Write)",
    "    try {",
    "        foreach ($item in $parts) {",
    "            $path = Join-Path $folder $item.Name",
    "            if (!(Test-Path -LiteralPath $path -PathType Leaf)) { throw ('缺少分包：' + $item.Name) }",
    "            if ((Get-Item -LiteralPath $path).Length -ne $item.Size) { throw ('分包大小不符：' + $item.Name) }",
    "            if ((Get-FileHash -LiteralPath $path -Algorithm SHA256).Hash -ne $item.Sha256) { throw ('分包校验失败：' + $item.Name) }",
    "            $reader = [IO.File]::OpenRead($path)",
    "            try { $reader.CopyTo($writer) } finally { $reader.Dispose() }",
    "        }",
    "    } finally { $writer.Dispose() }",
    "    if ((Get-FileHash -LiteralPath $temporary -Algorithm SHA256).Hash -ne $expectedSha) { throw '合并后的安装包校验失败' }",
    "    Move-Item -LiteralPath $temporary -Destination $output -Force",
    "    Write-Host ('合并完成：' + $output) -ForegroundColor Green",
    "} finally { if (Test-Path -LiteralPath $temporary) { Remove-Item -LiteralPath $temporary -Force } }",
]
(root / "Join-R1.4.9-Install.ps1").write_text("\n".join(ps_lines) + "\n", encoding="utf-8-sig")
(root / "Join-R1.4.9-Install.cmd").write_text(
    '@echo off\npowershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Join-R1.4.9-Install.ps1"\nif errorlevel 1 pause\n', encoding="ascii"
)
(root / "补丁安装包分包校验.json").write_text(json.dumps({"FullZipSha256": expected, "Parts": parts}, ensure_ascii=False, indent=2) + "\n", encoding="utf-8")
manifest_path = root / "SHA256.json"
manifest = json.loads(manifest_path.read_text())
for path in [*(root / item["Name"] for item in parts), root / "Join-R1.4.9-Install.ps1", root / "Join-R1.4.9-Install.cmd", root / "补丁安装包分包校验.json"]:
    manifest[path.name] = {"Size": path.stat().st_size, "Sha256": hashlib.sha256(path.read_bytes()).hexdigest()}
manifest_path.write_text(json.dumps(dict(sorted(manifest.items())), indent=2, ensure_ascii=False) + "\n")
print(f"{len(parts)} parts, {archive.stat().st_size} bytes, SHA256 {expected}")
