# 纤路 R1 · 1.0.1 导入与布局修正版

**便携包下载：[R1 Windows x64 ZIP](https://github.com/13417072606/FiberRouteR1/raw/refs/heads/r1-distribution/downloads/FiberRouteR1-win-x64-R1.zip)**。完整解压后双击“纤路R1.exe”；校验值及验证边界见 [下载说明](downloads/README.md)。

C# / .NET 8 Windows Forms + SQLite 本地光路初筛工具。Windows x64自包含便携发布，四页中文界面：路由配置、中继段资源、节点类型、数据导入。仅操作此仓库。

成品构建输出：`artifacts/FiberRouteR1-win-x64-R1.zip`。完整解压后运行 `纤路R1.exe`；首次通过数据导入页载入附带调测数据或自己的 CSV / XLS / XLSX。个人数据独立存于 `%LOCALAPPDATA%\FiberRouteR1`。

当前状态：核心测试和Windows交叉编译通过，Windows桌面实机验收尚未完成。真实99 MB原始数据和HTTPS更新部署待验证。不要把构建通过视为所有产品验收完成。

- [使用说明](docs/使用说明.txt)
- [验证报告与性能边界](docs/验证报告.md)
- [更新发布协议](docs/更新发布.md)

## 开发和构建

可在任意独立检出目录工作；SDK、依赖缓存和构建输出均位于当前仓库，不依赖 VisioToCAD 目录。执行 `bash scripts/install.sh` 安装/恢复并验证。

```bash
. scripts/dotnet-env.sh
dotnet run --project tests/FiberRoute.Tests -c Release
# 29万行合成数据测试
dotnet run --project tests/FiberRoute.Tests -c Release -- --benchmark
# 自包含Windows x64发布 + ZIP / PE / 运行依赖检查
bash scripts/package.sh
```

Windows本地开发：安装.NET 8 SDK，打开 `FiberRouteR1.sln`，将FiberRoute.App设为启动项目。测试与核心项目可跨平台运行，GUI需Windows。

路由按目标指标做有界优先队列搜索，完整保存路径防止节点循环；触限明确报告完整性/最优性边界。距离为已记录值之和，缺失与零值分开显示。CSV后台逐行导入、每1000行事务提交到临时库，校验及索引加载成功后原子切换；人工类型和偏好分库存储。

更新不预置服务，使用同源HTTPS与RSA-PSS签名清单、SHA256包校验。需管理员落实真实部署、访问控制及Windows更新流程验收。

## 1.0.1 修正

CSV 自动识别 UTF-8 / GB18030 / UTF-16 等编码和常见分隔符；新增 XLS / XLSX 流式读取与工作表选择。预览和正式导入共用读取规则，切换文件、编码或工作表后须重新预览。顶部导航和统计卡片、左侧路由条件、右侧候选与路径图；类型导入预览独立位于节点类型页。所有操作保持本地处理。
