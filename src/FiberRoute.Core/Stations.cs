using Microsoft.Data.Sqlite;
using System.Text;
using System.Text.Json;
using System.Security.Cryptography;
namespace FiberRoute.Core;
public sealed record ClusterInput(string Name,string Type,string[] Members,string Source,int Row,string Station="");
public sealed partial class Store
{
    static void EnsureStations(SqliteConnection c)=>Run(c,"CREATE TABLE IF NOT EXISTS facility_stations(name TEXT,station TEXT,source TEXT,rownum INTEGER,PRIMARY KEY(name,source,rownum)) WITHOUT ROWID; CREATE TABLE IF NOT EXISTS station_sources(source TEXT PRIMARY KEY,signature TEXT); CREATE TABLE IF NOT EXISTS cluster_inputs(source TEXT,rownum INTEGER,name TEXT,type TEXT,members TEXT,station TEXT,PRIMARY KEY(source,rownum)) WITHOUT ROWID");
    public static string ClassifyType(string name,string type)
    {
        if(name.Contains("POS",StringComparison.OrdinalIgnoreCase))return "分纤箱";
        var tags=type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(NormalizeType).Distinct().ToArray();
        return tags.Length==0?"未识别":string.Join(';',tags);
    }
    public void ApplyStationFiles(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        using var c=Open(Personal);EnsureStations(c);using var tx=c.BeginTransaction();
        foreach(var file in files)
        {
            string source=Path.GetFileName(file.Path);using var stream=File.OpenRead(file.Path);string signature=Convert.ToHexString(SHA256.HashData(stream))+"|"+file.Sheet;
            using var old=c.CreateCommand();old.Transaction=tx;old.CommandText="SELECT signature FROM station_sources WHERE source=$s";old.Parameters.AddWithValue("$s",source);if((string?)old.ExecuteScalar()==signature)continue;
            using var rows=ReadTypeRows(file.Path,file.Sheet).GetEnumerator();if(!rows.MoveNext())continue;var h=rows.Current.Select(x=>x.Trim()).ToArray();int ni=Array.FindIndex(h,x=>x is "设施名称" or "设备名称" or "节点名称"),si=Array.FindIndex(h,x=>x is "站点名称/位置点" or "所属站点");if(ni<0||si<0)continue;
            using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="DELETE FROM facility_stations WHERE source=$s";q.Parameters.AddWithValue("$s",source);q.ExecuteNonQuery();q.CommandText="INSERT OR REPLACE INTO facility_stations VALUES($n,$v,$s,$r)";q.Parameters.AddWithValue("$n","");q.Parameters.AddWithValue("$v","");q.Parameters.AddWithValue("$r",0);int row=1;
            while(rows.MoveNext()){ct.ThrowIfCancellationRequested();row++;var cells=rows.Current;if(ni>=cells.Length||string.IsNullOrWhiteSpace(cells[ni]))continue;q.Parameters["$n"].Value=cells[ni].Trim();q.Parameters["$v"].Value=si<cells.Length?cells[si].Trim():"";q.Parameters["$r"].Value=row;q.ExecuteNonQuery();}
            using var put=c.CreateCommand();put.Transaction=tx;put.CommandText="INSERT OR REPLACE INTO station_sources VALUES($s,$v)";put.Parameters.AddWithValue("$s",source);put.Parameters.AddWithValue("$v",signature);put.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();tx.Commit();Reload();
    }
    static string MatchName(string name)=>string.Concat(name.Normalize(NormalizationForm.FormKC).Where(ch=>!char.IsWhiteSpace(ch))).ToUpperInvariant();
    public static string[] SplitClusterMembers(string text)
    {
        var result=new List<string>();var value=new StringBuilder();int depth=0;
        void Flush(){string s=value.ToString().Trim();if(s.Length>0)result.Add(s);value.Clear();}
        foreach(char ch in text){if(ch is '(' or '（' or '[' or '【')depth++;if(ch is ')' or '）' or ']' or '】')depth=Math.Max(0,depth-1);if(depth==0&&ch is ',' or '，' or ';' or '；' or '\n' or '\r')Flush();else value.Append(ch);}Flush();return result.Distinct(StringComparer.Ordinal).ToArray();
    }
    public ClusterInput[] ReadClusterInputs(string path,string? sheet=null,CancellationToken ct=default)
    {
        using var rows=TableInput.Read(path,"自动",sheet,"__not_used__").GetEnumerator();if(!rows.MoveNext())throw new InvalidDataException("集群表为空");var h=rows.Current.Select(x=>x.Trim()).ToArray();int ni=Array.IndexOf(h,"设备名称"),mi=Array.IndexOf(h,"集群列表"),ti=Array.IndexOf(h,"集群类型"),si=Array.IndexOf(h,"所属站点");if(ni<0||mi<0)throw new InvalidDataException("需要设备名称、集群列表字段");var result=new List<ClusterInput>();int row=1;
        while(rows.MoveNext()){ct.ThrowIfCancellationRequested();row++;var r=rows.Current;string Get(int i)=>i>=0&&i<r.Length?r[i].Trim():"";if(Get(ni).Length==0)continue;result.Add(new(Get(ni),NormalizeType(Get(ti)),SplitClusterMembers(Get(mi)),"集群表："+Path.GetFileName(path),row,Get(si)));}return result.ToArray();
    }
    public List<(string Name,string Reason)> FilteredClusters {get;}=[];
    ClusterInput[] SavedClusterInputs()
    {
        using var c=Open(Personal);EnsureStations(c);using var q=c.CreateCommand();q.CommandText="SELECT name,type,members,source,rownum,station FROM cluster_inputs";using var r=q.ExecuteReader();var values=new List<ClusterInput>();while(r.Read())values.Add(new(r.GetString(0),r.GetString(1),JsonSerializer.Deserialize<string[]>(r.GetString(2))!,r.GetString(3),r.GetInt32(4),r.GetString(5)));return values.ToArray();
    }
    public void SaveClusterImport(string path,string? sheet=null,bool replace=true,CancellationToken ct=default)
    {
        var inputs=ReadClusterInputs(path,sheet,ct);using(var c=Open(Personal)){EnsureStations(c);using var tx=c.BeginTransaction();if(replace)Run(c,"DELETE FROM cluster_inputs");using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT OR REPLACE INTO cluster_inputs VALUES($s,$r,$n,$t,$m,$v)";foreach(string p in new[]{"$s","$r","$n","$t","$m","$v"})q.Parameters.AddWithValue(p,"");foreach(var row in inputs){ct.ThrowIfCancellationRequested();object[] v=[row.Source,row.Row,row.Name,row.Type,JsonSerializer.Serialize(row.Members),row.Station];for(int i=0;i<v.Length;i++)q.Parameters[i].Value=v[i];q.ExecuteNonQuery();}if(replace)Run(c,"DELETE FROM clusters WHERE source NOT LIKE '人工%' AND source NOT LIKE '历史确认%'");tx.Commit();}Reload();WriteClusterAudit();
    }
    void WriteClusterAudit(){_ = Clusters();Csv.Write(Path.Combine(Root,"集群过滤报告.csv"),new[]{new object?[]{"集群名称","过滤原因"}}.Concat(FilteredClusters.Select(x=>new object?[]{x.Name,x.Reason})));}
    List<ClusterRecord> AnalyzeClusters(IEnumerable<ClusterRecord> persisted,IEnumerable<ClusterInput> raw,Graph graph)
    {
        var inputs=raw.ToArray();var sources=inputs.Select(x=>x.Source).ToHashSet();var all=persisted.Where(x=>Human(x.Source)||!sources.Contains(x.Source.Replace("云端：",""))).Concat(inputs.Select(x=>new ClusterRecord("",x.Name,x.Type,x.Members,x.Source,"已确认",x.Members.Contains(x.Name)?"":"设备名称不在集群列表中",true,x.Station))).ToList();
        using var c=Open(Personal);EnsureStations(c);using var q=c.CreateCommand();q.CommandText="SELECT name,station FROM facility_stations";using var r=q.ExecuteReader();var station=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);while(r.Read()){string n=r.GetString(0),s=r.GetString(1);if(!station.TryGetValue(n,out var ss))station[n]=ss=[];if(HasRoom(s))ss.Add(s);}
        var known=station.Keys.Concat(graph.Nodes.Select(n=>n.Name)).ToHashSet(StringComparer.Ordinal);var aliases=known.GroupBy(MatchName).Where(x=>x.Count()==1).ToDictionary(x=>x.Key,x=>x.First());string Resolve(string n)=>known.Contains(n)?n:aliases.GetValueOrDefault(MatchName(n),n);
        var bySite=station.Where(x=>x.Value.Count==1).GroupBy(x=>x.Value.Single(),StringComparer.Ordinal).ToDictionary(x=>x.Key,x=>x.Select(p=>p.Key).ToArray());var expanded=new List<ClusterRecord>();FilteredClusters.Clear();
        foreach(var x in all)
        {
            if(Human(x.Source)){expanded.Add(x with{Station=string.Join('；',x.Members.Where(station.ContainsKey).SelectMany(n=>station[n]).Distinct(StringComparer.Ordinal))});continue;}
            var members=x.Members.Select(Resolve).Distinct(StringComparer.Ordinal).ToHashSet(StringComparer.Ordinal);var sites=members.Append(Resolve(x.Name)).Where(station.ContainsKey).SelectMany(n=>station[n]).Distinct(StringComparer.Ordinal).ToList();if(HasRoom(x.Station))sites.Add(x.Station);sites=sites.Distinct().ToList();
            string reason="";if(sites.Count>1)reason="成员所属站点冲突："+string.Join("；",sites);
            if(sites.Count==1&&bySite.TryGetValue(sites[0],out var same))members.UnionWith(same);
            if(members.Count<2){FilteredClusters.Add((x.Name,"补齐所属站点后仍不足两个成员"));continue;}
            if(!members.Contains(Resolve(x.Name)))reason=string.Join('；',new[]{reason,"设备名称不在集群列表中"}.Where(s=>s.Length>0));
            var names=members.Order(StringComparer.Ordinal).ToArray();string id=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(names))));expanded.Add(x with{Id=id,Members=names,Station=string.Join('；',sites),Status=reason.Length>0?"需核对":"已确认",Reason=reason,Enabled=reason.Length==0});
        }
        var unique=expanded.GroupBy(x=>string.Join('\n',x.Members.Order(StringComparer.Ordinal))).Select(g=>g.OrderByDescending(x=>Human(x.Source)).ThenBy(x=>x.Name,StringComparer.Ordinal).First()).ToList();
        var overlaps=unique.SelectMany(x=>x.Members.Select(n=>(n,x.Id))).GroupBy(x=>x.n).Where(x=>x.Select(y=>y.Id).Distinct().Count()>1).Select(x=>x.Key).ToHashSet();var graphNames=graph.Nodes.Select(n=>n.Name).ToHashSet(StringComparer.Ordinal);
        return unique.Select(x=>{var reasons=new List<string>();if(x.Status=="需核对"&&x.Reason.Length>0&&!x.Reason.Contains("成员未匹配当前资源"))reasons.Add(x.Reason);var missing=x.Members.Where(n=>!known.Contains(n)).ToArray();if(missing.Length>0)reasons.Add("设施表与中继段均未找到："+string.Join('；',missing));if(x.Members.Any(overlaps.Contains))reasons.Add("成员归属重叠");bool bad=reasons.Count>0;var idle=x.Members.Where(n=>known.Contains(n)&&!graphNames.Contains(n)).ToArray();if(idle.Length>0)reasons.Add("设施表存在，暂无中继段："+string.Join('；',idle));bool enabled=x.Enabled&&!bad&&x.Members.Count(graphNames.Contains)>=2;if(!bad&&!enabled&&x.Members.Count(graphNames.Contains)<2)reasons.Add("当前可参与路由的成员不足两个");return x with{Status=bad?"需核对":x.Status,Reason=string.Join('；',reasons),Enabled=enabled};}).ToList();
    }
    public void ApplyClusterRepairBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;string hash=FileSha(path);if(Setting("cluster-repair-r143")==hash)return;
        using var target=Open(Personal);EnsureStations(target);using var attach=target.CreateCommand();attach.CommandText="ATTACH DATABASE $p AS repair";attach.Parameters.AddWithValue("$p",path);attach.ExecuteNonQuery();using var tx=target.BeginTransaction();
        // Existing source files take priority. Repair only the supplied source baselines, never manual clusters.
        foreach(string table in new[]{"facility_stations","station_sources","cluster_inputs"}){ct.ThrowIfCancellationRequested();using var q=target.CreateCommand();q.Transaction=tx;q.CommandText=table=="facility_stations"?"INSERT OR IGNORE INTO main.facility_stations SELECT * FROM repair.facility_stations WHERE source NOT IN(SELECT source FROM main.station_sources)":table=="station_sources"?"INSERT OR IGNORE INTO main.station_sources SELECT * FROM repair.station_sources":"INSERT OR IGNORE INTO main.cluster_inputs SELECT * FROM repair.cluster_inputs WHERE source NOT IN(SELECT DISTINCT source FROM main.cluster_inputs)";q.ExecuteNonQuery();}
        tx.Commit();SetSetting("cluster-repair-r143",hash);Reload();WriteClusterAudit();
    }
}
