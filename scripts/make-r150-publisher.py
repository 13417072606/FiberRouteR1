"""Build the separate R1.5.0 administrator publisher without business data or keys."""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
output = root / 'downloads/R1.5.0/FiberRouteR1-Publisher-R1.5.0.zip'
program = root / 'artifacts/publisher-r150'
prefix = '管理方发布工具R1.5.0/'

with ZipFile(root / 'downloads/R1.4.6/FiberRouteR1-win-x64-R1.4.6.zip') as old:
    launcher = old.read('光路调度路由查询R1.4.6/管理方发布工具.exe')

files = {prefix + '管理方发布工具.exe': launcher}
for path in sorted(program.iterdir()):
    if path.is_file() and path.suffix.lower() != '.pdb':
        files[prefix + '程序/' + path.name] = path.read_bytes()

files[prefix + '使用说明.txt'] = '''管理方发布工具 R1.5.0

完整解压后双击“管理方发布工具.exe”，不要只复制 EXE；“程序”文件夹是运行依赖。
在同一台已能运行 R1.5.0 客户端的电脑上，无须另外安装 .NET SDK。

管理工作目录使用原来的 F:\\管线资源发布区。若这是重建的目录，请先核实原 private-key.pem
确实位于该目录；不要新建第二套签名密钥，也不要把密钥发给客户或上传服务器。
资源来源目录应选主程序“设置”中显示的实际数据目录，而不是这个工具包。
客户 HTTPS 地址使用现有站点 https://guangludiaodu.pages.dev/；Cloudflare Pages 场景下
“服务器发布目录”留空。分别为数据和软件生成更新包、发布清单后，在第 4 步点击
“生成 Cloudflare 上传ZIP”。将生成的“待上传-时间.zip”部署到现有 Pages 项目生产环境，
不要上传整个管理工作目录。上传后在第 4 步验证外网清单，再用一台客户端分别检测
软件和数据更新。软件来源要选升级后的完整程序目录；数据来源要选实际数据目录。

现有 Pages 是公开站点：数据更新包包含明文中继段和设施归属，知道地址的人可下载。
本工具仅保证签名、完整性和版本校验，不提供数据加密或访问限制。

此包不含业务数据库、原始表格、活动服务器配置或签名私钥。
'''.encode('utf-8-sig')

assert any(name.endswith('/程序/FiberRoute.Publisher.exe') for name in files)
assert not any(name.endswith(('.db', '.csv', '.xlsx', '.pem', '.pdb')) for name in files)
output.parent.mkdir(parents=True, exist_ok=True)
with ZipFile(output, 'w', ZIP_DEFLATED, compresslevel=6) as archive:
    for name, data in sorted(files.items()):
        entry = ZipInfo(name, (2026, 10, 30, 0, 0, 0))
        entry.compress_type = ZIP_DEFLATED
        entry.external_attr = 0o644 << 16
        archive.writestr(entry, data)
with ZipFile(output) as archive:
    assert archive.testzip() is None
print(output.name, output.stat().st_size, sha256(output.read_bytes()).hexdigest())
