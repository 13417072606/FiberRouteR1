using Microsoft.Data.Sqlite;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;

namespace FiberRoute.Core;

public sealed record DataManifest(string Version,int Schema,string ResourceSha256,string CatalogSha256,string[] TypeSources,string[] RoomSources,string[] EvidenceSources,string[] ClusterSources,long DuplicatesIgnored=0,string[]? RoomKinds=null);
public sealed record DataChange(string 类别,string 名称,string 变更,string 原因="");
public sealed class PreparedDataUpdate : IDisposable
{
    public string Directory {get;internal set;}="";public DataManifest Manifest {get;internal set;}=null!;
    public long Added {get;internal set;}public long Changed {get;internal set;}public long Removed {get;internal set;}public long Unchanged {get;internal set;}public long MetadataChanged {get;internal set;}
    public List<DataChange> Details {get;}=[];public int ManualConflicts {get;internal set;}
    public bool HasChanges=>Added+Changed+Removed+MetadataChanged>0;
    public void Dispose(){try{if(System.IO.Directory.Exists(Directory))System.IO.Directory.Delete(Directory,true);}catch(IOException){}catch(UnauthorizedAccessException){}}
}

public sealed partial class Store
{
    static readonly string[] CatalogTables=["types","type_meta","clusters","room_members","room_facilities","facility_evidence","evidence_sources","facility_stations","station_sources","cluster_inputs"];
    static string FileSha(string path){using var file=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(file));}
    static void BackupDatabase(string source,string target)
    {
        using var from=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=source,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());from.Open();using var to=Open(target);from.BackupDatabase(to);
    }
    static void CheckDatabase(string path)
    {using var c=Open(path);using var q=c.CreateCommand();q.CommandText="PRAGMA quick_check";if((string?)q.ExecuteScalar()!="ok")throw new InvalidDataException("数据包数据库完整性检查失败");}
    public string BuildDataPackage(string destination,string version,CancellationToken ct=default)
    {
        if(string.IsNullOrWhiteSpace(version)||!File.Exists(Database))throw new InvalidDataException("请提供数据版本及有效的管理方资源库");
        string stage=Path.Combine(Path.GetTempPath(),"FiberRoute-publish-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);string temporary=Path.GetFullPath(destination)+".partial";
        try
        {
            string resources=Path.Combine(stage,"resources.db"),catalog=Path.Combine(stage,"catalog.db");BackupDatabase(Database,resources);BackupDatabase(Personal,catalog);ct.ThrowIfCancellationRequested();
            ValidateResourceIds(resources);long duplicates;
            using(var c=Open(resources))
            {
                using var count=c.CreateCommand();count.CommandText="SELECT count(*) FROM edges";long before=(long)count.ExecuteScalar()!;
                Run(c,"CREATE TABLE edges_next AS SELECT row_number() OVER(ORDER BY min(id))-1 AS id,business_id,name,a,b,length,free,total,raw FROM edges GROUP BY business_id,name,a,b,length,free,total,raw; DROP TABLE edges; ALTER TABLE edges_next RENAME TO edges; CREATE UNIQUE INDEX edge_id ON edges(id); CREATE INDEX edge_a ON edges(a); CREATE INDEX edge_b ON edges(b)");count.CommandText="SELECT count(*) FROM edges";duplicates=before-(long)count.ExecuteScalar()!;Run(c,"VACUUM");
            }
            string[] typeSources,roomSources,evidenceSources,clusterSources;
            using(var c=Open(catalog))
            {
                EnsureEvidence(c);EnsureStations(c);EnsureRoomFacilities(c);Run(c,"PRAGMA secure_delete=ON");using var names=c.CreateCommand();names.CommandText="SELECT name FROM sqlite_master WHERE type='table' AND name NOT LIKE 'sqlite_%'";var drop=new List<string>();using(var r=names.ExecuteReader())while(r.Read())if(!CatalogTables.Contains(r.GetString(0)))drop.Add(r.GetString(0));foreach(string name in drop)Run(c,"DROP TABLE \""+name.Replace("\"","\"\"")+"\"");
                string[] Sources(string table){using var q=c.CreateCommand();q.CommandText="SELECT DISTINCT source FROM "+table;using var r=q.ExecuteReader();var list=new List<string>();while(r.Read())list.Add(r.GetString(0));return list.ToArray();}
                typeSources=Sources("type_meta");roomSources=Sources("room_members").Concat(Sources("room_facilities")).Distinct().ToArray();evidenceSources=Sources("evidence_sources").Concat(Sources("station_sources")).Distinct().ToArray();clusterSources=Sources("clusters").Concat(Sources("cluster_inputs")).Distinct().ToArray();Run(c,"VACUUM");
            }
            CheckDatabase(resources);CheckDatabase(catalog);ct.ThrowIfCancellationRequested();
            var manifest=new DataManifest(version,1,FileSha(resources),FileSha(catalog),typeSources,roomSources,evidenceSources,clusterSources,duplicates,["ODF","光交接箱","光分纤箱","光终端盒","多媒体箱"]);File.WriteAllText(Path.Combine(stage,"manifest.json"),JsonSerializer.Serialize(manifest));
            Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);if(File.Exists(temporary))File.Delete(temporary);using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))foreach(string name in new[]{"resources.db","catalog.db","manifest.json"})zip.CreateEntryFromFile(Path.Combine(stage,name),name,CompressionLevel.Optimal);File.Move(temporary,destination,true);return destination;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);Directory.Delete(stage,true);}
    }
    static Dictionary<string,string> SnapshotRows(string path,CancellationToken ct)
    {
        var values=new Dictionary<string,List<string>>(StringComparer.Ordinal);if(!File.Exists(path))return [];
        using var c=Open(path);using var q=c.CreateCommand();q.CommandText="SELECT e.business_id,a.name,b.name,e.name,e.length,e.free,e.total,CASE WHEN json_valid(e.raw) THEN COALESCE(json_extract(e.raw,'$.始端区域'),'') ELSE '' END,CASE WHEN json_valid(e.raw) THEN COALESCE(json_extract(e.raw,'$.终端区域'),'') ELSE '' END,CASE WHEN json_valid(e.raw) THEN COALESCE(json_extract(e.raw,'$.业务状态'),'') ELSE '' END,e.raw FROM edges e JOIN nodes a ON a.id=e.a JOIN nodes b ON b.id=e.b";using var r=q.ExecuteReader();
        while(r.Read()){ct.ThrowIfCancellationRequested();string key=r.GetString(0);var parts=new object?[r.FieldCount-1];for(int i=1;i<r.FieldCount;i++)parts[i-1]=r.IsDBNull(i)?null:r.GetValue(i);string fingerprint=Convert.ToHexString(SHA256.HashData(JsonSerializer.SerializeToUtf8Bytes(parts)));if(!values.TryGetValue(key,out var list))values[key]=list=[];list.Add(fingerprint);}
        return values.ToDictionary(p=>p.Key,p=>string.Join('\n',p.Value.Order(StringComparer.Ordinal)),StringComparer.Ordinal);
    }
    static void ValidateResourceIds(string path)
    {
        using var c=Open(path);using var q=c.CreateCommand();q.CommandText="SELECT count(*),count(DISTINCT business_id),coalesce(sum(CASE WHEN business_id IS NULL OR trim(business_id)='' THEN 1 ELSE 0 END),0) FROM edges";using var r=q.ExecuteReader();r.Read();if(r.GetInt64(0)==0||r.GetInt64(0)!=r.GetInt64(1)||r.GetInt64(2)>0)throw new InvalidDataException("中继段 ID 必须非空且唯一且资源表不能清空；资源库保持不变");
    }
    static (long Added,long Changed,long Removed,long Unchanged) CompareResources(string oldPath,string nextPath,CancellationToken ct,Action<string,string>? detail=null)
    {
        var old=SnapshotRows(oldPath,ct);var next=SnapshotRows(nextPath,ct);long added=0,changed=0,removed=0,unchanged=0;
        foreach(var row in next){if(!old.TryGetValue(row.Key,out var previous)){added++;detail?.Invoke(row.Key,"新增");}else if(previous!=row.Value){changed++;detail?.Invoke(row.Key,"变更");}else unchanged++;}
        foreach(string key in old.Keys.Where(k=>!next.ContainsKey(k))){removed++;detail?.Invoke(key,"删除");}
        return (added,changed,removed,unchanged);
    }
    public PreparedDataUpdate PrepareDataPackage(string zip,string? expectedVersion=null,CancellationToken ct=default)
    {
        var prepared=new PreparedDataUpdate{Directory=Path.Combine(Path.GetTempPath(),"FiberRoute-data-"+Guid.NewGuid().ToString("N"))};Directory.CreateDirectory(prepared.Directory);
        try
        {
            using(var archive=ZipFile.OpenRead(zip))
            {
                string[] allowed=["resources.db","catalog.db","manifest.json"];if(archive.Entries.Count!=3||archive.Entries.Select(e=>e.FullName).Distinct().Count()!=3)throw new InvalidDataException("数据更新包目录不符合规范");long size=0;
                foreach(var entry in archive.Entries){ct.ThrowIfCancellationRequested();if(!allowed.Contains(entry.FullName)||entry.Length<1||entry.Length>2L*1024*1024*1024||((size+=entry.Length)>3L*1024*1024*1024)||entry.FullName=="manifest.json"&&entry.Length>1024*1024)throw new InvalidDataException("数据更新包文件无效");entry.ExtractToFile(Path.Combine(prepared.Directory,entry.FullName));}
            }
            var manifest=JsonSerializer.Deserialize<DataManifest>(File.ReadAllText(Path.Combine(prepared.Directory,"manifest.json")))??throw new InvalidDataException("数据清单为空");prepared.Manifest=manifest;
            if(manifest.Schema!=1||string.IsNullOrWhiteSpace(manifest.Version)||expectedVersion!=null&&manifest.Version!=expectedVersion)throw new InvalidDataException("数据版本或结构不兼容");
            string resource=Path.Combine(prepared.Directory,"resources.db"),catalog=Path.Combine(prepared.Directory,"catalog.db");if(!FileSha(resource).Equals(manifest.ResourceSha256,StringComparison.OrdinalIgnoreCase)||!FileSha(catalog).Equals(manifest.CatalogSha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("数据包内容SHA256不符");CheckDatabase(resource);CheckDatabase(catalog);
            using(var c=Open(resource)){using var q=c.CreateCommand();q.CommandText="SELECT count(*) FROM edges e LEFT JOIN nodes a ON a.id=e.a LEFT JOIN nodes b ON b.id=e.b WHERE a.id IS NULL OR b.id IS NULL OR free<0 OR total<free OR length<0";if((long)q.ExecuteScalar()!>0)throw new InvalidDataException("资源端点或芯数非法");foreach(string table in new[]{"nodes","edges"}){q.CommandText="SELECT count(*),coalesce(max(id),-1),coalesce(min(id),0),count(DISTINCT id) FROM "+table;using var r=q.ExecuteReader();if(!r.Read()||(table=="nodes"&&r.GetInt64(0)==0)||r.GetInt64(0)!=r.GetInt64(1)+1||r.GetInt64(2)!=0||r.GetInt64(0)!=r.GetInt64(3))throw new InvalidDataException("资源编号不连续："+table);}}
            ValidateResourceIds(resource);
            var counts=CompareResources(Database,resource,ct,(key,change)=>{if(prepared.Details.Count<500)prepared.Details.Add(new("中继段",key,change));});
            (prepared.Added,prepared.Changed,prepared.Removed,prepared.Unchanged)=counts;
            BackupDatabase(Personal,Path.Combine(prepared.Directory,"personal.db"));MergeCatalog(prepared,catalog,ct);return prepared;
        }
        catch{prepared.Dispose();throw;}
    }
    static bool Human(string source)=>source.StartsWith("人工",StringComparison.Ordinal)||source.StartsWith("历史确认",StringComparison.Ordinal);
    void MergeCatalog(PreparedDataUpdate prepared,string catalog,CancellationToken ct)
    {
        using(var incoming=Open(catalog)){EnsureStations(incoming);EnsureRoomFacilities(incoming);}
        using var c=Open(Path.Combine(prepared.Directory,"personal.db"));EnsureEvidence(c);EnsureStations(c);EnsureRoomFacilities(c);using(var attach=c.CreateCommand()){attach.CommandText="ATTACH DATABASE $p AS incoming";attach.Parameters.AddWithValue("$p",catalog);attach.ExecuteNonQuery();}using var tx=c.BeginTransaction();
        long Execute(string sql){ct.ThrowIfCancellationRequested();using var q=c.CreateCommand();q.Transaction=tx;q.CommandText=sql;return q.ExecuteNonQuery();}
        long Total(){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="SELECT total_changes()";return(long)q.ExecuteScalar()!;}
        string Scope(string key,string[] incoming){var old=Setting("cloud-scope-"+key);var values=incoming.Concat(old==null?[]:JsonSerializer.Deserialize<string[]>(old)??[]).Distinct().ToArray();return values.Length==0?"NULL":string.Join(',',values.Select(s=>"'"+s.Replace("'","''")+"'"));}
        long before=Total();string human="(coalesce(m.source,'') LIKE '人工%' OR coalesce(m.source,'') LIKE '历史确认%')";
        using(var conflicts=c.CreateCommand())
        {
            conflicts.Transaction=tx;conflicts.CommandText=$"SELECT t.name,t.type,i.type FROM main.types t JOIN main.type_meta m ON m.name=t.name JOIN incoming.types i ON i.name=t.name WHERE {human} AND t.type<>i.type";using var r=conflicts.ExecuteReader();while(r.Read()){prepared.ManualConflicts++;if(prepared.Details.Count<1000)prepared.Details.Add(new("人工类型",r.GetString(0),"需核对","保留人工类型："+r.GetString(1)+"；云端类型："+r.GetString(2)));}
        }
        string typeScope=Scope("types",prepared.Manifest.TypeSources);
        Execute($"DELETE FROM main.types WHERE name IN(SELECT name FROM main.type_meta m WHERE NOT {human} AND (m.source LIKE '云端：%' OR m.source IN({typeScope})) AND NOT EXISTS(SELECT 1 FROM incoming.types i WHERE i.name=m.name)); DELETE FROM main.type_meta WHERE name NOT IN(SELECT name FROM main.types)");
        Execute($"INSERT INTO main.types(name,type) SELECT i.name,i.type FROM incoming.types i LEFT JOIN main.type_meta m ON m.name=i.name WHERE NOT {human} ON CONFLICT(name) DO UPDATE SET type=excluded.type WHERE type<>excluded.type");
        Execute($"INSERT INTO main.type_meta(name,source,status,reason) SELECT i.name,CASE WHEN i.source LIKE '云端：%' THEN i.source ELSE '云端：'||i.source END,i.status,i.reason FROM incoming.type_meta i LEFT JOIN main.type_meta m ON m.name=i.name WHERE NOT {human} ON CONFLICT(name) DO UPDATE SET source=excluded.source,status=excluded.status,reason=excluded.reason WHERE source<>excluded.source OR status<>excluded.status OR reason<>excluded.reason");
        Execute("UPDATE main.type_meta SET status='需核对',reason='云端类型与人工确认不一致，保留人工类型；云端类型：'||(SELECT type FROM incoming.types i WHERE i.name=type_meta.name) WHERE (source LIKE '人工%' OR source LIKE '历史确认%') AND EXISTS(SELECT 1 FROM incoming.types i JOIN main.types t ON t.name=i.name WHERE i.name=type_meta.name AND t.type<>i.type) AND (status<>'需核对' OR reason<>'云端类型与人工确认不一致，保留人工类型；云端类型：'||(SELECT type FROM incoming.types i WHERE i.name=type_meta.name))");
        Execute("DELETE FROM main.clusters WHERE source NOT LIKE '人工%' AND source NOT LIKE '历史确认%' AND NOT EXISTS(SELECT 1 FROM incoming.clusters i WHERE i.id=main.clusters.id)");
        using(var read=c.CreateCommand())
        {
            read.Transaction=tx;read.CommandText="SELECT id,name,type,members,source,status,reason,enabled FROM incoming.clusters";using var r=read.ExecuteReader();
            while(r.Read())
            {
                ct.ThrowIfCancellationRequested();using var old=c.CreateCommand();old.Transaction=tx;old.CommandText="SELECT source,members,type FROM main.clusters WHERE id=$id";old.Parameters.AddWithValue("$id",r.GetString(0));using var existing=old.ExecuteReader();if(existing.Read()&&Human(existing.GetString(0))){if(existing.GetString(1)!=r.GetString(3)||existing.GetString(2)!=r.GetString(2)){prepared.ManualConflicts++;if(prepared.Details.Count<1000)prepared.Details.Add(new("人工集群",r.GetString(1),"需核对","保留客户人工维护集群，暂停归并"));existing.Close();using var pause=c.CreateCommand();pause.Transaction=tx;pause.CommandText="UPDATE main.clusters SET status='需核对',reason='云端集群与人工维护不一致，保留人工成员并暂停归并',enabled=0 WHERE id=$id AND (status<>'需核对' OR enabled<>0 OR reason<>'云端集群与人工维护不一致，保留人工成员并暂停归并')";pause.Parameters.AddWithValue("$id",r.GetString(0));pause.ExecuteNonQuery();}continue;}
                existing.Close();using var put=c.CreateCommand();put.Transaction=tx;put.CommandText="INSERT INTO main.clusters VALUES($a,$b,$c,$d,$e,$f,$g,$h) ON CONFLICT(id) DO UPDATE SET name=excluded.name,type=excluded.type,members=excluded.members,source=excluded.source,status=excluded.status,reason=excluded.reason,enabled=excluded.enabled WHERE name<>excluded.name OR type<>excluded.type OR members<>excluded.members OR source<>excluded.source OR status<>excluded.status OR reason<>excluded.reason OR enabled<>excluded.enabled";for(int i=0;i<8;i++)put.Parameters.AddWithValue("$"+(char)('a'+i),i==4?(r.GetString(i).StartsWith("云端：")?r.GetString(i):"云端："+r.GetString(i)):r.GetValue(i));put.ExecuteNonQuery();
            }
        }
        Execute("DELETE FROM main.room_members");
        string[] roomKinds=prepared.Manifest.RoomKinds??[];
        if(roomKinds.Length==0){using var kinds=c.CreateCommand();kinds.Transaction=tx;kinds.CommandText="SELECT DISTINCT kind FROM incoming.room_facilities";using var kindsReader=kinds.ExecuteReader();var found=new List<string>();while(kindsReader.Read())found.Add(kindsReader.GetString(0));roomKinds=found.ToArray();}
        string[] validKinds=["ODF","光交接箱","光分纤箱","光终端盒","多媒体箱"];
        if(roomKinds.Any(k=>!validKinds.Contains(k,StringComparer.Ordinal)))throw new InvalidDataException("数据包机房设施类别无效");
        foreach(string kind in roomKinds)
        {
            using var remove=c.CreateCommand();remove.Transaction=tx;remove.CommandText="DELETE FROM main.room_facilities WHERE kind=$kind AND NOT EXISTS(SELECT 1 FROM incoming.room_facilities i WHERE i.kind=main.room_facilities.kind AND i.id=main.room_facilities.id)";remove.Parameters.AddWithValue("$kind",kind);remove.ExecuteNonQuery();
            using var mark=c.CreateCommand();mark.Transaction=tx;mark.CommandText="INSERT OR IGNORE INTO main.room_kind_imports VALUES($kind)";mark.Parameters.AddWithValue("$kind",kind);mark.ExecuteNonQuery();
        }
        Execute("INSERT INTO main.room_facilities(id,name,room,source,kind,rank,rownum) SELECT id,name,CASE WHEN instr(room,'挂墙')>0 THEN '' ELSE room END,source,kind,rank,rownum FROM incoming.room_facilities WHERE true ON CONFLICT(kind,id) DO UPDATE SET name=excluded.name,room=excluded.room,source=excluded.source,rank=excluded.rank,rownum=excluded.rownum WHERE name<>excluded.name OR room<>excluded.room OR rank<>excluded.rank");
        string evidenceScope=Scope("evidence",prepared.Manifest.EvidenceSources);
        Execute($"DELETE FROM main.facility_evidence WHERE source IN({evidenceScope}) AND NOT EXISTS(SELECT 1 FROM incoming.facility_evidence i WHERE i.name=facility_evidence.name AND i.source=facility_evidence.source AND i.rownum=facility_evidence.rownum); DELETE FROM main.evidence_sources WHERE source IN({evidenceScope}) AND NOT EXISTS(SELECT 1 FROM incoming.evidence_sources i WHERE i.source=evidence_sources.source)");
        Execute("INSERT INTO main.facility_evidence SELECT * FROM incoming.facility_evidence WHERE true ON CONFLICT(name,source,rownum) DO UPDATE SET region=excluded.region,room=excluded.room,field=excluded.field,sheet=excluded.sheet WHERE region<>excluded.region OR room<>excluded.room OR field<>excluded.field OR sheet<>excluded.sheet; INSERT INTO main.evidence_sources SELECT * FROM incoming.evidence_sources WHERE true ON CONFLICT(source) DO UPDATE SET signature=excluded.signature WHERE signature<>excluded.signature");
        Execute($"DELETE FROM main.facility_stations WHERE source IN({evidenceScope}) AND NOT EXISTS(SELECT 1 FROM incoming.facility_stations i WHERE i.source=facility_stations.source AND i.name=facility_stations.name AND i.rownum=facility_stations.rownum); DELETE FROM main.station_sources WHERE source IN({evidenceScope}) AND NOT EXISTS(SELECT 1 FROM incoming.station_sources i WHERE i.source=station_sources.source)");
        Execute("INSERT INTO main.facility_stations SELECT * FROM incoming.facility_stations WHERE true ON CONFLICT(name,source,rownum) DO UPDATE SET station=excluded.station WHERE station<>excluded.station; INSERT INTO main.station_sources SELECT * FROM incoming.station_sources WHERE true ON CONFLICT(source) DO UPDATE SET signature=excluded.signature WHERE signature<>excluded.signature");
        Execute("DELETE FROM main.cluster_inputs WHERE NOT EXISTS(SELECT 1 FROM incoming.cluster_inputs i WHERE i.source=main.cluster_inputs.source AND i.rownum=main.cluster_inputs.rownum)");
        Execute("INSERT INTO main.cluster_inputs SELECT * FROM incoming.cluster_inputs WHERE true ON CONFLICT(source,rownum) DO UPDATE SET name=excluded.name,type=excluded.type,members=excluded.members,station=excluded.station WHERE name<>excluded.name OR type<>excluded.type OR members<>excluded.members OR station<>excluded.station");
        prepared.MetadataChanged=Total()-before;
        foreach(var (key,values) in new[]{("types",prepared.Manifest.TypeSources),("clusters",prepared.Manifest.ClusterSources),("rooms",prepared.Manifest.RoomSources),("evidence",prepared.Manifest.EvidenceSources)}){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT OR REPLACE INTO settings VALUES($k,$v)";q.Parameters.AddWithValue("$k","cloud-scope-"+key);q.Parameters.AddWithValue("$v",JsonSerializer.Serialize(values));q.ExecuteNonQuery();}
        foreach(var (key,value) in new[]{("remoteResourceVersion",prepared.Manifest.Version),("remoteCatalogHash",prepared.Manifest.CatalogSha256)}){using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT OR REPLACE INTO settings VALUES($k,$v)";q.Parameters.AddWithValue("$k",key);q.Parameters.AddWithValue("$v",value);q.ExecuteNonQuery();}
        ct.ThrowIfCancellationRequested();tx.Commit();
    }
    public void ApplyDataPackage(PreparedDataUpdate prepared,CancellationToken ct=default)
    {
        ct.ThrowIfCancellationRequested();if(!prepared.HasChanges){SetSetting("remoteResourceVersion",prepared.Manifest.Version);return;}
        string resource=Path.Combine(prepared.Directory,"resources.db"),personal=Path.Combine(prepared.Directory,"personal.db");bool resourcesChanged=prepared.Added+prepared.Changed+prepared.Removed>0,resourceReplaced=false,personalReplaced=false,hadResource=File.Exists(Database);
        // Validate the staged pair before touching either live database.
        _=new Store(prepared.Directory);ct.ThrowIfCancellationRequested();
        try
        {
            if(resourcesChanged){if(hadResource)File.Replace(resource,Database,Database+".bak",true);else File.Move(resource,Database);resourceReplaced=true;}
            File.Replace(personal,Personal,Personal+".update.bak",true);personalReplaced=true;Reload();
        }
        catch
        {
            if(personalReplaced)File.Copy(Personal+".update.bak",Personal,true);
            if(resourceReplaced){if(hadResource)File.Copy(Database+".bak",Database,true);else File.Delete(Database);}
            Reload();throw;
        }
    }
}
