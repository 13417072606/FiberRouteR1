using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly DataGridView roomGrid=Grid();readonly TextBox roomSearch=new(){Width=300,PlaceholderText="搜索机房或成员设施"};
    List<IGrouping<string,RoomAssignment>> visibleRooms=[];TabControl? maintenanceTabs;readonly NumericUpDown roomPage=Num(1,99999,1);readonly Label roomPageInfo=L("");
    readonly ComboBox roomArea=new UiComboBox{Width=140},roomRouteFilter=new UiComboBox{Width=140};
    readonly Panel roomDetails=new(){Dock=DockStyle.Fill,AutoScroll=true,BackColor=Theme.Canvas,Padding=new Padding(4)};
    string roomState="全部";FlowLayoutPanel? roomStatusChips;Graph? cachedRoomGraph;List<RoomAssignment> cachedRoomData=[];
    void RefreshRoomAreaChoices()=>FillChoices(roomArea,store.Graph.Nodes.Select(n=>Area(n.Region)),"全部区域");
    void BuildRoomManagement(TabControl inner)
    {
        maintenanceTabs=inner;var tab=new TabPage("机房归属"){Padding=new Padding(12),BackColor=Theme.Canvas};inner.TabPages.Add(tab);
        var left=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};var right=Card("归属详情与来源",roomDetails);
        var actionButtons=Flow();actionButtons.Dock=DockStyle.Bottom;actionButtons.BackColor=Color.White;actionButtons.Controls.AddRange([B("查看来源",ShowRoomEvidence),B("查看成员",ShowRoomMembers)]);right.Controls.Add(actionButtons);
        roomRouteFilter.Items.AddRange(["全部归并状态","已纳入","部分纳入","已暂停"]);roomRouteFilter.SelectedIndex=0;
        var actions=Flow();actions.BackColor=Color.White;roomSearch.Width=220;var frame=InputFrame(roomSearch);frame.Width=230;actions.Controls.AddRange([frame,roomArea,roomRouteFilter,B("重置",()=>{buildingFilters=true;roomSearch.Clear();roomArea.SelectedIndex=roomRouteFilter.SelectedIndex=0;buildingFilters=false;roomState="全部";roomPage.Value=1;LoadRooms();}),B("导入设施清单表",ImportRoomTables),B("导入机房清单表",ImportRoomCatalog),B("导出筛选清单",()=>{var p=Save("机房归属.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"设施名称","所属机房","确认状态","参与机房整体路由","来源","原因"}}.Concat(visibleRooms.SelectMany(g=>g).Select(x=>new object?[]{x.Name,x.Room,x.Status,x.Enabled,x.Source,x.Reason})));})]);
        roomStatusChips=StatusChips(()=>roomState,value=>{roomState=value;roomPage.Value=1;LoadRooms();});left.Controls.Add(roomGrid);ConfigurePager(roomPager,"rooms",()=>{roomPage.Value=roomPager.Page;LoadRooms();});left.Controls.Add(roomPager);left.Controls.Add(actions);left.Controls.Add(roomStatusChips);tab.Controls.Add(Columns(76,left,24,right,"split-v5-room-affiliation"));
        tab.Controls.Add(new Label{Text="节点类型表含所属机房列时会同步导入归属；仅需单独更新归属时使用下方按钮。机房清单表用于核验机房名称，未命中项不参与归属。",Dock=DockStyle.Top,Height=38,ForeColor=Theme.Muted,Padding=new Padding(6)});
        Sortable(roomGrid,()=>{roomPage.Value=1;LoadRooms();});roomGrid.SelectionChanged+=(_,_)=>LoadRoomDetails();roomGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)ShowRoomMembers();};roomGrid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&roomGrid.Columns[e.ColumnIndex].Name=="操作")ShowRoomMembers();};
        var timer=new System.Windows.Forms.Timer{Interval=300};roomSearch.TextChanged+=(_,_)=>{timer.Stop();timer.Start();};timer.Tick+=(_,_)=>{timer.Stop();if(!buildingFilters){roomPage.Value=1;LoadRooms();}};FormClosed+=(_,_)=>timer.Dispose();foreach(var box in new[]{roomArea,roomRouteFilter})box.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters){roomPage.Value=1;LoadRooms();}};
    }
    void LoadRooms()
    {
        if(maintenanceTabs==null)return;if(!ReferenceEquals(cachedRoomGraph,store.Graph)){cachedRoomGraph=store.Graph;cachedRoomData=store.RoomAssignments();}
        string term=roomSearch.Text.Trim(),area=roomArea.Text,route=roomRouteFilter.Text;var regions=store.Graph.Nodes.ToDictionary(n=>n.Name,n=>Area(n.Region),StringComparer.Ordinal);var all=cachedRoomData.GroupBy(x=>x.Room,StringComparer.Ordinal).ToArray();
        string State(IGrouping<string,RoomAssignment> g)=>g.Any(x=>x.Status=="需核对")?"需核对":g.All(x=>x.Status=="已确认")?"已确认":"待确认";
        string Routing(IGrouping<string,RoomAssignment> g)=>g.All(x=>x.Enabled)?"已纳入":g.Any(x=>x.Enabled)?"部分纳入":"已暂停";
        visibleRooms=all.Where(g=>(term.Length==0||g.Key.Contains(term,StringComparison.OrdinalIgnoreCase)||g.Any(x=>x.Name.Contains(term,StringComparison.OrdinalIgnoreCase)))&&(roomState=="全部"||State(g)==roomState)&&(area.Length==0||area=="全部区域"||g.Any(x=>regions.GetValueOrDefault(x.Name,"未提供")==area))&&(route.Length==0||route=="全部归并状态"||Routing(g)==route)).OrderBy(g=>g.Key,StringComparer.CurrentCulture).ToList();
        var rows=Ordered(roomGrid,visibleRooms.Select(g=>new RoomRow(0,g.Key,g.Count(),g.Count(x=>x.Enabled),State(g),Routing(g),"查看成员")));roomPager.Set(rows.Length,(int)roomPage.Value);roomPage.Value=roomPager.Page;int skip=(roomPager.Page-1)*roomPager.PageSize;
        roomGrid.DataSource=rows.Skip(skip).Take(roomPager.PageSize).Select((r,i)=>r with{序号=skip+i+1}).ToList();if(roomGrid.Columns.Contains("所属机房"))roomGrid.Columns["所属机房"].HeaderText="所属机房原值";UpdateStatusCounts(roomStatusChips,all.Select(State),roomState);LoadRoomDetails();
    }
    IGrouping<string,RoomAssignment>? SelectedRoom()=>roomGrid.CurrentRow?.Cells["所属机房"].Value is string name?visibleRooms.FirstOrDefault(g=>g.Key==name):null;
    void LoadRoomDetails()
    {
        foreach(Control old in roomDetails.Controls.Cast<Control>().ToArray())old.Dispose();roomDetails.Controls.Clear();
        if(SelectedRoom() is not {} group)return;var member=group.First();string region=store.Graph.Nodes.FirstOrDefault(n=>n.Name==member.Name)?.Region??"";
        void Add(string title,string value,int height)
        {
            var card=new Surface{Dock=DockStyle.Top,Height=height,Padding=new Padding(12),BackColor=Color.White};
            var heading=new Label{Text=title,Dock=DockStyle.Top,Height=28,ForeColor=Theme.Muted,Font=new Font(Font,FontStyle.Bold)};
            var body=new TextBox{Text=value,Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,WordWrap=true,ScrollBars=ScrollBars.Vertical,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink};card.Controls.Add(body);card.Controls.Add(heading);roomDetails.Controls.Add(card);
            roomDetails.Controls.Add(new Panel{Dock=DockStyle.Top,Height=8,BackColor=Theme.Canvas});
        }
        Add("核对原因",member.Reason.Length>0?member.Reason:"无",90);
        Add("来源依据",ProvenanceText(member.Name,member.Room),130);
        Add("区域与归并状态",Area(region)+"  ·  "+member.Status+"  ·  "+(member.Enabled?"已纳入":"已暂停"),90);
        Add("当前设施",member.Name+"\r\n共 "+group.Count()+" 项设施归属同名机房",95);
        Add("机房名称",group.Key,88);
    }
    void ShowRoomEvidence()
    {
        if(SelectedRoom() is not {} group)return;using var dialog=new Form{Icon=Icon,Text="归属来源 · "+group.Key,Width=780,Height=600,Font=Font,StartPosition=FormStartPosition.CenterParent,BackColor=Theme.Canvas};var cards=Stack();cards.BackColor=Theme.Canvas;cards.Controls.Add(DialogTextCard("机房名称",group.Key,82));foreach(var member in group)cards.Controls.Add(DialogTextCard(member.Name,ProvenanceText(member.Name,member.Room),132));dialog.Controls.Add(cards);dialog.ShowDialog(this);
    }
    void ShowRoomMembers()
    {
        if(SelectedRoom() is not {} selectedGroup)return;string room=selectedGroup.Key;
        using var d=new Form{Icon=Icon,Text="机房成员 · "+room,Width=1100,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var grid=Grid();PrepareGrid(grid);List<RoomAssignment> rows=[];
        void Load(){rows=store.RoomAssignments().Where(x=>x.Room==room||x.Room.Split('；').Contains(room)).ToList();grid.DataSource=rows.Select((x,i)=>new{序号=i+1,设施名称=x.Name,所属机房=x.Room,确认状态=x.Status,参与整体路由=x.Enabled?"已纳入":"已暂停",原因=x.Reason,来源=x.Source}).ToList();grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.None;foreach(DataGridViewColumn col in grid.Columns)col.Width=(int)((col.Name is "设施名称" or "所属机房" or "来源" or "原因"?260:85)*grid.DeviceDpi/96f);grid.AutoResizeColumnHeadersHeight();QueueRowMeasure(grid);}
        async Task Set(bool enable)
        {
            if(busy||grid.CurrentRow?.Cells["序号"].Value is not int n)return;var row=rows[n-1];var rooms=row.Room.Split('；');string selected=rooms.Length==1?row.Room:room;
            if(!rooms.Contains(selected))throw new InvalidDataException("请先更正源表中的冲突机房归属后重新导入。");
            string title=row.IsCabinet?"光交箱归入机房提示":"确认机房归属";string msg=$"设施：{row.Name}\n所属机房：{selected}\n\n"+(enable?"确认将该设施纳入此机房整体进行路由分析？表格归属不能验证现场接续与容量，真实中继段仍逐段校验。":"暂停该设施参与机房整体路由？实际设施和中继段均保留。");
            if(MessageBox.Show(d,msg,title,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
            if(await Work("保存机房归属确认…",ct=>Task.Run(()=>store.ConfirmRoom(row.Name,selected,enable),ct))){Load();RefreshData();}
        }
        d.Controls.Add(grid);var controls=Flow();controls.Controls.AddRange([B("确认选中设施并纳入机房",async()=>await Set(true)),B("暂停选中设施归并",async()=>await Set(false)),B("关闭",()=>d.Close())]);d.Controls.Add(controls);var head=DialogTextCard("机房成员",room+"  ·  选择一条设施后确认归并状态",82);head.Dock=DockStyle.Top;d.Controls.Add(head);Load();d.ShowDialog(this);
    }
    async Task ImportRoomTables()
    {
        if(busy)return;using var f=new OpenFileDialog{Filter=TableInput.Filter,Multiselect=true,Title="选择含设施名称和机房名称/所属机房字段的表格"};if(f.ShowDialog()!=DialogResult.OK)return;
        var selected=new List<(string Path,string Type,string? Sheet)>();
        foreach(string path in f.FileNames)
        {
            using var dialog=new Form{Icon=Icon,Text="机房设施表类别 · "+Path.GetFileName(path),Width=540,Height=230,Font=Font};var stack=Stack();
            var kind=new UiComboBox{Width=260,DropDownStyle=ComboBoxStyle.DropDownList};kind.Items.AddRange(["ODF","光交接箱","光分纤箱","光终端盒","多媒体箱"]);
            string filename=Path.GetFileName(path);kind.SelectedItem=filename.Contains("ODF",StringComparison.OrdinalIgnoreCase)?"ODF":filename.Contains("光交")?"光交接箱":filename.Contains("分纤")?"光分纤箱":filename.Contains("终端")?"光终端盒":filename.Contains("多媒体")?"多媒体箱":null;
            InputInfo? info=null;if(!await Work("识别设施表…",async ct=>info=await Task.Run(()=>TableInput.Inspect(path,"自动",ct),ct)))return;
            var sheet=new UiComboBox{Width=300,DropDownStyle=ComboBoxStyle.DropDownList};sheet.Items.AddRange(info!.Sheets);if(sheet.Items.Count>0)sheet.SelectedIndex=0;
            stack.Controls.AddRange([L("选择设施表类别以确定跨表 ID 的优先级"),kind,L("工作表"),sheet,B("确定",()=>{if(kind.SelectedItem==null)throw new InvalidDataException("请选择设施表类别");dialog.DialogResult=DialogResult.OK;})]);dialog.Controls.Add(stack);
            if(dialog.ShowDialog(this)!=DialogResult.OK)return;selected.Add((path,(string)kind.SelectedItem!,sheet.SelectedItem as string));
        }
        var files=selected.ToArray();List<RoomAssignment> values=[];long removed=0;if(!await Work("读取明确的所属机房字段…",async ct=>{values=await Task.Run(()=>store.PreviewRoomFiles(files,ct),ct);removed=await Task.Run(()=>store.CountRoomRemovals(files,ct),ct);}))return;
        if(MessageBox.Show($"本次文件含 {values.Count:N0} 条有效机房归属，其中 {values.Count(x=>x.Status=="需核对"):N0} 条需核对；将清除本次设施类别中新表未列出的 {removed:N0} 个旧 ID。相同 ID 内容不变则跳过，内容变化则更新；其他设施类别不受影响。继续？","导入机房归属",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        if(await Work("更新机房归属…",ct=>Task.Run(()=>store.ApplyRoomFiles(files,ct),ct))){RefreshData();}
    }
    async Task ImportRoomCatalog()
    {
        if(busy)return;
        using var dialog=new OpenFileDialog{Filter=TableInput.Filter,Multiselect=true,Title="选择完整的机房清单表（可同时选择多个分卷）"};
        if(dialog.ShowDialog(this)!=DialogResult.OK)return;
        var files=dialog.FileNames.Select(path=>(Path:path,Sheet:(string?)null)).ToArray();RoomCatalogPreview? preview=null;
        if(!await Work("比对机房清单表与现有归属…",ct=>Task.Run(()=>preview=store.PreviewRoomCatalog(files,ct),ct)))return;
        using var confirm=new Form{Icon=Icon,Text="机房清单表匹配预览",ClientSize=new Size(650,240),MinimumSize=new Size(560,220),StartPosition=FormStartPosition.CenterParent,Font=Font,BackColor=Theme.Canvas};
        var summary=DialogTextCard("匹配结果",$"机房清单 {preview!.UniqueRooms:N0} 个名称 · 设施清单 {preview.CurrentRooms:N0} 个名称\r\n匹配 {preview.MatchedRooms:N0} · 未命中 {preview.UnmatchedRooms:N0}\r\n未命中机房清单项的设施暂不参与归属；但设施和中继段仍会保留。",138);summary.Dock=DockStyle.Fill;
        var actions=Flow();actions.Dock=DockStyle.Bottom;actions.AutoSize=false;actions.Height=58;actions.Padding=new Padding(16,6,12,4);
        actions.Controls.AddRange([B("导出未命中清单",()=>{var path=Save("未命中清单.csv");if(path!=null)Csv.Write(path,new[]{new object?[]{"设施ID","设施名称","未命中机房名称","设施类别","来源文件","来源行"}}.Concat(preview.Misses.Select(x=>new object?[]{x.FacilityId,x.FacilityName,x.Room,x.Kind,x.Source,x.SourceRow})));}),B("确认导入",()=>confirm.DialogResult=DialogResult.OK),B("取消",()=>confirm.DialogResult=DialogResult.Cancel)]);
        confirm.Controls.Add(summary);confirm.Controls.Add(actions);
        if(confirm.ShowDialog(this)!=DialogResult.OK)return;
        if(await Work("保存机房清单表…",ct=>Task.Run(()=>store.ApplyRoomCatalog(files,ct),ct)))RefreshData();
    }
}
