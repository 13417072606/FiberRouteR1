using System.IO.Compression;
using System.Security.Cryptography;
using System.Text.Json;
namespace FiberRoute.Updater;
public static class PackageValidator
{
    public static void Extract(string zip,string stage)
    {
        stage=Path.GetFullPath(stage);Directory.CreateDirectory(stage);var written=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        using(var archive=ZipFile.OpenRead(zip))
        {
            long expanded=0;foreach(var entry in archive.Entries)
            {
                expanded+=entry.Length;if(expanded>2L*1024*1024*1024)throw new InvalidDataException("解压大小超限");
                string name=entry.FullName.Replace('\\','/');foreach(string prefix in new[]{"纤路R1/","光路调度智能分析R1.4/"})if(name.StartsWith(prefix,StringComparison.Ordinal)){name=name[prefix.Length..];break;}
                if(name.Length==0||name.EndsWith('/'))continue;
                if(name.Contains(':')||name.Split('/').Any(part=>part is ".." or "." or "")||!written.Add(name))throw new InvalidDataException("更新包路径重复或非法："+name);
                if(name is not ("光路调度智能分析.exe" or "纤路R1.exe" or "使用说明.txt" or "验证报告.md" or "文件校验SHA256.json" or "更新清单.json")&&!name.StartsWith("第三方许可/",StringComparison.Ordinal)&&!name.StartsWith("程序/",StringComparison.Ordinal)&&!name.StartsWith("资源/",StringComparison.Ordinal))throw new InvalidDataException("更新包存在非允许文件："+name);
                string target=Path.GetFullPath(Path.Combine(stage,name));if(!target.StartsWith(stage+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("非法压缩路径");Directory.CreateDirectory(Path.GetDirectoryName(target)!);entry.ExtractToFile(target);
            }
        }
        if(!File.Exists(Path.Combine(stage,"程序","FiberRouteR1.exe"))&&!File.Exists(Path.Combine(stage,"程序","FiberRouteR1.dll")))throw new InvalidDataException("缺少主程序入口");
        string manifest=Path.Combine(stage,"更新清单.json");if(File.Exists(manifest))
        {
            using var json=JsonDocument.Parse(File.ReadAllBytes(manifest));var expected=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            foreach(var file in json.RootElement.GetProperty("Files").EnumerateArray())
            {
                string relative=file.GetProperty("Path").GetString()!.Replace('\\','/');if(!written.Contains(relative)||!expected.Add(relative))throw new InvalidDataException("软件更新清单与包内容不符");string path=Path.Combine(stage,relative);using var stream=File.OpenRead(path);
                if(stream.Length!=file.GetProperty("Bytes").GetInt64()||!Convert.ToHexString(SHA256.HashData(stream)).Equals(file.GetProperty("Sha256").GetString(),StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("软件更新文件校验失败："+relative);
            }
            if(written.Count!=expected.Count+1)throw new InvalidDataException("软件更新包含清单外文件");
        }
    }
}
