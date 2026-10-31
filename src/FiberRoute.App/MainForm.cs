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
    readonly Label status=new(){Dock=DockStyle.Bottom,Height=29,Padding=new Padding(8,5,8,4),Text="就绪"};
    readonly Color accent=Color.FromArgb(8,126,128);
    CancellationTokenSource? job; bool busy; bool resourceRefreshPending; string? errorFile; Graph? routeGraph;RouteOptions? routeOptions;List<Route> routes=[];
    readonly DataGridView routeGrid=Grid(),segments=Grid(),resourceGrid=Grid(),typeGrid=Grid(),importGrid=Grid(),typePreview=Grid();
    readonly PathDiagram pathView=new(){Dock=DockStyle.Fill};
    readonly NumericUpDown demand=Num(1,99999,1),jumps=Num(0,100,8),count=Num(1,50,8),minFree=Num(0,99999,0);
    readonly ComboBox order=new UiComboBox(){DropDownStyle=ComboBoxStyle.DropDownList,Width=180};
    readonly TypeChecklist avoidTypes=new();
    readonly ListBox avoidNodes=new(){Width=280,Height=80};
    NodePicker start=null!,end=null!,avoid=null!;
    readonly TextBox resourceSearch=new(){Width=300},typeSearch=new(){Width=240};
    readonly ComboBox resourceType=new UiComboBox(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList},nodeType=new UiComboBox(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label resourceStats=new(){AutoSize=true},pageLabel=new(){AutoSize=true},typeStats=new(){AutoSize=true};
    readonly NumericUpDown page=Num(1,100000,1); const int PageSize=100;
    readonly TextBox importPath=new(){Width=460,ReadOnly=true};
    readonly ComboBox encoding=new UiComboBox(){Width=120,DropDownStyle=ComboBoxStyle.DropDownList},units=new UiComboBox(){Width=80,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly FlowLayoutPanel mappings=Flow(); readonly Dictionary<string,ComboBox> map=[];
    string[] headers=[]; List<TypeChange> pendingTypes=[]; Button applyTypes=null!;
    readonly TextBox updateUrl=new(){Width=500},updateKey=new(){Multiline=true,Width=500,Height=90,ScrollBars=ScrollBars.Vertical};
    readonly Label updateStatus=new(){AutoSize=true,MaximumSize=new Size(1100,0)};
    bool initialTablesPending=true;
    public MainForm(Store s)
    {
        SuspendLayout();
        store=s;Text="光路调度路由查询 R"+DisplayVersion;Width=1380;Height=950;MinimumSize=new Size(1080,700);StartPosition=FormStartPosition.CenterScreen;Font=new Font("Microsoft YaHei UI",10);BackColor=Theme.Canvas;ForeColor=Theme.Ink;
        AutoScaleDimensions=new SizeF(96,96);AutoScaleMode=AutoScaleMode.Dpi;
        tabs.Appearance=TabAppearance.FlatButtons;tabs.ItemSize=new Size(1,1);tabs.SizeMode=TabSizeMode.Fixed;
        Controls.Add(tabs);Controls.Add(status);BuildRoutes();BuildResources();BuildTypes();BuildImport();Controls.Add(BuildHeader());InstallLayoutMemory();RefreshData();tabs.SelectedIndexChanged+=(_,_)=>{if(tabs.SelectedIndex==1)LoadResources();else if(tabs.SelectedIndex==2){LoadTypes();LoadClusters();LoadRooms();}};
        EventHandler icons=(_,_)=>{if(Icon!=null)foreach(Form child in Application.OpenForms.Cast<Form>().ToArray())if(child!=this&&!child.IsDisposed&&!ReferenceEquals(child.Icon,Icon))child.Icon=Icon;};Application.Idle+=icons;FormClosed+=(_,_)=>Application.Idle-=icons;
        FormClosing+=(_,e)=>{if(busy){job?.Cancel();e.Cancel=true;MessageBox.Show("正在停止后台任务，完成后可关闭。");}};
        Shown+=(_,_)=>{RestoreRouteSettings();StartDailyUpdateCheck();};
        AutoScaleDimensions=new SizeF(96,96);ResumeLayout(true);
    }
    static DataGridView Grid()=>new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AllowUserToDeleteRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None,AllowUserToResizeColumns=true,AllowUserToResizeRows=true,AllowUserToOrderColumns=true,AutoSizeRowsMode=DataGridViewAutoSizeRowsMode.None,BorderStyle=BorderStyle.None,GridColor=Theme.Line,RowTemplate={Height=40},DefaultCellStyle={Alignment=DataGridViewContentAlignment.MiddleLeft,WrapMode=DataGridViewTriState.True,Padding=new Padding(9,5,9,5),ForeColor=Theme.Ink,SelectionBackColor=Theme.Tint,SelectionForeColor=Theme.Ink},ColumnHeadersDefaultCellStyle={BackColor=Color.FromArgb(239,244,250),ForeColor=Theme.Ink,SelectionBackColor=Color.FromArgb(239,244,250),SelectionForeColor=Theme.Ink,Padding=new Padding(9,8,9,8),WrapMode=DataGridViewTriState.True,Alignment=DataGridViewContentAlignment.MiddleLeft},ColumnHeadersHeight=52,ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.AutoSize,EnableHeadersVisualStyles=false,SelectionMode=DataGridViewSelectionMode.FullRowSelect,MultiSelect=false,BackgroundColor=Color.White,RowHeadersVisible=false,RowHeadersWidth=24,CellBorderStyle=DataGridViewCellBorderStyle.None,ColumnHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,RowHeadersBorderStyle=DataGridViewHeaderBorderStyle.None,AutoGenerateColumns=true,ScrollBars=ScrollBars.Both};
    static NumericUpDown Num(int min,int max,int val)=>new(){Minimum=min,Maximum=max,Value=val,Width=110,AutoSize=false,Height=38,MinimumSize=new Size(70,34)};
    static FlowLayoutPanel Flow()=>new(){Dock=DockStyle.Top,AutoSize=true,WrapContents=true,Padding=new Padding(8)};
    static Label L(string text)=>new(){Text=text,AutoSize=true,Margin=new Padding(6,8,6,4)};
    Button B(string text,Action action) {var b=new UiButton(){Text=text,AutoSize=true,MinimumSize=new Size(72,40),Primary=text.StartsWith("确认")||text.StartsWith("查询")||text.StartsWith("导入")||text.StartsWith("保存")};b.Click+=(_,_)=>{try{action();}catch(Exception e){MessageBox.Show(e.Message,"光路调度路由查询");}};return b;}
    Button B(string text,Func<Task> action)
    {return B(text,()=>{_ = Run();});async Task Run(){try{await action();}catch(Exception e){MessageBox.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}}}
    TabPage Tab(string title) {var p=new TabPage(title){Padding=new Padding(10),BackColor=BackColor};tabs.TabPages.Add(p);return p;}
    async Task<bool> Work(string label,Func<CancellationToken,Task> work)
    {
        if(busy){MessageBox.Show("请等待当前任务完成或停止任务。");return false;}busy=true;requireCapacity.Enabled=false;job=new();status.Text=label;
        try{await work(job.Token);return true;}catch(OperationCanceledException){status.Text="已取消，原数据保持不变";return false;}catch(Exception e){errorFile=Directory.GetFiles(store.Root,"导入异常-*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault();status.Text="失败："+e.Message;MessageBox.Show(e.Message,"操作失败",MessageBoxButtons.OK,MessageBoxIcon.Error);return false;}finally{busy=false;requireCapacity.Enabled=true;job.Dispose();job=null;if(resourceRefreshPending){resourceRefreshPending=false;BeginInvoke((Action)LoadResources);}}
    }
    void BuildRoutes()
    {
        BuildRouteWorkspace();
    }

    async Task SearchRoutes()
    {
        if(start.Selected is not Node a||end.Selected is not Node b)throw new InvalidOperationException("请从搜索结果中选择完整起终点");
        var opts=new RouteOptions(a.Id,b.Id,(int)demand.Value,(int)jumps.Value,(int)count.Value,(RouteOrder)order.SelectedIndex,avoidTypes.CheckedTypes.ToHashSet(),avoidNodes.Items.Cast<Node>().Select(x=>x.Id).ToHashSet(),RequireCapacity:requireCapacity.Checked);
        store.SetSetting("route",JsonSerializer.Serialize(new{a=a.Name,b=b.Name,demand=(int)demand.Value,jumps=(int)jumps.Value,count=(int)count.Value,order=order.SelectedIndex,capacityOnly=requireCapacity.Checked,types=opts.AvoidTypes,nodes=avoidNodes.Items.Cast<Node>().Select(x=>x.Name).ToArray()}));
        await Work("正在计算…",async ct=>{
            var g=store.Graph;var result=await Task.Run(()=>Routing.Find(g,opts,ct));routeGraph=g;routeOptions=opts;routes=result.Routes;
            routeGrid.DataSource=routes.Select((r,i)=>new{方案=i+1,容量状态=r.CapacityStatus(opts.Demand),已记录米=Math.Round(r.Length,2),未知长度段=r.Unknown,零长度段=r.Zero,跳纤=r.Jumps,路由最小空闲芯数=r.Bottleneck,需求后余量=r.Edges.Length==0?"内部关系未核验":(r.Bottleneck-opts.Demand).ToString()}).ToList();
            UpdateRouteSummary(result);
            status.Text=$"{routes.Count} 个方案 · 扩展 {result.Expansions:N0} · {result.ElapsedMs} ms · "+(result.Cancelled?"已停止，结果不完整":result.Limited?"达到搜索上限，结果不完整，不能保证最优":routes.Count>=opts.Count?"按所选指标从优到次返回；同值顺序不保证":"搜索完成");ShowRoute();if(routes.Any(x=>x.Transfers.Length>0))status.Text+=" · 含集群内部关系，内部可用芯数未单独核验";
        });
    }
    void ShowRoute()
    {
        if(routeGraph is not Graph g||routeOptions is not RouteOptions o)return;int ix=schemeChoice.SelectedIndex;if(ix<0||ix>=routes.Count)return;var r=routes[ix];
        pathView.SetRoute(g,r);
        routeSequence.Text=string.Join(" → ",ExportPlan.FromRoute(g,r,o,ix+1).Sequence);copyRoute.Enabled=routeSequence.Text.Length>0;
        schemeChoice.BackColor=r.Edges.Length>0&&!r.HasCapacity(o.Demand)?Color.FromArgb(255,240,224):Theme.Tint;
        segments.DataSource=r.Edges.Select((i,j)=>{var e=g.Edges[i];var step=r.Steps.ElementAtOrDefault(j);return new{序号=j+1,起端设施=g.Nodes[step?.From??e.A].Name,终端设施=g.Nodes[step?.To??e.B].Name,长度米=e.Length?.ToString("0.##",CultureInfo.InvariantCulture)??"未知",空闲_总芯=$"{e.Free} / {e.Total}",需求后余量=e.Free-o.Demand};}).ToList();
        foreach(DataGridViewColumn c in segments.Columns)c.HeaderText=c.Name=="长度米"?"长度（米）":c.Name.Replace("_"," / ");
    }
    void ClearRoutes(){routeSequence.Clear();copyRoute.Enabled=false;routes=[];routeGraph=null;routeOptions=null;routeGrid.DataSource=null;segments.DataSource=null;pathView.Clear();schemeChoice.Items.Clear();schemeChoice.BackColor=Theme.Tint;routeSummary.Text="候选路由 · 选择起终点后查询";}
    void ExportRoutes(bool currentOnly=false)
    {
        try
        {
            if(routeGraph==null||routeOptions==null||routes.Count==0)throw new InvalidOperationException("无方案可导出");
            string directory=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);
            using var dialog=new SaveFileDialog{FileName="路由方案.xlsx",Filter="Excel工作簿|*.xlsx",DefaultExt="xlsx",AddExtension=true,InitialDirectory=directory};
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;WriteRouteWorkbook(dialog.FileName,currentOnly);
        }
        catch(Exception ex){status.Text="导出失败："+ex.Message;MessageBox.Show(this,ex.Message,"导出失败",MessageBoxButtons.OK,MessageBoxIcon.Error);}
    }
    void WriteRouteWorkbook(string path,bool currentOnly)
    {
        if(routeGraph is not Graph graph||routeOptions is not RouteOptions options||routes.Count==0)throw new InvalidOperationException("无方案可导出");
        var selected=routes.Select((route,i)=>(route,i)).Where(x=>!currentOnly||x.i==schemeChoice.SelectedIndex).Select(x=>ExportPlan.FromRoute(graph,x.route,options,x.i+1)).ToArray();
        RouteWorkbook.Write(path,selected);status.Text=$"已导出 {selected.Length} 条路由：路由总览含跳纤路由，各方案分别列出逐段资源。";
    }
    void BuildResources()=>BuildResourceWorkspace();
    IEnumerable<Edge> FilterResources()
    {
        var g=store.Graph;string term=resourceSearch.Text.Trim(),type=resourceType.Text,endType=resourceEndType.Text,from=resourceStartArea.Text,to=resourceEndArea.Text,business=resourceBusiness.Text;var ids=term.Length>0?SearchRaw(term):null;int minimum=(int)minFree.Value;
        return g.Edges.Where(e=>e.Free>=minimum&&(ids==null||ids.Contains(e.Index))&&TypeMatches(g.Nodes[e.A].Type,type)&&TypeMatches(g.Nodes[e.B].Type,endType)&&(from.Length==0||from=="全部区域"||Area(e.StartRegion)==from)&&(to.Length==0||to=="全部区域"||Area(e.EndRegion)==to)&&(business.Length==0||business=="全部状态"||Area(e.BusinessStatus)==business));
    }
    static bool TypeMatches(string actual,string selected)=>string.IsNullOrEmpty(selected)||selected is "全部" or "全部类型"||actual.Split(';',StringSplitOptions.TrimEntries).Contains(selected);
    HashSet<int> SearchRaw(string term)
    {
        var ids=new HashSet<int>();if(!File.Exists(store.Database))return ids;
        using var c=new Microsoft.Data.Sqlite.SqliteConnection(new Microsoft.Data.Sqlite.SqliteConnectionStringBuilder(){DataSource=store.Database,Pooling=false}.ToString());c.Open();using var q=c.CreateCommand();q.CommandText="SELECT id FROM edges WHERE instr(raw,$s)>0 OR instr(business_id,$s)>0 OR instr(name,$s)>0";q.Parameters.AddWithValue("$s",term);using var r=q.ExecuteReader();while(r.Read())ids.Add(r.GetInt32(0));return ids;
    }
    async void LoadResources()
    {
        if(busy){resourceRefreshPending=true;return;}
        var g=store.Graph;string term=resourceSearch.Text.Trim(),type=resourceType.Text,endType=resourceEndType.Text,from=resourceStartArea.Text,to=resourceEndArea.Text,business=resourceBusiness.Text;int minimum=(int)minFree.Value;ResourceRow[] filtered=[];
        bool ok=await Work("正在筛选资源…",async ct=>{
            filtered=await Task.Run(()=>{var ids=term.Length>0?SearchRaw(term):null;ct.ThrowIfCancellationRequested();return g.Edges.Where(e=>e.Free>=minimum&&(ids==null||ids.Contains(e.Index))&&TypeMatches(g.Nodes[e.A].Type,type)&&TypeMatches(g.Nodes[e.B].Type,endType)&&(from.Length==0||from=="全部区域"||Area(e.StartRegion)==from)&&(to.Length==0||to=="全部区域"||Area(e.EndRegion)==to)&&(business.Length==0||business=="全部状态"||Area(e.BusinessStatus)==business)).Select(e=>new ResourceRow(0,e.Index,g.Nodes[e.A].Name,Area(e.StartRegion),g.Nodes[e.A].Type,g.Nodes[e.B].Name,Area(e.EndRegion),g.Nodes[e.B].Type,e.Length,$"{e.Free} / {e.Total}",e.BusinessStatus,"详情")).ToArray();},ct);
        });if(!ok)return;filtered=Ordered(resourceGrid,filtered);resourcePageSize=resourcePager.PageSize;resourcePager.Set(filtered.Length,(int)page.Value);page.Value=resourcePager.Page;int skip=((int)page.Value-1)*resourcePageSize;
        resourceGrid.DataSource=filtered.Skip(skip).Take(resourcePageSize).Select((r,i)=>r with{序号=skip+i+1}).ToList();
        if(resourceGrid.Columns.Contains("内部序号"))resourceGrid.Columns["内部序号"].Visible=false;foreach(DataGridViewColumn c in resourceGrid.Columns)c.HeaderText=c.Name=="长度米"?"长度（米）":c.Name.Replace("_"," / ");
        if(resourceGrid.Columns.Contains("长度米")){resourceGrid.Columns["长度米"].DefaultCellStyle.Format="0.##";resourceGrid.Columns["长度米"].DefaultCellStyle.NullValue="未知";}
        status.Text=$"本地数据 · 已就绪    |    中继段 {g.Edges.Length:N0} 条    |    资源快照：{g.Version}";
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
            while(reader.Read()){ct.ThrowIfCancellationRequested();if(!wanted.Contains(reader.GetInt32(0)))continue;var row=JsonSerializer.Deserialize<Dictionary<string,string>>(reader.GetString(1))!;var edge=store.Graph.Edges[reader.GetInt32(0)];row=ShantouRegions.ExportFields(row,edge);row["始端类型"]=store.Graph.Nodes[edge.A].Type;row["终端类型"]=store.Graph.Nodes[edge.B].Type;if(hs==null){hs=row.Keys.ToArray();w.WriteLine(Csv.Line(hs));}w.WriteLine(Csv.Line(hs.Select(h=>row.GetValueOrDefault(h,""))));}
            }
            ct.ThrowIfCancellationRequested();File.Move(temp,path,true);
            }finally{if(File.Exists(temp))File.Delete(temp);}

        },ct));if(completed)status.Text="导出完成";
    }
    async Task PreviewTypes()
    {
        if(busy)return;using var file=new OpenFileDialog{Filter=TableInput.Filter};if(file.ShowDialog()!=DialogResult.OK)return;
        using var dialog=new Form{Text="节点类型表 · 格式、工作表及字段映射",Width=680,Height=540,StartPosition=FormStartPosition.CenterParent,Font=Font};
        var panel=Stack();var enc=new UiComboBox{Width=240,DropDownStyle=ComboBoxStyle.DropDownList};enc.Items.AddRange(["自动","UTF-8","GB18030","GBK","utf-16","utf-16BE"]);enc.SelectedIndex=0;
        var sheet=new UiComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var nc=new UiComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var tc=new UiComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};var info=L("");
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
    void BuildImport()=>BuildImportWorkspace();

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
            foreach(string field in new[]{"始端设施","终端设施","空闲数量","中继纤芯数量","ID","名称","长度","始端类型","终端类型"}){var combo=new UiComboBox{Width=240};combo.Items.Add("（无）");combo.Items.AddRange(headers);combo.SelectedItem=headers.Contains(field)?field:"（无）";map[field]=combo;mappings.Controls.Add(MappingRow(field,combo));combo.SelectedIndexChanged+=(_,_)=>UpdateImportPreviewColumns();}
            mappings.Height=map.Count*41+5;foreach(Control rowControl in mappings.Controls)rowControl.Width=Math.Max(140,mappings.ClientSize.Width-5);
            var data=new DataTable();foreach(var h in headers)data.Columns.Add(h);foreach(var r in result.rows.Skip(1)){var row=data.NewRow();for(int i=0;i<Math.Min(r.Length,headers.Length);i++)row[i]=r[i];data.Rows.Add(row);}importGrid.DataSource=data;UpdateImportPreviewColumns();
            detectedEncoding=result.info.Format=="CSV"?result.info.Encoding:enc;previewKey=InputKey();
            previewStats.Text=$"预览 {data.Rows.Count} 条 · {headers.Length} 个字段 · 必需字段未映射 {Store.Required.Count(f=>map[f].Text=="（无）")} 项\n等待全量校验 · 确认后执行；异常隔离。";
            status.Text="预览完成。请核对编码、读取格式、工作表、ID和字段映射后确认导入。";importGrid.Focus();if(importMappingBody!=null)importMappingBody.AutoScrollPosition=Point.Empty;
        });
    }

    async Task ImportData()
    {
        if(map.Count==0||previewKey!=InputKey())throw new InvalidOperationException("请先重新预览当前文件、编码及工作表");foreach(var field in Store.Required)if(map[field].Text=="（无）")throw new InvalidOperationException("请映射必需字段："+field);if(MessageBox.Show("校验成功后替换当前资源；失败或取消保留原版。继续？","确认导入",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        string file=importPath.Text,enc=detectedEncoding;string? sheet=resourceFormat.SelectedIndex==1&&sheets.Items.Count==0?"__RESOURCE_RAW__":sheets.SelectedItem as string;double unit=units.SelectedIndex==0?1:1000;var mapping=map.ToDictionary(x=>x.Key,x=>x.Value.Text);
        await Work("开始导入…",async ct=>{
            var progress=new Progress<ImportProgress>(p=>status.Text=$"读取 {p.Read:N0} · 可接入 {p.Accepted:N0} · 异常 {p.Invalid:N0} · 节点 {p.Nodes:N0}（可取消）");
            var r=await store.ImportAsync(file,enc,mapping,unit,progress,ct,sheet);if(!r.Unchanged)store.SetSetting("remoteResourceVersion","");errorFile=r.ErrorFile;RefreshData();if(r.Unchanged){status.Text="中继段 ID 与内容均无变化，已跳过资源更新。";return;}previewStats.Text=$"校验完成：读取 {r.Read:N0} 条 · 可接入 {r.Accepted:N0} 条 · 隔离异常 {r.Invalid:N0} 条";status.Text=$"完成：新增 {r.Added:N0} · 更新 {r.Changed:N0} · 删除旧 ID {r.Removed:N0}；现有 {r.Accepted:N0} 段。数据版本 {r.Version}";
        });
    }
    void RefreshData()
    {
        UpdateStats();ClearRoutes();RefreshAreaChoices();pendingTypes=[];typePreview.DataSource=null;if(applyTypes!=null)applyTypes.Enabled=false;start?.Clear();end?.Clear();avoid?.Clear();avoidNodes.Items.Clear();var types=store.Graph.Nodes.SelectMany(n=>n.Type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries)).Concat(new[]{"机房","光交箱","分纤箱","未识别"}).Distinct().OrderBy(t=>t=="未识别"?1:0).ThenBy(t=>t).ToArray();
        editType.Items.Clear();editType.Items.AddRange(types);
        var checkedTypes=avoidTypes.CheckedTypes.ToHashSet();avoidTypes.SetTypes(types,checkedTypes);
        buildingFilters=true;foreach(var c in new[]{resourceType,resourceEndType,nodeType}){var old=c.SelectedItem;c.Items.Clear();string all=c==nodeType?"全部":"全部类型";c.Items.Add(all);c.Items.AddRange(types);c.SelectedItem=old!=null&&c.Items.Contains(old)?old:all;}buildingFilters=false;
        var g=store.Graph;resourceStats.Text=$"{g.Edges.Length:N0} 段 / {g.Nodes.Length:N0} 节点 / 已记录 {g.Edges.Sum(e=>e.Length??0):N2} 米 / 空闲逐段合计 {g.Edges.Sum(e=>(long)e.Free):N0} 芯（非路由数） / 版本 {g.Version}";if(initialTablesPending)initialTablesPending=false;else{LoadResources();LoadTypes();LoadClusters();LoadRooms();}
    }
    void RestoreRouteSettings()
    {
        try{if(store.Setting("route") is not string s)return;var r=JsonDocument.Parse(s).RootElement;showingRoute=true;requireCapacity.Checked=!r.TryGetProperty("capacityOnly",out var cap)||cap.GetBoolean();showingRoute=false;demand.Value=Math.Clamp(r.GetProperty("demand").GetInt32(),1,99999);jumps.Value=Math.Clamp(r.GetProperty("jumps").GetInt32(),0,100);count.Value=Math.Clamp(r.GetProperty("count").GetInt32(),1,50);order.SelectedIndex=Math.Clamp(r.GetProperty("order").GetInt32(),0,3);start.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("a").GetString()));end.SelectNode(store.Graph.Nodes.FirstOrDefault(n=>n.Name==r.GetProperty("b").GetString()));var ts=r.GetProperty("types").EnumerateArray().Select(x=>x.GetString()??"").ToHashSet();avoidTypes.SetCheckedTypes(ts);foreach(var name in r.GetProperty("nodes").EnumerateArray()){var node=store.Graph.Nodes.FirstOrDefault(n=>n.Name==name.GetString());if(node!=null)avoidNodes.Items.Add(node);}}catch{/* Older or damaged personal query preferences are optional. */}finally{showingRoute=false;}
    }
    static string? Save(string name) {string dir=Environment.GetFolderPath(Environment.SpecialFolder.MyDocuments);using var d=new SaveFileDialog(){FileName=name,Filter="CSV文件|*.csv",InitialDirectory=dir};return d.ShowDialog()==DialogResult.OK?d.FileName:null;}

}
