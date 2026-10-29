using FiberRoute.Core;

static class R147Checks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        string folder=Path.Combine(root,"r147");Directory.CreateDirectory(folder);
        async Task<Store> Seed(string key,params string[] routed)
        {
            string csv=Path.Combine(folder,key+"-edges.csv");
            Csv.Write(csv,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(routed.Select((name,i)=>new object?[]{i.ToString(),name,"对端"+i,10,2,4})));
            var store=new Store(Path.Combine(folder,key));await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default);return store;
        }
        string a="龙潭古寺ODB01/GF01",b="龙潭古寺ODB02/GF02";
        var splitters=await Seed("splitters",a);
        splitters.ApplyTypeAssignments([new(a,"分纤箱","设施表","已确认",""),new(b,"分纤箱","设施表","已确认","")]);
        string table=Path.Combine(folder,"splitters.csv");Csv.Write(table,new object?[][]{["设备名称","设备类型","集群列表"],[a,"分纤箱",a+";"+b],[b,"分纤箱",a+";"+b]});splitters.SaveClusterImport(table);
        check(splitters.Clusters().Single() is {Status:"已确认",Enabled:true},"R147 all-splitter cluster enables with one routed member");
        string head="潮阳综合楼环PX01/GJ01",peer="潮阳综合楼环PX01/GJ01-1",third="凤南-和睦PX11/GJ02",missing="（废弃）潮阳综合楼PX11/GJ01";
        var legacy=await Seed("legacy",peer,third);
        legacy.ApplyTypeAssignments([new(head,"光交箱","设施表","已确认",""),new(peer,"光交箱","设施表","已确认",""),new(third,"光交箱","设施表","已确认","")]);
        legacy.SaveClusters([new("legacy-1",head,"光交箱",[head,peer,third,missing],"集群表：历史","已确认","",true)]);
        var group=legacy.Clusters().Single();
        check(group.Status=="已确认"&&group.Enabled&&group.Reason.Contains(missing),"R147 missing individual device row is informational when two cabinets are routed");
        var abandoned=new Store(Path.Combine(folder,"abandoned"));
        abandoned.SaveClusters([new("legacy-2","(废弃)上村宽带环PX01/GJ03","光交箱",["(废弃)上村宽带环PX01/GJ03","(废弃)上村宽带环PX01/GJ03-1"],"集群表：历史","已确认","",true)]);
        group=abandoned.Clusters().Single();
        check(group.Status=="已确认"&&!group.Enabled&&group.Reason.Contains("不足两个"),"R147 explicit listed but unrouted cabinets are confirmed and paused");
    }
}
