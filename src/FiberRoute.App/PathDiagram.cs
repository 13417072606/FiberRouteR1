using FiberRoute.Core;
using System.Drawing.Drawing2D;
namespace FiberRoute.App;

public sealed class PathDiagram : ScrollableControl
{
    sealed record Item(int Logical,string Name,string Type,int[] Members,HashSet<int> Used,string Room);
    sealed record Link(int From,int To,string Distance,int Free,int Total);
    readonly List<Item> nodes=[];readonly List<Link> links=[];readonly List<PointF> positions=[];
    readonly ToolTip tip=new(){AutoPopDelay=20000};ToolStripDropDown? details;Graph? graph;
    bool dragging,moved;int draggedNode=-1,hovered=-1;Point origin,scrollOrigin;PointF nodeOrigin;string signature="";
    public PathDiagram()
    {
        DoubleBuffered=true;AutoScroll=true;BackColor=Color.White;TabStop=true;Cursor=Cursors.Hand;
        AccessibleName="路径示意";AccessibleDescription="拖动单个节点，连线与距离标签实时跟随；拖动空白平移；滚轮横向浏览。";
    }
    float DpiScale=>DeviceDpi/96f;
    public int DisplayedNodeCount=>nodes.Count;
    public IReadOnlyList<PointF> NodePositions=>positions;
    public IReadOnlyList<string> RoomWarnings=>nodes.SelectMany((n,i)=>Warnings(i)).ToArray();
    IEnumerable<string> Warnings(int i)
    {
        if(graph==null)yield break;
        foreach(int id in nodes[i].Used)
        {
            var affiliation=graph.RoomAffiliations.GetValueOrDefault(id);string room=affiliation?.Room??nodes[i].Room;
            bool cabinet=affiliation?.IsCabinet==true||graph.Nodes[id].Type.Split(';').Contains("光交箱")||System.Text.RegularExpressions.Regex.IsMatch(graph.Nodes[id].Name,@"(?:^|[/\-])GJ\d*(?:-\d+)?$",System.Text.RegularExpressions.RegexOptions.IgnoreCase);
            if(cabinet&&room.Length>0)yield return $"{graph.Nodes[id].Name}\n所属机房：{room}\n该设施为光交箱，请核对所属机房是否正确。"+(affiliation?.Reason.Length>0?"\n登记说明："+affiliation.Reason:"");
        }
    }
    public void SetRoute(Graph g,Route r)
    {
        string next=g.Version+":"+string.Join(',',r.Edges)+":"+string.Join(',',r.Nodes);
        if(signature==next&&ReferenceEquals(graph,g)){Invalidate();return;}
        details?.Close();signature=next;graph=g;nodes.Clear();links.Clear();positions.Clear();
        var roomOf=g.Rooms.SelectMany(x=>x.Value.Select(id=>(id,x.Key))).GroupBy(x=>x.id).ToDictionary(x=>x.Key,x=>x.First().Key);
        int Add(int id)
        {
            int logical=g.Logical[id];if(nodes.Count>0&&nodes[^1].Logical==logical){nodes[^1].Used.Add(id);return nodes.Count-1;}
            string room=roomOf.GetValueOrDefault(id,"");string label=room.Length>0?room:g.ClusterNames.GetValueOrDefault(logical,g.Nodes[id].Name);
            int[] members=g.ClusterMembers.GetValueOrDefault(logical)??(room.Length>0?g.Rooms[room]:[id]);
            nodes.Add(new(logical,label,room.Length>0?"机房":g.ClusterNames.ContainsKey(logical)?"集群":g.Nodes[id].Type,members,[id],room));return nodes.Count-1;
        }
        if(r.Steps.Length>0)
        {
            foreach(var step in r.Steps){int a=Add(step.From),b=Add(step.To);if(a!=b){var edge=g.Edges[step.Edge];links.Add(new(a,b,edge.Length is double d?$"{d:0.##} 米":"长度未知",edge.Free,edge.Total));}}
        }
        else foreach(int id in r.Nodes.Concat(r.Transfers.SelectMany(x=>new[]{x.From,x.To})))Add(id);
        ResetLayout();
    }
    public void ResetLayout()
    {
        positions.Clear();for(int i=0;i<nodes.Count;i++)positions.Add(new(112+i*252,54));
        AutoScrollPosition=Point.Empty;MeasureContent();Invalidate();
    }
    void MeasureContent()
    {
        AutoScrollMinSize=positions.Count==0?Size.Empty:new((int)((positions.Max(p=>p.X)+125)*DpiScale),(int)((positions.Max(p=>p.Y)+155)*DpiScale));
    }
    protected override void OnDpiChangedAfterParent(EventArgs e){base.OnDpiChangedAfterParent(e);MeasureContent();}
    public void Clear(){details?.Close();nodes.Clear();links.Clear();positions.Clear();signature="";graph=null;AutoScrollMinSize=Size.Empty;AutoScrollPosition=Point.Empty;Invalidate();}
    void ScrollTo(int x,int y)
    {
        AutoScrollPosition=new(Math.Clamp(x,0,Math.Max(0,AutoScrollMinSize.Width-ClientSize.Width)),Math.Clamp(y,0,Math.Max(0,AutoScrollMinSize.Height-ClientSize.Height)));Invalidate();
    }
    PointF World(Point p)=>new((p.X-AutoScrollPosition.X)/DpiScale,(p.Y-AutoScrollPosition.Y)/DpiScale);
    int Hit(Point p)
    {
        var world=World(p);for(int i=positions.Count-1;i>=0;i--)if(new RectangleF(positions[i].X-108,positions[i].Y-31,216,151).Contains(world))return i;return -1;
    }
    public (PointF Start,PointF End,PointF Label) ConnectionGeometry(int index)
    {
        var edge=links[index];var a=positions[edge.From];var b=positions[edge.To];float dx=b.X-a.X,dy=b.Y-a.Y,d=Math.Max(1,MathF.Sqrt(dx*dx+dy*dy));float ux=dx/d,uy=dy/d;
        return (new(a.X+ux*32,a.Y+uy*32),new(b.X-ux*32,b.Y-uy*32),new((a.X+b.X)/2+uy*21,(a.Y+b.Y)/2-ux*21));
    }
    protected override void OnMouseWheel(MouseEventArgs e){ScrollTo(-AutoScrollPosition.X-(int)(e.Delta/120f*120*DpiScale),-AutoScrollPosition.Y);}
    protected override void OnMouseDown(MouseEventArgs e)
    {
        base.OnMouseDown(e);if(e.Button!=MouseButtons.Left)return;details?.Close();Focus();dragging=true;moved=false;origin=e.Location;scrollOrigin=new(-AutoScrollPosition.X,-AutoScrollPosition.Y);draggedNode=Hit(e.Location);if(draggedNode>=0)nodeOrigin=positions[draggedNode];Capture=true;Cursor=draggedNode>=0?Cursors.SizeAll:Cursors.Hand;
    }
    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        if(dragging)
        {
            int dx=e.X-origin.X,dy=e.Y-origin.Y;moved|=Math.Abs(dx)+Math.Abs(dy)>4;
            if(draggedNode>=0){positions[draggedNode]=new(Math.Clamp(nodeOrigin.X+dx/DpiScale,110,20000),Math.Clamp(nodeOrigin.Y+dy/DpiScale,48,10000));MeasureContent();Invalidate();}
            else ScrollTo(scrollOrigin.X-dx,scrollOrigin.Y-dy);return;
        }
        int index=Hit(e.Location);Cursor=index>=0?Cursors.SizeAll:Cursors.Hand;
        if(index==hovered)return;hovered=index;
        string? text=index>=0?nodes[index].Name+"\n"+(nodes[index].Members.Length>1?$"{nodes[index].Members.Length} 个设施 · 单击查看成员\n":"")+string.Join("\n",Warnings(index))+"\n拖动节点可调整位置":AccessibleDescription;
        tip.SetToolTip(this,text);
    }
    protected override void OnMouseUp(MouseEventArgs e)
    {
        base.OnMouseUp(e);if(e.Button!=MouseButtons.Left)return;int clicked=draggedNode;bool show=dragging&&!moved&&clicked>=0;dragging=false;draggedNode=-1;Capture=false;Cursor=Cursors.Hand;if(show)ShowDetails(clicked,e.Location);
    }
    protected override void OnMouseCaptureChanged(EventArgs e){base.OnMouseCaptureChanged(e);if(!Capture){dragging=false;draggedNode=-1;Cursor=Cursors.Hand;}}
    void ShowDetails(int i,Point location)
    {
        if(graph==null)return;details?.Dispose();var node=nodes[i];var panel=new Panel{Width=430,Height=280,BackColor=Color.White,Padding=new Padding(14),Font=Font};
        var heading=new Label{Text=node.Name,Dock=DockStyle.Top,Height=48,Font=new Font(Font,FontStyle.Bold),ForeColor=Theme.Ink};
        var warning=string.Join("\n\n",Warnings(i));var content=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,BorderStyle=BorderStyle.None,BackColor=Color.White,ForeColor=Theme.Ink,ScrollBars=ScrollBars.Vertical,Text=(warning.Length>0?"光交归属提示\n"+warning+"\n\n":"")+$"设施成员（{node.Members.Length}）\n"+string.Join("\n",node.Members.Select(id=>graph.Nodes[id].Name))};
        var close=new UiButton{Text="关闭",Dock=DockStyle.Bottom,Height=34};close.Click+=(_,_)=>details?.Close();panel.Controls.Add(content);panel.Controls.Add(close);panel.Controls.Add(heading);
        details=new ToolStripDropDown{Padding=new Padding(1),AutoClose=true};details.Items.Add(new ToolStripControlHost(panel){Margin=Padding.Empty,Padding=Padding.Empty});details.Show(this,location);
    }
    protected override bool IsInputKey(Keys keyData)=>keyData is Keys.Left or Keys.Right or Keys.Home or Keys.End||base.IsInputKey(keyData);
    protected override void OnKeyDown(KeyEventArgs e)
    {
        base.OnKeyDown(e);switch(e.KeyCode){case Keys.Left:ScrollTo(-AutoScrollPosition.X-120,-AutoScrollPosition.Y);break;case Keys.Right:ScrollTo(-AutoScrollPosition.X+120,-AutoScrollPosition.Y);break;case Keys.Home:ScrollTo(0,0);break;case Keys.End:ScrollTo(int.MaxValue,-AutoScrollPosition.Y);break;default:return;}e.Handled=true;
    }
    protected override void WndProc(ref Message m){if(m.Msg==0x20e){ScrollTo(-AutoScrollPosition.X+(short)((long)m.WParam>>16),-AutoScrollPosition.Y);m.Result=IntPtr.Zero;return;}base.WndProc(ref m);}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);var g=e.Graphics;g.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);g.ScaleTransform(DpiScale,DpiScale);g.SmoothingMode=SmoothingMode.AntiAlias;
        using var lineFont=new Font(Font.FontFamily,13,FontStyle.Bold,GraphicsUnit.Pixel);using var nameFont=new Font(Font.FontFamily,13,FontStyle.Bold,GraphicsUnit.Pixel);using var small=new Font(Font.FontFamily,12,FontStyle.Regular,GraphicsUnit.Pixel);
        using var ink=new SolidBrush(Theme.Ink);using var teal=new SolidBrush(Theme.Teal);using var muted=new SolidBrush(Theme.Muted);using var white=new SolidBrush(Color.White);using var tint=new SolidBrush(Color.FromArgb(239,245,250));using var orange=new SolidBrush(Color.FromArgb(176,101,0));using var amber=new SolidBrush(Color.FromArgb(255,240,215));using var center=new StringFormat{Alignment=StringAlignment.Center,LineAlignment=StringAlignment.Center,Trimming=StringTrimming.EllipsisCharacter};
        if(nodes.Count==0){g.DrawString("设置起终点，查询方案后查看路径示意",Font,muted,new RectangleF(8,35,Math.Max(40,ClientSize.Width/DpiScale-16),45),center);return;}
        using var pen=new Pen(Theme.Teal,2.2f);
        for(int i=0;i<links.Count;i++)
        {
            var edge=links[i];var line=ConnectionGeometry(i);g.DrawLine(pen,line.Start,line.End);g.FillEllipse(teal,line.Start.X-3,line.Start.Y-3,6,6);g.FillEllipse(teal,line.End.X-3,line.End.Y-3,6,6);
            string text=$"{edge.Distance} · 空闲 {edge.Free} 芯";var bounds=new RectangleF(line.Label.X-112,line.Label.Y-13,224,26);g.FillRectangle(white,bounds);g.DrawString(text,lineFont,teal,bounds,center);
        }
        for(int i=0;i<nodes.Count;i++)
        {
            var n=nodes[i];var p=positions[i];g.FillEllipse(tint,p.X-28,p.Y-28,56,56);Theme.Glyph(g,n.Type.Contains("光交")?"cabinet":"building",Rectangle.Round(new(p.X-17,p.Y-17,34,34)),Theme.Ink);
            if(dragging&&draggedNode==i){using var focus=new Pen(Theme.Teal,1.5f);g.DrawEllipse(focus,p.X-32,p.Y-32,64,64);}
            g.DrawString(n.Name,nameFont,ink,new RectangleF(p.X-105,p.Y+38,210,43),center);
            string subtitle=n.Members.Length>1?$"{n.Members.Length} 个设施 · 查看成员":n.Type;g.DrawString(subtitle,small,muted,new RectangleF(p.X-105,p.Y+83,210,22),center);
            if(Warnings(i).Any()){var rect=new RectangleF(p.X-72,p.Y+109,144,24);using var path=Theme.Round(rect,6);g.FillPath(amber,path);g.DrawString("含光交 · 归属提示",small,orange,rect,center);}
        }
    }
    protected override void Dispose(bool disposing){if(disposing){tip.Dispose();details?.Dispose();}base.Dispose(disposing);}
}
