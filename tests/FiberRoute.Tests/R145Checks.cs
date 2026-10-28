using FiberRoute.Core;
using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;

static class R145Checks
{
    const string Cabinet="南澳区财政局FG/GJ01",SplitA="汕头市南澳县财政局宿舍A栋2梯FGL02",SplitB="汕头市南澳县财政局宿舍B栋2梯FGL01";
    const string FinanceSite="汕头市南澳县后宅镇育新路财政局宿舍-1";
    const string Lian="莲下环PX01/GJ01-1",Retired="莲下环PX01/GJ01-1（废弃，现场已合并）";
    const string Bao="潮阳区宝华苑(三网)FG/GJ";
    static void ClusterCsv(string path,params (string Name,string Type,string Members,string Station)[] rows)
        =>Csv.Write(path,new[]{new object?[]{"设备名称","设备类型","集群列表","所属站点"}}.Concat(rows.Select(x=>new object?[]{x.Name,x.Type,x.Members,x.Station})));
    static void Stations(string path,params (string Name,string Site)[] rows)
        =>Csv.Write(path,new[]{new object?[]{"设施名称","站点名称/位置点"}}.Concat(rows.Select(x=>new object?[]{x.Name,x.Site})));
    public static async Task Run(string root,Action<bool,string> check)
    {
        string folder=Path.Combine(root,"r145");Directory.CreateDirectory(folder);
        async Task<Store> Create(string label,params string[] names)
        {
            string path=Path.Combine(folder,label+".csv");Csv.Write(path,new[]{new object?[]{"ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"}}.Concat(names.Select((n,i)=>new object?[]{label+i,n,"路由对端"+i,10,2,4})));
            var s=new Store(Path.Combine(folder,label));await s.ImportAsync(path,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);return s;
        }
        var finance=await Create("finance",Cabinet,SplitA,SplitB);string cab=Path.Combine(folder,"光交接箱.csv"),split=Path.Combine(folder,"光分纤箱.csv");
        Stations(cab,(Cabinet,FinanceSite));Stations(split,(SplitA,FinanceSite),(SplitB,FinanceSite));finance.ApplyStationFiles([(cab,"光交箱",null),(split,"分纤箱",null)]);
        finance.ApplyTypeAssignments([new(Cabinet,"光交箱","类型表","已确认",""),new(SplitA,"分纤箱","类型表","已确认",""),new(SplitB,"分纤箱","类型表","已确认","")]);
        string table=Path.Combine(folder,"finance-clusters.csv");ClusterCsv(table,(Cabinet,"光交接箱",Cabinet,""),(SplitA,"光分纤箱",SplitA+";"+SplitB,""),(SplitB,"光分纤箱",SplitA+";"+SplitB,""),("孤立GJ99","光交箱","孤立GJ99",""));finance.SaveClusterImport(table);
        var fc=finance.Clusters().Single();
        check(fc.Members.Length==3&&fc.Members.Contains(Cabinet)&&fc.Members.Contains(SplitA)&&fc.Members.Contains(SplitB)&&fc.Station==FinanceSite&&fc.Status=="已确认"&&fc.Enabled,"R145 same backend site unifies cabinet and two splitters");
        check(fc.Type.Split(';').Contains("光交箱")&&fc.Type.Split(';').Contains("分纤箱")&&finance.FilteredClusters.Any(x=>x.Name=="孤立GJ99"),"R145 mixed types retained and only post-merge isolated member filtered");
        check(finance.Graph.Logical[finance.Graph.Nodes.Single(n=>n.Name==Cabinet).Id]==finance.Graph.Logical[finance.Graph.Nodes.Single(n=>n.Name==SplitA).Id]&&finance.Graph.Logical[finance.Graph.Nodes.Single(n=>n.Name==Cabinet).Id]==finance.Graph.Logical[finance.Graph.Nodes.Single(n=>n.Name==SplitB).Id],"R145 confirmed same-site members join the routing group");

        var bao=await Create("bao",Bao+"01");bao.ApplyTypeAssignments([new(Bao+"02","光交箱","类型表","已确认",""),new(Bao+"03","光交箱","类型表","已确认","")]);
        string bt=Path.Combine(folder,"bao-clusters.csv");string list=string.Join(';',new[]{Bao+"01",Bao+"02",Bao+"03"});ClusterCsv(bt,Enumerable.Range(1,3).Select(i=>(Bao+i.ToString("00"),"光交接箱",list,"")).ToArray());bao.SaveClusterImport(bt);var bc=bao.Clusters().Single();
        check(bc.Members.Length==3&&bc.Status=="已确认"&&!bc.Reason.Contains("均未找到"),"R145 type catalog prevents false missing Bao members");
        check(!bc.Enabled&&bc.Reason.Contains("不足两个")&&!bao.Graph.Nodes.Any(n=>n.Name==Bao+"02"||n.Name==Bao+"03"),"R145 source-only devices remain confirmed without fabricated routable nodes");

        var tableOnly=await Create("table-only","无关节点");string tt=Path.Combine(folder,"table-only-clusters.csv");ClusterCsv(tt,("甲","光交箱","甲;乙",""),("乙","光交箱","甲;乙",""));tableOnly.SaveClusterImport(tt);
        check(tableOnly.Clusters().Single() is {Status:"已确认",Enabled:false}&&tableOnly.Clusters()[0].Members.Length==2,"R145 actual source rows count as existence evidence independently of routes");

        var legacy=await Create("legacy-lian",Lian);legacy.ApplyTypeAssignments([new(Retired,"光交箱","类型表","已确认","")]);legacy.SaveClusters([new("old","莲下环PX01/GJ01-1（废弃，现场已合并）","光交箱",["现场已合并）",Lian,"莲下环PX01/GJ01-1（废弃"],"集群表：集群管理.xlsx","需核对","设备名称不在集群列表中",false)]);
        var lc=legacy.Clusters().Single();check(lc.Members.Length==2&&lc.Members.Contains(Retired)&&lc.Members.Contains(Lian)&&lc.Status=="已确认","R145 repairs persisted comma fragments against complete known names");
        string lt=Path.Combine(folder,"lian-clusters.csv");ClusterCsv(lt,(Retired,"光交箱",Retired+","+Lian,""),(Lian,"光交箱",Retired+","+Lian,""));legacy.SaveClusterImport(lt);
        check(legacy.Clusters().Single().Members.Length==2&&legacy.ParseClusterMemberText(Retired+"\r\n"+Lian).Length==2,"R145 future imports and manual text preserve commas inside parentheses");

        string wrapped="潮阳区宝华苑（三网）FG/\r\nGJ02";ClusterCsv(bt,(Bao+"01","光交箱",Bao+"01;"+wrapped+";"+Bao+"03",""),(Bao+"02","光交箱",list,""),(Bao+"03","光交箱",list,""));bao.SaveClusterImport(bt);
        check(bao.Clusters().Single().Members.Order(StringComparer.Ordinal).SequenceEqual(new[]{Bao+"01",Bao+"02",Bao+"03"}),"R145 joins wrapped names only when a complete normalized reference matches");

        var chain=await Create("chain","A/GJ01","B/GF01","C/GF02");string chainTable=Path.Combine(folder,"chain-clusters.csv");ClusterCsv(chainTable,("A/GJ01","光交箱","A/GJ01;B/GF01",""),("B/GF01","分纤箱","B/GF01;C/GF02",""));chain.SaveClusterImport(chainTable);var cc=chain.Clusters().Single();
        check(cc.Members.Length==3&&cc.Status=="已确认"&&cc.Enabled,"R145 overlapping automatic relations union all members without dedup loss");
        chain.SaveClusters([new("manual",cc.Name,cc.Type,cc.Members,"人工修改","已确认","",false)]);
        check(chain.Clusters().Single() is {Id:"manual",Enabled:false,Source:"人工修改"},"R145 equivalent manually suspended relation retains its identity and priority");

        ClusterCsv(bt,(Bao+"01","光交箱",Bao+"01;真正不存在的成员",""));bao.SaveClusterImport(bt);
        check(bao.Clusters().Single() is {Status:"需核对",Enabled:false},"R145 member mentions without source rows remain reviewable");

        var sync=await Create("sync","GJ01","GF01","GF02");string st=Path.Combine(folder,"站点来源.csv"),ct=Path.Combine(folder,"sync-clusters.csv");
        Stations(st,("GJ01","站甲"),("GF01","站甲"),("GF02","站甲"));sync.ApplyStationFiles([(st,"",null)]);ClusterCsv(ct,("GJ01","光交箱","GJ01",""));sync.SaveClusterImport(ct);
        string package=Path.Combine(folder,"r145-data.zip");sync.BuildDataPackage(package,"r145.1");var client=new Store(Path.Combine(folder,"sync-client"));using(var prepared=client.PrepareDataPackage(package))client.ApplyDataPackage(prepared);
        check(client.Clusters().Single().Members.Length==3,"R145 online data import carries station-derived membership");
        Stations(st,("GJ01","站甲"),("GF01","站甲"));sync.ApplyStationFiles([(st,"",null)]);sync.BuildDataPackage(package,"r145.2");using(var prepared=client.PrepareDataPackage(package))client.ApplyDataPackage(prepared);
        check(sync.Clusters().Single().Members.Length==2&&client.Clusters().Single().Members.Length==2,"R145 subsequent online data removes obsolete station-only evidence");
        ClusterCsv(ct,("GJ01","光交箱","GJ01;GF01",""));sync.SaveClusterImport(ct);Stations(st,("GJ01","站甲"),("GJ01","站乙"),("GF01","站甲"));sync.ApplyStationFiles([(st,"",null)]);
        check(sync.Clusters().Single() is {Status:"需核对",Enabled:false},"R145 conflicting station evidence stays paused for review");
    }
    public static void Full(string original,string inputs,string output)
    {
        Directory.CreateDirectory(output);string legacy=Path.Combine(output,"legacy");Directory.CreateDirectory(legacy);
        foreach(string file in new[]{"resources.db","personal.db"})File.Copy(Path.Combine(original,file),Path.Combine(legacy,file),true);
        var s=new Store(legacy);string hash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s.Database)));
        void Cases(string label)
        {
            var groups=s.Clusters();var bao=groups.Single(c=>c.Members.Contains(Bao+"03"));var lian=groups.Single(c=>c.Members.Contains(Retired));
            if(!Enumerable.Range(1,3).All(i=>bao.Members.Contains(Bao+i.ToString("00")))||bao.Status!="已确认")throw new Exception("Bao source members lost or falsely missing");
            if(lian.Members.Length!=2||!lian.Members.Contains(Lian)||lian.Members.Any(n=>n=="现场已合并）"||n=="莲下环PX01/GJ01-1（废弃"))throw new Exception("Lian full names not restored");
            Console.WriteLine($"{label}: clusters={groups.Count}; confirmed={groups.Count(c=>c.Status=="已确认")}; review={groups.Count(c=>c.Status=="需核对")}; filtered={s.FilteredClusters.Count}; bao={bao.Members.Length}; lian={lian.Members.Length}");
            Console.WriteLine(JsonSerializer.Serialize(bao));Console.WriteLine(JsonSerializer.Serialize(lian));
        }
        Cases("legacy-before-stations");
        s.ApplyStationFiles(new[]{"光交接箱-1.CSV","光分纤箱-1.CSV"}.Select(n=>(Path.Combine(inputs,n),"",(string?)null)));Cases("legacy-with-stations");
        var finance=s.Clusters().Single(c=>c.Members.Contains(Cabinet));
        if(finance.Members.Length!=3||finance.Status!="已确认"||!finance.Members.Contains(SplitA)||!finance.Members.Contains(SplitB))throw new Exception("Finance must contain exactly the cabinet and two splitters");
        Console.WriteLine(JsonSerializer.Serialize(finance));
        string[] Sets()=>s.Clusters().Select(c=>JsonSerializer.Serialize(c.Members.Order(StringComparer.Ordinal))).Order(StringComparer.Ordinal).ToArray();var migrated=Sets();
        string baseline=Path.Combine(output,"集群站点基线-R1.4.5.db");if(File.Exists(baseline))File.Delete(baseline);
        using(var c=new SqliteConnection($"Data Source={baseline}"))
        {
            c.Open();using var q=c.CreateCommand();q.CommandText="ATTACH DATABASE $p AS src";q.Parameters.AddWithValue("$p",s.Personal);q.ExecuteNonQuery();
            foreach(string table in new[]{"facility_stations","station_sources"}){q.CommandText=$"CREATE TABLE {table} AS SELECT * FROM src.{table}";q.ExecuteNonQuery();}
            q.CommandText="CREATE TABLE cluster_inputs(source TEXT,rownum INTEGER,name TEXT,type TEXT,members TEXT,station TEXT); VACUUM";q.ExecuteNonQuery();
        }
        s.SaveClusterImport(Path.Combine(inputs,"集群管理.xlsx"));Cases("fresh-import");
        if(!migrated.SequenceEqual(Sets()))throw new Exception("Legacy repair and fresh import differ in complete membership sets");
        Csv.Write(Path.Combine(output,"集群核查结果.csv"),new[]{new object?[]{"集群名称","后台所属站点","成员数","类型","状态","参与路由","原因","完整成员"}}.Concat(s.Clusters().Select(c=>new object?[]{c.Name,c.Station,c.Members.Length,c.Type,c.Status,c.Enabled,c.Reason,string.Join('；',c.Members)})));
        if(hash!=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(s.Database))))throw new Exception("Resource database was modified");
        Console.WriteLine("FULL R145 PASS: identical migrated/fresh memberships; resource bytes untouched; baseline="+baseline);
    }
}
