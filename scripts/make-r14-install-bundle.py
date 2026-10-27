"""Pin the R1.4 installer to the published payload commit, then build offline bundle."""
from pathlib import Path
import hashlib,json,zipfile,subprocess,sys
r=Path(__file__).resolve().parents[1];out=r/'downloads/R1.4';commit=sys.argv[1] if len(sys.argv)>1 else subprocess.check_output(['git','rev-parse','HEAD'],cwd=r,text=True).strip()
p=out/'Install-R1.4-patch.ps1';s=p.read_text(encoding='utf-8-sig');s=s.replace('__ARTIFACT_COMMIT__',commit);assert '__ARTIFACT_COMMIT__' not in s;p.write_text(s,encoding='utf-8-sig')
command='''@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install-R1.4-patch.ps1" -OfflineZip "%~dp0FiberRouteR1-R1.4-patch.zip" -NoOpen
if errorlevel 1 (
  echo Installation failed. Please read the error above.
  pause
  exit /b 1
)
echo SUCCESS. Start the app with your existing shortcut.
pause
'''.replace('\n','\r\n').encode('ascii')
notes='''关闭主程序，将安装包全部解压，双击安装补丁.cmd。选择原运行目录（包含“程序”“数据”的文件夹）。成功后从原快捷方式启动，标题R1.4。
不要再次解压内部FiberRouteR1-R1.4-patch.zip，脚本需校验其原始字节。
补丁保留数据库和人工记录；首次启动整理区域/来源基线，完成后清理安装基线。
管理方工具和指南随补丁安装到原运行目录；暂无生产地址，两个更新按钮默认显示“更新服务未配置”。
'''.encode('utf-8-sig')
files={'R1.4补丁/安装补丁.cmd':command,'R1.4补丁/安装说明.txt':notes,'R1.4补丁/Install-R1.4-patch.ps1':p.read_bytes(),'R1.4补丁/FiberRouteR1-R1.4-patch.zip':(out/'FiberRouteR1-R1.4-patch.zip').read_bytes()}
target=out/'FiberRouteR1-R1.4-install.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for path,data in sorted(files.items()):
  info=zipfile.ZipInfo(path,(2026,10,27,12,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
with zipfile.ZipFile(target) as z:assert z.testzip() is None
metadata={p.name:{'Size':p.stat().st_size,'Sha256':hashlib.sha256(p.read_bytes()).hexdigest()} for p in [target,p,out/'FiberRouteR1-R1.4-patch.zip']};(out/'SHA256.json').write_text(json.dumps(metadata,indent=2));print(json.dumps(metadata,indent=2))
