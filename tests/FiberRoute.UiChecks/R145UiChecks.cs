using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;

static class R145UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,hidden)!.GetValue(form)!;
        object? Call(string name,params object[] args)=>typeof(MainForm).GetMethod(name,hidden)!.Invoke(form,args);
        string cabinet="南澳区财政局FG/GJ01",a="汕头市南澳县财政局宿舍A栋2梯FGL02",b="汕头市南澳县财政局宿舍B栋2梯FGL01";
        string lian="莲下环PX01/GJ01-1",retired="莲下环PX01/GJ01-1（废弃，现场已合并）",bao="潮阳区宝华苑(三网)FG/GJ";
        string csv=Path.Combine(output,"r145-ui-resources.csv");
        Csv.Write(csv,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(new[]{cabinet,a,b,lian,bao+"01"}.Select((n,i)=>new object?[]{"r145-"+i,n,"测试对端"+i,10,2,4})));
        await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        string station=Path.Combine(output,"r145-ui-stations.csv");Csv.Write(station,new object?[][]{["设施名称","站点名称/位置点"],[cabinet,"财政局同一站点"],[a,"财政局同一站点"],[b,"财政局同一站点"]});store.ApplyStationFiles([(station,"",null)]);
        store.ApplyTypeAssignments([new(cabinet,"光交箱","类型表","已确认",""),new(a,"分纤箱","类型表","已确认",""),new(b,"分纤箱","类型表","已确认",""),new(retired,"光交箱","类型表","已确认",""),new(bao+"02","光交箱","类型表","已确认",""),new(bao+"03","光交箱","类型表","已确认","")]);
        string table=Path.Combine(output,"r145-ui-clusters.csv"),members=string.Join(';',new[]{bao+"01",bao+"02",bao+"03"});
        Csv.Write(table,new object?[][]{["设备名称","设备类型","集群列表","所属站点"],[cabinet,"光交接箱",cabinet,""],[a,"光分纤箱",a+";"+b,""],[b,"光分纤箱",a+";"+b,""],[bao+"01","光交接箱",members,""],[bao+"02","光交接箱",members,""],[bao+"03","光交接箱",members,""],["孤立GJ99","光交箱","孤立GJ99",""]});store.SaveClusterImport(table);
        store.SaveClusters([new("r145-legacy-lian",retired,"光交箱",["现场已合并）",lian,"莲下环PX01/GJ01-1（废弃"],"集群表：旧数据.xlsx","需核对","设备名称不在集群列表中",false)]);
        Call("RefreshData");await idle(600);Field<TabControl>("tabs").SelectedIndex=2;Field<TabControl>("maintenanceTabs").SelectedIndex=1;
        typeof(MainForm).GetField("clusterState",hidden)!.SetValue(form,"全部");Field<ComboBox>("clusterTypeFilter").SelectedIndex=0;Field<ComboBox>("clusterRouteFilter").SelectedIndex=0;
        async Task Search(string text){Field<TextBox>("clusterSearch").Text=text;Call("LoadClusters");await idle(500);}
        await Search("财政局");var grid=Field<DataGridView>("clusterGrid");
        check(!grid.Columns.Contains("所属站点")&&grid.Rows.Count==1&&Convert.ToInt32(grid.Rows[0].Cells["成员数"].Value)==3,"R145 finance displays one cross-type three-member cluster with station kept in backend");
        check(grid.Rows[0].Cells["类型"].Value?.ToString()?.Contains("分纤箱")==true&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认","R145 mixed group type and confirmation are shown accurately");shot("R1.4.5-财政局跨类型三成员");
        Field<ComboBox>("clusterTypeFilter").SelectedItem="分纤箱";Call("LoadClusters");await idle(300);check(grid.Rows.Count==1,"R145 splitter filter includes a mixed cabinet and splitter cluster");Field<ComboBox>("clusterTypeFilter").SelectedIndex=0;
        await Search("宝华苑");
        check(grid.Rows.Count==1&&Convert.ToInt32(grid.Rows[0].Cells["成员数"].Value)==3&&grid.Rows[0].Cells["确认状态"].Value?.ToString()=="已确认"&&grid.Rows[0].Cells["路由状态"].Value?.ToString()=="已暂停","R145 existing source members remain confirmed while insufficient live graph endpoints pause routing");shot("R1.4.5-宝华苑完整成员与路由状态");
        await Search("莲下环");
        check(grid.Rows.Count==1&&Convert.ToInt32(grid.Rows[0].Cells["成员数"].Value)==2&&grid.Rows[0].Cells["成员名单"].Value?.ToString()?.Contains(retired)==true,"R145 historical split name is shown as two complete members");shot("R1.4.5-历史名称恢复");
        var selected=store.Clusters().Single(c=>c.Members.Contains(retired));form.BeginInvoke(new Action(()=>Call("EditCluster",selected)));await idle(400);
        var editor=Application.OpenForms.Cast<Form>().Single(f=>f.Text=="修改集群");
        IEnumerable<Control> Walk(Control control){foreach(Control child in control.Controls){yield return child;foreach(var nested in Walk(child))yield return nested;}}
        var text=Walk(editor).OfType<TextBox>().Single(t=>t.Multiline);
        check(!text.WordWrap&&text.Lines.Length==2&&text.Lines.Contains(retired),"R145 editor preserves complete names and never converts visual wrapping to saved members");editor.Close();await idle(200);
        check(store.Clusters().All(c=>c.Members.Length>=2)&&store.FilteredClusters.Any(c=>c.Name=="孤立GJ99"),"R145 isolated source row stays filtered after UI reload");
        check(form.Text.StartsWith("光路调度路由查询 R1.4.5"),"R145 app title identifies the cumulative release");
    }
}
