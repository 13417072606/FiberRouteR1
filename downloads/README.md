# 纤路 R1 1.0.1 Windows x64 自包含修正版

[下载 FiberRouteR1-win-x64-R1.zip](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-distribution/downloads/FiberRouteR1-win-x64-R1.zip)

完整解压后双击“纤路R1.exe”。包含 .NET 运行组件，无需另装 .NET。首次进入“数据导入”，载入附带调测数据即可试用。

大小：94,082,462 字节（约89.72 MiB）。SHA256：

```text
863799a77d7375c34f678d13012c6d47051eda065fce25de867fd0bbe524e16f
```

已通过65项核心检查及400次小图排序穷举对照。Windows实机界面、真实99 MB原始数据及真实在线更新部署尚未验证，详情参见根目录 docs/验证报告.md。此为可试运行包，不代表完整生产验收。

本分支暂用于仓库分发；后续成品优先使用 GitHub Releases，避免持续将二进制写入 Git 历史。如果浏览器显示需要登录，请使用有该仓库权限的GitHub账号登录后下载。

本次包含 CSV 自动编码/分隔符、XLS/XLSX 工作表选择、新导航和统计卡片、左右路由布局、路径图及独立类型预览。约30 MB分包可执行 `python3 scripts/split-delivery.py` 生成，合并工具会检查每份及完整ZIP的 SHA256。
