# Windows界面检查

使用.NET 8 SDK构建，输出为自包含Windows x64程序：

```sh
dotnet publish tests/FiberRoute.UiChecks -c Release -r win-x64 --self-contained true -p:DebugType=None -p:DebugSymbols=false -o artifacts/ui-checks
```

在Windows或配置好中文字体、Wine及图形显示的兼容环境中运行输出EXE，首个参数为独立输出目录。测试使用独立数据库和PNG素材，不应指向客户运行目录；会生成界面截图和检查日志。100%与150%显示缩放分别运行。

新增测试覆盖：两端类型方向及多标签筛选、全局排序、单行筛选、完整页码、设置窗口DPI、品牌PNG原字节/比例/透明度及PE图标资源、图形发布向导的打包/签名/服务器目录发布。

参数--delivery-check和--publisher-delivery-check用于完整运行目录的原生启动器检查；--r141-real-ui可用隔离的全量数据副本生成实际资源及设置截图。升级文件事务及故障回退另外使用独立目录和tests/native/restart-marker.c验证重启，不调用业务程序修改数据库。
