using ExcelDataReader;
using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
namespace FiberRoute.Core;

public sealed record TableInfo(string Format,string Encoding,string[] Sheets);
public static class Tabular
{
    public static bool IsExcel(string path)=>Path.GetExtension(path).ToLowerInvariant() is ".xls" or ".xlsx";
    static IExcelDataReader Excel(Stream stream)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return ExcelReaderFactory.CreateReader(stream,new ExcelReaderConfiguration{LeaveOpen=true,FallbackEncoding=Encoding.GetEncoding(1252)});
    }
    public static TableInfo Inspect(string path,string encoding=Csv.Auto)
    {
        if(!IsExcel(path))return new("CSV",Csv.ResolveEncoding(path,encoding),[]);
        using var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=Excel(stream);var sheets=new List<string>();
        do {sheets.Add(reader.Name);}while(reader.NextResult());
        return new(Path.GetExtension(path).TrimStart('.').ToUpperInvariant(),"Excel 工作簿",sheets.ToArray());
    }
    public static IEnumerable<string[]> Read(string path,string encoding=Csv.Auto,string? sheet=null)
    {
        if(!IsExcel(path)){foreach(var row in Csv.Read(path,encoding))yield return row;yield break;}
        using var stream=File.Open(path,FileMode.Open,FileAccess.Read,FileShare.ReadWrite);
        using var reader=Excel(stream);
        if(sheet!=null)
        {
            while(reader.Name!=sheet)if(!reader.NextResult())throw new InvalidDataException("找不到工作表："+sheet+"，请重新选择并预览。");
        }
        int width=0;
        while(reader.Read())
        {
            var row=Enumerable.Range(0,reader.FieldCount).Select(i=>Cell(reader,i)).ToArray();
            int used=Array.FindLastIndex(row,x=>!string.IsNullOrWhiteSpace(x))+1;
            if(used==0)continue;
            if(width==0)width=used;
            // Ignore style-only trailing Excel columns, but keep genuinely extra
            // cells so resource import can isolate that row as an anomaly.
            yield return row.Take(Math.Max(width,used)).ToArray();
        }
    }
    static string Cell(IExcelDataReader reader,int index)
    {
        object? value=reader.GetValue(index);
        if(value is null)return "";
        if(value is string s)return s;
        if(value is DateTime date)return date.ToString("yyyy-MM-dd HH:mm:ss",CultureInfo.InvariantCulture);
        if(value is double number)
        {
            string format=reader.GetNumberFormatString(index)??"";
            if(format.Length<=100&&Regex.IsMatch(format,"^0+$")&&double.IsFinite(number)&&number==Math.Truncate(number))return number.ToString(format,CultureInfo.InvariantCulture);
            return number.ToString("G17",CultureInfo.InvariantCulture);
        }
        return Convert.ToString(value,CultureInfo.InvariantCulture)??"";
    }
    public static string[] Headers(string[] row)
    {
        var headers=row.Select(x=>x.Trim().TrimStart('\uFEFF').Trim()).ToArray();
        if(headers.Length==0||headers.Any(string.IsNullOrWhiteSpace))throw new InvalidDataException("表头含空列，请删除无名列或补齐表头。");
        if(headers.Distinct(StringComparer.Ordinal).Count()!=headers.Length)throw new InvalidDataException("表头有重复列名，请修改后重新导入。");
        return headers;
    }
}
