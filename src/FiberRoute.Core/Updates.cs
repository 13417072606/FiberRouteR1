using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
namespace FiberRoute.Core;
public sealed record UpdateConfig(string ManifestUrl,string PublicKeyPem);
public sealed record ReleaseItem(string Version,string Url,string Sha256,long Bytes);
public sealed record ReleaseManifest(ReleaseItem? Program,ReleaseItem? Resources);
public static class Updates
{
    static readonly HttpClient Client=new(new HttpClientHandler{AllowAutoRedirect=false}){Timeout=TimeSpan.FromMinutes(5)};
    const long MaxPackage=1024L*1024*1024;
    static Uri Trusted(string url,Uri origin) {var u=new Uri(url);if(u.Scheme!="https"||u.Host!=origin.Host||u.Port!=origin.Port||u.UserInfo.Length>0)throw new InvalidDataException("更新下载必须来自配置的同一HTTPS源，不允许重定向");return u;}
    public static async Task<ReleaseManifest> Check(UpdateConfig config,CancellationToken ct)
    {
        var origin=new Uri(config.ManifestUrl);Trusted(config.ManifestUrl,origin);
        using var response=await Client.GetAsync(origin,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
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
    public static async Task<string> Download(UpdateConfig config,ReleaseItem item,string root,CancellationToken ct)
    {
        if(item.Bytes<1||item.Bytes>MaxPackage||item.Sha256.Length!=64)throw new InvalidDataException("更新包元数据无效");
        var uri=Trusted(item.Url,new Uri(config.ManifestUrl));Directory.CreateDirectory(root);string path=Path.Combine(root,Guid.NewGuid()+".download");
        try
        {
            using var response=await Client.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct);response.EnsureSuccessStatusCode();
            await using(var input=await response.Content.ReadAsStreamAsync(ct))await using(var output=File.Create(path))
            {byte[] buffer=new byte[81920];long total=0;int n;while((n=await input.ReadAsync(buffer,ct))>0){total+=n;if(total>item.Bytes)throw new InvalidDataException("更新包超出声明大小");await output.WriteAsync(buffer.AsMemory(0,n),ct);}if(total!=item.Bytes)throw new InvalidDataException("更新包大小不符");}
            await using var verify=File.OpenRead(path);string hash=Convert.ToHexString(await SHA256.HashDataAsync(verify,ct));if(!hash.Equals(item.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包SHA256校验失败");return path;
        }catch{File.Delete(path);throw;}
    }
}
