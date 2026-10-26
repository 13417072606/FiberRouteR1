using FiberRoute.Core;
using System.Drawing.Drawing2D;
namespace FiberRoute.App;
public sealed class PathDiagram:ScrollableControl
{
    sealed record Item(string Name,string Type,bool Dashed,string Distance,string Group,bool Room);
    List<Item> nodes=[];bool dragging;Point origin;int dragOffset;readonly ToolTip tip=new();int hovered=-1;
    public PathDiagram(){DoubleBuffered=true;AutoScroll=true;BackColor=Color.White;TabStop=true;Cursor=Cursors.Hand;AccessibleName="连接顺序图";AccessibleDescription="鼠标滚轮横移，按住空白处拖动，或使用左右方向键。";}
    public void SetRoute(Graph g,Route r)
    {
        nodes=[];void Add(int id,bool dashed,string length=""){string group=g.ClusterNames.GetValueOrDefault(g.Logical[id],"");nodes.Add(new(g.Nodes[id].Name,g.Nodes[id].Type,dashed,length,group,g.Rooms.ContainsKey(group)));}
        if(r.Steps.Length>0){Add(r.Nodes[0],false);foreach(var s in r.Steps){if(nodes[^1].Name!=g.Nodes[s.From].Name)Add(s.From,true);var e=g.Edges[s.Edge];Add(s.To,false,e.Length is double d?$"{d:0.##} 米":"长度未知");}foreach(var t in r.Transfers.Where(t=>t.BeforeSegment==r.Edges.Length))if(nodes[^1].Name!=g.Nodes[t.To].Name)Add(t.To,true);}
        else if(r.Transfers.Length>0){Add(r.Transfers[0].From,false);foreach(var t in r.Transfers)Add(t.To,true);}else foreach(var n in r.Nodes)Add(n,false);
        AutoScrollPosition=Point.Empty;MeasureContent();Invalidate();
    }
    float DpiScale=>DeviceDpi/96f;
    void MeasureContent(){AutoScrollMinSize=nodes.Count==0?Size.Empty:new Size((int)(Math.Max(1,nodes.Count-1)*280*DpiScale+240*DpiScale),(int)(180*DpiScale));}
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);MeasureContent();}
    public void Clear(){nodes=[];AutoScrollMinSize=Size.Empty;AutoScrollPosition=Point.Empty;Invalidate();}
    void ScrollTo(int x){int max=Math.Max(0,AutoScrollMinSize.Width-ClientSize.Width);AutoScrollPosition=new Point(Math.Clamp(x,0,max),Math.Max(0,-AutoScrollPosition.Y));Invalidate();}
    protected override void OnMouseWheel(MouseEventArgs e){ScrollTo(-AutoScrollPosition.X-(int)(e.Delta/120f*120*DpiScale));}
    protected override void OnMouseDown(MouseEventArgs e){base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;Focus();dragging=true;origin=e.Location;dragOffset=-AutoScrollPosition.X;Capture=true;Cursor=Cursors.SizeWE;}
    protected override void OnMouseMove(MouseEventArgs e){base.OnMouseMove(e);if(dragging){ScrollTo(dragOffset-(e.X-origin.X));return;}int index=(int)((e.X-AutoScrollPosition.X)/(280*DpiScale));if(index!=hovered){hovered=index;tip.SetToolTip(this,index>=0&&index<nodes.Count?nodes[index].Name+(nodes[index].Group.Length>0?"\n所属整体："+nodes[index].Group:"")+"\n滚轮横移 · 按住拖动":AccessibleDescription);}}
    protected override void OnMouseUp(MouseEventArgs e){base.OnMouseUp(e);if(e.Button==MouseButtons.Left){dragging=false;Capture=false;Cursor=Cursors.Hand;}}
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;Cursor=Cursors.Hand;}}
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e){base.OnKeyDown(e);if(e.KeyCode==Keys.Left){ScrollTo(-AutoScrollPosition.X-120);e.Handled=true;}if(e.KeyCode==Keys.Right){ScrollTo(-AutoScrollPosition.X+120);e.Handled=true;}if(e.KeyCode==Keys.Home){ScrollTo(0);e.Handled=true;}if(e.KeyCode==Keys.End){ScrollTo(int.MaxValue);e.Handled=true;}}
    protected override void WndProc(ref Message m){if(m.Msg==0x20e){int delta=(short)((long)m.WParam>>16);ScrollTo(-AutoScrollPosition.X+delta);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);g.ScaleTransform(DpiScale,DpiScale);g.SmoothingMode=SmoothingMode.AntiAlias;
        if(nodes.Count==0){TextRenderer.DrawText(g,"设置起终点后，查询并选择方案查看连接顺序",Font,new Rectangle(10,45,(int)(ClientSize.Width/DpiScale)-20,48),Theme.Muted,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak);return;}
        using var pen=new Pen(Theme.Teal,2.2f);using var tint=new SolidBrush(Color.FromArgb(239,245,250));using var lineText=new Font(Font.FontFamily,14,FontStyle.Bold,GraphicsUnit.Pixel);using var typeFont=new Font(Font.FontFamily,13,FontStyle.Regular,GraphicsUnit.Pixel);using var ink=new SolidBrush(Theme.Ink);using var teal=new SolidBrush(Theme.Teal);using var muted=new SolidBrush(Theme.Muted);using var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center};
        for(int i=0;i<nodes.Count;i++)
        {
            int x=100+i*280;var n=nodes[i];if(i>0){pen.DashStyle=n.Dashed?DashStyle.Dash:DashStyle.Solid;g.DrawLine(pen,x-252,49,x-28,49);g.DrawString(n.Dashed?"归属关系":n.Distance,lineText,teal,new RectangleF(x-250,12,220,28),center);}pen.DashStyle=DashStyle.Solid;g.FillEllipse(tint,x-28,21,56,56);Theme.Glyph(g,n.Type.Contains("光交")?"cabinet":"building",new Rectangle(x-17,31,34,34),Theme.Ink);
            if(n.Room){g.DrawString(n.Group,lineText,ink,new RectangleF(x-108,87,216,40),new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter});g.DrawString(n.Name,typeFont,muted,new RectangleF(x-108,130,216,22),new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter,FormatFlags=StringFormatFlags.NoWrap});}
            else g.DrawString(n.Name,lineText,ink,new RectangleF(x-108,87,216,64),new StringFormat{Alignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter});
            g.DrawString(n.Type,typeFont,muted,new RectangleF(x-108,156,216,24),center);
        }
    }
    protected override void Dispose(bool disposing){if(disposing)tip.Dispose();base.Dispose(disposing);}
}
