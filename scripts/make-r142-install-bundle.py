"""Pin the R1.4.2 installer to a payload commit and build the offline bundle."""
from pathlib import Path
import hashlib
import json
import re
import subprocess
import sys
import zipfile

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.4.2'
commit = sys.argv[1] if len(sys.argv) > 1 else subprocess.check_output(
    ['git', 'rev-parse', 'HEAD'], cwd=root, text=True).strip()
assert re.fullmatch(r'[0-9a-f]{40}', commit), 'Pass the full published payload commit'
installer = out / 'Install-R1.4.2-patch.ps1'
script = installer.read_text(encoding='utf-8-sig')
script = re.sub(r'(?<=ref=)(?:__ARTIFACT_COMMIT__|[0-9a-f]{40})', commit, script)
script = re.sub(r'(?<=FiberRouteR1/)(?:__ARTIFACT_COMMIT__|[0-9a-f]{40})(?=/downloads/)', commit, script)
assert '__ARTIFACT_COMMIT__' not in script
installer.write_text(script, encoding='utf-8-sig')
command = '''@echo off
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Install-R1.4.2-patch.ps1" -OfflineZip "%~dp0FiberRouteR1-R1.4.2-patch.zip" -NoOpen
if errorlevel 1 (
  echo Installation failed. Please read the error above.
  pause
  exit /b 1
)
echo SUCCESS. Start the app with your existing shortcut.
pause
'''.replace('\n', '\r\n').encode('ascii')
notes = '''累计修订包，适用于R1.3至R1.4.1，无须逐个安装中间版本。
关闭主程序、管理方工具和品牌工具，将本安装包全部解压，双击安装补丁.cmd。
选择原来的运行目录，里面有“程序”“数据”和“光路调度智能分析.exe”。不要选新下载的补丁文件夹。
不要再次解压内部FiberRouteR1-R1.4.2-patch.zip，脚本需校验其原始字节。
成功后从原快捷方式启动，标题R1.4.2。数据库与人工记录保留。
双击原运行目录的管理方发布工具.exe，四步图形向导代替命令行操作。
双击更换品牌图标.exe选择本机原始PNG，等比更新全套图标，完整保留Y形；聊天原图未作为文件进入包内。
服务器部署文件夹包含Windows/IIS部署资料；Cloudflare测试站点已创建，签名清单、版本包和客户端配置仍需完成，详见管理方在线更新发布指南。
'''.encode('utf-8-sig')
payload = out / 'FiberRouteR1-R1.4.2-patch.zip'
files = {
    'R1.4.2补丁/安装补丁.cmd': command,
    'R1.4.2补丁/安装说明.txt': notes,
    'R1.4.2补丁/' + installer.name: installer.read_bytes(),
    'R1.4.2补丁/' + payload.name: payload.read_bytes(),
}
target = out / 'FiberRouteR1-R1.4.2-install.zip'
with zipfile.ZipFile(target, 'w', zipfile.ZIP_DEFLATED, compresslevel=6) as z:
    for path, data in sorted(files.items()):
        info = zipfile.ZipInfo(path, (2026, 10, 28, 16, 0, 0))
        info.compress_type = zipfile.ZIP_DEFLATED
        info.external_attr = 0o644 << 16
        z.writestr(info, data)
with zipfile.ZipFile(target) as z:
    assert z.testzip() is None
metadata = {p.name: {'Size': p.stat().st_size, 'Sha256': hashlib.sha256(p.read_bytes()).hexdigest()}
            for p in [target, installer, payload]}
(out / 'SHA256.json').write_text(json.dumps(metadata, indent=2))
print(json.dumps(metadata, indent=2))
