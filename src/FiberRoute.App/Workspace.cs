using FiberRoute.Core;
namespace FiberRoute.App;

public sealed partial class MainForm
{
    readonly Label routeSummary=new(){Text="候选路由 · 选择起终点后查询",Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    readonly ComboBox schemeChoice=new UiComboBox(){Dock=DockStyle.Fill,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly UiCheckBox requireCapacity=new(){Text="仅看满足需求芯数的方案",Checked=true};
    readonly List<UiButton> sortingButtons=[];Control? routeDetailsHost;bool showingRoute;
    UiButton ActionButton(string title,Action action,string glyph="",bool primary=false)
    {var b=(UiButton)B(title,action);b.AutoSize=false;b.Glyph=glyph;b.Primary=primary;return b;}
    UiButton AsyncButton(string title,Func<Task> action,string glyph="",bool primary=false)
    {var b=(UiButton)B(title,action);b.AutoSize=false;b.Glyph=glyph;b.Primary=primary;return b;}
    Label Title(string text,int size=16)=>new(){Text=text,AutoSize=true,Font=new Font(Font.FontFamily,size,FontStyle.Bold),Margin=new Padding(4,3,4,10),ForeColor=Theme.Ink};
    static Control QueryField(string title,Control editor)
    {
        var row=new TableLayoutPanel{Height=42,ColumnCount=2,RowCount=1,Margin=new Padding(0,3,0,3),BackColor=Color.White};row.RowStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Percent,48));row.ColumnStyles.Add(new(SizeType.Percent,52));row.Controls.Add(new Label{Text=title,Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft},0,0);
        var border=new Surface{Dock=DockStyle.Fill,BackColor=Color.White,Padding=new Padding(9),Margin=new Padding(0,2,0,2)};if(editor is NumericUpDown number)number.BorderStyle=BorderStyle.None;editor.Dock=DockStyle.None;border.Controls.Add(editor);void Place(){editor.Width=Math.Max(50,border.ClientSize.Width-18);editor.Location=new Point(9,Math.Max(3,(border.ClientSize.Height-editor.Height)/2));}border.SizeChanged+=(_,_)=>Place();Place();row.Controls.Add(border,1,0);return row;
    }
    void BuildRouteWorkspace()
    {
        var tab=Tab("路由配置");start=new(()=>store.Graph);end=new(()=>store.Graph);avoid=new(()=>store.Graph);avoidNodes.DisplayMember="Name";
        order.Items.AddRange(["满足需求","距离最短","跳纤最少","余量最多"]);order.SelectedIndex=0;
        var leftBody=Stack();leftBody.Padding=new Padding(3);leftBody.Controls.Add(Title("查询条件",15));leftBody.Controls.AddRange([L("起点设施"),start]);
        var exchange=new Panel{Height=43,BackColor=Color.White};var swap=ActionButton("",()=>{var a=start.Selected;start.SelectNode(end.Selected);end.SelectNode(a);},"swap");swap.Size=new(46,34);exchange.Controls.Add(swap);exchange.Resize+=(_,_)=>swap.Location=new Point((exchange.ClientSize.Width-swap.Width)/2,4);leftBody.Controls.Add(exchange);
        leftBody.Controls.AddRange([L("终点设施"),end,QueryField("需求芯数",demand),QueryField("最大跳纤",jumps),QueryField("最多返回方案",count)]);
        var extras=Stack();extras.Dock=DockStyle.None;extras.Padding=new Padding(6);extras.AutoScroll=false;extras.Visible=false;extras.BackColor=Color.FromArgb(247,250,252);avoidTypes.BackColor=extras.BackColor;
        extras.Layout+=(_,_)=>{int height=extras.Controls.Cast<Control>().Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(0).Max()+extras.Padding.Bottom;if(extras.Height!=height)extras.Height=height;};
        extras.Controls.AddRange([L("规避节点类型"),avoidTypes,L("规避具体节点"),avoid,B("添加规避节点",()=>{if(avoid.Selected is Node n&&!avoidNodes.Items.Cast<Node>().Any(x=>x.Id==n.Id))avoidNodes.Items.Add(n);}),avoidNodes,B("移除选中节点",()=>{if(avoidNodes.SelectedIndex>=0)avoidNodes.Items.RemoveAt(avoidNodes.SelectedIndex);})]);
        var more=ActionButton("更多条件",()=>{});more.Height=36;more.Click+=(_,_)=>{extras.Visible=!extras.Visible;more.Text=extras.Visible?"收起条件":"更多条件";};leftBody.Controls.Add(more);leftBody.Controls.Add(extras);
        var query=new TableLayoutPanel{Dock=DockStyle.Bottom,Height=48,ColumnCount=2,RowCount=1,BackColor=Color.White};query.RowStyles.Add(new(SizeType.Percent,100));query.ColumnStyles.Add(new(SizeType.Percent,68));query.ColumnStyles.Add(new(SizeType.Percent,32));var search=AsyncButton("查询路由",SearchRoutes,"search",true);search.Dock=DockStyle.Fill;var stop=ActionButton("停止",()=>job?.Cancel(),"stop");stop.Dock=DockStyle.Fill;query.Controls.Add(search,0,0);query.Controls.Add(stop,1,0);
        var left=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};left.Controls.Add(leftBody);left.Controls.Add(query);left.Controls.Add(new Label{Text="长度单位：米",Dock=DockStyle.Bottom,Height=24,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.White,ForeColor=Theme.Muted});

