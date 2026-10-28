using FiberRoute.Core;
try
{
    switch(args)
    {
        case ["init",var directory,var url]:Publishing.Initialize(Path.GetFullPath(directory),url);Console.WriteLine("已初始化发布工作目录。只上传site目录；私钥不要上传或发给客户。client-config中的文件用于配置客户端。");break;
        case ["pack-data",var data,var version,var destination]:var store=new Store(Path.GetFullPath(data));store.BuildDataPackage(Path.GetFullPath(destination),version);Console.WriteLine("已生成数据包，不包含管理方personal.db及原始Excel。");break;
        case ["pack-program",var root,var version,var destination]:Publishing.BuildProgramPackage(Path.GetFullPath(root),Path.GetFullPath(destination),version);Console.WriteLine("已生成软件更新包。");break;
        case ["release",var directory,var channel,var version,var package,..var remainder]:Publishing.Release(Path.GetFullPath(directory),channel,version,Path.GetFullPath(package),string.Join(' ',remainder));Console.WriteLine("已生成site目录。先上传版本包并验证，最后上传manifest.json，客户即可检测到更新。");break;
        default:Console.WriteLine("光路调度路由查询 R1.4.4 管理方发布工具\n\ninit <工作目录> <HTTPS发布目录URL>\npack-data <管理方数据目录> <数据版本> <输出ZIP>\npack-program <新版本完整运行目录> <软件版本如1.4.4> <输出ZIP>\nrelease <工作目录> <software|data> <版本> <ZIP> [更新说明]\n\n客户端使用工作目录client-config/update-service.json，复制到原运行目录的资源文件夹。服务器只上传site目录，先包后清单。数据包由管理方已校验数据生成，客户人工值仍会保留。");break;
    }
    return 0;
}
catch(Exception ex){Console.Error.WriteLine("发布未完成："+ex.Message);return 1;}
