using System.Runtime.InteropServices;
using System.Text;
namespace FiberRoute.App;
public static class BrandApplication
{
    const int IconType=3,GroupIconType=14;
    delegate bool NameCallback(IntPtr module,IntPtr type,IntPtr name,IntPtr param);
    delegate bool LanguageCallback(IntPtr module,IntPtr type,IntPtr name,ushort language,IntPtr param);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr LoadLibraryExW(string file,IntPtr handle,uint flags);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool FreeLibrary(IntPtr handle);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EnumResourceNamesW(IntPtr module,IntPtr type,NameCallback callback,IntPtr param);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool EnumResourceLanguagesW(IntPtr module,IntPtr type,IntPtr name,LanguageCallback callback,IntPtr param);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern IntPtr BeginUpdateResourceW(string file,bool deleteExisting);
    [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)]static extern bool UpdateResourceW(IntPtr handle,IntPtr type,IntPtr name,ushort language,byte[]? data,uint length);
    [DllImport("kernel32.dll",SetLastError=true)]static extern bool EndUpdateResourceW(IntPtr handle,bool discard);
    [DllImport("shell32.dll")]static extern void SHChangeNotify(uint eventId,uint flags,IntPtr item1,IntPtr item2);
    sealed record Resource(int Type,int Id,string? Name,ushort Language);
    static void Require(bool success,string action){if(!success)throw new IOException(action+"（Windows错误 "+Marshal.GetLastWin32Error()+"）");}
    static void PatchExecutable(string path,byte[] ico)
    {
        var resources=new List<Resource>();IntPtr module=LoadLibraryExW(path,IntPtr.Zero,2);if(module==IntPtr.Zero)throw new IOException("无法读取可执行文件资源："+path);
        try
        {
            foreach(int type in new[]{IconType,GroupIconType})
            {
                NameCallback names=(m,t,n,p)=>{bool numeric=(n.ToInt64()>>16)==0;int id=numeric?n.ToInt32():0;string? name=numeric?null:Marshal.PtrToStringUni(n);LanguageCallback languages=(mm,tt,nn,lang,pp)=>{resources.Add(new(type,id,name,lang));return true;};EnumResourceLanguagesW(m,t,n,languages,IntPtr.Zero);return true;};EnumResourceNamesW(module,(IntPtr)type,names,IntPtr.Zero);
            }
        }
        finally{FreeLibrary(module);}
        IntPtr update=BeginUpdateResourceW(path,false);if(update==IntPtr.Zero)throw new IOException("文件无法修改，请关闭主程序和管理方工具："+path);bool committed=false;
        try
        {
            foreach(var resource in resources){IntPtr name=resource.Name==null?(IntPtr)resource.Id:Marshal.StringToHGlobalUni(resource.Name);try{Require(UpdateResourceW(update,(IntPtr)resource.Type,name,resource.Language,null,0),"清理旧图标资源失败");}finally{if(resource.Name!=null)Marshal.FreeHGlobal(name);}}
            using var input=new BinaryReader(new MemoryStream(ico));if(input.ReadUInt16()!=0||input.ReadUInt16()!=1)throw new InvalidDataException("图标格式错误");int count=input.ReadUInt16();using var group=new MemoryStream();using var output=new BinaryWriter(group,Encoding.UTF8,true);output.Write((ushort)0);output.Write((ushort)1);output.Write((ushort)count);
            for(int i=0;i<count;i++){byte[] header=input.ReadBytes(12);uint offset=input.ReadUInt32();uint size=BitConverter.ToUInt32(header,8);if(offset+size>ico.Length)throw new InvalidDataException("图标资源范围错误");byte[] png=ico.AsSpan((int)offset,(int)size).ToArray();Require(UpdateResourceW(update,(IntPtr)IconType,(IntPtr)(i+1),0,png,size),"写入图标失败");output.Write(header);output.Write((ushort)(i+1));}
            byte[] bytes=group.ToArray();Require(UpdateResourceW(update,(IntPtr)GroupIconType,(IntPtr)1,0,bytes,(uint)bytes.Length),"写入图标组失败");Require(EndUpdateResourceW(update,false),"保存可执行文件图标失败");committed=true;
        }
        finally{if(!committed)EndUpdateResourceW(update,true);}
    }
    public static void Install(string sourcePng,string root)
    {
        root=Path.GetFullPath(root);string main=Path.Combine(root,"光路调度智能分析.exe"),publisher=Path.Combine(root,"管理方发布工具.exe");bool hasMain=File.Exists(main)&&File.Exists(Path.Combine(root,"程序","FiberRouteR1.dll")),hasPublisher=File.Exists(publisher)&&File.Exists(Path.Combine(root,"程序","FiberRoute.Publisher.dll"));if(!hasMain&&!hasPublisher)throw new InvalidDataException("请选择R1.4.1主程序或管理方工具的完整运行目录");if(hasMain&&new FileInfo(main).Length>5_000_000||!hasMain&&new FileInfo(publisher).Length>5_000_000)throw new InvalidDataException("请先安装R1.4.1修订补丁，更新启动器后再应用原图。");
        byte[] original=File.ReadAllBytes(sourcePng);if(original.Length<8||!original.AsSpan(0,8).SequenceEqual(new byte[]{137,80,78,71,13,10,26,10}))throw new InvalidDataException("请选择PNG原文件");using var input=new MemoryStream(original);using var image=Image.FromStream(input);if((long)image.Width*image.Height>32_000_000)throw new InvalidDataException("原图像素过大，请提供3200万像素以内的PNG");byte[] icon=BrandAssets.CreateIconBytes(image);
        string stage=Path.Combine(Path.GetTempPath(),"FiberRoute-brand-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(stage);var files=new Dictionary<string,string>(StringComparer.OrdinalIgnoreCase);var changes=new List<(string Path,string? Backup)>();
        try
        {
            void Add(string relative,byte[] bytes){string path=Path.Combine(stage,"new",relative);Directory.CreateDirectory(Path.GetDirectoryName(path)!);File.WriteAllBytes(path,bytes);files.Add(relative,path);}
            Add("资源/品牌原图.png",original);Add("资源/光路调度智能分析.ico",icon);Add("程序/app.ico",icon);
            foreach(string relative in new[]{"光路调度智能分析.exe","管理方发布工具.exe","更换品牌图标.exe","程序/FiberRouteR1.exe","程序/FiberRoute.Publisher.exe"})
            {
                string target=Path.Combine(root,relative);if(!File.Exists(target))continue;string copy=Path.Combine(stage,"new",relative);Directory.CreateDirectory(Path.GetDirectoryName(copy)!);File.Copy(target,copy);PatchExecutable(copy,icon);files.Add(relative,copy);
            }
            foreach(var pair in files)
            {
                string target=Path.Combine(root,pair.Key),backup=Path.Combine(stage,"old",pair.Key);if(File.Exists(target)){Directory.CreateDirectory(Path.GetDirectoryName(backup)!);File.Copy(target,backup);}else backup="";changes.Add((target,backup.Length==0?null:backup));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(pair.Value,target,true);
            }
            string saved=Path.Combine(root,"补丁备份","品牌图标");if(Directory.Exists(saved))Directory.Delete(saved,true);Directory.CreateDirectory(saved);string old=Path.Combine(stage,"old");if(Directory.Exists(old))foreach(string file in Directory.GetFiles(old,"*",SearchOption.AllDirectories)){string target=Path.Combine(saved,Path.GetRelativePath(old,file));Directory.CreateDirectory(Path.GetDirectoryName(target)!);File.Copy(file,target);}SHChangeNotify(0x08000000,0,IntPtr.Zero,IntPtr.Zero);
        }
        catch{foreach(var change in changes.AsEnumerable().Reverse())try{if(change.Backup!=null)File.Copy(change.Backup,change.Path,true);else File.Delete(change.Path);}catch{}throw;}
        finally{if(Directory.Exists(stage))Directory.Delete(stage,true);}
    }
}
