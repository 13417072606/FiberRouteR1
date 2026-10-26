using FiberRoute.Core;
namespace FiberRoute.App;

public sealed partial class MainForm
{
    readonly Label routeSummary=new(){Text="选择起终点，查询可用光路",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    readonly ComboBox schemeChoice=new(){Dock=DockStyle.Top,DropDownStyle=ComboBoxStyle.DropDownList,DrawMode=DrawMode.OwnerDrawFixed,ItemHeight=48,IntegralHeight=false,DropDownHeight=300,FlatStyle=FlatStyle.Flat,BackColor=Theme.Tint,ForeColor=Theme.Teal};
    readonly List<UiButton> sortingButtons=[];
    UiButton ActionButton(string title,Action action,string glyph="",bool primary=false)
    {var b=(UiButton)B(title,action);b.AutoSize=false;b.Glyph=glyph;b.Primary=primary;return b;}
    UiButton AsyncButton(string title,Func<Task> action,string glyph="",bool primary=false)
    {var b=(UiButton)B(title,action);b.AutoSize=false;b.Glyph=glyph;b.Primary=primary;return b;}
    Label Title(string text,int size=16)=>new(){Text=text,AutoSize=true,Font=new Font(Font.FontFamily,size,FontStyle.Bold),Margin=new Padding(4,4,4,14),ForeColor=Theme.Ink};
    static Control QueryField(string title,Control editor)
    {
        var row=new TableLayoutPanel{Height=48,ColumnCount=2,RowCount=1,Margin=new Padding(4,5,4,5),BackColor=Color.White};row.RowStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Percent,48));row.ColumnStyles.Add(new(SizeType.Percent,52));row.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        var border=new Surface{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(10),Margin=new Padding(0,4,0,4)};if(editor is NumericUpDown number)number.BorderStyle=BorderStyle.None;editor.Dock=DockStyle.None;border.Controls.Add(editor);void Place(){editor.Width=Math.Max(60,border.ClientSize.Width-20);editor.Location=new Point(10,Math.Max(3,(border.ClientSize.Height-editor.Height)/2));}border.SizeChanged+=(_,_)=>Place();Place();row.Controls.Add(border,1,0);return row;
    }
    void BuildRouteWorkspace()
    {
        var tab=Tab("路由配置");start=new(()=>store.Graph);end=new(()=>store.Graph);avoid=new(()=>store.Graph);avoidNodes.DisplayMember="Name";
        order.Items.AddRange(["综合候选","距离最短","跳纤最少","空闲最多"]);order.SelectedIndex=0;
        var leftBody=Stack();leftBody.Padding=new Padding(6);leftBody.Controls.Add(Title("查询条件"));leftBody.Controls.AddRange([L("起点设施"),start]);
        var exchange=new Panel{Height=52,BackColor=Color.White};var swap=ActionButton("",()=>{var a=start.Selected;start.SelectNode(end.Selected);end.SelectNode(a);},"swap");swap.Size=new(54,42);exchange.Controls.Add(swap);exchange.Resize+=(_,_)=>swap.Location=new Point((exchange.ClientSize.Width-swap.Width)/2,4);leftBody.Controls.Add(exchange);
        leftBody.Controls.AddRange([L("终点设施"),end,QueryField("需求芯数",demand),QueryField("最大跳纤",jumps),QueryField("最多返回方案数",count)]);
        var extras=Stack();extras.Dock=DockStyle.None;extras.Padding=new Padding(6);extras.Height=400;extras.Visible=false;extras.BackColor=Color.FromArgb(247,250,252);avoidTypes.Width=280;avoidTypes.Height=110;avoidTypes.MultiColumn=true;avoidTypes.ColumnWidth=130;avoidTypes.BorderStyle=BorderStyle.None;avoidTypes.BackColor=extras.BackColor;
        extras.Controls.AddRange([L("规避节点类型（端点豁免）"),avoidTypes,L("规避具体节点"),avoid,B("添加规避节点",()=>{if(avoid.Selected is Node n&&!avoidNodes.Items.Cast<Node>().Any(x=>x.Id==n.Id))avoidNodes.Items.Add(n);}),avoidNodes,B("移除选中节点",()=>{if(avoidNodes.SelectedIndex>=0)avoidNodes.Items.RemoveAt(avoidNodes.SelectedIndex);})]);
        var more=ActionButton("更多条件  ﹀",()=>{});more.TextAlign=ContentAlignment.MiddleLeft;more.Click+=(_,_)=>{extras.Visible=!extras.Visible;more.Text=extras.Visible?"更多条件  ﹁":"更多条件  ﹀";};leftBody.Controls.Add(more);leftBody.Controls.Add(extras);
        var query=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=58,ColumnCount=2,RowCount=1,BackColor=Color.White,Margin=new Padding(0,8,0,0)};query.RowStyles.Add(new(SizeType.Percent,100));query.ColumnStyles.Add(new(SizeType.Percent,68));query.ColumnStyles.Add(new(SizeType.Percent,32));var search=AsyncButton("查询路由",SearchRoutes,"search",true);search.Dock=DockStyle.Fill;var stop=ActionButton("停止",()=>job?.Cancel(),"stop");stop.Dock=DockStyle.Fill;query.Controls.Add(search,0,0);query.Controls.Add(stop,1,0);
        var left=new Surface{Dock=DockStyle.Fill,Padding=new Padding(14)};left.Controls.Add(leftBody);left.Controls.Add(query);left.Controls.Add(new Label{Text="长度单位：米",Dock=DockStyle.Bottom,Height=34,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.White,ForeColor=Theme.Muted,Padding=new Padding(8,0,0,0)});

        var right=new Surface{Dock=DockStyle.Fill,Padding=new Padding(18)};var summary=new TableLayoutPanel{Dock=DockStyle.Top,Height=64,ColumnCount=2,BackColor=Color.White};summary.ColumnStyles.Add(new(SizeType.Percent,100));summary.ColumnStyles.Add(new(SizeType.Absolute,152));routeSummary.Font=new Font(Font.FontFamily,13,FontStyle.Bold);summary.Controls.Add(routeSummary,0,0);var export=ActionButton("导出方案",ExportRoutes,"export");export.Dock=DockStyle.Fill;export.Margin=new Padding(8,4,0,12);summary.Controls.Add(export,1,0);
        var sorting=new TableLayoutPanel{Dock=DockStyle.Top,Height=66,ColumnCount=4,BackColor=Color.White,Padding=new Padding(0,0,0,12)};
        for(int i=0;i<4;i++){int choice=i;sorting.ColumnStyles.Add(new(SizeType.Percent,25));var b=AsyncButton(order.Items[i]?.ToString()??"",async()=>{order.SelectedIndex=choice;if(routes.Count>0)await SearchRoutes();});b.Dock=DockStyle.Fill;b.Selected=i==0;b.Margin=new Padding(i==0?0:4,0,i==3?0:4,0);sortingButtons.Add(b);sorting.Controls.Add(b,i,0);}
        order.SelectedIndexChanged+=(_,_)=>{for(int i=0;i<sortingButtons.Count;i++){sortingButtons[i].Selected=i==order.SelectedIndex;sortingButtons[i].Invalidate();}};
        schemeChoice.Margin=new Padding(0,0,0,12);schemeChoice.DrawItem+=(_,e)=>{if(e.Index<0)return;e.DrawBackground();using var choiceFont=new Font(Font.FontFamily,12,FontStyle.Bold);TextRenderer.DrawText(e.Graphics,schemeChoice.Items[e.Index]?.ToString()??"",choiceFont,new Rectangle(e.Bounds.X+16,e.Bounds.Y,e.Bounds.Width-24,e.Bounds.Height),Theme.Teal,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);};schemeChoice.SelectedIndexChanged+=(_,_)=>ShowRoute();
        var diagram=Card("连接顺序 · 非地理地图",pathView);var details=Card("逐段资源详情",segments);var results=Rows([55,45],diagram,details);var content=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};content.Controls.Add(results);content.Controls.Add(schemeChoice);
        right.Controls.Add(content);right.Controls.Add(sorting);right.Controls.Add(summary);tab.Controls.Add(Columns(30,left,70,right));
    }
    void UpdateRouteSummary(RouteResult result)
    {
        routeSummary.Text=$"找到 {routes.Count} 个方案 · "+(result.Cancelled?"已停止（结果不完整）":result.Limited?"达到上限（结果不完整）":"搜索完成");
        schemeChoice.Items.Clear();for(int i=0;i<routes.Count;i++){var r=routes[i];schemeChoice.Items.Add($"方案{i+1:00}  ·  {r.Length:0.##}米  ·  跳纤{r.Jumps}次  ·  瓶颈空闲{r.Bottleneck}芯  ·  未知长度{r.Unknown}段");}if(schemeChoice.Items.Count>0)schemeChoice.SelectedIndex=0;
    }
}
