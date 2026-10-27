# R1.3.3 小补丁

已有 R1.3 / R1.3.1 / R1.3.2 使用本补丁。第 1～5 项界面问题已修复；第 6 项先提供模板，尚未替换软件导出格式。

- [下载安装包 ZIP](FiberRouteR1-R1.3.3-install.zip?raw=true)：内含补丁、安装脚本、双击入口及说明。解压后双击 `安装补丁.cmd`，关闭原程序再安装；不用复制浏览器里的脚本文字。
- [先看路由方案 Excel 模板](../../docs/导出模板/路由方案导出模板-待确认.xlsx?raw=true)：8 条路由、39 段；路由总览与每条路由的明细分开显示。
- [本次修改清单](../../docs/R1.3.3修改清单.md) · [实际运行截图](../../docs/R1.3.3-preview/) · [验证结果](验证结果.txt) · [文件校验](SHA256.json)
- 已自行下载脚本的用户：[安装脚本](Install-R1.3.3-patch.ps1) · [单独补丁 ZIP](FiberRouteR1-R1.3.3-patch.zip?raw=true)

安装程序会检查当前运行目录、桌面及下载文件夹，找不到时弹出文件夹选择框。应选择含“程序”“数据”的原运行目录；不是补丁解压目录。安装成功后从原来的“光路调度智能分析.exe”启动，标题显示 R1.3.3。

发现多个旧运行目录时，请明确指定要升级的目录。例如在 PowerShell 运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File .\Install-R1.3.3-patch.ps1 -Target "$env:USERPROFILE\Desktop\FiberRouteR1-R1.3\运行目录\光路调度智能分析R1.3" -OfflineZip .\FiberRouteR1-R1.3.3-patch.zip
```

补丁校验版本与 SHA256，替换前备份应用文件，写入失败回滚；保留 resources.db、personal.db、人工确认及原始表格。新装完整程序仍使用 [R1.3.2 完整交付](../R1.3.2/)，然后应用本补丁。

Excel 模板尚待用户确认；不应把模板当作软件已支持新导出的说明。具体芯号、内部接续可用芯数没有数据时不推断。本次未调整品牌图标。
