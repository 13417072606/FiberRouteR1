using System.IO.Compression;
namespace FiberRoute.Updater;
public static class PackageValidator
{
    public static void Extract(string zip,string stage)
    {
        stage=Path.GetFullPath(stage);
        Directory.CreateDirectory(stage);using(var archive=ZipFile.OpenRead(zip))
    {
        long expanded=0;
        foreach(var entry in archive.Entries)
        {
            expanded+=entry.Length;if(expanded>2L*1024*1024*1024)throw new InvalidDataException("解压大小超限");
            var name=entry.FullName.Replace('\\','/');if(name.StartsWith("纤路R1/",StringComparison.Ordinal))name=name[5..];if(name.Length==0||name.EndsWith('/'))continue;
            if(name!="纤路R1.exe"&&name!="使用说明.txt"&&name!="验证报告.md"&&name!="文件校验SHA256.json"&&!name.StartsWith("第三方许可/",StringComparison.Ordinal)&&!name.StartsWith("程序/",StringComparison.Ordinal)&&!name.StartsWith("资源/",StringComparison.Ordinal))throw new InvalidDataException("更新包存在非允许文件："+name);
            string target=Path.GetFullPath(Path.Combine(stage,name));if(!target.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("非法压缩路径");
            Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target);
        }
    }
    if(!File.Exists(Path.Combine(stage,"程序","FiberRouteR1.exe")))throw new InvalidDataException("缺少主程序入口");
    }
}
