using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Xml;
using System.Xml.Linq;

namespace FiberRoute.Core;

public sealed record ExportSegment(int Number,string From,string To,double? Length,int Free,int Total);
public sealed record ExportTransfer(int BeforeSegment,string From,string To,string Kind="同机房/集群衔接");
public sealed record ExportPlan(int Number,string Start,string End,string[] Sequence,int Demand,double Length,int Jumps,int? Minimum,string Capacity,ExportSegment[] Segments,ExportTransfer[] Transfers)
{
    public static ExportPlan FromRoute(Graph graph,Route route,RouteOptions options,int number)
    {
        var segments=route.Edges.Select((ix,i)=>{var edge=graph.Edges[ix];var step=route.Steps.ElementAtOrDefault(i);return new ExportSegment(i+1,graph.Nodes[step?.From??route.Nodes[i]].Name,graph.Nodes[step?.To??route.Nodes[i+1]].Name,edge.Length,edge.Free,edge.Total);}).ToArray();
        var transfers=route.Transfers.Select(t=>new ExportTransfer(t.BeforeSegment,graph.Nodes[t.From].Name,graph.Nodes[t.To].Name)).ToArray();
        string start=graph.Nodes[options.Start].Name,end=graph.Nodes[options.End].Name;
        return new(number,start,end,BuildSequence(start,end,segments,transfers),options.Demand,route.Length,route.Jumps,route.Edges.Length>0?route.Bottleneck:null,route.CapacityStatus(options.Demand),segments,transfers);
    }
    public static string[] BuildSequence(string start,string end,ExportSegment[] segments,ExportTransfer[] transfers)
    {
        var sequence=new List<string>();void Add(string name){if(sequence.Count==0||sequence[^1]!=name)sequence.Add(name);}
        Add(start);
        for(int i=0;i<=segments.Length;i++)
        {
            foreach(var transfer in transfers.Where(t=>t.BeforeSegment==i)){Add(transfer.From);Add(transfer.To);}
            if(i<segments.Length){Add(segments[i].From);Add(segments[i].To);}
        }
        Add(end);return sequence.ToArray();
    }
}

