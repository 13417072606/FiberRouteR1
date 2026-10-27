using FiberRoute.Core;
using System.Diagnostics;

namespace FiberRoute.App;
public sealed partial class MainForm
{
    internal static string ProgramVersion=>typeof(MainForm).Assembly.GetName().Version!.ToString(3);
    internal static string DisplayVersion=>ProgramVersion.EndsWith(".0",StringComparison.Ordinal)?ProgramVersion[..^2]:ProgramVersion;
    static string ApplicationRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
    internal static string UpdateConfiguration=>Path.Combine(ApplicationRoot,"资源","update-service.json");
    Form BuildSettingsDialog()
    {
        var dialog=new Form{Text="设置",Width=900,Height=810,MinimumSize=new Size(760,650),Font=Font,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Canvas,AutoScaleMode=AutoScaleMode.Dpi};
        var body=Stack();body.BackColor=Theme.Canvas;body.Padding=new Padding(14);body.Controls.Add(Title("设置",20));
        var online=new Surface{Height=350,Padding=new Padding(14)};var onlineBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.White};onlineBody.ColumnStyles.Add(new(SizeType.Percent,50));onlineBody.ColumnStyles.Add(new(SizeType.Percent,50));onlineBody.RowStyles.Add(new(SizeType.Percent,100));online.Controls.Add(onlineBody);online.Controls.Add(new Label{Text="在线更新",Dock=DockStyle.Top,Height=36,Font=new Font(Font.FontFamily,14,FontStyle.Bold),ForeColor=Theme.Ink});
        var service=new Label{Dock=DockStyle.Bottom,Height=36,ForeColor=Theme.Muted,Text="更新服务未配置，请联系管理方获取配置。",BackColor=Theme.Tint,Padding=new Padding(8)};online.Controls.Add(service);
        void UpdateCard(bool program,int column)
        {
            var card=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12),Margin=new Padding(column==0?0:8,0,column==0?8:0,0)};
            string Current()=>program?"当前版本：R"+DisplayVersion:"数据版本："+(store.Setting("remoteResourceVersion") is string v&&v.Length>0?v:store.Graph.Version);
            var version=new Label{Text=Current(),Dock=DockStyle.Top,Height=34,ForeColor=Theme.Muted,AutoEllipsis=true};var state=new Label{Text="尚未检查",Height=64,Dock=DockStyle.Top,ForeColor=Theme.Ink,Padding=new Padding(0,8,0,4)};
            var note=new Label{Text=program?"发现新版后，可直接下载并安装\n保留现有数据库与人工记录":"预览差异后更新本地资源\n忽略无变化，保留人工确认",Dock=DockStyle.Bottom,Height=38,ForeColor=Theme.Muted};
            ReleaseItem? available=null;var check=AsyncButton(program?"检测软件更新":"检测数据更新",async()=>{await Task.CompletedTask;},primary:true);check.Dock=DockStyle.Bottom;check.Height=42;
            check.Click+=async(_,_)=>
            {
                if(busy)return;check.Enabled=false;
                try
                {
                    var config=Updates.LoadConfig(UpdateConfiguration);if(config==null){state.Text="更新服务未配置";service.Text="管理方配置国内更新服务后，即可在这里直接在线更新。";return;}
                    if(available==null)
                    {
                        ReleaseManifest? manifest=null;state.Text="正在检测…";bool ok=await Work("正在检测"+(program?"软件":"数据")+"更新…",async ct=>manifest=await Updates.Check(config,ct));if(!ok){state.Text=status.Text;return;}
                        available=program?manifest?.Program:manifest?.Resources;
                        if(available==null){state.Text="当前渠道暂无"+(program?"软件":"数据")+"更新";return;}
                        if(program&&!Updates.IsNewProgram(available.Version,ProgramVersion)||!program&&available.Version==store.Setting("remoteResourceVersion")){state.Text="已是最新版本";available=null;return;}
                        if(!Updates.Compatible(available,ProgramVersion)){state.Text="请先升级软件至 R"+available.MinimumProgram;available=null;return;}
                        state.Text=$"可用版本：{available.Version}\n大小：{available.Bytes/1024d/1024d:0.##} MB\n{available.Notes}";service.Text="检测成功。软件与数据分别更新，按对应按钮继续。";check.Text=program?"下载并升级":"下载并预览更新";return;
                    }
                    var release=available;string? package=null;PreparedDataUpdate? prepared=null;string? helper=null;bool launched=false;
                    try
                    {
                        var progress=new Progress<DownloadProgress>(p=>state.Text=$"正在下载：{p.Received*100/Math.Max(1,p.Total)}%\n{p.Received/1024d/1024d:0.##} / {p.Total/1024d/1024d:0.##} MB");
                        bool ok=await Work("下载并校验更新…",async ct=>{package=await Updates.Download(config,release,Path.Combine(Path.GetTempPath(),"FiberRoute-downloads"),ct,progress);if(!program){state.Text="下载完成，正在分析新增、变更和人工冲突…";prepared=await Task.Run(()=>store.PrepareDataPackage(package,release.Version,ct),ct);}else{state.Text="下载完成，正在准备程序升级…";helper=await Task.Run(()=>PrepareUpdateHelper(ct),ct);}});
                        if(!ok){state.Text=status.Text;return;}
                        if(program)
                        {
                            string launcher=Path.Combine(ApplicationRoot,"光路调度智能分析.exe");if(!File.Exists(launcher))launcher=Application.ExecutablePath;
                            var start=new ProcessStartInfo(Path.Combine(helper!,"FiberRoute.Updater.exe")){UseShellExecute=false,WorkingDirectory=helper!};foreach(string arg in new[]{Environment.ProcessId.ToString(),package!,ApplicationRoot,launcher,release.Sha256})start.ArgumentList.Add(arg);Process.Start(start);launched=true;package=null;dialog.Close();BeginInvoke((Action)Close);return;
                        }
                        if(prepared==null)throw new InvalidDataException("没有可应用的数据预览");
                        if(ConfirmDataUpdate(dialog,prepared))
                        {
                            bool applied=await Work("应用数据更新，保留人工记录…",ct=>Task.Run(()=>store.ApplyDataPackage(prepared,ct),ct));if(applied){RefreshData();version.Text=Current();state.Text=prepared.HasChanges?"数据更新完成；人工记录保留":"数据无变化，已忽略重复更新";available=null;check.Text="检测数据更新";}
                        }
                    }
                    finally{prepared?.Dispose();if(package!=null&&File.Exists(package))File.Delete(package);if(helper!=null&&!launched)try{Directory.Delete(helper,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
                }
                catch(Exception ex){state.Text="更新未完成："+ex.Message;}
                finally{if(!check.IsDisposed)check.Enabled=true;}
            };
            card.Controls.Add(state);card.Controls.Add(version);card.Controls.Add(new Label{Text=program?"软件更新":"数据更新",Dock=DockStyle.Top,Height=28,Font=new Font(Font.FontFamily,13,FontStyle.Bold)});card.Controls.Add(note);card.Controls.Add(check);onlineBody.Controls.Add(card,column,0);
        }
        UpdateCard(true,0);UpdateCard(false,1);try{if(Updates.LoadConfig(UpdateConfiguration)!=null)service.Text="已配置更新服务；软件与数据分别检测和更新。";}catch(Exception ex){service.Text="更新服务配置有误："+ex.Message;}body.Controls.Add(online);
        var directory=new Surface{Height=180,Padding=new Padding(12)};var row=Flow();row.Dock=DockStyle.Bottom;row.WrapContents=false;row.BackColor=Color.White;row.Controls.AddRange([B("打开数据目录",()=>Process.Start(new ProcessStartInfo(store.Root){UseShellExecute=true})),B("更换数据目录",ChangeDataDirectory)]);directory.Controls.Add(row);directory.Controls.Add(new Label{Text="原始Excel仅在导入时读取，保留分析后的数据。",Dock=DockStyle.Bottom,Height=30,ForeColor=Theme.Muted});directory.Controls.Add(new Label{Text="当前数据目录："+store.Root,Dock=DockStyle.Top,Height=42,Padding=new Padding(0,10,0,0),AutoEllipsis=true});directory.Controls.Add(new Label{Text="数据与目录",Dock=DockStyle.Top,Height=32,Font=new Font(Font.FontFamily,13,FontStyle.Bold)});body.Controls.Add(directory);
        var layout=new Surface{Height=110,Padding=new Padding(12)};var layoutActions=Flow();layoutActions.Dock=DockStyle.Fill;layoutActions.BackColor=Color.White;layoutActions.Controls.AddRange([B("创建桌面快捷方式",CreateDesktopShortcut),B("恢复表格布局",()=>{foreach(var g in gridStates.Keys.ToArray())store.SetSetting(g.Name,"");foreach(var key in savedSplits.Keys)store.SetSetting("layout-"+key,"");gridStates.Clear();foreach(var g in new[]{segments,resourceGrid,typeGrid,clusterGrid,roomGrid})FitGrid(g);})]);layout.Controls.Add(layoutActions);layout.Controls.Add(new Label{Text="界面与快捷方式",Dock=DockStyle.Top,Height=32,Font=new Font(Font.FontFamily,13,FontStyle.Bold)});body.Controls.Add(layout);
        var bottom=Flow();bottom.BackColor=Theme.Canvas;bottom.Controls.AddRange([B("停止更新",()=>job?.Cancel()),B("关闭",()=>{if(!busy)dialog.Close();})]);body.Controls.Add(bottom);dialog.Controls.Add(body);dialog.FormClosing+=(_,e)=>{if(busy){job?.Cancel();e.Cancel=true;}};return dialog;
    }
    void ChangeDataDirectory()
    {
        if(busy)return;using var pick=new FolderBrowserDialog{Description="选择包含resources.db和personal.db的数据文件夹，或新的空文件夹"};if(pick.ShowDialog()!=DialogResult.OK)return;string root=Path.GetFullPath(pick.SelectedPath);_=new Store(root);Program.SaveRoot(root);var restart=new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=false};restart.ArgumentList.Add("--wait-pid");restart.ArgumentList.Add(Environment.ProcessId.ToString());Process.Start(restart);Application.Exit();
    }
    bool ConfirmDataUpdate(Form owner,PreparedDataUpdate update)
    {
        using var dialog=new Form{Text="数据更新预览 · "+update.Manifest.Version,Width=1000,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var grid=Grid();grid.DataSource=update.Details.Take(1000).ToList();grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;grid.AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.AllCells;dialog.Controls.Add(grid);
        dialog.Controls.Add(new Label{Text=$"新增 {update.Added:N0} · 变更 {update.Changed:N0} · 删除 {update.Removed:N0} · 无变化 {update.Unchanged:N0}\n元数据变更 {update.MetadataChanged:N0} · 人工冲突 {update.ManualConflicts:N0}（保留人工值并标需核对） · 预览明细最多1000项",Dock=DockStyle.Top,Height=64,Padding=new Padding(10),BackColor=Theme.Tint});var actions=Flow();actions.Dock=DockStyle.Bottom;actions.Controls.AddRange([B("确认更新",()=>dialog.DialogResult=DialogResult.OK),B("取消",()=>dialog.DialogResult=DialogResult.Cancel)]);dialog.Controls.Add(actions);return dialog.ShowDialog(owner)==DialogResult.OK;
    }
    internal static void CleanupUpdateHelpers()
    {
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","update-helpers");if(!Directory.Exists(root))return;
        foreach(string old in Directory.GetDirectories(root,"helper-*"))try{string marker=Path.Combine(old,".active.pid");if(File.Exists(marker)&&int.TryParse(File.ReadAllText(marker),out int pid)){try{using var process=Process.GetProcessById(pid);if(!process.HasExited)continue;}catch(ArgumentException){}}Directory.Delete(old,true);}catch(IOException){}catch(UnauthorizedAccessException){}
    }
    static string PrepareUpdateHelper(CancellationToken ct)
    {
        string updater=Path.Combine(AppContext.BaseDirectory,"FiberRoute.Updater.exe");if(!File.Exists(updater))throw new FileNotFoundException("缺少在线升级组件，请使用完整R1.4补丁安装。",updater);
        string root=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","update-helpers");Directory.CreateDirectory(root);CleanupUpdateHelpers();
        string directory=Path.Combine(root,"helper-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(directory);try{foreach(string source in Directory.GetFiles(AppContext.BaseDirectory,"*",SearchOption.AllDirectories)){ct.ThrowIfCancellationRequested();string destination=Path.Combine(directory,Path.GetRelativePath(AppContext.BaseDirectory,source));Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(source,destination);}return directory;}catch{Directory.Delete(directory,true);throw;}
    }
}
