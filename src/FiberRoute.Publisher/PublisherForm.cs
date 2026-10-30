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
    sealed record Preferences(string Workspace,string Url,string Destination,string Source);
    readonly TabControl tabs=new(){Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,ItemSize=new Size(0,1),SizeMode=TabSizeMode.Fixed};readonly List<Button> actions=[];readonly UiButton stop=new(){Text="停止当前任务",Primary=false,Width=180,Height=44,Enabled=false};CancellationTokenSource? job;
    static string AppRoot=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));
    public PublisherForm(string? preferences=null)
    {
        preferenceFile=preferences??Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","publisher-preferences.json");
        Text="光路调度路由查询 · 管理方发布工具 R1.5.0";ClientSize=new Size(1080,800);MinimumSize=new Size(900,720);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Canvas;ForeColor=Theme.Ink;AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        var brand=BrandAssets.Load();Icon=brand.Icon??(File.Exists(Path.Combine(AppContext.BaseDirectory,"app.ico"))?new Icon(Path.Combine(AppContext.BaseDirectory,"app.ico")):null);brand.Image?.Dispose();
        work.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments),"纤路发布工作区");source.Text=Path.Combine(AppRoot,"数据");string location=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");if(File.Exists(location))source.Text=File.ReadAllText(location).Trim();version.Text=DateTime.Now.ToString("yyyy-MM-dd")+".1";try{if(File.Exists(preferenceFile)){var saved=JsonSerializer.Deserialize<Preferences>(File.ReadAllText(preferenceFile));if(saved!=null){work.Text=saved.Workspace;url.Text=saved.Url;destination.Text=saved.Destination;source.Text=saved.Source;}}}catch{}
        var heading=new Panel{Dock=DockStyle.Top,Height=100,Padding=new Padding(20,12,20,12),BackColor=Color.White};heading.Controls.Add(new Label{Text="管理方发布工具",Font=new Font(Font.FontFamily,21,FontStyle.Bold),Dock=DockStyle.Top,Height=36});heading.Controls.Add(new Label{Text="整理资源、生成更新包、签名发布；客户在软件内检测更新",Dock=DockStyle.Bottom,Height=22,ForeColor=Theme.Muted});
        var brandButton=new UiButton{Text="更换品牌图标",Dock=DockStyle.Right,Width=150,Height=42};brandButton.Click+=(_,_)=>{try{using var imagePick=new OpenFileDialog{Title="选择品牌 PNG 原图",Filter="PNG 图片|*.png"};if(imagePick.ShowDialog(this)!=DialogResult.OK)return;using var folderPick=new FolderBrowserDialog{Description="选择需要更换图标的完整运行目录"};if(folderPick.ShowDialog(this)!=DialogResult.OK)return;BrandApplication.Install(imagePick.FileName,folderPick.SelectedPath);MessageBox.Show(this,"品牌图标已更新。","管理方发布工具");}catch(Exception ex){MessageBox.Show(this,ex.Message,"更换品牌图标失败");}};heading.Controls.Add(brandButton);
        var navigation=new FlowLayoutPanel{Dock=DockStyle.Top,Height=58,Padding=new Padding(16,8,16,8),BackColor=Color.White,WrapContents=false};
        var body=new Panel{Dock=DockStyle.Fill,Padding=new Padding(16),BackColor=Theme.Canvas};body.Controls.Add(tabs);Controls.Add(body);Controls.Add(navigation);Controls.Add(heading);state.Padding=new Padding(16,0,0,0);Controls.Add(state);
        string[] steps=["1 生成更新包","2 配置服务","3 生成发布清单","4 发布与验证"];
        for(int i=0;i<steps.Length;i++){int index=i;var tab=new TabPage{BackColor=Theme.Canvas,Padding=new Padding(0)};tabs.TabPages.Add(tab);var button=new UiButton{Text=steps[i],Width=244,Height=40,Selected=i==0,Margin=new Padding(0,0,10,0)};button.Click+=(_,_)=>tabs.SelectedIndex=index;navigation.Controls.Add(button);tabs.SelectedIndexChanged+=(_,_)=>{button.Selected=tabs.SelectedIndex==index;button.Invalidate();};}
        BuildPackage();BuildService();BuildManifest();BuildPublish();state.Text="选择资源后开始。尚无服务地址也可以先在本地生成更新包。";LoadConfiguration();FormClosing+=(_,e)=>{if(job!=null){job.Cancel();e.Cancel=true;}else SavePreferences();};
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
        var page=Card(tabs.TabPages[0],"生成软件或数据更新包","数据包来自已导入并复核的数据库；软件包来自开发方提供的新版本完整运行目录。请先关闭正在使用该数据目录的主程序。");
        var bottom=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};page.Controls.Add(bottom);Message(bottom,"本工具不会把原始Excel、个人设置或原始personal.db直接打进客户更新包。\n数据无变化会在客户更新时忽略；人工冲突保留并提示复核。",80);
        ActionRow(page,ActionButton("生成更新包",async ct=>
        {
            string input=Path.GetFullPath(source.Text),release=version.Text.Trim(),kind=channel.SelectedIndex==0?"data":"software";if(!System.Text.RegularExpressions.Regex.IsMatch(release,@"^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$"))throw new InvalidDataException("版本号请使用数字、字母、点、短横线或下划线");if(kind=="data"&&(!File.Exists(Path.Combine(input,"resources.db"))||!File.Exists(Path.Combine(input,"personal.db"))))throw new InvalidDataException("请选择包含resources.db和personal.db的数据目录，可在主程序设置中打开数据目录定位");string folder=Path.Combine(Path.GetFullPath(work.Text),"packages");Directory.CreateDirectory(folder);string file=Path.Combine(folder,(kind=="data"?"数据-":"软件-")+release+".zip");if(File.Exists(file))throw new IOException("同名更新包已存在，请填写新的版本号，或先核对已有文件。");
            state.Text="正在生成更新包，请稍候…";await Task.Run(()=>{ct.ThrowIfCancellationRequested();if(kind=="data"){var store=new Store(input);store.ApplyClusterRepairBaseline(Path.Combine(AppRoot,"数据","集群站点修复基线.db"),ct);store.BuildDataPackage(file,release,ct);}else Publishing.BuildProgramPackage(input,file,release);ct.ThrowIfCancellationRequested();},ct);package.Text=file;state.Text="更新包已生成："+file;tabs.SelectedIndex=1;
        }),ActionButton("打开工作目录",ct=>{OpenFolder(work.Text);return Task.CompletedTask;},false));
        var fields=Fields(page);channel.Items.AddRange(["数据更新","软件更新"]);channel.SelectedIndex=0;Field(fields,"更新类型",channel);Field(fields,"资源来源目录",source,()=>{Folder(source);if(channel.SelectedIndex==1){string dll=Path.Combine(source.Text,"程序","FiberRouteR1.dll");if(File.Exists(dll))version.Text=AssemblyName.GetAssemblyName(dll).Version!.ToString(3);}});Field(fields,"版本号",version);notes.Multiline=true;notes.Height=45;Field(fields,"更新说明",notes);channel.SelectedIndexChanged+=(_,_)=>{version.Text=channel.SelectedIndex==0?DateTime.Now.ToString("yyyy-MM-dd")+".1":"1.5.0";};
    }
    void BuildService()
    {
        var page=Card(tabs.TabPages[1],"配置更新服务","Cloudflare Pages 只需发布一次；每台客户电脑首次安装公开配置后，便可自行检测和下载更新。");
        Message(page,"管理工作目录保存签名私钥，只放在管理员电脑。给其他电脑的是 client-config/update-service.json（公开地址和公钥），不要发送 private-key.pem。\nCloudflare Pages：服务器发布目录留空；第 4 步生成上传 ZIP，部署到现有项目。",80);
        ActionRow(page,ActionButton("保存服务配置",async ct=>{string directory=work.Text,address=url.Text.Trim();if(!Uri.TryCreate(address,UriKind.Absolute,out var service)||service.Scheme!="https")throw new InvalidDataException("请填写真实HTTPS更新目录地址；尚无地址时可先在第1步生成本地更新包");state.Text="正在保存服务配置…";await Task.Run(()=>Publishing.SaveConfiguration(directory,address),ct);state.Text="服务配置已保存。其他电脑只需首次安装公开配置，此后可自行连接 Cloudflare 下载更新。";}),ActionButton("导出其他电脑配置",ct=>{string config=Path.Combine(work.Text,"client-config","update-service.json");_=Updates.LoadConfig(config)??throw new InvalidDataException("请先初始化服务配置");using var pick=new FolderBrowserDialog{Description="选择保存客户配置的目录"};if(pick.ShowDialog(this)==DialogResult.OK){File.Copy(config,Path.Combine(pick.SelectedPath,"update-service.json"),true);state.Text="已导出公开配置：发给其他用户，放入各自客户端根目录的“资源”文件夹；只需一次。";}return Task.CompletedTask;},false));
        ActionRow(page,ActionButton("应用到本机客户端",ct=>{string config=Path.Combine(work.Text,"client-config","update-service.json");_=Updates.LoadConfig(config)??throw new InvalidDataException("请先初始化服务配置");string target=AppRoot;if(!File.Exists(Path.Combine(target,"光路调度路由查询.exe"))||!File.Exists(Path.Combine(target,"程序","FiberRouteR1.dll"))){using var pick=new FolderBrowserDialog{Description="选择客户主程序运行目录，包含程序和数据文件夹"};if(pick.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;target=pick.SelectedPath;if(!File.Exists(Path.Combine(target,"光路调度路由查询.exe"))||!File.Exists(Path.Combine(target,"程序","FiberRouteR1.dll")))throw new InvalidDataException("所选目录不是主程序运行目录");}Directory.CreateDirectory(Path.Combine(target,"资源"));File.Copy(config,Path.Combine(target,"资源","update-service.json"),true);state.Text="仅本机客户端已配置；其他用户请生成客户启用包，首次安装后即可自行下载。";return Task.CompletedTask;},false),ActionButton("生成客户启用包",ct=>{using var pick=new SaveFileDialog{Title="保存给其他电脑的在线更新启用包",Filter="ZIP 压缩包|*.zip",FileName="客户在线更新启用包.zip",OverwritePrompt=false};if(pick.ShowDialog(this)!=DialogResult.OK)return Task.CompletedTask;string output=PublishingDelivery.BuildClientActivationPackage(work.Text,pick.FileName);state.Text="客户启用包已生成："+output+"；发给其他用户，每台电脑首次运行其中的 Install.cmd。";return Task.CompletedTask;},false),ActionButton("打开服务器部署说明",ct=>{string doc=Path.Combine(AppRoot,"服务器部署","Windows服务器部署说明.md");if(File.Exists(doc))Process.Start(new ProcessStartInfo(doc){UseShellExecute=true});else state.Text="请使用随交付提供的Windows服务器部署包。";return Task.CompletedTask;},false));
        var fields=Fields(page);Field(fields,"管理工作目录",work,()=>{Folder(work);LoadConfiguration();});url.PlaceholderText="https://单位真实域名/fiber-updates/";Field(fields,"客户HTTPS地址",url);Field(fields,"服务器发布目录",destination,()=>Folder(destination));
    }
    void SavePreferences(){try{Directory.CreateDirectory(Path.GetDirectoryName(preferenceFile)!);File.WriteAllText(preferenceFile,JsonSerializer.Serialize(new Preferences(work.Text,url.Text,destination.Text,source.Text)));}catch(IOException){}catch(UnauthorizedAccessException){}}
    void LoadConfiguration(){try{var config=Updates.LoadConfig(Path.Combine(work.Text,"update-service.json"));if(config!=null)url.Text=new Uri(new Uri(config.ManifestUrl),".").AbsoluteUri;}catch(Exception e){state.Text="已保存的服务配置需修正："+e.Message;}}
    void BuildManifest()
    {
        var page=Card(tabs.TabPages[2],"生成签名发布清单","选择上一步生成的更新包。软件和数据独立发布，发布一个渠道会保留另一个渠道。");
        log.Dock=DockStyle.Fill;log.BorderStyle=BorderStyle.None;log.BackColor=Color.FromArgb(246,249,253);page.Controls.Add(log);
        ActionRow(page,ActionButton("生成发布清单",async ct=>
        {
            string kind=channel.SelectedIndex==0?"data":"software",directory=work.Text,release=version.Text.Trim(),file=package.Text,note=notes.Text;
            state.Text="正在校验更新包并生成清单…";
            var result=await Task.Run(()=>Publishing.Release(directory,kind,release,file,note),ct);
            string[] files=[];string pending="";
            try{files=PublishingDelivery.ActiveFiles(directory);}
            catch(FileNotFoundException e){pending="\r\n尚缺旧渠道文件："+e.Message+"\r\n请继续发布本轮另一渠道；两者齐全后才能生成上传ZIP。";}
            log.Text=$"软件版本：{result.Program?.Version??"尚未发布"}\r\n数据版本：{result.Resources?.Version??"尚未发布"}\r\n\r\n当前可发布文件：\r\n"+string.Join("\r\n",files)+pending+"\r\n\r\n两渠道齐全后，在第4步生成Cloudflare上传ZIP。";
            state.Text=pending.Length>0?"清单已生成；请补齐另一渠道文件。":"签名清单和两渠道文件已齐全，尚未上传。";
        }),ActionButton("打开待发布目录",ct=>{OpenFolder(Path.Combine(work.Text,"site"));return Task.CompletedTask;},false));
        var fields=Fields(page);Field(fields,"更新包ZIP",package,()=>{using var pick=new OpenFileDialog{Filter="更新包ZIP|*.zip"};if(pick.ShowDialog(this)!=DialogResult.OK)return;package.Text=pick.FileName;using var zip=System.IO.Compression.ZipFile.OpenRead(package.Text);var entry=zip.GetEntry("manifest.json")??zip.GetEntry("更新清单.json")??throw new InvalidDataException("不是受支持的更新包");channel.SelectedIndex=entry.FullName=="manifest.json"?0:1;using var stream=entry.Open();using var data=JsonDocument.Parse(stream);version.Text=data.RootElement.GetProperty("Version").GetString();});
    }
    void BuildPublish()
    {
        var page=Card(tabs.TabPages[3],"发布到 Cloudflare Pages 并验证","生成本次签名清单对应的上传 ZIP，解压后将文件夹部署到现有 Pages 项目的生产环境。");
        Message(page,"先点“生成 Cloudflare 上传ZIP”，完整解压后把内含 manifest.json、program、data 的文件夹上传至现有 Pages 项目。不要上传整个管理工作目录，其中有签名私钥。\n部署完成后点“验证外网清单”；客户电脑首次安装公开配置后，就能自行检测和下载。\n\n下方的“发布到服务器目录”仅适用于自有服务器，Cloudflare Pages 用户无需使用。",125);
        var publishRow=ActionRow(page,ActionButton("发布到服务器目录",async ct=>{if(string.IsNullOrWhiteSpace(destination.Text))throw new InvalidDataException("请在第2步选择服务器发布目录");string workspace=work.Text,target=destination.Text;var progress=new Progress<PublishingDelivery.TransferProgress>(p=>state.Text=$"发布中：{p.Name} · {p.Bytes*100/Math.Max(1,p.Total)}%");await Task.Run(()=>PublishingDelivery.PublishDirectory(workspace,target,progress,ct),ct);state.Text="服务器目录写入完成；请继续验证外网清单。";}),ActionButton("验证外网清单",async ct=>{await PublishingDelivery.VerifyRemote(work.Text,ct);state.Text="外网签名清单与本次发布一致。请在客户机测试更新。";},false),ActionButton("打开服务器目录",ct=>{if(string.IsNullOrWhiteSpace(destination.Text))throw new InvalidDataException("请先设置服务器发布目录");OpenFolder(destination.Text);return Task.CompletedTask;},false));
        ActionRow(page,ActionButton("生成 Cloudflare 上传ZIP",async ct=>{string workspace=Path.GetFullPath(work.Text),target=Path.Combine(workspace,"待上传-"+DateTime.Now.ToString("yyyyMMdd-HHmmss")+".zip");ct.ThrowIfCancellationRequested();await Task.Run(()=>PublishingDelivery.BuildCloudflareUpload(workspace,target),ct);state.Text="只含当前发布文件的上传包已生成："+target;},false),ActionButton("打开管理工作目录",ct=>{OpenFolder(work.Text);return Task.CompletedTask;},false));
        stop.Click+=(_,_)=>job?.Cancel();publishRow.Controls.Add(stop);
    }
    async Task Run(Func<CancellationToken,Task> action)
    {
        if(job!=null)return;job=new();stop.Enabled=true;foreach(var b in actions)b.Enabled=false;foreach(var field in new[]{work,url,destination,source,version,package,notes})field.ReadOnly=true;channel.Enabled=false;
        try{await action(job.Token);}catch(OperationCanceledException){state.Text="已取消。未发布的新清单不会替换服务器当前版本。";}catch(Exception e){state.Text="操作未完成："+e.Message;MessageBox.Show(this,e.Message,"管理方发布工具",MessageBoxButtons.OK,MessageBoxIcon.Warning);}finally{SavePreferences();job.Dispose();job=null;stop.Enabled=false;foreach(var b in actions)b.Enabled=true;foreach(var field in new[]{work,url,destination,source,version,package,notes})field.ReadOnly=false;channel.Enabled=true;}
    }
}
