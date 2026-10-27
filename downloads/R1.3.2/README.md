# R1.3.2 交付

已有 R1.3 / R1.3.1 **推荐小补丁**，不用重下全量数据。关闭程序后运行安装脚本，会优先识别当前运行目录，再检查桌面和下载目录。

- [安装小补丁脚本](Install-R1.3.2-patch.ps1) · [补丁ZIP，约1MB](FiberRouteR1-R1.3.2-patch.zip)
- [完整程序和数据下载脚本](Download-R1.3.2.ps1)：首次安装使用，下载5个程序分卷及复用的4个数据分卷，断点续传、逐份/整包校验后合并解压。
- [源码及说明](FiberRouteR1-R1.3.2-source.zip)
- [完整程序ZIP](FiberRouteR1-win-x64-R1.3.2.zip)：不包含全量数据库；已有用户优先用小补丁。
- [12项修改清单](../../docs/R1.3.2修改清单.md) · [实际运行截图](../../docs/R1.3.2-preview/) · [验证记录](验证结果.txt) · [SHA256](SHA256.json)

脚本下载到“下载”文件夹后，可在PowerShell运行：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\Install-R1.3.2-patch.ps1"
```

需要手动指定桌面目录时：

```powershell
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\Install-R1.3.2-patch.ps1" -Target "$env:USERPROFILE\Desktop\FiberRouteR1-R1.3\运行目录\光路调度智能分析R1.3"
```

已下载补丁ZIP可加 `-OfflineZip "完整ZIP路径"`。使用完整包的脚本改为 `Download-R1.3.2.ps1`。分卷不单独解压，脚本负责拼接。

安装器已验证：R1.3 / R1.3.1升级、当前目录识别、重复安装、错误原版本拒绝、发生中途错误回滚、数据库字节保留。完整下载器已验证缓存分卷校验、整包拼接和程序/数据目录合并，重复运行保留已有运行目录。PowerShell脚本在PowerShell 7测试，使用Windows PowerShell 5.1兼容语法；Windows本机运行仍请核对实际体验。

**指定品牌PNG原文件仍待补充，当前包暂用既有近似图标。** 把原文件放入运行目录 `资源/品牌原图.png` 后，名称旁、标题栏和任务栏将等比采用原图；设置可创建采用当前图标的桌面快捷方式。EXE自身的嵌入图标取得原图后再编译替换。其余功能图标保持原设计。

小补丁保留现有resources.db、personal.db和人工维护；不会将原Excel/CSV存进程序。核心113项、100%/150%各27项交互通过，完整程序启动、全量208.21米和2044.87米机房样例验证记录见附件。
