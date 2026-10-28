using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;

static class R145UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,hidden)!.GetValue(form)!;
        void Call(string name)=>typeof(MainForm).GetMethod(name,hidden)!.Invoke(form,null);
        string cabinet="南澳区财政局FG/GJ01",a="汕头市南澳县财政局宿舍B栋2梯FGL01",b="汕头市南澳县财政局宿舍A栋2梯FGL02",normal="莲下环PX01/GJ01-1",retired="莲下环PX01/GJ01-1（废弃，现场已合并）",bao="潮阳区宝华苑(三网)FG/GJ";
        string csv=Path.Combine(output,"r145-resources.csv");var names=new[]{cabinet,a,b,normal,bao+"01"};
        Csv.Write(csv,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(names.Select((n,i)=>new object?[]{i.ToString(),n,"测试对端"+i,10,2,4})));
        await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        string stations=Path.Combine(output,"r145-facilities.csv");Csv.Write(stations,new object?[][]{["设施名称","站点名称/位置点"],[cabinet,"财政局宿舍站点"],[a,"财政局宿舍站点"],[b,"财政局宿舍站点"]});store.ApplyStationFiles([(stations,"",null)]);
        store.ApplyTypeAssignments([new(cabinet,"光交箱","类型表：r145-facilities.csv","已确认",""),new(a,"分纤箱","类型表：r145-facilities.csv","已确认",""),new(b,"分纤箱","类型表：r145-facilities.csv","已确认",""),new(retired,"光交箱","类型表：r145-facilities.csv","已确认",""),new(bao+"02","光交箱","类型表：r145-facilities.csv","已确认",""),new(bao+"03","光交箱","类型表：r145-facilities.csv","已确认","")]);
        string table=Path.Combine(output,"r145-clusters.csv");string bm=bao+"02,"+bao+"01,"+bao+"03";
        Csv.Write(table,new object?[][]{["设备名称","集群类型","集群列表"],[cabinet,"光交箱",cabinet],[a,"分纤箱",a+","+b],[b,"分纤箱",a+","+b],[bao+"01","光交箱",bm],[bao+"02","光交箱",bm],[bao+"03","光交箱",bm],["孤立GJ99","光交箱","孤立GJ99"]});store.SaveClusterImport(table);
        store.SaveClusters([new("legacy-r145",retired,"光交箱",[normal,"莲下环PX01/GJ01-1（废弃","现场已合并）"],"集群表：旧数据.xlsx","需核对","设备名称不在集群列表中",false)]);
        Call("RefreshData");await idle(600);Field<TabControl>("tabs").SelectedIndex=2;Field<TabControl>("maintenanceTabs").SelectedIndex=1;
        typeof(MainForm).GetField("clusterState",hidden)!.SetValue(form,"全部");Field<ComboBox>("clusterTypeFilter").SelectedIndex=0;Field<ComboBox>("clusterRouteFilter").SelectedIndex=0;var search=Field<TextBox>("clusterSearch");search.Text=cabinet;Call("LoadClusters");await idle(500);
        var grid=Field<DataGridView>("clusterGrid");check(!grid.Columns.Contains("所属站点")&&grid.Rows.Count==1&&(int)grid.Rows[0].Cells["成员数"].Value==3,"R145 finance cross-type cluster has three members with station metadata kept off the visible list");
        check(grid.Rows[0].Cells["类型"].Value!.ToString()!.Split(';').Contains("分纤箱")&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认","R145 finance member types and confirmation are correct in the actual UI");shot("R1.4.5-财政局跨类型三成员");
        Field<ComboBox>("clusterTypeFilter").SelectedItem="分纤箱";await idle(300);check(grid.Rows.Count==1,"R145 splitter filter finds a mixed cluster represented by a cabinet");Field<ComboBox>("clusterTypeFilter").SelectedIndex=0;
        search.Text="宝华苑";Call("LoadClusters");await idle(400);check(grid.Rows.Count==1&&(int)grid.Rows[0].Cells["成员数"].Value==3&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认"&&grid.Rows[0].Cells["路由状态"].Value?.ToString()=="已暂停","R145 source-valid Baohua members are confirmed independently of insufficient graph endpoints");shot("R1.4.5-宝华苑完整成员与路由状态");
        search.Text="莲下环";Call("LoadClusters");await idle(400);check(grid.Rows.Count==1&&(int)grid.Rows[0].Cells["成员数"].Value==2&&grid.Rows[0].Cells["成员名单"].Value!.ToString()!.Contains(retired),"R145 historic broken Lianxia names render as two complete members");shot("R1.4.5-莲下环历史名称恢复");
        var selected=store.Clusters().Single(c=>c.Members.Contains(retired));form.BeginInvoke((Action)(()=>typeof(MainForm).GetMethod("EditCluster",hidden)!.Invoke(form,[selected])));await idle(400);
        var editor=Application.OpenForms.Cast<Form>().Single(f=>f.Text=="修改集群");IEnumerable<Control> Walk(Control c){yield return c;foreach(Control child in c.Controls)foreach(var v in Walk(child))yield return v;}
        var memberBox=Walk(editor).OfType<TextBox>().Single(t=>t.Multiline);check(!memberBox.WordWrap&&memberBox.Lines.Length==2&&memberBox.Text.Contains(retired),"R145 editor keeps each complete member on one logical line and does not turn visual wrapping into names");
        editor.Close();await idle(200);check(!store.Clusters().Any(c=>c.Members.Length<2)&&store.FilteredClusters.Any(c=>c.Name=="孤立GJ99"),"R145 isolated singleton is filtered from the live cluster list");
        check(form.Text.StartsWith("光路调度路由查询 R1.4.5"),"R145 release title includes the cumulative version");
    }
}
