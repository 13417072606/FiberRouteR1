"""Package the tested R1.5.2 Windows application without administrator secrets."""
from hashlib import sha256
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.5.2/FiberRouteR1-R1.5.2-program-source.zip'
app = root / 'artifacts/app-r152'
updater = root / 'artifacts/updater-r152'
launcher = root / 'artifacts/launcher-r152/光路调度路由查询.exe'
assert (app / 'FiberRouteR1.dll').is_file() and launcher.is_file()
files = {'光路调度路由分析R1.5.2/光路调度路由查询.exe': launcher}
for path in app.rglob('*'):
    if path.is_file() and path.suffix.lower() != '.pdb' and path.name != 'createdump.exe':
        files['光路调度路由分析R1.5.2/程序/' + path.relative_to(app).as_posix()] = path
for name in ('FiberRoute.Updater.exe', 'FiberRoute.Updater.dll', 'FiberRoute.Updater.deps.json', 'FiberRoute.Updater.runtimeconfig.json'):
    files['光路调度路由分析R1.5.2/程序/' + name] = updater / name
out.parent.mkdir(parents=True, exist_ok=True)
with ZipFile(out, 'w', ZIP_DEFLATED, compresslevel=6) as archive:
    for name, path in sorted(files.items()):
        assert not name.lower().endswith(('.db', '.pem', '.csv', '.xlsx', '.pdb'))
        entry = ZipInfo(name, (2026, 11, 2, 0, 0, 0))
        entry.compress_type = ZIP_DEFLATED
        archive.writestr(entry, path.read_bytes())
with ZipFile(out) as archive:
    assert archive.testzip() is None
print(out, out.stat().st_size, sha256(out.read_bytes()).hexdigest())
