using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
namespace FiberRoute.App;

internal static class BrandAssets
{
    // A supplied original can replace the brand without redrawing or stretching it.
    public static string OriginalPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","品牌原图.png"));
    public static string IconPath=>Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","光路调度智能分析.ico"));
    public static (Image? Image,Icon? Icon) Load()
    {
        if(!File.Exists(OriginalPath))return (null,null);
        using var source=Image.FromFile(OriginalPath);var image=new Bitmap(source);
        byte[] bytes=CreateIconBytes(source);
try{Directory.CreateDirectory(Path.GetDirectoryName(IconPath)!);if(!File.Exists(IconPath)||!File.ReadAllBytes(IconPath).SequenceEqual(bytes))File.WriteAllBytes(IconPath,bytes);}catch(IOException){}catch(UnauthorizedAccessException){}
        using var stream=new MemoryStream(bytes);using var icon=new Icon(stream);return (image,(Icon)icon.Clone());
    }
    internal static byte[] CreateIconBytes(Image source)
    {
        var payloads=new List<(int Size,byte[] Data)>();
        foreach(int size in new[]{16,24,32,48,64,128,256})
        {
            using var bmp=new Bitmap(size,size,PixelFormat.Format32bppArgb);using(var g=Graphics.FromImage(bmp))
            {
                g.Clear(Color.Transparent);g.CompositingQuality=CompositingQuality.HighQuality;g.InterpolationMode=InterpolationMode.HighQualityBicubic;g.PixelOffsetMode=PixelOffsetMode.HighQuality;
                float scale=Math.Min(size/(float)source.Width,size/(float)source.Height);float width=source.Width*scale,height=source.Height*scale;g.DrawImage(source,(size-width)/2,(size-height)/2,width,height);
            }
            using var png=new MemoryStream();bmp.Save(png,ImageFormat.Png);payloads.Add((size,png.ToArray()));
        }
        using var stream=new MemoryStream();using(var writer=new BinaryWriter(stream,System.Text.Encoding.UTF8,true))
        {
            writer.Write((ushort)0);writer.Write((ushort)1);writer.Write((ushort)payloads.Count);int offset=6+16*payloads.Count;
            foreach(var p in payloads){writer.Write((byte)(p.Size==256?0:p.Size));writer.Write((byte)(p.Size==256?0:p.Size));writer.Write((byte)0);writer.Write((byte)0);writer.Write((ushort)1);writer.Write((ushort)32);writer.Write(p.Data.Length);writer.Write(offset);offset+=p.Data.Length;}
            foreach(var p in payloads)writer.Write(p.Data);
        }
        return stream.ToArray();
    }

}
