using Microsoft.Data.Sqlite;
using System.Globalization;
using System.Text;
using System.Text.Json;
namespace FiberRoute.Core;
public sealed partial class Store
{
    public string Root {get;} public string Database=>Path.Combine(Root,"resources.db");
    public string Personal=>Path.Combine(Root,"personal.db"); public Graph Graph {get;private set;}=new([],[],"未导入");
    public Store(string root) {Root=root;Directory.CreateDirectory(root);using var c=Open(Personal);Run(c,"CREATE TABLE IF NOT EXISTS types(name TEXT PRIMARY KEY,type TEXT NOT NULL); CREATE TABLE IF NOT EXISTS settings(name TEXT PRIMARY KEY,value TEXT NOT NULL)");InitMetadata();using(var stations=Open(Personal))EnsureStations(stations);Reload();}
    static SqliteConnection Open(string path) { var c=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Pooling=false}.ToString());c.Open();return c; }
    static void Run(SqliteConnection c,string sql) {using var q=c.CreateCommand();q.CommandText=sql;q.ExecuteNonQuery();}
    public string? Setting(string name) {using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT value FROM settings WHERE name=$n";q.Parameters.AddWithValue("$n",name);return q.ExecuteScalar() as string;}
    public void SetSetting(string name,string value) {using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="INSERT OR REPLACE INTO settings VALUES($n,$v)";q.Parameters.AddWithValue("$n",name);q.Parameters.AddWithValue("$v",value);q.ExecuteNonQuery();}
    public Dictionary<string,string> ManualTypes() {using var c=Open(Personal);using var q=c.CreateCommand();q.CommandText="SELECT name,type FROM types";using var r=q.ExecuteReader();var d=new Dictionary<string,string>(StringComparer.Ordinal);while(r.Read())d[r.GetString(0)]=r.GetString(1);return d;}
    public void SetTypes(IEnumerable<TypeChange> changes,string source="人工修改")
    {
        using var c=Open(Personal);using var t=c.BeginTransaction();using var q=c.CreateCommand();q.Transaction=t;q.CommandText="INSERT OR REPLACE INTO types VALUES($n,$v)";q.Parameters.Add("$n",SqliteType.Text);q.Parameters.Add("$v",SqliteType.Text);
        var known=Graph.Nodes.Select(n=>n.Name).ToHashSet(StringComparer.Ordinal);
        var changed=changes.Where(x=>x.Status=="可更新").ToArray();
        foreach(var x in changed) {if(!known.Contains(x.Name)||string.IsNullOrWhiteSpace(x.After))continue;q.Parameters[0].Value=x.Name;q.Parameters[1].Value=ClassifyType(x.Name,x.After);q.ExecuteNonQuery();}t.Commit();foreach(var x in changed)SaveTypeMeta(x.Name,source,"已确认","");Reload();
    }
    public List<TypeChange> PreviewTypes(string path,string encoding,string nameColumn,string typeColumn,string? sheet=null,CancellationToken ct=default)
    {
        using var it=TableInput.Read(path,encoding,sheet).GetEnumerator(); if(!it.MoveNext())throw new InvalidDataException("类型表为空");var h=it.Current;int ni=Array.IndexOf(h,nameColumn),ti=Array.IndexOf(h,typeColumn);if(ni<0||ti<0)throw new InvalidDataException("类型表字段映射无效");
        var known=Graph.Nodes.ToDictionary(n=>n.Name,n=>n.Type,StringComparer.Ordinal);var rows=new List<(string Name,string Type)>();
        while(it.MoveNext()) {ct.ThrowIfCancellationRequested();var r=it.Current;rows.Add((ni<r.Length?r[ni].Trim():"",ti<r.Length?r[ti].Trim():""));}
        var conflicts=rows.GroupBy(x=>x.Name).Where(g=>g.Select(x=>x.Type).Distinct().Count()>1).Select(g=>g.Key).ToHashSet();
        return rows.Select(x=>new TypeChange(x.Name,known.GetValueOrDefault(x.Name,""),x.Type,conflicts.Contains(x.Name)?"同名类型冲突":string.IsNullOrWhiteSpace(x.Type)?"空类型":!known.ContainsKey(x.Name)?"未匹配":"可更新")).ToList();
    }
    public void Reload()
    {
        Graph=File.Exists(Database)?LoadGraph(Database,ManualTypes()):new([],[],"未导入");
    }
    Graph LoadGraph(string path,Dictionary<string,string> manual)
    {
        using var c=Open(path);
        var ns=new List<Node>();using(var q=c.CreateCommand()){q.CommandText="SELECT id,name,type FROM nodes ORDER BY id";using var r=q.ExecuteReader();while(r.Read()){var name=r.GetString(1);bool confirmed=manual.TryGetValue(name,out var type);ns.Add(new(r.GetInt32(0),name,ClassifyType(name,confirmed?type!:r.GetString(2)),confirmed));}}
        var es=new List<Edge>();using(var q=c.CreateCommand()){q.CommandText="SELECT id,business_id,name,a,b,length,free,total,CASE WHEN json_valid(raw) THEN COALESCE(json_extract(raw,'$.始端区域'),'') ELSE '' END,CASE WHEN json_valid(raw) THEN COALESCE(json_extract(raw,'$.终端区域'),'') ELSE '' END,CASE WHEN json_valid(raw) THEN COALESCE(json_extract(raw,'$.业务状态'),'') ELSE '' END FROM edges ORDER BY id";using var r=q.ExecuteReader();while(r.Read())es.Add(new(r.GetInt32(0),r.GetString(1),r.GetString(2),r.GetInt32(3),r.GetInt32(4),r.IsDBNull(5)?null:r.GetDouble(5),r.GetInt32(6),r.GetInt32(7),"",r.GetString(8).Trim(),r.GetString(9).Trim(),r.GetString(10).Trim()));}
        var regions=ResolveRegions(Decorate(ns.ToArray()),es.ToArray());
        using var v=c.CreateCommand();v.CommandText="SELECT value FROM meta WHERE name='version'";var graph=new Graph(regions.Nodes,regions.Edges,v.ExecuteScalar() as string??"未知");
        EnsureResourceRooms(c);graph.SetClusters(RoutingGroups(graph,ResourceRooms(c)));return graph;
    }
    public string Raw(int index) {using var c=Open(Database);using var q=c.CreateCommand();q.CommandText="SELECT raw FROM edges WHERE id=$i";q.Parameters.AddWithValue("$i",index);return q.ExecuteScalar() as string??"{}";}
    public static readonly string[] Fields=["ID","名称","长度","空闲数量","中继纤芯数量","始端设施","终端设施","始端类型","终端类型"];
    public static readonly string[] Required=["ID","空闲数量","中继纤芯数量","始端设施","终端设施"];
    public async Task<ImportResult> ImportAsync(string path,string encoding,Dictionary<string,string> mapping,double unit,IProgress<ImportProgress>? progress,CancellationToken ct,string? sheet=null)
    {return await Task.Run(()=>Import(path,encoding,mapping,unit,progress,ct,sheet),ct);}
    ImportResult Import(string path,string encoding,Dictionary<string,string> mapping,double unit,IProgress<ImportProgress>? progress,CancellationToken ct,string? sheet)
    {
        if(unit!=1&&unit!=1000)throw new ArgumentException("长度单位无效");
        string digest;
        using(var input=File.OpenRead(path))digest=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(input));
        ct.ThrowIfCancellationRequested();string signature=digest+JsonSerializer.Serialize(new{encoding,sheet,unit,mapping=mapping.OrderBy(x=>x.Key).ToArray()});
        if(File.Exists(Database)){using var old=Open(Database);using var sig=old.CreateCommand();sig.CommandText="SELECT value FROM meta WHERE name='import_signature'";if((string?)sig.ExecuteScalar()==signature)return new(Graph.Edges.Length,Graph.Edges.Length,0,Graph.Nodes.Length,Graph.Version,Directory.GetFiles(Root,"导入异常-*.csv").OrderByDescending(File.GetLastWriteTimeUtc).FirstOrDefault()??"",true);}
        string temp=Path.Combine(Root,"import-"+Guid.NewGuid()+".db"),errors=Path.Combine(Root,"导入异常-"+DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+".csv");
        long read=0,accepted=0,invalid=0;var nodes=new Dictionary<string,int>(StringComparer.Ordinal);string version=DateTimeOffset.Now.ToString("yyyy-MM-dd HH:mm:ss zzz");
        try
        {
            using(var c=Open(temp))
            using(var report=new StreamWriter(errors,false,new UTF8Encoding(true)))
            {
                Run(c,"PRAGMA journal_mode=DELETE; CREATE TABLE nodes(id INTEGER PRIMARY KEY,name TEXT UNIQUE,type TEXT); CREATE TABLE edges(id INTEGER PRIMARY KEY,business_id TEXT,name TEXT,a INTEGER,b INTEGER,length REAL,free INTEGER,total INTEGER,raw TEXT); CREATE TABLE meta(name TEXT PRIMARY KEY,value TEXT); CREATE TABLE rooms(name TEXT,room TEXT,PRIMARY KEY(name,room));");
                using var it=TableInput.Read(path,encoding,sheet,mapping.GetValueOrDefault("ID")).GetEnumerator();if(!it.MoveNext())throw new InvalidDataException("数据表为空");var headers=it.Current.Select(x=>x.Trim()).ToArray();if(headers.Distinct().Count()!=headers.Length)throw new InvalidDataException("数据表头重名");
                var indexes=mapping.ToDictionary(x=>x.Key,x=>Array.IndexOf(headers,x.Value));foreach(var field in Required)if(!indexes.TryGetValue(field,out int ix)||ix<0)throw new InvalidDataException("缺少字段映射："+field);
                string Get(string[] row,string key)=>indexes.TryGetValue(key,out int ix)&&ix>=0&&ix<row.Length?row[ix].Trim():"";
                report.WriteLine(Csv.Line(new[]{"记录序号","原因","原始字段JSON"}));
                SqliteTransaction transaction=c.BeginTransaction();
                using var nq=c.CreateCommand();nq.CommandText="INSERT INTO nodes VALUES($id,$name,$type)";nq.Parameters.Add("$id",SqliteType.Integer);nq.Parameters.Add("$name",SqliteType.Text);nq.Parameters.Add("$type",SqliteType.Text);
                using var eq=c.CreateCommand();eq.CommandText="INSERT INTO edges VALUES($id,$business,$name,$a,$b,$length,$free,$total,$raw)";
                using var rq=c.CreateCommand();rq.CommandText="INSERT OR IGNORE INTO rooms VALUES($n,$r)";rq.Parameters.AddWithValue("$n","");rq.Parameters.AddWithValue("$r","");
                void Room(string name,string room){room=room.Trim();if(room.Length==0||room=="--"||room=="无")return;rq.Transaction=transaction;rq.Parameters[0].Value=name;rq.Parameters[1].Value=room;rq.ExecuteNonQuery();}
                foreach(var p in new[]{"$id","$business","$name","$a","$b","$length","$free","$total","$raw"})eq.Parameters.AddWithValue(p,"");
                int NodeId(string name,string type,string room)
                {
                    if(nodes.TryGetValue(name,out int existing))return existing; int id=nodes.Count;nodes.Add(name,id);
                    if(name.Contains("POS",StringComparison.OrdinalIgnoreCase))type="分纤箱";else if(string.IsNullOrWhiteSpace(type)) type=name.Contains("GJ",StringComparison.OrdinalIgnoreCase)?"光交箱":name.Contains("机房")||!string.IsNullOrWhiteSpace(room)?"机房":System.Text.RegularExpressions.Regex.IsMatch(name,@"(?:^|[/\-])GF\d*",System.Text.RegularExpressions.RegexOptions.IgnoreCase)?"分纤箱":"未识别";
                    nq.Transaction=transaction;nq.Parameters[0].Value=id;nq.Parameters[1].Value=name;nq.Parameters[2].Value=ClassifyType(name,type);nq.ExecuteNonQuery();return id;
                }
                try
                {
                    while(it.MoveNext())
                    {
                        ct.ThrowIfCancellationRequested();read++;var r=it.Current;
                        string reason="",id=Get(r,"ID"),a=Get(r,"始端设施"),b=Get(r,"终端设施"),len=Get(r,"长度");
                        int free=0,total=0;double? length=null;
                        if(r.Length!=headers.Length)reason="字段数量与表头不一致";
                        else if(id.Length==0||a.Length==0||b.Length==0)reason="ID或端点缺失";
                        else if(!int.TryParse(Get(r,"空闲数量"),NumberStyles.Integer,CultureInfo.InvariantCulture,out free)||!int.TryParse(Get(r,"中继纤芯数量"),NumberStyles.Integer,CultureInfo.InvariantCulture,out total)||free<0||total<0||free>total)reason="芯数非法";
                        else if(len.Length>0){if(double.TryParse(len,NumberStyles.Float,CultureInfo.InvariantCulture,out double value)&&double.IsFinite(value)&&value>=0&&double.IsFinite(value*unit))length=value*unit;else reason="长度非法（空值或零值可接入）";}
                        if(reason.Length>0){invalid++;report.WriteLine(Csv.Line(new object?[]{read+1,reason,JsonSerializer.Serialize(r)}));}
                        else
                        {
                            var raw=headers.Select((h,i)=>(h,i)).ToDictionary(x=>x.h,x=>r[x.i]);int ai=NodeId(a,Get(r,"始端类型"),raw.GetValueOrDefault("始端机房","")),bi=NodeId(b,Get(r,"终端类型"),raw.GetValueOrDefault("终端机房",""));
                            Room(a,raw.GetValueOrDefault("始端机房",""));Room(b,raw.GetValueOrDefault("终端机房",""));
                            eq.Transaction=transaction;object[] vals=[(int)accepted,id,Get(r,"名称"),ai,bi,length is null?DBNull.Value:length.Value,free,total,JsonSerializer.Serialize(raw,new JsonSerializerOptions{Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping})];for(int i=0;i<vals.Length;i++)eq.Parameters[i].Value=vals[i];eq.ExecuteNonQuery();accepted++;
                        }
                        if(read%1000==0){transaction.Commit();transaction.Dispose();transaction=c.BeginTransaction();progress?.Report(new(read,accepted,invalid,nodes.Count));}
                    }
                    transaction.Commit();
                }finally{transaction.Dispose();}
                if(accepted==0)throw new InvalidDataException("没有可接入的中继段，原数据库保持不变。异常报告："+errors);
                using var meta=c.CreateCommand();meta.CommandText="INSERT INTO meta VALUES('version',$v)";meta.Parameters.AddWithValue("$v",version);meta.ExecuteNonQuery();using(var signatureCommand=c.CreateCommand()){signatureCommand.CommandText="INSERT INTO meta VALUES('import_signature',$s)";signatureCommand.Parameters.AddWithValue("$s",signature);signatureCommand.ExecuteNonQuery();}
                Run(c,"CREATE INDEX edge_a ON edges(a); CREATE INDEX edge_b ON edges(b);");
                using var check=c.CreateCommand();check.CommandText="PRAGMA integrity_check";if((string?)check.ExecuteScalar()!="ok")throw new InvalidDataException("数据库完整性校验失败");
            }
            var stagedGraph=LoadGraph(temp,ManualTypes());
            ct.ThrowIfCancellationRequested();
            // All connections have closed. File.Replace is atomic on supported local Windows volumes.
            if(File.Exists(Database))File.Replace(temp,Database,Database+".bak",true);else File.Move(temp,Database);
            Graph=stagedGraph;progress?.Report(new(read,accepted,invalid,nodes.Count));return new(read,accepted,invalid,nodes.Count,version,errors);
        }finally{if(File.Exists(temp))File.Delete(temp);foreach(var stale in Directory.GetFiles(Root,"导入异常-*.csv").OrderByDescending(File.GetLastWriteTimeUtc).Skip(1))File.Delete(stale);}
    }
    public void Rollback() {string backup=Database+".bak";if(!File.Exists(backup))throw new FileNotFoundException("无上一版数据库");string temp=Database+".rollback";File.Copy(backup,temp,true);File.Replace(temp,Database,backup,true);Reload();}
}
