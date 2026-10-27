using FiberRoute.Core;
using System.Text.Json;
using System.Diagnostics;
static class R13Checks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        var nodes=new[]{new Node(0,"甲","分纤箱",true),new Node(1,"火车站GJ01","光交箱",true),new Node(2,"火车站GJ01-1","光交箱",true),new Node(3,"乙","分纤箱",true)};
        var es=new[]{new Edge(0,"e1","",0,1,102,1,2,""),new Edge(1,"e2","",2,3,106.21,1,2,"")};
        Graph G(bool enable=true,string status="已确认"){var g=new Graph(nodes,es,"cluster");g.SetClusters([new("c","火车站","光交箱",[nodes[1].Name,nodes[2].Name],"表格",status,"",enable)]);return g;}
        RouteOptions O()=>new(0,3,1,1,8,RouteOrder.Distance,[],[]);
        check(Routing.Find(G(false),O()).Routes.Count==0,"cluster disabled cannot bridge disconnected devices");
        check(Routing.Find(G(true,"待确认"),O()).Routes.Count==0,"unconfirmed cluster cannot connect routing");
        foreach(RouteOrder mode in Enum.GetValues<RouteOrder>()){
            var r=Routing.Find(G(),O() with{Order=mode}).Routes.Single();check(r.Jumps==1&&Math.Abs(r.Length-208.21)<1e-9&&r.Bottleneck==1&&r.Edges.SequenceEqual(new[]{0,1})&&r.Transfers.Single()==new ClusterTransfer(1,2,1)&&r.Steps[1].From==2,"cluster hard rules and exact real endpoints "+mode);
        }
        check(Routing.Find(G(),O() with{Demand=2}).Routes.Count==0,"cluster cannot bypass real capacity");check(Routing.Find(G(),O() with{MaxJumps=0}).Routes.Count==0,"cluster does not erase actual hop");check(Routing.Find(G(),O() with{AvoidTypes=["光交箱"]}).Routes.Count==0,"cluster type exclusion enforced");
        check(Routing.Find(G(),O() with{AvoidNodes=[2]}).Routes.Count==0,"avoiding either cluster member excludes logical node");
        bool conflict=false;try{Routing.Find(G(),O() with{Start=1,AvoidNodes=[2]});}catch(ArgumentException){conflict=true;}check(conflict,"cluster member avoid endpoint conflict rejected");
        var reverse=Routing.Find(G(),O() with{Start=3,End=0}).Routes.Single();check(reverse.Steps[0].From==3&&reverse.Transfers.Single().From==2,"reverse cluster path retains direction");
        var same=Routing.Find(G(),O() with{Start=1,End=2,MaxJumps=0}).Routes.Single();check(same.Edges.Length==0&&same.Jumps==0&&same.Bottleneck==0&&same.Transfers.Length==1,"same cluster endpoint relation has no invented capacity or resource");
        var refs=Routing.Find(G(),O() with{Demand=2,RequireCapacity=false}).Routes;
        check(refs.Count==1&&!refs[0].HasCapacity(2)&&refs[0].CapacityStatus(2).Contains("芯数不足"),"capacity filter can explicitly include a clearly marked shortage reference");
        check(Routing.Find(G(),O() with{Demand=2,RequireCapacity=false,AvoidTypes=["光交箱"]}).Routes.Count==0&&Routing.Find(G(),O() with{Demand=2,RequireCapacity=false,MaxJumps=0}).Routes.Count==0,"reference routes still enforce exclusions and hop limits");
        check(Routing.Find(G(),O() with{Start=1,End=3,AvoidTypes=["光交箱"]}).Routes.Count==1,"endpoint type exemption remains after its UI wording is removed");
        var refNodes=Enumerable.Range(0,4).Select(i=>new Node(i,"参考"+i,"站点",true)).ToArray();
        var refEdges=new[]{new Edge(0,"short","",0,3,1,0,4,""),new Edge(1,"long1","",0,1,20,3,4,""),new Edge(2,"long2","",1,3,20,3,4,"")};
        var refGraph=new Graph(refNodes,refEdges,"ref");var refOrder=Routing.Find(refGraph,new(0,3,2,2,8,RouteOrder.Distance,[],[],RequireCapacity:false));
        check(refOrder.Routes.Count==2&&refOrder.Routes[0].HasCapacity(2)&&refOrder.Routes[1].Bottleneck==0,"sufficient routes precede shortage references without inventing capacity on zero-free edges");
        check(Routing.Find(refGraph,new(0,3,2,2,8,RouteOrder.Distance,[],[])).Routes.All(r=>r.HasCapacity(2)),"default capacity filter never returns shortage routes");
        check(same.CapacityStatus(1)=="内部关系容量待核验","zero-segment room relation does not claim verified capacity");
        using var cancel=new CancellationTokenSource();cancel.Cancel();check(Routing.Find(G(),O(),cancel.Token).Cancelled,"cluster routing cancellation");
        var s=new Store(Path.Combine(root,"status-test"));var map=Store.Fields.ToDictionary(x=>x,x=>x);string resources=Path.Combine(root,"status.csv");Csv.Write(resources,new object?[][]{Store.Fields.Cast<object?>().ToArray(),["a","edge",1,1,2,"甲","乙","",""]});await s.ImportAsync(resources,"自动",map,1,null,default);
        check(s.Graph.Nodes.All(n=>n.Confirmation=="待确认"),"automatic identification is pending, not abnormal");
        string type=Path.Combine(root,"type.csv");Csv.Write(type,new object?[][]{["设施名称","红线范围","红线范围"],["甲","x","y"],["未来节点","x","y"]});var preview=s.PreviewTypeFiles([(type,"光交接箱",null)]);s.ApplyTypeAssignments(preview);
        check(s.Graph.Nodes[0].Confirmation=="已确认"&&s.Graph.Nodes[0].Type=="光交箱","real asset type table tolerates duplicate unused headers and confirms exact matches");
        check(s.TypeAssignments().ContainsKey("未来节点"),"unmatched type mapping retained for future resource snapshots");
        s.SetTypes([new("甲","","机房","可更新")]);var collision=s.PreviewTypeFiles([(type,"光交箱",null)]);s.ApplyTypeAssignments(collision);check(s.Graph.Nodes[0].Type=="机房"&&s.Graph.Nodes[0].Confirmation=="需核对"&&s.PreviewTypeFiles([(type,"光交箱",null)]).First(x=>x.Name=="甲").Type=="机房","type import flags manual conflict without overwriting manual value");
        s.SetTypes([new("甲","","机房","可更新")]);check(s.Graph.Nodes[0].Confirmation=="已确认","manual correction clears abnormal state");
        string second=Path.Combine(root,"type2.csv");Csv.Write(second,new object?[][]{["设施名称"],["乙"]});var mixed=s.PreviewTypeFiles([(second,"光交箱",null),(second,"机房",null)]);check(mixed.Single().Status=="需核对","conflicting type tables require review");
        string before=JsonSerializer.Serialize(s.TypeAssignments());bool cancelled=false;try{s.ApplyTypeAssignments([new("甲","坏","表格","已确认","")],cancel.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled&&JsonSerializer.Serialize(s.TypeAssignments())==before,"type transaction cancellation preserves metadata");
        string clusters=Path.Combine(root,"clusters.csv");Csv.Write(clusters,new object?[][]{["设备名称","集群类型","集群列表"],["甲","机房","甲,乙"],["乙","机房","乙,甲"]});var cp=s.PreviewClusters(clusters);check(cp.Count==1&&cp[0].Status=="已确认","reciprocal cluster rows deduplicated and table import confirms");s.SaveClusters(cp);check(s.Graph.Logical[0]==s.Graph.Logical[1],"persisted cluster builds logical graph");
        var reopened=new Store(s.Root);check(reopened.Clusters().Count==1&&reopened.Graph.Nodes[0].Confirmation=="已确认","restart preserves types clusters status");
        s.SaveClusters([new("overlap","bad","",["甲","乙","丙"],"人工修改","已确认","",true)]);check(s.Clusters().All(c=>c.Status=="需核对"&&!c.Enabled)&&s.Graph.Logical[0]!=s.Graph.Logical[1],"overlap and missing members do not silently merge");
        string raw=Path.Combine(root,"raw-resource.csv");string[] rawRow=["00001","含\"引号\"名称","1","1","1","2","50","在网","","","","","","","\"甲设施\"","乙设施","光缆甲,光缆乙"];
        File.WriteAllText(raw,string.Join(',',Csv.RawResourceHeaders)+"\n"+string.Join(',',rawRow)+"\n",Csv.EncodingFor("GB18030"));await s.ImportAsync(raw,"自动",map,1,null,default,"__RESOURCE_RAW__");
        check(s.Graph.Edges.Single().Name==rawRow[1]&&s.Graph.Nodes[0].Name==rawRow[14]&&s.Raw(0).Contains("光缆甲,光缆乙"),"explicit raw 17-column export preserves literal quotes and final multi-value field");
        string fingerprint=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(s.Database)));var repeated=await s.ImportAsync(raw,"自动",map,1,null,default,"__RESOURCE_RAW__");check(repeated.Unchanged&&fingerprint==Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(s.Database))),"identical periodic resource import skips DB replacement");
        s.SaveClusters([new("manual","人工组","",["x","y"],"人工修改","已确认","",false)]);s.SaveClusters([],true);check(s.Clusters().Any(c=>c.Id=="manual")&&s.Clusters().All(c=>c.Source=="人工修改"),"full cluster refresh removes stale table groups and retains manual groups");
        s.SaveClusters([new("manual","覆盖尝试","",["changed","members"],"集群表：测试","已确认","",true)],true);var preserved=s.Clusters().Single(c=>c.Id=="manual");check(preserved.Source=="人工修改"&&preserved.Members.Contains("x")&&preserved.Status=="需核对","table refresh preserves conflicting manual cluster and flags review");
        // Independent exhaustive DFS on contracted multigraphs compares all four scoring modes.
        var random=new Random(13013);
        for(int trial=0;trial<80;trial++){
            var ns=Enumerable.Range(0,7).Select(i=>new Node(i,"n"+i,"机房",true)).ToArray();var edges=new List<Edge>();for(int i=0;i<14;i++){int a=random.Next(7),b=random.Next(7);if(a!=b)edges.Add(new(edges.Count,"e"+i,"",a,b,random.Next(8),random.Next(1,4),4,""));}var g=new Graph(ns,edges.ToArray(),"oracle");g.SetClusters([new("x","x","机房",["n2","n3"],"表格","已确认","",true)]);int Group(int i)=>i==3?2:i;
            var scores=new List<(int D,double L,int C)>();void Dfs(int n,HashSet<int> visited,int depth,double length,int capacity){if(n==6){scores.Add((depth,length,capacity));return;}if(depth==4)return;foreach(var e in edges){int a=Group(e.A),b=Group(e.B);if(a==b||e.Free<2||a!=n&&b!=n)continue;int next=a==n?b:a;if(visited.Contains(next))continue;visited.Add(next);Dfs(next,visited,depth+1,length+e.Length!.Value,Math.Min(capacity,e.Free));visited.Remove(next);}}
            Dfs(0,[0],0,0,int.MaxValue);foreach(var mode in Enum.GetValues<RouteOrder>()){double Score((int D,double L,int C) x)=>mode==RouteOrder.Distance?x.L:mode==RouteOrder.Capacity?-x.C:x.D;var expected=scores.OrderBy(Score).Take(8).Select(Score);var got=Routing.Find(g,new(0,6,2,3,8,mode,[],[]));var actual=got.Routes.Select(r=>mode==RouteOrder.Distance?r.Length:mode==RouteOrder.Capacity?-(double)r.Bottleneck:r.Edges.Length);if(got.Limited||!expected.SequenceEqual(actual))throw new Exception("cluster oracle mismatch");}
        }check(true,"320 random contracted multigraph comparisons against independent exhaustive oracle");
    }
    public static async Task Full(string input,string output)
    {
        Directory.CreateDirectory(output);var s=new Store(output);var files=Directory.GetFiles(input,"*",SearchOption.AllDirectories);string Pick(string n)=>files.Single(p=>Path.GetFileName(p)==n);var timer=Stopwatch.StartNew();var result=await s.ImportAsync(Pick("中继段-1.CSV"),"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default,"__RESOURCE_RAW__");Console.WriteLine($"FULL resources_read={result.Read} accepted={result.Accepted} invalid={result.Invalid} nodes={result.Nodes} milliseconds={timer.ElapsedMilliseconds}");
        var list=new[]{("光交接箱-1.CSV","光交箱"),("ODF-1.CSV","机房"),("光终端盒-1.CSV","光终端盒"),("光分纤箱-1.CSV","分纤箱"),("节点类型-多媒体箱-20261026131714.xlsx","多媒体箱")}.Select(x=>(Pick(x.Item1),x.Item2,(string?)null)).ToArray();var types=s.PreviewTypeFiles(list);s.ApplyTypeAssignments(types);Console.WriteLine($"FULL type_assignments={types.Count} matched={s.Graph.Nodes.Count(n=>n.Confirmation=="已确认")} review={s.Graph.Nodes.Count(n=>n.Confirmation=="需核对")} pending={s.Graph.Nodes.Count(n=>n.Confirmation=="待确认")}");
        var clusters=s.PreviewClusters(Pick("集群管理.xlsx"));s.SaveClusters(clusters);Console.WriteLine($"FULL clusters={clusters.Count} enabled={s.Clusters().Count(c=>c.Enabled)} review={s.Clusters().Count(c=>c.Status=="需核对")} elapsed_ms={timer.ElapsedMilliseconds}");
        if(!result.Unchanged&&result.Read!=281242||result.Accepted==0||s.Graph.Nodes.Length<1000||types.Count<200000||clusters.Count!=364)throw new Exception("full dataset checks failed");
        Csv.Write(Path.Combine(output,"节点类型匹配报告.csv"),new[]{new object?[]{"设施名称","类型","确认状态","来源","原因"}}.Concat(s.Graph.Nodes.Select(n=>new object?[]{n.Name,n.Type,n.Confirmation,n.Source,n.Reason})));
        Csv.Write(Path.Combine(output,"集群核对报告.csv"),new[]{new object?[]{"集群","成员","确认状态","原因","启用"}}.Concat(s.Clusters().Select(c=>new object?[]{c.Name,string.Join(',',c.Members),c.Status,c.Reason,c.Enabled})));
        Console.WriteLine("FULL DATA PASS");
    }
}
