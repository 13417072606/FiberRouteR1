using System.Diagnostics;
namespace FiberRoute.Core;
public static class Routing
{
    sealed record State(int Node, State? Prev, int Edge, int Depth, double Distance, int Unknown, int Zero, int Capacity);
    public static RouteResult Find(Graph g, RouteOptions o, CancellationToken ct = default)
    {
        if(o.Start<0 || o.Start>=g.Nodes.Length || o.End<0 || o.End>=g.Nodes.Length || o.Start==o.End) throw new ArgumentException("请选择不同的有效起终点");
        if(o.Demand<1 || o.MaxJumps<0 || o.MaxJumps>100 || o.Count<1 || o.Count>50) throw new ArgumentException("芯数须大于0，跳纤上限0–100，候选数1–50");
        if(o.AvoidNodes.Any(n=>g.Logical[n]==g.Logical[o.Start]||g.Logical[n]==g.Logical[o.End])) throw new ArgumentException("具体规避节点与起终点冲突");
        var watch=Stopwatch.StartNew(); var results=new List<Route>(); int expansions=0; bool limited=false;
        var queue=new PriorityQueue<State,(double,long)>(); long serial=0;
        double Score(State s)=>o.Order switch { RouteOrder.Distance=>s.Distance, RouteOrder.Capacity=>-(double)s.Capacity, _=>s.Depth };
        var root=new State(o.Start,null,-1,0,0,0,0,int.MaxValue); queue.Enqueue(root,(Score(root),serial++));
        while(queue.TryDequeue(out var s,out _))
        {
            if(ct.IsCancellationRequested) return new(results,true,true,expansions,watch.ElapsedMilliseconds);
            if(watch.ElapsedMilliseconds>=o.TimeoutMs || expansions>=o.MaxExpansions) {limited=true;break;}
            if(g.Logical[s.Node]==g.Logical[o.End])
            {
                var ns=new List<int>();var es=new List<int>(); for(State? p=s;p!=null;p=p.Prev){ns.Add(p.Node);if(p.Edge>=0)es.Add(p.Edge);} ns.Reverse();es.Reverse();
                var steps=new List<RouteStep>();var transfers=new List<ClusterTransfer>();int cursor=o.Start;
                for(int i=0;i<es.Count;i++) {var edge=g.Edges[es[i]];int from=g.Logical[edge.A]==g.Logical[cursor]?edge.A:edge.B;int to=from==edge.A?edge.B:edge.A;if(cursor!=from)transfers.Add(new(cursor,from,i));steps.Add(new(edge.Index,from,to));cursor=to;}
                if(cursor!=o.End)transfers.Add(new(cursor,o.End,es.Count));
                results.Add(new(ns.ToArray(),es.ToArray(),s.Distance,s.Unknown,s.Zero,es.Count==0?0:s.Capacity){Steps=steps.ToArray(),Transfers=transfers.ToArray()});
                if(results.Count>=o.Count)break; continue;
            }
            if(s.Depth>=o.MaxJumps+1)continue;
            expansions++;
            foreach(int ix in g.LogicalAdjacency[g.Logical[s.Node]])
            {
                if(ct.IsCancellationRequested) return new(results,true,true,expansions,watch.ElapsedMilliseconds);
                var e=g.Edges[ix]; if(e.Free<o.Demand)continue; int n=g.Logical[e.A]==g.Logical[s.Node]?e.B:e.A;
                bool seen=false;for(State? p=s;p!=null;p=p.Prev)if(g.Logical[p.Node]==g.Logical[n]){seen=true;break;} if(seen)continue;
                if(o.AvoidNodes.Any(x=>g.Logical[x]==g.Logical[n]))continue;
                if(g.Logical[n]!=g.Logical[o.End] && (g.ClusterMembers.GetValueOrDefault(g.Logical[n])??[n]).Any(id=>g.Nodes[id].Type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Any(o.AvoidTypes.Contains)))continue;
                if(queue.Count>=o.MaxQueue){limited=true;break;}
                var next=new State(n,s,ix,s.Depth+1,s.Distance+(e.Length??0),s.Unknown+(e.Length==null?1:0),s.Zero+(e.Length==0?1:0),Math.Min(s.Capacity,e.Free));
                queue.Enqueue(next,(Score(next),serial++));
            }
            // No states are silently discarded: queue exhaustion aborts with an explicit incomplete result.
            if(limited)break;
        }
        return new(results,limited,false,expansions,watch.ElapsedMilliseconds);
    }
}
