using FiberRoute.Core;
using Microsoft.Data.Sqlite;

string root=Path.Combine(Path.GetTempPath(),"fiber-r148-contract-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
try
{
    string Write(string name,string value){string path=Path.Combine(root,name);File.WriteAllText(path,value);return path;}
    void Check(bool ok,string label){if(!ok)throw new Exception("FAIL "+label);Console.WriteLine("PASS "+label);}
    var store=new Store(Path.Combine(root,"server"));
    string relay=Write("relay.csv","ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量\ne1,A,X,1,1,2\ne2,B,Y,1,1,2\ne3,C,Z,1,1,2\ne4,D,W,1,1,2\n");
    await store.ImportAsync(relay,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default);
    string odf=Write("ODF.csv","ID,设施名称,机房名称\n1,A,甲机房\n2,B,甲机房\n3,C,乙机房\n");
    string cabinet=Write("光交接箱.csv","ID,设施名称,机房名称\n1,A,乙机房\n4,D,甲机房\n");
    string splitter=Write("光分纤箱.csv","ID,设施名称,机房名称\n2,B,乙机房\n");
    var sources=new[]{(odf,"ODF",(string?)null),(cabinet,"光交接箱",(string?)null),(splitter,"光分纤箱",(string?)null)};
    Check(store.PreviewRoomFiles(sources).Any(x=>x.Name=="A"&&x.Status=="需核对"),"同级 ID 冲突在导入前提示");
    store.ApplyRoomFiles([(odf,"ODF",null)]);store.ConfirmRoom("A","甲机房",true);
    store.ApplyRoomFiles(sources);var rooms=store.RoomAssignments();
    Check(rooms.Single(x=>x.Name=="A") is {Status:"需核对",Enabled:false},"新增同级 ID 冲突使旧人工确认重新进入复核");
    store.ConfirmRoom("A","甲机房",true);
    Check(store.RoomAssignments().Single(x=>x.Name=="A") is {Status:"已确认",Enabled:true},"对当前同级冲突人工确认后允许尾纤跳通");
    Check(rooms.Single(x=>x.Name=="B") is {Room:"甲机房",Status:"已确认"},"较高优先级 ID 认领机房");
    Check(store.Graph.Edges.Length==4,"机房更新不修改中继段资源");
    string cluster1=Write("clusters1.csv","设备名称,设备类型,集群列表\nA,光交接箱,A;B\nB,光交接箱,A;B\n");
    string cluster2=Write("clusters2.csv","设备名称,设备类型,集群列表\nC,光交接箱,C;D\nD,光交接箱,C;D\n");
    store.SaveClusterImport(cluster1);
    store.SaveClusters([new("manual","人工关系","光交箱",["X","Y"],"人工修改","已确认","",true)]);
    string package=Path.Combine(root,"data.zip");store.BuildDataPackage(package,"r148-cluster-old");
    var client=new Store(Path.Combine(root,"client"));
    client.SaveClusters([new("client-manual","客户人工关系","光交箱",["X","Y"],"人工修改","已确认","",true)]);
    using(var staged=client.PrepareDataPackage(package))client.ApplyDataPackage(staged);
    Check(client.Clusters().Any(x=>x.Members.Contains("A")),"在线首次同步导入旧表集群");
    store.SaveClusterImport(cluster2,replace:false);
    Check(!store.Clusters().Any(x=>x.Members.Contains("A"))&&store.Clusters().Any(x=>x.Members.Contains("C"))&&store.Clusters().Any(x=>x.Id=="manual"),"集群导入完整替换旧表关系并保留人工关系");
    store.BuildDataPackage(package,"r148-contract");
    using(var staged=client.PrepareDataPackage(package))client.ApplyDataPackage(staged);
    Check(client.RoomAssignments().Single(x=>x.Name=="A").Status=="需核对"&&!client.Clusters().Any(x=>x.Members.Contains("A"))&&client.Clusters().Any(x=>x.Members.Contains("C"))&&client.Clusters().Any(x=>x.Id=="client-manual"),"在线数据包同步 ID 优先级并全量替换旧表集群，保留客户人工关系");
    using(var repeated=client.PrepareDataPackage(package))Check(!repeated.HasChanges,"相同在线资源和集群关系重复更新时跳过写入");
    string renamed=Write("新文件名.csv","ID,设施名称,机房名称\n2,B,丙机房\n");
    store.ApplyRoomFiles([(renamed,"ODF",null)]);
    Check(store.RoomAssignments().Single(x=>x.Name=="B").Room=="丙机房"&&!store.RoomAssignments().Any(x=>x.Name=="C"),"文件改名时按类别和 ID 更新，并清除新表缺少的旧 ID");
    File.WriteAllText(odf,"ID,设施名称,机房名称\n2,B,丁机房\n");store.ApplyRoomFiles([(odf,"ODF",null)]);
    Check(store.RoomAssignments().Single(x=>x.Name=="B").Room=="丁机房"&&!store.RoomAssignments().Any(x=>x.Name=="C"),"同名文件更新也清除新表缺少的旧 ID");
    string CurrentSource(){using var c=new SqliteConnection($"Data Source={store.Personal};Pooling=false");c.Open();using var q=c.CreateCommand();q.CommandText="SELECT source FROM room_facilities WHERE kind='ODF' AND id='2'";return (string)q.ExecuteScalar()!;}
    string beforeSource=CurrentSource(),same=Write("内容相同但改名.csv","ID,设施名称,机房名称\n2,B,丁机房\n");store.ApplyRoomFiles([(same,"ODF",null)]);
    Check(CurrentSource()==beforeSource,"同 ID、同设施和机房的重复记录跳过写入");
    var partial=new Store(Path.Combine(root,"partial-server"));File.Copy(store.Database,partial.Database,true);partial.Reload();partial.ApplyRoomFiles([(renamed,"ODF",null)]);
    string partialPackage=Path.Combine(root,"partial-data.zip");partial.BuildDataPackage(partialPackage,"r148-partial");using(var staged=client.PrepareDataPackage(partialPackage))client.ApplyDataPackage(staged);
    Check(client.RoomAssignments().Count==1&&client.RoomAssignments().Single(x=>x.Name=="B").Room=="丙机房","在线机房完整五类别同步删除新包未列出的旧 ID");
    string baseline=Path.Combine(root,"room-baseline.db");File.Copy(store.Personal,baseline);
    File.WriteAllText(odf,"ID,设施名称,机房名称\n");store.ApplyRoomFiles([(odf,"ODF",null)]);
    Check(store.RoomAssignments().Single(x=>x.Name=="B").Room=="乙机房"&&store.RoomAssignments().Any(x=>x.Name=="D"),"空 ODF 表清除旧 ID，低优先级类别可重新认领");
    store.ApplyRoomFacilityBaseline(baseline);
    Check(store.RoomAssignments().Single(x=>x.Name=="B").Room=="乙机房","首次升级基线不会恢复已删除的旧 ID");
    File.WriteAllText(relay,"ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量\ne1,A,V,1,1,2\ne2,B,Y,1,1,2\ne4,D,W,1,1,2\n");
    var relayResult=await store.ImportAsync(relay,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default);
    Check(relayResult.Changed==1&&relayResult.Removed==1&&store.Graph.Edges.Length==3&&store.Graph.Nodes.Any(x=>x.Name=="V")&&!store.Graph.Nodes.Any(x=>x.Name=="X"),"手动中继段按 ID 更新端点并删除缺失旧 ID");
    store.BuildDataPackage(package,"r148-relay-id");using(var staged=client.PrepareDataPackage(package)){Check(staged.Changed==1&&staged.Added==0&&staged.Removed==1,"在线中继段按 ID 识别修改与删除");client.ApplyDataPackage(staged);}
    Check(client.Graph.Edges.Length==3&&client.Graph.Nodes.Any(x=>x.Name=="V")&&!client.Graph.Nodes.Any(x=>x.Name=="X"),"在线完整中继段数据与手动导入得到同一结果");
    File.WriteAllText(relay,"ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量\ne4,D,W,1,1,2\ne2,B,Y,1,1,2\ne1,A,V,1,1,2\n");
    Check((await store.ImportAsync(relay,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default)).Unchanged,"中继段仅调整行顺序时按 ID 跳过重复内容");
    File.WriteAllText(relay,"ID,始端设施,终端设施,长度,空闲数量,中继纤芯数量\ne1,A,V,1,1,2\ne1,B,Y,1,1,2\n");
    bool duplicateRejected=false;try{await store.ImportAsync(relay,"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default);}catch(InvalidDataException){duplicateRejected=true;}
    Check(duplicateRejected&&store.Graph.Edges.Length==3,"同表重复中继段 ID 拒绝导入并保留原资源");
}
finally{try{Directory.Delete(root,true);}catch(IOException){}}
