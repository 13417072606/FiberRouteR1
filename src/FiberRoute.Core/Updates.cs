using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
namespace FiberRoute.Core;
public sealed record UpdateConfig(string ManifestUrl,string PublicKeyPem);
public sealed record ReleaseItem(string Version,string Url,string Sha256,long Bytes,string Notes="",string MinimumProgram="1.4.0",ReleasePart[]? Parts=null);
public sealed record ReleasePart(string Url,string Sha256,long Bytes);
public sealed record DownloadProgress(long Received,long Total);
public sealed record ReleaseManifest(ReleaseItem? Program,ReleaseItem? Resources);
public static class Updates
{
    static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(5)};
    const long MaxPackage=1024L*1024*1024;
    static Uri Trusted(string url,Uri origin) {var u=new Uri(url);if(u.Scheme!="https"||u.Host!=origin.Host||u.Port!=origin.Port||u.UserInfo.Length>0)throw new InvalidDataException("更新下载必须来自配置的同一HTTPS源，不允许重定向");return u;}
    public static async Task<ReleaseManifest> Check(UpdateConfig config,CancellationToken ct,HttpClient? transport=null)
    {
        var origin=new Uri(config.ManifestUrl);Trusted(config.ManifestUrl,origin);
        using var response=await (transport??Client).GetAsync(origin,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
        byte[] bytes=await ReadBounded(response,1024*1024,ct);return VerifyEnvelope(bytes,config.PublicKeyPem);
    }
    public static ReleaseManifest VerifyEnvelope(byte[] bytes,string publicKeyPem)
    {
        using var json=JsonDocument.Parse(bytes);
        byte[] payload=Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString()!),signature=Convert.FromBase64String(json.RootElement.GetProperty("signature").GetString()!);
        using var rsa=RSA.Create();rsa.ImportFromPem(publicKeyPem);if(!rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pss))throw new InvalidDataException("更新清单签名无效");
        return JsonSerializer.Deserialize<ReleaseManifest>(payload)??throw new InvalidDataException("更新清单为空");
    }
    static async Task<byte[]> ReadBounded(HttpResponseMessage response,int max,CancellationToken ct)
    {using var s=await response.Content.ReadAsStreamAsync(ct);using var m=new MemoryStream();byte[] b=new byte[81920];int n;while((n=await s.ReadAsync(b,ct))>0){if(m.Length+n>max)throw new InvalidDataException("更新清单过大");m.Write(b,0,n);}return m.ToArray();}
    public static async Task<string> Download(UpdateConfig config,ReleaseItem item,string root,CancellationToken ct,IProgress<DownloadProgress>? progress=null,HttpClient? transport=null)
    {
        if(item.Bytes<1||item.Bytes>MaxPackage||item.Sha256.Length!=64)throw new InvalidDataException("更新包元数据无效");
        var origin=new Uri(config.ManifestUrl);Trusted(item.Url,origin);
        var parts=DownloadParts(item);foreach(var part in parts)Trusted(part.Url,origin);
        Directory.CreateDirectory(root);string path=Path.Combine(root,Guid.NewGuid()+".download");
        try
        {
            long total=0;await using(var output=File.Create(path))
            foreach(var part in parts)
            {
                using var response=await (transport??Client).GetAsync(Trusted(part.Url,origin),HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
                await using var input=await response.Content.ReadAsStreamAsync(ct);using var hash=IncrementalHash.CreateHash(HashAlgorithmName.SHA256);byte[] buffer=new byte[81920];long received=0;int n;
                while((n=await input.ReadAsync(buffer,ct))>0){received+=n;total+=n;if(received>part.Bytes||total>item.Bytes)throw new InvalidDataException("更新分片超出声明大小");hash.AppendData(buffer,0,n);await output.WriteAsync(buffer.AsMemory(0,n),ct);progress?.Report(new(total,item.Bytes));}
                if(received!=part.Bytes||!Convert.ToHexString(hash.GetHashAndReset()).Equals(part.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新分片大小或SHA256校验失败");
            }
            await using var verify=File.OpenRead(path);string digest=Convert.ToHexString(await SHA256.HashDataAsync(verify,ct));if(!digest.Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("合并更新包SHA256校验失败");return path;
        }catch{File.Delete(path);throw;}
    }
    public static ReleasePart[] DownloadParts(ReleaseItem item)
    {
        bool Hash(string h)=>h.Length==64&&h.All(Uri.IsHexDigit);
        if(item.Bytes<1||item.Bytes>MaxPackage||!Hash(item.Sha256))throw new InvalidDataException("更新包元数据无效");
        if(item.Parts==null)return [new(item.Url,item.Sha256,item.Bytes)];
        if(item.Parts.Length<2||item.Parts.Length>100||item.Parts.Any(p=>p.Bytes<1||p.Bytes>24L*1024*1024||!Hash(p.Sha256))||item.Parts.Sum(p=>p.Bytes)!=item.Bytes||item.Parts.Select(p=>p.Url).Distinct().Count()!=item.Parts.Length)throw new InvalidDataException("更新分片清单无效");
        return item.Parts;
    }

    public static UpdateConfig? LoadConfig(string path)
    {
        if(!File.Exists(path))return null;if(new FileInfo(path).Length>128*1024)throw new InvalidDataException("更新服务配置过大");var config=JsonSerializer.Deserialize<UpdateConfig>(File.ReadAllText(path),new JsonSerializerOptions{PropertyNameCaseInsensitive=true});
        if(config==null||string.IsNullOrWhiteSpace(config.ManifestUrl)||string.IsNullOrWhiteSpace(config.PublicKeyPem))return null;Trusted(config.ManifestUrl,new Uri(config.ManifestUrl));using var rsa=RSA.Create();rsa.ImportFromPem(config.PublicKeyPem);return config;
    }
    public static bool IsNewProgram(string available,string current)=>Version.TryParse(available.TrimStart('R','r'),out var next)&&Version.TryParse(current.TrimStart('R','r'),out var old)&&next>old;
    public static bool Compatible(ReleaseItem item,string current)=>Version.TryParse(item.MinimumProgram,out var minimum)&&Version.TryParse(current,out var version)&&version>=minimum;
}
