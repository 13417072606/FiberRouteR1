using System.Diagnostics;
using System.Runtime.InteropServices;
static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int MessageBoxW(IntPtr owner,string text,string title,uint type);
    static void Main()
    {
        try{var p=Path.Combine(AppContext.BaseDirectory,"程序","FiberRouteR1.exe");if(!File.Exists(p))throw new FileNotFoundException("缺少程序文件，请完整解压ZIP后再启动。",p);var start=new ProcessStartInfo(p){WorkingDirectory=Path.GetDirectoryName(p)!,UseShellExecute=false};string data=Path.Combine(AppContext.BaseDirectory,"数据");if(File.Exists(Path.Combine(data,"resources.db"))){start.ArgumentList.Add("--data");start.ArgumentList.Add(data);}Process.Start(start);}
        catch(Exception e){MessageBoxW(IntPtr.Zero,e.Message,"光路调度智能分析 启动失败",0x10);}
    }
}
