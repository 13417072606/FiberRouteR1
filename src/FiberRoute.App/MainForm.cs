using System.Data;
using System.Diagnostics;
using System.Globalization;
using System.Text.Json;
using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm:Form
{
    readonly Store store;
    readonly TabControl tabs=new(){Dock=DockStyle.Fill};
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=40,Padding=new Padding(12),Text="就绪"};
    readonly Color accent=Color.FromArgb(8,126,128);
    CancellationTokenSource? job; bool busy; bool resourceRefreshPending; string? errorFile; Graph? routeGraph;RouteOptions? routeOptions;List<Route> routes=[];
    readonly DataGridView routeGrid=Grid(),segments=Grid(),resourceGrid=Grid(),typeGrid=Grid(),importGrid=Grid(),typePreview=Grid();
    readonly PathDiagram pathView=new(){Dock=DockStyle.Fill};
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
        store=s;Text="光路调度智能分析 R1.3";Width=1400;Height=920;MinimumSize=new Size(1100,780);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(241,245,246);
        AutoScaleMode=AutoScaleMode.Dpi;
        tabs.Appearance=TabAppearance.FlatButtons;tabs.ItemSize=new Size(1,1);tabs.SizeMode=TabSizeMode.Fixed;
        Controls.Add(tabs);Controls.Add(status);BuildRoutes();BuildResources();BuildTypes();BuildImport();Controls.Add(BuildHeader());InstallLayoutMemory();RefreshData();
        FormClosing+=(_,e)=>{if(busy){job?.Cancel();e.Cancel=true;MessageBox.Show("正在停止后台任务，完成后可关闭。");}};
        Shown+=(_,_)=>{RestoreRouteSettings();};
    }
    static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,AllowUserToResizeColumns=true,AllowUserToResizeRows=true,AllowUserToOrderColumns=true,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None,BorderStyle=BorderStyle.None,GridColor=Color.FromArgb(232,238,240),RowTemplate={Height=48},DefaultCellStyle={WrapMode=DataGridViewTriState.True,Padding=new Padding(6)},ColumnHeadersDefaultCellStyle={BackColor=Color.FromArgb(241,246,247),ForeColor=Color.FromArgb(35,50,65),Padding=new Padding(6)},EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White,RowHeadersVisible=true,RowHeadersWidth=24,AutoGenerateColumns=true};
    static NumericUpDown Num(int min,int max,int val)=>new(){Minimum=min,Maximum=max,Value=val,Width=80};
    static FlowLayoutPanel Flow()=>new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
    static Label L(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(6,8,6,4)};
    Button B(string text,Action action) {var b=new Button(){Text=text,AutoSize=true,BackColor=accent,ForeColor=Color.White,FlatStyle=FlatStyle.Flat,Padding=new Padding(6,3,6,3)};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(e.Message,"纤路 R1");}};return b;}
    Button B(string text,Func<Task> action)
    {return B(text,()=>{_ = Run();});async Task Run(){try{await action();}catch(Exception e){MessageBox.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}}}
    TabPage Tab(string title) {var p=new TabPage(title){Padding=new Padding(10),BackColor=BackColor};tabs.TabPages.Add(p);return p;}
    async Task<bool> Work(string label,Func<CancellationToken,Task> work)
    {
        if(busy){MessageBox.Show("请等待当前任务完成或停止任务。");return false;}busy=true;job=new();status.Text=label;
        try{await work(job.Token);return true;}catch(OperationCanceledException){status.Text="已取消，原数据保持不变";return false;}catch(Exception e){errorFile=Directory.GetFiles(store.Root,"导入异常-*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();status.Text="失败："+e.Message;MessageBox.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}finally{busy=false;job.Dispose();job=null;if(resourceRefreshPending){resourceRefreshPending=false;BeginInvoke((Action)LoadResources);}}
    }
    void BuildRoutes()
    {
        var tab=Tab("路由配置");start=new(()=>store.Graph);end=new(()=>store.Graph);avoid=new(()=>store.Graph);
        avoidNodes.DisplayMember="Name";
        order.Items.AddRange(["候选方案（按跳纤数）","已记录距离最短","跳纤次数最少","瓶颈空闲芯数最多"]);order.SelectedIndex=0;
        var left=Stack();
        left.Controls.AddRange([L("01  设置起终点"),L("起点设施（输入关键词并选择）"),start,
            B("交换起终点",()=>{var a=start.Selected;start.SelectNode(end.Selected);end.SelectNode(a);}),L("终点设施"),end,
            L("02  容量与路径限制"),Field("需求芯数",demand),Field("最大跳纤数",jumps),Field("最多返回方案数",count),
            L("03  规避中间节点类型（端点豁免）"),avoidTypes,L("具体节点规避"),avoid,
            B("添加规避节点",()=>{if(avoid.Selected is Node n&&!avoidNodes.Items.Cast<Node>().Any(x=>x.Id==n.Id))avoidNodes.Items.Add(n);}),avoidNodes,
            B("删除选中规避",()=>{if(avoidNodes.SelectedIndex>=0)avoidNodes.Items.RemoveAt(avoidNodes.SelectedIndex);}),
            B("搜索路由",async()=>await SearchRoutes()),B("停止搜索",()=>job?.Cancel())]);
        var extra=Stack();var moving=left.Controls.Cast<Control>().Skip(10).Where(c=>c!=null).ToArray();
        // Keep the first controls visible; detailed exclusion controls are grouped below the query action.
        var detail=left.Controls.Cast<Control>().FirstOrDefault(c=>c is Label l&&l.Text.StartsWith("03"));
        if(detail!=null){int at=left.Controls.GetChildIndex(detail);var controls=left.Controls.Cast<Control>().Skip(at).Where(c=>c is not Button b||(!b.Text.Contains("搜索路由")&&!b.Text.Contains("停止搜索"))).ToArray();foreach(var c in controls){left.Controls.Remove(c);extra.Controls.Add(c);}extra.Height=560;extra.Dock=DockStyle.None;extra.Visible=false;left.Controls.Add(B("更多条件 ▾",()=>{extra.Visible=!extra.Visible;}));left.Controls.Add(extra);}
        var results=Rows([25,35,40],Card("候选方案",routeGrid),Card("连接顺序示意 · 非地理地图（可横向滚动）",pathView),Card("逐段资源详情 · 快照不保证实时占用或实际接续条件",segments));
        var actions=Flow();actions.Controls.AddRange([L("结果方式"),order,B("按所选方式搜索",async()=>await SearchRoutes()),B("导出方案",ExportRoutes)]);
        var right=new Panel{Dock=DockStyle.Fill};right.Controls.Add(results);right.Controls.Add(actions);
        tab.Controls.Add(Columns(30,left,70,right));routeGrid.SelectionChanged+=(_,_)=>ShowRoute();
    }

    async Task SearchRoutes()
    {
        if(start.Selected is not Node a||end.Selected is not Node b)throw new InvalidOperationException("请从搜索结果中选择完整起终点");
        var opts=new RouteOptions(a.Id,b.Id,(int)demand.Value,(int)jumps.Value,(int)count.Value,(RouteOrder)order.SelectedIndex,avoidTypes.CheckedItems.Cast<string>().ToHashSet(),avoidNodes.Items.Cast<Node>().Select(x=>x.Id).ToHashSet());
        store.SetSetting("route",JsonSerializer.Serialize(new{a=a.Name,b=b.Name,demand=(int)demand.Value,jumps=(int)jumps.Value,count=(int)count.Value,order=order.SelectedIndex,types=opts.AvoidTypes,nodes=avoidNodes.Items.Cast<Node>().Select(x=>x.Name).ToArray()}));
        await Work("正在计算…",async ct=>{
            var g=store.Graph;var result=await Task.Run(()=>Routing.Find(g,opts,ct));routeGraph=g;routeOptions=opts;routes=result.Routes;
            routeGrid.DataSource=routes.Select((r,i)=>new{方案=i+1,已记录米=Math.Round(r.Length,2),未知长度段=r.Unknown,零长度段=r.Zero,跳纤=r.Jumps,瓶颈空闲=r.Bottleneck,需求后余量=r.Edges.Length==0?"内部关系未核验":(r.Bottleneck-opts.Demand).ToString()}).ToList();
            status.Text=$"{routes.Count} 个方案 · 扩展 {result.Expansions:N0} · {result.ElapsedMs} ms · "+(result.Cancelled?"已停止，结果不完整":result.Limited?"达到搜索上限，结果不完整，不能保证最优":routes.Count>=opts.Count?"按所选指标从优到次返回；同值顺序不保证":"搜索完成");ShowRoute();if(routes.Any(x=>x.Transfers.Length>0))status.Text+=" · 含集群内部关系，内部可用芯数未单独核验";
        });
    }
    void ShowRoute()
    {
        if(routeGraph is not Graph g||routeOptions is not RouteOptions o||routeGrid.CurrentRow is null)return;int ix=routeGrid.CurrentRow.Index;if(ix>=routes.Count)return;var r=routes[ix];
        pathView.SetRoute(g,r);
        segments.DataSource=r.Edges.Select(i=>{var e=g.Edges[i];return new{原始ID=e.Id,名称=e.Name,始端=g.Nodes[e.A].Name,终端=g.Nodes[e.B].Name,长度米=e.Length?.ToString("0.##",CultureInfo.InvariantCulture)??"未知",空闲=e.Free,总芯=e.Total,余量=e.Free-o.Demand};}).ToList();
    }
    void ClearRoutes(){routes=[];routeGraph=null;routeOptions=null;routeGrid.DataSource=null;segments.DataSource=null;pathView.Clear();}
    void ExportRoutes()
    {
        if(routeGraph is not Graph g||routeOptions is not RouteOptions o||routes.Count==0)throw new InvalidOperationException("无方案可导出");
        var path=Save("路由方案.csv");if(path==null)return;
        var rows=new List<object?[]>{new object?[]{"资源版本","方案","排序方式","需求芯数","最大跳纤","规避类型","规避节点","总已记录米","未知长度段","零长度段","跳纤","瓶颈空闲","段序","原始ID","中继段","始端","终端","长度米","空闲","总芯","需求后余量"}};
        for(int i=0;i<routes.Count;i++){var r=routes[i];for(int j=0;j<r.Edges.Length;j++){var e=g.Edges[r.Edges[j]];rows.Add([g.Version,i+1,o.Order,o.Demand,o.MaxJumps,string.Join(';',o.AvoidTypes),string.Join(';',o.AvoidNodes.Select(n=>g.Nodes[n].Name)),r.Length,r.Unknown,r.Zero,r.Jumps,r.Bottleneck,j+1,e.Id,e.Name,g.Nodes[r.Steps.Length>j?r.Steps[j].From:r.Nodes[j]].Name,g.Nodes[r.Steps.Length>j?r.Steps[j].To:r.Nodes[j+1]].Name,e.Length,e.Free,e.Total,e.Free-o.Demand]);}}
        Csv.Write(path,rows);status.Text="方案已导出；CSV ID 为文本内容，Excel 打开时请按文本列导入。";
    }
    void BuildResources()
    {
        var tab=Tab("中继段资源");var top=Flow();top.Controls.AddRange([L("关键词（含ID、站点、机房）"),resourceSearch,L("最少空闲"),minFree,L("任一端点类型"),resourceType,B("筛选",()=>{page.Value=1;LoadResources();}),B("导出筛选资源",async()=>await ExportResources()),B("完整详情",()=>{if(resourceGrid.CurrentRow?.Cells["内部序号"].Value is int ix){using var f=new Form(){Text="中继段完整详情",Width=850,Height=650};var t=new TextBox(){Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};t.Text=JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(store.Raw(ix)),new JsonSerializerOptions(){WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});f.Controls.Add(t);f.ShowDialog();}})]);
        var stats=Flow();stats.Controls.Add(resourceStats);var pager=Flow();pager.Dock=DockStyle.Bottom;pager.Controls.AddRange([B("上一页",()=>{if(page.Value>1)page.Value--;LoadResources();}),page,B("转到页",LoadResources),B("下一页",()=>{page.Value++;LoadResources();}),pageLabel]);tab.Controls.Add(Card("中继段资源",resourceGrid));tab.Controls.Add(pager);tab.Controls.Add(top);tab.Controls.Add(Stats());
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
    async void LoadResources()
    {
        if(busy){resourceRefreshPending=true;return;}
        var g=store.Graph;string term=resourceSearch.Text.Trim(),type=resourceType.SelectedItem as string??"全部";int minimum=(int)minFree.Value;Edge[] filtered=[];
        bool ok=await Work("正在筛选资源…",async ct=>{
            filtered=await Task.Run(()=>{var ids=term.Length>0?SearchRaw(term):null;ct.ThrowIfCancellationRequested();return g.Edges.Where(e=>e.Free>=minimum&&(ids==null||ids.Contains(e.Index))&&(type=="全部"||g.Nodes[e.A].Type.Split(';').Contains(type)||g.Nodes[e.B].Type.Split(';').Contains(type))).ToArray();},ct);
        });if(!ok)return;int pages=Math.Max(1,(filtered.Length+PageSize-1)/PageSize);if(page.Value>pages)page.Value=pages;pageLabel.Text=$"共 {filtered.Length:N0} 条 / {pages} 页（每页100）";
        resourceGrid.DataSource=filtered.Skip(((int)page.Value-1)*PageSize).Take(PageSize).Select(e=>new{内部序号=e.Index,ID=e.Id,名称=e.Name,始端=g.Nodes[e.A].Name,终端=g.Nodes[e.B].Name,始端类型=g.Nodes[e.A].Type,终端类型=g.Nodes[e.B].Type,长度米=e.Length?.ToString("0.##")??"未知",空闲=e.Free,总芯=e.Total,业务状态=RawStatus(e.Index)}).ToList();
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
    async Task PreviewTypes()
    {
        if(busy)return;using var file=new OpenFileDialog{Filter=TableInput.Filter};if(file.ShowDialog()!=DialogResult.OK)return;
        using var dialog=new Form{Text="节点类型表 · 格式、工作表及字段映射",Width=680,Height=540,StartPosition=FormStartPosition.CenterParent,Font=Font};
        var panel=Stack();var enc=new ComboBox{Width=240,DropDownStyle=ComboBoxStyle.DropDownList};enc.Items.AddRange(["自动","UTF-8","GB18030","GBK","utf-16","utf-16BE"]);enc.SelectedIndex=0;
        var sheet=new ComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var nc=new ComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var tc=new ComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var info=L("");
        string? key=null;string detected="自动";
        string Key()=>enc.Text+"\n"+sheet.Text;
        async Task Read()
        {
            string chosenEnc=enc.Text;string? chosen=sheet.SelectedItem as string;key=null;
            await Work("正在读取类型表字段…",async ct=>{
                var r=await Task.Run(()=>{var meta=TableInput.Inspect(file.FileName,chosenEnc,ct);string? sh=meta.Sheets.Contains(chosen)?chosen:meta.Sheets.FirstOrDefault();var hs=TableInput.Read(file.FileName,meta.Format=="CSV"?meta.Encoding:chosenEnc,sh).FirstOrDefault()??[];return (meta,sh,hs);},ct);
                if(r.hs.Length==0)throw new InvalidDataException("类型表为空或表头重名");
                sheet.Items.Clear();sheet.Items.AddRange(r.meta.Sheets);if(r.sh!=null)sheet.SelectedItem=r.sh;sheet.Enabled=r.meta.Format!="CSV";enc.Enabled=r.meta.Format=="CSV";info.Text=r.meta.Format+" · "+r.meta.Encoding;
                nc.Items.Clear();tc.Items.Clear();nc.Items.AddRange(r.hs);tc.Items.AddRange(r.hs);nc.SelectedItem=r.hs.Contains("节点名称")?"节点名称":r.hs[0];tc.SelectedItem=r.hs.Contains("节点类型")?"节点类型":r.hs.Last();detected=r.meta.Format=="CSV"?r.meta.Encoding:chosenEnc;key=Key();
            });
        }
        panel.Controls.AddRange([L("编码"),enc,L("工作表（切换后刷新字段）"),sheet,info,B("刷新字段",Read),L("节点名称字段"),nc,L("节点类型字段"),tc,B("生成匹配预览",async()=>{
            if(key==null||key!=Key())throw new InvalidOperationException("请先刷新当前编码及工作表字段");if(nc.Text==tc.Text)throw new InvalidOperationException("名称与类型必须选择不同字段");
            string name=nc.Text,type=tc.Text;string? sh=sheet.SelectedItem as string;
            bool ok=await Work("正在校验类型匹配…",async ct=>{pendingTypes=await Task.Run(()=>store.PreviewTypes(file.FileName,detected,name,type,sh,ct),ct);});if(ok)dialog.DialogResult=DialogResult.OK;
        })]);dialog.Controls.Add(panel);await Read();
        if(dialog.ShowDialog(this)==DialogResult.OK){typePreview.DataSource=pendingTypes.Select(x=>new{节点=x.Name,原类型=x.Before,新类型=x.After,校验=x.Status}).ToList();applyTypes.Enabled=pendingTypes.Any(x=>x.Status=="可更新");typeStats.Text=$"预览 {pendingTypes.Count} 行 · 可更新 {pendingTypes.Count(x=>x.Status=="可更新")} · 未匹配 {pendingTypes.Count(x=>x.Status=="未匹配")} · 空类型 {pendingTypes.Count(x=>x.Status=="空类型")} · 冲突 {pendingTypes.Count(x=>x.Status=="同名类型冲突")}";}
    }
    void BuildImport()
    {
        var tab=Tab("数据导入");resourceFormat.Items.AddRange(["标准CSV / Excel","原始导出17列（末列多值）"]);resourceFormat.SelectedIndex=0;encoding.Items.AddRange(["自动","UTF-8","GB18030","GBK","utf-16","utf-16BE"]);encoding.SelectedIndex=0;
        units.Items.AddRange(["米","千米"]);units.SelectedIndex=0;importPath.Dock=DockStyle.None;
        var left=Stack();
        left.Controls.AddRange([L("01  选择资源文件"),importPath,B("选择 CSV / XLS / XLSX",async()=>{
            if(busy)return;using var d=new OpenFileDialog{Filter=TableInput.Filter};if(d.ShowDialog()!=DialogResult.OK)return;
            importPath.Text=d.FileName;sheets.Items.Clear();if(Path.GetExtension(d.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase))resourceFormat.SelectedIndex=await Task.Run(()=>Csv.IsResourceExport(d.FileName,"自动"))?1:0;else resourceFormat.SelectedIndex=0;await ReadPreview();}),L("读取格式"),resourceFormat,L("CSV编码（默认自动）"),encoding,inputInfo,
            L("Excel工作表（切换后重新预览）"),sheets,L("原表长度单位"),units,B("读取 / 刷新预览",async()=>await ReadPreview()),
            B("载入附带调测数据",async()=>{if(busy)return;importPath.Text=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","调测中继段.csv"));encoding.SelectedIndex=0;sheets.Items.Clear();units.SelectedIndex=0;await ReadPreview();}),
            L("02  字段映射（*为必需）"),mappings]);
        mappings.FlowDirection=FlowDirection.TopDown;mappings.WrapContents=false;mappings.Dock=DockStyle.None;mappings.Width=300;
        var confirm=Flow();confirm.Controls.AddRange([B("确认校验并替换资源",async()=>await ImportData()),B("停止导入",()=>job?.Cancel()),B("导出上次异常",()=>{if(errorFile==null||!File.Exists(errorFile))throw new InvalidOperationException("暂无异常报告");var p=Save("导入异常.csv");if(p!=null)File.Copy(errorFile,p,true);}),B("回退上一版",()=>{if(busy)return;if(MessageBox.Show("切换到上一版资源？人工类型及设置保留。","确认回退",MessageBoxButtons.YesNo)==DialogResult.Yes){store.Rollback();store.SetSetting("remoteResourceVersion","");RefreshData();}})]);
        var right=new Panel{Dock=DockStyle.Fill};right.Controls.Add(Card("数据预览 · 前20条，确认后后台全量校验",importGrid));right.Controls.Add(confirm);right.Controls.Add(previewStats);
        tab.Controls.Add(Columns(30,left,70,right));
    }

    async Task ReadPreview()
    {
        if(busy)return;string file=importPath.Text,enc=encoding.Text;string? requested=sheets.SelectedItem as string;bool raw=resourceFormat.SelectedIndex==1;
        previewKey=null;importGrid.DataSource=null;map.Clear();mappings.Controls.Clear();
        await Work("正在识别格式、严格检测编码并读取预览…",async ct=>{
            var result=await Task.Run(()=>{
                var info=TableInput.Inspect(file,enc,ct);var sheet=info.Format=="CSV"&&raw?"__RESOURCE_RAW__":info.Sheets.Contains(requested)?requested:info.Sheets.FirstOrDefault();
                var rows=TableInput.Read(file,info.Format=="CSV"?info.Encoding:enc,sheet).Take(21).ToArray();
                return (info,sheet,rows);
            },ct);
            if(result.rows.Length==0)throw new InvalidDataException("所选数据表为空");
            sheets.Items.Clear();sheets.Items.AddRange(result.info.Sheets);if(result.sheet!=null&&result.info.Format!="CSV")sheets.SelectedItem=result.sheet;
            sheets.Enabled=result.info.Format!="CSV";encoding.Enabled=result.info.Format=="CSV";
            inputInfo.Text=$"格式：{result.info.Format}  ·  编码：{result.info.Encoding}";
            headers=result.rows[0].Select(x=>x.Trim()).ToArray();if(headers.Any(string.IsNullOrWhiteSpace)||headers.Distinct().Count()!=headers.Length)throw new InvalidDataException("表头存在空白或重名，请检查表头行");
            foreach(string field in Store.Fields){var combo=new ComboBox{Width=240,DropDownStyle=ComboBoxStyle.DropDownList};combo.Items.Add("（无）");combo.Items.AddRange(headers);combo.SelectedItem=headers.Contains(field)?field:"（无）";mappings.Controls.Add(L(field+(Store.Required.Contains(field)?"*":"")));mappings.Controls.Add(combo);map[field]=combo;}
            var data=new DataTable();foreach(var h in headers)data.Columns.Add(h);foreach(var r in result.rows.Skip(1)){var row=data.NewRow();for(int i=0;i<Math.Min(r.Length,headers.Length);i++)row[i]=r[i];data.Rows.Add(row);}importGrid.DataSource=data;
            detectedEncoding=result.info.Format=="CSV"?result.info.Encoding:enc;previewKey=InputKey();
            previewStats.Text=$"预览 {data.Rows.Count} 条 · {headers.Length} 个字段 · 缺少自动映射 {Store.Required.Count(f=>!headers.Contains(f))} 项（请在左侧映射）\n全量校验在确认后执行；异常隔离，失败或停止保留原库。";
            status.Text="预览完成。请核对编码、读取格式、工作表、ID和字段映射后确认导入。";
        });
    }

    async Task ImportData()
    {
        if(map.Count==0||previewKey!=InputKey())throw new InvalidOperationException("请先重新预览当前文件、编码及工作表");foreach(var field in Store.Required)if(map[field].Text=="（无）")throw new InvalidOperationException("请映射必需字段："+field);if(MessageBox.Show("校验成功后替换当前资源；失败或取消保留原版。继续？","确认导入",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        string file=importPath.Text,enc=detectedEncoding;string? sheet=resourceFormat.SelectedIndex==1&&sheets.Items.Count==0?"__RESOURCE_RAW__":sheets.SelectedItem as string;double unit=units.SelectedIndex==0?1:1000;var mapping=map.ToDictionary(x=>x.Key,x=>x.Value.Text);
        await Work("开始导入…",async ct=>{
            var progress=new Progress<ImportProgress>(p=>status.Text=$"读取 {p.Read:N0} · 可接入 {p.Accepted:N0} · 异常 {p.Invalid:N0} · 节点 {p.Nodes:N0}（可取消）");
            var r=await store.ImportAsync(file,enc,mapping,unit,progress,ct,sheet);store.SetSetting("remoteResourceVersion","");errorFile=r.ErrorFile;RefreshData();if(r.Unchanged){status.Text="文件内容及导入设置无变化，已跳过更新，不新增数据库或报告。";return;}previewStats.Text=$"校验完成：读取 {r.Read:N0} 条 · 可接入 {r.Accepted:N0} 条 · 隔离异常 {r.Invalid:N0} 条";status.Text=$"完成：{r.Accepted:N0} 段，{r.Nodes:N0} 节点，{r.Invalid:N0} 条异常。数据版本 {r.Version}";
        });
    }
    void RefreshData()
    {
        UpdateStats();ClearRoutes();pendingTypes=[];typePreview.DataSource=null;if(applyTypes!=null)applyTypes.Enabled=false;start?.Clear();end?.Clear();avoid?.Clear();avoidNodes.Items.Clear();var types=store.Graph.Nodes.SelectMany(n=>n.Type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)).Concat(new[]{"站点","机房","光交箱","分纤箱","未识别"}).Distinct().Order().ToArray();
        var checkedTypes=avoidTypes.CheckedItems.Cast<string>().ToHashSet();avoidTypes.Items.Clear();foreach(var t in types)avoidTypes.Items.Add(t,checkedTypes.Contains(t));
        foreach(var c in new[]{resourceType,nodeType}){var old=c.SelectedItem;c.Items.Clear();c.Items.Add("全部");c.Items.AddRange(types);c.SelectedItem=old!=null&&c.Items.Contains(old)?old:"全部";}
        var g=store.Graph;resourceStats.Text=$"{g.Edges.Length:N0} 段 / {g.Nodes.Length:N0} 节点 / 已记录 {g.Edges.Sum(e=>e.Length??0):N2} 米 / 空闲逐段合计 {g.Edges.Sum(e=>(long)e.Free):N0} 芯（非路由数） / 版本 {g.Version}";LoadResources();LoadTypes();LoadClusters();
    }
    void RestoreRouteSettings()
    {
        try{if(store.Setting("route") is not string s)return;var r=JsonDocument.Parse(s).RootElement;demand.Value=Math.Clamp(r.GetProperty("demand").GetInt32(),1,99999);jumps.Value=Math.Clamp(r.GetProperty("jumps").GetInt32(),0,100);count.Value=Math.Clamp(r.GetProperty("count").GetInt32(),1,50);order.SelectedIndex=Math.Clamp(r.GetProperty("order").GetInt32(),0,3);start.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("a").GetString()));end.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("b").GetString()));var ts=r.GetProperty("types").EnumerateArray().Select(x=>x.GetString()).ToHashSet();for(int i=0;i<avoidTypes.Items.Count;i++)avoidTypes.SetItemChecked(i,ts.Contains(avoidTypes.Items[i].ToString()));foreach(var name in r.GetProperty("nodes").EnumerateArray()){var node=store.Graph.Nodes.FirstOrDefault(n=>n.Name==name.GetString());if(node!=null)avoidNodes.Items.Add(node);}}catch{/* Older or damaged personal query preferences are optional. */}
    }
    static string? Save(string name) {string dir=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","导出结果"));try{Directory.CreateDirectory(dir);}catch{dir=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);}using var d=new SaveFileDialog(){FileName=name,Filter="CSV文件|*.csv",InitialDirectory=dir};return d.ShowDialog()==DialogResult.OK?d.FileName:null;}

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
