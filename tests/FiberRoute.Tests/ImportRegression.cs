using System.Security.Cryptography;
using System.Text;
using FiberRoute.Core;

internal static class ImportRegression
{
    public static async Task Run(Action<bool,string> check,string root)
    {
        var store=new Store(Path.Combine(root,"import-regression"));
        var mapping=Store.Fields.ToDictionary(x=>x,x=>x);
        string header="ID,名称,长度,空闲数量,中继纤芯数量,始端设施,终端设施";
        string text=header+"\n000123456789012345678901,中文资源,1.25,1,2,中文甲机房,中文乙GJ\n";
        foreach(string encoding in new[]{"UTF-8","GBK","GB18030","UTF-16LE","UTF-16BE","UTF-32LE","UTF-32BE"})
        {
            string path=Path.Combine(root,"auto-"+encoding+".csv");
            var enc=Csv.EncodingFor(encoding);File.WriteAllText(path,text,enc);
            var info=Tabular.Inspect(path);var result=await store.ImportAsync(path,Csv.Auto,mapping,1,null,default);
            check(result.Accepted==1&&store.Graph.Nodes[0].Name=="中文甲机房"&&store.Graph.Edges[0].Id=="000123456789012345678901",encoding+" automatic detection/import preserves Chinese and long text ID");
        }
        string full=Path.Combine(root,"late-gb.csv");File.WriteAllText(full,header+"\n"+new string(' ',5000)+"1,GB资源,1,1,2,中文甲,中文乙\n",Csv.EncodingFor("GB18030"));
        check(Csv.DetectEncoding(full)=="GB18030","encoding validation covers bytes beyond first sample");
        string ext=Path.Combine(root,"gb-four.csv");File.WriteAllText(ext,text.Replace("中文甲机房","𠀀机房"),Csv.EncodingFor("GB18030"));
        await store.ImportAsync(ext,Csv.Auto,mapping,1,null,default);check(store.Graph.Nodes[0].Name=="𠀀机房","GB18030 four-byte characters preserved");
        foreach(string delimiter in new[]{";","\t"})
        {
            string path=Path.Combine(root,delimiter==";"?"semicolon.csv":"tabs.tsv");File.WriteAllText(path,(delimiter==";"?"sep=;\n":"")+text.Replace(",",delimiter),new UTF8Encoding(true));
            var imported=await store.ImportAsync(path,Csv.Auto,mapping,1,null,default);check(imported.Accepted==1,"automatic delimiter "+(delimiter==";"?"semicolon with Excel sep line":"tab"));
        }
        string multiline=Path.Combine(root,"auto-quotes.csv");Csv.Write(multiline,new object?[][]{[" ID ","名称","长度","空闲数量","中继纤芯数量","始端设施","终端设施"],["0007","名称,含\"引号\"\n第二行",2.5,1,4,"甲,机房","乙"]});
        await store.ImportAsync(multiline,Csv.Auto,mapping,1,null,default);
        check(store.Graph.Edges[0].Id=="0007"&&store.Graph.Nodes[0].Name=="甲,机房","normalized preview/import headers and quoted comma/multiline fields");
        string before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database)));
        string invalid=Path.Combine(root,"broken-utf8.csv");File.WriteAllBytes(invalid,new byte[]{239,187,191,255,255,255});
        bool rejected=false;try{await store.ImportAsync(invalid,Csv.Auto,mapping,1,null,default);}catch(InvalidDataException){rejected=true;}
        check(rejected&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),"invalid BOM encoding fails without replacing database");
        foreach(string extension in new[]{"xls","xlsx"})
        {
            string path=Path.GetFullPath("tests/fixtures/导入回归."+extension);var info=Tabular.Inspect(path);
            check(info.Sheets.SequenceEqual(new[]{"说明","资源数据","节点类型"}),extension+" workbook lists worksheets");
            var preview=Tabular.Read(path,Csv.Auto,"资源数据").Take(21).ToArray();
            check(Tabular.Headers(preview[0]).Length==7&&preview.Length==3,extension+" ignores style-only trailing columns in preview");
            var imported=await store.ImportAsync(path,Csv.Auto,mapping,1000,null,default,"资源数据");
            check(imported.Accepted==2&&store.Graph.Edges[0].Id=="000123456789012345678901"&&store.Graph.Edges[1].Id=="0007"&&store.Graph.Edges[0].Length==1250&&store.Graph.Edges[1].Length==0,extension+" selected-sheet import, long text/zero-format IDs, km and zero values");
            string oldType=store.Graph.Nodes[1].Type;
            var changes=store.PreviewTypes(path,Csv.Auto,"节点名称","节点类型","节点类型");
            check(changes.Count==1&&changes[0].Status=="可更新"&&changes[0].After=="机房;自定义"&&store.Graph.Nodes[1].Type==oldType,extension+" type preview independent and nonmutating");
            store.SetTypes(changes);check(store.Graph.Nodes[1].Confirmed&&store.Graph.Nodes[1].Type=="机房;自定义",extension+" explicit type confirmation applies");
            before=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database)));
            rejected=false;try{await store.ImportAsync(path,Csv.Auto,mapping,1,null,default,"不存在的工作表");}catch(InvalidDataException){rejected=true;}
            check(rejected&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),extension+" missing worksheet preserves database");
            rejected=false;try{await store.ImportAsync(path,Csv.Auto,mapping,1,null,default,"说明");}catch(InvalidDataException){rejected=true;}
            check(rejected&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),extension+" wrong worksheet/mapping preserves database");
            using var cancelled=new CancellationTokenSource();cancelled.Cancel();rejected=false;try{await store.ImportAsync(path,Csv.Auto,mapping,1,null,cancelled.Token,"资源数据");}catch(OperationCanceledException){rejected=true;}
            check(rejected&&before==Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(store.Database))),extension+" cancelled import preserves database");
        }
    }
}
