using System.Drawing.Drawing2D;
namespace FiberRoute.App;
internal sealed class SettingsArtwork:Control
{
    public string Kind {get;set;}="database";
    public SettingsArtwork(){DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;int size=Math.Min(Width,Height);var bounds=new Rectangle((Width-size)/2,(Height-size)/2,size,size);
        if(Kind is "settings" or "info"){Theme.Glyph(g,Kind,Rectangle.Inflate(bounds,-5,-5),Kind=="info"?Color.FromArgb(0,113,223):Theme.Ink);return;}
        using(var rounded=Theme.Round(bounds,size*.2f)){using var fill=new LinearGradientBrush(bounds,Kind=="software"?Color.FromArgb(24,121,199):Color.FromArgb(228,243,255),Kind=="software"?Color.FromArgb(0,163,153):Color.FromArgb(209,231,250),60);g.FillPath(fill,rounded);}
        var inner=Rectangle.Inflate(bounds,-size/5,-size/5);if(Kind=="database")
        {
            using var blue=new SolidBrush(Color.FromArgb(27,107,174));using var separator=new Pen(Color.FromArgb(226,243,255),Math.Max(1.5f,size/38f));int ellipse=Math.Max(5,inner.Height/4);g.FillRectangle(blue,inner.X,inner.Y+ellipse/2,inner.Width,inner.Height-ellipse);g.FillEllipse(blue,inner.X,inner.Y,inner.Width,ellipse);g.FillEllipse(blue,inner.X,inner.Bottom-ellipse,inner.Width,ellipse);g.DrawArc(separator,inner.X,inner.Y+inner.Height/4,inner.Width,ellipse,0,180);g.DrawArc(separator,inner.X,inner.Y+inner.Height/2,inner.Width,ellipse,0,180);
        }
        else Theme.Glyph(g,Kind,inner,Color.White);
    }
}
