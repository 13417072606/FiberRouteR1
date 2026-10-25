using System.Drawing.Drawing2D;
using FiberRoute.Core;
namespace FiberRoute.App;

public sealed class RouteDiagram:ScrollableControl
{
    Graph? graph;Route? route;
    public RouteDiagram(){DoubleBuffered=true;AutoScroll=true;BackColor=Color.White;Resize+=(_,_)=>Relayout();}
    public void SetRoute(Graph g,Route r){graph=g;route=r;Relayout();}
    public void Clear(){graph=null;route=null;AutoScrollMinSize=Size.Empty;Invalidate();}
    void Relayout()
    {
        int columns=Math.Max(1,(ClientSize.Width-24)/210);
        int rows=route==null?0:(route.Nodes.Length+columns-1)/columns;
        AutoScrollMinSize=new Size(230,Math.Max(0,rows*124+35));Invalidate();
    }
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.SmoothingMode=SmoothingMode.AntiAlias;
        if(graph==null||route==null){TextRenderer.DrawText(g,"选择候选方案后显示连接路径",Font,ClientRectangle,Color.Gray,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);return;}
        g.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);
        int columns=Math.Max(1,(ClientSize.Width-24)/210);
        Rectangle Box(int i){int row=i/columns,col=i%columns;if(row%2==1)col=columns-1-col;return new Rectangle(18+col*210,16+row*124,166,66);}
        using var pen=new Pen(Color.FromArgb(40,133,143),2){CustomEndCap=new AdjustableArrowCap(4,5)};
        using var text=new SolidBrush(Color.FromArgb(45,76,87));
        using var edgeFont=new Font(Font.FontFamily,8);
        for(int i=1;i<route.Nodes.Length;i++)
        {
            var previous=Box(i-1);var next=Box(i);Point a,b;
            if(previous.Y==next.Y){bool forward=next.X>previous.X;a=new(forward?previous.Right:previous.Left,previous.Top+33);b=new(forward?next.Left:next.Right,next.Top+33);}
            else{a=new(previous.Left+83,previous.Bottom);b=new(next.Left+83,next.Top);}
            g.DrawLine(pen,a,b);
            var edge=graph.Edges[route.Edges[i-1]];string label=(edge.Length?.ToString("0.##")??"未知")+"m / 空闲"+edge.Free;
            var size=g.MeasureString(label,edgeFont);var rect=new RectangleF((a.X+b.X-size.Width)/2,(a.Y+b.Y)/2+5,size.Width,size.Height);
            g.FillRectangle(Brushes.White,rect);g.DrawString(label,edgeFont,text,rect);
        }
        for(int i=0;i<route.Nodes.Length;i++)
        {
            var node=graph.Nodes[route.Nodes[i]];var box=Box(i);bool endpoint=i==0||i==route.Nodes.Length-1;
            using var fill=new SolidBrush(endpoint?Color.FromArgb(0,105,108):Color.FromArgb(235,245,246));g.FillRectangle(fill,box);
            TextRenderer.DrawText(g,$"{i+1}. {node.Name}\n{node.Type}{(node.Confirmed?"":" · 待确认")}",Font,new Rectangle(box.X+8,box.Y+6,box.Width-16,box.Height-12),endpoint?Color.White:Color.FromArgb(34,62,72),TextFormatFlags.WordBreak|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPrefix);
        }
    }
}
