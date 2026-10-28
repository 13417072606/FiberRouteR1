using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;
static class R143UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string n)=>(T)typeof(MainForm).GetField(n,hidden)!.GetValue(form)!;
        object? Call(string n,params object[] args)=>typeof(MainForm).GetMethod(n,hidden)!.Invoke(form,args);
        string longName="汕头市潮阳区平南社区(2期)茶园文明小区南栋1梯FGL034-POS117-1:8";
        string room="汕头市南澳县后宅镇育新路财政局宿舍一楼机房完整名称及尾部验证ODF01";
        string csv=Path.Combine(output,"长名称.csv");Csv.Write(csv,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端类型","终端类型","始端机房","终端机房"],["long",longName,"财政局GJ01",10,6,12,"未识别","光交箱","",room],["second","财政局GJ01-1","B/ODF01",10,6,12,"光交箱","机房",room,"B机房"]});await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);store.SaveClusters([new("r143-fullnames","完整集群名称-"+longName,"光交箱",["财政局GJ01","财政局GJ01-1"],"人工修改","已确认","",true)]);Call("RefreshData");
        var tabs=Field<TabControl>("tabs");var inner=Field<TabControl>("maintenanceTabs");tabs.SelectedIndex=2;inner.SelectedIndex=0;Field<TextBox>("typeSearch").Text="POS117";Call("LoadTypes");await idle(400);
        var node=Field<DataGridView>("typeGrid");node.Columns["名称"].Width=146*form.DeviceDpi/96;await idle(400);
        void Fits(DataGridView grid,string column,string expected)
        {
            var row=grid.Rows.Cast<DataGridViewRow>().FirstOrDefault(r=>r.Cells[column].Value?.ToString()==expected)??throw new Exception(column+" missing; rows="+string.Join("|",grid.Rows.Cast<DataGridViewRow>().Select(r=>r.Cells[column].Value?.ToString())));var cell=row.Cells[column];var style=cell.InheritedStyle;int width=grid.Columns[column].Width-style.Padding.Horizontal-6*form.DeviceDpi/96;string wrapped=(string)typeof(MainForm).GetMethod("WrapCellText",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[expected,style.Font??grid.Font,width])!;
            check(wrapped.Replace("\n","")==expected&&row.Height>=TextRenderer.MeasureText(wrapped,style.Font??grid.Font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height+style.Padding.Vertical+8*form.DeviceDpi/96,"R143 "+column+" full text and final suffix fit the measured row at "+form.DeviceDpi+" DPI");
        }
        Fits(node,"名称",longName);check(node.Rows[0].Cells["类型"].Value?.ToString()=="分纤箱"&&!Field<FlowLayoutPanel>("typePills").Controls.Cast<Control>().Any(c=>c.Text.StartsWith("站点")),"R143 POS classification and removal of station chip apply to live imported data");shot("R1.4.3-节点全称与POS");
        tabs.SelectedIndex=1;Call("LoadResources");await idle(400);var resource=Field<DataGridView>("resourceGrid");resource.Columns["始端设施"].Width=146*form.DeviceDpi/96;await idle(400);Fits(resource,"始端设施",longName);shot("R1.4.3-中继段全称");
        tabs.SelectedIndex=2;inner.SelectedIndex=1;typeof(MainForm).GetField("clusterState",hidden)!.SetValue(form,"全部");Field<ComboBox>("clusterTypeFilter").SelectedIndex=0;Field<ComboBox>("clusterRouteFilter").SelectedIndex=0;Field<TextBox>("clusterSearch").Text="完整集群名称-";Call("LoadClusters");await idle(400);var cluster=Field<DataGridView>("clusterGrid");cluster.Columns["集群名称"].Width=150*form.DeviceDpi/96;await idle(400);Fits(cluster,"集群名称","完整集群名称-"+longName);check(cluster.Columns.Contains("所属站点"),"R143 cluster management has its own station column");shot("R1.4.3-集群全称与所属站点");
        inner.SelectedIndex=2;typeof(MainForm).GetField("roomState",hidden)!.SetValue(form,"全部");Field<ComboBox>("roomArea").SelectedIndex=0;Field<ComboBox>("roomRouteFilter").SelectedIndex=0;Field<TextBox>("roomSearch").Text=room;Call("LoadRooms");await idle(400);var rooms=Field<DataGridView>("roomGrid");rooms.Columns["所属机房"].Width=150*form.DeviceDpi/96;await idle(400);Fits(rooms,"所属机房",room);shot("R1.4.3-机房全称");check(form.Text.StartsWith("光路调度路由查询 R1.4."),"R143 application title uses the requested new product name");
    }
}
