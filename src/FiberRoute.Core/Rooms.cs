using Microsoft.Data.Sqlite;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FiberRoute.Core;

public sealed record RoomAssignment(string Name,string Room,string Source,string Status,string Reason,bool Enabled,bool IsCabinet);
public sealed record RoomSeed(string Name,string Room,string Source);
public sealed record RoomFacility(string Id,string Name,string Room,string Source,string Kind,int Rank,int Row);

public sealed partial class Store
{
    static void EnsureRoomFacilities(SqliteConnection c)
    {
        using var check=c.CreateCommand();check.CommandText="SELECT sql FROM sqlite_master WHERE type='table' AND name='room_facilities'";string? schema=check.ExecuteScalar() as string;
        if(schema!=null&&!schema.Contains("PRIMARY KEY(kind,id)",StringComparison.OrdinalIgnoreCase))
        {
            Run(c,"CREATE TABLE room_facilities_next(id TEXT NOT NULL,name TEXT NOT NULL,room TEXT NOT NULL,source TEXT NOT NULL,kind TEXT NOT NULL,rank INTEGER NOT NULL,rownum INTEGER NOT NULL,PRIMARY KEY(kind,id)) WITHOUT ROWID; INSERT OR REPLACE INTO room_facilities_next SELECT id,name,room,source,kind,rank,rownum FROM room_facilities ORDER BY rowid; DROP TABLE room_facilities; ALTER TABLE room_facilities_next RENAME TO room_facilities");
        }
        Run(c,"CREATE TABLE IF NOT EXISTS room_facilities(id TEXT NOT NULL,name TEXT NOT NULL,room TEXT NOT NULL,source TEXT NOT NULL,kind TEXT NOT NULL,rank INTEGER NOT NULL,rownum INTEGER NOT NULL,PRIMARY KEY(kind,id)) WITHOUT ROWID; CREATE INDEX IF NOT EXISTS room_facilities_id ON room_facilities(id); CREATE TABLE IF NOT EXISTS room_kind_imports(kind TEXT PRIMARY KEY) WITHOUT ROWID");
    }
    // Room affiliation is an explicit field, never a station name, upstream room or name prefix.
    static bool HasRoom(string room)=>room.Trim() is not ("" or "--" or "无" or "未知" or "未填写");
    static bool UsableRoom(string room)=>HasRoom(room)&&!room.Contains("挂墙",StringComparison.OrdinalIgnoreCase);
    public static bool SuspiciousRoom(string room)=>Regex.IsMatch(room,@"挂墙|壁挂",RegexOptions.IgnoreCase);
    static bool IsCabinet(Node n)=>n.Type.Split(';').Contains("光交箱")||Regex.IsMatch(n.Name,@"(?:^|[/\-])GJ\d*$",RegexOptions.IgnoreCase);
    static bool IsRoomFacility(Node n)=>IsCabinet(n)||n.Type.Split(';').Contains("机房")||Regex.IsMatch(n.Name,@"(?:^|[/\-])(?:ODF|ODB)[A-Z0-9]*(?:$|[/\-])",RegexOptions.IgnoreCase);
    static (string Kind,int Rank) RoomSourceKind(string type,string path)
    {
        string label=type.Length>0?type:Path.GetFileName(path);
        if(label.Contains("ODF",StringComparison.OrdinalIgnoreCase)||label=="机房")return ("ODF",4);
        if(label.Contains("光交",StringComparison.OrdinalIgnoreCase))return ("光交接箱",4);
        if(label.Contains("分纤",StringComparison.OrdinalIgnoreCase))return ("光分纤箱",3);
        if(label.Contains("终端",StringComparison.OrdinalIgnoreCase))return ("光终端盒",2);
        if(label.Contains("多媒体",StringComparison.OrdinalIgnoreCase))return ("多媒体箱",1);
        return ("未识别来源",0);
    }
    static List<RoomFacility> ReadRoomFacilities(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct,HashSet<string>? importedKinds=null)
    {
        var result=new List<RoomFacility>();
        foreach(var file in files)
        {
            using var rows=ReadTypeRows(file.Path,file.Sheet).GetEnumerator();if(!rows.MoveNext())continue;
            var h=rows.Current.Select(x=>x.Trim()).ToArray();int ni=Array.FindIndex(h,x=>x is "设施名称" or "节点名称" or "设备名称"),ri=Array.FindIndex(h,x=>x is "机房名称" or "所属机房"),ii=Array.FindIndex(h,x=>x is "ID" or "设施ID" or "设备ID" or "设施编码"),ti=Array.FindIndex(h,x=>x is "设施类型" or "节点类型" or "设备类型");
            if(ni<0||ri<0)continue;if(ii<0)throw new InvalidDataException("机房设施表缺少 ID 列："+Path.GetFileName(file.Path));
            var (fileKind,_) = RoomSourceKind(file.Type,file.Path);
            if(fileKind=="未识别来源"&&ti<0)throw new InvalidDataException("无法识别机房设施表类别："+Path.GetFileName(file.Path));
            if(fileKind!="未识别来源")importedKinds?.Add(fileKind);
            string source="类型表："+Path.GetFileName(file.Path);int row=1;
            while(rows.MoveNext())
            {
                ct.ThrowIfCancellationRequested();row++;var cells=rows.Current;string Get(int i)=>i>=0&&i<cells.Length?cells[i].Trim():"";
                string id=Get(ii),name=Get(ni);if(id.Length==0&&name.Length==0)continue;if(id.Length==0)throw new InvalidDataException($"机房设施表第 {row} 行缺少 ID：{Path.GetFileName(file.Path)}");
                var (kind,rank)=RoomSourceKind(file.Type.Length>0?file.Type:Get(ti),file.Path);
                if(kind=="未识别来源")throw new InvalidDataException($"机房设施表第 {row} 行类别无法识别：{Path.GetFileName(file.Path)}");
                importedKinds?.Add(kind);
                result.Add(new(id,name,Get(ri),source,kind,rank,row));
            }
        }
        return result;
    }

