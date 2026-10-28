using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiberRoute.Core;

public sealed record RegionDecision(string Region,string Status,string Basis)
{
    public static readonly RegionDecision Missing=new("","未提供","本端缺少可可靠识别的区域信息");
}

// Public 2023 statistical division snapshot; embedded so desktop use stays offline.
public static class ShantouRegions
{
    static readonly Dictionary<string,string[]> Towns=Load();
    public static IReadOnlyList<string> Districts {get;}=Array.AsReadOnly(Towns.Keys.ToArray());
    static Dictionary<string,string[]> Load()
    {
        using var stream=typeof(ShantouRegions).Assembly.GetManifestResourceStream("FiberRoute.Core.shantou-regions.json")!;
        return JsonSerializer.Deserialize<Dictionary<string,string[]>>(stream)!;
    }
    public static string[] Candidates(string text,bool facilityName=false)
    {
        text=text.Trim();if(text.Length==0)return [];
        var found=new HashSet<string>(StringComparer.Ordinal);
        foreach(var (district,towns) in Towns)
        {
            string shortName=district[..^1];
            if(text.Contains(district,StringComparison.Ordinal)||district=="南澳县"&&text.Contains("南澳区",StringComparison.Ordinal)
                ||(!facilityName&&text==shortName)
                ||facilityName&&Regex.IsMatch(text,@"^(?:广东省)?汕头市?"+shortName))found.Add(district);
            if(towns.Any(t=>text.Contains(t,StringComparison.Ordinal)||!facilityName&&text==t.Replace("街道","").TrimEnd('镇')))found.Add(district);
        }
        return found.ToArray();
    }
    public static Dictionary<string,string> ExportFields(Dictionary<string,string> raw,Edge edge)
    {
        var result=new Dictionary<string,string>(raw);
        void Add(string end,string region,RegionDecision? decision)
        {
            result[end+"区域原值"]=raw.GetValueOrDefault(end+"区域","");result[end+"区域"]=region.Length==0?"未提供":region;
            result[end+"区域判定"]=decision?.Status??"未提供";result[end+"区域依据"]=decision?.Basis??"";
        }
        Add("始端",edge.StartRegion,edge.StartRegionDecision);Add("终端",edge.EndRegion,edge.EndRegionDecision);return result;
    }
}

public sealed partial class Store
{
    (Node[] Nodes,Edge[] Edges) ResolveRegions(Node[] nodes,Edge[] edges)
    {
        var areaCache=new Dictionary<string,string[]>(StringComparer.Ordinal);
        string[] Areas(string value){if(!areaCache.TryGetValue(value,out var result))areaCache[value]=result=ShantouRegions.Candidates(value);return result;}
        var explicitAreas=new Dictionary<int,HashSet<string>>();var evidence=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);
        void Add<TKey>(Dictionary<TKey,HashSet<string>> target,TKey key,IEnumerable<string> values) where TKey:notnull
        {foreach(string value in values){if(!target.TryGetValue(key,out var set))target[key]=set=new(StringComparer.Ordinal);set.Add(value);}}
        foreach(var edge in edges){Add(explicitAreas,edge.A,Areas(edge.StartRegion));Add(explicitAreas,edge.B,Areas(edge.EndRegion));}
        using(var c=Open(Personal))
        {
            EnsureEvidence(c);using var q=c.CreateCommand();q.CommandText="SELECT DISTINCT name,region FROM facility_evidence WHERE region<>''";using var r=q.ExecuteReader();
            while(r.Read())Add(evidence,r.GetString(0),Areas(r.GetString(1)));
        }
        RegionDecision Decide(Node node)
        {
            var resource=explicitAreas.GetValueOrDefault(node.Id)??[];var typed=evidence.GetValueOrDefault(node.Name)??[];
            var areas=resource.Concat(typed).Distinct(StringComparer.Ordinal).ToArray();
            if(areas.Length>1)return new("","需核对","本端明确区域来源冲突："+string.Join('、',areas.Order()));
            if(areas.Length==1)return new(areas[0],"已归并",typed.Contains(areas[0])?"同名设施表区域字段":"同名本端中继段区域字段");
            var named=ShantouRegions.Candidates(node.Name,true);
            return named.Length==1?new(named[0],"已推断","本端设施完整名称包含行政区或完整街镇名"):
                named.Length>1?new("","需核对","本端设施名称含多个行政区线索："+string.Join('、',named)):RegionDecision.Missing;
        }
        var decisions=nodes.ToDictionary(n=>n.Id,Decide);
        RegionDecision Endpoint(int id,string raw)
        {
            var decision=decisions[id];var direct=Areas(raw);
            return direct.Length==1&&decision.Status!="需核对"?new(direct[0],"已归并","本端区域字段："+raw):decision;
        }
        var resolvedEdges=edges.Select(e=>{var a=Endpoint(e.A,e.StartRegion);var b=Endpoint(e.B,e.EndRegion);return e with{StartRegion=a.Region,EndRegion=b.Region,StartRegionDecision=a,EndRegionDecision=b};}).ToArray();
        var resolvedNodes=nodes.Select(n=>n with{Region=decisions[n.Id].Region}).ToArray();
        return (resolvedNodes,resolvedEdges);
    }
}
