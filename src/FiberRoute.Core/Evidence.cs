using Microsoft.Data.Sqlite;
using System.Security.Cryptography;

namespace FiberRoute.Core;

public sealed record FacilityEvidence(string Name,string Region,string Room,string Source,int Row,string Field,string Sheet="");
public sealed partial class Store
{
    static void EnsureEvidence(SqliteConnection c)=>Run(c,"CREATE TABLE IF NOT EXISTS facility_evidence(name TEXT,region TEXT,room TEXT,source TEXT,rownum INTEGER,field TEXT,sheet TEXT,PRIMARY KEY(name,source,rownum)) WITHOUT ROWID; CREATE TABLE IF NOT EXISTS evidence_sources(source TEXT PRIMARY KEY,signature TEXT)");
    public FacilityEvidence[] EvidenceFor(string name)
    {
        using var c=Open(Personal);EnsureEvidence(c);using var q=c.CreateCommand();q.CommandText="SELECT name,region,room,source,rownum,field,sheet FROM facility_evidence WHERE name=$n ORDER BY source,rownum LIMIT 50";q.Parameters.AddWithValue("$n",name);using var r=q.ExecuteReader();var values=new List<FacilityEvidence>();while(r.Read())values.Add(new(r.GetString(0),r.GetString(1),r.GetString(2),r.GetString(3),r.GetInt32(4),r.GetString(5),r.GetString(6)));return values.ToArray();
    }
    public void ApplyFacilityEvidence(IEnumerable<(string Path,string Type,string? Sheet)> inputs,CancellationToken ct=default)
    {
        var files=inputs.ToArray();
        using var c=Open(Personal);EnsureEvidence(c);using var tx=c.BeginTransaction();
        foreach(var input in files)
        {
            ct.ThrowIfCancellationRequested();string source=Path.GetFileName(input.Path);using var stream=File.OpenRead(input.Path);string signature=Convert.ToHexString(SHA256.HashData(stream))+"|"+input.Sheet;
            using var old=c.CreateCommand();old.Transaction=tx;old.CommandText="SELECT signature FROM evidence_sources WHERE source=$s";old.Parameters.AddWithValue("$s",source);if((string?)old.ExecuteScalar()==signature)continue;
            using var rows=ReadTypeRows(input.Path,input.Sheet).GetEnumerator();if(!rows.MoveNext())continue;string[] headers=rows.Current.Select(h=>h.Trim()).ToArray();int ni=Array.FindIndex(headers,h=>h is "设施名称" or "节点名称" or "设备名称"),ri=Array.FindIndex(headers,h=>h is "所属区县" or "区域" or "所属区域"),room=Array.FindIndex(headers,h=>h is "机房名称" or "所属机房");if(ni<0)continue;
            using var clear=c.CreateCommand();clear.Transaction=tx;clear.CommandText="DELETE FROM facility_evidence WHERE source=$s";clear.Parameters.AddWithValue("$s",source);clear.ExecuteNonQuery();
            using var put=c.CreateCommand();put.Transaction=tx;put.CommandText="INSERT OR REPLACE INTO facility_evidence VALUES($n,$r,$m,$s,$i,$f,$h)";foreach(string p in new[]{"$n","$r","$m","$s","$i","$f","$h"})put.Parameters.AddWithValue(p,"");int row=1;
            while(rows.MoveNext())
            {
                ct.ThrowIfCancellationRequested();row++;var cells=rows.Current;string Get(int index)=>index>=0&&index<cells.Length?cells[index].Trim():"";string name=Get(ni);if(name.Length==0)continue;
                object[] values=[name,Get(ri),Get(room),source,row,room>=0?FieldCaption(room,headers[room]):"",input.Sheet??""];for(int i=0;i<values.Length;i++)put.Parameters[i].Value=values[i];put.ExecuteNonQuery();
            }
            using var stamp=c.CreateCommand();stamp.Transaction=tx;stamp.CommandText="INSERT OR REPLACE INTO evidence_sources VALUES($s,$v)";stamp.Parameters.AddWithValue("$s",source);stamp.Parameters.AddWithValue("$v",signature);stamp.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();tx.Commit();ApplyStationFiles(files,ct);Reload();
    }
    static string FieldCaption(int index,string name){string column="";for(int value=index+1;value>0;value=(value-1)/26)column=(char)('A'+(value-1)%26)+column;return column+"列 · "+name;}
    public void ApplyEvidenceBaseline(string path,CancellationToken ct=default)
    {
        if(!File.Exists(path))return;using var file=File.OpenRead(path);string hash=Convert.ToHexString(SHA256.HashData(file));if(Setting("evidence-baseline-hash")==hash)return;
        using var source=new SqliteConnection(new SqliteConnectionStringBuilder{DataSource=path,Mode=SqliteOpenMode.ReadOnly,Pooling=false}.ToString());source.Open();using(var check=source.CreateCommand()){check.CommandText="PRAGMA quick_check";if((string?)check.ExecuteScalar()!="ok")throw new InvalidDataException("来源基线完整性校验失败");}
        using var target=Open(Personal);EnsureEvidence(target);using var tx=target.BeginTransaction();using var list=source.CreateCommand();list.CommandText="SELECT source,signature FROM evidence_sources";using var files=list.ExecuteReader();
        while(files.Read())
        {
            ct.ThrowIfCancellationRequested();string name=files.GetString(0),signature=files.GetString(1);using var exists=target.CreateCommand();exists.Transaction=tx;exists.CommandText="SELECT count(*) FROM evidence_sources WHERE source=$s";exists.Parameters.AddWithValue("$s",name);if((long)exists.ExecuteScalar()!>0)continue;
            using var read=source.CreateCommand();read.CommandText="SELECT name,region,room,source,rownum,field,sheet FROM facility_evidence WHERE source=$s";read.Parameters.AddWithValue("$s",name);using var rows=read.ExecuteReader();using var put=target.CreateCommand();put.Transaction=tx;put.CommandText="INSERT OR IGNORE INTO facility_evidence VALUES($a,$b,$c,$d,$e,$f,$g)";foreach(string parameter in new[]{"$a","$b","$c","$d","$e","$f","$g"})put.Parameters.AddWithValue(parameter,"");
            while(rows.Read()){ct.ThrowIfCancellationRequested();for(int i=0;i<7;i++)put.Parameters[i].Value=rows.GetValue(i);put.ExecuteNonQuery();}
            using var stamp=target.CreateCommand();stamp.Transaction=tx;stamp.CommandText="INSERT INTO evidence_sources VALUES($s,$v)";stamp.Parameters.AddWithValue("$s",name);stamp.Parameters.AddWithValue("$v",signature);stamp.ExecuteNonQuery();
        }
        ct.ThrowIfCancellationRequested();tx.Commit();SetSetting("evidence-baseline-hash",hash);Reload();
    }
}
