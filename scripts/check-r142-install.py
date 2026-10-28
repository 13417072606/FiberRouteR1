"""Exercise the cumulative installer against each published base in isolated fixtures."""
from pathlib import Path
import hashlib,json,os,shutil,subprocess,tempfile,zipfile
repo=Path(__file__).resolve().parents[1];out=Path(os.environ.get('FIBER_INSTALL_FIXTURES',tempfile.mkdtemp(prefix='fiber-r142-')));out.mkdir(parents=True,exist_ok=True)
package=repo/'downloads/R1.4.2/FiberRouteR1-R1.4.2-patch.zip';installer=package.parent/'Install-R1.4.2-patch.ps1';pwsh=os.environ.get('FIBER_PWSH','pwsh')
sha=lambda p:hashlib.sha256(p.read_bytes()).hexdigest()
with zipfile.ZipFile(package) as z:manifest=json.loads(z.read('补丁校验.json'))
def fixture(v):
 target=out/(v+'-'+__import__('uuid').uuid4().hex[:8])/'原运行目录';(target/'程序').mkdir(parents=True,exist_ok=True);(target/'数据').mkdir(exist_ok=True)
 archive=repo/(f'downloads/{v}/FiberRouteR1-{v}-patch.zip' if v!='R1.3' else 'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip')
 with zipfile.ZipFile(archive) as z:
  for n in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json']:(target/'程序'/n).write_bytes(z.read(('光路调度智能分析R1.3/' if v=='R1.3' else '')+'程序/'+n))
 for n in ['personal.db','resources.db']:(target/'数据'/n).write_bytes(('customer-'+n).encode())
 return target
def run(target,z=package):return subprocess.run([pwsh,'-NoProfile','-File',str(installer),'-Target',str(target),'-OfflineZip',str(z),'-NoOpen'],text=True,capture_output=True)
for version in ['R1.3','R1.3.1','R1.3.2','R1.3.3','R1.3.4','R1.4','R1.4.1']:
 target=fixture(version);before={n:sha(target/'数据'/n) for n in ['personal.db','resources.db']};p=run(target);assert p.returncode==0,(version,p.stderr[-2000:])
 for f in manifest['Files']:assert sha(target/f['Path'])==f['Sha256']
 p=run(target);assert p.returncode==0,p.stderr
 assert all(sha(target/'数据'/n)==h for n,h in before.items());print('PASS',version,'upgrade and repeat; complete payload verified; customer DB bytes preserved',flush=True)
target=fixture('R1.3');old=sha(target/'程序/FiberRouteR1.dll');bad=out/'bad.zip';bad.write_bytes(b'truncated');assert run(target,bad).returncode!=0 and sha(target/'程序/FiberRouteR1.dll')==old;print('PASS truncated ZIP rejected without modifying program',flush=True)
(target/'程序/FiberRoute.Core.dll').write_bytes(b'unknown-base');assert run(target).returncode!=0 and sha(target/'程序/FiberRouteR1.dll')==old;print('PASS unknown base rejected before modifying program',flush=True)
# Force a fresh install-time conflict to exercise rollback.
target=fixture('R1.3')
(target/'资源/update-service.example.json').mkdir(parents=True,exist_ok=True);assert run(target).returncode!=0 and sha(target/'程序/FiberRouteR1.dll')==old and not (target/'程序/FiberRoute.Admin.exe').exists();print('PASS write conflict rolls back changed and newly created files',flush=True)
