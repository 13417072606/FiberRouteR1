using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;

namespace FiberRoute.Core;
public static class Publishing
{
    static readonly JsonSerializerOptions Pretty=new(){WriteIndented=true};
    static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    static void ValidVersion(string version){if(!Regex.IsMatch(version,@"^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$"))throw new ArgumentException("版本号只允许字母、数字、点、短横线和下划线");}
    public static void Initialize(string directory,string baseUrl)
    {
        var uri=new Uri(baseUrl.EndsWith('/')?baseUrl:baseUrl+"/");if(uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)throw new ArgumentException("请提供国内发布目录的HTTPS地址");Directory.CreateDirectory(directory);string key=Path.Combine(directory,"private-key.pem");if(File.Exists(key))throw new IOException("此工作目录已存在私钥，不会覆盖");
        using var rsa=RSA.Create(3072);File.WriteAllText(key,rsa.ExportPkcs8PrivateKeyPem());File.WriteAllText(Path.Combine(directory,"public-key.pem"),rsa.ExportSubjectPublicKeyInfoPem());var config=new UpdateConfig(new Uri(uri,"manifest.json").AbsoluteUri,rsa.ExportSubjectPublicKeyInfoPem());File.WriteAllText(Path.Combine(directory,"update-service.json"),JsonSerializer.Serialize(config,Pretty));
        string client=Path.Combine(directory,"client-config");Directory.CreateDirectory(client);File.WriteAllText(Path.Combine(client,"update-service.json"),JsonSerializer.Serialize(config,Pretty));Directory.CreateDirectory(Path.Combine(directory,"site"));File.WriteAllText(Path.Combine(directory,"release-payload.json"),JsonSerializer.Serialize(new ReleaseManifest(null,null),Pretty));
    }
    public static void BuildProgramPackage(string root,string destination,string version)
    {
        ValidVersion(version);string assembly=Path.Combine(root,"程序","FiberRouteR1.dll");if(!File.Exists(assembly))throw new FileNotFoundException("请选择包含“程序”文件夹的新版本完整运行目录");if(AssemblyName.GetAssemblyName(assembly).Version?.ToString(3)!=version)throw new InvalidDataException("发布版本必须与新版本DLL一致，例如1.4.0");
        var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        foreach(string directory in new[]{"程序","资源","第三方许可"}){string path=Path.Combine(root,directory);if(!Directory.Exists(path))continue;foreach(string file in Directory.GetFiles(path,"*",SearchOption.AllDirectories)){string relative=Path.GetRelativePath(root,file).Replace('\\','/');if(relative.EndsWith(".pdb",StringComparison.OrdinalIgnoreCase)||relative.Contains(".partial",StringComparison.OrdinalIgnoreCase))continue;if(Path.GetFileName(file).Contains("private",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(file).Contains("私钥"))throw new InvalidDataException("软件目录中发现私钥文件，拒绝发布");files.Add(relative,file);}}
        foreach(string name in new[]{"光路调度智能分析.exe","使用说明.txt"})if(File.Exists(Path.Combine(root,name)))files.Add(name,Path.Combine(root,name));
        var manifest=new{Version=version,Files=files.Select(pair=>new{Path=pair.Key,Bytes=new FileInfo(pair.Value).Length,Sha256=Hash(pair.Value)}).ToArray()};Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);string temp=destination+".partial";try{if(File.Exists(temp))File.Delete(temp);using(var archive=ZipFile.Open(temp,ZipArchiveMode.Create)){foreach(var file in files)archive.CreateEntryFromFile(file.Value,file.Key,CompressionLevel.Optimal);using var writer=new StreamWriter(archive.CreateEntry("更新清单.json").Open());writer.Write(JsonSerializer.Serialize(manifest));}File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    public static ReleaseManifest Release(string directory,string channel,string version,string package,string notes="")
    {
        ValidVersion(version);if(channel is not ("software" or "data"))throw new ArgumentException("发布类型为software或data");var config=Updates.LoadConfig(Path.Combine(directory,"update-service.json"))??throw new InvalidDataException("请先初始化发布工作目录");
        using(var archive=ZipFile.OpenRead(package)){string file=channel=="software"?"更新清单.json":"manifest.json";var entry=archive.GetEntry(file)??throw new InvalidDataException("更新包类型不符");using var stream=entry.Open();using var json=JsonDocument.Parse(stream);if(json.RootElement.GetProperty("Version").GetString()!=version)throw new InvalidDataException("更新包内部版本与发布版本不一致");}
        string relative=(channel=="software"?"program/":"data/")+version+"/"+(channel=="software"?"program-":"resources-")+version+".zip";string target=Path.Combine(directory,"site",relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(package,target,true);
        var item=new ReleaseItem(version,new Uri(new Uri(config.ManifestUrl),relative).AbsoluteUri,Hash(target),new FileInfo(target).Length,notes);var payloadFile=Path.Combine(directory,"release-payload.json");var current=File.Exists(payloadFile)?JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(payloadFile))??new(null,null):new ReleaseManifest(null,null);var next=channel=="software"?current with{Program=item}:current with{Resources=item};byte[] payload=JsonSerializer.SerializeToUtf8Bytes(next);using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(Path.Combine(directory,"private-key.pem")));byte[] signature=rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);string manifest=Path.Combine(directory,"site","manifest.json");File.WriteAllText(manifest+".partial",JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(signature)}));File.Move(manifest+".partial",manifest,true);File.WriteAllBytes(payloadFile,payload);return next;
    }
}
