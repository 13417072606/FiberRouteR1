using System.Text;
using Microsoft.VisualBasic.FileIO;
namespace FiberRoute.Core;

public static class Csv
{
    public const string Auto = "自动识别";
    public static Encoding EncodingFor(string name)
    {
        Encoding.RegisterProvider(CodePagesEncodingProvider.Instance);
        return name.ToUpperInvariant() switch {
            "UTF-8" => new UTF8Encoding(false, true),
            "UTF-16LE" => new UnicodeEncoding(false, true, true),
            "UTF-16BE" => new UnicodeEncoding(true, true, true),
            "UTF-32LE" => new UTF32Encoding(false, true, true),
            "UTF-32BE" => new UTF32Encoding(true, true, true),
            _ => Encoding.GetEncoding(name, EncoderFallback.ExceptionFallback, DecoderFallback.ExceptionFallback)
        };
    }
    public static string DetectEncoding(string path)
    {
        using var stream = File.OpenRead(path);
        byte[] sample = new byte[4096]; int count = stream.Read(sample);
        if (count >= 4 && sample.AsSpan(0,4).SequenceEqual(new byte[]{255,254,0,0})) return Checked(path,"UTF-32LE");
        if (count >= 4 && sample.AsSpan(0,4).SequenceEqual(new byte[]{0,0,254,255})) return Checked(path,"UTF-32BE");
        if (count >= 3 && sample.AsSpan(0,3).SequenceEqual(new byte[]{239,187,191})) return Checked(path,"UTF-8");
        if (count >= 2 && sample[0]==255 && sample[1]==254) return Checked(path,"UTF-16LE");
        if (count >= 2 && sample[0]==254 && sample[1]==255) return Checked(path,"UTF-16BE");
        // Office sometimes exports UTF-16 without a BOM. Require repeated ASCII
        // pairs instead of mistaking an arbitrary binary NUL for an encoding.
        int even=0,odd=0;
        for(int i=0;i+1<count;i+=2){if(sample[i]==0)even++;if(sample[i+1]==0)odd++;}
        if(odd>=4 && odd>even*4 && Valid(path,"UTF-16LE")) return Checked(path,"UTF-16LE");
        if(even>=4 && even>odd*4 && Valid(path,"UTF-16BE")) return Checked(path,"UTF-16BE");
        if(Valid(path,"UTF-8")) return "UTF-8";
        if(Valid(path,"GB18030")) return "GB18030";
        throw new InvalidDataException("无法识别文本编码。请另存为 UTF-8 CSV，或手动选择文件实际编码。");
    }
    static string Checked(string path,string encoding)
    {
        if(!Valid(path,encoding))throw new InvalidDataException("文件包含不符合 "+encoding+" 的字节，请检查原文件编码。");
        return encoding;
    }
    static StreamReader OpenReader(string path,string encoding)
    {
        var reader=new StreamReader(path,EncodingFor(encoding),false);
        if(reader.Peek()==0xFEFF)reader.Read();
        return reader;
    }
    static bool Valid(string path,string encoding)
    {
        try { using var reader=new StreamReader(path,EncodingFor(encoding),false);char[] buffer=new char[32768];while(reader.Read(buffer)>0){}return true; }
        catch(DecoderFallbackException){return false;}
    }
    public static string ResolveEncoding(string path,string encoding) => string.IsNullOrWhiteSpace(encoding)||encoding==Auto ? DetectEncoding(path):encoding;
    public static IEnumerable<string[]> Read(string path,string encoding=Auto)
    {
        string resolved=ResolveEncoding(path,encoding);
        using var source=OpenReader(path,resolved);
        string? first;
        do { first=source.ReadLine(); } while(first!=null&&string.IsNullOrWhiteSpace(first));
        if(first==null)yield break;
        bool separator=first.StartsWith("sep=",StringComparison.OrdinalIgnoreCase)&&first.Length==5&&",;\t".Contains(first[4]);
        string delimiter=separator?first[4].ToString():DetectDelimiter(first);
        using var reader=OpenReader(path,resolved);
        if(separator) {string? line;do{line=reader.ReadLine();}while(line!=null&&string.IsNullOrWhiteSpace(line));}
        using var parser=new TextFieldParser(reader){TextFieldType=FieldType.Delimited,HasFieldsEnclosedInQuotes=true,TrimWhiteSpace=false};
        parser.SetDelimiters(delimiter);
        while(!parser.EndOfData)
        {
            string[] row;
            try{row=parser.ReadFields()??[];}
            catch(MalformedLineException e){throw new InvalidDataException($"CSV 第 {e.LineNumber} 行引号或分隔符格式错误：{e.Message}",e);}
            yield return row;
        }
    }
    static string DetectDelimiter(string header)
    {
        var counts=new Dictionary<char,int>{{',',0},{';',0},{'\t',0}};bool quoted=false;
        for(int i=0;i<header.Length;i++)
        {
            if(header[i]=='"'){if(quoted&&i+1<header.Length&&header[i+1]=='"'){i++;continue;}quoted=!quoted;}
            else if(!quoted&&counts.ContainsKey(header[i]))counts[header[i]]++;
        }
        return counts.OrderByDescending(x=>x.Value).First().Key.ToString();
    }
    public static string Line(IEnumerable<object?> fields)=>string.Join(",",fields.Select(v=>"\""+(Convert.ToString(v,System.Globalization.CultureInfo.InvariantCulture)??"").Replace("\"","\"\"")+"\""));
    public static void Write(string path,IEnumerable<IEnumerable<object?>> rows)
    {using var w=new StreamWriter(path,false,new UTF8Encoding(true));foreach(var r in rows)w.WriteLine(Line(r));}
}
