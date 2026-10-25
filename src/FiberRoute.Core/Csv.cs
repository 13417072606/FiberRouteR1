using System.Text;
using Microsoft.VisualBasic.FileIO;
namespace FiberRoute.Core;
public static class Csv
{
    public static Encoding EncodingFor(string name) { Encoding.RegisterProvider(CodePagesEncodingProvider.Instance); return name=="UTF-8"?new UTF8Encoding(false,true):Encoding.GetEncoding(name,EncoderFallback.ExceptionFallback,DecoderFallback.ExceptionFallback); }
    public static IEnumerable<string[]> Read(string path,string encoding="UTF-8")
    {
        using var parser=new TextFieldParser(path,EncodingFor(encoding),true) { TextFieldType=FieldType.Delimited, HasFieldsEnclosedInQuotes=true, TrimWhiteSpace=false };
        parser.SetDelimiters(",");
        while(!parser.EndOfData) yield return parser.ReadFields()??[];
    }
    public static string Line(IEnumerable<object?> fields)=>string.Join(",",fields.Select(v=>"\""+(Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture)??"").Replace("\"","\"\"")+"\""));
    public static void Write(string path,IEnumerable<IEnumerable<object?>> rows)
    {using var w=new StreamWriter(path,false,new UTF8Encoding(true)); foreach(var r in rows)w.WriteLine(Line(r));}
}
