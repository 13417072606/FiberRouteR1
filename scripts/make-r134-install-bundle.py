"""Wrap the pinned, verified installer and small patch for double-click installation."""
import hashlib
import json
import zipfile
from pathlib import Path

root=Path(__file__).resolve().parents[1]
out=root/'downloads/R1.3.4'
installer=out/'Install-R1.3.4-patch.ps1'
assert '__ARTIFACT_COMMIT__' not in installer.read_text(encoding='utf-8-sig')
command='''@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-R1.3.4-patch.ps1" -OfflineZip "%~dp0FiberRouteR1-R1.3.4-patch.zip" -NoOpen
if errorlevel 1 (
  echo.
  echo Installation failed. Please read the error above.
  pause
  exit /b 1
)
echo.
echo Done. Start the app using your existing shortcut.
pause
'''.replace('\n','\r\n').encode('ascii')
notes='''R1.3.4 补丁安装说明

1. 关闭正在运行的“光路调度智能分析”。
2. 将本ZIP全部解压到一个文件夹，不要直接在压缩包内运行。
3. 双击“安装补丁.cmd”。脚本会自动定位原程序目录；必要时请选包含“程序”“数据”的旧运行目录。
4. 看到绿色 SUCCESS 后，仍从原来的程序或桌面快捷方式启动，标题应显示 R1.3.4。

本包包含完整小补丁，不需要联网下载其他文件。保留原数据库及人工确认。
导出方案已更新为Excel工作簿；总览新增跳纤路由，每条方案单独明细，删除资源追溯及字段说明。
若提示找到多个运行目录，请按README使用-Target明确指定要更新的目录。
'''.encode('utf-8-sig')
files={
    'R1.3.4补丁/安装补丁.cmd':command,
    'R1.3.4补丁/安装说明.txt':notes,
    'R1.3.4补丁/Install-R1.3.4-patch.ps1':installer.read_bytes(),
    'R1.3.4补丁/FiberRouteR1-R1.3.4-patch.zip':(out/'FiberRouteR1-R1.3.4-patch.zip').read_bytes(),
}
target=out/'FiberRouteR1-R1.3.4-install.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as archive:
    for path,data in sorted(files.items()):
        info=zipfile.ZipInfo(path,(2026,10,27,0,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16
        archive.writestr(info,data)
with zipfile.ZipFile(target) as archive:assert archive.testzip() is None
metadata={}
for path in [target,installer,out/'FiberRouteR1-R1.3.4-patch.zip',out/'Route-Export-Sample.xlsx',out/'Route-Export-Sample.zip']:
    metadata[path.name]=dict(Size=path.stat().st_size,Sha256=hashlib.sha256(path.read_bytes()).hexdigest())
(out/'SHA256.json').write_text(json.dumps(metadata,ensure_ascii=False,indent=2),encoding='utf-8')
print(json.dumps(metadata,ensure_ascii=False,indent=2))
