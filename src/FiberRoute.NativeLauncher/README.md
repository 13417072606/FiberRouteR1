# Windows原生启动器

使用Win32 API启动“程序”中的自包含.NET主程序、管理方GUI或品牌工具。支持Unicode路径，使用同一个运行目录定位依赖，不需要系统安装.NET SDK；主启动器继续传入本地数据目录，客户自定义数据位置由原程序设置优先读取。

在Linux安装MinGW-w64 POSIX工具链及binutils后，运行：

```sh
FIBER_MINGW_ROOT=/usr bash scripts/build-native-launchers.sh
```

构建机也可使用解压到独立目录的官方Debian工具链，通过FIBER_MINGW_ROOT指定usr目录。该目录须包含bin/x86_64-w64-mingw32-gcc-posix、windres、Windows头文件和导入库。当前交付使用Debian GCC14/MinGW12构建。输出在artifacts/native-r141，不把编译器放入客户程序。

图标使用多尺寸ICO资源。用户原PNG通过品牌工具直接转换与写入，不通过重绘简化。版本发布包默认素材仍待原PNG预置；交付的品牌工具可在用户电脑选原文件完成替换。
