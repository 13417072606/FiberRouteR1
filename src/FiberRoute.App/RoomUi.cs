using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly DataGridView roomGrid=Grid();readonly TextBox roomSearch=new(){Width=300,PlaceholderText="搜索机房或成员设施"};
    List<IGrouping<string,RoomAssignment>> visibleRooms=[];TabControl? maintenanceTabs;readonly NumericUpDown roomPage=Num(1,99999,1);readonly Label roomPageInfo=L("");
    void BuildRoomManagement(TabControl inner)
    {
        maintenanceTabs=inner;var tab=new TabPage("机房归属"){Padding=new Padding(12),BackColor=Color.White};inner.TabPages.Add(tab);
        var actions=Flow();actions.Controls.AddRange([roomSearch,B("筛选",LoadRooms),B("查看成员与确认",ShowRoomMembers),B("导入所属机房表",ImportRoomTables),B("导出归属清单",()=>{var p=Save("机房归属.csv");if(p!=null)Csv.Write(p,new[]{new object?[]{"设施名称","所属机房","确认状态","参与机房整体路由","来源","原因"}}.Concat(store.RoomAssignments().Select(x=>new object?[]{x.Name,x.Room,x.Status,x.Enabled,x.Source,x.Reason})));})]);
        var note=new Label{Dock=DockStyle.Top,Height=62,Text="以明确的所属机房为核心归并 ODF / ODB；保留实际设施和中继段。明确且无冲突的光交归属参与计算，在路径图内显示提示。\n仅使用所属机房字段，不使用上联机房、站点或名称相似度推断。",ForeColor=Theme.Muted,Padding=new Padding(8)};
        var pager=Flow();pager.Dock=DockStyle.Bottom;pager.Controls.AddRange([roomPageInfo,B("上一页",()=>{if(roomPage.Value>1)roomPage.Value--;LoadRooms();}),roomPage,B("跳转",LoadRooms),B("下一页",()=>{roomPage.Value++;LoadRooms();})]);tab.Controls.Add(roomGrid);tab.Controls.Add(pager);tab.Controls.Add(actions);tab.Controls.Add(note);roomGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)ShowRoomMembers();};
    }
    void LoadRooms()
    {
        if(maintenanceTabs==null)return;string term=roomSearch.Text.Trim();visibleRooms=store.RoomAssignments().Where(x=>x.Room.Contains(term,StringComparison.OrdinalIgnoreCase)||x.Name.Contains(term,StringComparison.OrdinalIgnoreCase)).GroupBy(x=>x.Room,StringComparer.Ordinal).OrderBy(x=>x.Key,StringComparer.Ordinal).ToList();
        int pages=Math.Max(1,(visibleRooms.Count+99)/100);if(roomPage.Value>pages)roomPage.Value=pages;int skip=((int)roomPage.Value-1)*100;roomPageInfo.Text=$"共 {visibleRooms.Count:N0} 个机房归属 / {pages} 页";
        roomGrid.DataSource=visibleRooms.Skip(skip).Take(100).Select((g,i)=>new{序号=i+skip+1,所属机房=g.Key,设施数=g.Count(),已纳入=g.Count(x=>x.Enabled),已暂停光交箱=g.Count(x=>x.IsCabinet&&!x.Enabled),确认状态=g.Any(x=>x.Status=="需核对")?"需核对":"已确认",成员=string.Join("；",g.Select(x=>x.Name))}).ToList();
    }
    void ShowRoomMembers()
    {
        if(roomGrid.CurrentRow?.Cells["序号"].Value is not int ix||ix<1||ix>visibleRooms.Count)return;string room=visibleRooms[ix-1].Key;
        using var d=new Form{Text="机房成员 · "+room,Width=1100,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var grid=Grid();List<RoomAssignment> rows=[];
        void Load(){rows=store.RoomAssignments().Where(x=>x.Room==room||x.Room.Split('；').Contains(room)).ToList();grid.DataSource=rows.Select((x,i)=>new{序号=i+1,设施名称=x.Name,所属机房=x.Room,确认状态=x.Status,参与整体路由=x.Enabled,原因=x.Reason,来源=x.Source}).ToList();grid.AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells;grid.AutoResizeColumnHeadersHeight();}
        async Task Set(bool enable)
        {
            if(busy||grid.CurrentRow?.Cells["序号"].Value is not int n)return;var row=rows[n-1];var rooms=row.Room.Split('；');string selected=rooms.Length==1?row.Room:room;
            if(!rooms.Contains(selected))throw new InvalidDataException("请先更正源表中的冲突机房归属后重新导入。");
            string title=row.IsCabinet?"光交箱归入机房提示":"确认机房归属";string msg=$"设施：{row.Name}\n所属机房：{selected}\n\n"+(enable?"确认将该设施纳入此机房整体进行路由分析？表格归属不能验证现场接续与容量，真实中继段仍逐段校验。":"暂停该设施参与机房整体路由？实际设施和中继段均保留。");
            if(MessageBox.Show(d,msg,title,MessageBoxButtons.YesNo,MessageBoxIcon.Information)!=DialogResult.Yes)return;
            if(await Work("保存机房归属确认…",ct=>Task.Run(()=>store.ConfirmRoom(row.Name,selected,enable),ct))){Load();RefreshData();}
        }
        d.Controls.Add(grid);var controls=Flow();controls.Controls.AddRange([L(room),B("确认选中设施并纳入机房",async()=>await Set(true)),B("暂停选中设施归并",async()=>await Set(false)),B("关闭",()=>d.Close())]);d.Controls.Add(controls);Load();d.ShowDialog(this);
    }
    async Task ImportRoomTables()
    {
        if(busy)return;using var f=new OpenFileDialog{Filter=TableInput.Filter,Multiselect=true,Title="选择含设施名称和机房名称/所属机房字段的表格"};if(f.ShowDialog()!=DialogResult.OK)return;
        var files=f.FileNames.Select(p=>(p,"",(string?)null)).ToArray();List<RoomAssignment> values=[];if(!await Work("读取明确的所属机房字段…",async ct=>values=await Task.Run(()=>store.PreviewRoomFiles(files,ct),ct)))return;
        if(values.Count==0)throw new InvalidDataException("未找到非空的机房名称/所属机房字段；不会使用上联机房替代。");
        if(MessageBox.Show($"读取 {values.Count:N0} 条归属。按本次文件替换同来源的归属记录，保留人工启用选择；明确且无冲突的光交归属会在路径图内显示提示。继续？","导入机房归属",MessageBoxButtons.YesNo)!=DialogResult.Yes)return;
        if(await Work("更新机房归属…",ct=>Task.Run(()=>store.ApplyRoomFiles(files,ct),ct))){RefreshData();}
    }
}
