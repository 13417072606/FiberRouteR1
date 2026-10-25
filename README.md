# 纤路 R1.1 修正版下载

本分支包含1.1.0修正版源码及Windows x64自包含交付文件。原main与r1-distribution分支未覆盖。

**[下载完整Windows便携包（94,075,979字节）](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-revised-20261025/downloads/R1.1/FiberRouteR1-win-x64-R1.1.zip)**

完整解压后运行“纤路R1.exe”，无需安装.NET或编译。

- [分包1](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-revised-20261025/downloads/R1.1/FiberRouteR1-win-x64-R1.1.zip.part001)
- [分包2](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-revised-20261025/downloads/R1.1/FiberRouteR1-win-x64-R1.1.zip.part002)
- [分包3](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-revised-20261025/downloads/R1.1/FiberRouteR1-win-x64-R1.1.zip.part003)
- [分包4](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-revised-20261025/downloads/R1.1/FiberRouteR1-win-x64-R1.1.zip.part004)
- [合并分包.cmd](downloads/R1.1/合并分包.cmd)
- [SHA256校验清单](downloads/R1.1/SHA256SUMS.txt)
- [验证报告](downloads/R1.1/验证报告.md)

完整ZIP SHA256：`1011ea4aac733c1491630adf092305f62ebd0ab49a2eb8fce2ada010188aa173`。分包字节流合并已校验与完整包一致；前三份各30,000,000字节，第四份4,075,979字节。

**60项检查和Windows交叉构建通过，尚未在Windows桌面实际运行验收。**

# 纤路 R1.1 修正版

基于2026-10-25交接源码制作，版本1.1.0。C#/.NET 8 Windows Forms + SQLite，Windows x64自包含便携程序。

修复CSV默认UTF-8导致非UTF-8文件无法预览的问题；支持严格自动编码识别、XLS/XLSX原生流式读取、工作表选择、字段映射和后台导入；重排四页界面并增加真实节点连线示意图。人工类型和资源回退规则保留。

当前验证：核心测试通过，Windows交叉构建通过。Linux无法运行Windows Forms，尚未完成Windows桌面实际交互或截图验收；请先用附带调测数据试用。未配置线上更新源。

## 开发

在当前检出目录开发，不需要Git worktree。

```bash
bash scripts/install.sh
. scripts/dotnet-env.sh
dotnet run --project tests/FiberRoute.Tests -c Release
bash scripts/package.sh
```

脚本以自身目录定位项目；SDK 8.0.426，下载时校验SHA512，依赖版本锁定。生成产物为 artifacts/FiberRouteR1-win-x64-R1.1.zip。测试输入为tests/fixtures真实二进制XLS及XLSX；tests/generate_fixtures.py仅为开发时重新生成夹具用，不随成品运行。

完整解压后运行“纤路R1.exe”，不需要用户安装.NET、Python或编译。使用说明和验证边界见docs目录。
