"""Produce a source-only R1.5.1 ZIP without binary releases or business data."""
from pathlib import Path
from subprocess import check_output
from zipfile import ZipFile, ZIP_DEFLATED

root = Path(__file__).resolve().parents[1]
out = root / 'downloads/R1.5.1/FiberRouteR1-R1.5.1-source.zip'
paths = check_output(['git', 'ls-files', '-z'], cwd=root).split(b'\0')
include = ('src/', 'tests/', 'scripts/', 'assets/')
root_files = {'FiberRouteR1.sln', 'Directory.Build.props', 'Directory.Build.targets', 'global.json', 'README.md'}
exclude = {'.db', '.pem', '.xlsx', '.xls', '.zip', '.exe', '.dll', '.pdb'}
with ZipFile(out, 'w', ZIP_DEFLATED, compresslevel=6) as archive:
    for raw in paths:
        if not raw:
            continue
        name = raw.decode()
        if not (name.startswith(include) or name in root_files):
            continue
        if Path(name).suffix.lower() in exclude or '/bin/' in name or '/obj/' in name:
            continue
        archive.write(root / name, 'FiberRouteR1-R1.5.1-source/' + name)
print(out, out.stat().st_size)
