using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.IO.Compression;
namespace FiberRoute.Core;
public static partial class PublishingDelivery
{
    public sealed record TransferProgress(string Name,long Bytes,long Total);
    public static string BuildClientActivationPackage(string workspace,string destination)
    {
        string source=Path.Combine(workspace,"client-config","update-service.json");
        var config=Updates.LoadConfig(source)??throw new InvalidDataException("请先在第 2 步保存更新服务配置");
        if(Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))!=config)throw new InvalidDataException("管理方与客户配置不一致，请先在第 2 步重新保存服务配置");
        string target=Path.GetFullPath(destination),temporary=target+".partial";
        if(File.Exists(target))throw new IOException("客户启用包已存在，请选择新的文件名");
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))
            {
                zip.CreateEntryFromFile(source,"update-service.json",CompressionLevel.Optimal);
                Write("Install.cmd","@echo off\r\npowershell.exe -NoProfile -ExecutionPolicy Bypass -STA -File \"%~dp0Install-UpdateConfig.ps1\"\r\nif errorlevel 1 pause\r\n",Encoding.ASCII);
                Write("Install-UpdateConfig.ps1","""
                    $ErrorActionPreference = 'Stop'
                    Add-Type -AssemblyName System.Windows.Forms
                    $pick = New-Object System.Windows.Forms.FolderBrowserDialog
                    $pick.Description = '选择客户端根目录（含光路调度路由查询.exe 和 程序 文件夹）'
                    if ($pick.ShowDialog() -ne [System.Windows.Forms.DialogResult]::OK) { exit 1 }
                    $root = $pick.SelectedPath
                    if (-not (Test-Path -LiteralPath (Join-Path $root '光路调度路由查询.exe')) -or -not (Test-Path -LiteralPath (Join-Path $root '程序\FiberRouteR1.dll'))) {
                        [System.Windows.Forms.MessageBox]::Show('所选目录不是客户端根目录。请选择包含主程序和“程序”文件夹的目录。','在线更新配置') | Out-Null
                        exit 1
                    }
                    $source = Join-Path $PSScriptRoot 'update-service.json'
                    $targetDir = Join-Path $root '资源'
                    New-Item -ItemType Directory -Path $targetDir -Force | Out-Null
                    Copy-Item -LiteralPath $source -Destination (Join-Path $targetDir 'update-service.json') -Force
                    [System.Windows.Forms.MessageBox]::Show('在线更新已配置。重新打开客户端后，可在“设置”中检测和下载更新。','在线更新配置') | Out-Null
                    """,new UTF8Encoding(true));
                Write("使用说明.txt","此包只含公开更新地址和验签公钥，不含管理方私钥或业务数据。\r\n完整解压后双击 Install.cmd，选择本机客户端根目录（包含光路调度路由查询.exe 和 程序 文件夹）。\r\n每台电脑只需配置一次；之后管理方发布到 Cloudflare，用户可在各自客户端的“设置”中检测和下载。\r\n数据更新下载后仍需预览并确认应用。\r\n",new UTF8Encoding(true));
                void Write(string name,string content,Encoding encoding){using var writer=new StreamWriter(zip.CreateEntry(name,CompressionLevel.Optimal).Open(),encoding);writer.Write(content);}
            }
            File.Move(temporary,target);return target;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
    }
    public static string[] ActiveFiles(string workspace)
    {
        var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))??throw new InvalidDataException("请先设置真实HTTPS发布地址并初始化工作目录");
        string site=Path.Combine(Path.GetFullPath(workspace),"site"),manifest=Path.Combine(site,"manifest.json");var release=Updates.VerifyEnvelope(File.ReadAllBytes(manifest),config.PublicKeyPem);var origin=new Uri(config.ManifestUrl);var baseUri=new Uri(origin,".");var result=new List<string>();
        foreach(var item in new[]{release.Program,release.Resources}.Where(x=>x!=null))
        {
            foreach(var part in Updates.DownloadParts(item!))
            {
                var uri=new Uri(part.Url);if(uri.Scheme!="https"||uri.Authority!=baseUri.Authority||!uri.AbsolutePath.StartsWith(baseUri.AbsolutePath,StringComparison.Ordinal)||uri.Query.Length>0||uri.Fragment.Length>0)throw new InvalidDataException("版本包地址不属于配置的发布目录");
                string relative=Uri.UnescapeDataString(baseUri.MakeRelativeUri(uri).ToString());if(relative.Split('/').Any(x=>x is "." or ".." or "")||!System.Text.RegularExpressions.Regex.IsMatch(relative,@"\.zip(?:\.\d{3})?$"))throw new InvalidDataException("版本包路径不合法");
                string path=Path.GetFullPath(Path.Combine(site,relative.Replace('/',Path.DirectorySeparatorChar)));if(!path.StartsWith(site+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)||!File.Exists(path))throw new FileNotFoundException("清单引用的更新文件不存在："+relative);
                using var f=File.OpenRead(path);if(f.Length!=part.Bytes||!Convert.ToHexString(SHA256.HashData(f)).Equals(part.Sha256,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("待发布更新文件校验失败："+relative);result.Add(relative);
            }
        }
        foreach(string name in new[]{"health.json","index.html","_headers"})if(File.Exists(Path.Combine(site,name)))result.Add(name);result.Add("manifest.json");return result.ToArray();
    }
    public static string BuildCloudflareUpload(string workspace,string destination)
    {
        string site=Path.Combine(Path.GetFullPath(workspace),"site"),target=Path.GetFullPath(destination),temporary=target+".partial";
        if(target.StartsWith(site+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("上传包不能放在公开 site 目录内");
        if(File.Exists(target))throw new IOException("同名上传包已存在，请使用新的文件名");
        string[] active=ActiveFiles(workspace);
        Directory.CreateDirectory(Path.GetDirectoryName(target)!);
        try
        {
            using(var zip=ZipFile.Open(temporary,ZipArchiveMode.Create))foreach(string relative in active)
                zip.CreateEntryFromFile(Path.Combine(site,relative),relative.Replace('\\','/'),CompressionLevel.NoCompression);
            File.Move(temporary,target);
            return target;
        }
        finally{if(File.Exists(temporary))File.Delete(temporary);}
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
