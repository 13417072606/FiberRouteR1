using FiberRoute.App;
using FiberRoute.Core;
using System.Diagnostics;
using System.Reflection;
using System.Text.Json;
namespace FiberRoute.Publisher;
public sealed class PublisherForm:Form
{
    readonly TextBox work=new(),url=new(),destination=new(),source=new(),version=new(),package=new(),notes=new(),log=new(){Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Vertical};
    readonly ComboBox channel=new(){DropDownStyle=ComboBoxStyle.DropDownList};readonly Label state=new(){Dock=DockStyle.Bottom,Height=32,TextAlign=ContentAlignment.MiddleLeft};
    readonly string preferenceFile;
    sealed record Preferences(string Workspace,string Url,string Destination,string Source,bool SelfHosted=false);
    readonly TabControl tabs=new(){Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,ItemSize=new Size(0,1),SizeMode=TabSizeMode.Fixed};readonly List<Button> actions=[];readonly UiButton stop=new(){Text="停止当前任务",Primary=false,Width=180,Height=44,Enabled=false};readonly RadioButton selfHosted=new(){Text="自有服务器",AutoSize=true};CancellationTokenSource? job;
    static string AppRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
    public PublisherForm(string? preferences=null)
    {
        preferenceFile=preferences??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","publisher-preferences.json");
        Text="光路调度更新工具 R1.5.5";ClientSize=new Size(1080,800);MinimumSize=new Size(900,720);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Canvas;ForeColor=Theme.Ink;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        var brand=BrandAssets.Load();Icon=brand.Icon??(File.Exists(Path.Combine(AppContext.BaseDirectory,"app.ico"))?new Icon(Path.Combine(AppContext.BaseDirectory,"app.ico")):null);brand.Image?.Dispose();
        work.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"纤路发布工作区");source.Text=Path.Combine(AppRoot,"数据");string location=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");if(File.Exists(location))source.Text=File.ReadAllText(location).Trim();version.Text=DateTime.Now.ToString("yyyy-MM-dd")+".1";try{if(File.Exists(preferenceFile)){var saved=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(preferenceFile));if(saved!=null){work.Text=saved.Workspace;url.Text=saved.Url;destination.Text=saved.Destination;source.Text=saved.Source;selfHosted.Checked=saved.SelfHosted;}}}catch{}
        var heading=new Panel{Dock=DockStyle.Top,Height=86,Padding=new Padding(20,12,20,12),BackColor=Color.White};heading.Controls.Add(new Label{Text="光路调度更新工具",Font=new Font(Font.FontFamily,21,FontStyle.Bold),Dock=DockStyle.Top,Height=36});heading.Controls.Add(new Label{Text="R1.5.5 · 数据与软件在线发布",Dock=DockStyle.Bottom,Height=22,ForeColor=Theme.Muted});
        var navigation=new FlowLayoutPanel{Dock=DockStyle.Left,Width=236,Padding=new Padding(14,20,14,8),BackColor=Color.White,WrapContents=false,FlowDirection=FlowDirection.TopDown};
        var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16),BackColor=Theme.Canvas};body.Controls.Add(tabs);Controls.Add(body);Controls.Add(navigation);Controls.Add(heading);state.Padding=new Padding(16,0,0,0);Controls.Add(state);
        string[] steps=["服务设置","1 生成更新包","2 生成发布清单","3 上传与验证"];
        for(int i=0;i<steps.Length;i++){int index=i;var tab=new TabPage{BackColor=Theme.Canvas,Padding=new Padding(0)};tabs.TabPages.Add(tab);var button=new UiButton{Text=steps[i],Width=200,Height=52,Selected=i==0,Margin=new Padding(0,0,0,12)};button.Click+=(_,_)=>tabs.SelectedIndex=index;navigation.Controls.Add(button);tabs.SelectedIndexChanged+=(_,_)=>{button.Selected=tabs.SelectedIndex==index;button.Invalidate();};}
        BuildService();BuildPackage();BuildManifest();BuildPublish();state.Text="先核对服务设置，再生成本次更新包。";LoadConfiguration();FormClosing+=(_,e)=>{if(job!=null){job.Cancel();e.Cancel=true;}else SavePreferences();};
    }
    Panel Card(TabPage tab,string title,string description)
    {
        var card=new Surface{Dock=DockStyle.Fill,Padding=new Padding(22)};var top=new Panel{Dock=DockStyle.Top,Height=86,BackColor=Color.White};top.Controls.Add(new Label{Text=description,Dock=DockStyle.Bottom,Height=40,ForeColor=Theme.Muted});top.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=36,Font=new Font(Font.FontFamily,17,FontStyle.Bold)});var content=new Panel{Dock=DockStyle.Fill,BackColor=Color.White,AutoScroll=true};card.Controls.Add(content);card.Controls.Add(top);tab.Controls.Add(card);return content;
    }
    TableLayoutPanel Fields(Panel parent)
    {
        var table=new TableLayoutPanel{Dock=DockStyle.Top,ColumnCount=3,AutoSize=true,BackColor=Color.White,Padding=new Padding(0,4,0,8)};table.ColumnStyles.Add(new(SizeType.Absolute,145));table.ColumnStyles.Add(new(SizeType.Percent,100));table.ColumnStyles.Add(new(SizeType.Absolute,110));parent.Controls.Add(table);return table;
    }
    void Field(TableLayoutPanel table,string name,Control input,Action? browse=null,string button="选择…")
    {
        int row=table.RowCount++;table.RowStyles.Add(new(SizeType.Absolute,54));var label=new Label{Text=name,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft};table.Controls.Add(label,0,row);input.Dock=DockStyle.Fill;input.Margin=new Padding(0,12,10,10);table.Controls.Add(input,1,row);if(browse!=null){var pick=new UiButton{Text=button,Dock=DockStyle.Fill,Margin=new Padding(0,6,0,6)};pick.Click+=(_,_)=>browse();table.Controls.Add(pick,2,row);actions.Add(pick);}
    }
    Button ActionButton(string text,Func<CancellationToken,Task> action,bool primary=true)
    {var b=new UiButton{Text=text,Primary=primary,Width=210,Height=44,Margin=new Padding(0,0,12,0)};b.Click+=async(_,_)=>await Run(action);actions.Add(b);return b;}
    FlowLayoutPanel ActionRow(Panel parent,params Control[] controls){var row=new FlowLayoutPanel{Dock=DockStyle.Top,Height=58,BackColor=Color.White,WrapContents=false,Padding=new Padding(0,6,0,0)};row.Controls.AddRange(controls);parent.Controls.Add(row);return row;}
    void Folder(TextBox field){using var pick=new FolderBrowserDialog{Description="选择目录",SelectedPath=Directory.Exists(field.Text)?field.Text:""};if(pick.ShowDialog(this)==DialogResult.OK)field.Text=pick.SelectedPath;}
    void Message(Panel parent,string text,int height=70){var label=new Label{Text=text,Dock=DockStyle.Top,Height=height,ForeColor=Theme.Muted,BackColor=Color.White,Padding=new Padding(0,8,0,0)};parent.Controls.Add(label);}
    void OpenFolder(string path){Directory.CreateDirectory(path);Process.Start(new ProcessStartInfo(path){UseShellExecute=true});}
    void BuildPackage()
    {
        var page=Card(tabs.TabPages[1],"生成更新包","选择已保存的数据目录或新版本程序目录。");
        var bottom=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};page.Controls.Add(bottom);Message(bottom,"本工具不会把原始Excel、个人设置或原始personal.db直接打进客户更新包。\n数据无变化会在客户更新时忽略；人工冲突保留并提示复核。",80);
        ActionRow(page,ActionButton("生成更新包",async ct=>
        {
            string input=Path.GetFullPath(source.Text),release=version.Text.Trim(),kind=channel.SelectedIndex==0?"data":"software";if(!System.Text.RegularExpressions.Regex.IsMatch(release,@"^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$"))throw new InvalidDataException("版本号请使用数字、字母、点、短横线或下划线");if(kind=="data"&&(!File.Exists(Path.Combine(input,"resources.db"))||!File.Exists(Path.Combine(input,"personal.db"))))throw new InvalidDataException("请选择包含resources.db和personal.db的数据目录，可在主程序设置中打开数据目录定位");string folder=Path.Combine(Path.GetFullPath(work.Text),"packages");Directory.CreateDirectory(folder);string file=Path.Combine(folder,(kind=="data"?"数据-":"软件-")+release+".zip");if(File.Exists(file))throw new IOException("同名更新包已存在，请填写新的版本号，或先核对已有文件。");
            state.Text="正在生成更新包…";await Task.Run(()=>{ct.ThrowIfCancellationRequested();if(kind=="data"){var store=new Store(input);store.ApplyClusterRepairBaseline(Path.Combine(AppRoot,"数据","集群站点修复基线.db"),ct);store.BuildDataPackage(file,release,ct);}else Publishing.BuildProgramPackage(input,file,release);ct.ThrowIfCancellationRequested();},ct);package.Text=file;state.Text="更新包已生成："+file;tabs.SelectedIndex=2;
        }),ActionButton("打开工作目录",ct=>{OpenFolder(work.Text);return Task.CompletedTask;},false));
        var fields=Fields(page);channel.Items.AddRange(["数据更新","软件更新"]);channel.SelectedIndex=0;Field(fields,"更新类型",channel);Field(fields,"资源来源目录",source,()=>{Folder(source);if(channel.SelectedIndex==1){string dll=Path.Combine(source.Text,"程序","FiberRouteR1.dll");if(File.Exists(dll))version.Text=AssemblyName.GetAssemblyName(dll).Version!.ToString(3);}});Field(fields,"版本号",version);notes.Multiline=true;notes.Height=45;Field(fields,"更新说明",notes);channel.SelectedIndexChanged+=(_,_)=>{version.Text=channel.SelectedIndex==0?DateTime.Now.ToString("yyyy-MM-dd")+".1":"1.5.5";};
    }
    void BuildService()
    {
        var page=Card(tabs.TabPages[0],"服务设置","首次启用或更换发布地址时修改。客户端配置仅需交付一次。");
        var clientActions=new Panel{Dock=DockStyle.Top,Height=78,Visible=false,BackColor=Color.White};
        var clientRow=new FlowLayoutPanel{Dock=DockStyle.Fill,WrapContents=false,BackColor=Color.White,Padding=new Padding(0,8,0,0)};clientActions.Controls.Add(clientRow);
        clientRow.Controls.Add(ActionButton("应用到本机",ct=>{string config=ClientConfig();string target=AppRoot;if(!ValidClient(target)){using var pick=new FolderBrowserDialog{Description="选择客户端根目录（含主程序及程序文件夹）"};if(pick.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;target=pick.SelectedPath;if(!ValidClient(target))throw new InvalidDataException("所选目录不是客户端根目录");}Directory.CreateDirectory(Path.Combine(target,"资源"));File.Copy(config,Path.Combine(target,"资源","update-service.json"),true);state.Text="本机客户端已配置在线更新。";return Task.CompletedTask;},false));
        clientRow.Controls.Add(ActionButton("生成客户启用包",ct=>{using var pick=new SaveFileDialog{Title="保存客户启用包",Filter="ZIP 压缩包|*.zip",FileName="客户在线更新启用包.zip",OverwritePrompt=false};if(pick.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;string output=PublishingDelivery.BuildClientActivationPackage(work.Text,pick.FileName);state.Text="启用包已生成："+output;return Task.CompletedTask;},false));
        clientRow.Controls.Add(ActionButton("导出配置文件",ct=>{string config=ClientConfig();using var pick=new FolderBrowserDialog{Description="选择保存公开配置的目录"};if(pick.ShowDialog(this)==DialogResult.OK){File.Copy(config,Path.Combine(pick.SelectedPath,"update-service.json"),true);state.Text="公开配置已导出。请放入客户端根目录的资源文件夹。";}return Task.CompletedTask;},false));
        page.Controls.Add(clientActions);
        var clientToggle=new UiButton{Text="客户端首次启用 · 查看方式",Dock=DockStyle.Top,Height=45,Margin=new Padding(0,12,0,0)};clientToggle.Click+=(_,_)=>{clientActions.Visible=!clientActions.Visible;clientToggle.Text=clientActions.Visible?"客户端首次启用 · 收起":"客户端首次启用 · 查看方式";};page.Controls.Add(clientToggle);
        ActionRow(page,ActionButton("保存服务配置",async ct=>{string directory=work.Text,address=url.Text.Trim();if(!Uri.TryCreate(address,UriKind.Absolute,out var service)||service.Scheme!="https")throw new InvalidDataException("请填写 HTTPS 更新地址");state.Text="正在保存服务配置…";await Task.Run(()=>Publishing.SaveConfiguration(directory,address),ct);var active=Updates.LoadConfig(Path.Combine(work.Text,"client-config","update-service.json"));var delivered=Updates.LoadConfig(Path.Combine(AppRoot,"资源","update-service.json"));state.Text=delivered!=null&&active?.PublicKeyPem!=delivered.PublicKeyPem?"服务已保存；签名公钥与交付客户端不同，请为客户端重新安装公开配置。":"服务配置已保存。";}));
        var serverFields=new Panel{Dock=DockStyle.Top,Height=58,Visible=selfHosted.Checked,BackColor=Color.White};var serverTable=Fields(serverFields);Field(serverTable,"服务器发布目录",destination,()=>Folder(destination));page.Controls.Add(serverFields);
        var fields=Fields(page);Field(fields,"管理工作目录",work,()=>{Folder(work);LoadConfiguration();});url.PlaceholderText="https://更新站点/";Field(fields,"客户 HTTPS 地址",url);
        var mode=new FlowLayoutPanel{Dock=DockStyle.Top,Height=52,WrapContents=false,BackColor=Color.White};mode.Controls.Add(new Label{Text="发布方式",Width=145,Height=45,TextAlign=ContentAlignment.MiddleLeft});var cloud=new RadioButton{Text="Cloudflare Pages",AutoSize=true,Checked=!selfHosted.Checked,Margin=new Padding(0,13,22,0)};mode.Controls.Add(cloud);selfHosted.Margin=new Padding(0,13,0,0);mode.Controls.Add(selfHosted);page.Controls.Add(mode);selfHosted.CheckedChanged+=(_,_)=>{serverFields.Visible=selfHosted.Checked;SavePreferences();};
    }
    string ClientConfig(){string path=Path.Combine(work.Text,"client-config","update-service.json");_=Updates.LoadConfig(path)??throw new InvalidDataException("请先保存服务配置");return path;}
    static bool ValidClient(string root)=>(File.Exists(Path.Combine(root,"光路调度路由分析.exe"))||File.Exists(Path.Combine(root,"光路调度路由查询.exe")))&&File.Exists(Path.Combine(root,"程序","FiberRouteR1.dll"));
    void SavePreferences(){try{Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile)!);File.WriteAllText(preferenceFile,JsonSerializer.Serialize(new Preferences(work.Text,url.Text,destination.Text,source.Text,selfHosted.Checked)));}catch(IOException){}catch(UnauthorizedAccessException){}}
    void LoadConfiguration(){try{var config=Updates.LoadConfig(Path.Combine(work.Text,"update-service.json"));if(config!=null)url.Text=new Uri(new Uri(config.ManifestUrl),".").AbsoluteUri;}catch(Exception e){state.Text="已保存的服务配置需修正："+e.Message;}}
    void BuildManifest()
    {
        var page=Card(tabs.TabPages[2],"生成发布清单","校验并签名本次更新包；软件和数据独立发布。");
        log.Dock=DockStyle.Fill;log.BorderStyle=BorderStyle.None;log.BackColor=Color.FromArgb(246,249,253);page.Controls.Add(log);
        ActionRow(page,ActionButton("生成发布清单",async ct=>
        {
            string kind=channel.SelectedIndex==0?"data":"software",directory=work.Text,release=version.Text.Trim(),file=package.Text,note=notes.Text;
            state.Text="正在校验更新包并生成清单…";
            var result=await Task.Run(()=>Publishing.Release(directory,kind,release,file,note),ct);
            string[] files=[];string pending="";
            try{files=PublishingDelivery.ActiveFiles(directory);}
            catch(FileNotFoundException e){pending="\r\n尚缺旧渠道文件："+e.Message+"\r\n请继续发布本轮另一渠道；两者齐全后才能生成上传ZIP。";}
            log.Text=$"软件版本：{result.Program?.Version??"尚未发布"}\r\n数据版本：{result.Resources?.Version??"尚未发布"}\r\n\r\n当前可发布文件：\r\n"+string.Join("\r\n",files)+pending;
            state.Text=pending.Length>0?"清单已生成；请补齐另一渠道文件。":"签名清单和两渠道文件已齐全，尚未上传。";
        }),ActionButton("打开待发布目录",ct=>{OpenFolder(Path.Combine(work.Text,"site"));return Task.CompletedTask;},false));
        var fields=Fields(page);Field(fields,"更新包ZIP",package,()=>{using var pick=new OpenFileDialog{Filter="更新包ZIP|*.zip"};if(pick.ShowDialog(this)!=DialogResult.OK)return;package.Text=pick.FileName;using var zip=System.IO.Compression.ZipFile.OpenRead(package.Text);var entry=zip.GetEntry("manifest.json")??zip.GetEntry("更新清单.json")??throw new InvalidDataException("不是受支持的更新包");channel.SelectedIndex=entry.FullName=="manifest.json"?0:1;using var stream=entry.Open();using var data=JsonDocument.Parse(stream);version.Text=data.RootElement.GetProperty("Version").GetString();});
    }
    void BuildPublish()
    {
        var page=Card(tabs.TabPages[3],"上传与验证","按当前发布方式上传，完成后核对线上清单。");
        Surface Step(string title,string description,params Control[] controls)
        {
            var card=new Surface{Dock=DockStyle.Top,Height=136,Padding=new Padding(14),BackColor=Color.White};
            var row=new FlowLayoutPanel{Dock=DockStyle.Bottom,Height=49,WrapContents=false,BackColor=Color.White};row.Controls.AddRange(controls);
            card.Controls.Add(row);card.Controls.Add(new Label{Text=description,Dock=DockStyle.Top,Height=30,ForeColor=Theme.Muted,BackColor=Color.White});card.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=31,Font=new Font(Font.FontFamily,12,FontStyle.Bold),BackColor=Color.White});page.Controls.Add(card);page.Controls.Add(new Panel{Dock=DockStyle.Top,Height=10,BackColor=Color.White});return card;
        }
        stop.Click+=(_,_)=>job?.Cancel();
        Step("3 验证发布","核对线上签名清单，再用客户端测试下载。",ActionButton("验证线上清单",async ct=>{await PublishingDelivery.VerifyRemote(work.Text,ct);state.Text="线上签名清单与本次发布一致；请用客户端测试下载。";},false),stop);
        var cloudCard=Step("2 上传到 Cloudflare","在现有 Pages 项目创建生产部署。",ActionButton("打开 Cloudflare 控制台",ct=>{Process.Start(new ProcessStartInfo("https://dash.cloudflare.com/"){UseShellExecute=true});return Task.CompletedTask;},false));
        var uploadCard=Step("1 生成上传包","仅包含本次发布文件，不含签名私钥。",ActionButton("生成 Cloudflare 上传ZIP",async ct=>{string workspace=Path.GetFullPath(work.Text),target=Path.Combine(workspace,"待上传-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");ct.ThrowIfCancellationRequested();await Task.Run(()=>PublishingDelivery.BuildCloudflareUpload(workspace,target),ct);state.Text="上传包已生成："+target;}),ActionButton("查看上传包",ct=>{OpenFolder(work.Text);return Task.CompletedTask;},false));
        var serverCard=Step("发布到自有服务器","把已签名文件写入配置的服务器目录。",ActionButton("发布到服务器目录",async ct=>{if(string.IsNullOrWhiteSpace(destination.Text))throw new InvalidDataException("请先选择服务器发布目录");string workspace=work.Text,target=destination.Text;var progress=new Progress<PublishingDelivery.TransferProgress>(p=>state.Text=$"发布中：{p.Name} · {p.Bytes*100/Math.Max(1,p.Total)}%");await Task.Run(()=>PublishingDelivery.PublishDirectory(workspace,target,progress,ct),ct);state.Text="服务器目录写入完成。";}),ActionButton("打开服务器目录",ct=>{if(string.IsNullOrWhiteSpace(destination.Text))throw new InvalidDataException("请先选择服务器发布目录");OpenFolder(destination.Text);return Task.CompletedTask;},false));
        selfHosted.CheckedChanged+=(_,_)=>{serverCard.Visible=selfHosted.Checked;cloudCard.Visible=uploadCard.Visible=!selfHosted.Checked;};serverCard.Visible=selfHosted.Checked;cloudCard.Visible=uploadCard.Visible=!selfHosted.Checked;
    }
    async Task Run(Func<CancellationToken,Task> action)
    {
        if(job!=null)return;job=new();stop.Enabled=true;foreach(var b in actions)b.Enabled=false;foreach(var field in new[]{work,url,destination,source,version,package,notes})field.ReadOnly=true;channel.Enabled=false;
        try{await action(job.Token);}catch(OperationCanceledException){state.Text="已取消。";}catch(Exception e){state.Text="操作未完成："+e.Message;MessageBox.Show(this,e.Message,"光路调度更新工具",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{SavePreferences();job.Dispose();job=null;stop.Enabled=false;foreach(var b in actions)b.Enabled=true;foreach(var field in new[]{work,url,destination,source,version,package,notes})field.ReadOnly=false;channel.Enabled=true;}
    }
}
