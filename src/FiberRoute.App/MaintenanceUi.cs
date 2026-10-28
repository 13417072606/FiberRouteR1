using FiberRoute.Core;
using System.Text.Json;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly DataGridView clusterGrid=Grid(),assignmentGrid=Grid();
    readonly FlowLayoutPanel typePills=Flow();readonly NumericUpDown typePage=Num(1,99999,1);readonly Label typePageLabel=L("");
    readonly TextBox clusterSearch=new(){Width=280};readonly ComboBox stateFilter=new UiComboBox(){Width=115,DropDownStyle=ComboBoxStyle.DropDownList};
    List<TypeAssignment> assignments=[];List<ClusterRecord> pendingClusters=[];List<ClusterRecord> visibleClusters=[];
    List<(string Path,string Type,string? Sheet)> pendingTypeFiles=[];
    readonly Panel previewPanel=new(){Dock=DockStyle.Bottom,Height=240,Visible=false};
    readonly TextBox editName=new(){ReadOnly=true,Width=260,TabStop=false};readonly ComboBox editType=new UiComboBox(){Width=260,DropDownStyle=ComboBoxStyle.DropDown};readonly Label editSource=L(""),editReason=L("");
    string RawStatus(int index){try{var r=JsonSerializer.Deserialize<Dictionary<string,string>>(store.Raw(index));return r?.GetValueOrDefault("业务状态","")??"";}catch{return "";}}
    readonly ComboBox clusterTypeFilter=new UiComboBox{Width=125},clusterRouteFilter=new UiComboBox{Width=125};
    string clusterState="全部";int clusterPageNumber=1;FlowLayoutPanel? clusterStatusChips;
    ClusterRecord? SelectedCluster()=>clusterGrid.CurrentRow?.Cells["内部标识"].Value is string id?visibleClusters.FirstOrDefault(c=>c.Id==id):null;
    void BuildTypes()
    {
        var tab=Tab("节点类型");var inner=new TabControl{Dock=DockStyle.Fill,Appearance=TabAppearance.FlatButtons,ItemSize=new Size(1,1),SizeMode=TabSizeMode.Fixed};var nodeTab=new TabPage("节点类型"){Padding=Padding.Empty,BackColor=Theme.Canvas};var groupTab=new TabPage("集群管理"){Padding=new Padding(12),BackColor=Color.White};inner.TabPages.Add(nodeTab);inner.TabPages.Add(groupTab);BuildRoomManagement(inner);
        var nav=new FlowLayoutPanel{Dock=DockStyle.Top,Height=38,BackColor=Theme.Canvas,Padding=Padding.Empty};for(int i=0;i<inner.TabPages.Count;i++){int at=i;var button=new UiButton{Text=inner.TabPages[i].Text,Navigation=true,Selected=i==0,Size=new Size(112,34),Margin=new Padding(0,0,8,0)};button.Click+=(_,_)=>inner.SelectedIndex=at;inner.SelectedIndexChanged+=(_,_)=>{button.Selected=inner.SelectedIndex==at;button.Invalidate();};nav.Controls.Add(button);}tab.Controls.Add(inner);tab.Controls.Add(nav);tab.Controls.Add(PageIntro("节点类型管理","维护节点类型、集群与机房归属"));
        stateFilter.Items.AddRange(["全部状态","待确认","已确认","需核对"]);stateFilter.SelectedIndex=0;
        var search=Flow();search.BackColor=Color.White;search.Padding=new Padding(0,0,0,5);typeSearch.PlaceholderText="搜索节点名称或地址关键词";var searchFrame=InputFrame(typeSearch);searchFrame.Width=240;search.Controls.AddRange([searchFrame,nodeArea,stateFilter,B("重置",()=>{buildingFilters=true;typeSearch.Clear();nodeArea.SelectedIndex=stateFilter.SelectedIndex=0;nodeType.SelectedItem="全部";buildingFilters=false;typePage.Value=1;LoadTypes();}),B("导出节点类型",ExportNodeTypes)]);
        var editorBody=Stack();editorBody.Padding=new Padding(2);editName.Multiline=true;editName.Height=64;editName.BorderStyle=BorderStyle.None;editName.BackColor=Color.White;editReason.MaximumSize=editSource.MaximumSize=new Size(340,0);
        editorBody.Controls.AddRange([Title("节点详情",13),editName,L("节点类型（英文 ; 分隔多标签）"),InputFrame(editType),editSource,editReason]);
        var help=new StatusLegend{Dock=DockStyle.Bottom,Height=150};var editor=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};editor.Controls.Add(editorBody);var editActions=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=80,ColumnCount=1,RowCount=2,BackColor=Color.White};var saveType=AsyncButton("保存修改并确认",SaveNodeType,primary:true);saveType.Dock=DockStyle.Fill;var importType=AsyncButton("导入节点类型表",ImportTypeTables,"upload");importType.Dock=DockStyle.Fill;editActions.Controls.Add(saveType,0,0);editActions.Controls.Add(importType,0,1);editActions.RowStyles.Add(new(SizeType.Percent,50));editActions.RowStyles.Add(new(SizeType.Percent,50));editor.Controls.Add(editActions);editor.Controls.Add(help);
        typeGrid.SelectionChanged+=(_,_)=>LoadSelectedNode();typeGrid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&typeGrid.Columns[e.ColumnIndex].Name=="操作")editType.Focus();};typeGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)editType.Focus();};
        var list=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};list.Controls.Add(typeGrid);ConfigurePager(typePager,"types",()=>{typePage.Value=typePager.Page;LoadTypes();});Sortable(typeGrid,()=>{typePage.Value=1;LoadTypes();});list.Controls.Add(typePager);list.Controls.Add(search);list.Controls.Add(new Label{Text="节点清单",Dock=DockStyle.Top,Height=32,Font=new Font(Font.FontFamily,12,FontStyle.Bold),BackColor=Color.White});
        nodeTab.Controls.Add(Columns(74,list,26,editor));nodeTab.Controls.Add(previewPanel);typePills.Padding=new Padding(0,2,0,6);typePills.BackColor=Theme.Canvas;nodeTab.Controls.Add(typePills);
        previewPanel.Controls.Add(assignmentGrid);var buttons=Flow();buttons.Controls.AddRange([B("确认应用类型表",async()=>{if(busy)return;var values=assignments.ToArray();var files=pendingTypeFiles.ToArray();bool ok=await Work("保存类型、区域来源与机房归属…",ct=>Task.Run(()=>{store.ApplyTypeAssignments(values,ct);store.ApplyRoomFiles(files,ct);},ct));if(ok){previewPanel.Visible=false;RefreshData();}}),B("取消预览",()=>{assignments=[];pendingTypeFiles=[];previewPanel.Visible=false;}),B("导出匹配报告",()=>{var p=Save("类型匹配报告.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"设施名称","类型","确认状态","原因","来源"}}.Concat(assignments.Select(x=>new object?[]{x.Name,x.Type,x.Status,x.Reason,x.Source})));})]);previewPanel.Controls.Add(buttons);
        clusterRouteFilter.Items.AddRange(["全部路由状态","已启用","已暂停"]);clusterRouteFilter.SelectedIndex=0;
        var groupTop=Flow();groupTop.Controls.AddRange([InputFrame(clusterSearch),clusterTypeFilter,clusterRouteFilter,B("重置",()=>{buildingFilters=true;clusterSearch.Clear();clusterTypeFilter.SelectedIndex=clusterRouteFilter.SelectedIndex=0;buildingFilters=false;clusterState="全部";clusterPageNumber=1;LoadClusters();}),B("导入集群表",ImportClusterTable),B("新建集群",()=>EditCluster(null)),B("修改集群",()=>{if(SelectedCluster() is {} c)EditCluster(c);}),B("删除集群",()=>{if(busy)return;if(SelectedCluster() is {} c&&MessageBox.Show("删除选中集群关系？原资源不删除。","确认",MessageBoxButtons.YesNo)==DialogResult.Yes){store.RemoveCluster(c.Id);RefreshData();}}),B("导出筛选结果",()=>{var p=Save("集群关系.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"设备名称","集群类型","集群列表","确认状态","原因","启用"}}.Concat(visibleClusters.Select(c=>new object?[]{c.Name,c.Type,string.Join(',',c.Members),c.Status,c.Reason,c.Enabled})));})]);groupTop.Controls[0].Width=260;
        clusterGrid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;clusterGrid.CellPainting+=(_,e)=>{if(e.RowIndex<0&&e.ColumnIndex>=0)PaintTableHeader(clusterGrid,e);};
        clusterStatusChips=StatusChips(()=>clusterState,value=>{clusterState=value;clusterPageNumber=1;LoadClusters();});groupTab.Controls.Add(clusterGrid);ConfigurePager(clusterPager,"clusters",()=>{clusterPageNumber=clusterPager.Page;LoadClusters();});groupTab.Controls.Add(clusterPager);groupTab.Controls.Add(groupTop);groupTab.Controls.Add(clusterStatusChips);Sortable(clusterGrid,()=>{clusterPageNumber=1;LoadClusters();});
        clusterGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0&&SelectedCluster() is {} c)EditCluster(c);};clusterGrid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&clusterGrid.Columns[e.ColumnIndex].Name=="操作"&&SelectedCluster() is {} c)EditCluster(c);};
        var t=new System.Windows.Forms.Timer{Interval=300};typeSearch.TextChanged+=(_,_)=>{t.Stop();t.Start();};t.Tick+=(_,_)=>{t.Stop();if(!buildingFilters){typePage.Value=1;LoadTypes();}};FormClosed+=(_,_)=>t.Dispose();stateFilter.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters){typePage.Value=1;LoadTypes();}};nodeArea.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters){typePage.Value=1;LoadTypes();}};
        var clusterTimer=new System.Windows.Forms.Timer{Interval=300};clusterSearch.TextChanged+=(_,_)=>{clusterTimer.Stop();clusterTimer.Start();};clusterTimer.Tick+=(_,_)=>{clusterTimer.Stop();if(!buildingFilters){clusterPageNumber=1;LoadClusters();}};FormClosed+=(_,_)=>clusterTimer.Dispose();foreach(var filter in new[]{clusterTypeFilter,clusterRouteFilter})filter.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters){clusterPageNumber=1;LoadClusters();}};
    }
    static string NodeSource(string source)=>source.Contains("人工")?"人工确认":source.Contains("表")?"类型表匹配":source.Length==0?"软件识别":source;
    void LoadSelectedNode()
    {
        if(typeGrid.CurrentRow?.Cells["名称"].Value is not string name)return;var n=store.Graph.Nodes.FirstOrDefault(n=>n.Name==name);if(n==null)return;editName.Text=n.Name;editType.Text=n.Type;editSource.Text="来源："+n.Source;string? room=store.Graph.Rooms.FirstOrDefault(x=>x.Value.Contains(n.Id)).Key;editReason.Text="区域："+Area(n.Region)+"\n确认状态："+n.Confirmation+(n.Reason.Length>0?"\n"+n.Reason:"")+(room!=null?"\n所属机房："+room:"")+"\n\n"+ProvenanceText(n.Name);
    }
    async Task SaveNodeType()
    {
        if(busy||editName.Text.Length==0)return;if(string.IsNullOrWhiteSpace(editType.Text))throw new InvalidDataException("类型不能为空");string name=editName.Text,type=string.Join(';',editType.Text.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(Store.NormalizeType).Distinct());await Work("保存节点类型…",ct=>Task.Run(()=>store.ApplyTypeAssignments([new(name,type,"人工修改","已确认","")],ct),ct));RefreshData();
    }
    void ExportNodeTypes(){var p=Save("节点类型.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"节点名称","区域","节点类型","确认状态","来源","原因"}}.Concat(FilteredNodes().Select(n=>new object?[]{n.Name,n.Region,n.Type,n.Confirmation,n.Source,n.Reason})));}
    Graph? pillGraph;string pillChoice="";
    IEnumerable<Node> FilteredNodes()
    {
        string chosen=nodeType.SelectedItem as string??"全部",state=stateFilter.Text,area=nodeArea.Text;
        return store.Graph.Nodes.Where(n=>n.Name.Contains(typeSearch.Text.Trim(),StringComparison.OrdinalIgnoreCase)&&(chosen=="全部"||n.Type.Split(';').Contains(chosen))&&(state=="全部状态"||state.Length==0||n.Confirmation==state)&&(area.Length==0||area=="全部区域"||Area(n.Region)==area));
    }
    void LoadTypes()
    {
        string chosen=nodeType.SelectedItem as string??"全部";var rows=Ordered(typeGrid,FilteredNodes().Select(n=>new TypeRow(n.Name,Area(n.Region),n.Type,n.Confirmation,NodeSource(n.Source),n.Reason,"修改")));typePager.Set(rows.Length,(int)typePage.Value);typePage.Value=typePager.Page;
        typeGrid.DataSource=rows.Skip(((int)typePage.Value-1)*typePager.PageSize).Take(typePager.PageSize).ToList();
        if(ReferenceEquals(pillGraph,store.Graph)&&pillChoice==chosen)return;pillGraph=store.Graph;pillChoice=chosen;var totals=store.Graph.Nodes.SelectMany(n=>n.Type.Split(';').Distinct()).GroupBy(x=>x).ToDictionary(g=>g.Key,g=>g.Count());
        foreach(Control old in typePills.Controls.Cast<Control>().ToArray())old.Dispose();typePills.Controls.Clear();foreach(string type in new[]{"全部","光交箱","机房","分纤箱","站点","光终端盒","多媒体箱","未识别"}.Concat(store.Graph.Nodes.SelectMany(n=>n.Type.Split(';'))).Distinct())
        {int num=type=="全部"?store.Graph.Nodes.Length:totals.GetValueOrDefault(type);var b=(UiButton)B($"{type}  {num:N0}",()=>{nodeType.SelectedItem=type;typePage.Value=1;LoadTypes();});b.Primary=false;b.Pill=true;b.Height=34;b.MinimumSize=new Size(76,34);b.Margin=new Padding(0,2,7,3);b.Selected=chosen==type;typePills.Controls.Add(b);}
    }
    async Task ImportTypeTables()
    {
        if(busy)return;using var files=new OpenFileDialog{Filter=TableInput.Filter,Multiselect=true};if(files.ShowDialog()!=DialogResult.OK)return;var selected=new List<(string Path,string Type,string? Sheet)>();
        foreach(string file in files.FileNames)
        {
            using var d=new Form{Text="类型表字段/类别 · "+Path.GetFileName(file),Width=680,Height=340,Font=Font};var p=Stack();var type=new UiComboBox{Width=250,DropDownStyle=ComboBoxStyle.DropDown};type.Items.AddRange(["（读取表内节点类型列）","光交箱","机房","分纤箱","光终端盒","多媒体箱","站点"]);type.Text=Path.GetFileName(file) switch{var n when n.Contains("ODF",StringComparison.OrdinalIgnoreCase)=>"机房",var n when n.Contains("光交")=>"光交箱",var n when n.Contains("分纤")=>"分纤箱",var n when n.Contains("终端盒")=>"光终端盒",var n when n.Contains("多媒体")=>"多媒体箱",_=>"（读取表内节点类型列）"};
            InputInfo? info=null;bool ok=await Work("识别类型文件…",async ct=>{info=await Task.Run(()=>TableInput.Inspect(file,"自动",ct),ct);});if(!ok)return;var sh=new UiComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};sh.Items.AddRange(info!.Sheets);if(sh.Items.Count>0)sh.SelectedIndex=0;p.Controls.AddRange([L("设施名称自动匹配；其他辅助字段的重复表头不影响匹配。"),L("本文件对应节点类型"),type,L("工作表（CSV无需选择）"),sh,B("继续生成预览",()=>d.DialogResult=DialogResult.OK)]);d.Controls.Add(p);if(d.ShowDialog(this)!=DialogResult.OK)return;selected.Add((file,type.Text.StartsWith('（')?"":type.Text,sh.SelectedItem as string));
        }
        bool done=await Work("读取类型表并匹配…",async ct=>{assignments=await Task.Run(()=>store.PreviewTypeFiles(selected,ct),ct);});if(done){pendingTypeFiles=selected;var known=store.Graph.Nodes.Select(n=>n.Name).ToHashSet();assignmentGrid.DataSource=assignments.Take(1000).Select(x=>new{设施=x.Name,类型=x.Type,确认状态=x.Status,原因=x.Reason,当前资源匹配=known.Contains(x.Name)?"匹配":"待资源匹配"}).ToList();previewPanel.Visible=true;status.Text=$"类型预览 {assignments.Count:N0} 项（显示前1000），需核对 {assignments.Count(x=>x.Status=="需核对"):N0}；确认后保存，同时读取明确的所属机房字段。";}
    }
    void LoadClusters()
    {
        var all=store.Clusters().Select(c=>store.Graph.GroupWarnings.TryGetValue(c.Id,out var reason)?c with{Enabled=false,Status="需核对",Reason=string.Join('；',new[]{c.Reason,reason}.Where(x=>x.Length>0))}:c).ToArray();
        string type=clusterTypeFilter.Text,route=clusterRouteFilter.Text,term=clusterSearch.Text.Trim();
        if(clusterTypeFilter.Items.Count==0){buildingFilters=true;FillChoices(clusterTypeFilter,all.Select(c=>c.Type),"全部类型");buildingFilters=false;type=clusterTypeFilter.Text;}
        visibleClusters=all.Where(c=>(clusterState=="全部"||c.Status==clusterState)&&(type.Length==0||type=="全部类型"||c.Type==type)&&(route=="全部路由状态"||route.Length==0||c.Enabled==(route=="已启用"))&&(c.Name.Contains(term,StringComparison.OrdinalIgnoreCase)||c.Members.Any(n=>n.Contains(term,StringComparison.OrdinalIgnoreCase)))).ToList();
        var rows=Ordered(clusterGrid,visibleClusters.Select(c=>new ClusterRow(0,c.Id,c.Name,c.Members.Length,c.Type,c.Status,c.Enabled?"已启用":"已暂停",c.Reason,string.Join("；",c.Members),"复核")));clusterPager.Set(rows.Length,clusterPageNumber);clusterPageNumber=clusterPager.Page;int skip=(clusterPageNumber-1)*clusterPager.PageSize;
        clusterGrid.DataSource=rows.Skip(skip).Take(clusterPager.PageSize).Select((r,i)=>r with{序号=skip+i+1}).ToList();if(clusterGrid.Columns.Contains("内部标识"))clusterGrid.Columns["内部标识"].Visible=false;UpdateStatusCounts(clusterStatusChips,all.Select(c=>c.Status),clusterState);
    }
    async Task ImportClusterTable()
    {
        if(busy)return;using var file=new OpenFileDialog{Filter=TableInput.Filter};if(file.ShowDialog()!=DialogResult.OK)return;string? sheet=null;InputInfo? info=null;if(!await Work("识别集群文件…",async ct=>{info=await Task.Run(()=>TableInput.Inspect(file.FileName,"自动",ct),ct);}))return;
        if(info!.Sheets.Length>1){using var d=new Form{Text="选择集群工作表",Width=440,Height=200,Font=Font};var list=new UiComboBox{Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList};list.Items.AddRange(info.Sheets);list.SelectedIndex=0;d.Controls.Add(list);var ok=B("确定",()=>d.DialogResult=DialogResult.OK);ok.Dock=DockStyle.Bottom;d.Controls.Add(ok);if(d.ShowDialog(this)!=DialogResult.OK)return;sheet=list.Text;}
        if(!await Work("分析集群成员与异常…",async ct=>{pendingClusters=await Task.Run(()=>store.PreviewClusters(file.FileName,sheet,ct),ct);}))return;
        var replaceMode=new CheckBox{Text="全量替换表格关系（保留人工维护）",Checked=true,AutoSize=true};
        using var preview=new Form{Text="集群导入预览 · 确认后保存",Width=1100,Height=650,Font=Font};var grid=Grid();grid.DataSource=pendingClusters.Select(c=>new{集群=c.Name,成员数=c.Members.Length,确认状态=c.Status,原因=c.Reason,启用=c.Enabled,成员=string.Join("；",c.Members)}).ToList();grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.Fill;preview.Controls.Add(grid);var controls=Flow();controls.Controls.AddRange([replaceMode,L($"归并后{pendingClusters.Count}项，需核对{pendingClusters.Count(c=>c.Status=="需核对")}项；异常关系不参与合并。"),B("确认导入",()=>preview.DialogResult=DialogResult.OK),B("取消",()=>preview.DialogResult=DialogResult.Cancel)]);preview.Controls.Add(controls);if(preview.ShowDialog(this)!=DialogResult.OK)return;
        if(await Work("保存集群关系…",ct=>Task.Run(()=>store.SaveClusters(pendingClusters,replaceMode.Checked,ct),ct)))RefreshData();
    }
    void EditCluster(ClusterRecord? old)
    {
        if(busy)return;using var d=new Form{Text=old==null?"新建集群":"修改集群",Width=700,Height=660,Font=Font};var p=Stack();var name=new TextBox{Text=old?.Name??""};var type=new TextBox{Text=old?.Type??"光交箱"};var members=new TextBox{Multiline=true,Height=230,ScrollBars=ScrollBars.Vertical,Text=string.Join(Environment.NewLine,old?.Members??[])};var enabled=new CheckBox{Text="启用该集群参与路由",Checked=old?.Enabled??true,Height=32};p.Controls.AddRange([L("来源："+(old?.Source??"人工新建")+"\n核对原因："+(old?.Reason??"")),L("集群名称"),name,L("类型"),type,L("成员完整名称，每行一个；仅维护有明确连通关系的设备"),members,enabled,B("人工确认并保存",async()=>{
            string[] ns=members.Lines.Select(x=>x.Trim()).Where(x=>x.Length>0).Distinct().Order(StringComparer.Ordinal).ToArray();if(string.IsNullOrWhiteSpace(name.Text)||ns.Length<2)throw new InvalidDataException("请输入集群名称及至少两个成员");string id=old?.Id??Guid.NewGuid().ToString("N");var c=new ClusterRecord(id,name.Text.Trim(),Store.NormalizeType(type.Text),ns,"人工修改","已确认","",enabled.Checked);if(await Work("保存集群…",ct=>Task.Run(()=>store.SaveClusters([c],ct:ct),ct))){d.DialogResult=DialogResult.OK;RefreshData();}})]);d.Controls.Add(p);d.ShowDialog(this);
    }
}
