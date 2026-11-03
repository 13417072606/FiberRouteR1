"""Package the R1.5.4 publisher with a root launcher and private runtime folder."""
from pathlib import Path
from zipfile import ZipFile, ZipInfo, ZIP_DEFLATED
from hashlib import sha256

root = Path(__file__).resolve().parents[1]
runtime = root / 'artifacts/publisher-r154-tidy'
launcher = root / 'artifacts/native-r141/publisher.exe'
out = root / 'downloads/R1.5.4-publisher-tidy'
out.mkdir(parents=True, exist_ok=True)
assert launcher.is_file() and (runtime / 'FiberRoute.Publisher.exe').is_file()
files = {'管理方发布工具.exe': launcher.read_bytes()}
for path in runtime.rglob('*'):
    if path.is_file() and path.suffix.lower() != '.pdb' and path.name != 'createdump.exe':
        files['程序/' + path.relative_to(runtime).as_posix()] = path.read_bytes()
assert not any(name.endswith(('.db', '.pem', '.xlsx', '使用说明.txt')) for name in files)
target = out / 'FiberRouteR1-Publisher-R1.5.4-tidy.zip'
with ZipFile(target, 'w', ZIP_DEFLATED, compresslevel=6) as archive:
    for path, data in sorted(files.items()):
        entry = ZipInfo('管理方发布工具R1.5.4/' + path, (2026, 11, 3, 0, 0, 0))
        entry.compress_type = ZIP_DEFLATED
        archive.writestr(entry, data)
with ZipFile(target) as archive:
    assert archive.testzip() is None
    assert '管理方发布工具R1.5.4/管理方发布工具.exe' in archive.namelist()
    assert '管理方发布工具R1.5.4/程序/FiberRoute.Publisher.exe' in archive.namelist()
print(target.name, target.stat().st_size, sha256(target.read_bytes()).hexdigest())
