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
        MainForm.CleanupUpdateHelpers();
        try{string root=File.Exists(LocationFile)?File.ReadAllText(LocationFile).Trim():args.Length==2&&args[0]=="--data"?Path.GetFullPath(args[1]):Path.GetDirectoryName(LocationFile)!;var store=new Store(root);string seed=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","数据","机房归属基线.json"));store.ApplyRoomBaseline(seed);try{File.Delete(seed);}catch(IOException){}catch(UnauthorizedAccessException){}string evidence=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","数据","设施来源基线.db"));if(File.Exists(evidence)){using var loading=new Form{Text="光路调度智能分析 R"+MainForm.DisplayVersion+" · 首次升级",Icon=File.Exists(BrandAssets.IconPath)?new Icon(BrandAssets.IconPath):File.Exists(Path.Combine(AppContext.BaseDirectory,"app.ico"))?new Icon(Path.Combine(AppContext.BaseDirectory,"app.ico")):null,Width=480,Height=150,StartPosition=FormStartPosition.CenterScreen};loading.Controls.Add(new Label{Text="正在整理区域与来源信息，完成后自动打开…",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleCenter});loading.Show();Application.DoEvents();store.ApplyEvidenceBaseline(evidence);try{File.Delete(evidence);}catch(IOException){}catch(UnauthorizedAccessException){}loading.Close();}Application.Run(new MainForm(store));}catch(Exception e){MessageBox.Show(e.Message,"启动失败");}
    }
}
