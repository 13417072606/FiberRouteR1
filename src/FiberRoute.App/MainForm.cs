using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FiberRoute.Core;
namespace FiberRoute.App;
public sealed class MainForm:Form
{
    readonly Store store;
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=40,Padding=new Padding(12),Text="就绪"};
    readonly Color accent=Color.FromArgb(0,105,108);
    CancellationTokenSource? job; bool busy; string? errorFile; Graph? routeGraph;RouteOptions? routeOptions;List<Route> routes=[];
    readonly DataGridView routeGrid=Grid(),segments=Grid(),resourceGrid=Grid(),typeGrid=Grid(),importGrid=Grid(),typePreview=Grid();
    readonly TextBox pathView=new(){Multiline=true,ReadOnly=true,Dock=DockStyle.Fill,ScrollBars=ScrollBars.Both};
    readonly NumericUpDown demand=Num(1,99999,1),jumps=Num(0,100,8),count=Num(1,50,8),minFree=Num(0,99999,0);
    readonly ComboBox order=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    readonly CheckedListBox avoidTypes=new(){Width=155,Height=112,CheckOnClick=true};
    readonly ListBox avoidNodes=new(){Width=280,Height=80};
    NodePicker start=null!,end=null!,avoid=null!;
    readonly TextBox resourceSearch=new(){Width=300},typeSearch=new(){Width=240};
    readonly ComboBox resourceType=new(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList},nodeType=new(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label resourceStats=new(){AutoSize=true},pageLabel=new(){AutoSize=true},typeStats=new(){AutoSize=true};
    readonly NumericUpDown page=Num(1,100000,1); const int PageSize=100;
    readonly TextBox importPath=new(){Width=460,ReadOnly=true};
    readonly ComboBox encoding=new(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList},units=new(){Width=80,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly FlowLayoutPanel mappings=Flow(); readonly Dictionary<string,ComboBox> map=[];
    string[] headers=[]; List<TypeChange> pendingTypes=[]; Button applyTypes=null!;
    readonly TextBox updateUrl=new(){Width=500},updateKey=new(){Multiline=true,Width=500,Height=90,ScrollBars=ScrollBars.Vertical};
    readonly Label updateStatus=new(){AutoSize=true,MaximumSize=new Size(1100,0)};
    public MainForm(Store s)
    {
        store=s;Text="纤路 R1 · 本地光路初筛";Width=1400;Height=920;MinimumSize=new Size(1100,780);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(241,245,246);
        Controls.Add(tabs);Controls.Add(status);BuildRoutes();BuildResources();BuildTypes();BuildImport();RefreshData();
        FormClosing+=(_,e)=>{if(busy){job?.Cancel();e.Cancel=true;MessageBox.Show("正在停止后台任务，完成后可关闭。");}};
        Shown+=async(_,_)=>{RestoreRouteSettings();await CheckUpdates(true);};
    }
    static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White,RowHeadersVisible=false,AutoGenerateColumns=true};
    static NumericUpDown Num(int min,int max,int val)=>new(){Minimum=min,Maximum=max,Value=val,Width=80};
    static FlowLayoutPanel Flow()=>new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
    static Label L(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(6,8,6,4)};
    Button B(string text,Action action) {var b=new Button(){Text=text,AutoSize=true,BackColor=accent,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Padding=new Padding(6,3,6,3)};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(e.Message,"纤路 R1");}};return b;}
    TabPage Tab(string title) {var p=new TabPage(title){Padding=new Padding(10),BackColor=BackColor};tabs.TabPages.Add(p);return p;}
    async Task<bool> Work(string label,Func<CancellationToken,Task> work)
    {
        if(busy){MessageBox.Show("请等待当前任务完成或停止任务。");return false;}busy=true;job=new();status.Text=label;
        try{await work(job.Token);return true;}catch(OperationCanceledException){status.Text="已取消，原数据保持不变";return false;}catch(Exception e){errorFile=Directory.GetFiles(store.Root,"导入异常-*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();status.Text="失败："+e.Message;MessageBox.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}finally{busy=false;job.Dispose();job=null;}
    }
    void BuildRoutes()
    {
        var tab=Tab("路由配置");start=new(()=>store.Graph);end=new(()=>store.Graph);avoid=new(()=>store.Graph);
        var search=Flow();search.Controls.AddRange([L("起点"),start,L("终点"),end,B("交换",()=>{var a=start.Selected;start.SelectNode(end.Selected);end.SelectNode(a);})]);
        var options=Flow();order.Items.AddRange(["候选方案（按跳纤数）","已记录距离最短","跳纤次数最少","瓶颈空闲芯数最多"]);order.SelectedIndex=0;
        options.Controls.AddRange([L("需求芯数"),demand,L("最大跳纤"),jumps,L("候选数量"),count,order,B("搜索",async()=>await SearchRoutes()),B("停止",()=>job?.Cancel()),B("导出方案",ExportRoutes)]);
        var exclusions=Flow();exclusions.Controls.AddRange([L("规避中间类型"),avoidTypes,L("具体规避节点"),avoid,B("添加",()=>{if(avoid.Selected is Node n&&!avoidNodes.Items.Cast<Node>().Any(x=>x.Id==n.Id))avoidNodes.Items.Add(n);}),avoidNodes,B("删除",()=>{if(avoidNodes.SelectedIndex>=0)avoidNodes.Items.RemoveAt(avoidNodes.SelectedIndex);})]);avoidNodes.DisplayMember="Name";
        var body=new TableLayoutPanel(){Dock=DockStyle.Fill,RowCount=3,ColumnCount=1};body.RowStyles.Add(new(SizeType.Percent,35));body.RowStyles.Add(new(SizeType.Percent,25));body.RowStyles.Add(new(SizeType.Percent,40));body.Controls.Add(routeGrid,0,0);body.Controls.Add(pathView,0,1);body.Controls.Add(segments,0,2);
        routeGrid.SelectionChanged+=(_,_)=>ShowRoute();tab.Controls.Add(body);tab.Controls.Add(exclusions);tab.Controls.Add(options);tab.Controls.Add(search);
    }
    async Task SearchRoutes()
    {
        if(start.Selected is not Node a||end.Selected is not Node b)throw new InvalidOperationException("请从搜索结果中选择完整起终点");
        var opts=new RouteOptions(a.Id,b.Id,(int)demand.Value,(int)jumps.Value,(int)count.Value,(RouteOrder)order.SelectedIndex,avoidTypes.CheckedItems.Cast<string>().ToHashSet(),avoidNodes.Items.Cast<Node>().Select(x=>x.Id).ToHashSet());
        store.SetSetting("route",JsonSerializer.Serialize(new{a=a.Name,b=b.Name,demand=(int)demand.Value,jumps=(int)jumps.Value,count=(int)count.Value,order=order.SelectedIndex,types=opts.AvoidTypes,nodes=avoidNodes.Items.Cast<Node>().Select(x=>x.Name).ToArray()}));
        await Work("正在计算…",async ct=>{
            var g=store.Graph;var result=await Task.Run(()=>Routing.Find(g,opts,ct));routeGraph=g;routeOptions=opts;routes=result.Routes;
            routeGrid.DataSource=routes.Select((r,i)=>new{方案=i+1,已记录米=Math.Round(r.Length,2),未知长度段=r.Unknown,零长度段=r.Zero,跳纤=r.Jumps,瓶颈空闲=r.Bottleneck,需求后余量=r.Bottleneck-opts.Demand}).ToList();
            status.Text=$"{routes.Count} 个方案 · 扩展 {result.Expansions:N0} · {result.ElapsedMs} ms · "+(result.Cancelled?"已停止，结果不完整":result.Limited?"达到搜索上限，结果不完整，不能保证最优":routes.Count>=opts.Count?"按所选指标从优到次返回；同值顺序不保证":"搜索完成");ShowRoute();
        });
    }
    void ShowRoute()
    {
        if(routeGraph is not Graph g||routeOptions is not RouteOptions o||routeGrid.CurrentRow is null)return;int ix=routeGrid.CurrentRow.Index;if(ix>=routes.Count)return;var r=routes[ix];
        pathView.Text="连接顺序示意（非地理地图）\r\n"+string.Join(" → ",r.Nodes.Select(n=>$"{g.Nodes[n].Name} [{g.Nodes[n].Type}{(g.Nodes[n].Confirmed?"":"·待确认")} ]"))+"\r\n资源快照不保证实时占用、芯号连续或实际接续条件。";
        segments.DataSource=r.Edges.Select(i=>{var e=g.Edges[i];return new{原始ID=e.Id,名称=e.Name,始端=g.Nodes[e.A].Name,终端=g.Nodes[e.B].Name,长度米=e.Length?.ToString("0.##",CultureInfo.InvariantCulture)??"未知",空闲=e.Free,总芯=e.Total,余量=e.Free-o.Demand};}).ToList();
    }
    void ClearRoutes(){routes=[];routeGraph=null;routeOptions=null;routeGrid.DataSource=null;segments.DataSource=null;pathView.Clear();}
    void ExportRoutes()
    {
        if(routeGraph is not Graph g||routeOptions is not RouteOptions o||routes.Count==0)throw new InvalidOperationException("无方案可导出");
        var path=Save("路由方案.csv");if(path==null)return;
        var rows=new List<object?[]>{new object?[]{"资源版本","方案","排序方式","需求芯数","最大跳纤","规避类型","规避节点","总已记录米","未知长度段","零长度段","跳纤","瓶颈空闲","段序","原始ID","中继段","始端","终端","长度米","空闲","总芯","需求后余量"}};
        for(int i=0;i<routes.Count;i++){var r=routes[i];for(int j=0;j<r.Edges.Length;j++){var e=g.Edges[r.Edges[j]];rows.Add([g.Version,i+1,o.Order,o.Demand,o.MaxJumps,string.Join(';',o.AvoidTypes),string.Join(';',o.AvoidNodes.Select(n=>g.Nodes[n].Name)),r.Length,r.Unknown,r.Zero,r.Jumps,r.Bottleneck,j+1,e.Id,e.Name,g.Nodes[r.Nodes[j]].Name,g.Nodes[r.Nodes[j+1]].Name,e.Length,e.Free,e.Total,e.Free-o.Demand]);}}
        Csv.Write(path,rows);status.Text="方案已导出；CSV ID 为文本内容，Excel 打开时请按文本列导入。";
    }
    void BuildResources()
    {
        var tab=Tab("中继段资源");var top=Flow();top.Controls.AddRange([L("关键词（含ID、站点、机房）"),resourceSearch,L("最少空闲"),minFree,L("任一端点类型"),resourceType,B("筛选",()=>{page.Value=1;LoadResources();}),B("导出筛选资源",async()=>await ExportResources()),B("完整详情",()=>{if(resourceGrid.CurrentRow?.Cells["内部序号"].Value is int ix){using var f=new Form(){Text="中继段完整详情",Width=850,Height=650};var t=new TextBox(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};t.Text=JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(store.Raw(ix)),new JsonSerializerOptions(){WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});f.Controls.Add(t);f.ShowDialog();}})]);
        var stats=Flow();stats.Controls.Add(resourceStats);var pager=Flow();pager.Dock=DockStyle.Bottom;pager.Controls.AddRange([B("上一页",()=>{if(page.Value>1)page.Value--;LoadResources();}),page,B("转到页",LoadResources),B("下一页",()=>{page.Value++;LoadResources();}),pageLabel]);tab.Controls.Add(resourceGrid);tab.Controls.Add(pager);tab.Controls.Add(stats);tab.Controls.Add(top);
    }
    IEnumerable<Edge> FilterResources()
    {
        var g=store.Graph;string term=resourceSearch.Text.Trim();string type=resourceType.SelectedItem as string??"全部";HashSet<int>? ids=null;
        if(term.Length>0)ids=SearchRaw(term);
        return g.Edges.Where(e=>e.Free>=minFree.Value&&(ids==null||ids.Contains(e.Index))&&(type=="全部"||g.Nodes[e.A].Type.Split(';').Contains(type)||g.Nodes[e.B].Type.Split(';').Contains(type)));
    }
    HashSet<int> SearchRaw(string term)
    {
        var ids=new HashSet<int>();if(!File.Exists(store.Database))return ids;
        using var c=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(){DataSource=store.Database,Pooling=false}.ToString());c.Open();using var q=c.CreateCommand();q.CommandText="SELECT id FROM edges WHERE instr(raw,$s)>0 OR instr(business_id,$s)>0 OR instr(name,$s)>0";q.Parameters.AddWithValue("$s",term);using var r=q.ExecuteReader();while(r.Read())ids.Add(r.GetInt32(0));return ids;
    }
    void LoadResources()
    {
        var g=store.Graph;var filtered=FilterResources().ToArray();int pages=Math.Max(1,(filtered.Length+PageSize-1)/PageSize);if(page.Value>pages)page.Value=pages;pageLabel.Text=$"共 {filtered.Length:N0} 条 / {pages} 页（每页100）";
        resourceGrid.DataSource=filtered.Skip(((int)page.Value-1)*PageSize).Take(PageSize).Select(e=>new{内部序号=e.Index,ID=e.Id,名称=e.Name,始端=g.Nodes[e.A].Name,终端=g.Nodes[e.B].Name,始端类型=g.Nodes[e.A].Type,终端类型=g.Nodes[e.B].Type,长度米=e.Length?.ToString("0.##")??"未知",空闲=e.Free,总芯=e.Total}).ToList();
        if(resourceGrid.Columns.Contains("内部序号"))resourceGrid.Columns["内部序号"].Visible=false;
    }
    async Task ExportResources()
    {
        var selected=FilterResources().ToArray();var path=Save("筛选资源.csv");if(path==null)return;
        bool completed=await Work("正在导出筛选资源…",ct=>Task.Run(()=>{
            string temp=path+"."+Guid.NewGuid()+".tmp";
            try
            {
            using(var w=new StreamWriter(temp,false,new System.Text.UTF8Encoding(true)))
            {
            var wanted=selected.Select(e=>e.Index).ToHashSet();
            using var c=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(){DataSource=store.Database,Pooling=false}.ToString());c.Open();using var q=c.CreateCommand();q.CommandText="SELECT id,raw FROM edges ORDER BY id";using var reader=q.ExecuteReader();string[]? hs=null;
            while(reader.Read()){ct.ThrowIfCancellationRequested();if(!wanted.Contains(reader.GetInt32(0)))continue;var row=JsonSerializer.Deserialize<Dictionary<string,string>>(reader.GetString(1))!;if(hs==null){hs=row.Keys.ToArray();w.WriteLine(Csv.Line(hs));}w.WriteLine(Csv.Line(hs.Select(h=>row.GetValueOrDefault(h,""))));}
            }
            ct.ThrowIfCancellationRequested();File.Move(temp,path,true);
            }finally{if(File.Exists(temp))File.Delete(temp);}

        },ct));if(completed)status.Text="导出完成";
    }
    void BuildTypes()
    {
        var tab=Tab("节点类型");var top=Flow();var custom=new TextBox(){Width=170};
        top.Controls.AddRange([L("节点关键词"),typeSearch,nodeType,B("筛选",LoadTypes),L("设置类型（;分隔多标签）"),custom,B("保存类型",()=>{if(busy)return;if(typeGrid.CurrentRow?.Cells["名称"].Value is string name){if(string.IsNullOrWhiteSpace(custom.Text))throw new ArgumentException("类型不能为空");store.SetTypes([new(name,"",custom.Text.Trim(),"可更新")]);RefreshData();}}),B("导出类型表",()=>{var p=Save("节点类型.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"节点名称","节点类型"}}.Concat(store.Graph.Nodes.Select(n=>new object?[]{n.Name,n.Type})));}),B("导入类型表",PreviewTypes)]);
        var buttons=Flow();applyTypes=B("确认更新类型",()=>{if(busy)return;store.SetTypes(pendingTypes);pendingTypes=[];typePreview.DataSource=null;applyTypes.Enabled=false;RefreshData();});applyTypes.Enabled=false;buttons.Controls.AddRange([typeStats,applyTypes,B("导出预览报告",()=>{var p=Save("节点类型预览报告.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"节点名称","原类型","新类型","状态"}}.Concat(pendingTypes.Select(x=>new object?[]{x.Name,x.Before,x.After,x.Status})));}),B("历史未匹配类型",()=>{var known=store.Graph.Nodes.Select(n=>n.Name).ToHashSet();var p=Save("历史未匹配类型.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"节点名称","人工类型","状态"}}.Concat(store.ManualTypes().Where(x=>!known.Contains(x.Key)).Select(x=>new object?[]{x.Key,x.Value,"资源更新后未匹配，请核对改名或消失"})));}),B("取消预览",()=>{pendingTypes=[];typePreview.DataSource=null;applyTypes.Enabled=false;})]);
        var body=new TableLayoutPanel(){Dock=DockStyle.Fill,ColumnCount=1,RowCount=2};body.RowStyles.Add(new(SizeType.Percent,60));body.RowStyles.Add(new(SizeType.Percent,40));body.Controls.Add(typeGrid,0,0);body.Controls.Add(typePreview,0,1);tab.Controls.Add(body);tab.Controls.Add(buttons);tab.Controls.Add(top);
    }
    void LoadTypes()
    {
        string type=nodeType.SelectedItem as string??"全部";var matches=store.Graph.Nodes.Where(n=>n.Name.Contains(typeSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)&&(type=="全部"||n.Type.Split(';').Contains(type))).ToArray();
        typeGrid.DataSource=matches.Take(500).Select(n=>new{名称=n.Name,类型=n.Type,标注=n.Confirmed?"人工确认":"资源/经验·待确认"}).ToList();int missing=store.ManualTypes().Keys.Except(store.Graph.Nodes.Select(n=>n.Name)).Count();typeStats.Text=$"匹配 {matches.Length:N0} 个，显示前500；历史人工类型未匹配 {missing} 个";
    }
    void PreviewTypes()
    {
        if(busy)return;using var file=new OpenFileDialog(){Filter="CSV文件|*.csv"};if(file.ShowDialog()!=DialogResult.OK)return;
        using var dialog=new Form(){Text="类型表字段映射",Width=650,Height=260,StartPosition=FormStartPosition.CenterParent};var panel=Flow();var enc=new ComboBox(){Width=110,DropDownStyle=ComboBoxStyle.DropDownList};enc.Items.AddRange(["UTF-8","GB18030","GBK"]);enc.SelectedIndex=0;var nc=new ComboBox(){Width=180,DropDownStyle=ComboBoxStyle.DropDownList};var tc=new ComboBox(){Width=180,DropDownStyle=ComboBoxStyle.DropDownList};
        void Read(){var h=Csv.Read(file.FileName,enc.Text).First();nc.Items.Clear();tc.Items.Clear();nc.Items.AddRange(h);tc.Items.AddRange(h);nc.SelectedItem=h.Contains("节点名称")?"节点名称":h[0];tc.SelectedItem=h.Contains("节点类型")?"节点类型":h.Last();}
        enc.SelectedIndexChanged+=(_,_)=>{try{Read();}catch(Exception e){MessageBox.Show(e.Message);}};Read();panel.Controls.AddRange([L("编码"),enc,B("重新识别",Read),L("节点名称字段"),nc,L("节点类型字段"),tc,B("预览",()=>{pendingTypes=store.PreviewTypes(file.FileName,enc.Text,nc.Text,tc.Text);dialog.DialogResult=DialogResult.OK;})]);dialog.Controls.Add(panel);
        if(dialog.ShowDialog()==DialogResult.OK){typePreview.DataSource=pendingTypes.Select(x=>new{节点=x.Name,原类型=x.Before,新类型=x.After,校验=x.Status}).ToList();applyTypes.Enabled=pendingTypes.Any(x=>x.Status=="可更新");typeStats.Text=$"类型预览 {pendingTypes.Count} 行，可更新 {pendingTypes.Count(x=>x.Status=="可更新")} 行；其余隔离";}
    }
    void BuildImport()
    {
        var tab=Tab("数据导入");encoding.Items.AddRange(["UTF-8","GB18030","GBK"]);encoding.SelectedIndex=0;units.Items.AddRange(["米","千米"]);units.SelectedIndex=0;
        var top=Flow();top.Controls.AddRange([importPath,B("选择CSV",()=>{using var d=new OpenFileDialog(){Filter="CSV文件|*.csv"};if(d.ShowDialog()==DialogResult.OK){importPath.Text=d.FileName;ReadPreview();}}),encoding,L("原表长度单位"),units,B("重新预览",ReadPreview),B("导入调测数据",()=>{importPath.Text=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","调测中继段.csv"));encoding.SelectedIndex=0;units.SelectedIndex=0;ReadPreview();}),B("前往节点类型",()=>tabs.SelectedIndex=2)]);
        var controls=Flow();controls.Controls.AddRange([B("确认替换并导入",async()=>await ImportData()),B("取消任务",()=>job?.Cancel()),B("导出上次异常",()=>{if(errorFile==null||!File.Exists(errorFile))throw new InvalidOperationException("暂无异常报告");var p=Save("导入异常.csv");if(p!=null)File.Copy(errorFile,p,true);}),B("回退上一版",()=>{if(busy)return;if(MessageBox.Show("切换到上一版资源？人工类型及设置保留。","确认回退",MessageBoxButtons.YesNo)==DialogResult.Yes){store.Rollback();store.SetSetting("remoteResourceVersion","");RefreshData();}})]);
        var update=Flow();update.Dock=DockStyle.Bottom;update.Controls.AddRange([L("在线更新（未配置时保持本地使用）"),updateStatus,L("HTTPS签名清单地址"),updateUrl,L("RSA公钥PEM"),updateKey,B("保存更新源",()=>{if(updateUrl.Text.Length>0){var uri=new Uri(updateUrl.Text);if(uri.Scheme!="https"||uri.UserInfo.Length>0)throw new ArgumentException("必须为无凭据的HTTPS地址");using var rsa=System.Security.Cryptography.RSA.Create();rsa.ImportFromPem(updateKey.Text);}store.SetSetting("update",JsonSerializer.Serialize(new UpdateConfig(updateUrl.Text.Trim(),updateKey.Text.Trim())));updateStatus.Text=updateUrl.Text.Length==0?"未配置更新源":"已配置，等待检查";}),B("检查更新",async()=>await CheckUpdates(false))]);
        var cfg=store.Setting("update");if(cfg!=null){try{var c=JsonSerializer.Deserialize<UpdateConfig>(cfg)!;updateUrl.Text=c.ManifestUrl;updateKey.Text=c.PublicKeyPem;}catch{}}
        tab.Controls.Add(importGrid);tab.Controls.Add(update);tab.Controls.Add(controls);tab.Controls.Add(mappings);tab.Controls.Add(top);
    }
    void ReadPreview()
    {
        if(busy)return;var rows=Csv.Read(importPath.Text,encoding.Text).Take(21).ToArray();if(rows.Length==0)throw new InvalidDataException("CSV为空");headers=rows[0].Select(x=>x.Trim()).ToArray();if(headers.Distinct().Count()!=headers.Length)throw new InvalidDataException("表头重名");
        mappings.Controls.Clear();map.Clear();foreach(string field in Store.Fields){var combo=new ComboBox(){Width=125,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.Add("（无）");combo.Items.AddRange(headers);combo.SelectedItem=headers.Contains(field)?field:"（无）";mappings.Controls.Add(L(field+(Store.Required.Contains(field)?"*":"")));mappings.Controls.Add(combo);map[field]=combo;}
        var data=new DataTable();foreach(var h in headers)data.Columns.Add(h);foreach(var r in rows.Skip(1)){var row=data.NewRow();for(int i=0;i<Math.Min(r.Length,headers.Length);i++)row[i]=r[i];data.Rows.Add(row);}importGrid.DataSource=data;status.Text="仅预览前20条；确认导入时全量校验。缺失及零长度可接入，异常记录隔离。";
    }
    async Task ImportData()
    {
        if(map.Count==0)throw new InvalidOperationException("请先选择并预览CSV");if(MessageBox.Show("校验成功后替换当前资源；失败或取消保留原版。继续？","确认导入",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        string file=importPath.Text,enc=encoding.Text;double unit=units.SelectedIndex==0?1:1000;var mapping=map.ToDictionary(x=>x.Key,x=>x.Value.Text);
        await Work("开始导入…",async ct=>{
            var progress=new Progress<ImportProgress>(p=>status.Text=$"读取 {p.Read:N0} · 可接入 {p.Accepted:N0} · 异常 {p.Invalid:N0} · 节点 {p.Nodes:N0}（可取消）");
            var r=await store.ImportAsync(file,enc,mapping,unit,progress,ct);store.SetSetting("remoteResourceVersion","");errorFile=r.ErrorFile;RefreshData();status.Text=$"完成：{r.Accepted:N0} 段，{r.Nodes:N0} 节点，{r.Invalid:N0} 条异常。数据版本 {r.Version}";
        });
    }
    void RefreshData()
    {
        ClearRoutes();pendingTypes=[];typePreview.DataSource=null;if(applyTypes!=null)applyTypes.Enabled=false;start?.Clear();end?.Clear();avoid?.Clear();avoidNodes.Items.Clear();var types=store.Graph.Nodes.SelectMany(n=>n.Type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)).Concat(new[]{"站点","机房","光交箱","分纤箱","未识别"}).Distinct().Order().ToArray();
        var checkedTypes=avoidTypes.CheckedItems.Cast<string>().ToHashSet();avoidTypes.Items.Clear();foreach(var t in types)avoidTypes.Items.Add(t,checkedTypes.Contains(t));
        foreach(var c in new[]{resourceType,nodeType}){var old=c.SelectedItem;c.Items.Clear();c.Items.Add("全部");c.Items.AddRange(types);c.SelectedItem=old!=null&&c.Items.Contains(old)?old:"全部";}
        var g=store.Graph;resourceStats.Text=$"{g.Edges.Length:N0} 段 / {g.Nodes.Length:N0} 节点 / 已记录 {g.Edges.Sum(e=>e.Length??0):N2} 米 / 空闲逐段合计 {g.Edges.Sum(e=>(long)e.Free):N0} 芯（非路由数） / 版本 {g.Version}";LoadResources();LoadTypes();
    }
    void RestoreRouteSettings()
    {
        try{if(store.Setting("route") is not string s)return;var r=JsonDocument.Parse(s).RootElement;demand.Value=Math.Clamp(r.GetProperty("demand").GetInt32(),1,99999);jumps.Value=Math.Clamp(r.GetProperty("jumps").GetInt32(),0,100);count.Value=Math.Clamp(r.GetProperty("count").GetInt32(),1,50);order.SelectedIndex=Math.Clamp(r.GetProperty("order").GetInt32(),0,3);start.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("a").GetString()));end.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("b").GetString()));var ts=r.GetProperty("types").EnumerateArray().Select(x=>x.GetString()).ToHashSet();for(int i=0;i<avoidTypes.Items.Count;i++)avoidTypes.SetItemChecked(i,ts.Contains(avoidTypes.Items[i].ToString()));foreach(var name in r.GetProperty("nodes").EnumerateArray()){var node=store.Graph.Nodes.FirstOrDefault(n=>n.Name==name.GetString());if(node!=null)avoidNodes.Items.Add(node);}}catch{/* Older or damaged personal query preferences are optional. */}
    }
    static string? Save(string name) {string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","导出结果"));try{Directory.CreateDirectory(dir);}catch{dir=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);}using var d=new SaveFileDialog(){FileName=name,Filter="CSV文件|*.csv",InitialDirectory=dir};return d.ShowDialog()==DialogResult.OK?d.FileName:null;}
    async Task CheckUpdates(bool automatic)
    {
        if(string.IsNullOrWhiteSpace(updateUrl.Text)){updateStatus.Text="未配置更新源；查询、导入及路由均在本地可用";return;}
        if(busy)return;
        await Work("正在检查签名更新…",async ct=>{
            var cfg=new UpdateConfig(updateUrl.Text.Trim(),updateKey.Text.Trim());var manifest=await Updates.Check(cfg,ct);var staging=Path.Combine(store.Root,"updates");
            if(manifest.Resources is ReleaseItem res && store.Setting("remoteResourceVersion")!=res.Version)
            {
                string file=await Updates.Download(cfg,res,staging,ct);try{var hs=Csv.Read(file).First();var mapping=Store.Fields.ToDictionary(x=>x,x=>x);foreach(var field in Store.Required)if(!hs.Contains(field))throw new InvalidDataException("资源更新必须为UTF-8 CSV（米），缺字段 "+field);var result=await store.ImportAsync(file,"UTF-8",mapping,1,new Progress<ImportProgress>(p=>status.Text=$"更新资源 {p.Read:N0} 行"),ct);errorFile=result.ErrorFile;store.SetSetting("remoteResourceVersion",res.Version);store.SetSetting("resourceUpdatedAt",DateTimeOffset.Now.ToString("O"));RefreshData();}finally{File.Delete(file);}
            }
            string info=$"检查成功 · 资源版本 {store.Setting("remoteResourceVersion")??"本地导入"} · 更新时间 {store.Setting("resourceUpdatedAt")??"无"}";
            if(manifest.Program is ReleaseItem app && Version.TryParse(app.Version,out var remote)&&remote>(typeof(MainForm).Assembly.GetName().Version??new Version(1,0,0)))
            {
                string package=await Updates.Download(cfg,app,staging,ct);info+=" · 程序更新已校验下载";
                if(MessageBox.Show("发现程序版本 "+app.Version+"，更新包已通过签名清单及哈希校验。立即退出并更新？","程序更新",MessageBoxButtons.YesNo)==DialogResult.Yes)
                {
                    string updater=Path.Combine(AppContext.BaseDirectory,"FiberRoute.Updater.exe");string helper=Path.Combine(staging,"helper");Directory.CreateDirectory(helper);
                    foreach(string f in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(f,Path.Combine(helper,Path.GetFileName(f)),true);
                    var p=new ProcessStartInfo(Path.Combine(helper,Path.GetFileName(updater))){UseShellExecute=false};foreach(var a in new[]{Environment.ProcessId.ToString(),package,Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..")),"1.0.0",app.Sha256})p.ArgumentList.Add(a);Process.Start(p);busy=false;Close();
                }
            }
            updateStatus.Text=info;status.Text=info;
        });
        if(status.Text.StartsWith("失败："))updateStatus.Text="更新失败，保留本地资源。"+status.Text;
    }
}
public sealed class NodePicker:UserControl
{
    readonly TextBox input=new(){Dock=DockStyle.Top};readonly ListBox list=new(){Dock=DockStyle.Fill,DisplayMember="Name"};readonly Func<Graph> graph;readonly System.Windows.Forms.Timer timer=new(){Interval=220};Node? selected;
    public Node? Selected=>selected;
    public NodePicker(Func<Graph> source)
    {
        graph=source;Width=300;Height=112;Controls.Add(list);Controls.Add(input);
        input.TextChanged+=(_,_)=>{selected=null;timer.Stop();timer.Start();};timer.Tick+=(_,_)=>{timer.Stop();list.DataSource=graph().Nodes.Where(n=>n.Name.Contains(input.Text.Trim(),StringComparison.OrdinalIgnoreCase)).Take(30).ToList();};
        list.SelectedIndexChanged+=(_,_)=>selected=list.SelectedItem as Node;list.DoubleClick+=(_,_)=>{if(list.SelectedItem is Node n)SelectNode(n);};
    }
    public void SelectNode(Node? n){input.Text=n?.Name??"";timer.Stop();list.DataSource=n is null?new List<Node>():new List<Node>(){n};selected=n;}
    public void Clear()=>SelectNode(null);
    protected override void Dispose(bool disposing){if(disposing)timer.Dispose();base.Dispose(disposing);}
}
