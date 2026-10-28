"""Build the cumulative R1.4.3 patch; keep earlier published releases immutable."""
import hashlib,json,zipfile
from pathlib import Path
root=Path(__file__).resolve().parents[1];import os
out=Path(os.environ.get('FIBER_PATCH_OUTPUT',str(root/'downloads/R1.4.3')));out.mkdir(parents=True,exist_ok=True)
sha=lambda b:hashlib.sha256(b).hexdigest()
base=zipfile.ZipFile(root/'downloads/R1.3/FiberRouteR1-win-x64-R1.3.zip')
previous=[zipfile.ZipFile(root/f'downloads/R1.3.{i}/FiberRouteR1-R1.3.{i}-patch.zip') for i in range(1,5)]+[zipfile.ZipFile(root/'downloads/R1.4/FiberRouteR1-R1.4-patch.zip'),zipfile.ZipFile(root/'downloads/R1.4.1/FiberRouteR1-R1.4.1-patch.zip'),zipfile.ZipFile(root/'downloads/R1.4.2/FiberRouteR1-R1.4.2-patch.zip')]
files={}
for name in ['FiberRouteR1.dll','FiberRoute.Core.dll','FiberRouteR1.deps.json','FiberRouteR1.exe','FiberRouteR1.runtimeconfig.json','app.ico']:files['程序/'+name]=(root/'artifacts/app-r143'/name).read_bytes()
for project,folder in [('FiberRoute.Publisher','publisher-r143'),('FiberRoute.Brand','brand-r143'),('FiberRoute.Updater','updater-r143'),('FiberRoute.Admin','admin-r143')]:
 for ext in ['.exe','.dll','.deps.json','.runtimeconfig.json']:files['程序/'+project+ext]=(root/'artifacts'/folder/(project+ext)).read_bytes()
for name,source in [('光路调度路由查询.exe','app.exe'),('光路调度智能分析.exe','app.exe'),('管理方发布工具.exe','publisher.exe'),('更换品牌图标.exe','brand.exe')]:files[name]=(root/'artifacts/native-r143'/source).read_bytes()
files['数据/机房归属基线.json']=previous[1].read('数据/机房归属基线.json');files['数据/设施来源基线.db']=(root/'artifacts/r14-data/设施来源基线.db').read_bytes()
files['R1.4.3更新说明.md']=(root/'docs/R1.4.3/R1.4.3修改清单.md').read_bytes();files['管理方在线更新发布指南.md']=(root/'docs/R1.4.3/管理方EXE使用说明.md').read_bytes()
for path in (root/'deployment/windows').iterdir():
 if path.is_file():files['服务器部署/'+path.name]=path.read_bytes()
files['管理方发布工具.cmd']=b'@echo off\r\nchcp 65001 >nul\r\n'+'''if "%~1"=="" (
  start "" "%~dp0管理方发布工具.exe"
) else (
  "%~dp0程序\\FiberRoute.Admin.exe" %*
)
'''.replace('\n','\r\n').encode('utf-8')
files['资源/update-service.example.json']=json.dumps({'ManifestUrl':'https://单位真实域名/fiber-updates/manifest.json','PublicKeyPem':'由管理方EXE生成；示例文件不能直接作为活动配置'},ensure_ascii=False,indent=2).encode()
if os.environ.get('FIBER_REPAIR_BASELINE'):files['数据/集群站点修复基线.db']=Path(os.environ['FIBER_REPAIR_BASELINE']).read_bytes()
manifest=[]
for path,data in sorted(files.items()):
 hashes=[]
 if path in ['程序/FiberRouteR1.dll','程序/FiberRoute.Core.dll','程序/FiberRouteR1.deps.json']:
  hashes=[sha(base.read('光路调度智能分析R1.3/'+path))]+[sha(z.read(path)) for z in previous]
 manifest.append(dict(Path=path,Length=len(data),Sha256=sha(data),PreviousSha256=sorted(set(hashes))))
