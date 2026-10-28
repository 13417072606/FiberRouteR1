using Microsoft.Data.Sqlite;
using System.Text.Json;
namespace FiberRoute.Core;
public sealed record TypeAssignment(string Name,string Type,string Source,string Status,string Reason);
public sealed partial class Store
{
    void InitMetadata(){using var c=Open(Personal);Run(c,"CREATE TABLE IF NOT EXISTS type_meta(name TEXT PRIMARY KEY,source TEXT,status TEXT,reason TEXT); CREATE TABLE IF NOT EXISTS clusters(id TEXT PRIMARY KEY,name TEXT,type TEXT,members TEXT,source TEXT,status TEXT,reason TEXT,enabled INTEGER); CREATE TABLE IF NOT EXISTS room_members(name TEXT,room TEXT,source TEXT,PRIMARY KEY(name,room,source)); CREATE TABLE IF NOT EXISTS room_choices(name TEXT PRIMARY KEY,room TEXT,enabled INTEGER)");}
    void SaveTypeMeta(string name,string source,string status,string reason){using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="INSERT OR REPLACE INTO type_meta VALUES($n,$s,$t,$r)";q.Parameters.AddWithValue("$n",name);q.Parameters.AddWithValue("$s",source);q.Parameters.AddWithValue("$t",status);q.Parameters.AddWithValue("$r",reason);q.ExecuteNonQuery();}
    Node[] Decorate(Node[] nodes)
    {
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT name,source,status,reason FROM type_meta";using var r=q.ExecuteReader();var meta=new Dictionary<string,(string,string,string)>();while(r.Read())meta[r.GetString(0)]=(r.GetString(1),r.GetString(2),r.GetString(3));
        return nodes.Select(n=>meta.TryGetValue(n.Name,out var m)?n with{Source=m.Item1,Status=m.Item2,Reason=m.Item3,Confirmed=m.Item2=="已确认"}:n with{Source=n.Confirmed?"历史确认（来源未记录）":"资源/自动识别",Status=n.Confirmed?"已确认":"待确认"}).ToArray();
    }
    public static string NormalizeType(string t)=>t.Trim() switch{"站点"=>"未识别","光交接箱"=>"光交箱","光分纤箱"=>"分纤箱","ODF" or "机房ODF"=>"机房",var x=>x};
    public List<TypeAssignment> PreviewTypeFiles(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        var all=new Dictionary<string,List<(string Type,string Source)>>(StringComparer.Ordinal);
        foreach(var f in files)
        {
            using var it=ReadTypeRows(f.Path,f.Sheet).GetEnumerator();if(!it.MoveNext())throw new InvalidDataException("类型表为空："+Path.GetFileName(f.Path));var h=it.Current.Select(x=>x.Trim()).ToArray();
            int ni=Array.FindIndex(h,x=>x is "设施名称" or "节点名称" or "设备名称"),ti=Array.FindIndex(h,x=>x is "节点类型" or "设备类型");if(ni<0)throw new InvalidDataException("类型表缺少设施名称/节点名称/设备名称："+f.Path);
            while(it.MoveNext()) {ct.ThrowIfCancellationRequested();var row=it.Current;string name=ni<row.Length?row[ni].Trim():"";if(name.Length==0)continue;string type=f.Type.Length>0?f.Type:ti>=0&&ti<row.Length?row[ti].Trim():"";type=ClassifyType(name,type);if(!all.TryGetValue(name,out var list))all[name]=list=[];list.Add((type,Path.GetFileName(f.Path)));}
        }
        var previous=TypeAssignments();
        return all.Select(kv=>{
            var values=kv.Value.Select(x=>x.Type).Distinct().ToArray();string type=string.Join(';',values.Where(x=>x.Length>0));string source="类型表："+string.Join('；',kv.Value.Select(x=>x.Source).Distinct());string reason=values.Length>1?"多表类型冲突":type.Length==0?"类型为空":"";
            if(previous.TryGetValue(kv.Key,out var old)&&(old.Source=="人工修改"||old.Source.StartsWith("历史确认"))&&old.Type!=type){reason="与人工类型冲突，导入类型："+type;type=old.Type;source=old.Source;}
            return new TypeAssignment(kv.Key,type,source,reason.Length==0?"已确认":"需核对",reason);
        }).ToList();
    }
    static IEnumerable<string[]> ReadTypeRows(string path,string? sheet)
    {
        if(Path.GetExtension(path).Equals(".csv",StringComparison.OrdinalIgnoreCase))
        {string encoding=Csv.DetectEncoding(path);foreach(var row in Csv.IsRawFacility(path,encoding)?Csv.ReadRawFacility(path,encoding):Csv.Read(path,encoding))yield return row;yield break;}
        foreach(var row in TableInput.Read(path,"自动",sheet,"__not_used__"))yield return row;
    }
    public Dictionary<string,TypeAssignment> TypeAssignments()
    {
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT t.name,t.type,COALESCE(m.source,'历史确认（来源未记录）'),COALESCE(m.status,'已确认'),COALESCE(m.reason,'') FROM types t LEFT JOIN type_meta m ON t.name=m.name";using var r=q.ExecuteReader();var d=new Dictionary<string,TypeAssignment>();while(r.Read())d[r.GetString(0)]=new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4));return d;
    }
    public void ApplyTypeAssignments(IEnumerable<TypeAssignment> rows,CancellationToken ct=default)
    {
        var previous=TypeAssignments();rows=rows.Where(x=>!previous.TryGetValue(x.Name,out var old)||old!=x).ToArray();if(!rows.Any())return;
        using var c=Open(Personal);using var t=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT OR REPLACE INTO types VALUES($n,$v);INSERT OR REPLACE INTO type_meta VALUES($n,$s,$t,$r)";foreach(var n in new[]{"$n","$v","$s","$t","$r"})q.Parameters.Add(n,SqliteType.Text);
        foreach(var x in rows){ct.ThrowIfCancellationRequested();q.Parameters[0].Value=x.Name;q.Parameters[1].Value=ClassifyType(x.Name,x.Type);q.Parameters[2].Value=x.Source;q.Parameters[3].Value=x.Status;q.Parameters[4].Value=x.Reason;q.ExecuteNonQuery();}ct.ThrowIfCancellationRequested();t.Commit();Reload();
    }
    public List<ClusterRecord> PreviewClusters(string path,string? sheet=null,CancellationToken ct=default)
    {
        return AnalyzeClusters([],ReadClusterInputs(path,sheet,ct),Graph);
    }

