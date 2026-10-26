using FiberRoute.Core;
namespace FiberRoute.App;
internal static class Program
{
    static string LocationFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");
    public static void SaveRoot(string root){Directory.CreateDirectory(Path.GetDirectoryName(LocationFile)!);File.WriteAllText(LocationFile,root);}
    [STAThread] static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--wait-pid"&&int.TryParse(args[1],out var pid))try{using var process=System.Diagnostics.Process.GetProcessById(pid);if(!process.WaitForExit(45000))return;}catch(ArgumentException){}
        ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"光路调度智能分析",MessageBoxButtons.OK,MessageBoxIcon.Error);
        using var mutex=new Mutex(true,"Local\\FiberRouteR1.Desktop",out bool first);if(!first){MessageBox.Show("程序已在运行，请先关闭已有窗口。");return;}
        try{string root=File.Exists(LocationFile)?File.ReadAllText(LocationFile).Trim():args.Length==2&&args[0]=="--data"?Path.GetFullPath(args[1]):Path.GetDirectoryName(LocationFile)!;Application.Run(new MainForm(new Store(root)));}catch(Exception e){MessageBox.Show(e.Message,"启动失败");}
    }
}
