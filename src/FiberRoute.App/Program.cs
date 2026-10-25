using FiberRoute.Core;
namespace FiberRoute.App;
internal static class Program
{
    [STAThread] static void Main()
    {
        ApplicationConfiguration.Initialize();
        Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
        Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"纤路 R1",MessageBoxButtons.OK,MessageBoxIcon.Error);
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1");
        Directory.CreateDirectory(root);
        using var mutex=new Mutex(true,"Local\\FiberRouteR1.Desktop",out bool first);
        if(!first){MessageBox.Show("纤路 R1 已在运行，请先关闭已有窗口。");return;}
        try {Application.Run(new MainForm(new Store(root)));}catch(Exception e){MessageBox.Show(e.Message,"启动失败");}
    }
}
