# R1.3.1 小补丁

针对已下载、完整解压的 R1.3，约 1 MB，保留现有中继段数据库、人工维护和导出结果。无需重下运行库或全量数据。补丁安装前请关闭软件。

- [查看程序实际运行截图](../../docs/R1.3.1-preview/README.md)
- [修正内容与验证边界](../../docs/R1.3.1修正说明.md)
- [补丁文件](FiberRouteR1-R1.3.1-patch.zip)
- [下载、校验及安装脚本](Install-R1.3.1-patch.ps1)
- [SHA256](SHA256.json)

脚本默认定位本次聊天的原 R1.3 路径：`下载/FiberRouteR1-R1.3/运行目录/光路调度智能分析R1.3`。若已移动文件夹，运行下载后的脚本并指定路径：

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File "$env:USERPROFILE\Downloads\Install-R1.3.1-patch.ps1" -Target 'D:\光路调度智能分析R1.3'
```

安装完成仍从原目录的“光路调度智能分析.exe”打开，标题应为 R1.3.1。首次启动将派生归属写入结构化库后清理临时 JSON；不会删除用户原始表格。程序补丁备份只有一份，可在确认正常后自行删除。若校验失败则不要手动跳过校验。

本补丁需要原 R1.3 的其他程序和运行库文件，不能单独解压成独立软件。
