"""Create a clean standalone publisher EXE bundle and Windows deployment kit."""
import zipfile,hashlib,json
from pathlib import Path
r=Path(__file__).resolve().parents[1];out=r/'downloads/R1.4.1';out.mkdir(exist_ok=True)
notes='''管理方发布工具 R1.4.1

全部解压后，双击“管理方发布工具.exe”。保留“程序”文件夹完整，无须安装.NET SDK。此包不包含客户数据库、管理方私钥或活动服务器地址。
按界面的四步操作：生成更新包 → 配置服务 → 生成发布清单 → 发布与验证。
目前尚待单位真实外网网址，先向网管确认既有HTTPS站点和更新目录。详见随包使用说明及服务器部署文件夹。
若已在主程序安装R1.4.1修订补丁，原运行目录已自带相同工具，无须另下此独立包。
'''.encode('utf-8-sig')
def write(target,files):
 with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
  for name,data in sorted(files.items()):
   info=zipfile.ZipInfo(name,(2026,10,27,16,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
 with zipfile.ZipFile(target) as z:assert z.testzip() is None
files={'管理方发布工具R1.4.1/管理方发布工具.exe':(r/'artifacts/native-r141/publisher.exe').read_bytes(),'管理方发布工具R1.4.1/使用说明.txt':notes,'管理方发布工具R1.4.1/管理方EXE使用说明.md':(r/'docs/R1.4.1/管理方EXE使用说明.md').read_bytes()}
for p in (r/'artifacts/publisher-r141').rglob('*'):
 if p.is_file() and p.suffix.lower()!='.pdb':files['管理方发布工具R1.4.1/程序/'+str(p.relative_to(r/'artifacts/publisher-r141')).replace('\\','/')]=p.read_bytes()
files['管理方发布工具R1.4.1/更换品牌图标.exe']=(r/'artifacts/native-r141/brand.exe').read_bytes()
files['管理方发布工具R1.4.1/程序/FiberRouteR1.dll']=(r/'artifacts/app-r141/FiberRouteR1.dll').read_bytes()
for ext in ['.exe','.dll','.deps.json','.runtimeconfig.json']:files['管理方发布工具R1.4.1/程序/FiberRoute.Brand'+ext]=(r/'artifacts/brand-r141'/('FiberRoute.Brand'+ext)).read_bytes()
for p in (r/'deployment/windows').iterdir():
 if p.is_file():files['管理方发布工具R1.4.1/服务器部署/'+p.name]=p.read_bytes()
with zipfile.ZipFile(r/'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip') as old:
 for n in old.namelist():
  if n.startswith('光路调度智能分析R1.3/第三方许可/') and not n.endswith('/'):files['管理方发布工具R1.4.1/'+n.removeprefix('光路调度智能分析R1.3/')]=old.read(n)
target=out/'FiberRouteR1-Publisher-win-x64-R1.4.1.zip';write(target,files)
raw=target.read_bytes();meta={target.name:{'Size':len(raw),'Sha256':hashlib.sha256(raw).hexdigest()}}
for i,start in enumerate(range(0,len(raw),20_000_000),1):
 p=out/(target.name+f'.part{i:03}');b=raw[start:start+20_000_000];p.write_bytes(b);meta[p.name]={'Size':len(b),'Sha256':hashlib.sha256(b).hexdigest()}
kit=out/'FiberRouteR1-Windows-Server-Kit-R1.4.1.zip';write(kit,{'Windows服务器部署/'+p.name:p.read_bytes() for p in (r/'deployment/windows').iterdir() if p.is_file()});meta[kit.name]={'Size':kit.stat().st_size,'Sha256':hashlib.sha256(kit.read_bytes()).hexdigest()};(out/'Tools-SHA256.json').write_text(json.dumps(meta,indent=2));print(json.dumps(meta,indent=2))
