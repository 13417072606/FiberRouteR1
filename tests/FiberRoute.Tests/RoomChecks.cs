using FiberRoute.Core;
using Microsoft.Data.Sqlite;
using System.Text.Json;
static class RoomChecks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        var s=new Store(Path.Combine(root,"rooms"));string file=Path.Combine(root,"room-edges.csv");var mapping=Store.Fields.ToDictionary(x=>x,x=>x);
        Csv.Write(file,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端类型","终端类型","始端机房","终端机房"],
            ["a","甲","A/ODF01",10,1,2,"分纤箱","机房","","A机房"],["b","A/ODB02/GF01","乙",20,1,2,"光终端盒","分纤箱","A机房",""],
            ["c","A/GJ03","丙",5,1,2,"光交箱","分纤箱","A机房",""],["d","B/ODF01","丁",3,1,2,"机房","分纤箱","B机房",""],
            ["e","楼外GF01","戊",1,1,2,"分纤箱","分纤箱","A机房",""]});
        await s.ImportAsync(file,"自动",mapping,1,null,default);int Id(string name)=>s.Graph.Nodes.Single(n=>n.Name==name).Id;
        RouteResult Find(string a,string b)=>Routing.Find(s.Graph,new(Id(a),Id(b),1,4,8,RouteOrder.Distance,[],[]));
        check(Find("甲","乙").Routes.Single().Length==30&&Find("甲","乙").Routes.Single().Transfers.Length==1,"ODF and ODB with explicit room affiliation route as one room core");
        check(s.Graph.Edges.Length==5&&s.Graph.Nodes.Length==10&&s.Graph.ClusterNames.Values.Contains("A机房"),"room aggregation retains all real resources and names the room core");
        check(Find("甲","丙").Routes.Count==0&&s.RoomAssignments().Single(x=>x.Name=="A/GJ03") is {Status:"已确认",Enabled:false},"cabinet table match is confirmed but requires explicit grouping consent");
        check(!s.RoomAssignments().Any(x=>x.Name=="楼外GF01")&&Find("甲","戊").Routes.Count==0,"room field does not automatically contract non ODF ODB cabinet facilities");
        s.ConfirmRoom("A/GJ03","A机房",true);check(Find("甲","丙").Routes.Single().Length==15,"explicit cabinet room confirmation enables its real segments");
        var result=Routing.Find(s.Graph,new(Id("甲"),Id("乙"),2,4,8,RouteOrder.Distance,[],[]));check(result.Routes.Count==0,"room aggregation never bypasses real segment capacity");
        s.SaveClusters([new("cross","跨机房","机房",["A/ODF01","B/ODF01"],"人工修改","已确认","",true)]);check(Find("甲","丁").Routes.Count==0&&s.Graph.GroupWarnings.ContainsKey("cross"),"explicit cluster cannot silently bridge two different room cores");
        s.RemoveCluster("cross");string types=Path.Combine(root,"room-types.csv");Csv.Write(types,new object?[][]{["设施名称","机房名称","上联机房"],["A/ODB02/GF01","B机房","A机房"]});s.ApplyRoomFiles([(types,"",null)]);
        check(s.RoomAssignments().Single(x=>x.Name=="A/ODB02/GF01").Status=="需核对"&&Find("甲","乙").Routes.Count==0,"conflicting explicit room fields are quarantined and upstream room is not used");
        s.ConfirmRoom("A/ODB02/GF01","A机房",true);check(Find("甲","乙").Routes.Count==1,"human choice resolves a room affiliation conflict using a supplied room value");
        s=new Store(s.Root);check(Find("甲","乙").Routes.Count==1&&s.RoomAssignments().Single(x=>x.Name=="A/GJ03").Enabled,"reopen retains room choices and cabinet approval");
        s.ConfirmRoom("A/GJ03","A机房",false);check(Find("甲","丙").Routes.Count==0,"user can suspend cabinet aggregation without deleting real resources");
        Csv.Write(types,new object?[][]{["设施名称","机房名称","上联机房"],["A/ODB02/GF01","","A机房"]});s.ApplyRoomFiles([(types,"",null)]);check(s.RoomAssignments().Single(x=>x.Name=="A/ODB02/GF01").Room=="A机房","periodic room update replaces same source rows and does not accumulate obsolete affiliations");
        string baseline=Path.Combine(root,"room-baseline.json");File.WriteAllText(baseline,JsonSerializer.Serialize(new[]{new RoomSeed("B/ODF01","C机房","类型表：seed.csv")}));s.ApplyRoomBaseline(baseline);s.ApplyRoomBaseline(baseline);check(s.RoomAssignments().Count(x=>x.Name=="B/ODF01")==1&&s.Setting("room-baseline-hash")!=null,"compact room baseline is applied once and exposes conflicts rather than guessing");
        bool bad=false;try{s.ConfirmRoom("A/ODF01","不存在机房",true);}catch(InvalidDataException){bad=true;}check(bad,"human room choice cannot invent an unavailable affiliation");
        using(var c=new SqliteConnection($"Data Source={s.Database};Pooling=false")){c.Open();using var q=c.CreateCommand();q.CommandText="DROP TABLE rooms";q.ExecuteNonQuery();}s.Reload();check(Find("甲","乙").Routes.Count==1,"R1.3 database migration reconstructs explicit resource room fields");
        using var cancel=new CancellationTokenSource();cancel.Cancel();string before=JsonSerializer.Serialize(s.RoomAssignments());bool cancelled=false;try{s.ApplyRoomFiles([(types,"",null)],cancel.Token);}catch(OperationCanceledException){cancelled=true;}check(cancelled&&before==JsonSerializer.Serialize(s.RoomAssignments()),"cancelled affiliation refresh preserves previous room rows");
    }
}