files['补丁校验.json']=json.dumps(dict(Version='1.4.3',Files=manifest),ensure_ascii=False,indent=2).encode()
target=out/'FiberRouteR1-R1.4.3-patch.zip'
with zipfile.ZipFile(target,'w',zipfile.ZIP_DEFLATED,compresslevel=6) as z:
 for path,data in sorted(files.items()):
  info=zipfile.ZipInfo(path,(2026,10,28,16,0,0));info.compress_type=zipfile.ZIP_DEFLATED;info.external_attr=0o644<<16;z.writestr(info,data)
with zipfile.ZipFile(target) as z:assert z.testzip() is None
script=(root/'scripts/install-r132-patch.ps1').read_text().replace('R1.3.2','R1.4.3');a=script.index('    $allowed = @(');b=script.index('\n',a);script=script[:a]+'    $allowed = @('+','.join("'"+n+"'" for n in sorted(files))+')'+script[b:]
start=script.index('if ([string]::IsNullOrWhiteSpace($Target))');end=script.index('\n$Target = [IO.Path]::GetFullPath($Target)',start)
script=script[:start]+"""if ([string]::IsNullOrWhiteSpace($Target)) {
    $current = (Get-Location).ProviderPath
    if (Test-RunDirectory $current) { $Target = $current }
    else {
        if ($env:OS -ne 'Windows_NT') { throw '请使用-Target指定包含程序和数据的原运行目录。' }
        Add-Type -AssemblyName System.Windows.Forms
        $pick = New-Object System.Windows.Forms.FolderBrowserDialog
        $pick.Description = '选择要升级的原运行目录，里面有程序和数据；不要选择刚下载的补丁文件夹'
        try { if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { Write-Host '已取消，现有程序未修改。'; exit 2 }; $Target = $pick.SelectedPath } finally { $pick.Dispose() }
    }
}
"""+script[end:]
script=script.replace('Get-Process -Name FiberRouteR1','Get-Process -Name FiberRouteR1,FiberRoute.Publisher,FiberRoute.Brand,FiberRoute.Updater,FiberRoute.Admin');script=script.replace('请关闭光路调度智能分析，再重新运行本补丁。','请关闭主程序、管理方工具和图标工具，再重新运行本补丁。');script=script.replace('已发布的 R1.3 / R1.3.1 不符','已发布的 R1.3 至 R1.4.2 不符');script=script.replace("throw '补丁文件大小或 SHA256 校验失败，未修改现有程序。'","throw ('补丁缺失、下载不完整或SHA256不符，未修改现有程序。路径：' + $zip + '；预期大小：' + $expectedSize + '。请使用完整安装包，勿再次解压内部补丁ZIP。')")
needle="    Write-Host ('SUCCESS: R1.4.3 patch verified"
brand="""    $original = Join-Path $Target '资源\\品牌原图.png'
    if ((Test-Path -LiteralPath $original -PathType Leaf) -and $env:OS -eq 'Windows_NT') {
        $brand = Start-Process -FilePath (Join-Path $Target '程序\\FiberRoute.Brand.exe') -ArgumentList '--apply-existing' -Wait -PassThru
        if ($brand.ExitCode -ne 0) { throw '已有品牌原图应用失败，安装回退。请查看运行目录的品牌替换失败.txt。' }
    }
"""
assert needle in script;script=script.replace(needle,brand+needle);script=script.replace('__ZIP_SIZE__',str(target.stat().st_size)).replace('__ZIP_SHA__',sha(target.read_bytes())).replace('?ref=r1-revised-20261025','?ref=__ARTIFACT_COMMIT__').replace('/r1-revised-20261025/downloads/','/__ARTIFACT_COMMIT__/downloads/')
script=script.replace('请从原来的“光路调度智能分析.exe”启动','请从“光路调度路由查询.exe”启动')
if os.environ.get('FIBER_REPAIR_BASELINE'):script=script.replace("$ErrorActionPreference = 'Stop'","$ErrorActionPreference = 'Stop'\nif (-not $OfflineZip) { throw '专用修复包请完整解压后运行安装补丁.cmd，不能通过公开网址下载专用数据。' }")
(out/'Install-R1.4.3-patch.ps1').write_text(script,encoding='utf-8-sig')
meta={target.name:dict(Size=target.stat().st_size,Sha256=sha(target.read_bytes()))};(out/'SHA256.json').write_text(json.dumps(meta,indent=2));print(json.dumps(meta,indent=2))
