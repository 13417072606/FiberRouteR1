using System.Diagnostics;
using System.IO.Compression;
using System.Security.Cryptography;
if(args.Length!=5){Console.Error.WriteLine("此程序由纤路 R1 调用，请勿单独启动。");return 2;}
string root=Path.GetFullPath(args[2]),zip=args[1],stage=Path.Combine(Path.GetTempPath(),"FiberRoute-update-"+Guid.NewGuid()),backup=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"FiberRouteR1","program-backup-"+DateTime.Now.ToString("yyyyMMddHHmmss"));
var replaced=new List<(string Destination,string? Backup)>();
try
{
    using(var stream=File.OpenRead(zip))if(!Convert.ToHexString(SHA256.HashData(stream)).Equals(args[4],StringComparison.OrdinalIgnoreCase))throw new InvalidDataException("更新包二次校验失败");
    try{using var parent=Process.GetProcessById(int.Parse(args[0]));if(!parent.WaitForExit(60000))throw new TimeoutException("主程序未退出，已取消更新");}catch(ArgumentException){}
    FiberRoute.Updater.PackageValidator.Extract(zip,stage);
    foreach(string source in Directory.GetFiles(stage,"*",SearchOption.AllDirectories))
    {
        string relative=Path.GetRelativePath(stage,source),dest=Path.Combine(root,relative),old=Path.Combine(backup,relative);
        if(File.Exists(dest)){Directory.CreateDirectory(Path.GetDirectoryName(old)!);File.Copy(dest,old,true);}else old="";
        replaced.Add((dest,old.Length==0?null:old));Directory.CreateDirectory(Path.GetDirectoryName(dest)!);File.Copy(source,dest,true);
    }
    Process.Start(new ProcessStartInfo(Path.Combine(root,"纤路R1.exe")){UseShellExecute=true});File.Delete(zip);return 0;
}
catch(Exception e)
{
    foreach(var item in replaced.AsEnumerable().Reverse())try{if(item.Backup!=null)File.Copy(item.Backup,item.Destination,true);else File.Delete(item.Destination);}catch{}
    string message="程序更新失败，已尝试恢复原版。\r\n"+e+"\r\n备份："+backup;
    try{File.WriteAllText(Path.Combine(root,"更新失败.txt"),message);Process.Start(new ProcessStartInfo("notepad.exe",Path.Combine(root,"更新失败.txt")){UseShellExecute=true});}catch{Console.Error.WriteLine(message);}return 1;
}
finally{try{Directory.Delete(stage,true);}catch{}}
