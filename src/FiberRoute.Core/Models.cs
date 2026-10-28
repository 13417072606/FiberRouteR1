namespace FiberRoute.Core;
public sealed record Node(int Id, string Name, string Type, bool Confirmed, string Source="", string Status="", string Reason="",string Region="")
{
    public string Confirmation => Status.Length>0?Status:Confirmed?"已确认":"待确认";
}
public sealed record Edge(int Index, string Id, string Name, int A, int B, double? Length, int Free, int Total, string Raw,string StartRegion="",string EndRegion="",string BusinessStatus="");
public sealed class Graph(Node[] nodes, Edge[] edges, string version)
{
    public Node[] Nodes { get; } = nodes;
    public Edge[] Edges { get; } = edges;
    public string Version { get; } = version;
    public int[][] Adjacency { get; } = Build(nodes.Length, edges);
    public int[] Logical {get; private set;}=Enumerable.Range(0,nodes.Length).ToArray();
    public int[][] LogicalAdjacency {get; private set;}=Build(nodes.Length,edges);
    public Dictionary<int,string> ClusterNames {get;}=[];
    public Dictionary<int,int[]> ClusterMembers {get;}=[];
    public Dictionary<string,string> GroupWarnings {get;}=[];
    public Dictionary<string,int[]> Rooms {get;}=new(StringComparer.Ordinal);
    public Dictionary<int,RoomAssignment> RoomAffiliations {get;}=[];
    public void SetClusters(IEnumerable<ClusterRecord> clusters)
    {
        var names=Nodes.ToDictionary(n=>n.Name,n=>n.Id,StringComparer.Ordinal);
        foreach(var c in clusters.Where(c=>c.Enabled&&c.Status=="已确认"))
        {
            var ids=c.Members.Select(n=>names.GetValueOrDefault(n,-1)).ToArray();
            if(ids.Length<2||ids.Any(i=>i<0)||ids.Any(i=>Logical[i]!=i))continue;
            int group=ids.Min();foreach(int id in ids)Logical[id]=group;
            ClusterNames[group]=c.Name;ClusterMembers[group]=ids;
        }
        var lists=Enumerable.Range(0,Nodes.Length).Select(_=>new HashSet<int>()).ToArray();
        foreach(var e in Edges){int a=Logical[e.A],b=Logical[e.B];if(a==b)continue;lists[a].Add(e.Index);lists[b].Add(e.Index);}
        LogicalAdjacency=lists.Select(x=>x.ToArray()).ToArray();
    }
    static int[][] Build(int count, Edge[] es)
    {
        var lists = Enumerable.Range(0,count).Select(_ => new List<int>()).ToArray();
        foreach(var e in es) { lists[e.A].Add(e.Index); if(e.A!=e.B) lists[e.B].Add(e.Index); }
        return lists.Select(x=>x.ToArray()).ToArray();
    }
}
public enum RouteOrder { Candidates, Distance, Jumps, Capacity }
public sealed record RouteOptions(int Start, int End, int Demand, int MaxJumps, int Count, RouteOrder Order,
    HashSet<string> AvoidTypes, HashSet<int> AvoidNodes, int MaxExpansions = 200000, int MaxQueue = 50000, int TimeoutMs = 10000, bool RequireCapacity = true);
public sealed record Route(int[] Nodes, int[] Edges, double Length, int Unknown, int Zero, int Bottleneck)
{
    public int Jumps => Math.Max(0,Edges.Length-1);
    public bool HasCapacity(int demand) => Edges.Length > 0 && Bottleneck >= demand;
    public string CapacityStatus(int demand) => Edges.Length == 0 ? "内部关系容量待核验" : HasCapacity(demand) ? $"满足 {demand} 芯需求" : $"芯数不足（缺 {demand-Bottleneck} 芯）";
    public RouteStep[] Steps {get;init;}=[];
    public ClusterTransfer[] Transfers {get;init;}=[];
}
public sealed record RouteResult(List<Route> Routes, bool Limited, bool Cancelled, int Expansions, long ElapsedMs);
public sealed record ImportProgress(long Read, long Accepted, long Invalid, int Nodes);
public sealed record ImportResult(long Read, long Accepted, long Invalid, int Nodes, string Version, string ErrorFile,bool Unchanged=false);
public sealed record TypeChange(string Name, string Before, string After, string Status);

public sealed record RouteStep(int Edge,int From,int To);
public sealed record ClusterTransfer(int From,int To,int BeforeSegment);
public sealed record ClusterRecord(string Id,string Name,string Type,string[] Members,string Source,string Status,string Reason,bool Enabled,string Station="");
