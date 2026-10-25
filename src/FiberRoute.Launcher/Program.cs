using System.Diagnostics;
using System.Runtime.InteropServices;
static class Program
{
    [DllImport("user32.dll",CharSet=CharSet.Unicode)] static extern int MessageBoxW(IntPtr owner,string text,string title,uint type);
    static void Main()
    {
        try{var p=Path.Combine(AppContext.BaseDirectory,"程序","FiberRouteR1.exe");if(!File.Exists(p))throw new FileNotFoundException("缺少程序文件，请完整解压ZIP后再启动。",p);Process.Start(new ProcessStartInfo(p){WorkingDirectory=Path.GetDirectoryName(p)!,UseShellExecute=false});}
        catch(Exception e){MessageBoxW(IntPtr.Zero,e.Message,"纤路 R1 启动失败",0x10);}
    }
}
