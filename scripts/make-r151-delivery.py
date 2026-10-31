"""Build program-only online source and a separate R1.5.1 administrator tool."""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.5.1'
app = root / 'artifacts/app-r151'
publisher = root / 'artifacts/publisher-r151-standalone'
updater = root / 'artifacts/updater-r150'


def archive(path, files):
    path.parent.mkdir(parents=True, exist_ok=True)
    with ZipFile(path, 'w', ZIP_DEFLATED, compresslevel=6) as target:
        for name, data in sorted(files.items()):
            entry = ZipInfo(name, (2026, 10, 31, 0, 0, 0))
            entry.compress_type = ZIP_DEFLATED
            entry.external_attr = 0o644 << 16
            target.writestr(entry, data)
    with ZipFile(path) as result:
        assert result.testzip() is None
        assert not any(name.lower().endswith(('.db', '.pem', '.csv', '.xlsx', '.pdb')) for name in result.namelist())
    print(path.name, path.stat().st_size, sha256(path.read_bytes()).hexdigest())


with ZipFile(root / 'downloads/R1.5.0/FiberRouteR1-R1.5.0-patch.zip') as previous:
    app_launcher = previous.read('光路调度路由查询.exe')
with ZipFile(root / 'downloads/R1.4.6/FiberRouteR1-win-x64-R1.4.6.zip') as previous:
    publisher_launcher = previous.read('光路调度路由查询R1.4.6/管理方发布工具.exe')

program_prefix = 'R1.5.1软件发布源/'
program = {program_prefix + '光路调度路由查询.exe': app_launcher}
for path in app.rglob('*'):
    if path.is_file() and path.suffix.lower() != '.pdb' and path.name != 'createdump.exe':
        program[program_prefix + '程序/' + path.relative_to(app).as_posix()] = path.read_bytes()
for name in ('FiberRoute.Updater.exe', 'FiberRoute.Updater.dll', 'FiberRoute.Updater.deps.json', 'FiberRoute.Updater.runtimeconfig.json'):
    program[program_prefix + '程序/' + name] = (updater / name).read_bytes()
program[program_prefix + '使用说明.txt'] = (
    '这是 R1.5.1 软件在线发布的完整程序来源，不含业务数据库和签名私钥。\r\n'
    '完整解压后，在管理方发布工具第 1 步选“软件更新”，来源目录选本文件夹，版本填 1.5.1。\r\n'
    '第 3 步生成签名清单；第 4 步生成 Cloudflare 上传 ZIP，解压后部署到原 Pages 项目。\r\n'
    '已配置在线更新的 R1.5.0 客户端需手动检测并升级这一次；安装 R1.5.1 后，每天首次打开时会后台检查一次。\r\n'
    '发现新版时右上角红色更新标记会保留到相应更新完成；当天再次启动不自动重复检查。\r\n'
).encode('utf-8-sig')
archive(out / 'FiberRouteR1-R1.5.1-program-source.zip', program)

admin_prefix = '管理方发布工具R1.5.1/'
admin = {admin_prefix + '管理方发布工具.exe': publisher_launcher}
for path in publisher.iterdir():
    if path.is_file() and path.suffix.lower() != '.pdb':
        admin[admin_prefix + '程序/' + path.name] = path.read_bytes()
admin[admin_prefix + '使用说明.txt'] = (
    '完整解压后双击“管理方发布工具.exe”，不要单独复制 EXE。\r\n'
    '第 2 步沿用 F:\\管线资源发布区及原签名密钥，不要另建密钥。\r\n'
    '点击“生成客户启用包”可给其他电脑分发公开地址和公钥；私钥永不发给客户。\r\n'
    '第 4 步生成 Cloudflare 上传 ZIP，解压后部署到现有 Pages 项目；无需单位服务器。\r\n'
).encode('utf-8-sig')
archive(out / 'FiberRouteR1-Publisher-R1.5.1.zip', admin)