        var right=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};var summary=new TableLayoutPanel{Dock=DockStyle.Top,Height=42,ColumnCount=2,BackColor=Color.White};summary.ColumnStyles.Add(new(SizeType.Percent,100));summary.ColumnStyles.Add(new(SizeType.Absolute,140));routeSummary.Font=new Font(Font.FontFamily,13,FontStyle.Bold);summary.Controls.Add(routeSummary,0,0);
        var export=ActionButton("导出方案",()=>{} ,"export");export.Dock=DockStyle.Fill;export.Margin=new Padding(6,0,0,6);var exportMenu=new ContextMenuStrip();exportMenu.Items.Add("导出当前方案",null,(_,_)=>ExportRoutes(true));exportMenu.Items.Add("导出全部候选方案",null,(_,_)=>ExportRoutes());export.Click+=(_,_)=>exportMenu.Show(export,new Point(0,export.Height));summary.Controls.Add(export,1,0);
        var sorting=new TableLayoutPanel{Dock=DockStyle.Top,Height=43,ColumnCount=4,BackColor=Color.White,Padding=new Padding(0,0,0,6)};
        for(int i=0;i<4;i++){int choice=i;sorting.ColumnStyles.Add(new(SizeType.Percent,25));var b=AsyncButton(order.Items[i]?.ToString()??"",async()=>{order.SelectedIndex=choice;if(routes.Count>0)await SearchRoutes();});b.Dock=DockStyle.Fill;b.Selected=i==0;b.Margin=new Padding(i==0?0:3,0,i==3?0:3,0);sortingButtons.Add(b);sorting.Controls.Add(b,i,0);}
        order.SelectedIndexChanged+=(_,_)=>{for(int i=0;i<sortingButtons.Count;i++){sortingButtons[i].Selected=i==order.SelectedIndex;sortingButtons[i].Invalidate();}};
        var filter=Flow();filter.AutoSize=false;filter.Height=31;filter.Padding=new Padding(0);filter.BackColor=Color.White;filter.Controls.Add(requireCapacity);
        requireCapacity.CheckedChanged+=async(_,_)=>{
            if(showingRoute)return;if(routeOptions!=null&&requireCapacity.Checked){routes=routes.Where(r=>r.Edges.Length==0||r.HasCapacity(routeOptions.Demand)).ToList();UpdateRouteSummary(new(routes,false,false,0,0));}
            if(routeOptions!=null&&!busy)try{await SearchRoutes();}catch(Exception ex){status.Text=ex.Message;}
        };
        var select=new Panel{Dock=DockStyle.Top,Height=42,BackColor=Color.White,Padding=new Padding(0,3,0,6)};schemeChoice.Font=new Font(Font,FontStyle.Bold);schemeChoice.BackColor=Theme.Tint;select.Controls.Add(schemeChoice);schemeChoice.SelectedIndexChanged+=(_,_)=>ShowRoute();
        var diagram=new Surface{Dock=DockStyle.Fill,Padding=new Padding(10)};diagram.Controls.Add(pathView);var diagramHeading=new Panel{Dock=DockStyle.Top,Height=30,BackColor=Color.White};diagramHeading.Controls.Add(new Label{Text="路径示意",Dock=DockStyle.Fill,Font=new Font(Font.FontFamily,10.5f,FontStyle.Bold),TextAlign=ContentAlignment.MiddleLeft});var tools=new FlowLayoutPanel{Dock=DockStyle.Right,Width=185,Height=29,FlowDirection=FlowDirection.RightToLeft,BackColor=Color.White};foreach(string label in new[]{"恢复默认","自动排布"}){var b=ActionButton(label,pathView.ResetLayout);b.Size=new(88,27);b.MinimumSize=new(72,26);b.Margin=new Padding(3,0,0,0);tools.Controls.Add(b);}diagramHeading.Controls.Add(tools);diagram.Controls.Add(diagramHeading);
        var details=Card("资源详情",segments);routeDetailsHost=Rows([55,45],diagram,details);var content=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};content.Controls.Add(routeDetailsHost);content.Controls.Add(select);
        right.Controls.Add(content);right.Controls.Add(filter);right.Controls.Add(sorting);right.Controls.Add(summary);tab.Controls.Add(Columns(25,left,75,right));
    }
    void UpdateRouteSummary(RouteResult result)
    {
        routeSummary.Text=$"候选路由  {routes.Count} 个方案 · "+(result.Cancelled?"已停止":result.Limited?"达到搜索上限":"搜索完成");
        schemeChoice.BeginUpdate();schemeChoice.Items.Clear();for(int i=0;i<routes.Count;i++){var r=routes[i];string capacity=r.Edges.Length>0?$"路由最小空闲芯数 {r.Bottleneck} 芯":"内部关系容量待核验";schemeChoice.Items.Add($"方案 {i+1:00}  ·  {r.CapacityStatus(routeOptions!.Demand)}  ·  {r.Length:0.##} 米  ·  跳纤 {r.Jumps} 次  ·  {capacity}");}schemeChoice.EndUpdate();if(schemeChoice.Items.Count>0)schemeChoice.SelectedIndex=0;
        if(routes.Count==0){segments.DataSource=null;pathView.Clear();schemeChoice.BackColor=Theme.Tint;}
    }
}
