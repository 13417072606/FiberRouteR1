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
    static bool SigningMaterial(string file)
    {
        string name=Path.GetFileName(file);
        string[] runtime=["System.Private.CoreLib.dll","System.Private.Uri.dll","System.Private.Xml.dll","System.Private.Xml.Linq.dll","System.Private.DataContractSerialization.dll"];
        return name.Contains("私钥",StringComparison.Ordinal)||name.Contains("private",StringComparison.OrdinalIgnoreCase)&&!runtime.Contains(name,StringComparer.OrdinalIgnoreCase)||new[]{".pfx",".p12",".snk"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase);
    }
    public static void Initialize(string directory,string baseUrl)
    {
        var uri=new Uri(baseUrl.EndsWith('/')?baseUrl:baseUrl+"/");if(uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0)throw new ArgumentException("请提供发布目录的HTTPS地址");Directory.CreateDirectory(directory);string key=Path.Combine(directory,"private-key.pem");if(File.Exists(key))throw new IOException("此工作目录已存在私钥，不会覆盖");
        using var rsa=RSA.Create(3072);File.WriteAllText(key,rsa.ExportPkcs8PrivateKeyPem());File.WriteAllText(Path.Combine(directory,"public-key.pem"),rsa.ExportSubjectPublicKeyInfoPem());var config=new UpdateConfig(new Uri(uri,"manifest.json").AbsoluteUri,rsa.ExportSubjectPublicKeyInfoPem());File.WriteAllText(Path.Combine(directory,"update-service.json"),JsonSerializer.Serialize(config,Pretty));
        string client=Path.Combine(directory,"client-config");Directory.CreateDirectory(client);File.WriteAllText(Path.Combine(client,"update-service.json"),JsonSerializer.Serialize(config,Pretty));Directory.CreateDirectory(Path.Combine(directory,"site"));File.WriteAllText(Path.Combine(directory,"release-payload.json"),JsonSerializer.Serialize(new ReleaseManifest(null,null),Pretty));
    }
    public static void SaveConfiguration(string directory,string baseUrl)
    {
        var uri=new Uri(baseUrl.EndsWith('/')?baseUrl:baseUrl+"/");if(uri.Scheme!="https"||uri.UserInfo.Length>0||uri.Query.Length>0||uri.Fragment.Length>0||uri.AbsolutePath.TrimEnd('/').EndsWith(".json",StringComparison.OrdinalIgnoreCase))throw new ArgumentException("请填写真实HTTPS更新目录地址，例如https://单位域名/fiber-updates/，不要填写清单文件地址");
        string key=Path.Combine(directory,"private-key.pem"),payloadFile=Path.Combine(directory,"release-payload.json"),oldManifest=Path.Combine(directory,"site","manifest.json"),oldConfig=Path.Combine(directory,"update-service.json");
        if(!File.Exists(key))
        {
            if(File.Exists(payloadFile)||File.Exists(oldManifest)||File.Exists(oldConfig))throw new InvalidDataException("现有发布工作区缺少原签名私钥；不会生成第二套密钥覆盖旧客户端配置");
            Initialize(directory,baseUrl);return;
        }
        using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(key));var config=new UpdateConfig(new Uri(uri,"manifest.json").AbsoluteUri,rsa.ExportSubjectPublicKeyInfoPem());
        var previousConfig=Updates.LoadConfig(oldConfig);
        if(previousConfig!=null){using var previousKey=RSA.Create();previousKey.ImportFromPem(previousConfig.PublicKeyPem);if(!previousKey.ExportSubjectPublicKeyInfo().SequenceEqual(rsa.ExportSubjectPublicKeyInfo()))throw new InvalidDataException("工作区私钥与原客户公钥不匹配，已停止；请找回原密钥");}
        var current=File.Exists(oldManifest)?Updates.VerifyEnvelope(File.ReadAllBytes(oldManifest),config.PublicKeyPem):File.Exists(payloadFile)?JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(payloadFile))??new(null,null):new ReleaseManifest(null,null);
        ReleaseItem? Rebase(ReleaseItem? item,bool program)
        {
            if(item==null)return null;string relative=(program?"program/":"data/")+item.Version+"/"+(program?"program-":"resources-")+item.Version+".zip";
            return item with{Url=new Uri(uri,relative).AbsoluteUri,Parts=item.Parts?.Select((p,i)=>p with{Url=new Uri(uri,relative+"."+(i+1).ToString("000")).AbsoluteUri}).ToArray()};
        }
        var next=new ReleaseManifest(Rebase(current.Program,true),Rebase(current.Resources,false));byte[] payload=JsonSerializer.SerializeToUtf8Bytes(next),signature=rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);
        Directory.CreateDirectory(Path.Combine(directory,"client-config"));string json=JsonSerializer.Serialize(config,Pretty);File.WriteAllText(Path.Combine(directory,"update-service.json"),json);File.WriteAllText(Path.Combine(directory,"client-config","update-service.json"),json);File.WriteAllText(Path.Combine(directory,"public-key.pem"),config.PublicKeyPem);File.WriteAllBytes(payloadFile,payload);Directory.CreateDirectory(Path.Combine(directory,"site"));string manifest=Path.Combine(directory,"site","manifest.json");File.WriteAllText(manifest+".partial",JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(signature)}));File.Move(manifest+".partial",manifest,true);
    }
    public static void BuildProgramPackage(string root,string destination,string version)
    {
        ValidVersion(version);string assembly=Path.Combine(root,"程序","FiberRouteR1.dll");if(!File.Exists(assembly))throw new FileNotFoundException("请选择包含“程序”文件夹的新版本完整运行目录");if(AssemblyName.GetAssemblyName(assembly).Version?.ToString(3)!=version)throw new InvalidDataException("发布版本必须与新版本DLL一致，例如1.4.0");
        var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);
        // Software updates contain only executable files. The resource folder can contain real
        // facility/station databases, which must never be published as part of a program update.
        string program=Path.Combine(root,"程序");
        if(Directory.Exists(program))foreach(string file in Directory.GetFiles(program,"*",SearchOption.AllDirectories))
        {
            string relative=Path.GetRelativePath(root,file).Replace('\\','/'),name=Path.GetFileName(file);
            if(relative.EndsWith(".pdb",StringComparison.OrdinalIgnoreCase)||relative.Contains(".partial",StringComparison.OrdinalIgnoreCase)||name.StartsWith("FiberRoute.Publisher.",StringComparison.OrdinalIgnoreCase)||name.StartsWith("FiberRoute.Admin.",StringComparison.OrdinalIgnoreCase)||name.StartsWith("FiberRoute.Online.",StringComparison.OrdinalIgnoreCase))continue;
            if(SigningMaterial(file))throw new InvalidDataException("软件目录中发现私钥文件，拒绝发布");
            if(!new[]{".dll",".exe",".ico"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase)&&!name.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase))continue;
            files.Add(relative,file);
        }
        string launcher=Path.Combine(root,"光路调度路由查询.exe");if(File.Exists(launcher))files.Add("光路调度路由查询.exe",launcher);
        var manifest=new{Version=version,Files=files.Select(pair=>new{Path=pair.Key,Bytes=new FileInfo(pair.Value).Length,Sha256=Hash(pair.Value)}).ToArray()};Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(destination))!);string temp=destination+".partial";try{if(File.Exists(temp))File.Delete(temp);using(var archive=ZipFile.Open(temp,ZipArchiveMode.Create)){foreach(var file in files)archive.CreateEntryFromFile(file.Value,file.Key,CompressionLevel.Optimal);using var writer=new StreamWriter(archive.CreateEntry("更新清单.json").Open());writer.Write(JsonSerializer.Serialize(manifest));}File.Move(temp,destination,true);}finally{if(File.Exists(temp))File.Delete(temp);}
    }
    static void WriteSiteExtras(string site)
    {
        File.WriteAllText(Path.Combine(site,"health.json"),"{\"service\":\"fiber-route-updates\",\"status\":\"ok\"}");
        File.WriteAllText(Path.Combine(site,"index.html"),"<!doctype html><meta charset=utf-8><p>Fiber Route update service is ready.</p>");
        File.WriteAllText(Path.Combine(site,"_headers"),"/manifest.json\n  Cache-Control: no-store\n/health.json\n  Cache-Control: no-store\n");
    }
    public static ReleaseManifest Release(string directory,string channel,string version,string package,string notes="")
    {
        ValidVersion(version);if(channel is not ("software" or "data"))throw new ArgumentException("发布类型为software或data");var config=Updates.LoadConfig(Path.Combine(directory,"update-service.json"))??throw new InvalidDataException("请先初始化发布工作目录");
        using(var archive=ZipFile.OpenRead(package)){string file=channel=="software"?"更新清单.json":"manifest.json";var entry=archive.GetEntry(file)??throw new InvalidDataException("更新包类型不符");using var stream=entry.Open();using var json=JsonDocument.Parse(stream);if(json.RootElement.GetProperty("Version").GetString()!=version)throw new InvalidDataException("更新包内部版本与发布版本不一致");}
        string relative=(channel=="software"?"program/":"data/")+version+"/"+(channel=="software"?"program-":"resources-")+version+".zip";string target=Path.Combine(directory,"site",relative.Replace('/',Path.DirectorySeparatorChar));Directory.CreateDirectory(Path.GetDirectoryName(target)!);string digest=Hash(package);long bytes=new FileInfo(package).Length;
        // Keep published versions immutable, including releases whose complete ZIP is not public.
        string oldPayload=Path.Combine(directory,"release-payload.json");if(File.Exists(oldPayload)){var old=JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(oldPayload));var prior=channel=="software"?old?.Program:old?.Resources;if(prior?.Version==version&&!prior.Sha256.Equals(digest,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("该版本号已发布不同内容，请使用新版本号");}
        if(File.Exists(target)&&digest!=Hash(target))throw new InvalidDataException("该版本号已发布不同内容，请使用新版本号");
        ReleasePart[]? parts=null;const int partSize=24*1024*1024;
        if(bytes>partSize)
        {
            var values=new List<ReleasePart>();using var input=File.OpenRead(package);byte[] buffer=new byte[partSize];int count=0;
            while(true){int n=0,read;while(n<buffer.Length&&(read=input.Read(buffer,n,buffer.Length-n))>0)n+=read;if(n==0)break;string suffix="."+(++count).ToString("000"),part=target+suffix,temporary=part+".partial";string hash=Convert.ToHexString(SHA256.HashData(buffer.AsSpan(0,n)));if(File.Exists(part)&&Hash(part)!=hash)throw new InvalidDataException("分片版本已存在不同内容");try{using(var output=File.Create(temporary))output.Write(buffer,0,n);File.Move(temporary,part,true);}finally{if(File.Exists(temporary))File.Delete(temporary);}values.Add(new(new Uri(new Uri(config.ManifestUrl),relative+suffix).AbsoluteUri,hash,n));}
            parts=values.ToArray();
            // A previous whole ZIP in the public site would violate Pages' file limit.
            if(Path.GetFullPath(package)!=Path.GetFullPath(target)&&File.Exists(target))File.Delete(target);
        }
        else if(Path.GetFullPath(package)!=Path.GetFullPath(target))File.Copy(package,target,true);
        var item=new ReleaseItem(version,new Uri(new Uri(config.ManifestUrl),relative).AbsoluteUri,digest,bytes,notes,"1.4.3",parts);var payloadFile=Path.Combine(directory,"release-payload.json");var current=File.Exists(payloadFile)?JsonSerializer.Deserialize<ReleaseManifest>(File.ReadAllText(payloadFile))??new(null,null):new ReleaseManifest(null,null);var next=channel=="software"?current with{Program=item}:current with{Resources=item};byte[] payload=JsonSerializer.SerializeToUtf8Bytes(next);using var rsa=RSA.Create();rsa.ImportFromPem(File.ReadAllText(Path.Combine(directory,"private-key.pem")));byte[] signature=rsa.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pss);string manifest=Path.Combine(directory,"site","manifest.json");File.WriteAllText(manifest+".partial",JsonSerializer.Serialize(new{payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(signature)}));File.Move(manifest+".partial",manifest,true);File.WriteAllBytes(payloadFile,payload);WriteSiteExtras(Path.Combine(directory,"site"));return next;
    }
}
