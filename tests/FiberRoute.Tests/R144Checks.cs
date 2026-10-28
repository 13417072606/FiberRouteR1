using FiberRoute.Core;
using System.Security.Cryptography;
using System.Text.Json;

static class R144Checks
{
    public static void Full(string path,string? baseline=null)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();var store=new Store(path);if(baseline!=null)store.ApplyEvidenceBaseline(baseline);var graph=store.Graph;
        if(graph.Edges.Any(e=>e.StartRegion.Length>0&&!ShantouRegions.Districts.Contains(e.StartRegion)||e.EndRegion.Length>0&&!ShantouRegions.Districts.Contains(e.EndRegion)))throw new Exception("Non-district output");
        Console.WriteLine($"FULL R144 edges={graph.Edges.Length}; nodes={graph.Nodes.Length}; load_ms={watch.ElapsedMilliseconds}");
        foreach(var group in graph.Edges.SelectMany(e=>new[]{e.StartRegionDecision!,e.EndRegionDecision!}).GroupBy(d=>(d.Region,d.Status)).OrderBy(g=>g.Key.Region).ThenBy(g=>g.Key.Status))Console.WriteLine($"{group.Key}: {group.Count()}");
        string report=Path.Combine(path,"区域补齐核对.csv");Csv.Write(report,new[]{new object?[]{"中继段ID","端点","设施名称","行政区","判定","依据"}}.Concat(graph.Edges.SelectMany(e=>new[]{(End:"始端",Name:graph.Nodes[e.A].Name,Decision:e.StartRegionDecision!),(End:"终端",Name:graph.Nodes[e.B].Name,Decision:e.EndRegionDecision!)}.Where(x=>x.Decision.Status is "未提供" or "需核对").Select(x=>new object?[]{e.Id,x.End,x.Name,x.Decision.Region,x.Decision.Status,x.Decision.Basis}))));Console.WriteLine("Private unresolved endpoint report: "+report);
        int a=Array.FindIndex(graph.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋2楼")&&n.Name.EndsWith("-GF271")),b=Array.FindIndex(graph.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋4楼")&&n.Name.EndsWith("-GF272"));
        if(a>=0&&b>=0){var routes=Routing.Find(graph,new(a,b,1,1,8,RouteOrder.Distance,[],[]));if(!routes.Routes.Any(r=>Math.Abs(r.Length-208.21)<1e-8))throw new Exception("Full route regression");Console.WriteLine("FULL 208.21m golden route PASS");}
    }
    public static async Task Run(string root,Action<bool,string> check)
    {
        check(ShantouRegions.Districts.Count==7,"R144 exactly six districts and one county are embedded");
        check(ShantouRegions.Candidates("达濠街道").SequenceEqual(new[]{"濠江区"})&&ShantouRegions.Candidates("广益街道").SequenceEqual(new[]{"澄海区"}),"R144 requested streets map to their districts");
        check(ShantouRegions.Candidates("井都镇").SequenceEqual(new[]{"潮南区"})&&ShantouRegions.Candidates("文光街道").SequenceEqual(new[]{"潮阳区"})&&ShantouRegions.Candidates("南澳区").SequenceEqual(new[]{"南澳县"}),"R144 towns and existing Nanao alias normalize consistently");
        check(ShantouRegions.Candidates("广益五金厂",true).Length==0&&ShantouRegions.Candidates("和平小区",true).Length==0&&ShantouRegions.Candidates("龙湖小区",true).Length==0&&ShantouRegions.Candidates("潮阳路",true).Length==0,"R144 bare business and community names are not guessed from town substrings");
        string folder=Path.Combine(root,"r144");Directory.CreateDirectory(folder);string csv=Path.Combine(folder,"resources.csv");
        Csv.Write(csv,new object?[][]{
            ["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端区域","终端区域"],
            ["streets","达濠GJ01","广益GJ02",10,2,4,"达濠街道","广益街道"],
            ["names","汕头市潮南区家美社区FG/GJ07","汕头市潮南区雅源美宿GF271",20,2,4,"未提供","--"],
            ["unknown","没有地名的设施","未知的另一端",30,2,4,"",""],
            ["same-node","没有地名的设施","未知的另一端2",30,2,4,"井都镇",""],
            ["evidence","设施表端点","无区域终点",40,2,4,"未提供",""],
            ["conflict1","冲突节点","对端一",50,2,4,"龙湖区",""],
            ["conflict2","冲突节点","对端二",50,2,4,"潮阳区",""],
            ["multiple","潮南区至潮阳区连接设施","普通端",50,2,4,"",""],
            ["prefix","汕头南澳南澳综合楼/ODF01","广益五金厂",50,2,4,"",""],
            ["cross-area","另一端无信息","潮南区有信息端",50,2,4,"","潮南区"]});
        var store=new Store(Path.Combine(folder,"data"));await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database)));
        string evidence=Path.Combine(folder,"facilities.csv");Csv.Write(evidence,new object?[][]{["设施名称","所属区县"],["设施表端点","广益街道"],["设施表端点","澄海区"]});store.ApplyFacilityEvidence([(evidence,"",null)]);
        Edge Get(string id)=>store.Graph.Edges.Single(e=>e.Id==id);
        check(Get("streets") is {StartRegion:"濠江区",EndRegion:"澄海区"},"R144 both directions normalize independently");
        check(Get("names") is {StartRegion:"潮南区",EndRegion:"潮南区",StartRegionDecision.Status:"已推断"},"R144 missing placeholders infer only from the endpoint's own complete facility name");
        check(Get("unknown") is {StartRegion:"潮南区",EndRegion:""},"R144 exact same endpoint evidence can fill a missing field without copying the opposite endpoint");
        check(Get("evidence") is {StartRegion:"澄海区",EndRegion:"",StartRegionDecision.Status:"已归并"},"R144 street and district evidence from facilities agree after normalization");
        check(Get("conflict1") is {StartRegion:"",StartRegionDecision.Status:"需核对"}&&Get("conflict2").StartRegion=="","R144 conflicting explicit source districts remain unresolved with review evidence");
        check(Get("multiple") is {StartRegion:"",StartRegionDecision.Status:"需核对"},"R144 ambiguous multi-district facility name is not arbitrarily classified");
        check(Get("prefix") is {StartRegion:"南澳县",EndRegion:""},"R144 established Shantou district-name prefix is recognized without guessing a business-name substring");
        check(Get("cross-area") is {StartRegion:"",EndRegion:"潮南区",StartRegionDecision.Status:"未提供"},"R144 a known far endpoint cannot fill a missing near endpoint");
        var raw=JsonSerializer.Deserialize<Dictionary<string,string>>(store.Raw(Get("streets").Index))!;var fields=ShantouRegions.ExportFields(raw,Get("streets"));
        check(fields["始端区域"]=="濠江区"&&fields["始端区域原值"]=="达濠街道"&&fields["始端区域依据"].Contains("达濠街道")&&raw["始端区域"]=="达濠街道","R144 display/export carry normalized districts, original values and evidence without overwriting raw data");
        store.ApplyTypeAssignments([new("冲突节点","光交箱","人工修改","已确认","人工核对类型")]);check(store.Graph.Nodes.Single(n=>n.Name=="冲突节点") is {Confirmed:true,Status:"已确认",Reason:"人工核对类型"},"R144 region review does not change manually confirmed facility types");
        store.Reload();var reopened=new Store(store.Root);check(reopened.Graph.Edges.Single(e=>e.Id=="names").StartRegion=="潮南区"&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),"R144 existing data reopens normalized without reimport or resource database changes");
    }
}
