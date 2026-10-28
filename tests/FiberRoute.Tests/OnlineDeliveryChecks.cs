using FiberRoute.Core;
using System.IO.Compression;
using System.Net;
using System.Security.Cryptography;
using System.Text.Json;

static class OnlineDeliveryChecks
{
    public static async Task Run(string root,Action<bool,string> check)
    {
        string folder=Path.Combine(root,"online-delivery"),runtime=Path.Combine(folder,"runtime"),workspace=Path.Combine(folder,"workspace");Directory.CreateDirectory(Path.Combine(runtime,"程序"));Directory.CreateDirectory(Path.Combine(runtime,"资源"));
        using(var zip=ZipFile.OpenRead("downloads/R1.4.4/FiberRouteR1-R1.4.4-patch.zip"))File.WriteAllBytes(Path.Combine(runtime,"程序","FiberRouteR1.dll"),Read(zip.GetEntry("程序/FiberRouteR1.dll")!));
        File.WriteAllText(Path.Combine(runtime,"程序","FiberRouteR1.exe"),"entry-fixture");
        File.Copy(typeof(object).Assembly.Location,Path.Combine(runtime,"程序","System.Private.CoreLib.dll"));
        File.WriteAllText(Path.Combine(runtime,"资源","update-service.json"),"old customer configuration");File.WriteAllText(Path.Combine(runtime,"资源","update-service.json.before-online-setup"),"old public config backup");
        File.WriteAllText(Path.Combine(runtime,"程序","FiberRoute.Online.dll"),"auxiliary tool should not enter software package");
        using(var noise=File.Create(Path.Combine(runtime,"程序","noise.bin")))for(int i=0;i<26;i++)noise.Write(RandomNumberGenerator.GetBytes(1024*1024));
        Publishing.Initialize(workspace,"https://updates.example.test/");string key=Hash(Path.Combine(workspace,"private-key.pem"));
        string csv=Path.Combine(folder,"resources.csv");Csv.Write(csv,new object?[][]{["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量"],["a","GJ01","GF01",1,2,4]});var store=new Store(Path.Combine(runtime,"数据"));await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);
        var before=new[]{store.Database,store.Personal}.ToDictionary(p=>p,Hash);string signingFile=Path.Combine(runtime,"程序","private-key.pem");File.WriteAllText(signingFile,"sensitive fixture");bool privateBlocked=false;try{OnlineDelivery.Prepare(workspace,runtime,store.Root,"2026-10-28.blocked");}catch(InvalidDataException ex)when(ex.Message.Contains("私钥")){privateBlocked=true;}finally{File.Delete(signingFile);}check(privateBlocked&&!File.Exists(Path.Combine(workspace,"site","manifest.json")),"online preparation rejects real signing-key paths before publishing a software channel");
        string prepared=OnlineDelivery.Prepare(workspace,runtime,store.Root,"2026-10-28.210000");
        check(prepared.Contains("已生成 Cloudflare 上传 ZIP")&&before.All(p=>Hash(p.Key)==p.Value)&&Hash(Path.Combine(workspace,"private-key.pem"))==key,"online preparation reuses the signing key and preserves both administrator databases");
        string corePackage=Path.Combine(folder,"core-program.zip");Publishing.BuildProgramPackage(runtime,corePackage,"1.4.4");using(var coreZip=ZipFile.OpenRead(corePackage))check(coreZip.GetEntry("程序/System.Private.CoreLib.dll")!=null,"core publisher accepts the required System.Private.CoreLib runtime assembly");
        var config=Updates.LoadConfig(Path.Combine(workspace,"update-service.json"))!;var release=Updates.VerifyEnvelope(File.ReadAllBytes(Path.Combine(workspace,"site","manifest.json")),config.PublicKeyPem);
        check(release.Program?.Version=="1.4.4"&&release.Program.Parts?.Length==2&&release.Resources?.Version=="2026-10-28.210000","online preparation publishes both channels and automatically splits the real oversized software package");
        using(var upload=ZipFile.OpenRead(Path.Combine(workspace,"待上传-2026-10-28.210000.zip")))
        {
            var names=upload.Entries.Select(e=>e.FullName).ToArray();check(names.Contains("manifest.json")&&names.Contains("health.json")&&names.Any(n=>n.StartsWith("program/"))&&names.Any(n=>n.StartsWith("data/"))&&!names.Any(n=>n.StartsWith("site/")||n.Contains("private")||n.Contains("personal.db")),"Cloudflare upload ZIP preserves the root manifest and channel directories without keys, raw personal data or an extra site folder");
        }
        using(var software=ZipFile.OpenRead(Path.Combine(workspace,"packages","软件-1.4.4.zip")))
        {
            var names=software.Entries.Select(e=>e.FullName).ToArray();var shipped=JsonSerializer.Deserialize<UpdateConfig>(Read(software.GetEntry("资源/update-service.json")!));
            check(shipped==config&&names.Contains("程序/System.Private.CoreLib.dll")&&!names.Any(n=>n.Contains("before-online-setup")||n.Contains("FiberRoute.Online.")),"software package contains the matching public configuration and omits stale configuration backups and auxiliary tooling");
        }
        check(Updates.LoadConfig(Path.Combine(runtime,"资源","update-service.json"))==config&&File.ReadAllText(Path.Combine(runtime,"资源","update-service.json.before-online-setup"))=="old customer configuration","local client configuration is installed with the preceding configuration retained");
        using var http=new HttpClient(new Files(Path.Combine(workspace,"site")));string verified=await OnlineDelivery.Verify(workspace,transport:http);
        check(verified.Contains("软件 1.4.4")&&verified.Contains("数据 2026-10-28.210000")&&verified.Contains("逐片及整体SHA256"),"online verifier exercises the real client download, part merging, software extraction and data validation for both channels");
        var oldProgram=release.Program;OnlineDelivery.Prepare(workspace,runtime,store.Root,"2026-10-28.210001");var next=Updates.VerifyEnvelope(File.ReadAllBytes(Path.Combine(workspace,"site","manifest.json")),config.PublicKeyPem);
        check(JsonSerializer.Serialize(next.Program)==JsonSerializer.Serialize(oldProgram)&&next.Resources?.Version=="2026-10-28.210001"&&Hash(Path.Combine(workspace,"private-key.pem"))==key,"repeated setup advances data while preserving an already published software version and signing key");
        // A matching signed manifest alone is insufficient: an absent part must fail.
        string part=new Uri(next.Program!.Parts![0].Url).AbsolutePath.TrimStart('/');string partPath=Path.Combine(workspace,"site",part);File.Move(partPath,partPath+".saved");
        bool missing=false;try{await OnlineDelivery.Verify(workspace,transport:http);}catch(FileNotFoundException){missing=true;}finally{File.Move(partPath+".saved",partPath);}check(missing,"online verifier cannot report success when a referenced local part is missing");
        using var broken=new HttpClient(new Files(Path.Combine(workspace,"site"),missing:true));bool remoteMissing=false;try{await OnlineDelivery.Verify(workspace,transport:broken);}catch(HttpRequestException){remoteMissing=true;}check(remoteMissing,"online verifier rejects a missing remote update part even when the signed manifest matches");
        using var corrupt=new HttpClient(new Files(Path.Combine(workspace,"site"),corrupt:true));bool remoteCorrupt=false;try{await OnlineDelivery.Verify(workspace,transport:corrupt);}catch(InvalidDataException){remoteCorrupt=true;}check(remoteCorrupt,"online verifier rejects corrupt remote bytes using the same hashes as the client");
        string empty=Path.Combine(folder,"empty");Publishing.Initialize(empty,"https://updates.example.test/");Publishing.SaveConfiguration(empty,"https://updates.example.test/");bool emptyRejected=false;try{OnlineDelivery.Inspect(empty);}catch(InvalidDataException){emptyRejected=true;}check(emptyRejected,"empty signed channels are not presented as a ready update service");
    }
    static byte[] Read(ZipArchiveEntry entry){using var stream=entry.Open();using var buffer=new MemoryStream();stream.CopyTo(buffer);return buffer.ToArray();}
    static string Hash(string path){using var stream=File.OpenRead(path);return Convert.ToHexString(SHA256.HashData(stream));}
    sealed class Files(string root,bool missing=false,bool corrupt=false):HttpMessageHandler
    {
        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)
        {
            ct.ThrowIfCancellationRequested();string path=Path.Combine(root,Uri.UnescapeDataString(request.RequestUri!.AbsolutePath.TrimStart('/')));if(!File.Exists(path)||missing&&!path.EndsWith("manifest.json"))return Task.FromResult(new HttpResponseMessage(HttpStatusCode.NotFound));
            byte[] bytes=File.ReadAllBytes(path);if(corrupt&&!path.EndsWith("manifest.json")&&bytes.Length>0)bytes[0]^=1;return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new ByteArrayContent(bytes)});
        }
    }
}
