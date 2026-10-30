using FiberRoute.Core;
namespace FiberRoute.App;
internal static class Program
{
    static string LocationFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");
    public static void SaveRoot(string root){Directory.CreateDirectory(Path.GetDirectoryName(LocationFile)!);File.WriteAllText(LocationFile,root);}
    [STAThread] static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--wait-pid"&&int.TryParse(args[1],out var pid))try{using var process=System.Diagnostics.Process.GetProcessById(pid);if(!process.WaitForExit(45000))return;}catch(ArgumentException){}
        ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"光路调度路由查询",MessageBoxButtons.OK,MessageBoxIcon.Error);
        using var mutex=new Mutex(true,"Local\\FiberRouteR1.Desktop",out bool first);if(!first){MessageBox.Show("程序已在运行，请先关闭已有窗口。");return;}
        MainForm.CleanupUpdateHelpers();MainForm? main=null;Exception? failure=null;
        using(var splash=new Form{Text="光路调度路由查询 R"+MainForm.DisplayVersion,Width=520,Height=190,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ControlBox=false,BackColor=Color.White})
        {
            splash.Controls.Add(new ProgressBar{Dock=DockStyle.Bottom,Height=13,Style=ProgressBarStyle.Marquee,MarqueeAnimationSpeed=28});
            splash.Controls.Add(new Label{Text="正在载入资源与机房归属，请稍候…",Dock=DockStyle.Top,Height=42,TextAlign=ContentAlignment.MiddleCenter,Font=new Font("Microsoft YaHei UI",10)});
            splash.Controls.Add(new Label{Text="光路调度路由查询",Dock=DockStyle.Top,Height=75,TextAlign=ContentAlignment.BottomCenter,Font=new Font("Microsoft YaHei UI",18,FontStyle.Bold),ForeColor=Color.FromArgb(12,49,84)});
            splash.Shown+=async (_,_)=>{try{var store=await Task.Run(()=>LoadStore(args));main=new MainForm(store);}catch(Exception e){failure=e;}finally{splash.Close();}};
            Application.Run(splash);
        }
        if(failure!=null){MessageBox.Show(failure.Message,"启动失败");return;}
        if(main!=null)Application.Run(main);
    }
    static Store LoadStore(string[] args)
    {
        string root=File.Exists(LocationFile)?File.ReadAllText(LocationFile).Trim():args.Length==2&&args[0]=="--data"?Path.GetFullPath(args[1]):Path.GetDirectoryName(LocationFile)!;
        var store=new Store(root);string baseDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
        void Apply(string path,Action<string> work){if(!File.Exists(path))return;work(path);try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        Apply(Path.Combine(baseDir,"数据","机房归属基线.json"),p=>store.ApplyRoomBaseline(p));
        Apply(Path.Combine(baseDir,"数据","设施来源基线.db"),p=>store.ApplyEvidenceBaseline(p));
        Apply(Path.Combine(baseDir,"资源","机房设施ID基线.db"),p=>store.ApplyRoomFacilityBaseline(p));
        store.ApplyClusterRepairBaseline(Path.Combine(baseDir,"数据","集群站点修复基线.db"));
        store.ApplyClusterRepairBaseline(Path.Combine(baseDir,"资源","集群站点基线-R1.4.5.db"));return store;
    }
}