    static void EnsureResourceRooms(SqliteConnection c)
    {
        using(var exists=c.CreateCommand()){exists.CommandText="SELECT count(*) FROM sqlite_master WHERE name='rooms' AND type='table'";if((long)exists.ExecuteScalar()!>0)return;}
        using var tx=c.BeginTransaction();using(var create=c.CreateCommand()){create.Transaction=tx;create.CommandText="CREATE TABLE rooms(name TEXT,room TEXT,PRIMARY KEY(name,room))";create.ExecuteNonQuery();}
        using var read=c.CreateCommand();read.Transaction=tx;read.CommandText="SELECT raw FROM edges";using var r=read.ExecuteReader();using var put=c.CreateCommand();put.Transaction=tx;put.CommandText="INSERT OR IGNORE INTO rooms VALUES($n,$r)";put.Parameters.AddWithValue("$n","");put.Parameters.AddWithValue("$r","");
        while(r.Read())
        {
            var raw=JsonSerializer.Deserialize<Dictionary<string,string>>(r.GetString(0))!;
            foreach(var end in new[]{"始端","终端"}){string name=raw.GetValueOrDefault(end+"设施","").Trim(),room=raw.GetValueOrDefault(end+"机房","").Trim();if(name.Length==0||!HasRoom(room))continue;put.Parameters[0].Value=name;put.Parameters[1].Value=room;put.ExecuteNonQuery();}
        }
        r.Close();tx.Commit();
    }
    static List<(string Name,string Room,string Source)> ResourceRooms(SqliteConnection c)
    {
        using var q=c.CreateCommand();q.CommandText="SELECT name,room FROM rooms";using var r=q.ExecuteReader();var result=new List<(string,string,string)>();while(r.Read())result.Add((r.GetString(0),r.GetString(1),"中继段表"));return result;
    }
    public List<RoomAssignment> PreviewRoomFiles(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        var rows=ReadRoomFacilities(files,ct);var conflicts=new HashSet<string>(StringComparer.Ordinal);var winners=new List<RoomFacility>();
        foreach(var group in rows.GroupBy(x=>x.Id.Length>0?"ID:"+x.Id:"NAME:"+x.Name,StringComparer.Ordinal))
        {
            int rank=group.Max(x=>x.Rank);var selected=group.Where(x=>x.Rank==rank).ToArray();winners.AddRange(selected);if(selected.Length>1)foreach(var x in selected)conflicts.Add(x.Name);
        }
        foreach(var group in rows.Where(x=>x.Id.Length>0&&x.Name.Length>0).GroupBy(x=>x.Name,StringComparer.Ordinal))if(group.Select(x=>x.Id).Distinct(StringComparer.Ordinal).Skip(1).Any())conflicts.Add(group.Key);
        return winners.Where(x=>x.Name.Length>0&&UsableRoom(x.Room)).Distinct().Select(x=>new RoomAssignment(x.Name,x.Room,x.Source,conflicts.Contains(x.Name)?"需核对":"已确认",conflicts.Contains(x.Name)?"同级 ID 重复或同名不同 ID；导入后请在机房管理确认": "",false,false)).ToList();
    }
    public long CountRoomRemovals(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        var kinds=new HashSet<string>(StringComparer.Ordinal);var rows=ReadRoomFacilities(files,ct,kinds);
        using var c=Open(Personal);long total=0;using var q=c.CreateCommand();q.CommandText="SELECT count(*) FROM room_facilities WHERE kind=$kind AND id NOT IN (SELECT value FROM json_each($ids))";q.Parameters.AddWithValue("$kind","");q.Parameters.AddWithValue("$ids","");
        foreach(string kind in kinds){ct.ThrowIfCancellationRequested();q.Parameters["$kind"].Value=kind;q.Parameters["$ids"].Value=JsonSerializer.Serialize(rows.Where(x=>x.Kind==kind).Select(x=>x.Id).Distinct(StringComparer.Ordinal));total+=(long)q.ExecuteScalar()!;}
        return total;
    }
    public void ApplyRoomFiles(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        var inputs=files.ToArray();var kinds=new HashSet<string>(StringComparer.Ordinal);var values=ReadRoomFacilities(inputs,ct,kinds);
        foreach(var group in values.GroupBy(x=>(x.Kind,x.Id)))if(group.Select(x=>(x.Name,x.Room)).Distinct().Skip(1).Any())throw new InvalidDataException($"同一设施表类别中 ID {group.Key.Id} 对应不同设施或机房，请先核对原表");
        using(var c=Open(Personal))using(var tx=c.BeginTransaction())
        {
            using(var remove=c.CreateCommand())
            {
                remove.Transaction=tx;remove.CommandText="DELETE FROM room_facilities WHERE kind=$kind AND id NOT IN (SELECT value FROM json_each($ids))";
                remove.Parameters.AddWithValue("$kind","");remove.Parameters.AddWithValue("$ids","");
                foreach(string kind in kinds){ct.ThrowIfCancellationRequested();remove.Parameters["$kind"].Value=kind;remove.Parameters["$ids"].Value=JsonSerializer.Serialize(values.Where(x=>x.Kind==kind).Select(x=>x.Id).Distinct(StringComparer.Ordinal));remove.ExecuteNonQuery();using var mark=c.CreateCommand();mark.Transaction=tx;mark.CommandText="INSERT OR IGNORE INTO room_kind_imports VALUES($kind)";mark.Parameters.AddWithValue("$kind",kind);mark.ExecuteNonQuery();}
            }
            using var put=c.CreateCommand();put.Transaction=tx;put.CommandText="INSERT INTO room_facilities VALUES($id,$name,$room,$source,$kind,$rank,$row) ON CONFLICT(kind,id) DO UPDATE SET name=excluded.name,room=excluded.room,source=excluded.source,rank=excluded.rank,rownum=excluded.rownum WHERE name<>excluded.name OR room<>excluded.room OR rank<>excluded.rank";
            foreach(string key in new[]{"$id","$name","$room","$source","$kind","$rank","$row"})put.Parameters.AddWithValue(key,"");
            foreach(var x in values.DistinctBy(x=>(x.Kind,x.Id))){ct.ThrowIfCancellationRequested();object[] row=[x.Id,x.Name,x.Room.Contains("挂墙",StringComparison.OrdinalIgnoreCase)?"":x.Room,x.Source,x.Kind,x.Rank,x.Row];for(int i=0;i<row.Length;i++)put.Parameters[i].Value=row[i];put.ExecuteNonQuery();}
            ct.ThrowIfCancellationRequested();tx.Commit();
        }
        ApplyFacilityEvidence(inputs,ct);Reload();
    }
    public void ApplyRoomBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;if(new FileInfo(path).Length>20_000_000)throw new InvalidDataException("机房归属基线超过允许大小");
        byte[] bytes=File.ReadAllBytes(path);string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));if(Setting("room-baseline-hash")==hash)return;
        var rows=JsonSerializer.Deserialize<RoomSeed[]>(bytes)??throw new InvalidDataException("机房归属基线格式错误");if(rows.Any(x=>string.IsNullOrWhiteSpace(x.Name)||!HasRoom(x.Room)||!x.Source.StartsWith("类型表：")))throw new InvalidDataException("机房归属基线字段错误");
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT DISTINCT source FROM room_members";using var reader=q.ExecuteReader();var existing=new HashSet<string>(StringComparer.Ordinal);while(reader.Read())existing.Add(reader.GetString(0));reader.Close();
        var missing=rows.Where(x=>!existing.Contains(x.Source)).ToArray();ApplyRoomRows(missing,missing.Select(x=>x.Source).Distinct(),ct);SetSetting("room-baseline-hash",hash);
    }
    public void ApplyRoomFacilityBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;using var stream=File.OpenRead(path);string hash=Convert.ToHexString(SHA256.HashData(stream));
        if(Setting("room-facility-baseline-hash")==hash)return;
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());source.Open();
        using(var check=source.CreateCommand()){check.CommandText="PRAGMA quick_check";if((string?)check.ExecuteScalar()!="ok")throw new InvalidDataException("机房设施基线完整性校验失败");}
        using var target=Open(Personal);EnsureRoomFacilities(target);using var tx=target.BeginTransaction();
        using var kinds=source.CreateCommand();kinds.CommandText="SELECT DISTINCT kind FROM room_facilities";using var reader=kinds.ExecuteReader();
        while(reader.Read())
        {
            ct.ThrowIfCancellationRequested();string kind=reader.GetString(0);using var existing=target.CreateCommand();existing.Transaction=tx;existing.CommandText="SELECT (SELECT count(*) FROM room_facilities WHERE kind=$kind)+(SELECT count(*) FROM room_kind_imports WHERE kind=$kind)";existing.Parameters.AddWithValue("$kind",kind);if((long)existing.ExecuteScalar()!>0)continue;
            using var rows=source.CreateCommand();rows.CommandText="SELECT id,name,room,source,kind,rank,rownum FROM room_facilities WHERE kind=$kind";rows.Parameters.AddWithValue("$kind",kind);using var result=rows.ExecuteReader();
            using var put=target.CreateCommand();put.Transaction=tx;put.CommandText="INSERT OR IGNORE INTO room_facilities VALUES($id,$name,$room,$source,$kind,$rank,$row)";foreach(string key in new[]{"$id","$name","$room","$source","$kind","$rank","$row"})put.Parameters.AddWithValue(key,"");
            while(result.Read()){ct.ThrowIfCancellationRequested();for(int i=0;i<7;i++)put.Parameters[i].Value=result.GetValue(i);put.ExecuteNonQuery();}
        }
        ct.ThrowIfCancellationRequested();tx.Commit();SetSetting("room-facility-baseline-hash",hash);Reload();
    }
    void ApplyRoomRows(IEnumerable<RoomSeed> rows,IEnumerable<string> sources,CancellationToken ct)
    {
        var values=rows.Where(x=>UsableRoom(x.Room)).Distinct().ToArray();using var c=Open(Personal);using var tx=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="DELETE FROM room_members WHERE source=$s";q.Parameters.AddWithValue("$s","");
        foreach(var source in sources.Distinct()){ct.ThrowIfCancellationRequested();q.Parameters[0].Value=source;q.ExecuteNonQuery();}
        q.CommandText="DELETE FROM room_members WHERE instr(room,'挂墙')>0";q.ExecuteNonQuery();q.CommandText="INSERT OR IGNORE INTO room_members VALUES($n,$r,$s)";q.Parameters.AddWithValue("$n","");q.Parameters.AddWithValue("$r","");foreach(var x in values){ct.ThrowIfCancellationRequested();q.Parameters["$n"].Value=x.Name;q.Parameters["$r"].Value=x.Room;q.Parameters["$s"].Value=x.Source;q.ExecuteNonQuery();}ct.ThrowIfCancellationRequested();tx.Commit();Reload();
    }
    public List<RoomAssignment> RoomAssignments(Graph? graph=null)
    {
        return RoomAssignmentsFromFacilities(graph??Graph);
    }
    List<RoomAssignment> RoomAssignmentsFromFacilities(Graph g)
    {
        var values=new List<(string Name,string Room,string Source)>();
        var conflicts=new Dictionary<string,List<string>>(StringComparer.Ordinal);
        using var c=Open(Personal);
        var facilities=new List<RoomFacility>();using(var q=c.CreateCommand()){q.CommandText="SELECT id,name,room,source,kind,rank,rownum FROM room_facilities";using var r=q.ExecuteReader();while(r.Read())facilities.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetString(4),r.GetInt32(5),r.GetInt32(6)));}
        foreach(var group in facilities.GroupBy(x=>x.Id.Length>0?"ID:"+x.Id:"NAME:"+x.Name,StringComparer.Ordinal))
        {
            int rank=group.Max(x=>x.Rank);var winners=group.Where(x=>x.Rank==rank).ToArray();
            if(winners.Length>1)foreach(var x in winners.Where(x=>x.Name.Length>0)){
                if(!conflicts.TryGetValue(x.Name,out var reasons))conflicts[x.Name]=reasons=[];
                reasons.Add("同一设施 ID 在同优先级来源重复，需人工核对："+string.Join('；',winners.Select(w=>w.Kind+"/"+w.Source+"/第"+w.Row+"行")));
            }
            foreach(var x in winners.Where(x=>x.Name.Length>0&&UsableRoom(x.Room)))values.Add((x.Name,x.Room,x.Source+"（"+x.Kind+"；ID="+(x.Id.Length>0?x.Id:"缺失")+"）"));
        }
        foreach(var group in facilities.Where(x=>x.Name.Length>0&&x.Id.Length>0).GroupBy(x=>x.Name,StringComparer.Ordinal).Where(x=>x.Select(v=>v.Id).Distinct(StringComparer.Ordinal).Skip(1).Any()))
        {
            if(!conflicts.TryGetValue(group.Key,out var reasons))conflicts[group.Key]=reasons=[];
            reasons.Add("同名设施对应不同 ID，中继段端点仅有名称，需核对归属："+string.Join('；',group.Select(x=>x.Id).Distinct(StringComparer.Ordinal)));
        }
        var choices=new Dictionary<string,(string Room,bool Enabled)>();using(var q=c.CreateCommand()){q.CommandText="SELECT name,room,enabled FROM room_choices";using var r=q.ExecuteReader();while(r.Read())choices[r.GetString(0)]=(r.GetString(1),r.GetInt32(2)==1);}
        var reviews=new Dictionary<string,string>(StringComparer.Ordinal);using(var q=c.CreateCommand()){q.CommandText="SELECT name,signature FROM room_reviews";using var r=q.ExecuteReader();while(r.Read())reviews[r.GetString(0)]=r.GetString(1);}
        var known=g.Nodes.ToDictionary(n=>n.Name,StringComparer.Ordinal);
        values=values.Where(x=>UsableRoom(x.Room)).ToList();var sourceNames=values.Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        return values.Where(x=>known.ContainsKey(x.Name)).GroupBy(x=>x.Name,StringComparer.Ordinal).Select(group=>{
            var n=known[group.Key];bool cabinet=IsCabinet(n);var rooms=group.Select(x=>x.Room).Distinct(StringComparer.Ordinal).ToArray();string room=rooms.Length==1?rooms[0]:string.Join("；",rooms),reason=rooms.Length>1?"同一设施对应多个所属机房":"",source=string.Join('；',group.Select(x=>x.Source).Distinct());bool enable=rooms.Length==1;
            if(conflicts.TryGetValue(n.Name,out var issues)){reason=string.Join('；',issues.Distinct());enable=false;}
            if(choices.TryGetValue(n.Name,out var choice)){source+="；人工确认";if(rooms.Contains(choice.Room,StringComparer.Ordinal)){room=choice.Room;if(!conflicts.ContainsKey(n.Name)||reviews.GetValueOrDefault(n.Name)==reason){reason="";enable=choice.Enabled;}else enable=false;}else{reason="源数据与既有人工确认归属不一致；原人工归属："+choice.Room;enable=false;}}
            if(!sourceNames.Contains(n.Name)){reason="原所属机房来源已移除，保留人工记录并暂停归并";enable=false;}
            if(rooms.Any(SuspiciousRoom)){reason="疑似安装位置（挂墙/壁挂），所属机房需要人工复核";enable=false;}
            return new RoomAssignment(n.Name,room,source,reason.Length>0?"需核对":"已确认",reason.Length>0?reason:!enable?"已人工暂停归并":"",enable,cabinet);
        }).OrderBy(x=>x.Room,StringComparer.Ordinal).ThenBy(x=>x.Name,StringComparer.Ordinal).ToList();
    }
    public void ConfirmRoom(string name,string room,bool enabled)
    {
        var match=RoomAssignments().FirstOrDefault(x=>x.Name==name)??throw new InvalidDataException("未找到设施的所属机房记录");
        if(enabled&&SuspiciousRoom(room))throw new InvalidDataException("该名称疑似安装位置，请先核对并更正源表中的真实所属机房，再重新导入。当前已暂停归并。");
        if(!match.Room.Split('；').Contains(room,StringComparer.Ordinal))throw new InvalidDataException("只能从表格已有的所属机房中选择，请先更正并导入源表。");
        using var c=Open(Personal);using var tx=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="INSERT OR REPLACE INTO room_choices VALUES($n,$r,$e)";q.Parameters.AddWithValue("$n",name);q.Parameters.AddWithValue("$r",room);q.Parameters.AddWithValue("$e",enabled?1:0);q.ExecuteNonQuery();
        using var review=c.CreateCommand();review.Transaction=tx;review.CommandText="INSERT OR REPLACE INTO room_reviews VALUES($n,$s)";review.Parameters.AddWithValue("$n",name);
        string? previous=null;using(var old=c.CreateCommand()){old.Transaction=tx;old.CommandText="SELECT signature FROM room_reviews WHERE name=$n";old.Parameters.AddWithValue("$n",name);previous=old.ExecuteScalar() as string;}
        review.Parameters.AddWithValue("$s",match.Reason.Contains("ID",StringComparison.Ordinal)?match.Reason:previous??"");review.ExecuteNonQuery();tx.Commit();Reload();
    }
    IEnumerable<ClusterRecord> RoutingGroups(Graph g)
    {
        var allRooms=RoomAssignmentsFromFacilities(g);var rooms=allRooms.Where(x=>x.Enabled&&x.Status=="已确认").ToArray();var affiliation=rooms.ToDictionary(x=>x.Name,x=>x.Room,StringComparer.Ordinal);var blocked=allRooms.Where(x=>x.Status=="需核对"||x.IsCabinet&&!x.Enabled).Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        var names=g.Nodes.ToDictionary(n=>n.Name,n=>n.Id,StringComparer.Ordinal);foreach(var x in allRooms)g.RoomAffiliations[names[x.Name]]=x;var parent=Enumerable.Range(0,g.Nodes.Length).ToArray();int Root(int n){while(parent[n]!=n){parent[n]=parent[parent[n]];n=parent[n];}return n;}void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
        foreach(var room in rooms.GroupBy(x=>x.Room,StringComparer.Ordinal)){g.Rooms[room.Key]=room.Select(x=>names[x.Name]).ToArray();int first=names[room.First().Name];foreach(var n in room)Join(first,names[n.Name]);}
        foreach(var cluster in Clusters(g).Where(x=>x.Enabled&&x.Status=="已确认").Select(x=>x with{Members=x.Members.Where(names.ContainsKey).ToArray()}).Where(x=>x.Members.Length>=2))
        {
            if(cluster.Members.Any(blocked.Contains)){g.GroupWarnings[cluster.Id]="成员机房归属待核对或光交箱尚未确认";continue;}
            // Explicit clusters cannot accidentally join two distinct room cores, including transitive joins.
            var roots=cluster.Members.Select(n=>Root(names[n])).ToHashSet();var touched=rooms.Where(x=>roots.Contains(Root(names[x.Name]))).Select(x=>x.Room).Distinct(StringComparer.Ordinal).ToArray();if(touched.Length>1){g.GroupWarnings[cluster.Id]="集群关系跨越多个所属机房，未合并";continue;}
            int first=names[cluster.Members[0]];foreach(var n in cluster.Members)Join(first,names[n]);
        }
        var clusterNames=Clusters(g).Where(x=>x.Enabled&&x.Status=="已确认").SelectMany(x=>x.Members.Where(names.ContainsKey).Select(n=>(Name:n,GroupName:x.Name))).GroupBy(x=>x.Name).ToDictionary(x=>x.Key,x=>x.First().GroupName);
        foreach(var group in g.Nodes.GroupBy(n=>Root(n.Id)).Where(x=>x.Count()>1))
        {
            var members=group.Select(n=>n.Name).ToArray();string? room=members.Where(affiliation.ContainsKey).Select(n=>affiliation[n]).FirstOrDefault();string title=room??members.Where(clusterNames.ContainsKey).Select(n=>clusterNames[n]).FirstOrDefault()??members[0];
            yield return new("logical-"+group.Key,title,room!=null?"机房":"集群",members,"有效归属关系","已确认","",true);
        }
    }
}
