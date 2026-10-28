using System.Text;
using Microsoft.VisualBasic.FileIO;
namespace FiberRoute.Core;
public static class Csv
{
    public static Encoding EncodingFor(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return Encoding.GetEncoding(name == "UTF-8" ? "utf-8" : name,
            EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback);
    }
    public static string DetectEncoding(string path, CancellationToken ct = default)
    {
        using var f=File.OpenRead(path);var prefix=new byte[4096];int n=f.Read(prefix);
        if(n>=4 && ((prefix[0]==0xff&&prefix[1]==0xfe&&prefix[2]==0&&prefix[3]==0)||(prefix[0]==0&&prefix[1]==0&&prefix[2]==0xfe&&prefix[3]==0xff)))
            throw new InvalidDataException("暂不支持UTF-32，请使用UTF-8、UTF-16或Excel文件。");
        string? bom=n>=3&&prefix[0]==0xef&&prefix[1]==0xbb&&prefix[2]==0xbf?"UTF-8":n>=2&&prefix[0]==0xff&&prefix[1]==0xfe?"utf-16":n>=2&&prefix[0]==0xfe&&prefix[1]==0xff?"utf-16BE":null;
        if(bom!=null){Validate(path,bom,ct);return bom;}
        // BOM-less UTF-16 has NULs in ASCII column names, delimiters and digits.
        int even=0,odd=0;for(int i=0;i<n;i++)if(prefix[i]==0){if(i%2==0)even++;else odd++;}
        if(n>=8&&(even>n/12||odd>n/12))
        {string utf=odd>even?"utf-16":"utf-16BE";Validate(path,utf,ct);return utf;}
        try{Validate(path,"UTF-8",ct);return "UTF-8";}catch(DecoderFallbackException){}
        try
        {
            Validate(path,"GB18030",ct);
            var h=Read(path,"GB18030").FirstOrDefault()??[];
            string[] known=[..Store.Fields,"节点名称","节点类型","设施名称","设备名称","集群列表","占用数量","业务状态","始端机房","终端机房"];
            if(!h.Any(x=>known.Contains(x.Trim())))throw new InvalidDataException("编码存在歧义：GB18030可解码，但未识别中文表头。请选择编码后重新预览。");
            return "GB18030";
        }
        catch(DecoderFallbackException e){throw new InvalidDataException("文件既不是严格UTF-8，也不是有效GB18030文本。请确认文件格式，或手动选择编码。",e);}
    }
    static void Validate(string path,string encoding,CancellationToken ct)
    {using var r=new StreamReader(path,EncodingFor(encoding),false);var b=new char[32768];while(r.Read(b,0,b.Length)>0)ct.ThrowIfCancellationRequested();}
    public static IEnumerable<string[]> Read(string path,string encoding="自动")
    {
        using var rows=ReadStrict(path,encoding).GetEnumerator();
        while(true)
        {
            bool more;
            try{more=rows.MoveNext();}
            catch(DecoderFallbackException e){throw new InvalidDataException("CSV编码校验失败，未导入乱码。请返回编码选项，尝试自动识别或原文件编码后重新预览。"+e.Message,e);}
            catch(MalformedLineException e){throw new InvalidDataException($"CSV第{e.LineNumber}行引号或字段结构错误，请检查原始文件。",e);}
            if(!more)yield break;yield return rows.Current;
        }
    }
    static IEnumerable<string[]> ReadStrict(string path,string encoding)
    {
        if(encoding=="自动")encoding=DetectEncoding(path);
        using var r=new StreamReader(path,EncodingFor(encoding),false);
        if(r.Peek()==0xfeff)r.Read();
        using var parser=new TextFieldParser(r){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=false};
        parser.SetDelimiters(",");
        while(!parser.EndOfData)yield return parser.ReadFields()??[];
    }
    // The resource system exports these facility tables as fixed comma-separated fields,
    // with literal quotes in names rather than RFC CSV quoting. Never apply this to arbitrary CSV.
    public static bool IsRawFacility(string path,string encoding)
    {
        using var r=new StreamReader(path,EncodingFor(encoding),false);var h=(r.ReadLine()??"").TrimStart('\ufeff').Split(',');
        return h.Length>=9&&h.Take(9).SequenceEqual(new[]{"ID","设施名称","设施编码","所属末端网格","所属地市","所属区县","站点名称/位置点","机房名称","所属社区"});
    }
    public static IEnumerable<string[]> ReadRawFacility(string path,string encoding)
    {
        using var r=new StreamReader(path,EncodingFor(encoding),false);var h=(r.ReadLine()??"").TrimStart('\ufeff').Split(',');yield return h;int row=1;string? line;
        while((line=r.ReadLine())!=null){row++;if(line.Length==0)continue;var cells=line.Split(',');if(cells.Length!=h.Length||cells[0].Length<15||!cells[0].All(char.IsDigit))throw new InvalidDataException($"固定设施导出第{row}行字段错位，请检查原表；未自动猜测所属站点");yield return cells;}
    }
    public static readonly string[] RawResourceHeaders=["ID","名称","长度","空闲数量","占用数量","中继纤芯数量","占有率","业务状态","始端区域","终端区域","始端站点","终端站点","始端机房","终端机房","始端设施","终端设施","关联光缆段"];
    public static bool IsResourceExport(string path,string encoding)
    {using var r=new StreamReader(path,EncodingFor(encoding=="自动"?DetectEncoding(path):encoding),false);return (r.ReadLine()??"").TrimStart('\ufeff').Split(',').SequenceEqual(RawResourceHeaders);}
    public static IEnumerable<string[]> ReadResourceExport(string path,string encoding)
    {
        if(encoding=="自动")encoding=DetectEncoding(path);
        using var r=new StreamReader(path,EncodingFor(encoding),false);string header=(r.ReadLine()??"").TrimStart('\ufeff');if(!header.Split(',').SequenceEqual(RawResourceHeaders))throw new InvalidDataException("原始导出模式仅适用于固定17列中继段表");
        yield return RawResourceHeaders;
        string? line;while((line=r.ReadLine())!=null){if(line.Length==0)continue;yield return line.Split([','],17,StringSplitOptions.None);}
    }
    public static string Line(IEnumerable<object?> fields)=>string.Join(",",fields.Select(v=>"\""+(Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture)??"").Replace("\"","\"\"")+"\""));
    public static void Write(string path,IEnumerable<IEnumerable<object?>> rows)
    {using var w=new StreamWriter(path,false,new UTF8Encoding(true));foreach(var r in rows)w.WriteLine(Line(r));}
}
