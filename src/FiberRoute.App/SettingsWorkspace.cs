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
        var dialog=new Form{Text="设置",ClientSize=new Size(1000,840),MinimumSize=new Size(800,680),Font=Font,Icon=Icon,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Canvas,AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi};
        var hint=new ToolTip{AutoPopDelay=12000};dialog.FormClosed+=(_,_)=>hint.Dispose();
        Label TextLabel(string text,float size=10,FontStyle style=FontStyle.Regular)=>new(){Text=text,Font=new Font(Font.FontFamily,size,style),ForeColor=Theme.Ink,BackColor=Color.White,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft};
        var body=Stack();body.BackColor=Theme.Canvas;body.Padding=new Padding(14);body.AutoScroll=true;
        var heading=new Panel{Height=48,Margin=new Padding(0,0,0,6),BackColor=Theme.Canvas};var gear=new SettingsArtwork{Kind="settings",Width=36,Height=36,Location=new Point(0,3),BackColor=Theme.Canvas};heading.Controls.Add(gear);heading.Controls.Add(new Label{Text="设置",AutoSize=true,Font=new Font(Font.FontFamily,22,FontStyle.Bold),Location=new Point(48,0),ForeColor=Theme.Ink,BackColor=Theme.Canvas});body.Controls.Add(heading);
        var online=new Surface{Height=382,Padding=new Padding(20),Margin=new Padding(0,0,0,12)};var onlineBody=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1,BackColor=Color.White,Padding=new Padding(0,8,0,10)};onlineBody.ColumnStyles.Add(new(SizeType.Percent,50));onlineBody.ColumnStyles.Add(new(SizeType.Percent,50));onlineBody.RowStyles.Add(new(SizeType.Percent,100));
        var onlineTitle=TextLabel("在线更新",17,FontStyle.Bold);onlineTitle.Dock=DockStyle.Top;onlineTitle.Height=32;
        var banner=new Surface{Dock=DockStyle.Bottom,Height=42,SurfaceColor=Color.FromArgb(226,243,255),BackColor=Color.White,BorderColor=Color.FromArgb(226,243,255),Padding=new Padding(10,4,10,4)};
        var service=TextLabel("更新服务未配置，请联系管理方获取配置。",10);service.Dock=DockStyle.Fill;service.BackColor=Color.FromArgb(226,243,255);service.ForeColor=Color.FromArgb(0,102,192);service.AutoEllipsis=true;banner.Controls.Add(service);banner.Controls.Add(new SettingsArtwork{Kind="info",Dock=DockStyle.Left,Width=30,BackColor=Color.FromArgb(226,243,255)});
        online.Controls.Add(onlineBody);online.Controls.Add(onlineTitle);online.Controls.Add(banner);
        void UpdateCard(bool program,int column)
        {
            var card=new Surface{Dock=DockStyle.Fill,Padding=new Padding(18,14,18,10),Margin=new Padding(column==0?0:8,0,column==0?8:0,0)};
            string Current()=>program?"当前版本：R"+DisplayVersion:"数据版本："+(store.Setting("remoteResourceVersion") is string v&&v.Length>0?v:store.Graph.Version.Split(' ')[0]);
            var top=new TableLayoutPanel{Dock=DockStyle.Top,Height=98,ColumnCount=2,RowCount=1,BackColor=Color.White};top.ColumnStyles.Add(new(SizeType.Absolute,84));top.ColumnStyles.Add(new(SizeType.Percent,100));top.RowStyles.Add(new(SizeType.Percent,100));
            top.Controls.Add(new SettingsArtwork{Kind=program?"software":"database",Width=68,Height=68,Margin=new Padding(0,2,0,0),BackColor=Color.White},0,0);
            var info=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var title=TextLabel(program?"软件更新":"数据更新",15,FontStyle.Bold);title.Dock=DockStyle.Top;title.Height=30;
            var version=TextLabel(Current(),10);version.Dock=DockStyle.Top;version.Height=24;version.ForeColor=Theme.Muted;version.AutoEllipsis=true;
            var state=TextLabel("检查状态：尚未检查",9.5f);state.Dock=DockStyle.Fill;state.ForeColor=Theme.Muted;state.AutoEllipsis=true;info.Controls.Add(state);info.Controls.Add(version);info.Controls.Add(title);top.Controls.Add(info,1,0);
            var note=TextLabel(program?"发现新版后，可直接下载并安装\n保留现有数据库与人工记录":"查看变更后更新本地资源\n忽略无变化数据，保留人工确认",10);note.TextAlign=ContentAlignment.MiddleCenter;note.ForeColor=Theme.Muted;note.Dock=DockStyle.Bottom;note.Height=52;
            ReleaseItem? available=null;var check=new UiButton{Text=program?"检测软件更新":"检测数据更新",Primary=true,Height=46,Font=new Font(Font.FontFamily,12,FontStyle.Bold),Margin=Padding.Empty};
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
                        hint.SetToolTip(state,available.Notes);state.Text=$"可用版本：{available.Version}\n大小：{available.Bytes/1024d/1024d:0.##} MB\n{available.Notes}";service.Text="检测成功。软件与数据分别更新，按对应按钮继续。";check.Text=program?"下载并升级":"下载并预览更新";return;
                    }
                    var release=available;string? package=null;PreparedDataUpdate? prepared=null;string? helper=null;bool launched=false;
                    try
                    {
                        var progress=new Progress<DownloadProgress>(p=>state.Text=$"正在下载：{p.Received*100/Math.Max(1,p.Total)}%\n{p.Received/1024d/1024d:0.##} / {p.Total/1024d/1024d:0.##} MB");
                        bool ok=await Work("下载并校验更新…",async ct=>{package=await Updates.Download(config,release,Path.Combine(Path.GetTempPath(),"FiberRoute-downloads"),ct,progress);if(!program){state.Text="下载完成，正在分析新增、变更和人工冲突…";prepared=await Task.Run(()=>store.PrepareDataPackage(package,release.Version,ct),ct);}else{state.Text="下载完成，正在准备程序升级…";helper=await Task.Run(()=>PrepareUpdateHelper(ct),ct);}});
                        if(!ok){state.Text=status.Text;return;}
                        if(program)
                        {
                            string launcher=Path.Combine(ApplicationRoot,"光路调度路由查询.exe");if(!File.Exists(launcher))launcher=Application.ExecutablePath;
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

            var action=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};action.Controls.Add(check);void Place(){check.Width=Math.Max(180*action.DeviceDpi/96,action.ClientSize.Width-36*action.DeviceDpi/96);check.Location=new Point((action.Width-check.Width)/2,Math.Max(0,(action.Height-check.Height)/2));}action.SizeChanged+=(_,_)=>Place();Place();card.Controls.Add(action);card.Controls.Add(top);card.Controls.Add(note);onlineBody.Controls.Add(card,column,0);
        }
        UpdateCard(true,0);UpdateCard(false,1);try{if(Updates.LoadConfig(UpdateConfiguration)!=null)service.Text="已配置更新服务；软件与数据分别检测和更新。";}catch(Exception ex){service.Text="更新服务配置有误："+ex.Message;}body.Controls.Add(online);
        var directory=new Surface{Height=144,Padding=new Padding(20,12,20,12),Margin=new Padding(0,0,0,12)};var dirTitle=TextLabel("数据与目录",17,FontStyle.Bold);dirTitle.Dock=DockStyle.Top;dirTitle.Height=34;
        var row=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,BackColor=Color.White};row.ColumnStyles.Add(new(SizeType.AutoSize));row.ColumnStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Absolute,150));row.ColumnStyles.Add(new(SizeType.Absolute,150));row.RowStyles.Add(new(SizeType.Percent,100));var prefix=TextLabel("当前数据目录：",11);prefix.AutoSize=true;prefix.Anchor=AnchorStyles.Left;
        var path=InputFrame(new TextBox{Text=store.Root,ReadOnly=true,BackColor=Color.FromArgb(247,250,253)});path.Dock=DockStyle.Fill;path.Margin=new Padding(4,4,10,4);hint.SetToolTip(path,store.Root);
        var open=new UiButton{Text="打开数据目录",Primary=true,Dock=DockStyle.Fill,Margin=new Padding(0,4,8,4)};open.Click+=(_,_)=>Process.Start(new ProcessStartInfo(store.Root){UseShellExecute=true});var change=new UiButton{Text="更换数据目录",Dock=DockStyle.Fill,Margin=new Padding(0,4,0,4)};change.Click+=(_,_)=>ChangeDataDirectory();row.Controls.Add(prefix,0,0);row.Controls.Add(path,1,0);row.Controls.Add(open,2,0);row.Controls.Add(change,3,0);
        var dirNote=TextLabel("原始Excel仅在导入时读取，保留分析后的数据。",10);dirNote.Dock=DockStyle.Bottom;dirNote.Height=28;dirNote.ForeColor=Theme.Muted;directory.Controls.Add(row);directory.Controls.Add(dirTitle);directory.Controls.Add(dirNote);body.Controls.Add(directory);
        var layout=new Surface{Height=116,Padding=new Padding(20,12,20,12),Margin=new Padding(0,0,0,8)};var layoutTitle=TextLabel("界面与快捷方式",17,FontStyle.Bold);layoutTitle.Dock=DockStyle.Top;layoutTitle.Height=34;
        var layoutActions=new FlowLayoutPanel{Dock=DockStyle.Fill,FlowDirection=FlowDirection.LeftToRight,WrapContents=false,BackColor=Color.White,Padding=new Padding(0,4,0,0)};
        var shortcut=new UiButton{Text="创建桌面快捷方式",Glyph="desktop",Accent=true,Font=new Font(Font.FontFamily,11.5f,FontStyle.Bold),Width=230,Height=44,Margin=new Padding(0,0,14,0)};shortcut.Click+=(_,_)=>CreateDesktopShortcut();var restore=new UiButton{Text="恢复表格布局",Glyph="grid",Accent=true,Font=new Font(Font.FontFamily,11.5f,FontStyle.Bold),Width=230,Height=44,Margin=Padding.Empty};restore.Click+=(_,_)=>{foreach(var g in gridStates.Keys.ToArray())store.SetSetting(g.Name,"");foreach(var key in savedSplits.Keys)store.SetSetting("layout-"+key,"");gridStates.Clear();foreach(var g in new[]{segments,resourceGrid,typeGrid,clusterGrid,roomGrid})FitGrid(g);};layoutActions.Controls.AddRange([shortcut,restore]);layout.SizeChanged+=(_,_)=>layoutActions.Padding=new Padding(Math.Max(0,(layoutActions.ClientSize.Width-shortcut.Width-restore.Width-14)/2),4,0,0);layout.Controls.Add(layoutActions);layout.Controls.Add(layoutTitle);body.Controls.Add(layout);
        var bottom=new Panel{Height=60,BackColor=Theme.Canvas,Margin=Padding.Empty};var close=new UiButton{Text="关闭",Primary=true,Width=180,Height=46,Font=new Font(Font.FontFamily,12,FontStyle.Bold)};close.Click+=(_,_)=>{if(!busy)dialog.Close();};var stop=new UiButton{Text="停止更新",Width=110,Height=40,Visible=false};stop.Click+=(_,_)=>job?.Cancel();bottom.Controls.AddRange([close,stop]);bottom.SizeChanged+=(_,_)=>{close.Location=new Point((bottom.ClientSize.Width-close.Width)/2,6);stop.Location=new Point(8,9);};body.Controls.Add(bottom);
        var timer=new System.Windows.Forms.Timer{Interval=200};timer.Tick+=(_,_)=>{stop.Visible=busy;close.Enabled=!busy;};dialog.Shown+=(_,_)=>timer.Start();dialog.FormClosed+=(_,_)=>timer.Dispose();dialog.Controls.Add(body);dialog.FormClosing+=(_,e)=>{if(busy){job?.Cancel();e.Cancel=true;}};return dialog;
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
