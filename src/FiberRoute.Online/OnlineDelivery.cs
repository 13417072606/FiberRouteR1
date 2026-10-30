using FiberRoute.Core;
using FiberRoute.Updater;
using System.IO.Compression;
using System.Reflection;
using System.Text.Json;

public static class OnlineDelivery
{
    public static string Inspect(string workspace)
    {
        var config=Configuration(workspace);string manifest=Path.Combine(workspace,"site","manifest.json");
        if(!File.Exists(manifest))return "管理目录已有客户端配置；尚未生成签名发布清单。\n清单地址："+config.ManifestUrl;
        var release=Updates.VerifyEnvelope(File.ReadAllBytes(manifest),config.PublicKeyPem);var files=PublishingDelivery.ActiveFiles(workspace);
        if(release.Program==null&&release.Resources==null)throw new InvalidDataException("签名清单尚无软件或数据版本，请先生成正式更新包。");
        return $"清单地址：{config.ManifestUrl}\n软件版本：{release.Program?.Version??"未发布"}\n数据版本：{release.Resources?.Version??"未发布"}\n已校验本机待发布文件：{files.Length} 个。";
    }
    static UpdateConfig Configuration(string workspace)
    {
        var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))??throw new InvalidDataException("原管理工作目录缺少有效 update-service.json。请在原发布工具第2步保存服务配置，不要另建签名密钥。");
        var client=Updates.LoadConfig(Path.Combine(workspace,"client-config","update-service.json"))??throw new InvalidDataException("原管理目录缺少客户公钥配置，请在发布工具第2步保存服务配置。");
        if(config!=client)throw new InvalidDataException("管理方与客户配置不一致，请在原发布工具第2步重新保存服务配置。");
        return config;
    }
    public static string Prepare(string workspace,string runtime,string data,string dataVersion,CancellationToken ct=default)
    {
        if(!System.Text.RegularExpressions.Regex.IsMatch(dataVersion,@"^[0-9A-Za-z][0-9A-Za-z._-]{0,79}$"))throw new ArgumentException("数据版本号只能包含字母、数字、点、下划线和短横线。");
        workspace=Path.GetFullPath(workspace);runtime=Path.GetFullPath(runtime);data=Path.GetFullPath(data);var config=Configuration(workspace);
        if(!File.Exists(Path.Combine(workspace,"private-key.pem")))throw new InvalidDataException("原管理工作目录缺少签名私钥。请找回原工作目录，不会自动创建另一套密钥。");
        string assembly=Path.Combine(runtime,"程序","FiberRouteR1.dll");var version=AssemblyName.GetAssemblyName(assembly).Version!;
        if(version<new Version(1,4,3))throw new InvalidDataException("请先将管理员和客户程序手动升级至 R1.4.3 或更高版本，之后才支持分片在线更新。");
        if(!File.Exists(Path.Combine(data,"resources.db"))||!File.Exists(Path.Combine(data,"personal.db")))throw new InvalidDataException("请选择主程序设置中显示的实际数据目录，须包含两个数据库。");
        // Check the current signed channels before writing a new release.
        string current=Path.Combine(workspace,"site","manifest.json");ReleaseManifest? previous=null;
        if(File.Exists(current)){previous=Updates.VerifyEnvelope(File.ReadAllBytes(current),config.PublicKeyPem);_=PublishingDelivery.ActiveFiles(workspace);}
        string packages=Path.Combine(workspace,"packages");Directory.CreateDirectory(packages);
        string programVersion=version.ToString(3);bool createProgram=previous?.Program==null||Updates.IsNewProgram(programVersion,previous.Program.Version);
        string dataPackage=Path.Combine(packages,"数据-"+dataVersion+".zip");if(File.Exists(dataPackage))throw new IOException("本次数据版本已存在，请使用新的版本号。");
        var store=new Store(data);if(store.Graph.Edges.Length==0)throw new InvalidDataException("所选资源库没有中继段，不会发布空数据覆盖客户资源。请核对主程序当前数据目录。");
        Console.WriteLine($"管理方数据：{store.Graph.Edges.Length:N0} 条中继段、{store.Graph.Nodes.Length:N0} 个节点；来源：{data}");
        Console.WriteLine("正在从管理方数据库生成标准数据更新包…");ct.ThrowIfCancellationRequested();store.BuildDataPackage(dataPackage,dataVersion,ct);
        if(createProgram)
        {
            string software=Path.Combine(packages,"软件-"+programVersion+".zip");if(File.Exists(software))throw new IOException("同版本软件包已存在但尚未完成发布，请在发布工具中选择已有包生成清单，不会覆盖。");
            Console.WriteLine("正在生成软件更新包 R"+programVersion+"…");BuildSoftware(workspace,runtime,software,programVersion,ct);ct.ThrowIfCancellationRequested();Publishing.Release(workspace,"software",programVersion,software,"光路调度路由查询 R"+programVersion);
        }
        ct.ThrowIfCancellationRequested();Publishing.Release(workspace,"data",dataVersion,dataPackage,"同步管理方已整理的资源与设施归属");
        string[] active=PublishingDelivery.ActiveFiles(workspace);string output=Path.Combine(workspace,"待上传-"+dataVersion+".zip"),temporary=output+".partial";
        try
        {
            using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))foreach(string relative in active){ct.ThrowIfCancellationRequested();zip.CreateEntryFromFile(Path.Combine(workspace,"site",relative),relative.Replace('\\','/'),CompressionLevel.NoCompression);}
            File.Move(temporary,output,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
        // Public configuration only; the signing key stays in the existing workspace.
        string resources=Path.Combine(runtime,"资源");Directory.CreateDirectory(resources);string target=Path.Combine(resources,"update-service.json");
        if(File.Exists(target)&&File.ReadAllText(target)!=File.ReadAllText(Path.Combine(workspace,"client-config","update-service.json")))File.Copy(target,target+".before-online-setup",true);
        File.Copy(Path.Combine(workspace,"client-config","update-service.json"),target,true);
        return Inspect(workspace)+"\n已生成 Cloudflare 上传 ZIP："+output+"\n请在现有 Pages 项目创建生产部署并上传此 ZIP；ZIP 根目录直接包含 manifest.json 和 data/program，不多套 site 文件夹。\n本机已安装客户公钥配置。上传后运行 verify，才会验证完整线上下载。";
    }
    static void BuildSoftware(string workspace,string runtime,string software,string version,CancellationToken ct)
    {
        string stage=Path.Combine(Path.GetTempPath(),"FiberRoute-program-source-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);
        try
        {
            foreach(string directory in new[]{"程序"})
            {
                string source=Path.Combine(runtime,directory);if(!Directory.Exists(source))continue;
                foreach(string file in Directory.EnumerateFiles(source,"*",SearchOption.AllDirectories))
                {
                    ct.ThrowIfCancellationRequested();string relative=Path.GetRelativePath(runtime,file);
                    if(SigningKeyFile(file))throw new InvalidDataException("软件目录中发现私钥文件，拒绝发布："+Path.GetFileName(file));
                    if(Path.GetFileName(file).StartsWith("FiberRoute.Online.",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(file).StartsWith("FiberRoute.Publisher.",StringComparison.OrdinalIgnoreCase)||Path.GetFileName(file).StartsWith("FiberRoute.Admin.",StringComparison.OrdinalIgnoreCase)||relative.Replace('\\','/')=="资源/update-service.json"||file.EndsWith(".before-online-setup",StringComparison.OrdinalIgnoreCase))continue;
                    string name=Path.GetFileName(file);if(!new[]{".dll",".exe",".ico"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase)&&!name.EndsWith(".deps.json",StringComparison.OrdinalIgnoreCase)&&!name.EndsWith(".runtimeconfig.json",StringComparison.OrdinalIgnoreCase))continue;
                    string destination=Path.Combine(stage,relative);Directory.CreateDirectory(Path.GetDirectoryName(destination)!);File.Copy(file,destination);
                }
            }
            foreach(string name in new[]{"光路调度路由查询.exe"})if(File.Exists(Path.Combine(runtime,name)))File.Copy(Path.Combine(runtime,name),Path.Combine(stage,name));
            Directory.CreateDirectory(Path.Combine(stage,"资源"));File.Copy(Path.Combine(workspace,"client-config","update-service.json"),Path.Combine(stage,"资源","update-service.json"));
            WriteSoftwarePackage(stage,software,version);
        }
        finally{Directory.Delete(stage,true);}
    }
    static bool SigningKeyFile(string path)
    {
        string name=Path.GetFileName(path);
        // These are required Microsoft runtime assemblies, not signing material.
        string[] runtime=["System.Private.CoreLib.dll","System.Private.Uri.dll","System.Private.Xml.dll","System.Private.Xml.Linq.dll","System.Private.DataContractSerialization.dll"];
        return name.Contains("私钥",StringComparison.Ordinal)||name.Contains("private",StringComparison.OrdinalIgnoreCase)&&!runtime.Contains(name,StringComparer.OrdinalIgnoreCase)||new[]{".pfx",".p12",".snk"}.Contains(Path.GetExtension(name),StringComparer.OrdinalIgnoreCase);
    }
    static void WriteSoftwarePackage(string source,string destination,string version)
    {
        var files=Directory.GetFiles(source,"*",SearchOption.AllDirectories).Select(p=>(Path:Path.GetRelativePath(source,p).Replace('\\','/'),File:p)).Where(p=>!p.Path.EndsWith(".pdb",StringComparison.OrdinalIgnoreCase)&&!p.Path.Contains(".partial",StringComparison.OrdinalIgnoreCase)).OrderBy(p=>p.Path,StringComparer.Ordinal).ToArray();
        string Hash(string file){using var stream=File.OpenRead(file);return Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(stream));}
        var manifest=new{Version=version,Files=files.Select(f=>new{f.Path,Bytes=new FileInfo(f.File).Length,Sha256=Hash(f.File)}).ToArray()};
        string temporary=destination+".partial";
        try
        {
            using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                foreach(var file in files)zip.CreateEntryFromFile(file.File,file.Path,CompressionLevel.Optimal);
                using var writer=new StreamWriter(zip.CreateEntry("更新清单.json").Open());writer.Write(JsonSerializer.Serialize(manifest));
            }
            File.Move(temporary,destination,false);
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static async Task<string> Verify(string workspace,CancellationToken ct=default,HttpClient? transport=null)
    {
        _=Inspect(workspace);var config=Configuration(workspace);var release=await PublishingDelivery.VerifyRemote(workspace,ct,transport);
        if(release.Program==null&&release.Resources==null)throw new InvalidDataException("线上清单没有任何可更新版本。");
        string stage=Path.Combine(Path.GetTempPath(),"FiberRoute-online-verify-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var results=new List<string>();
        try
        {
            foreach(var (channel,item) in new[]{("软件",release.Program),("数据",release.Resources)})
            {
                if(item==null)continue;Console.WriteLine($"正在完整验证{channel} {item.Version}，{item.Bytes/1024d/1024d:0.##} MiB…");
                string downloaded=await Updates.Download(config,item,stage,ct,transport:transport);
                try
                {
                    if(channel=="软件")PackageValidator.Extract(downloaded,Path.Combine(stage,"program"));
                    else{var client=new Store(Path.Combine(stage,"preview-client"));using var preview=client.PrepareDataPackage(downloaded,item.Version,ct);}
                    results.Add($"{channel} {item.Version}：清单签名、{Updates.DownloadParts(item).Length} 个下载文件、逐片及整体SHA256、包内文件/数据库校验通过。");
                }
                finally{File.Delete(downloaded);}
            }
            return string.Join('\n',results)+"\n线上文件验证通过。尚未修改客户数据库或替换客户程序；最后请在一台客户机设置页完成下载、确认应用/重启验收。";
        }
        finally{Directory.Delete(stage,true);}
    }
}
