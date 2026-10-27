using System.Security.Cryptography;
using System.Text.Json;
namespace FiberRoute.Core;
public static partial class PublishingDelivery
{
    public sealed record TransferProgress(string Name,long Bytes,long Total);
    public static string[] ActiveFiles(string workspace)
    {
        var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))??throw new InvalidDataException("请先设置真实HTTPS发布地址并初始化工作目录");
        string site=Path.Combine(Path.GetFullPath(workspace),"site"),manifest=Path.Combine(site,"manifest.json");var release=Updates.VerifyEnvelope(File.ReadAllBytes(manifest),config.PublicKeyPem);var origin=new Uri(config.ManifestUrl);var baseUri=new Uri(origin,".");var result=new List<string>();
        foreach(var item in new[]{release.Program,release.Resources}.Where(x=>x!=null))
        {
            var uri=new Uri(item!.Url);if(uri.Scheme!="https"||uri.Authority!=baseUri.Authority||!uri.AbsolutePath.StartsWith(baseUri.AbsolutePath,StringComparison.Ordinal)||uri.Query.Length>0||uri.Fragment.Length>0)throw new InvalidDataException("版本包地址不属于配置的发布目录");
            string relative=Uri.UnescapeDataString(baseUri.MakeRelativeUri(uri).ToString());if(relative.Split('/').Any(x=>x is "." or ".." or "")||!relative.EndsWith(".zip",StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("版本包路径不合法");
            string path=Path.GetFullPath(Path.Combine(site,relative.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(site+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new FileNotFoundException("清单引用的更新包不存在："+relative);
            using var f=File.OpenRead(path);if(f.Length!=item.Bytes||!Convert.ToHexString(SHA256.HashData(f)).Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("待发布更新包校验失败："+relative);result.Add(relative);
        }
        result.Add("manifest.json");return result.ToArray();
    }
    public static async Task PublishDirectory(string workspace,string destination,IProgress<TransferProgress>? progress=null,CancellationToken ct=default)
    {
        string site=Path.Combine(Path.GetFullPath(workspace),"site"),target=Path.GetFullPath(destination);if(target.Equals(site,StringComparison.OrdinalIgnoreCase)||target.StartsWith(Path.GetFullPath(workspace)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||Path.GetFullPath(workspace).StartsWith(target+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("服务器发布目录必须与管理工作目录分开");
        string[] files=ActiveFiles(workspace);Directory.CreateDirectory(target);
        foreach(string relative in files)
        {
            ct.ThrowIfCancellationRequested();string source=Path.Combine(site,relative),dest=Path.Combine(target,relative),partial=dest+".upload-"+Guid.NewGuid().ToString("N");Directory.CreateDirectory(Path.GetDirectoryName(dest)!);
            try
            {
                if(File.Exists(dest)&&relative!="manifest.json"){using var previous=File.OpenRead(dest);using var current=File.OpenRead(source);if(previous.Length==current.Length&&SHA256.HashData(previous).SequenceEqual(SHA256.HashData(current))){progress?.Report(new(relative,current.Length,current.Length));continue;}}
                await using(var input=File.OpenRead(source))await using(var output=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,FileOptions.Asynchronous))
                {byte[] buffer=new byte[81920];long sent=0;int read;while((read=await input.ReadAsync(buffer,ct))>0){await output.WriteAsync(buffer.AsMemory(0,read),ct);sent+=read;progress?.Report(new(relative,sent,input.Length));}await output.FlushAsync(ct);}
                using(var original=File.OpenRead(source))using(var copied=File.OpenRead(partial))if(original.Length!=copied.Length||!SHA256.HashData(original).SequenceEqual(SHA256.HashData(copied)))throw new InvalidDataException("服务器目录写入校验失败："+relative);
                ct.ThrowIfCancellationRequested();File.Move(partial,dest,true);
            }
            finally{if(File.Exists(partial))File.Delete(partial);}
        }
    }
    public static async Task<ReleaseManifest> VerifyRemote(string workspace,CancellationToken ct=default,HttpClient? transport=null)
    {
        var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))??throw new InvalidDataException("服务配置未完成");var local=Updates.VerifyEnvelope(File.ReadAllBytes(Path.Combine(workspace,"site","manifest.json")),config.PublicKeyPem);var remote=await Updates.Check(config,ct,transport);if(JsonSerializer.Serialize(local)!=JsonSerializer.Serialize(remote))throw new InvalidDataException("网站清单与本次待发布清单不一致，请检查发布目录和网站缓存");return remote;
    }
}
