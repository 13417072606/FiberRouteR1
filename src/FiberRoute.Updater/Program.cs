using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
if(args.Length!=5){Console.Error.WriteLine("此程序由光路调度路由查询调用，请勿单独启动。");return 2;}
string root=Path.GetFullPath(args[2]),zip=args[1],stage=Path.Combine(Path.GetTempPath(),"FiberRoute-update-"+Guid.NewGuid()),backupRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","补丁备份"),backup=Path.Combine(backupRoot,"R"+System.Reflection.AssemblyName.GetAssemblyName(Path.Combine(root,"程序","FiberRouteR1.dll")).Version!.ToString(3));
string rollback=Path.Combine(Path.GetTempPath(),"FiberRoute-rollback-"+Guid.NewGuid());
var replaced=new List<(string Destination,string? Backup)>();
string active=Path.Combine(AppContext.BaseDirectory,".active.pid");try{File.WriteAllText(active,Environment.ProcessId.ToString());}catch{}
try
{
    using(var stream=File.OpenRead(zip))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(args[4],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包二次校验失败");
    try{using var parent=Process.GetProcessById(int.Parse(args[0]));if(!parent.WaitForExit(60000))throw new TimeoutException("主程序未退出，已取消更新");}catch(ArgumentException){}
    FiberRoute.Updater.PackageValidator.Extract(zip,stage);
    string location=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","data-location.txt");
    string dataRoot=File.Exists(location)?File.ReadAllText(location).Trim():root;
    if(!Directory.Exists(dataRoot))throw new DirectoryNotFoundException("业务数据目录不存在："+dataRoot);
    foreach(string name in new[]{"resources.db","resources.db-wal","resources.db-shm","personal.db","personal.db-wal","personal.db-shm"})
    {
        string source=Path.Combine(dataRoot,name);if(!File.Exists(source))continue;
        string target=Path.Combine(backup,"业务数据",name);Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(source,target,true);
    }
    foreach(string source in Directory.GetFiles(stage,"*",SearchOption.AllDirectories))
    {
        string relative=Path.GetRelativePath(stage,source);if(relative=="更新清单.json")continue;string dest=Path.Combine(root,relative),old="";
        if(File.Exists(dest))
        {
            old=Path.Combine(rollback,relative);Directory.CreateDirectory(Path.GetDirectoryName(old)!);File.Copy(dest,old,true);
            if(!relative.EndsWith(".ico",StringComparison.OrdinalIgnoreCase)&&!relative.EndsWith("品牌原图.png",StringComparison.OrdinalIgnoreCase))
            {string retained=Path.Combine(backup,relative);Directory.CreateDirectory(Path.GetDirectoryName(retained)!);File.Copy(dest,retained,true);}
        }
        replaced.Add((dest,old.Length==0?null:old));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(source,dest,true);
    }
    string brand=Path.Combine(root,"程序","FiberRoute.Brand.exe");if(File.Exists(Path.Combine(root,"资源","品牌原图.png"))&&File.Exists(brand))
    {
        using var apply=Process.Start(new ProcessStartInfo(brand){UseShellExecute=false,Arguments="--apply-existing",WorkingDirectory=root})??throw new IOException("品牌图标组件未启动");if(!apply.WaitForExit(60000)){try{apply.Kill();apply.WaitForExit(2000);}catch{}throw new TimeoutException("品牌图标应用超时");}if(apply.ExitCode!=0)throw new IOException("原品牌图标恢复失败，程序升级已取消，请查看品牌替换失败.txt");
    }
    foreach(string obsolete in new[]{"管理方发布工具.exe","管理方发布工具.cmd"})try{File.Delete(Path.Combine(root,obsolete));}catch{}
    foreach(string obsolete in Directory.Exists(Path.Combine(root,"程序"))?Directory.GetFiles(Path.Combine(root,"程序"),"FiberRoute.Publisher.*").Concat(Directory.GetFiles(Path.Combine(root,"程序"),"FiberRoute.Admin.*")):[])try{File.Delete(obsolete);}catch{}
    foreach(string old in Directory.Exists(backupRoot)?Directory.GetDirectories(backupRoot):[])if(!old.Equals(backup,StringComparison.OrdinalIgnoreCase))try{Directory.Delete(old,true);}catch{}
    Process.Start(new ProcessStartInfo(args[3]){UseShellExecute=true});File.Delete(zip);return 0;
}
catch(Exception e)
{
    foreach(var item in replaced.AsEnumerable().Reverse())try{if(item.Backup!=null)File.Copy(item.Backup,item.Destination,true);else File.Delete(item.Destination);}catch{}
    string message="程序更新失败，已尝试恢复原版。\r\n"+e+"\r\n备份："+backup;
    try{File.WriteAllText(Path.Combine(root,"更新失败.txt"),message);if(File.Exists(args[3]))Process.Start(new ProcessStartInfo(args[3]){UseShellExecute=true});Process.Start(new ProcessStartInfo("notepad.exe",Path.Combine(root,"更新失败.txt")){UseShellExecute=true});}catch{Console.Error.WriteLine(message);}return 1;
}
finally{try{Directory.Delete(rollback,true);}catch{}try{Directory.Delete(stage,true);}catch{}try{File.Delete(active);}catch{}try{File.Delete(zip);}catch{}}
