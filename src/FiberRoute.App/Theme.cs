using System.Drawing.Drawing2D;
namespace FiberRoute.App;

internal static class Theme
{
    public static readonly Color Ink=Color.FromArgb(22,38,62),Muted=Color.FromArgb(111,125,145),Teal=Color.FromArgb(0,137,147),Line=Color.FromArgb(221,230,239),Canvas=Color.FromArgb(242,246,250),Tint=Color.FromArgb(227,247,249);
    public static GraphicsPath Round(RectangleF r,float radius)
    {
        float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    public static void Glyph(Graphics g,string kind,Rectangle bounds,Color color)
    {
        var state=g.Save();g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24f,bounds.Height/24f);g.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        switch(kind)
        {
            case "search":g.DrawEllipse(p,3,3,13,13);g.DrawLine(p,14,14,21,21);break;
            case "swap":g.DrawLines(p,new Point[]{new(8,3),new(8,20),new(3,15)});g.DrawLine(p,8,20,13,15);g.DrawLines(p,new Point[]{new(17,21),new(17,4),new(12,9)});g.DrawLine(p,17,4,22,9);break;
            case "export":g.DrawLines(p,new Point[]{new(4,13),new(4,21),new(21,21),new(21,13)});g.DrawLines(p,new Point[]{new(12,3),new(12,15)});g.DrawLines(p,new Point[]{new(7,10),new(12,15),new(17,10)});break;
            case "settings":g.DrawEllipse(p,8,8,8,8);for(int i=0;i<8;i++){double a=i*Math.PI/4;g.DrawLine(p,12+(float)Math.Cos(a)*7,12+(float)Math.Sin(a)*7,12+(float)Math.Cos(a)*10,12+(float)Math.Sin(a)*10);}g.DrawEllipse(p,4,4,16,16);break;
            case "cabinet":g.DrawRectangle(p,5,3,14,18);g.DrawLine(p,8,7,16,7);g.DrawLine(p,8,11,16,11);g.DrawEllipse(p,8,15,2,2);break;
            case "stop":using(var b=new SolidBrush(color))g.FillRectangle(b,6,6,12,12);break;
            case "chevron":g.DrawLines(p,new Point[]{new(6,9),new(12,15),new(18,9)});break;
            case "check":g.DrawLines(p,new Point[]{new(5,12),new(10,17),new(20,6)});break;
            default:g.DrawLines(p,new Point[]{new(4,21),new(4,4),new(14,4),new(14,21)});g.DrawLines(p,new Point[]{new(14,10),new(20,10),new(20,21)});g.DrawLine(p,2,21,22,21);for(int i=7;i<19;i+=4)g.DrawLine(p,7,i,11,i);break;
        }g.Restore(state);
    }
}
internal sealed class Surface:Panel
{
    public Color SurfaceColor {get;set;}=Color.White;
    public Color BorderColor {get;set;}=Theme.Line;
    public Surface(){DoubleBuffered=true;BackColor=Theme.Canvas;Padding=new Padding(16);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),10);using var fill=new SolidBrush(SurfaceColor);using var pen=new Pen(BorderColor);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
}
internal sealed class UiButton:Button
{
    public bool Primary {get;set;}
    public bool Selected {get;set;}
    public bool Navigation {get;set;}
    public string Glyph {get;set;}="";
    bool hover,pressed;
    public UiButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Color.White;Cursor=Cursors.Hand;Height=42;Padding=new Padding(12,6,12,6);Margin=new Padding(4);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaintBackground(PaintEventArgs e)=>e.Graphics.Clear(Parent?.BackColor??Color.White);
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float scale=DeviceDpi/96f;g.Clear(Parent?.BackColor??Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary||Selected&&!Navigation?Theme.Teal:hover?Color.FromArgb(237,246,248):Color.White;Color ink=Primary||Selected&&!Navigation?Color.White:Selected?Theme.Teal:Theme.Ink;if(!Enabled){fill=Color.FromArgb(239,243,247);ink=Theme.Muted;}
        if(Navigation){g.Clear(Color.White);if(Selected)using(var p=new Pen(Theme.Teal,4*scale){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawLine(p,15*scale,Height-3*scale,Width-15*scale,Height-3*scale);}
        else {using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),7);using var b=new SolidBrush(pressed?ControlPaint.Dark(fill,.04f):fill);g.FillPath(b,path);using var p=new Pen(Primary||Selected?Theme.Teal:Theme.Line);g.DrawPath(p,path);}
        int left=(int)((Glyph.Length>0?36:8)*scale),icon=(int)(21*scale);if(Glyph.Length>0)Theme.Glyph(g,Glyph,new Rectangle((int)(12*scale),(Height-icon)/2,icon,icon),ink);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(left,2,Width-left-(int)(8*scale),Height-5),ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if(Focused&&ShowFocusCues)ControlPaint.DrawFocusRectangle(g,new Rectangle(5,5,Width-11,Height-11),ink,fill);
    }
}
