using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace FiberRoute.Core;

public sealed partial class Store
{
    // A table's device-name rows are evidence. Merely mentioning a name in a
    // member list is not evidence that the device exists.
    HashSet<string> ClusterFacilityNames()
    {
        using var c=Open(Personal);EnsureEvidence(c);EnsureStations(c);using var q=c.CreateCommand();
        q.CommandText="SELECT name FROM types UNION SELECT name FROM facility_evidence UNION SELECT name FROM facility_stations";
        using var r=q.ExecuteReader();var names=new HashSet<string>(StringComparer.Ordinal);while(r.Read())if(r.GetString(0).Length>0)names.Add(r.GetString(0));return names;
    }
    static string[] ParseClusterText(string text,HashSet<string> nameKeys)
    {
        if(nameKeys.Contains(MatchName(text)))return [text.Trim()];
        // Separate explicit list delimiters first; newlines may be a wrapped
        // name or a list separator. Join only when a complete reference exists.
        var chunks=new List<string>();var buffer=new StringBuilder();int depth=0;
        void Flush(){if(buffer.Length>0)chunks.Add(buffer.ToString());buffer.Clear();}
        foreach(char ch in text)
        {
            if(ch is '(' or '（' or '[' or '【')depth++;
            if(ch is ')' or '）' or ']' or '】')depth=Math.Max(0,depth-1);
            if(depth==0&&ch is ',' or '，' or ';' or '；')Flush();else buffer.Append(ch);
        }
        Flush();var result=new List<string>();
        foreach(string chunk in chunks)
        {
            if(nameKeys.Contains(MatchName(chunk))){result.Add(chunk.Trim());continue;}
            string[] lines=SplitClusterMembers(chunk);
            for(int i=0;i<lines.Length;)
            {
                int end=i;string value=lines[i];
                for(int j=lines.Length-1;j>i;j--){string joined=string.Join('\n',lines[i..(j+1)]);if(nameKeys.Contains(MatchName(joined))){end=j;value=joined;break;}}
                if(value.Length>0)result.Add(value);i=end+1;
            }
        }
        return result.ToArray();
    }
    static string[] LegacyFragments(string name)=>name.Split([',','，',';','；','\r','\n'],StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);
    static string ClusterId(IEnumerable<string> members)=>Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(JsonSerializer.Serialize(members.Order(StringComparer.Ordinal).ToArray()))));
    static bool HasStation(string value)=>HasRoom(value)&&value.Trim() is not ("0" or "未提供" or "未识别" or "null" or "NULL" or "N/A");

    List<ClusterRecord> AnalyzeClusters(IEnumerable<ClusterRecord> persisted,IEnumerable<ClusterInput> raw,Graph graph)
    {
        var inputs=raw.ToArray();var sources=inputs.Select(x=>x.Source).ToHashSet(StringComparer.Ordinal);
        var all=persisted.Where(x=>Human(x.Source)||!sources.Contains(x.Source.Replace("云端：","")))
            .Concat(inputs.Select(x=>new ClusterRecord("",x.Name,x.Type,x.Members,x.Source,"已确认","",true,x.Station))).ToArray();
        FilteredClusters.Clear();if(all.Length==0)return [];
        var facilityNames=ClusterFacilityNames();var graphNames=graph.Nodes.Select(n=>n.Name).ToHashSet(StringComparer.Ordinal);
        var tableNames=all.Where(x=>!Human(x.Source)).Select(x=>x.Name).ToHashSet(StringComparer.Ordinal);
        var known=facilityNames.Concat(graphNames).Concat(tableNames).ToHashSet(StringComparer.Ordinal);
        var nameKeys=known.Select(MatchName).ToHashSet(StringComparer.Ordinal);
        var aliases=known.GroupBy(MatchName).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var facilityAliases=facilityNames.GroupBy(MatchName).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        var graphAliases=graphNames.GroupBy(MatchName).ToDictionary(g=>g.Key,g=>g.ToArray(),StringComparer.Ordinal);
        string Resolve(string n)
        {
            if(graphNames.Contains(n))return n;string key=MatchName(n);
            if(graphAliases.TryGetValue(key,out var nodes)&&nodes.Length==1)return nodes[0];
            if(facilityAliases.TryGetValue(key,out var facilities)&&facilities.Length==1)return facilities[0];
            if(known.Contains(n))return n;
            return aliases.TryGetValue(key,out var values)&&values.Length==1?values[0]:n;
        }
        // Recover old persisted comma fragments only against complete known
        // names. Never reorder fragments and concatenate unrelated devices.
        var recovery=new Dictionary<string,List<(string Name,string[] Parts)>>(StringComparer.Ordinal);
        foreach(string name in known.Where(n=>n.IndexOfAny([',','，',';','；'])>=0))
        {
            if(SplitClusterMembers(name).Length!=1)continue;var parts=LegacyFragments(name).Select(MatchName).Distinct().ToArray();if(parts.Length<2)continue;
            foreach(string part in parts){if(!recovery.TryGetValue(part,out var values))recovery[part]=values=[];values.Add((name,parts));}
        }
        string[] Members(ClusterRecord row)
        {
            var values=row.Members.SelectMany(n=>ParseClusterText(n,nameKeys)).ToList();
            if(!Human(row.Source))
            {
                var keys=values.Select(MatchName).ToHashSet(StringComparer.Ordinal);var repaired=new HashSet<string>(StringComparer.Ordinal);var consumed=new HashSet<string>(StringComparer.Ordinal);
                foreach(string key in keys)if(recovery.TryGetValue(key,out var candidates))foreach(var candidate in candidates)
                    if(candidate.Parts.All(keys.Contains)){repaired.Add(candidate.Name);consumed.UnionWith(candidate.Parts);}
                values=values.Where(v=>!consumed.Contains(MatchName(v))).Concat(repaired).ToList();
            }
            return values.Select(Resolve).Distinct(StringComparer.Ordinal).ToArray();
        }
        var station=new Dictionary<string,HashSet<string>>(StringComparer.Ordinal);var stationLabels=new Dictionary<string,string>(StringComparer.Ordinal);
        var types=graph.Nodes.ToDictionary(n=>n.Name,n=>n.Type,StringComparer.Ordinal);
        using(var c=Open(Personal))
        {
            using(var q=c.CreateCommand()){q.CommandText="SELECT name,type FROM types";using var r=q.ExecuteReader();while(r.Read())types[Resolve(r.GetString(0))]=r.GetString(1);}
            using(var q=c.CreateCommand())
            {
                q.CommandText="SELECT name,station FROM facility_stations";using var r=q.ExecuteReader();while(r.Read())
                {
                    string name=Resolve(r.GetString(0)),value=r.GetString(1);if(!HasStation(value))continue;string key=MatchName(value);stationLabels.TryAdd(key,value);
                    if(!station.TryGetValue(name,out var values))station[name]=values=[];values.Add(key);
                }
            }
        }
        foreach(var row in all.Where(x=>!Human(x.Source)))
        {
            string name=Resolve(row.Name);
            if(!types.TryGetValue(name,out var existing)||existing=="未识别")types[name]=row.Type;
        }
        var bySite=station.Where(x=>x.Value.Count==1).GroupBy(x=>x.Value.Single()).ToDictionary(g=>g.Key,g=>g.Select(x=>x.Key).ToArray());
        bool Cabinet(string name)=>types.TryGetValue(name,out var type)&&type.Split(';').Select(NormalizeType).Contains("光交箱");
        bool Splitter(string name)=>types.TryGetValue(name,out var type)&&type.Split(';').Select(NormalizeType).Contains("分纤箱");
        var candidates=new List<(ClusterRecord Row,HashSet<string> Members,HashSet<string> Sites,string Head,bool ListedCabinets)>();var manual=new List<ClusterRecord>();
        foreach(var row in all)
        {
            var members=Members(row).ToHashSet(StringComparer.Ordinal);string head=Resolve(row.Name);
            // A cabinet-only member list is an explicit relationship supplied by
            // the cluster table. Its station labels do not define membership.
            bool listedCabinets=!Human(row.Source)&&members.Count>=2&&members.All(Cabinet);
            var sites=members.Append(head).Where(station.ContainsKey).SelectMany(n=>station[n]).ToHashSet(StringComparer.Ordinal);
            if(HasStation(row.Station)){string key=MatchName(row.Station);sites.Add(key);stationLabels.TryAdd(key,row.Station);}
            if(Human(row.Source)){manual.Add(row with{Members=members.Order(StringComparer.Ordinal).ToArray(),Station=string.Join('；',sites.Select(s=>stationLabels[s]).Order(StringComparer.Ordinal))});continue;}
            if(sites.Count==1&&!listedCabinets)
            {
                if(bySite.TryGetValue(sites.Single(),out var same))members.UnionWith(same);
                // The explicit station on a device's source row also establishes
                // its membership, even when the other facility file is absent.
                if(HasStation(row.Station))members.Add(head);
            }
            candidates.Add((row,members,sites,head,listedCabinets));
        }
        // Merge reciprocal/subset rows and connected automatic relationships.
        // Deduplication unions the full members; it never deletes device names.
        int[] parent=Enumerable.Range(0,candidates.Count).ToArray();int Root(int i){while(parent[i]!=i){parent[i]=parent[parent[i]];i=parent[i];}return i;}
        void Join(int a,int b){a=Root(a);b=Root(b);if(a!=b)parent[b]=a;}
        var owner=new Dictionary<string,int>(StringComparer.Ordinal);var siteOwner=new Dictionary<string,int>(StringComparer.Ordinal);
        for(int i=0;i<candidates.Count;i++)
        {
            foreach(string member in candidates[i].Members){if(owner.TryGetValue(member,out int old))Join(i,old);else owner[member]=i;}
            if(!candidates[i].ListedCabinets&&candidates[i].Sites.Count==1){string site=candidates[i].Sites.Single();if(siteOwner.TryGetValue(site,out int old))Join(i,old);else siteOwner[site]=i;}
        }
        for(int i=0;i<candidates.Count;i++)if(owner.TryGetValue(candidates[i].Head,out int related))Join(i,related);
        var automatic=new List<ClusterRecord>();
        foreach(var component in Enumerable.Range(0,candidates.Count).GroupBy(Root))
        {
            var rows=component.Select(i=>candidates[i]).ToArray();var representative=rows.OrderByDescending(x=>x.Row.Type.Split(';').Select(NormalizeType).Contains("光交箱")).ThenBy(x=>x.Row.Name,StringComparer.Ordinal).First().Row;
            var members=rows.SelectMany(x=>x.Members).Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal).ToArray();var sites=rows.SelectMany(x=>x.Sites).Distinct().ToArray();var reasons=new List<string>();
            if(members.Length<2){foreach(var row in rows)FilteredClusters.Add((row.Row.Name,"按原始集群关系及所属站点跨类型补齐后仍只有自身或不足两个成员"));continue;}
            bool cabinetOnly=members.All(Cabinet)&&rows.All(x=>x.ListedCabinets);
            if(sites.Length>1&&!cabinetOnly)reasons.Add("成员所属站点冲突："+string.Join('；',sites.Select(s=>stationLabels[s]).Order(StringComparer.Ordinal)));
            if(rows.Any(x=>!members.Contains(x.Head,StringComparer.Ordinal)))reasons.Add("设备名称不在集群列表中");
            var tags=rows.SelectMany(x=>x.Row.Type.Split(';')).Concat(members.Where(types.ContainsKey).SelectMany(n=>types[n].Split(';'))).Select(NormalizeType).Where(t=>t.Length>0).Distinct().ToArray();
            if(tags.Length>1)tags=tags.Where(t=>t!="未识别").ToArray();
            automatic.Add(representative with{Id=ClusterId(members),Name=Resolve(representative.Name),Members=members,Type=string.Join(';',tags.Order(StringComparer.Ordinal)),Station=string.Join('；',sites.Select(s=>stationLabels[s]).Order(StringComparer.Ordinal)),Status=reasons.Count==0?"已确认":"需核对",Reason=string.Join('；',reasons),Enabled=reasons.Count==0});
        }
        string Key(ClusterRecord x)=>string.Join('\n',x.Members.Order(StringComparer.Ordinal));
        var manualKeys=manual.Select(Key).ToHashSet(StringComparer.Ordinal);
        var unique=manual.Concat(automatic.Where(x=>!manualKeys.Contains(Key(x)))).GroupBy(Key).Select(g=>g.First()).ToArray();
        var manualMembers=manual.SelectMany(x=>x.Members).ToHashSet(StringComparer.Ordinal);
        return unique.Select(row=>
        {
            var reasons=new List<string>();if(row.Status=="需核对"&&row.Reason.Length>0&&row.Reason!="成员未匹配当前资源")reasons.Add(row.Reason);
            var missing=row.Members.Where(n=>!known.Contains(n)&&!graphNames.Contains(n)).ToArray();if(missing.Length>0)reasons.Add("集群原表、设施表与中继段均未找到："+string.Join('；',missing));
            if(!Human(row.Source)&&row.Members.Any(manualMembers.Contains))reasons.Add("自动关系与人工维护集群重叠，保留人工关系优先");
            if(Human(row.Source)&&manual.Any(other=>other.Id!=row.Id&&other.Members.Intersect(row.Members).Any()))reasons.Add("人工集群成员归属重叠");
            bool bad=reasons.Count>0;var idle=row.Members.Where(n=>!graphNames.Contains(n)&&facilityNames.Contains(n)).ToArray();if(idle.Length>0)reasons.Add("设施表存在，暂无中继段："+string.Join('；',idle));
            var tableOnly=row.Members.Where(n=>!graphNames.Contains(n)&&!facilityNames.Contains(n)&&tableNames.Contains(n)).ToArray();if(tableOnly.Length>0)reasons.Add("集群原表存在，暂无设施表或中继段："+string.Join('；',tableOnly));
            bool mixedOneCabinet=row.Members.Count(Cabinet)==1&&row.Members.Any(Splitter)&&row.Members.All(n=>Cabinet(n)||Splitter(n))&&row.Station.Length>0&&!row.Station.Contains('；');
            int minimum=mixedOneCabinet?1:2;
            int routable=row.Members.Count(graphNames.Contains);if(routable<minimum)reasons.Add("当前可参与路由的成员不足"+(minimum==1?"一个":"两个")+"，集群保留且不纳入路由");
            return row with{Status=bad?"需核对":row.Status,Enabled=row.Enabled&&!bad&&routable>=minimum,Reason=string.Join('；',reasons)};
        }).OrderBy(x=>x.Name,StringComparer.Ordinal).ToList();
    }
}
