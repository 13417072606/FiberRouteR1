"""Reuse the audited native launcher and replace its equal-length product-title literal.
The C source contains the same new literal for fresh MinGW builds. PE code and icons stay unchanged.
"""
from pathlib import Path
import zipfile
r=Path(__file__).resolve().parents[1];out=r/'artifacts/native-r143';out.mkdir(parents=True,exist_ok=True)
with zipfile.ZipFile(r/'downloads/R1.4.1/FiberRouteR1-R1.4.1-patch.zip') as z:
 for kind,name in [('app','光路调度智能分析.exe'),('publisher','管理方发布工具.exe'),('brand','更换品牌图标.exe')]:
  b=z.read(name);old='光路调度智能分析'.encode('utf-16le');new='光路调度路由查询'.encode('utf-16le');assert len(old)==len(new)
  if kind=='app':assert b.count(old)==1;b=b.replace(old,new)
  (out/f'{kind}.exe').write_bytes(b)
