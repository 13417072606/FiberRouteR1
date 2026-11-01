using FiberRoute.Core;
namespace FiberRoute.App;
internal static class Program
{
    static string DefaultDataRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","数据"));
    static string LocationFile=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");
    public static void SaveRoot(string root){Directory.CreateDirectory(Path.GetDirectoryName(LocationFile)!);File.WriteAllText(LocationFile,root);}
    [STAThread] static void Main(string[] args)
    {
        if(args.Length==2&&args[0]=="--wait-pid"&&int.TryParse(args[1],out var pid))try{using var process=System.Diagnostics.Process.GetProcessById(pid);if(!process.WaitForExit(45000))return;}catch(ArgumentException){}
        ApplicationConfiguration.Initialize();Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);Application.ThreadException+=(_,e)=>MessageBox.Show(e.Exception.Message,"光路调度路由查询",MessageBoxButtons.OK,MessageBoxIcon.Error);
        using var mutex=new Mutex(true,"Local\\FiberRouteR1.Desktop",out bool first);if(!first){MessageBox.Show("程序已在运行，请先关闭已有窗口。");return;}
        MainForm.CleanupUpdateHelpers();MainForm? main=null;Exception? failure=null;var startup=System.Diagnostics.Stopwatch.StartNew();var phases=new List<string>();
        using(var splash=new Form{Text="光路调度路由查询 R"+MainForm.DisplayVersion,Width=520,Height=190,StartPosition=FormStartPosition.CenterScreen,FormBorderStyle=FormBorderStyle.FixedDialog,MaximizeBox=false,MinimizeBox=false,ControlBox=false,BackColor=Color.White})
        {
            var stage=new Label{Text="正在连接数据库…",Location=new Point(30,112),Size=new Size(440,26),Font=new Font("Microsoft YaHei UI",10),ForeColor=Color.FromArgb(82,101,125)};
            var title=new Label{Text="光路调度路由查询",Location=new Point(30,32),Size=new Size(275,42),Font=new Font("Microsoft YaHei UI",18,FontStyle.Bold),ForeColor=Color.FromArgb(12,49,84)};
            var version=new Label{Text="R"+MainForm.DisplayVersion,Location=new Point(32,77),AutoSize=true,Font=new Font("Microsoft YaHei UI",10),ForeColor=Color.FromArgb(82,101,125)};
            var route=new Panel{Location=new Point(335,42),Size=new Size(142,35),BackColor=Color.White};
            route.Paint+=(_,e)=>{using var line=new Pen(Color.FromArgb(8,147,158),2);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;e.Graphics.DrawLine(line,14,17,128,17);for(int i=0;i<3;i++){var x=14+i*57;using var fill=new SolidBrush(i==0?Color.FromArgb(8,147,158):Color.White);e.Graphics.FillEllipse(fill,x-6,11,12,12);e.Graphics.DrawEllipse(line,x-6,11,12,12);}};
            splash.Controls.Add(route);splash.Controls.Add(version);splash.Controls.Add(title);splash.Controls.Add(stage);
            splash.Shown+=async (_,_)=>{try{var store=await Task.Run(()=>LoadStore(args,phases));phases.Add($"资源与基线准备完成 {startup.ElapsedMilliseconds} ms");stage.Text="正在打开主界面…";main=new MainForm(store);phases.Add($"窗口创建完成 {startup.ElapsedMilliseconds} ms");}catch(Exception e){failure=e;}finally{try{Directory.CreateDirectory(Path.GetDirectoryName(LocationFile)!);File.WriteAllLines(Path.Combine(Path.GetDirectoryName(LocationFile)!,"startup-last.txt"),phases);}catch{}splash.Close();}};
            Application.Run(splash);
        }
        if(failure!=null){MessageBox.Show(failure.Message,"启动失败");return;}
        if(main!=null)Application.Run(main);
    }
    static Store LoadStore(string[] args,List<string> phases)
    {
        string root=File.Exists(LocationFile)?File.ReadAllText(LocationFile).Trim():args.Length==2&&args[0]=="--data"?Path.GetFullPath(args[1]):DefaultDataRoot;
        var watch=System.Diagnostics.Stopwatch.StartNew();var store=new Store(root);phases.Add($"数据库及资源图 {watch.ElapsedMilliseconds} ms");string baseDir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
        void Apply(string path,Action<string> work){if(!File.Exists(path))return;work(path);try{File.Delete(path);}catch(IOException){}catch(UnauthorizedAccessException){}}
        Apply(Path.Combine(baseDir,"数据","机房归属基线.json"),p=>store.ApplyRoomBaseline(p));
        Apply(Path.Combine(baseDir,"数据","设施来源基线.db"),p=>store.ApplyEvidenceBaseline(p));
        Apply(Path.Combine(baseDir,"资源","机房设施ID基线.db"),p=>store.ApplyRoomFacilityBaseline(p));
        store.ApplyClusterRepairBaseline(Path.Combine(baseDir,"数据","集群站点修复基线.db"));
        store.ApplyClusterRepairBaseline(Path.Combine(baseDir,"资源","集群站点基线-R1.4.5.db"));phases.Add($"基线整理完成 {watch.ElapsedMilliseconds} ms");return store;
    }
}
