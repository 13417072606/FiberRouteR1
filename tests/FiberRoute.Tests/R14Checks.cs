using FiberRoute.Core;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using System.Net;

static class R14Checks
{
    public static void Full(string directory,string baseline)
    {
        var watch=System.Diagnostics.Stopwatch.StartNew();var store=new Store(directory);long before=(long)Scalar(store.Personal,"SELECT count(*) FROM types")!;store.ApplyEvidenceBaseline(baseline);var g=store.Graph;
        var suspect=store.RoomAssignments().Where(x=>Store.SuspiciousRoom(x.Room)).ToArray();Console.WriteLine($"FULL DIAG: suspect={suspect.Length}, wall={suspect.Count(x=>x.Room.Contains("挂墙"))}, enabled={suspect.Count(x=>x.Enabled)}, nonreview={suspect.Count(x=>x.Status!="需核对")}");if(suspect.Count(x=>x.Room.Contains("挂墙"))!=146||suspect.Any(x=>x.Enabled||x.Status!="需核对"))throw new Exception("wall-location quarantine did not match actual source data");
        if(!store.EvidenceFor("汕头市龙湖区利宏达包装有限公司/ODB11").Any(x=>x.Source=="光分纤箱-1.CSV"&&x.Row==163942&&x.Field=="H列 · 机房名称"))throw new Exception("real source provenance does not match audited row");
        if((long)Scalar(store.Personal,"SELECT count(*) FROM types")! !=before)throw new Exception("evidence migration changed type records");
        int a=Array.FindIndex(g.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋2楼")&&n.Name.EndsWith("GF271")),b=Array.FindIndex(g.Nodes,n=>n.Name.Contains("家美社区雅源美宿公寓1栋4楼")&&n.Name.EndsWith("GF272"));
        if(!Routing.Find(g,new(a,b,1,1,8,RouteOrder.Distance,[],[])).Routes.Any(r=>Math.Abs(r.Length-208.21)<.001))throw new Exception("real 208.21 metre route changed");
        string start="汕头南澳县南澳综合楼汇聚点四楼机房传输1",end="汕头南澳县后宅汇聚点一楼机房传输1";
        if(!Routing.Find(g,new(g.Rooms[start][0],g.Rooms[end][0],1,8,8,RouteOrder.Distance,[],[])).Routes.Any(r=>r.Edges.Length==1&&Math.Abs(r.Length-2044.87)<.001))throw new Exception("real room folding route changed");
        Console.WriteLine($"FULL R1.4 PASS: edges={g.Edges.Length}; nodes={g.Nodes.Length}; explicit_node_areas={g.Nodes.Count(n=>n.Region.Length>0)}; quarantine={suspect.Length}; evidence={Scalar(store.Personal,"SELECT count(*) FROM facility_evidence")}; type_rows_preserved={before}; source_H163942=PASS; 208.21m=PASS; 2044.87m=PASS; elapsed_ms={watch.ElapsedMilliseconds}");
    }
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
    static object? Scalar(string path,string sql){using var c=new SqliteConnection("Data Source="+path+";Pooling=False");c.Open();using var q=c.CreateCommand();q.CommandText=sql;return q.ExecuteScalar();}
    public static async Task Run(string root,Action<bool,string> check)
    {
        string areaCsv=Path.Combine(root,"areas.csv");Csv.Write(areaCsv,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端区域","终端区域","始端机房","终端机房","业务状态"],["1","甲,设备/ODB01","乙/ODF01",12,3,8,"龙湖区","澄海区","挂墙001","乙机房","在网"]});
        var store=new Store(Path.Combine(root,"r14-evidence"));await store.ImportAsync(areaCsv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        check(store.Graph.Edges[0] is {StartRegion:"龙湖区",EndRegion:"澄海区",BusinessStatus:"在网"}&&store.Graph.Nodes[0].Region=="龙湖区","R1.4 resource area and business fields survive loading with both endpoint areas distinct");
        string types=Path.Combine(root,"R14类型.csv");Csv.Write(types,new object?[][]{["ID","设施名称","所属区县","机房名称","备注","备注"],["01","甲,设备/ODB01","金平区","挂墙001","第一行\n第二行","重复辅助列"]});
        store.ApplyRoomFiles([(types,"机房",null)]);var proof=store.EvidenceFor("甲,设备/ODB01").Single();check(proof.Row==2&&proof.Field=="D列 · 机房名称"&&proof.Region=="金平区"&&proof.Name=="甲,设备/ODB01","R1.4 full CSV parser preserves commas, multiline cells, duplicate auxiliary headers and exact source row");
        check(store.Graph.Nodes[0].Region=="金平区"&&store.RoomAssignments().First(x=>x.Name==proof.Name) is {Status:"需核对",Enabled:false},"typed area is displayed and wall-location room is quarantined rather than auto-confirmed");
        bool blocked=false;try{store.ConfirmRoom(proof.Name,"挂墙001",true);}catch(InvalidDataException){blocked=true;}check(blocked,"suspect wall location cannot be enabled as a room without correcting its source");
        string personalBefore=Hash(store.Personal);store.ApplyFacilityEvidence([(types,"机房",null)]);check(store.EvidenceFor(proof.Name).Length==1&&Hash(store.Personal)==personalBefore,"unchanged evidence import skips duplicate writes");check(!File.Exists(Path.Combine(store.Root,Path.GetFileName(types)))&&!File.Exists(Path.Combine(store.Root,Path.GetFileName(areaCsv))),"source CSV is not copied into the customer database directory");
        Csv.Write(types,new object?[][]{["设施名称","所属区县","机房名称"],[proof.Name,"潮阳区","甲机房"]});store.ApplyFacilityEvidence([(types,"机房",null)]);check(store.EvidenceFor(proof.Name) is [{Region:"潮阳区",Row:2}],"new same-source evidence replaces obsolete rows without accumulating copies");

        string serverCsv=Path.Combine(root,"server-resources.csv"),clientCsv=Path.Combine(root,"client-resources.csv");
        object?[] header=["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端机房","终端机房"];
        Csv.Write(clientCsv,new object?[][]{header,["a","甲/ODF01","乙/ODF01",10,1,4,"甲机房","乙机房"],["old","乙/ODF01","旧节点",20,1,4,"乙机房",""]});
        Csv.Write(serverCsv,new object?[][]{header,["a","甲/ODF01","乙/ODF01",15,2,4,"新甲机房","乙机房"],["b","乙/ODF01","丙",30,2,4,"乙机房",""],["b","乙/ODF01","丙",30,2,4,"乙机房",""]});
        var server=new Store(Path.Combine(root,"r14-server"));var client=new Store(Path.Combine(root,"r14-client"));foreach(var pair in new[]{(server,serverCsv),(client,clientCsv)})await pair.Item1.ImportAsync(pair.Item2,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        server.ApplyTypeAssignments([new("甲/ODF01","机房","类型表：官方.csv","已确认","")]);server.SetSetting("private-setting","ADMIN-PRIVATE-DO-NOT-PUBLISH-73921");client.SetSetting("private-setting","CUSTOMER-PREFERENCE");client.SetTypes([new("甲/ODF01","","光交箱","可更新")]);client.ConfirmRoom("甲/ODF01","甲机房",false);
        string package=Path.Combine(root,"data-update.zip");server.BuildDataPackage(package,"2026-11-01.1");
        using(var archive=System.IO.Compression.ZipFile.OpenRead(package)){check(archive.Entries.Select(e=>e.FullName).Order().SequenceEqual(new[]{"catalog.db","manifest.json","resources.db"}),"data publishing emits normalized databases and manifest without personal.db or original tables");using var entry=archive.GetEntry("catalog.db")!.Open();using var bytes=new MemoryStream();entry.CopyTo(bytes);check(!System.Text.Encoding.UTF8.GetString(bytes.ToArray()).Contains("ADMIN-PRIVATE-DO-NOT-PUBLISH"),"published catalogue contains no administrator settings or residual private setting bytes");}
        string resourceHash=Hash(client.Database),personalHash=Hash(client.Personal);
        using(var preview=client.PrepareDataPackage(package,"2026-11-01.1"))
        {
            check(preview.Added==1&&preview.Changed==1&&preview.Removed==1&&preview.Manifest.DuplicatesIgnored==1&&preview.ManualConflicts==1,"data preview detects adds, changes, removals, exact duplicates and human type conflict");check(Hash(client.Database)==resourceHash&&Hash(client.Personal)==personalHash,"preparing data update never changes either live database");client.ApplyDataPackage(preview);
        }
        check(client.Graph.Edges.Length==2&&client.Graph.Nodes.Single(n=>n.Name=="甲/ODF01") is {Type:"光交箱",Status:"需核对"}&&client.Setting("private-setting")=="CUSTOMER-PREFERENCE","online data apply replaces resource snapshot and preserves customer type and settings");
        check((long)Scalar(client.Personal,"SELECT count(*) FROM room_choices WHERE name='甲/ODF01' AND room='甲机房' AND enabled=0")! ==1&&client.RoomAssignments().Single(x=>x.Name=="甲/ODF01").Status=="需核对","customer room choice survives a conflicting server affiliation and is exposed for review");
        using(var repeated=client.PrepareDataPackage(package,"2026-11-01.1")){check(!repeated.HasChanges&&repeated.Unchanged==2,"identical subsequent data update ignores existing unchanged records");client.ApplyDataPackage(repeated);}
        using(var cancelled=new CancellationTokenSource()){cancelled.Cancel();bool rejected=false;try{client.PrepareDataPackage(package,ct:cancelled.Token);}catch(OperationCanceledException){rejected=true;}check(rejected&&client.Graph.Edges.Length==2,"cancelled data preparation preserves the active resource snapshot");}

        // Room-only changes in resource auxiliary fields must count as a changed resource.
        Scalar(server.Database,"UPDATE edges SET raw=json_set(raw,'$.始端机房','更新后的归属') WHERE business_id='a'");Scalar(server.Database,"UPDATE rooms SET room='更新后的归属' WHERE name='甲/ODF01'");server.Reload();server.BuildDataPackage(package,"2026-11-02.0");using(var roomOnly=client.PrepareDataPackage(package)){check(roomOnly.Changed==1,"resource room-only auxiliary change is included in data diff instead of being ignored");client.ApplyDataPackage(roomOnly);}check(client.RoomAssignments().Single(x=>x.Name=="甲/ODF01").Room=="更新后的归属","resource room-only update applies the new source while retaining human conflict for review");
        server.SaveClusters([new("manual-test","管理方集群","机房",["甲/ODF01","乙/ODF01"],"集群表：测试.xlsx","已确认","",true)]);client.SaveClusters([new("manual-test","客户集群","机房",["乙/ODF01","丙"],"人工修改","已确认","",true)]);server.BuildDataPackage(package,"2026-11-02.1");
        using(var update=client.PrepareDataPackage(package)){client.ApplyDataPackage(update);}var humanCluster=client.Clusters().Single();check(humanCluster.Source=="人工修改"&&humanCluster.Members.Contains("丙")&&humanCluster.Status=="需核对"&&!humanCluster.Enabled,"cloud cluster conflict preserves human members and visibly pauses logical merge");
        // A removed source must not make the customer's explicit room decision vanish.
        Scalar(client.Database,"DELETE FROM rooms WHERE name='甲/ODF01'");client.Reload();check(client.RoomAssignments().Single(x=>x.Name=="甲/ODF01") is {Status:"需核对",Enabled:false},"removed affiliation source retains customer choice for review instead of silently dropping it");
        server.BuildDataPackage(package,"2026-11-01.1");
        string workspace=Path.Combine(root,"publisher");Publishing.Initialize(workspace,"https://updates.example.test/fiber/");check(Updates.LoadConfig(Path.Combine(workspace,"client-config","update-service.json")) is {ManifestUrl:"https://updates.example.test/fiber/manifest.json"}&&Updates.LoadConfig(Path.Combine(root,"absent-config.json"))==null,"publisher creates public client configuration and missing service remains explicitly unconfigured");
        Publishing.Release(workspace,"data","2026-11-01.1",package,"测试数据更新");var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))!;byte[] signed=File.ReadAllBytes(Path.Combine(workspace,"site","manifest.json"));var release=Updates.VerifyEnvelope(signed,config.PublicKeyPem);check(release.Resources?.Version=="2026-11-01.1"&&release.Program==null,"management release produces client-verifiable signed independent data channel");
        using var transport=new HttpClient(new MemoryHandler(uri=>uri.AbsolutePath.EndsWith("manifest.json")?signed:File.ReadAllBytes(package)));
        var manifest=await Updates.Check(config,default,transport);string cache=Path.Combine(root,"download-cache");string downloaded=await Updates.Download(config,manifest.Resources!,cache,default,transport:transport);check(Hash(downloaded)==Hash(package),"online check and download validate signed release, declared bytes and content hash");File.Delete(downloaded);
        bool badHash=false;try{await Updates.Download(config,manifest.Resources! with{Sha256=new string('0',64)},cache,default,transport:transport);}catch(InvalidDataException){badHash=true;}check(badHash&&!Directory.GetFiles(cache).Any(),"invalid download hash is rejected and temporary package cleaned");
        bool foreign=false;try{await Updates.Download(config,manifest.Resources! with{Url="https://other.example.test/file.zip"},cache,default,transport:transport);}catch(InvalidDataException){foreign=true;}check(foreign,"update download cannot silently redirect to an unrelated origin");
        check(Updates.IsNewProgram("1.4.1","1.4.0")&&!Updates.IsNewProgram("1.4.0","1.4.0")&&!Updates.Compatible(manifest.Resources! with{MinimumProgram="1.5.0"},"1.4.0"),"program version and data compatibility checks distinguish new, current and incompatible releases");
    }
    sealed class MemoryHandler(Func<Uri,byte[]> content):HttpMessageHandler
    {protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken cancellationToken)=>Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(content(request.RequestUri!))});}
}