    Graph? cachedClusterGraph;List<ClusterRecord>? cachedClusters; (string Name,string Reason)[] cachedClusterFilters=[];
    public List<ClusterRecord> Clusters(Graph? graph=null)
    {
        graph??=Graph;if(ReferenceEquals(cachedClusterGraph,graph)&&cachedClusters!=null){FilteredClusters.Clear();FilteredClusters.AddRange(cachedClusterFilters);return cachedClusters.ToList();}
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT id,name,type,members,source,status,reason,enabled FROM clusters";using var r=q.ExecuteReader();var list=new List<ClusterRecord>();while(r.Read())list.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),JsonSerializer.Deserialize<string[]>(r.GetString(3))!,r.GetString(4),r.GetString(5),r.GetString(6),r.GetInt32(7)==1));r.Close();var analyzed=AnalyzeClusters(list,SavedClusterInputs(),graph);cachedClusterGraph=graph;cachedClusters=analyzed;cachedClusterFilters=FilteredClusters.ToArray();return analyzed.ToList();
    }
    public void SaveClusters(IEnumerable<ClusterRecord> records,bool replace=false,CancellationToken ct=default)
    {
        var incoming=records.ToArray();var manual=Clusters().Where(c=>c.Source=="人工修改").ToDictionary(c=>c.Id);
        incoming=incoming.Select(x=>{
            if(x.Source=="人工修改"||!manual.TryGetValue(x.Id,out var old))return x;
            return old.Members.Order(StringComparer.Ordinal).SequenceEqual(x.Members.Order(StringComparer.Ordinal))&&old.Type==x.Type?old:old with{Status="需核对",Reason="与人工集群关系冲突",Enabled=false};
        }).ToArray();var ids=incoming.Select(x=>x.Id).ToHashSet();var retained=replace?manual.Values.Where(c=>!ids.Contains(c.Id)).ToArray():[];
        records=incoming.Concat(retained);
        using var c=Open(Personal);using var t=c.BeginTransaction();if(replace){using var delete=c.CreateCommand();delete.Transaction=t;delete.CommandText="DELETE FROM clusters";delete.ExecuteNonQuery();}using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT OR REPLACE INTO clusters VALUES($i,$n,$t,$m,$s,$c,$r,$e)";foreach(string n in new[]{"$i","$n","$t","$m","$s","$c","$r","$e"})q.Parameters.AddWithValue(n,"");
        foreach(var input in records){ct.ThrowIfCancellationRequested();string intrinsic=string.Join('；',input.Reason.Split('；').Where(v=>v!="成员未匹配当前资源"&&v!="成员归属重叠"));var x=input with{Reason=intrinsic,Status=intrinsic.Length>0?"需核对":input.Status=="需核对"?"已确认":input.Status};object[] vals=[x.Id,x.Name,x.Type,JsonSerializer.Serialize(x.Members),x.Source,x.Status,x.Reason,x.Enabled?1:0];for(int i=0;i<vals.Length;i++)q.Parameters[i].Value=vals[i];q.ExecuteNonQuery();}ct.ThrowIfCancellationRequested();t.Commit();Reload();
    }
    public void RemoveCluster(string id){using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="DELETE FROM clusters WHERE id=$i";q.Parameters.AddWithValue("$i",id);q.ExecuteNonQuery();Reload();}
}
