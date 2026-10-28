using FiberRoute.Core;
using System.Diagnostics;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
if(args.Length==3&&args[0]=="--r131-baseline"){
 var full=new Store(args[2]);var files=Directory.GetFiles(args[1],"*",SearchOption.AllDirectories).Where(p=>Path.GetExtension(p).Equals(".csv",StringComparison.OrdinalIgnoreCase)&&!Path.GetFileName(p).StartsWith("中继段")).Select(p=>(p,"",(string?)null)).ToArray();
 full.ApplyRoomFiles(files);var seed=full.PreviewRoomFiles(files).Select(x=>new RoomSeed(x.Name,x.Room,x.Source)).ToArray();File.WriteAllText(Path.Combine(args[2],"机房归属基线.json"),System.Text.Json.JsonSerializer.Serialize(seed,new System.Text.Json.JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping}));
 var rooms=full.RoomAssignments();Console.WriteLine($"FULL ROOM PASS: baseline={seed.Length} bytes={new FileInfo(Path.Combine(args[2],"机房归属基线.json")).Length} matched_facilities={rooms.Count} room_cores={rooms.Where(x=>x.Enabled).Select(x=>x.Room).Distinct().Count()} cabinet_pending={rooms.Count(x=>x.IsCabinet&&!x.Enabled)} conflicts={rooms.Count(x=>x.Status=="需核对")} graph_logical_groups={full.Graph.ClusterNames.Count}");return;
}
if(args.Length==2&&args[0]=="--r132-room-verify"){
 var full=new Store(args[1]);var g=full.Graph;var rooms=full.RoomAssignments();
 string a="汕头南澳县南澳综合楼汇聚点四楼机房传输1",b="汕头南澳县后宅汇聚点一楼机房传输1";
 var rr=Routing.Find(g,new(g.Rooms[a][0],g.Rooms[b][0],1,8,8,RouteOrder.Distance,[],[]));
 if(!rr.Routes.Any(r=>r.Edges.Length==1&&Math.Abs(r.Length-2044.87)<.01))throw new Exception("real room-to-room example failed");
 if(rooms.Any(x=>x.IsCabinet&&x.Status=="已确认"&&!x.Enabled&&!x.Source.Contains("人工确认")))throw new Exception("unnecessary cabinet approval gate remains");
 Console.WriteLine($"FULL R1.3.2 ROOM PASS: source_facilities={rooms.Count} room_names={g.Rooms.Count} cabinets={rooms.Count(x=>x.IsCabinet)} enabled_cabinets={rooms.Count(x=>x.IsCabinet&&x.Enabled)} conflicts={rooms.Count(x=>x.Status=="需核对")} logical_groups={g.ClusterNames.Count}");
 Console.WriteLine($"FULL room example PASS: {a} -> {b}; 2044.87m; one real segment; 0 jumps; returns={rr.Routes.Count}; limited={rr.Limited}");return;
}
if(args.Length==3&&args[0]=="--r13-full"){await R13Checks.Full(args[1],args[2]);return;}
if(args.Length==2&&args[0]=="--r13-verify"){
 var full=new Store(args[1]);var g=full.Graph;int a=Array.FindIndex(g.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋2楼")&&n.Name.EndsWith("GF271")),b=Array.FindIndex(g.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋4楼")&&n.Name.EndsWith("GF272"));
 if(a<0||b<0)throw new Exception("full golden endpoints missing");var rr=Routing.Find(g,new(a,b,1,1,8,RouteOrder.Distance,[],[]));if(rr.Limited||!rr.Routes.Any(r=>Math.Abs(r.Length-208.21)<1e-8&&r.Jumps==1))throw new Exception("full golden route failed");
 Console.WriteLine($"FULL golden route 208.21m / 1 jump PASS; edges={g.Edges.Length} nodes={g.Nodes.Length}; standalone reopen PASS");return;
}
if(args.Length==3&&args[0]=="--export-preview"){RouteWorkbookChecks.Preview(args[1],args[2]);return;}
if(args.Length==3&&args[0]=="--r14-full-verify"){R14Checks.Full(args[1],args[2]);return;}
if(args.Length==4&&args[0]=="--r143-repair"){R143Checks.Repair(args[1],args[2],args[3]);return;}
if(args.Length==3&&args[0]=="--r143-full"){await R143Checks.Full(args[1],args[2]);return;}
if(args.Length>=2&&args[0]=="--r144-full"){R144Checks.Full(args[1],args.ElementAtOrDefault(2));return;}
int checks=0;
void Check(bool ok,string label){checks++;if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);}
string root=Path.Combine(Path.GetTempPath(),"fiber-tests-"+Guid.NewGuid());Directory.CreateDirectory(root);
if(args.Length==2&&args[0]=="--validate-package")
{
    try{FiberRoute.Updater.PackageValidator.Extract(args[1],Path.Combine(root,"package"));Console.WriteLine("PASS complete delivery ZIP accepted by program updater");}
    finally{Directory.Delete(root,true);}
    return;
}
var sample=Path.GetFullPath(args.FirstOrDefault(x=>x.EndsWith(".csv"))??"assets/调测中继段.csv");
Dictionary<string,string> mapping=Store.Fields.ToDictionary(x=>x,x=>x);
var store=new Store(Path.Combine(root,"store"));
if(args.Contains("--benchmark"))
{
    string csv=Path.Combine(root,"290k.csv");using(var w=new StreamWriter(csv,false,new UTF8Encoding(true)))
    {
        w.WriteLine("ID,名称,长度,空闲数量,中继纤芯数量,始端设施,终端设施,始端站点,终端站点,始端机房,终端机房,业务状态,关联光缆段");
        for(int i=0;i<290000;i++)w.WriteLine(Csv.Line(new object?[]{"441100000000097"+i.ToString("D9"),"调测中继段资源快照性能测试记录"+i,100+i%90,4,12,"节点机房"+(i%20000),"节点机房"+((i+1)%20000),"性能调测站点甲","性能调测站点乙","性能调测机房甲","性能调测机房乙","在建","关联资源仅作显示不拆解连接端点-长字段验证"+new string('测',8)}));
    }
    var clock=Stopwatch.StartNew();var result=await store.ImportAsync(csv,"自动",mapping,1,null,default);Console.WriteLine($"BENCH rows={result.Accepted} bytes={new FileInfo(csv).Length} nodes={result.Nodes} import_ms={clock.ElapsedMilliseconds}");
    var g=store.Graph;var o=new RouteOptions(0,10000,1,100,8,RouteOrder.Distance,[],[],MaxExpansions:20000,MaxQueue:10000,TimeoutMs:2000);var routes=Routing.Find(g,o);Console.WriteLine($"BENCH complex_search_ms={routes.ElapsedMs} limited={routes.Limited} expansions={routes.Expansions} peak_working_set={Process.GetCurrentProcess().PeakWorkingSet64}");
    Check(result.Accepted==290000,"synthetic 290000 rows");Check(routes.Limited,"dense parallel search bounded");Directory.Delete(root,true);return;
}
try
{
    RouteWorkbookChecks.Run(Check,root);
    var imported=await store.ImportAsync(sample,"UTF-8",mapping,1,null,default);var g=store.Graph;
    Check(imported.Accepted==12&&imported.Nodes==20,"sample 12 edges / 20 nodes");Check(Math.Abs(g.Edges.Sum(x=>x.Length??0)-1659.56)<.00001,"sample 1659.56 m");Check(g.Edges.Sum(x=>x.Free)==40,"sample 40 aggregate cores");Check(g.Edges[0].Id=="441100000000097471404159","long ID exact text");
    int a=Array.FindIndex(g.Nodes,n=>n.Name.EndsWith("-GF271")),b=Array.FindIndex(g.Nodes,n=>n.Name.EndsWith("-GF272")),middle=Array.FindIndex(g.Nodes,n=>n.Name=="潮南区家美社区FG/GJ07");
    RouteOptions Opt()=>new(a,b,1,1,8,RouteOrder.Distance,[],[]);
    var result=Routing.Find(g,Opt());Check(result.Routes.Count==1,"A route exists");var r=result.Routes[0];Check(Math.Abs(r.Length-208.21)<1e-8&&r.Jumps==1&&r.Bottleneck==1&&r.Edges.All(e=>g.Edges[e].Free-1==0),"A length / jumps / capacity / residual");
    Check(Routing.Find(g,Opt() with{Demand=2}).Routes.Count==0,"B insufficient cores");Check(Routing.Find(g,Opt() with{AvoidTypes=["光交箱"]}).Routes.Count==0,"C avoid type");Check(Routing.Find(g,Opt() with{AvoidNodes=[middle]}).Routes.Count==0,"D avoid node");Check(Routing.Find(g,Opt() with{MaxJumps=0}).Routes.Count==0,"E zero jumps");
    var room=g.Edges.First(e=>g.Nodes[e.A].Type=="机房"&&g.Nodes[e.B].Type=="机房");Check(Routing.Find(g,new(room.A,room.B,1,0,8,RouteOrder.Jumps,["机房"],[])).Routes.Count==1,"F endpoint type exemption");var zero=g.Edges.First(e=>e.Length==0);Check(Routing.Find(g,new(zero.A,zero.B,1,0,8,RouteOrder.Distance,[],[])).Routes[0].Length==0,"G zero length allowed");
    bool conflict=false;try{Routing.Find(g,Opt() with{AvoidNodes=[a]});}catch(ArgumentException){conflict=true;}Check(conflict,"explicit endpoint conflict rejected");
    using(var cancel=new CancellationTokenSource()){cancel.Cancel();Check(Routing.Find(g,Opt(),cancel.Token).Cancelled,"route cancellation");}
    Check(Routing.Find(g,Opt() with{MaxQueue=1}).Limited,"queue upper bound reported");Check(Routing.Find(g,Opt() with{MaxExpansions=0}).Limited,"expansion upper bound reported");Check(Routing.Find(g,Opt() with{TimeoutMs=0}).Limited,"time upper bound reported");
    store.SetTypes([new(g.Nodes[middle].Name,"光交箱","机房;自定义","可更新")]);g=store.Graph;
    Check(Routing.Find(g,Opt() with{AvoidTypes=["自定义","光交箱"]}).Routes.Count==0,"manual multi-label type changes routing");
    await store.ImportAsync(sample,"UTF-8",mapping,1,null,default);Check(store.Graph.Nodes[middle].Type=="机房;自定义"&&store.Graph.Nodes[middle].Confirmed,"manual types survive resource replacement");store.SetSetting("personal","kept");
    string types=Path.Combine(root,"types.csv");Csv.Write(types,new object?[][]{["节点名称","节点类型"],[g.Nodes[middle].Name,"光交箱"],[g.Nodes[middle].Name,"机房"],["不存在节点","站点"],[g.Nodes[a].Name,""]});var preview=store.PreviewTypes(types,"UTF-8","节点名称","节点类型");Check(preview.Count(x=>x.Status=="同名类型冲突")==2&&preview.Count(x=>x.Status=="未匹配")==1&&preview.Count(x=>x.Status=="空类型")==1,"type import conflicts / unknown / empty preview");
    Check(store.Graph.Nodes.Length==20,"type preview creates no nodes and changes nothing");
    string quoted=Path.Combine(root,"quoted.csv");Csv.Write(quoted,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","名称"],["000123456789012345678901"," 甲,\"站\"\n机房 ","乙","",1,2,"有,逗号\r\n换行"],["bad","","乙",2,1,2,"缺端点"],["bad2","甲","乙",2,3,2,"芯数非法"]});
    var qr=await store.ImportAsync(quoted,"UTF-8",mapping,1,null,default);Check(qr.Accepted==1&&qr.Invalid==2,"quoted multiline CSV and invalid isolation");Check(store.Graph.Nodes[0].Name=="甲,\"站\"\n机房"&&store.Graph.Edges[0].Length==null,"trim exact endpoints / missing length retained");Check(store.Graph.Edges[0].Id.StartsWith("000"),"leading zeros preserved");Check(Csv.Read(qr.ErrorFile).Count()==3,"anomaly report header + 2 records");
    store.Rollback();Check(store.Graph.Edges.Length==12&&store.Graph.Nodes[middle].Confirmed&&store.Setting("personal")=="kept","resource rollback preserves manual settings");
    string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database)));using(var cts=new CancellationTokenSource()){string big=Path.Combine(root,"cancel.csv");using(var w=new StreamWriter(big)){w.WriteLine("ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量");for(int i=0;i<2000;i++)w.WriteLine($"{i},甲{i},乙{i},1,1,2");}bool cancelled=false;try{await store.ImportAsync(big,"UTF-8",mapping,1,new ImmediateProgress(p=>{if(p.Read>=1000)cts.Cancel();}),cts.Token);}catch(OperationCanceledException){cancelled=true;}Check(cancelled&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),"mid-import cancellation preserves original database");}
    string invalid=Path.Combine(root,"invalid.csv");File.WriteAllText(invalid,"ID,始端设施\nx,甲\n");bool failed=false;try{await store.ImportAsync(invalid,"UTF-8",mapping,1,null,default);}catch(InvalidDataException){failed=true;}Check(failed&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),"failed import preserves original database");
    foreach(string enc in new[]{"GBK","GB18030"}){string f=Path.Combine(root,enc+".csv");File.WriteAllText(f,"ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量\n0001,中文甲,中文乙,1.25,1,2\n",Csv.EncodingFor(enc));await store.ImportAsync(f,enc,mapping,1000,null,default);Check(store.Graph.Nodes[0].Name=="中文甲"&&store.Graph.Edges[0].Length==1250,enc+" import + km conversion");}
    // Exhaustive oracle on small random multigraphs; each ordering is tested over all simple feasible paths.
    var random=new Random(93271);
    for(int trial=0;trial<100;trial++)
    {
        var ns=Enumerable.Range(0,6).Select(i=>new Node(i,"n"+i,i==2?"机房;站点":"光交箱",true)).ToArray();var es=new List<Edge>();
        for(int i=0;i<12;i++){int x=random.Next(6),y=random.Next(6);if(x==y)continue;es.Add(new(es.Count,"edge"+i,"",x,y,random.Next(6)==0?null:random.Next(5),random.Next(1,5),5,""));}
        var graph=new Graph(ns,es.ToArray(),"test");HashSet<string> avoidT=trial%2==0?["站点"]:[];HashSet<int> avoidN=trial%3==0?[3]:[];
        var all=new List<Route>();void Dfs(int n,List<int> path,List<int> edges){if(n==5){all.Add(new(path.ToArray(),edges.ToArray(),edges.Sum(i=>es[i].Length??0),0,0,edges.Min(i=>es[i].Free)));return;}if(edges.Count>=4)return;foreach(int ix in graph.Adjacency[n]){var e=es[ix];int next=e.A==n?e.B:e.A;if(path.Contains(next)||e.Free<2||avoidN.Contains(next)||(next!=5&&ns[next].Type.Split(';').Any(avoidT.Contains)))continue;path.Add(next);edges.Add(ix);Dfs(next,path,edges);path.RemoveAt(path.Count-1);edges.RemoveAt(edges.Count-1);}}
        Dfs(0,[0],[]);
        foreach(RouteOrder sort in Enum.GetValues<RouteOrder>())
        {
            double Score(Route x)=>sort switch{RouteOrder.Distance=>x.Length,RouteOrder.Capacity=>-x.Bottleneck,_=>x.Jumps};
            var actual=Routing.Find(graph,new(0,5,2,3,8,sort,avoidT,avoidN));var expected=all.OrderBy(Score).Take(8).Select(Score).ToArray();
            if(actual.Limited||!actual.Routes.Select(Score).SequenceEqual(expected))throw new Exception($"exhaustive mismatch {trial} {sort}");
        }
    }
    Check(true,"400 random multigraph ordering comparisons against exhaustive oracle");
    using(var rsa=RSA.Create(2048))
    {
        byte[] payload=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new ReleaseManifest(null,new("test","https://example.com/resources.csv",new string('0',64),100)));
        var sig=rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
        byte[] envelope=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(sig)});
        Check(Updates.VerifyEnvelope(envelope,rsa.ExportSubjectPublicKeyInfoPem()).Resources?.Version=="test","RSA-PSS signed manifest verification");
        sig[0]^=1;envelope=System.Text.Json.JsonSerializer.SerializeToUtf8Bytes(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(sig)});bool rejected=false;try{Updates.VerifyEnvelope(envelope,rsa.ExportSubjectPublicKeyInfoPem());}catch(InvalidDataException){rejected=true;}Check(rejected,"tampered manifest rejected");
    }
    string package=Path.Combine(root,"program.zip");
    void MakeZip(string extra){if(File.Exists(package))File.Delete(package);using var z=System.IO.Compression.ZipFile.Open(package,System.IO.Compression.ZipArchiveMode.Create);using(var w=new StreamWriter(z.CreateEntry("纤路R1/程序/FiberRouteR1.exe").Open()))w.Write("test");using(var w=new StreamWriter(z.CreateEntry(extra).Open()))w.Write("test");}
    MakeZip("纤路R1/第三方许可/LICENSE.txt");FiberRoute.Updater.PackageValidator.Extract(package,Path.Combine(root,"valid-package"));Check(File.Exists(Path.Combine(root,"valid-package","程序","FiberRouteR1.exe")),"program ZIP layout and license extraction");
    MakeZip("纤路R1/程序/../../escape.exe");bool traversal=false;try{FiberRoute.Updater.PackageValidator.Extract(package,Path.Combine(root,"bad-package"));}catch(InvalidDataException){traversal=true;}Check(traversal&&!File.Exists(Path.Combine(root,"escape.exe")),"update ZIP traversal rejected");
    MakeZip("纤路R1/导出结果/private.csv");bool blocked=false;try{FiberRoute.Updater.PackageValidator.Extract(package,Path.Combine(root,"bad-exports"));}catch(InvalidDataException){blocked=true;}Check(blocked,"update cannot overwrite personal exports");
    await ImportChecks.Run(root,Check);
    await R13Checks.Run(root,Check);
    await RoomChecks.Run(root,Check);
    await R14Checks.Run(root,Check);
await R141Checks.Run(root,Check);
await R143Checks.Run(root,Check);
await R144Checks.Run(root,Check);
    Console.WriteLine($"ALL PASS: {checks} checks");
}finally{Directory.Delete(root,true);}
sealed class ImmediateProgress(Action<ImportProgress> callback):IProgress<ImportProgress>{public void Report(ImportProgress value)=>callback(value);}
