using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;

static class R146UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,hidden)!.GetValue(form)!;
        void Load()=>typeof(MainForm).GetMethod("LoadClusters",hidden)!.Invoke(form,null);
        string cabinet="龙星大厦PX01/GJ01",cabinetTwo="龙星大厦PX01/GJ01-1",splitter="财政局FGL01";
        string csv=Path.Combine(output,"r146-resources.csv");Csv.Write(csv,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"],["1",cabinet,"测试对端",10,2,4],["2",cabinetTwo,"测试对端二",10,2,4]});
        await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        store.ApplyTypeAssignments([new(cabinet,"光交箱","类型表","已确认",""),new(cabinetTwo,"光交箱","类型表","已确认",""),new(splitter,"分纤箱","类型表","已确认","")]);
        string stations=Path.Combine(output,"r146-stations.csv");Csv.Write(stations,new object?[][]{["设施名称","所属站点"],[cabinet,"站甲"],[cabinetTwo,"站乙"],[splitter,"站甲"]});store.ApplyStationFiles([(stations,"",null)]);
        string table=Path.Combine(output,"r146-clusters.csv");Csv.Write(table,new object?[][]{["设备名称","设备类型","集群列表"],[cabinet,"光交箱",cabinet+";"+cabinetTwo],[cabinetTwo,"光交箱",cabinet+";"+cabinetTwo]});store.SaveClusterImport(table);
        typeof(MainForm).GetMethod("RefreshData",hidden)!.Invoke(form,null);await idle(400);Field<TabControl>("tabs").SelectedIndex=2;Field<TabControl>("maintenanceTabs").SelectedIndex=1;typeof(MainForm).GetField("clusterState",hidden)!.SetValue(form,"全部");Field<ComboBox>("clusterRouteFilter").SelectedIndex=0;Field<TextBox>("clusterSearch").Text=cabinet;Load();await idle(400);
        var grid=Field<DataGridView>("clusterGrid");check(grid.Rows.Count==1&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认"&&grid.Rows[0].Cells["路由状态"].Value?.ToString()=="已启用","R146 two explicitly listed cabinets remain enabled despite different stations");shot("R1.4.6-龙星大厦纯光交箱");
        Csv.Write(table,new object?[][]{["设备名称","设备类型","集群列表"],[cabinet,"光交箱",cabinet+";"+splitter],[splitter,"分纤箱",cabinet+";"+splitter]});store.SaveClusterImport(table);Field<TextBox>("clusterSearch").Text=cabinet;Load();await idle(400);
        check(grid.Rows.Count==1&&grid.Rows[0].Cells["成员数"].Value is 2&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认"&&grid.Rows[0].Cells["路由状态"].Value?.ToString()=="已启用","R146 same-site cabinet and splitter enable with one routed member");shot("R1.4.6-财政局混合类型单路由成员");
    }
}
