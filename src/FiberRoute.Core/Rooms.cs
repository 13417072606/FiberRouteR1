using Microsoft.Data.Sqlite;
using System.Text.Json;
using System.Text.RegularExpressions;
namespace FiberRoute.Core;

public sealed record RoomAssignment(string Name,string Room,string Source,string Status,string Reason,bool Enabled,bool IsCabinet);
public sealed record RoomSeed(string Name,string Room,string Source);

public sealed partial class Store
{
    // Room affiliation is an explicit field, never a station name, upstream room or name prefix.
    static bool HasRoom(string room)=>room.Trim() is not ("" or "--" or "无" or "未知" or "未填写");
    public static bool SuspiciousRoom(string room)=>Regex.IsMatch(room,@"挂墙|壁挂",RegexOptions.IgnoreCase);
    static bool IsCabinet(Node n)=>n.Type.Split(';').Contains("光交箱")||Regex.IsMatch(n.Name,@"(?:^|[/\-])GJ\d*$",RegexOptions.IgnoreCase);
    static bool IsRoomFacility(Node n)=>IsCabinet(n)||n.Type.Split(';').Contains("机房")||Regex.IsMatch(n.Name,@"(?:^|[/\-])(?:ODF|ODB)[A-Z0-9]*(?:$|[/\-])",RegexOptions.IgnoreCase);

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
        var values=new List<(string Name,string Room,string Source)>();var known=Graph.Nodes.ToDictionary(n=>n.Name,StringComparer.Ordinal);
        foreach(var file in files)
        {
            using var rows=ReadTypeRows(file.Path,file.Sheet).GetEnumerator();if(!rows.MoveNext())continue;var h=rows.Current.Select(x=>x.Trim()).ToArray();int ni=Array.FindIndex(h,x=>x is "设施名称" or "节点名称" or "设备名称"),ri=Array.FindIndex(h,x=>x is "机房名称" or "所属机房");if(ni<0||ri<0)continue;
            while(rows.MoveNext()){ct.ThrowIfCancellationRequested();var r=rows.Current;if(r.Length<=Math.Max(ni,ri))continue;string n=r[ni].Trim(),room=r[ri].Trim();bool eligible=known.TryGetValue(n,out var node)?IsRoomFacility(node):file.Type is "机房" or "光交箱"||Regex.IsMatch(n,@"(?:^|[/\-])(?:ODF|ODB|GJ)[A-Z0-9]*(?:$|[/\-])",RegexOptions.IgnoreCase);if(n.Length>0&&HasRoom(room)&&eligible)values.Add((n,room,"类型表："+Path.GetFileName(file.Path)));}
        }
        return values.Distinct().Select(x=>new RoomAssignment(x.Name,x.Room,x.Source,"已确认","",false,false)).ToList();
    }
    public void ApplyRoomFiles(IEnumerable<(string Path,string Type,string? Sheet)> files,CancellationToken ct=default)
    {
        var inputs=files.ToArray();var values=PreviewRoomFiles(inputs,ct);
        ApplyRoomRows(values.Select(x=>new RoomSeed(x.Name,x.Room,x.Source)),inputs.Select(f=>"类型表："+Path.GetFileName(f.Path)),ct);ApplyFacilityEvidence(inputs,ct);
    }
    public void ApplyRoomBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;if(new FileInfo(path).Length>20_000_000)throw new InvalidDataException("机房归属基线超过允许大小");
        byte[] bytes=File.ReadAllBytes(path);string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(bytes));if(Setting("room-baseline-hash")==hash)return;
        var rows=JsonSerializer.Deserialize<RoomSeed[]>(bytes)??throw new InvalidDataException("机房归属基线格式错误");if(rows.Any(x=>string.IsNullOrWhiteSpace(x.Name)||!HasRoom(x.Room)||!x.Source.StartsWith("类型表：")))throw new InvalidDataException("机房归属基线字段错误");
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT DISTINCT source FROM room_members";using var reader=q.ExecuteReader();var existing=new HashSet<string>(StringComparer.Ordinal);while(reader.Read())existing.Add(reader.GetString(0));reader.Close();
        var missing=rows.Where(x=>!existing.Contains(x.Source)).ToArray();ApplyRoomRows(missing,missing.Select(x=>x.Source).Distinct(),ct);SetSetting("room-baseline-hash",hash);
    }
    void ApplyRoomRows(IEnumerable<RoomSeed> rows,IEnumerable<string> sources,CancellationToken ct)
    {
        var values=rows.Distinct().ToArray();using var c=Open(Personal);using var tx=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=tx;q.CommandText="DELETE FROM room_members WHERE source=$s";q.Parameters.AddWithValue("$s","");
        foreach(var source in sources.Distinct()){ct.ThrowIfCancellationRequested();q.Parameters[0].Value=source;q.ExecuteNonQuery();}
        q.CommandText="INSERT OR IGNORE INTO room_members VALUES($n,$r,$s)";q.Parameters.AddWithValue("$n","");q.Parameters.AddWithValue("$r","");foreach(var x in values){ct.ThrowIfCancellationRequested();q.Parameters["$n"].Value=x.Name;q.Parameters["$r"].Value=x.Room;q.Parameters["$s"].Value=x.Source;q.ExecuteNonQuery();}ct.ThrowIfCancellationRequested();tx.Commit();Reload();
    }
    public List<RoomAssignment> RoomAssignments(Graph? graph=null)
    {
        var source=new List<(string Name,string Room,string Source)>();if(File.Exists(Database)){using var c=Open(Database);EnsureResourceRooms(c);source.AddRange(ResourceRooms(c));}return RoomAssignments(graph??Graph,source);
    }
    List<RoomAssignment> RoomAssignments(Graph g,List<(string Name,string Room,string Source)> values)
    {
        using var c=Open(Personal);using(var q=c.CreateCommand()){q.CommandText="SELECT name,room,source FROM room_members";using var r=q.ExecuteReader();while(r.Read())values.Add((r.GetString(0),r.GetString(1),r.GetString(2)));}
        var choices=new Dictionary<string,(string Room,bool Enabled)>();using(var q=c.CreateCommand()){q.CommandText="SELECT name,room,enabled FROM room_choices";using var r=q.ExecuteReader();while(r.Read())choices[r.GetString(0)]=(r.GetString(1),r.GetInt32(2)==1);}
        var known=g.Nodes.ToDictionary(n=>n.Name,StringComparer.Ordinal);
        var sourceNames=values.Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        foreach(var choice in choices.Where(x=>!sourceNames.Contains(x.Key)&&known.ContainsKey(x.Key)))values.Add((choice.Key,choice.Value.Room,"人工确认（原来源已移除）"));
        return values.Where(x=>HasRoom(x.Room)&&known.TryGetValue(x.Name,out var n)&&IsRoomFacility(n)).GroupBy(x=>x.Name,StringComparer.Ordinal).Select(group=>{
            var n=known[group.Key];bool cabinet=IsCabinet(n);var rooms=group.Select(x=>x.Room).Distinct(StringComparer.Ordinal).ToArray();string room=rooms.Length==1?rooms[0]:string.Join("；",rooms),reason=rooms.Length>1?"同一设施对应多个所属机房":"",source=string.Join('；',group.Select(x=>x.Source).Distinct());bool enable=rooms.Length==1;
            if(choices.TryGetValue(n.Name,out var choice)){source+="；人工确认";if(rooms.Contains(choice.Room,StringComparer.Ordinal)){room=choice.Room;reason="";enable=choice.Enabled;}else{reason="源数据与既有人工确认归属不一致；原人工归属："+choice.Room;enable=false;}}
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
        using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="INSERT OR REPLACE INTO room_choices VALUES($n,$r,$e)";q.Parameters.AddWithValue("$n",name);q.Parameters.AddWithValue("$r",room);q.Parameters.AddWithValue("$e",enabled?1:0);q.ExecuteNonQuery();Reload();
    }
    IEnumerable<ClusterRecord> RoutingGroups(Graph g,List<(string Name,string Room,string Source)> resourceRooms)
    {
        var allRooms=RoomAssignments(g,resourceRooms);var rooms=allRooms.Where(x=>x.Enabled&&x.Status=="已确认").ToArray();var affiliation=rooms.ToDictionary(x=>x.Name,x=>x.Room,StringComparer.Ordinal);var blocked=allRooms.Where(x=>x.Status=="需核对"||x.IsCabinet&&!x.Enabled).Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
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
