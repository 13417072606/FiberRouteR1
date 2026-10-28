using FiberRoute.Core;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

static class R145Checks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        Dictionary<string,string> map=Store.Fields.ToDictionary(f=>f,f=>f);
        async Task<Store> Create(string label,params string[] names)
        {
            string folder=Path.Combine(root,"r145-"+label);Directory.CreateDirectory(folder);string path=Path.Combine(folder,"resources.csv");
            Csv.Write(path,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(names.Select((n,i)=>new object?[]{i.ToString(),n,"路由对端"+i,10,2,4})));
            var s=new Store(Path.Combine(folder,"data"));await s.ImportAsync(path,"自动",map,1,null,default);return s;
        }
        string cabinet="南澳区财政局FG/GJ01",a="汕头市南澳县财政局宿舍B栋2梯FGL01",b="汕头市南澳县财政局宿舍A栋2梯FGL02",site="汕头市南澳县后宅镇育新路财政局宿舍-1";
        var finance=await Create("finance",cabinet,a,b);string cabinets=Path.Combine(finance.Root,"光交接箱.csv"),splitters=Path.Combine(finance.Root,"光分纤箱.csv"),table=Path.Combine(finance.Root,"集群管理.csv");
        Csv.Write(cabinets,new object?[][]{["设施名称","站点名称/位置点"],[cabinet,site]});Csv.Write(splitters,new object?[][]{["设施名称","站点名称/位置点"],[a,site],[b,site]});finance.ApplyStationFiles([(cabinets,"光交箱",null),(splitters,"分纤箱",null)]);
        finance.ApplyTypeAssignments([new(cabinet,"光交箱","类型表：光交接箱.csv","已确认",""),new(a,"分纤箱","类型表：光分纤箱.csv","已确认",""),new(b,"分纤箱","类型表：光分纤箱.csv","已确认","")]);
        Csv.Write(table,new object?[][]{["设备名称","集群类型","集群列表"],[cabinet,"光交接箱",cabinet],[a,"光分纤箱",a+","+b],[b,"光分纤箱",a+","+b],["独立GJ99","光交箱","独立GJ99"]});finance.SaveClusterImport(table);var cluster=finance.Clusters().Single();
        check(cluster.Members.ToHashSet().SetEquals(new[]{cabinet,a,b})&&cluster.Status=="已确认"&&cluster.Enabled&&cluster.Station==site,"R145 finance cabinet plus two splitters form one three-member cluster across types");
        check(cluster.Type.Split(';').Contains("光交箱")&&cluster.Type.Split(';').Contains("分纤箱")&&finance.FilteredClusters.Any(x=>x.Name=="独立GJ99"),"R145 mixed member types are retained and unresolved singleton is filtered");
        check(new[]{cabinet,a,b}.Select(n=>finance.Graph.Logical[finance.Graph.Nodes.Single(x=>x.Name==n).Id]).Distinct().Count()==1,"R145 cross-type site membership reaches the actual routing graph");

        string one="潮阳区宝华苑(三网)FG/GJ01",two="潮阳区宝华苑(三网)FG/GJ02",three="潮阳区宝华苑(三网)FG/GJ03";
        var bao=await Create("baohua",one);bao.ApplyTypeAssignments([new(two,"光交箱","类型表：光交接箱.csv","已确认",""),new(three,"光交箱","类型表：光交接箱.csv","已确认","")]);
        string members=string.Join(',',new[]{two,one,three});string baoTable=Path.Combine(bao.Root,"宝华苑.csv");Csv.Write(baoTable,new object?[][]{["设备名称","集群类型","集群列表"],[three,"光交箱",members],[two,"光交箱",members],[one,"光交箱",members]});bao.SaveClusterImport(baoTable);var baohua=bao.Clusters().Single();
        check(baohua.Members.Length==3&&baohua.Status=="已确认"&&!baohua.Reason.Contains("均未找到"),"R145 Baohua GJ02/GJ03 in facility type catalog are not falsely missing after deduplication");
        check(!baohua.Enabled&&baohua.Reason.Contains("不足两个")&&bao.Graph.Nodes.All(n=>n.Name!=two&&n.Name!=three),"R145 source-only members remain visible without inventing graph nodes or routable capacity");
        // The source table itself is independent evidence, even before facilities arrive.
        var tableOnly=await Create("table-only","普通端");string refs=Path.Combine(tableOnly.Root,"source.csv");Csv.Write(refs,new object?[][]{["设备名称","集群列表"],["表中设施甲","表中设施甲,表中设施乙"],["表中设施乙","表中设施甲,表中设施乙"]});tableOnly.SaveClusterImport(refs);check(tableOnly.Clusters().Single() is {Status:"已确认",Enabled:false},"R145 all source device rows are indexed before group deduplication");

        string normal="莲下环PX01/GJ01-1",retired="莲下环PX01/GJ01-1（废弃，现场已合并）";
        var lian=await Create("legacy-lian",normal);lian.ApplyTypeAssignments([new(retired,"光交箱","类型表：光交接箱.csv","已确认","")]);lian.SaveClusters([new("legacy",retired,"光交箱",["现场已合并）",normal,"莲下环PX01/GJ01-1（废弃"],"集群表：集群管理.xlsx","需核对","设备名称不在集群列表中",false)]);
        var repaired=lian.Clusters().Single();check(repaired.Members.ToHashSet().SetEquals(new[]{normal,retired})&&repaired.Status=="已确认","R145 existing broken comma fragments recover only against the exact complete catalog name");
        string lianTable=Path.Combine(lian.Root,"new.csv");Csv.Write(lianTable,new object?[][]{["设备名称","集群列表"],[retired,retired+","+normal],[normal,retired+","+normal]});lian.SaveClusterImport(lianTable);check(lian.Clusters().Single().Members.Length==2&&lian.ParseClusterMemberText(retired+"\r\n"+normal).Length==2,"R145 new imports and editor preserve the comma inside retired-name parentheses");

        string wrappedTwo="潮阳区宝华苑（三网）FG/\r\nGJ02";Csv.Write(baoTable,new object?[][]{["设备名称","集群列表"],[one,one+","+wrappedTwo+","+three],[wrappedTwo,members],[three,members]});bao.SaveClusterImport(baoTable);check(bao.Clusters().Single().Members.ToHashSet().SetEquals(new[]{one,two,three}),"R145 physical line breaks and full-width punctuation match a complete facility identity without splitting it");

        var chain=await Create("union","A/GJ01","B/GF01","C/GF02");string chainTable=Path.Combine(chain.Root,"chain.csv");Csv.Write(chainTable,new object?[][]{["设备名称","集群类型","集群列表"],["A/GJ01","光交箱","A/GJ01,B/GF01"],["B/GF01","分纤箱","B/GF01,C/GF02"]});chain.SaveClusterImport(chainTable);check(chain.Clusters().Single() is {Status:"已确认",Members.Length:3,Enabled:true},"R145 overlapping automatic source rows union their members instead of losing members or falsely reporting overlap");
        chain.SaveClusters([new("manual","人工暂停","光交箱",chain.Clusters().Single().Members,"人工修改","已确认","",false)]);check(chain.Clusters().Single() is {Id:"manual",Source:"人工修改",Enabled:false,Status:"已确认"},"R145 manual suspension survives automatic regrouping of the same members");

        Csv.Write(baoTable,new object?[][]{["设备名称","集群列表"],[one,one+",确实不存在的设备"]});bao.SaveClusterImport(baoTable);check(bao.Clusters().Single() is {Status:"需核对",Enabled:false},"R145 an unsupported member mention alone does not become evidence of a device");

        var sync=await Create("station-sync","GJ01","GF01","GF02");string stations=Path.Combine(sync.Root,"设施.csv"),seed=Path.Combine(sync.Root,"seed.csv");Csv.Write(stations,new object?[][]{["设施名称","所属站点"],["GJ01","站甲"],["GF01","站甲"],["GF02","站甲"]});sync.ApplyStationFiles([(stations,"",null)]);Csv.Write(seed,new object?[][]{["设备名称","集群列表"],["GJ01","GJ01"]});sync.SaveClusterImport(seed);
        string package=Path.Combine(sync.Root,"catalog.zip");var client=new Store(Path.Combine(root,"r145-sync-client"));sync.BuildDataPackage(package,"145.1");using(var update=client.PrepareDataPackage(package))client.ApplyDataPackage(update);check(client.Clusters().Single().Members.Length==3,"R145 online data contains the original cluster rows and station evidence");
        Csv.Write(stations,new object?[][]{["设施名称","所属站点"],["GJ01","站甲"],["GF01","站甲"]});sync.ApplyStationFiles([(stations,"",null)]);sync.BuildDataPackage(package,"145.2");using(var update=client.PrepareDataPackage(package))client.ApplyDataPackage(update);check(sync.Clusters().Single().Members.Length==2&&client.Clusters().Single().Members.Length==2,"R145 reimport and subsequent online sync remove obsolete site membership instead of keeping stale expansions");
        Csv.Write(seed,new object?[][]{["设备名称","集群列表"],["GJ01","GJ01,GF01"]});sync.SaveClusterImport(seed);
        Csv.Write(stations,new object?[][]{["设施名称","所属站点"],["GJ01","站甲"],["GJ01","站乙"],["GF01","站甲"]});sync.ApplyStationFiles([(stations,"",null)]);check(sync.Clusters().Single() is {Status:"需核对",Enabled:false},"R145 genuinely conflicting station assignments remain reviewable and cannot silently enable routing");
    }
    public static void Full(string original,string inputs,string output)
    {
        string root=Path.Combine(output,"legacy");Directory.CreateDirectory(root);foreach(string name in new[]{"resources.db","personal.db"})File.Copy(Path.Combine(original,name),Path.Combine(root,name),true);
        var watch=System.Diagnostics.Stopwatch.StartNew();var store=new Store(root);string resourceHash=Hash(store.Database);var before=store.Clusters();
        void Cases(string label)
        {
            var all=store.Clusters();var bao=all.Single(x=>x.Members.Contains("潮阳区宝华苑(三网)FG/GJ03"));var lian=all.Single(x=>x.Members.Contains("莲下环PX01/GJ01-1（废弃，现场已合并）"));
            Console.WriteLine(label+" BAO "+JsonSerializer.Serialize(bao));Console.WriteLine(label+" LIAN "+JsonSerializer.Serialize(lian));
            if(!new[]{"潮阳区宝华苑(三网)FG/GJ01","潮阳区宝华苑(三网)FG/GJ02","潮阳区宝华苑(三网)FG/GJ03"}.All(bao.Members.Contains)||bao.Status!="已确认"||!lian.Members.Contains("莲下环PX01/GJ01-1")||lian.Members.Any(n=>n=="现场已合并）"||n.EndsWith("（废弃")))throw new Exception(label+" cases failed");
            Console.WriteLine($"{label}: clusters={all.Count}, confirmed={all.Count(x=>x.Status=="已确认")}, review={all.Count(x=>x.Status=="需核对")}, filtered={store.FilteredClusters.Count}; Baohua={bao.Members.Length} confirmed; Lianxia={lian.Members.Length} confirmed");
        }
        Cases("LEGACY before station repair");
        store.ApplyStationFiles(new[]{"光交接箱-1.CSV","光分纤箱-1.CSV"}.Select(n=>(Path.Combine(inputs,n),"",(string?)null)));Cases("LEGACY with original station tables");
        var finance=store.Clusters().Single(x=>x.Members.Contains("南澳区财政局FG/GJ01"));if(finance.Members.Length!=3||finance.Status!="已确认")throw new Exception("Finance must have exactly three members");
        Console.WriteLine("FINANCE: "+JsonSerializer.Serialize(finance));
        var migrated=store.Clusters().Select(x=>string.Join('\n',x.Members.Order(StringComparer.Ordinal))).ToHashSet();
        string baseline=Path.Combine(output,"集群站点基线-R1.4.5.db");if(File.Exists(baseline))File.Delete(baseline);
        using(var c=new SqliteConnection($"Data Source={baseline}")){c.Open();using var q=c.CreateCommand();q.CommandText="ATTACH DATABASE $p AS src";q.Parameters.AddWithValue("$p",store.Personal);q.ExecuteNonQuery();foreach(string name in new[]{"facility_stations","station_sources"}){q.CommandText=$"CREATE TABLE {name} AS SELECT * FROM src.{name}";q.ExecuteNonQuery();}q.CommandText="CREATE TABLE cluster_inputs(source TEXT,rownum INTEGER,name TEXT,type TEXT,members TEXT,station TEXT);VACUUM";q.ExecuteNonQuery();}
        store.SaveClusterImport(Path.Combine(inputs,"集群管理.xlsx"));Cases("FRESH import");var fresh=store.Clusters().Select(x=>string.Join('\n',x.Members.Order(StringComparer.Ordinal))).ToHashSet();if(!migrated.SetEquals(fresh))throw new Exception("Legacy repair and fresh import membership sets differ");Console.WriteLine($"MIGRATION/FRESH membership: legacy={migrated.Count}; fresh={fresh.Count}; old-only={migrated.Except(fresh).Count()}; fresh-only={fresh.Except(migrated).Count()}");
        var all=store.Clusters();Csv.Write(Path.Combine(output,"集群核查结果.csv"),new[]{new object?[]{"集群名称","后台所属站点","成员数","类型","状态","参与路由","原因","完整成员"}}.Concat(all.Select(x=>new object?[]{x.Name,x.Station,x.Members.Length,x.Type,x.Status,x.Enabled,x.Reason,string.Join('；',x.Members)})));
        if(Hash(store.Database)!=resourceHash)throw new Exception("Original resource bytes changed");Console.WriteLine($"FULL R145 PASS: resource DB bytes untouched; elapsed_ms={watch.ElapsedMilliseconds}; metadata baseline prepared from existing repository input tables");
    }
    static string Hash(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
}
