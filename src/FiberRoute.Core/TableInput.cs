using ExcelDataReader;
using System.Globalization;
using System.Text;
namespace FiberRoute.Core;
public sealed record InputInfo(string Format,string Encoding,string[] Sheets);
public static class TableInput
{
    public const string Filter="支持的数据文件|*.csv;*.xls;*.xlsx|CSV文本|*.csv|Excel工作簿|*.xls;*.xlsx";
    static string Format(string path)
    {
        using var s=File.OpenRead(path);byte[] b=new byte[8];int n=s.Read(b);
        bool ole=n==8&&b.SequenceEqual(new byte[]{0xd0,0xcf,0x11,0xe0,0xa1,0xb1,0x1a,0xe1});
        bool zip=n>=4&&b[0]==0x50&&b[1]==0x4b&&b[2]==3&&b[3]==4;
        string ext=Path.GetExtension(path).ToLowerInvariant();
        if(ole||zip)
        {if(ext==".csv")throw new InvalidDataException("文件内容为Excel而不是CSV，请使用正确扩展名.xls或.xlsx后选择。");return ole?"XLS":"XLSX";}
        if(ext is ".xls" or ".xlsx")throw new InvalidDataException("该文件不是有效Excel工作簿，不能按Excel读取。请确认原始文件格式。");
        if(ext!=".csv")throw new InvalidDataException("仅支持CSV、XLS和XLSX。");
        return "CSV";
    }
    static IExcelDataReader Excel(Stream s)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        try{return ExcelReaderFactory.CreateReader(s,new ExcelReaderConfiguration{FallbackEncoding=Csv.EncodingFor("GB18030")});}
        catch(Exception e)when(e is not OutOfMemoryException){throw new InvalidDataException("Excel无法读取：文件可能损坏、加密或格式不受支持。"+e.Message,e);}
    }
    public static InputInfo Inspect(string path,string encoding="自动",CancellationToken ct=default)
    {
        string format=Format(path);
        if(format=="CSV")
        {
            try{return new(format,encoding=="自动"?Csv.DetectEncoding(path,ct):encoding,[]);}
            catch(DecoderFallbackException e){throw new InvalidDataException("CSV编码校验失败，文件含不完整或非法字节。请核对文件编码；未导入任何数据。",e);}
        }
        using var s=File.OpenRead(path);using var r=Excel(s);var sheets=new List<string>();
        do{ct.ThrowIfCancellationRequested();sheets.Add(r.Name);}while(r.NextResult());
        return new(format,"Excel原生读取（不使用CSV编码）",sheets.ToArray());
    }
    public static IEnumerable<string[]> Read(string path,string encoding="自动",string? sheet=null,string? idColumn=null)
    {
        if(Format(path)=="CSV"){foreach(var row in sheet=="__RESOURCE_RAW__"?Csv.ReadResourceExport(path,encoding):Csv.Read(path,encoding))yield return row;yield break;}
        using var s=File.OpenRead(path);using var r=Excel(s);
        if(sheet!=null){while(r.Name!=sheet)if(!r.NextResult())throw new InvalidDataException("工作表不存在："+sheet);}
        int width=-1,idIndex=-1;long line=0;
        while(r.Read())
        {
            line++;if(width<0)
            {
                width=r.FieldCount;while(width>0&&string.IsNullOrWhiteSpace(Convert.ToString(r.GetValue(width-1),CultureInfo.InvariantCulture)))width--;
                if(width==0){width=-1;continue;}
            }
            if(Enumerable.Range(width,Math.Max(0,r.FieldCount-width)).Any(i=>!string.IsNullOrWhiteSpace(Convert.ToString(r.GetValue(i),CultureInfo.InvariantCulture))))throw new InvalidDataException($"工作表“{r.Name}”第{line}行存在无表头数据列，请补全表头后重试。");
            var row=new string[width];
            for(int i=0;i<width;i++)
            {
                object? v=r.GetValue(i);
                if(i==idIndex&&v is double d)
                {
                    if(Math.Abs(d)>=1e15)throw new InvalidDataException($"工作表“{r.Name}”第{line}行：ID以超过15位的Excel数值保存，可能已丢失精度，无法恢复。请从原始系统导出文本ID后重试；原库保持不变。");
                    string fmt=r.GetNumberFormatString(i)??"";
                    row[i]=fmt.Length>0&&fmt.All(c=>c=='0')?d.ToString(fmt,CultureInfo.InvariantCulture):d.ToString("G17",CultureInfo.InvariantCulture);
                }
                else row[i]=v switch{null=>"",double number=>number.ToString("G17",CultureInfo.InvariantCulture),DateTime date=>date.ToString("O",CultureInfo.InvariantCulture),_=>Convert.ToString(v,CultureInfo.InvariantCulture)??""};
            }
            if(idIndex<0&&line>=1)idIndex=Array.FindIndex(row,x=>x.Trim()==(idColumn??"ID"));
            if(row.All(string.IsNullOrWhiteSpace))continue;
            yield return row;
        }
    }
}
