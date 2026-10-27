"""Build immutable R1.4 payload and verified installer for all published R1.3 bases."""
import hashlib,json,zipfile
from pathlib import Path
root=Path(__file__).resolve().parents[1];out=root/'downloads/R1.4';out.mkdir(parents=True,exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
base=zipfile.ZipFile(root/'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip')
patches=[zipfile.ZipFile(root/f'downloads/R1.3.{i}/FiberRouteR1-R1.3.{i}-patch.zip') for i in range(1,5)]
files={};manifest=[]
for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json']:files['程序/'+name]=(root/'artifacts/app-r14'/name).read_bytes()
for project,folder in [('FiberRoute.Updater','updater-r14'),('FiberRoute.Admin','admin-r14')]:
 for ext in ['.exe','.dll','.deps.json','.runtimeconfig.json']:files['程序/'+project+ext]=(root/'artifacts'/folder/(project+ext)).read_bytes()
files['数据/机房归属基线.json']=patches[1].read('数据/机房归属基线.json')
files['数据/设施来源基线.db']=(root/'artifacts/r14-data/设施来源基线.db').read_bytes()
files['R1.4更新说明.md']=(root/'docs/R1.4修改清单.md').read_bytes()
files['管理方在线更新发布指南.md']=(root/'docs/R1.4-planning/Online-Update-Publishing.md').read_bytes()
files['机房归属来源复核.xlsx']=(root/'docs/R1.4-planning/Room-Affiliation-Origin-Audit.xlsx').read_bytes()
files['管理方发布工具.cmd']=b'@echo off\r\n"%~dp0\xe7\xa8\x8b\xe5\xba\x8f\\FiberRoute.Admin.exe" %*\r\nif "%~1"=="" pause\r\n'
# Windows cmd must decode the Chinese folder using the console's UTF-8 code page.
files['管理方发布工具.cmd']=b'@echo off\r\nchcp 65001 >nul\r\n'+files['管理方发布工具.cmd'].split(b'\r\n',1)[1]
files['资源/update-service.example.json']=json.dumps({'ManifestUrl':'https://updates.example.cn/fiber/manifest.json','PublicKeyPem':'由管理方发布工具init生成公钥配置；不要将本示例改名为活动配置'},ensure_ascii=False,indent=2).encode()
for path,data in sorted(files.items()):
 previous=[]
 if path in ['程序/FiberRouteR1.dll','程序/FiberRoute.Core.dll','程序/FiberRouteR1.deps.json']:
  previous.append(sha(base.read('光路调度智能分析R1.3/'+path)))
  previous += [sha(z.read(path)) for z in patches]
 manifest.append(dict(Path=path,Length=len(data),Sha256=sha(data),PreviousSha256=sorted(set(previous))))
files['补丁校验.json']=json.dumps(dict(Version='1.4.0',Files=manifest),ensure_ascii=False,indent=2).encode()
target=out/'FiberRouteR1-R1.4-patch.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for path,data in sorted(files.items()):
  info=zipfile.ZipInfo(path,(2026,10,27,12,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
with zipfile.ZipFile(target) as z:assert z.testzip() is None
script=(root/'scripts/install-r132-patch.ps1').read_text().replace('R1.3.2','R1.4')
a=script.index('    $allowed = @(');b=script.index('\n',a);script=script[:a]+'    $allowed = @('+','.join("'"+x+"'" for x in sorted(files))+')'+script[b:]
script=script.replace('已发布的 R1.3 / R1.3.1 不符','已发布的 R1.3 / R1.3.1 / R1.3.2 / R1.3.3 / R1.3.4 不符')
script=script.replace("throw '补丁文件大小或 SHA256 校验失败，未修改现有程序。'", "throw ('补丁缺失或校验失败，未修改现有程序。路径：' + $zip + '；预期大小：' + $expectedSize + '；请重新使用管理员PowerShell下载完整安装包，不要用压缩软件解开其中的补丁ZIP。')")
script=script.replace('__ZIP_SIZE__',str(target.stat().st_size)).replace('__ZIP_SHA__',sha(target.read_bytes())).replace('?ref=r1-revised-20261025','?ref=__ARTIFACT_COMMIT__').replace('/r1-revised-20261025/downloads/','/__ARTIFACT_COMMIT__/downloads/')
(out/'Install-R1.4-patch.ps1').write_text(script,encoding='utf-8-sig')
meta={target.name:dict(Size=target.stat().st_size,Sha256=sha(target.read_bytes()))};(out/'SHA256.json').write_text(json.dumps(meta,indent=2));print(json.dumps(meta,indent=2))
