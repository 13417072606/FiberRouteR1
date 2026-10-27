# 光路调度智能分析 R1.4 补丁

[下载完整补丁安装包](FiberRouteR1-R1.4-install.zip?raw=true) · [下载管理员PowerShell安装脚本](Download-Install-R1.4.ps1?raw=true) · [修改清单](../../docs/R1.4修改清单.md) · [实际运行截图及验证](../../docs/R1.4-preview/) · [机房来源复核Excel](../../docs/R1.4-planning/Room-Affiliation-Origin-Audit.xlsx?raw=true) · [管理方发布指南](../../docs/R1.4-planning/Online-Update-Publishing.md)

关闭主程序，用管理员PowerShell运行下载脚本。脚本下载、校验并解压安装包，弹窗选择原运行目录（包含“程序”“数据”的文件夹），再次校验内部ZIP再安装。不需要在网页保存源码，不要用压缩软件把内部补丁ZIP再次解开。

离线安装：完整解压“FiberRouteR1-R1.4-install.zip”，双击“安装补丁.cmd”，按提示选择旧运行目录；或执行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File '.\Install-R1.4-patch.ps1' -Target '原运行目录完整路径' -OfflineZip '.\FiberRouteR1-R1.4-patch.zip' -NoOpen
```

补丁适用于R1.3～R1.3.4；保留resources.db、personal.db和人工记录，原启动器与桌面快捷方式继续使用。已测试五个旧版升级和重复安装、截断下载拒绝、错误基版拒绝及安装故障回退。

R1.4包括统一分页与每页数量、区域、完整换行、全局排序/快捷复核、机房来源追溯和挂墙异常暂停，以及软件/数据两条独立在线更新通道、管理方发布工具。首次启动导入结构化区域和来源基线，完成后清理安装基线。

在线服务尚未配置，两个按钮会如实显示“更新服务未配置”。管理方先部署国内HTTPS目录并使用发布工具生成签名清单和客户公钥配置。见发布指南。软件和数据更新包只含整理后的程序/数据，不重复携带源表，人工冲突保留并提示复核。

验证：核心151项；实际Windows界面在Wine10下96/144DPI各55项；全量272,561条中继段、264,608节点、277,159条来源证据；既有208.21米和2044.87米样例通过。原生Windows及管理方国内生产HTTPS尚待现场验证。


管理员PowerShell直接执行（校验脚本后启动，安装时会选择原运行目录）：

```powershell
$s = Join-Path $env:TEMP 'FiberRouteR1-R1.4-install.ps1'
curl.exe --fail --location --tlsv1.2 --retry 3 --output $s 'https://raw.githubusercontent.com/13417072606/FiberRouteR1/r1-revised-20261025/downloads/R1.4/Download-Install-R1.4.ps1'
if ($LASTEXITCODE -ne 0) { throw '下载失败，请重新运行' }
if ((Get-FileHash -LiteralPath $s -Algorithm SHA256).Hash -ne 'fbecd3f85f9bdd573aa0a6870cf6771c03d3501c22998b286a5e5eeb21b2d733') { throw '下载脚本校验失败' }
powershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File $s
```

各文件大小与SHA256见[校验清单](SHA256.json)。旧版交付文件不变。
