# R1.3.4 路由 Excel 导出补丁

- [下载完整小补丁安装包](FiberRouteR1-R1.3.4-install.zip?raw=true)：解压后双击 `安装补丁.cmd`。
- [下载修正后的 Excel 样例](Route-Export-Sample.xlsx?raw=true) · [样例 ZIP 备用下载](Route-Export-Sample.zip?raw=true)
- [修改清单](../../docs/R1.3.4修改清单.md) · [验证结果](验证结果.txt) · [SHA256](SHA256.json)
- [单独补丁](FiberRouteR1-R1.3.4-patch.zip?raw=true) · [安装脚本](Install-R1.3.4-patch.ps1)

关闭原程序，解压安装包后双击安装入口。安装器会寻找原运行目录；需要选择时，请选包含“程序”“数据”的文件夹。完成后从原来的程序或快捷方式启动，标题显示 R1.3.4。

安装器若提示多个运行目录，可在解压目录打开 PowerShell，明确指定要更新的原目录：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-R1.3.4-patch.ps1 -Target "$env:USERPROFILE\Desktop\FiberRouteR1-R1.3\运行目录\光路调度智能分析R1.3" -OfflineZip .\FiberRouteR1-R1.3.4-patch.zip
```

本版本已将“导出方案”改为 Excel 工作簿。“路由总览”新增“跳纤路由”，按实际设施顺序串联；每条方案单独明细。删除“资源追溯”“字段说明”及指定说明段落。支持当前方案、全部候选方案两种导出，并保留原方案号和子表跳转。

样例由程序同一模块生成，包含此前CSV里的8条路由、39段。旧CSV缺少一处内部衔接依据，样例仍标注待核对；软件导出使用路由计算结果中的实际衔接。

适用于 R1.3 / R1.3.1 / R1.3.2 / R1.3.3，可重复安装；保留数据库、人工维护及原始表格。完整新装继续使用 [R1.3.2完整交付](../R1.3.2/)，然后应用本补丁。本次未更换品牌图标。
