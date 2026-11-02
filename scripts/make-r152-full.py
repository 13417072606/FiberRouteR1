"""Build the complete R1.5.2 client layout from the verified program release.

Business databases and signing keys are deliberately never inferred from an old release.
"""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
import sys

root = Path(__file__).resolve().parents[1]
source = Path(sys.argv[1])
out = Path(sys.argv[2])
prefix = '光路调度路由分析R1.5.2/'
files = {}
with ZipFile(source) as program:
    for name in program.namelist():
        if not name.startswith(prefix) or name.endswith('/'):
            continue
        relative = name[len(prefix):]
        if relative.startswith(('数据/', '资源/', '第三方许可/')):
            raise ValueError('Unexpected client resource in program-only source: ' + relative)
        files[relative] = program.read(name)
with ZipFile(root / 'downloads/R1.4.6/FiberRouteR1-win-x64-R1.4.6.zip') as prior:
    base = '光路调度路由查询R1.4.6/'
    for name in prior.namelist():
        if not name.startswith(base) or name.endswith('/'):
            continue
        relative = name[len(base):]
        if relative.startswith('第三方许可/') or relative in (
            '资源/中继段导入模板.csv', '资源/节点类型模板.csv', '资源/调测中继段.csv'
        ):
            files[relative] = prior.read(name)
assert any(name.startswith('第三方许可/') for name in files)
assert '资源/中继段导入模板.csv' in files
assert '程序/FiberRouteR1.dll' in files and '程序/FiberRoute.Updater.exe' in files
assert '光路调度路由查询.exe' in files
files['使用说明.txt'] = (
    'R1.5.2 完整客户端（程序与静态资源）。解压后运行光路调度路由查询.exe。\r\n'
    '首次运行会在本文件夹内建立“数据”目录；已有电脑若保存过自定义数据目录，继续使用原目录。\r\n'
    '本包不含业务数据库、管理员密钥或管理方发布工具；请在软件“设置”中检测数据更新。\r\n'
    '要让新电脑直接检测在线更新，管理员须用同版配置打包脚本加入原工作区的公开 update-service.json。\r\n'
    '第三方许可与导入模板已随包提供。服务器部署资料在管理方工具中，旧版在线升级辅助脚本已停用。\r\n'
).encode('utf-8-sig')
for name in files:
    if name.lower().endswith(('.db', '.pem', '.xlsx', '.pdb')):
        raise ValueError('Private or stale data in package: ' + name)
manifest = {name: sha256(data).hexdigest().upper() for name, data in sorted(files.items())}
import json
files['文件校验SHA256.json'] = json.dumps(manifest, ensure_ascii=False, indent=2).encode('utf-8')
out.parent.mkdir(parents=True, exist_ok=True)
with ZipFile(out, 'w', ZIP_DEFLATED, compresslevel=6) as target:
    for name, data in sorted(files.items()):
        entry = ZipInfo(prefix + name, (2026, 11, 2, 0, 0, 0))
        entry.compress_type = ZIP_DEFLATED
        target.writestr(entry, data)
    for directory in ('数据/',):
        target.writestr(prefix + directory, b'')
with ZipFile(out) as result:
    assert result.testzip() is None
    assert all(name.startswith(prefix) for name in result.namelist())
print(out.name, out.stat().st_size, sha256(out.read_bytes()).hexdigest())
