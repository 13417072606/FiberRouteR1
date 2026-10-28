using FiberRoute.Core;
using System.Globalization;
using System.IO.Compression;
using System.Xml.Linq;

static class RouteWorkbookChecks
{
    static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static string Value(XElement sheet,string reference)=>sheet.Descendants(S+"c").Single(c=>(string?)c.Attribute("r")==reference).Value;
    public static void Run(Action<bool,string> check,string root)
    {
        System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);
        var graph=new Graph([new(0,"起点<&>\"/ODF01","机房",true),new(1,"起点/ODB02","机房",true),new(2,"中间GJ01","光交箱",true),new(3,"中间GJ01-1","光交箱",true),new(4,"终点/ODF01","机房",true),new(5,"终点机房","机房",true)],[new(0,"000123456789012345678901","",2,1,102,3,8,""),new(1,"2","",4,3,106.21,1,4,"")],"export-test");
        var options=new RouteOptions(0,5,2,8,8,RouteOrder.Distance,[],[],RequireCapacity:false);
        var route=new Route([0,2,4],[0,1],208.21,0,0,1){Steps=[new(0,1,2),new(1,3,4)],Transfers=[new(0,1,0),new(2,3,1),new(4,5,2)]};
        var plan=ExportPlan.FromRoute(graph,route,options,2);
        check(plan.Sequence.SequenceEqual(graph.Nodes.Skip(1).Take(4).Select(n=>n.Name)),"workbook sequence omits endpoint representatives and retains real endpoints and middle transfers");
        check(plan.Segments[0].From==graph.Nodes[1].Name&&plan.Segments[0].To==graph.Nodes[2].Name,"workbook follows oriented route steps rather than stored edge directions");
        string file=Path.Combine(root,"routes.xlsx");RouteWorkbook.Write(file,[plan]);
        using(var zip=ZipFile.OpenRead(file))
        {
            XElement Read(string name){using var stream=zip.GetEntry(name)!.Open();return XElement.Load(stream);}
            var names=Read("xl/workbook.xml").Descendants(S+"sheet").Select(x=>(string)x.Attribute("name")!).ToArray();
            check(names.SequenceEqual(new[]{"路由总览","方案02"}),"workbook contains only overview and selected numbered route sheets");
            var overview=Read("xl/worksheets/sheet1.xml");var detail=Read("xl/worksheets/sheet2.xml");
            check(Value(overview,"D5")=="跳纤路由"&&Value(overview,"D6")==string.Join(" → ",graph.Nodes.Skip(1).Take(4).Select(n=>n.Name)),"overview exports exact arrow-separated route including XML characters");
            check(Value(overview,"I6")=="-1"&&Value(overview,"J6").Contains("芯数不足"),"shortage routes export actual negative remainder and clear capacity status");
            check(overview.Descendants(S+"hyperlink").Single().Attribute("location")!.Value=="'方案02'!A1"&&detail.Descendants(S+"hyperlink").Single().Attribute("location")!.Value=="'路由总览'!A1","overview and detail hyperlinks preserve original route numbers");
            check(!detail.Value.Contains("不编造芯号")&&!detail.Value.Contains("资源追溯")&&!detail.Value.Contains("字段说明"),"deleted explanation and removed-sheet references are absent");
            check(detail.Descendants(S+"mergeCell").Any(m=>Value(detail,m.Attribute("ref")!.Value.Split(':')[0]).Contains("同机房/集群衔接")),"actual internal transfers remain separate from real segment capacities");
        }
        using(var stream=File.OpenRead(file))using(var reader=ExcelDataReader.ExcelReaderFactory.CreateOpenXmlReader(stream))
        {
            int sheets=0;bool readChinese=false;do{sheets++;while(reader.Read())for(int i=0;i<reader.FieldCount;i++)readChinese|=reader.GetValue(i)?.ToString()=="跳纤路由";}while(reader.NextResult());
            check(sheets==2&&readChinese,"independent Excel reader opens generated workbook and Chinese headings");
        }
        var internalPlan=new ExportPlan(1,"A","B",["A","B"],2,0,0,null,"内部关系容量待核验",[],[new(0,"A","B")]);
        var zeroPlan=plan with{Number=3,Segments=[plan.Segments[0] with{Length=0},plan.Segments[1] with{Length=null}]};
        RouteWorkbook.Write(file,[internalPlan,zeroPlan]);
        using(var zip=ZipFile.OpenRead(file))
        {
            using var first=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var overview=XElement.Load(first);
            using var last=zip.GetEntry("xl/worksheets/sheet3.xml")!.Open();var detail=XElement.Load(last);
            check(Value(overview,"H6")==""&&Value(overview,"J6")=="内部关系容量待核验","internal-only route never fabricates available capacity");
            check(detail.Descendants(S+"c").Any(c=>c.Value=="未知")&&detail.Descendants(S+"c").Any(c=>c.Element(S+"v")?.Value=="0"),"unknown length and real zero length remain distinct in Excel");
        }
        byte[] before=File.ReadAllBytes(file);bool failed=false;
        try{RouteWorkbook.Write(file,[plan with{Sequence=[new string('测',32768)]}]);}catch(InvalidOperationException){failed=true;}
        check(failed&&File.ReadAllBytes(file).SequenceEqual(before)&&!Directory.GetFiles(root,"*.tmp").Any(),"failed export preserves existing workbook and cleans temporary files");
        RouteWorkbook.Write(file,[plan with{Sequence=["=1+1","@formula"]}]);
        using(var zip=ZipFile.OpenRead(file)){using var xml=zip.GetEntry("xl/worksheets/sheet1.xml")!.Open();var overview=XElement.Load(xml);check(!overview.Descendants(S+"f").Any()&&Value(overview,"D6")=="=1+1 → @formula","facility text remains literal rather than an Excel formula");}
    }
    public static void Preview(string source,string output)
    {
        var rows=Csv.Read(source).ToArray();string[] fields=rows[0];var records=rows.Skip(1).Select(row=>fields.Select((f,i)=>(f,v:i<row.Length?row[i]:"")).ToDictionary(x=>x.f,x=>x.v)).ToArray();
        var plans=new List<ExportPlan>();
        foreach(var group in records.GroupBy(r=>r["方案"]))
        {
            var ordered=group.OrderBy(r=>int.Parse(r["段序"])).ToArray();var first=ordered[0];int demand=int.Parse(first["需求芯数"]);
            var segments=ordered.Select(r=>new ExportSegment(int.Parse(r["段序"]),r["始端"],r["终端"],double.TryParse(r["长度米"],CultureInfo.InvariantCulture,out var length)?length:null,int.Parse(r["空闲"]),int.Parse(r["总芯"]))).ToArray();
            var transfers=segments.Skip(1).Select((s,i)=>(s,i)).Where(x=>segments[x.i].To!=x.s.From).Select(x=>new ExportTransfer(x.i+1,segments[x.i].To,x.s.From,"内部衔接待核对")).ToArray();
            string start=segments[0].From,end=segments[^1].To;
            plans.Add(new(int.Parse(group.Key),start,end,ExportPlan.BuildSequence(start,end,segments,transfers),demand,Math.Round(double.Parse(first["总已记录米"],CultureInfo.InvariantCulture),2),int.Parse(first["跳纤"]),segments.Min(s=>s.Free),first["容量状态"],segments,transfers));
        }
        Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(output))!);RouteWorkbook.Write(output,plans);Console.WriteLine($"EXPORTED {plans.Count} routes / {plans.Sum(p=>p.Segments.Length)} segments: {output}");
    }
}
