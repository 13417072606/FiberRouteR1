"""Package the auxiliary publisher without replacing an installed app's runtime.

Run after publishing FiberRoute.Online to artifacts/online. Pass the published
payload commit to also create the hash-pinned Windows PowerShell bootstrap.
"""
from pathlib import Path
import hashlib
import json
import re
import sys
import zipfile

root=Path(__file__).resolve().parents[1]
out=root/'downloads/online-setup';out.mkdir(parents=True,exist_ok=True)
archive=out/'FiberRoute-Online-Setup-20261028.zip'
sha=lambda b:hashlib.sha256(b).hexdigest()
if len(sys.argv)==1:
    files={'Continue-Online-Update.ps1':(root/'scripts/Continue-Online-Update.ps1').read_text().encode('utf-8-sig')}
    for ext in ['.exe','.dll','.deps.json','.runtimeconfig.json']:
        name='FiberRoute.Online'+ext;files['工具/'+name]=(root/'artifacts/online'/name).read_bytes()
    files['使用说明.txt']=(root/'docs/online-setup/使用说明.md').read_text().encode('utf-8-sig')
    with zipfile.ZipFile(archive,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
        for name,data in sorted(files.items()):
            info=zipfile.ZipInfo(name,(2026,10,28,20,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
else:
    revision=sys.argv[1];assert re.fullmatch('[0-9a-f]{40}',revision)
    script=r'''param([ValidateSet('Prepare','Verify','Inspect')][string]$Stage='Prepare', [string]$RuntimeDirectory='', [string]$WorkspaceDirectory='F:\管线资源发布区', [string]$DataDirectory='')
$ErrorActionPreference='Stop'
$PSNativeCommandUseErrorActionPreference=$false
[Net.ServicePointManager]::SecurityProtocol=[Net.SecurityProtocolType]::Tls12
$directory=Join-Path $env:USERPROFILE 'Downloads\FiberRoute-Online-Setup'
New-Item -ItemType Directory -Path $directory -Force | Out-Null
$archive=Join-Path $directory 'FiberRoute-Online-Setup-20261028.zip'
function Test-Archive([string]$Path) { return ((Test-Path -LiteralPath $Path -PathType Leaf) -and (Get-Item -LiteralPath $Path).Length -eq __BYTES__ -and (Get-FileHash -LiteralPath $Path -Algorithm SHA256).Hash -eq '__HASH__') }
if (!(Test-Archive $archive)) {
    $partial=$archive+'.partial'
    $urls=@('https://raw.githubusercontent.com/13417072606/FiberRouteR1/__REV__/downloads/online-setup/FiberRoute-Online-Setup-20261028.zip','https://api.github.com/repos/13417072606/FiberRouteR1/contents/downloads/online-setup/FiberRoute-Online-Setup-20261028.zip?ref=__REV__')
    for ($i=0;$i -lt 4;$i++) {
        curl.exe --fail --location --http1.1 --tlsv1.2 --retry 2 --connect-timeout 20 --max-time 120 --header 'Accept: application/vnd.github.raw+json' --output $partial $urls[$i % 2]
        if ($LASTEXITCODE -eq 0 -and (Test-Archive $partial)) { Move-Item -LiteralPath $partial -Destination $archive -Force; break }
    }
    if (!(Test-Archive $archive)) { throw '辅助工具下载或SHA256校验失败，未继续发布。' }
}
$expanded=Join-Path $directory ('工具-'+[Guid]::NewGuid().ToString('N'))
Expand-Archive -LiteralPath $archive -DestinationPath $expanded
try { & (Join-Path $expanded 'Continue-Online-Update.ps1') -Stage $Stage -RuntimeDirectory $RuntimeDirectory -WorkspaceDirectory $WorkspaceDirectory -DataDirectory $DataDirectory }
finally { if (Test-Path -LiteralPath $expanded) { Remove-Item -LiteralPath $expanded -Recurse -Force } }
'''
    script=script.replace('__BYTES__',str(archive.stat().st_size)).replace('__HASH__',sha(archive.read_bytes())).replace('__REV__',revision)
    (out/'Start-Online-Update.ps1').write_text(script,encoding='utf-8-sig')
meta={p.name:{'Bytes':p.stat().st_size,'Sha256':sha(p.read_bytes())} for p in out.iterdir() if p.suffix in ['.zip','.ps1']}
(out/'SHA256.json').write_text(json.dumps(meta,indent=2)+'\n');print(json.dumps(meta,indent=2))
