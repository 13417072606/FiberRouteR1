using System.Diagnostics;
using System.Runtime.InteropServices;
static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)]static extern int MessageBoxW(IntPtr owner,string text,string title,uint type);
    static void Main()
    {
        try
        {
            string target=Path.Combine(AppContext.BaseDirectory,"程序","FiberRoute.Publisher.exe");
            if(!File.Exists(target))throw new FileNotFoundException("缺少更新工具程序文件，请完整解压后启动。",target);
            Process.Start(new ProcessStartInfo(target){WorkingDirectory=Path.GetDirectoryName(target)!,UseShellExecute=false});
        }
        catch(Exception e){MessageBoxW(IntPtr.Zero,e.Message,"光路调度更新工具 启动失败",0x10);}
    }
}
