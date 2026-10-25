using FiberRoute.Core;
using System.Text;
using System.Security.Cryptography;
static class ImportChecks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        string text="ID,名称,长度,空闲数量,中继纤芯数量,始端设施,终端设施\r\n000123456789012345678901,\"逗号,引号\"\"和\r\n换行\",0,1,2,甲设施,乙设施\r\n";
        var map=Store.Fields.ToDictionary(x=>x,x=>x);var store=new Store(Path.Combine(root,"new-imports"));
        foreach(var item in new[]{("utf8",(Encoding)new UTF8Encoding(false,true)),("utf8bom",new UTF8Encoding(true,true)),("gbk",Csv.EncodingFor("GBK")),("gb18030",Csv.EncodingFor("GB18030")),("utf16",new UnicodeEncoding(false,true,true)),("utf16be",new UnicodeEncoding(true,true,true)),("utf16nobom",new UnicodeEncoding(false,false,true))})
        {
            string path=Path.Combine(root,item.Item1+".csv");File.WriteAllText(path,text,item.Item2);
            var info=TableInput.Inspect(path);var result=await store.ImportAsync(path,"自动",map,1,null,default);
            check(result.Accepted==1&&store.Graph.Edges[0].Id=="000123456789012345678901"&&store.Graph.Nodes[0].Name=="甲设施"&&store.Graph.Edges[0].Name.Contains("\r\n"),"automatic "+item.Item1+" / strict text / ID / quoted multiline");
        }
        string four=Path.Combine(root,"four-byte.csv");File.WriteAllText(four,text.Replace("甲设施","𠀀设施"),Csv.EncodingFor("GB18030"));
        await store.ImportAsync(four,"自动",map,1,null,default);check(store.Graph.Nodes[0].Name=="𠀀设施","GB18030 four-byte character");
        foreach(string ext in new[]{"xls","xlsx"})
        {
            string file=Path.GetFullPath($"tests/fixtures/import.{ext}");var info=TableInput.Inspect(file);
            check(info.Format==ext.ToUpperInvariant()&&info.Sheets.SequenceEqual(new[]{"说明","资源表","精度风险","类型表"}),ext+" native format and sheet discovery");
            var custom=new Dictionary<string,string>(map){["ID"]="资源编号"};
            var result=await store.ImportAsync(file,"自动",custom,1,null,default,"资源表");var g=store.Graph;
            check(result.Accepted==2&&g.Nodes.Length==3&&g.Nodes[0].Name=="甲-设施"&&g.Edges[0].Id=="000123456789012345678901"&&g.Edges[0].Name.Contains('\n')&&g.Edges[0].Length==0&&g.Edges[1].Length==null,ext+" actual import / mapping / exact long ID / zero and missing length");
            string before=Hash(store.Database);bool failed=false;
            try{await store.ImportAsync(file,"自动",custom,1,null,default,"精度风险");}catch(InvalidDataException e){failed=e.Message.Contains("精度");}
            check(failed&&Hash(store.Database)==before,ext+" numeric ID precision risk rejects and preserves database");
            var preview=store.PreviewTypes(file,"自动","设施","类别","类型表");
            check(preview.Count(x=>x.Status=="同名类型冲突")==2&&preview.Count(x=>x.Status=="未匹配")==1&&preview.Count(x=>x.Status=="空类型")==1&&Hash(store.Database)==before,ext+" native type preview / conflicts / unknown / blank / no mutation");
            bool wrong=false;try{TableInput.Read(file,"自动","不存在").ToArray();}catch(InvalidDataException){wrong=true;}check(wrong,ext+" missing sheet actionable error");
            using var cts=new CancellationTokenSource();cts.Cancel();bool cancelled=false;
            try{await store.ImportAsync(file,"自动",custom,1,null,cts.Token,"资源表");}catch(OperationCanceledException){cancelled=true;}
            check(cancelled&&Hash(store.Database)==before,ext+" cancellation retains original");
        }
        string bad=Path.Combine(root,"bad.csv");File.WriteAllBytes(bad,[0xef,0xbb,0xbf,0x49,0x44,0x0a,0xc3]);bool strict=false;
        try{TableInput.Inspect(bad);}catch(InvalidDataException e){strict=e.Message.Contains("编码校验失败");}
        check(strict,"invalid UTF-8 BOM payload never replaced silently");
        File.Copy("tests/fixtures/import.xls",bad,true);bool mislabeled=false;try{TableInput.Inspect(bad);}catch(InvalidDataException e){mislabeled=e.Message.Contains("Excel");}check(mislabeled,"Excel binary never parsed as CSV text");
        string fake=Path.Combine(root,"bad.xlsx");File.WriteAllText(fake,"not an Excel file");bool invalid=false;try{TableInput.Inspect(fake);}catch(InvalidDataException){invalid=true;}check(invalid,"invalid Excel gives format error");
        string corrupt=Path.Combine(root,"corrupt.xlsx");File.WriteAllBytes(corrupt,[0x50,0x4b,3,4,0,0,0,0]);bool damaged=false;try{TableInput.Inspect(corrupt);}catch(InvalidDataException){damaged=true;}check(damaged,"corrupt Excel container rejected");
    }
    static string Hash(string path)=>Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(path)));
}
