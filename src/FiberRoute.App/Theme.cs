using System.Drawing.Drawing2D;
namespace FiberRoute.App;

internal static class Theme
{
    public static readonly Color Ink=Color.FromArgb(22,38,62),Muted=Color.FromArgb(111,125,145),Teal=Color.FromArgb(0,137,147),Line=Color.FromArgb(221,230,239),Canvas=Color.FromArgb(242,246,250),Tint=Color.FromArgb(227,247,249);
    public static GraphicsPath Round(RectangleF r,float radius)
    {
        float d=Math.Min(radius*2,Math.Min(r.Width,r.Height));var p=new GraphicsPath();p.AddArc(r.X,r.Y,d,d,180,90);p.AddArc(r.Right-d,r.Y,d,d,270,90);p.AddArc(r.Right-d,r.Bottom-d,d,d,0,90);p.AddArc(r.X,r.Bottom-d,d,d,90,90);p.CloseFigure();return p;
    }
    public static (Color Back,Color Fore) BadgeColors(string text) => text switch
    {
        "已确认" or "在网" => (Color.FromArgb(222,247,238),Color.FromArgb(0,119,84)),
        "需核对" or "在建" => (Color.FromArgb(255,241,215),Color.FromArgb(166,92,0)),
        "已暂停" => (Color.FromArgb(255,233,237),Color.FromArgb(169,54,73)),
        "已启用" or "已纳入" => (Color.FromArgb(222,247,238),Color.FromArgb(0,119,84)),
        "待确认" or "未识别" => (Color.FromArgb(233,240,248),Color.FromArgb(82,108,140)),
        "分纤箱" => (Color.FromArgb(239,231,255),Color.FromArgb(102,59,181)),
        "光交箱" => (Color.FromArgb(225,240,255),Color.FromArgb(0,107,181)),
        "机房" or "光终端盒" => (Color.FromArgb(222,248,239),Color.FromArgb(0,120,95)),
        _ when text.Contains("不足") => (Color.FromArgb(255,235,224),Color.FromArgb(168,64,24)),
        _ => (Tint,Teal)
    };
    public static void Badge(Graphics g,string text,Font font,Rectangle bounds,(Color Back,Color Fore)? palette=null)
    {
        var colors=palette??BadgeColors(text);int width=Math.Min(bounds.Width,TextRenderer.MeasureText(text,font).Width+14);
        if(width<=0||bounds.Height<=0)return;var rect=new Rectangle(bounds.X,bounds.Y,width,bounds.Height);
        using var path=Round(rect,5);using var b=new SolidBrush(colors.Back);g.FillPath(b,path);
        TextRenderer.DrawText(g,text,font,Rectangle.Inflate(rect,-5,0),colors.Fore,TextFormatFlags.VerticalCenter|TextFormatFlags.HorizontalCenter|TextFormatFlags.EndEllipsis);
    }
    public static void Glyph(Graphics g,string kind,Rectangle bounds,Color color)
    {
        var state=g.Save();g.TranslateTransform(bounds.X,bounds.Y);g.ScaleTransform(bounds.Width/24f,bounds.Height/24f);g.SmoothingMode=SmoothingMode.AntiAlias;using var p=new Pen(color,1.8f){StartCap=LineCap.Round,EndCap=LineCap.Round,LineJoin=LineJoin.Round};
        switch(kind)
        {
            case "software":using(var b=new SolidBrush(color)){g.FillRectangle(b,4,14,4,8);g.FillRectangle(b,10,9,4,13);g.FillRectangle(b,16,3,4,19);}break;
            case "desktop":g.DrawRectangle(p,2,3,20,14);g.DrawLine(p,12,17,12,22);g.DrawLine(p,7,22,17,22);break;
            case "grid":g.DrawRectangle(p,2,3,20,18);g.DrawLine(p,2,9,22,9);g.DrawLine(p,9,3,9,21);break;
            case "info":g.DrawEllipse(p,2,2,20,20);g.DrawLine(p,12,10,12,17);g.DrawEllipse(p,11.5f,6,1,1);break;
            case "link":g.DrawArc(p,3,10,13,9,35,290);g.DrawArc(p,8,3,13,9,215,290);g.DrawLine(p,8,16,16,8);break;
            case "list":g.DrawRectangle(p,5,2,14,20);for(int y=7;y<20;y+=5){g.DrawLine(p,9,y,16,y);}break;
            case "network":g.DrawLine(p,12,6,5,18);g.DrawLine(p,12,6,19,18);g.DrawEllipse(p,9,1,6,6);g.DrawEllipse(p,2,17,6,6);g.DrawEllipse(p,16,17,6,6);break;
            case "database":g.DrawEllipse(p,3,3,18,6);g.DrawLine(p,3,6,3,19);g.DrawLine(p,21,6,21,19);g.DrawArc(p,3,9,18,6,0,180);g.DrawArc(p,3,16,18,6,0,180);break;
            case "ruler":g.DrawPolygon(p,new Point[]{new Point(3,17),new Point(17,3),new Point(22,8),new Point(8,22)});for(int i=0;i<4;i++)g.DrawLine(p,7+i*3,15-i*3,9+i*3,17-i*3);break;
            case "warning":g.DrawPolygon(p,new Point[]{new Point(12,2),new Point(23,21),new Point(1,21)});g.DrawLine(p,12,8,12,14);g.DrawEllipse(p,11.5f,17,1,1);break;
            case "upload":g.DrawLines(p,new Point[]{new Point(4,14),new Point(4,21),new Point(21,21),new Point(21,14)});g.DrawLine(p,12,3,12,16);g.DrawLines(p,new Point[]{new Point(7,8),new Point(12,3),new Point(17,8)});break;
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
    public Surface(){DoubleBuffered=true;BackColor=Color.White;Padding=new Padding(16);}
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=SmoothingMode.AntiAlias;using var path=Theme.Round(new RectangleF(0,0,Width-1,Height-1),10);using var fill=new SolidBrush(SurfaceColor);using var pen=new Pen(BorderColor);e.Graphics.FillPath(fill,path);e.Graphics.DrawPath(pen,path);}
}
internal sealed class UiButton:Button
{
    public bool Primary {get;set;}
    public bool Selected {get;set;}
    public bool Navigation {get;set;}
    public bool Pill {get;set;}
    public string Glyph {get;set;}="";
    public string Tone {get;set;}="";public bool Accent {get;set;}
    bool hover,pressed;
    public UiButton(){FlatStyle=FlatStyle.Flat;FlatAppearance.BorderSize=0;UseVisualStyleBackColor=false;BackColor=Color.White;Cursor=Cursors.Hand;Height=42;Padding=new Padding(12,6,12,6);Margin=new Padding(4);SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);}
    protected override void OnPaintBackground(PaintEventArgs e)=>e.Graphics.Clear(Parent?.BackColor??Color.White);
    protected override void OnMouseEnter(EventArgs e){hover=true;Invalidate();base.OnMouseEnter(e);}
    protected override void OnMouseLeave(EventArgs e){hover=pressed=false;Invalidate();base.OnMouseLeave(e);}
    protected override void OnMouseDown(MouseEventArgs e){pressed=true;Invalidate();base.OnMouseDown(e);}
    protected override void OnMouseUp(MouseEventArgs e){pressed=false;Invalidate();base.OnMouseUp(e);}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float scale=DeviceDpi/96f;g.Clear(Parent?.BackColor??Color.White);g.SmoothingMode=SmoothingMode.AntiAlias;Color fill=Primary?Theme.Teal:Selected&&!Navigation?Theme.Tint:hover?Color.FromArgb(237,246,248):Color.White;Color ink=Primary?Color.White:Selected||Accent?Theme.Teal:Theme.Ink;if(!Enabled){fill=Color.FromArgb(239,243,247);ink=Theme.Muted;}
        if(Selected&&Tone.Length>0){var colors=Theme.BadgeColors(Tone);fill=colors.Back;ink=colors.Fore;}
        if(Navigation){g.Clear(Color.White);if(Selected)using(var p=new Pen(Theme.Teal,4*scale){StartCap=LineCap.Round,EndCap=LineCap.Round})g.DrawLine(p,15*scale,Height-3*scale,Width-15*scale,Height-3*scale);}
        else {using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),Pill?Height/2f:7);using Brush b=Primary?new LinearGradientBrush(ClientRectangle,Color.FromArgb(0,147,158),Color.FromArgb(0,126,139),35):new SolidBrush(pressed?ControlPaint.Dark(fill,.04f):fill);g.FillPath(b,path);using var p=new Pen(Selected&&Tone.Length>0?ink:Primary||Selected||Accent?Theme.Teal:Theme.Line);g.DrawPath(p,path);}
        int left=(int)((Glyph.Length>0?36:8)*scale),icon=(int)(21*scale);if(Glyph.Length>0)Theme.Glyph(g,Glyph,new Rectangle((int)(12*scale),(Height-icon)/2,icon,icon),ink);
        TextRenderer.DrawText(g,Text,Font,new Rectangle(left,2,Width-left-(int)(8*scale),Height-5),ink,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);
        if(Focused&&ShowFocusCues){using var focus=new Pen(ink,1.3f);using var ring=Theme.Round(new RectangleF(4,4,Width-9,Height-9),4);g.DrawPath(focus,ring);}
    }
}
