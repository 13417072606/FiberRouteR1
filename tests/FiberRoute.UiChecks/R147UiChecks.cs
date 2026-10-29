using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;

static class R147UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,hidden)!.GetValue(form)!;
        string a="龙潭古寺ODB01/GF01",b="龙潭古寺ODB02/GF02";
        string csv=Path.Combine(output,"r147-resources.csv");Csv.Write(csv,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"],["1",a,"测试对端",10,2,4]});
        await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        store.ApplyTypeAssignments([new(a,"分纤箱","设施表","已确认",""),new(b,"分纤箱","设施表","已确认","")]);
        string table=Path.Combine(output,"r147-clusters.csv");Csv.Write(table,new object?[][]{["设备名称","设备类型","集群列表"],[a,"分纤箱",a+";"+b],[b,"分纤箱",a+";"+b]});store.SaveClusterImport(table);
        typeof(MainForm).GetMethod("RefreshData",hidden)!.Invoke(form,null);await idle(400);
        Field<TabControl>("tabs").SelectedIndex=2;Field<TabControl>("maintenanceTabs").SelectedIndex=1;
        typeof(MainForm).GetField("clusterState",hidden)!.SetValue(form,"全部");Field<ComboBox>("clusterRouteFilter").SelectedIndex=0;Field<TextBox>("clusterSearch").Text="龙潭古寺";
        typeof(MainForm).GetMethod("LoadClusters",hidden)!.Invoke(form,null);await idle(400);
        var grid=Field<DataGridView>("clusterGrid");
        check(grid.Rows.Count==1&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认"&&grid.Rows[0].Cells["路由状态"].Value?.ToString()=="已启用","R147 all-splitter cluster shows confirmed and enabled with one routed member");
        shot("R1.4.7-龙潭古寺纯分纤箱单路由成员");
    }
}
