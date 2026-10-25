namespace FiberRoute.Core;
public sealed record Node(int Id, string Name, string Type, bool Confirmed);
public sealed record Edge(int Index, string Id, string Name, int A, int B, double? Length, int Free, int Total, string Raw);
public sealed class Graph(Node[] nodes, Edge[] edges, string version)
{
    public Node[] Nodes { get; } = nodes;
    public Edge[] Edges { get; } = edges;
    public string Version { get; } = version;
    public int[][] Adjacency { get; } = Build(nodes.Length, edges);
    static int[][] Build(int count, Edge[] es)
    {
        var lists = Enumerable.Range(0,count).Select(_ => new List<int>()).ToArray();
        foreach(var e in es) { lists[e.A].Add(e.Index); if(e.A!=e.B) lists[e.B].Add(e.Index); }
        return lists.Select(x=>x.ToArray()).ToArray();
    }
}
public enum RouteOrder { Candidates, Distance, Jumps, Capacity }
public sealed record RouteOptions(int Start, int End, int Demand, int MaxJumps, int Count, RouteOrder Order,
    HashSet<string> AvoidTypes, HashSet<int> AvoidNodes, int MaxExpansions = 200000, int MaxQueue = 50000, int TimeoutMs = 10000);
public sealed record Route(int[] Nodes, int[] Edges, double Length, int Unknown, int Zero, int Bottleneck)
{ public int Jumps => Edges.Length-1; }
public sealed record RouteResult(List<Route> Routes, bool Limited, bool Cancelled, int Expansions, long ElapsedMs);
public sealed record ImportProgress(long Read, long Accepted, long Invalid, int Nodes);
public sealed record ImportResult(long Read, long Accepted, long Invalid, int Nodes, string Version, string ErrorFile);
public sealed record TypeChange(string Name, string Before, string After, string Status);