// A small native Open XML writer keeps Excel export independent of Office/Internet
// installations and adds no runtime package dependencies to existing portable builds.
public static class RouteWorkbook
{
    static readonly XNamespace S="http://schemas.openxmlformats.org/spreadsheetml/2006/main";
    static readonly XNamespace R="http://schemas.openxmlformats.org/officeDocument/2006/relationships";
    static readonly XNamespace P="http://schemas.openxmlformats.org/package/2006/relationships";
    static readonly XNamespace C="http://schemas.openxmlformats.org/package/2006/content-types";
    static string Name(int number)=>$"方案{number:00}";
    public static void Write(string path,IReadOnlyList<ExportPlan> plans)
    {
        if(plans.Count==0)throw new InvalidOperationException("无方案可导出");
        if(plans.Select(p=>p.Number).Distinct().Count()!=plans.Count||plans.Any(p=>p.Number<1))throw new ArgumentException("方案编号必须唯一且大于0");
        string destination=Path.GetFullPath(path),temporary=destination+"."+Guid.NewGuid().ToString("N")+".tmp";
        try
        {
            using(var archive=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                var sheets=new[]{Overview(plans)}.Concat(plans.Select(Detail)).ToArray();
                string[] names=["路由总览",..plans.Select(p=>Name(p.Number))];
                Add(archive,"[Content_Types].xml",new XElement(C+"Types",new XElement(C+"Default",new XAttribute("Extension","rels"),new XAttribute("ContentType","application/vnd.openxmlformats-package.relationships+xml")),new XElement(C+"Default",new XAttribute("Extension","xml"),new XAttribute("ContentType","application/xml")),new XElement(C+"Override",new XAttribute("PartName","/xl/workbook.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.sheet.main+xml")),new XElement(C+"Override",new XAttribute("PartName","/xl/styles.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.styles+xml")),names.Select((_,i)=>new XElement(C+"Override",new XAttribute("PartName",$"/xl/worksheets/sheet{i+1}.xml"),new XAttribute("ContentType","application/vnd.openxmlformats-officedocument.spreadsheetml.worksheet+xml")))));
                Add(archive,"_rels/.rels",new XElement(P+"Relationships",Relationship("rId1","officeDocument","xl/workbook.xml")));
                Add(archive,"xl/_rels/workbook.xml.rels",new XElement(P+"Relationships",names.Select((_,i)=>Relationship($"rId{i+1}","worksheet",$"worksheets/sheet{i+1}.xml")),Relationship($"rId{names.Length+1}","styles","styles.xml")));
                Add(archive,"xl/workbook.xml",new XElement(S+"workbook",new XAttribute(XNamespace.Xmlns+"r",R),new XElement(S+"bookViews",new XElement(S+"workbookView",new XAttribute("activeTab",0))),new XElement(S+"sheets",names.Select((name,i)=>new XElement(S+"sheet",new XAttribute("name",name),new XAttribute("sheetId",i+1),new XAttribute(R+"id",$"rId{i+1}")))),new XElement(S+"definedNames",sheets.SelectMany((sheet,i)=>new[]{new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Area"),new XAttribute("localSheetId",i),$"'{names[i]}'!$A$1:${Column(sheet.Widths.Length-1)}${sheet.LastRow}"),new XElement(S+"definedName",new XAttribute("name","_xlnm.Print_Titles"),new XAttribute("localSheetId",i),$"'{names[i]}'!${sheet.FrozenRows}:${sheet.FrozenRows}")}))));
                Add(archive,"xl/styles.xml",Styles());
                for(int i=0;i<sheets.Length;i++)Add(archive,$"xl/worksheets/sheet{i+1}.xml",sheets[i].Xml());
            }
            File.Move(temporary,destination,true);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    static XElement Relationship(string id,string type,string target)=>new(P+"Relationship",new XAttribute("Id",id),new XAttribute("Type",$"{R}/{type}"),new XAttribute("Target",target));
    static void Add(ZipArchive archive,string name,XElement xml)
    {
        using var stream=archive.CreateEntry(name,CompressionLevel.Optimal).Open();using var writer=XmlWriter.Create(stream,new XmlWriterSettings{Encoding=new UTF8Encoding(false),Indent=false});new XDocument(new XDeclaration("1.0","utf-8","yes"),xml).Save(writer);
    }
    static string Column(int col){string result="";for(int n=col+1;n>0;n=(n-1)/26)result=(char)('A'+(n-1)%26)+result;return result;}
    static string Text(string value)=>new(value.Where(c=>c is '\t' or '\r' or '\n'||c>=32&&c!='\ufffe'&&c!='\uffff').ToArray());
    static double Height(string text,double width,double minimum=28)
    {
        int lines=text.Split('\n').Sum(line=>Math.Max(1,(int)Math.Ceiling(line.Sum(c=>c>255?2:1)/Math.Max(1,width-2))));return Math.Clamp(16*lines+10,minimum,409);
    }
    sealed class Sheet(double[] widths,int frozenRows)
    {
        public double[] Widths=widths;public int FrozenRows=frozenRows;public int LastRow;
        readonly SortedDictionary<int,XElement> rows=[];readonly List<XElement> merges=[],links=[];public string? Filter;
        public void Row(int row,double height){LastRow=Math.Max(row,LastRow);if(!rows.TryGetValue(row,out var element))rows[row]=element=new XElement(S+"row",new XAttribute("r",row));element.SetAttributeValue("ht",height.ToString(CultureInfo.InvariantCulture));element.SetAttributeValue("customHeight",1);}
        public void Cell(int row,int col,object? value,int style=0)
        {
            if(!rows.ContainsKey(row))Row(row,28);var cell=new XElement(S+"c",new XAttribute("r",Column(col)+row),new XAttribute("s",style));
            if(value is int or double or long)cell.Add(new XElement(S+"v",Convert.ToString(value,CultureInfo.InvariantCulture)));
            else{string text=Text(value?.ToString()??"");if(text.Length>32767)throw new InvalidOperationException("路由文字超过 Excel 单元格上限，请减少跳纤数量后导出。");cell.SetAttributeValue("t","inlineStr");cell.Add(new XElement(S+"is",new XElement(S+"t",new XAttribute(XNamespace.Xml+"space","preserve"),text)));}
            rows[row].Add(cell);
        }
        public void Merge(int row,int first,int last,string text,int style,double height){Row(row,height);Cell(row,first,text,style);merges.Add(new XElement(S+"mergeCell",new XAttribute("ref",$"{Column(first)}{row}:{Column(last)}{row}")));}
        public void Link(int row,int col,string sheet,string label){Cell(row,col,label,7);links.Add(new XElement(S+"hyperlink",new XAttribute("ref",Column(col)+row),new XAttribute("location",$"'{sheet}'!A1"),new XAttribute("display",label)));}
        public XElement Xml()=>new(S+"worksheet",new XElement(S+"dimension",new XAttribute("ref",$"A1:{Column(Widths.Length-1)}{LastRow}")),new XElement(S+"sheetViews",new XElement(S+"sheetView",new XAttribute("workbookViewId",0),new XAttribute("showGridLines",0),new XAttribute("zoomScale",85),new XElement(S+"pane",new XAttribute("xSplit",1),new XAttribute("ySplit",FrozenRows),new XAttribute("topLeftCell",$"B{FrozenRows+1}"),new XAttribute("activePane","bottomRight"),new XAttribute("state","frozen")),new XElement(S+"selection",new XAttribute("pane","bottomRight"),new XAttribute("activeCell",$"B{FrozenRows+1}"),new XAttribute("sqref",$"B{FrozenRows+1}")))),new XElement(S+"sheetFormatPr",new XAttribute("defaultRowHeight",26)),new XElement(S+"cols",Widths.Select((width,i)=>new XElement(S+"col",new XAttribute("min",i+1),new XAttribute("max",i+1),new XAttribute("width",width),new XAttribute("customWidth",1)))),new XElement(S+"sheetData",rows.Values),Filter==null?null:new XElement(S+"autoFilter",new XAttribute("ref",Filter)),merges.Count==0?null:new XElement(S+"mergeCells",new XAttribute("count",merges.Count),merges),links.Count==0?null:new XElement(S+"hyperlinks",links),new XElement(S+"printOptions",new XAttribute("horizontalCentered",1)),new XElement(S+"pageMargins",new XAttribute("left",.3),new XAttribute("right",.3),new XAttribute("top",.45),new XAttribute("bottom",.45),new XAttribute("header",.2),new XAttribute("footer",.2)),new XElement(S+"pageSetup",new XAttribute("paperSize",9),new XAttribute("orientation","landscape"),new XAttribute("fitToWidth",1),new XAttribute("fitToHeight",0)),new XElement(S+"headerFooter",new XElement(S+"oddFooter","&L路由方案&R第 &P / &N 页")));
    }
    static Sheet Overview(IReadOnlyList<ExportPlan> plans)
    {
        var sheet=new Sheet([12,36,36,68,12,16,12,18,18,20,12,14],5);
        sheet.Merge(1,0,11,"路由方案导出",1,42);
        sheet.Merge(2,0,11,$"共 {plans.Count} 条路由、{plans.Sum(p=>p.Segments.Length)} 段中继段。每条路由单独一页；点击“查看详情”可跳转。",2,28);
        sheet.Merge(3,0,11,"最小空闲芯数＝该路由各中继段空闲芯数的最小值；不累加。数值为资源快照，不代表现场已核验可接续。",2,28);
        string[] headers=["路由编号","起点设施","终点设施","跳纤路由","需求芯数","总长度（米）","跳纤（次）","最小空闲芯数","需求后最小余量","容量状态","中继段数","逐段明细"];
        sheet.Row(5,38);for(int col=0;col<headers.Length;col++)sheet.Cell(5,col,headers[col],3);
        for(int i=0;i<plans.Count;i++)
        {
            var p=plans[i];string sequence=string.Join(" → ",p.Sequence);int row=i+6;
            sheet.Row(row,Math.Max(Height(sequence,68,74),Math.Max(Height(p.Start,36),Height(p.End,36))));
            object?[] values=[Name(p.Number),p.Start,p.End,sequence,p.Demand,p.Length,p.Jumps,p.Minimum,p.Minimum is int n?n-p.Demand:null,p.Capacity,p.Segments.Length];
            for(int col=0;col<values.Length;col++)sheet.Cell(row,col,values[col],values[col] is double?9:values[col] is int?4:i%2==0?0:5);
            sheet.Link(row,11,Name(p.Number),"查看详情");
        }
        sheet.Filter=$"A5:K{plans.Count+5}";return sheet;
    }
    static Sheet Detail(ExportPlan p)
    {
        var sheet=new Sheet([8,42,42,16,12,12,12,16,22],11);sheet.Merge(1,0,7,$"{Name(p.Number)} · 路由方案明细",1,42);sheet.Link(1,8,"路由总览","返回总览");
        string minimum=p.Minimum is int min?$"最小空闲 {min} 芯    |    需求后最小余量 {min-p.Demand} 芯":"内部关系容量待核验";
        sheet.Merge(3,0,8,$"{p.Capacity}    |    需求 {p.Demand} 芯    |    总长 {p.Length:0.##} 米    |    跳纤 {p.Jumps} 次    |    {minimum}",6,36);
        sheet.Merge(5,0,8,"起点："+p.Start,0,Height(p.Start,160));sheet.Merge(6,0,8,"终点："+p.End,0,Height(p.End,160));
        string sequence="跳纤路由：\n"+string.Join(" → ",p.Sequence);sheet.Merge(8,0,8,sequence,2,Height(sequence,160,64));
        string[] headers=["段序","始端设施","终端设施","长度（米）","空闲芯数","总芯数","需求芯数","需求后余量","芯数校核"];
        sheet.Row(11,36);for(int col=0;col<headers.Length;col++)sheet.Cell(11,col,headers[col],3);
        int row=12;
        for(int i=0;i<=p.Segments.Length;i++)
        {
            foreach(var transfer in p.Transfers.Where(t=>t.BeforeSegment==i)){string text=transfer.Kind+"："+transfer.From+" → "+transfer.To;sheet.Merge(row++,0,8,text,8,Height(text,160,40));}
            if(i==p.Segments.Length)continue;var segment=p.Segments[i];sheet.Row(row,Math.Max(64,Math.Max(Height(segment.From,42),Height(segment.To,42))));
            object?[] values=[segment.Number,segment.From,segment.To,segment.Length is double d?d:"未知",segment.Free,segment.Total,p.Demand,segment.Free-p.Demand,segment.Free>=p.Demand?"满足需求":"芯数不足"];
            for(int col=0;col<values.Length;col++)sheet.Cell(row,col,values[col],col==8?segment.Free>=p.Demand?6:8:values[col] is double?9:values[col] is int?4:i%2==0?0:5);row++;
        }
        sheet.Merge(row+1,0,8,$"本路由合计：{p.Segments.Length} 段 · {p.Length:0.##} 米"+(p.Minimum is int m?$"；最小空闲 {m} 芯，需求后最小余量 {m-p.Demand} 芯。":"；内部关系容量待核验。"),6,36);
        return sheet;
    }
    static XElement Styles()
    {
        XElement Font(bool bold,string color,int size=11,bool underline=false)=>new(S+"font",new XElement(S+"sz",new XAttribute("val",size)),new XElement(S+"color",new XAttribute("rgb","FF"+color)),new XElement(S+"name",new XAttribute("val","微软雅黑")),bold?new XElement(S+"b"):null,underline?new XElement(S+"u"):null);
        XElement Fill(string pattern,string? color=null)=>new(S+"fill",new XElement(S+"patternFill",new XAttribute("patternType",pattern),color==null?null:new XElement(S+"fgColor",new XAttribute("rgb","FF"+color)),color==null?null:new XElement(S+"bgColor",new XAttribute("indexed",64))));
        XElement Xf(int font=0,int fill=0,bool center=false,int number=0)=>new(S+"xf",new XAttribute("numFmtId",number),new XAttribute("fontId",font),new XAttribute("fillId",fill),new XAttribute("borderId",0),new XAttribute("xfId",0),new XAttribute("applyAlignment",1),new XAttribute("applyNumberFormat",1),new XElement(S+"alignment",new XAttribute("vertical","center"),new XAttribute("horizontal",center?"center":"left"),new XAttribute("wrapText",1)));
        return new(S+"styleSheet",new XElement(S+"numFmts",new XAttribute("count",1),new XElement(S+"numFmt",new XAttribute("numFmtId",164),new XAttribute("formatCode","#,##0.00"))),new XElement(S+"fonts",new XAttribute("count",5),Font(false,"142B46"),Font(true,"008D98",22),Font(false,"65778E"),Font(true,"142B46"),Font(false,"008D98",11,true)),new XElement(S+"fills",new XAttribute("count",7),Fill("none"),Fill("gray125"),Fill("solid","EEF3F9"),Fill("solid","F5FAFC"),Fill("solid","E3F5F5"),Fill("solid","FFF3DD"),Fill("solid","FFFFFF")),new XElement(S+"borders",new XAttribute("count",1),new XElement(S+"border",new XElement(S+"left"),new XElement(S+"right"),new XElement(S+"top"),new XElement(S+"bottom"),new XElement(S+"diagonal"))),new XElement(S+"cellStyleXfs",new XAttribute("count",1),Xf()),new XElement(S+"cellXfs",new XAttribute("count",10),Xf(),Xf(1),Xf(2),Xf(3,2),Xf(center:true),Xf(fill:3),Xf(3,4),Xf(4,center:true),Xf(fill:5),Xf(center:true,number:164)),new XElement(S+"cellStyles",new XAttribute("count",1),new XElement(S+"cellStyle",new XAttribute("name","Normal"),new XAttribute("xfId",0),new XAttribute("builtinId",0))));
    }
}
