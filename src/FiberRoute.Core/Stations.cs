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
    static string MatchName(string name)=>string.Concat(name.Normalize(NormalizationForm.FormKC).Where(ch=>!char.IsWhiteSpace(ch)&&ch is not ('\u200b' or '\ufeff'))).ToUpperInvariant();
    public static string[] SplitClusterMembers(string text)
    {
        var result=new List<string>();var value=new StringBuilder();int depth=0;
        void Flush(){string s=value.ToString().Trim();if(s.Length>0)result.Add(s);value.Clear();}
        foreach(char ch in text){if(ch is '(' or '（' or '[' or '【')depth++;if(ch is ')' or '）' or ']' or '】')depth=Math.Max(0,depth-1);if(depth==0&&ch is ',' or '，' or ';' or '；' or '\n' or '\r')Flush();else value.Append(ch);}Flush();return result.Distinct(StringComparer.Ordinal).ToArray();
    }
    public ClusterInput[] ReadClusterInputs(string path,string? sheet=null,CancellationToken ct=default)
    {
        using var rows=TableInput.Read(path,"自动",sheet,"__not_used__").GetEnumerator();if(!rows.MoveNext())throw new InvalidDataException("集群表为空");var h=rows.Current.Select(x=>x.Trim()).ToArray();int ni=Array.IndexOf(h,"设备名称"),mi=Array.IndexOf(h,"集群列表"),ti=Array.IndexOf(h,"集群类型"),si=Array.IndexOf(h,"所属站点");if(ti<0)ti=Array.IndexOf(h,"设备类型");if(ni<0||mi<0)throw new InvalidDataException("需要设备名称、集群列表字段");var result=new List<(ClusterInput Input,string Raw)>();int row=1;
        while(rows.MoveNext()){ct.ThrowIfCancellationRequested();row++;var r=rows.Current;string Get(int i)=>i>=0&&i<r.Length?r[i].Trim():"";if(Get(ni).Length==0)continue;result.Add((new(Get(ni),NormalizeType(Get(ti)),[],"集群表："+Path.GetFileName(path),row,Get(si)),Get(mi)));}
        var keys=ClusterFacilityNames().Concat(Graph.Nodes.Select(n=>n.Name)).Concat(result.Select(x=>x.Input.Name)).Select(MatchName).ToHashSet(StringComparer.Ordinal);
        return result.Select(x=>x.Input with{Members=ParseClusterText(x.Raw,keys)}).ToArray();
    }
    public string[] ParseClusterMemberText(string text)
    {
        var names=ClusterFacilityNames().Concat(Graph.Nodes.Select(n=>n.Name)).Concat(SavedClusterInputs().Select(x=>x.Name)).Distinct(StringComparer.Ordinal).ToArray();
        var aliases=names.GroupBy(MatchName).ToDictionary(g=>g.Key,g=>g.ToArray());var keys=aliases.Keys.ToHashSet(StringComparer.Ordinal);
        return ParseClusterText(text,keys).Select(n=>aliases.TryGetValue(MatchName(n),out var values)&&values.Length==1?values[0]:n.Trim()).Distinct(StringComparer.Ordinal).ToArray();
    }
    public List<(string Name,string Reason)> FilteredClusters {get;}=[];
    ClusterInput[] SavedClusterInputs()
    {
        using var c=Open(Personal);EnsureStations(c);using var q=c.CreateCommand();q.CommandText="SELECT name,type,members,source,rownum,station FROM cluster_inputs";using var r=q.ExecuteReader();var values=new List<ClusterInput>();while(r.Read())values.Add(new(r.GetString(0),r.GetString(1),JsonSerializer.Deserialize<string[]>(r.GetString(2))!,r.GetString(3),r.GetInt32(4),r.GetString(5)));return values.ToArray();
    }
    public void SaveClusterImport(string path,string? sheet=null,bool replace=true,CancellationToken ct=default)
    {
        var inputs=ReadClusterInputs(path,sheet,ct);using(var c=Open(Personal)){EnsureStations(c);using var tx=c.BeginTransaction();Run(c,"DELETE FROM cluster_inputs; DELETE FROM clusters WHERE source NOT LIKE '人工%' AND source NOT LIKE '历史确认%'");using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT OR REPLACE INTO cluster_inputs VALUES($s,$r,$n,$t,$m,$v)";foreach(string p in new[]{"$s","$r","$n","$t","$m","$v"})q.Parameters.AddWithValue(p,"");foreach(var row in inputs){ct.ThrowIfCancellationRequested();object[] v=[row.Source,row.Row,row.Name,row.Type,JsonSerializer.Serialize(row.Members),row.Station];for(int i=0;i<v.Length;i++)q.Parameters[i].Value=v[i];q.ExecuteNonQuery();}tx.Commit();}Reload();WriteClusterAudit();
    }
    void WriteClusterAudit(){_ = Clusters();Csv.Write(Path.Combine(Root,"集群过滤报告.csv"),new[]{new object?[]{"集群名称","过滤原因"}}.Concat(FilteredClusters.Select(x=>new object?[]{x.Name,x.Reason})));}
    public void ApplyClusterRepairBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;string hash=FileSha(path),key="cluster-repair-baseline:"+Path.GetFileName(path);if(Setting(key)==hash||Setting("cluster-repair-r143")==hash)return;
        using var target=Open(Personal);EnsureStations(target);using var attach=target.CreateCommand();attach.CommandText="ATTACH DATABASE $p AS repair";attach.Parameters.AddWithValue("$p",path);attach.ExecuteNonQuery();using var tx=target.BeginTransaction();
        // Existing source files take priority. Repair only the supplied source baselines, never manual clusters.
        foreach(string table in new[]{"facility_stations","station_sources","cluster_inputs"}){ct.ThrowIfCancellationRequested();using var q=target.CreateCommand();q.Transaction=tx;q.CommandText=table=="facility_stations"?"INSERT OR IGNORE INTO main.facility_stations SELECT * FROM repair.facility_stations WHERE source NOT IN(SELECT source FROM main.station_sources)":table=="station_sources"?"INSERT OR IGNORE INTO main.station_sources SELECT * FROM repair.station_sources":"INSERT OR IGNORE INTO main.cluster_inputs SELECT * FROM repair.cluster_inputs WHERE source NOT IN(SELECT DISTINCT source FROM main.cluster_inputs)";q.ExecuteNonQuery();}
        tx.Commit();SetSetting(key,hash);Reload();WriteClusterAudit();
    }
}
