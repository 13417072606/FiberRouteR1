using FiberRoute.Core;

static class R146Checks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        string folder=Path.Combine(root,"r146");Directory.CreateDirectory(folder);
        async Task<Store> Seed(string key,params string[] routed)
        {
            string csv=Path.Combine(folder,key+"-edges.csv");Csv.Write(csv,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(routed.Select((n,i)=>new object?[]{i.ToString(),n,"对端"+i,10,2,4})));
            var store=new Store(Path.Combine(folder,key));await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default);return store;
        }
        string cabA="龙星大厦PX01/GJ01",cabB="龙星大厦PX01/GJ01-1",splitA="财政局FGL01",splitB="财政局FGL02";
        string station=Path.Combine(folder,"stations.csv"),table=Path.Combine(folder,"cluster.csv");
        void Sites(params (string Name,string Site)[] rows)=>Csv.Write(station,new[]{new object?[]{"设施名称","所属站点"}}.Concat(rows.Select(x=>new object?[]{x.Name,x.Site})));
        void Members(params (string Name,string Type,string List)[] rows)=>Csv.Write(table,new[]{new object?[]{"设备名称","设备类型","集群列表"}}.Concat(rows.Select(x=>new object?[]{x.Name,x.Type,x.List})));
        var cabinets=await Seed("two-cabinets",cabA,cabB);cabinets.ApplyTypeAssignments([new(cabA,"光交箱","类型表","已确认",""),new(cabB,"光交箱","类型表","已确认","")]);
        Sites((cabA,"金丰居委【可装2000M】-1"),(cabB,"金丰居委-17"));cabinets.ApplyStationFiles([(station,"",null)]);
        Members((cabA,"光交箱",cabA+";"+cabB),(cabB,"光交箱",cabA+";"+cabB));cabinets.SaveClusterImport(table);
        var group=cabinets.Clusters().Single();check(group.Members.Length==2&&group.Status=="已确认"&&group.Enabled&&!group.Reason.Contains("站点冲突"),"R146 explicitly listed cabinets ignore conflicting station labels and use two routed members");
        var oneCabinet=await Seed("one-cabinet",cabA);oneCabinet.ApplyTypeAssignments([new(cabA,"光交箱","类型表","已确认",""),new(cabB,"光交箱","类型表","已确认","")]);oneCabinet.ApplyStationFiles([(station,"",null)]);oneCabinet.SaveClusterImport(table);
        check(oneCabinet.Clusters().Single() is {Status:"已确认",Enabled:false}&&oneCabinet.Clusters()[0].Reason.Contains("不足两个"),"R146 cabinet-only relation still requires two routed members");
        var mixed=await Seed("mixed",cabA);mixed.ApplyTypeAssignments([new(cabA,"光交箱","类型表","已确认",""),new(splitA,"分纤箱","类型表","已确认",""),new(splitB,"分纤箱","类型表","已确认","")]);
        Sites((cabA,"财政局宿舍站点"),(splitA,"财政局宿舍站点"),(splitB,"财政局宿舍站点"));mixed.ApplyStationFiles([(station,"",null)]);
        string mixedList=string.Join(';',new[]{cabA,splitA,splitB});Members((cabA,"光交箱",mixedList),(splitA,"分纤箱",mixedList),(splitB,"分纤箱",mixedList));mixed.SaveClusterImport(table);
        check(mixed.Clusters().Single() is {Status:"已确认",Enabled:true,Members.Length:3},"R146 one cabinet and same-site splitters enable with one routed member");
        Sites((cabA,"财政局宿舍站点"),(splitA,"另一站点"),(splitB,"另一站点"));mixed.ApplyStationFiles([(station,"",null)]);
        check(mixed.Clusters().Single() is {Status:"需核对",Enabled:false},"R146 mixed relation retains the same-site requirement");
    }
}
