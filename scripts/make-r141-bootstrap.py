"""Generate a pinned, hash-checked Windows PowerShell download entry."""
from pathlib import Path
import hashlib,json,re,sys
r=Path(__file__).resolve().parents[1];out=r/'downloads/R1.4.1'
commit=sys.argv[1];assert re.fullmatch(r'[0-9a-f]{40}',commit)
metadata=json.loads((out/'SHA256.json').read_text())
script=(r/'downloads/R1.4/Download-Install-R1.4.ps1').read_text(encoding='utf-8-sig')
script=script.replace('R1.4','R1.4.1').replace('b8cb611bbe477af1ed5908ca36d264d46c9e918d',commit)
for name,oldsize,oldsha in [
 ('FiberRouteR1-R1.4.1-install.zip','6907621','8fbf63290068686120b0c792289be51a32265d02da1276e3c3c7d563018cc39a'),
 ('FiberRouteR1-R1.4.1-patch.zip','6903781','5f18937001474eeb5e4438e8439c8546ffcc6b9ceda4a4b2b8119fc2e8e4ba6b'),
 ('Install-R1.4.1-patch.ps1','9684','f7a72b6409004671dc3230394a9ea63e9d32166b87976e58d0008661158d3d87')]:
 script=script.replace(oldsize,str(metadata[name]['Size'])).replace(oldsha,metadata[name]['Sha256'])
script=script.replace("$ErrorActionPreference = 'Stop'", "$ErrorActionPreference = 'Stop'\n$PSNativeCommandUseErrorActionPreference = $false")
script=script.replace('$attempt -lt 4','$attempt -lt 6').replace('（{0}/4）','（{0}/6）')
script=script.replace('R1.4.1 已安装。请从原快捷方式启动，首次启动会整理区域和来源信息。','R1.4.1 已安装，数据库及人工记录保留。请从原快捷方式启动。')
needle="    Write-Host ('已校验的离线安装包保留在：' + $outer)"
script=script.replace(needle, """    Write-Host ('管理方图形工具：' + (Join-Path $Target '管理方发布工具.exe'))
    Write-Host ('品牌原图替换工具：' + (Join-Path $Target '更换品牌图标.exe'))
    Write-Host '图标工具需选择本机保存的原始PNG一次。单位真实HTTPS地址尚未提供，在线更新服务暂未配置。'
"""+needle)
p=out/'Download-Install-R1.4.1.ps1';p.write_text(script,encoding='utf-8-sig')
metadata[p.name]={'Size':p.stat().st_size,'Sha256':hashlib.sha256(p.read_bytes()).hexdigest()}
(out/'SHA256.json').write_text(json.dumps(metadata,indent=2));print(json.dumps(metadata,indent=2))
