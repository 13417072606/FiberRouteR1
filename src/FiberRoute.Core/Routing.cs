using System.Diagnostics;
namespace FiberRoute.Core;
public static class Routing
{
    sealed record State(int Node, State? Prev, int Edge, int Depth, double Distance, int Unknown, int Zero, int Capacity);
    public static RouteResult Find(Graph g, RouteOptions o, CancellationToken ct = default)
    {
        if(o.Start<0 || o.Start>=g.Nodes.Length || o.End<0 || o.End>=g.Nodes.Length || o.Start==o.End) throw new ArgumentException("请选择不同的有效起终点");
        if(o.Demand<1 || o.MaxJumps<0 || o.MaxJumps>100 || o.Count<1 || o.Count>50) throw new ArgumentException("芯数须大于0，跳纤上限0–100，候选数1–50");
        if(o.AvoidNodes.Contains(o.Start)||o.AvoidNodes.Contains(o.End)) throw new ArgumentException("具体规避节点与起终点冲突");
        var watch=Stopwatch.StartNew(); var results=new List<Route>(); int expansions=0; bool limited=false;
        var queue=new PriorityQueue<State,(double,long)>(); long serial=0;
        double Score(State s)=>o.Order switch { RouteOrder.Distance=>s.Distance, RouteOrder.Capacity=>-(double)s.Capacity, _=>s.Depth };
        var root=new State(o.Start,null,-1,0,0,0,0,int.MaxValue); queue.Enqueue(root,(Score(root),serial++));
        while(queue.TryDequeue(out var s,out _))
        {
            if(ct.IsCancellationRequested) return new(results,true,true,expansions,watch.ElapsedMilliseconds);
            if(watch.ElapsedMilliseconds>=o.TimeoutMs || expansions>=o.MaxExpansions) {limited=true;break;}
            if(s.Node==o.End)
            {
                var ns=new List<int>();var es=new List<int>(); for(State? p=s;p!=null;p=p.Prev){ns.Add(p.Node);if(p.Edge>=0)es.Add(p.Edge);} ns.Reverse();es.Reverse();
                results.Add(new(ns.ToArray(),es.ToArray(),s.Distance,s.Unknown,s.Zero,s.Capacity));
                if(results.Count>=o.Count)break; continue;
            }
            if(s.Depth>=o.MaxJumps+1)continue;
            expansions++;
            foreach(int ix in g.Adjacency[s.Node])
            {
                if(ct.IsCancellationRequested) return new(results,true,true,expansions,watch.ElapsedMilliseconds);
                var e=g.Edges[ix]; if(e.Free<o.Demand)continue; int n=e.A==s.Node?e.B:e.A;
                bool seen=false;for(State? p=s;p!=null;p=p.Prev)if(p.Node==n){seen=true;break;} if(seen)continue;
                if(o.AvoidNodes.Contains(n))continue;
                if(n!=o.End && g.Nodes[n].Type.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Any(o.AvoidTypes.Contains))continue;
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
