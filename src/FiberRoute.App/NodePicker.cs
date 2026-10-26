using FiberRoute.Core;
namespace FiberRoute.App;
public sealed class NodePicker:UserControl
{
    sealed record Choice(Node Node,string Label);
    readonly TextBox input=new(){BorderStyle=BorderStyle.None,Dock=DockStyle.Fill,PlaceholderText="输入机房或设施名称搜索"};
    readonly ListBox list=new(){BorderStyle=BorderStyle.None,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=40};
    readonly ToolStripDropDown dropdown=new(){Padding=Padding.Empty,AutoClose=false};
    readonly Func<Graph> graph;readonly System.Windows.Forms.Timer timer=new(){Interval=220};Node? selected;bool selecting;
    public Node? Selected=>selected;
    public NodePicker(Func<Graph> source)
    {
        graph=source;Width=300;Height=48;MinimumSize=new Size(120,44);Padding=new Padding(40,12,36,8);BackColor=Color.White;DoubleBuffered=true;Controls.Add(input);
        var host=new ToolStripControlHost(list){Margin=Padding.Empty,Padding=Padding.Empty,AutoSize=false,Size=new Size(500,280)};dropdown.Items.Add(host);
        input.TextChanged+=(_,_)=>{if(selecting)return;selected=null;timer.Stop();timer.Start();};timer.Tick+=(_,_)=>{timer.Stop();Find();};
        list.DrawItem+=(_,e)=>{if(e.Index<0||list.Items[e.Index] is not Choice item)return;using var bg=new SolidBrush((e.State&DrawItemState.Selected)!=0?Theme.Tint:Color.White);e.Graphics.FillRectangle(bg,e.Bounds);TextRenderer.DrawText(e.Graphics,item.Label,Font,new Rectangle(e.Bounds.X+10,e.Bounds.Y+4,e.Bounds.Width-20,e.Bounds.Height-8),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};
        list.Click+=(_,_)=>Choose();list.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){Choose();e.Handled=true;}if(e.KeyCode==Keys.Escape)dropdown.Close();};
        input.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Down){if(!dropdown.Visible)Find();list.Focus();if(list.Items.Count>0)list.SelectedIndex=0;e.Handled=true;}else if(e.KeyCode==Keys.Enter&&dropdown.Visible){Choose();e.SuppressKeyPress=true;}else if(e.KeyCode==Keys.Escape)dropdown.Close();};
        input.Leave+=(_,_)=>{if(IsHandleCreated)BeginInvoke((Action)(()=>{if(!list.ContainsFocus&&!input.ContainsFocus)dropdown.Close();}));};list.Leave+=(_,_)=>{if(!input.ContainsFocus)dropdown.Close();};
        MouseDown+=(_,_)=>{input.Focus();Find();};SizeChanged+=(_,_)=>Invalidate();
    }
    void Find()
    {
        if(!Visible||!IsHandleCreated)return;var g=graph();string term=input.Text.Trim();var choices=new List<Choice>();
        foreach(var pair in g.Rooms.Where(x=>x.Key.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(15))choices.Add(new(g.Nodes[pair.Value[0]],pair.Key+$"  ·  {pair.Value.Length}个设施"));
        foreach(var pair in g.ClusterNames.Where(x=>!g.Rooms.ContainsKey(x.Value)&&x.Value.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(15))choices.Add(new(g.Nodes[pair.Key],pair.Value+$"  ·  {g.ClusterMembers[pair.Key].Length}个设施"));
        choices.AddRange(g.Nodes.Where(n=>n.Name.Contains(term,StringComparison.OrdinalIgnoreCase)).Take(35).Select(n=>new Choice(n,n.Name)));list.Items.Clear();list.Items.AddRange(choices.Take(40).Cast<object>().ToArray());
        if(list.Items.Count==0){dropdown.Close();return;}var host=(ToolStripControlHost)dropdown.Items[0];host.Size=new Size(Math.Max(Width,480),Math.Min(280,list.Items.Count*list.ItemHeight+4));list.Size=host.Size;if(!dropdown.Visible)dropdown.Show(this,new Point(0,Height));input.Focus();
    }
    void Choose(){if(list.SelectedItem is Choice item){SetSelection(item.Node,item.Label);dropdown.Close();input.SelectionStart=input.TextLength;}}
    void SetSelection(Node? n,string? label=null){selecting=true;selected=n;input.Text=label??n?.Name??"";timer.Stop();dropdown.Close();selecting=false;Invalidate();}
    public void SelectNode(Node? n)=>SetSelection(n);
    public void Clear()=>SelectNode(null);
    protected override void OnPaint(PaintEventArgs e){base.OnPaint(e);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var p=new Pen(input.Focused?Theme.Teal:Theme.Line);using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),7);e.Graphics.DrawPath(p,path);Theme.Glyph(e.Graphics,"building",new Rectangle(10,(Height-24)/2,24,24),Theme.Muted);Theme.Glyph(e.Graphics,"chevron",new Rectangle(Width-28,(Height-20)/2,20,20),Theme.Muted);}
    protected override void Dispose(bool disposing){if(disposing){timer.Dispose();dropdown.Dispose();}base.Dispose(disposing);}
}
