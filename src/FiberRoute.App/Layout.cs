using FiberRoute.Core;
using System.Diagnostics;
using System.Text.Json;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly ComboBox resourceFormat=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox sheets=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label inputInfo=new(){AutoSize=true,MaximumSize=new Size(300,0),Padding=new Padding(4)};
    readonly Label previewStats=new(){Dock=DockStyle.Top,Height=74,Padding=new Padding(12),BackColor=Color.White};
    string? previewKey;string detectedEncoding="自动";
    readonly List<Label[]> statViews=[];int splitSerial;
    readonly Dictionary<string,SplitContainer> savedSplits=[];
    string InputKey()=>importPath.Text+"\n"+encoding.Text+"\n"+sheets.Text+"\n"+resourceFormat.Text;
    static FlowLayoutPanel Stack()
    {
        var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12),BackColor=Color.White};
        p.SizeChanged+=(_,_)=>{foreach(Control c in p.Controls)c.Width=Math.Max(120,p.ClientSize.Width-38);};p.ControlAdded+=(_,e)=>{if(e.Control!=null)e.Control.Width=Math.Max(120,p.ClientSize.Width-38);};return p;
    }
    static Control Field(string title,Control control){var p=new FlowLayoutPanel{Height=40,Width=285,WrapContents=false};p.Controls.Add(new Label{Text=title,Width=155,Margin=new Padding(3,7,3,3)});p.Controls.Add(control);return p;}
    static Control Card(string title,Control body){var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(10),Margin=new Padding(4),BackColor=Color.White};body.Dock=DockStyle.Fill;p.Controls.Add(body);p.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=34,AutoEllipsis=true,Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold)});return p;}
    Control Columns(float a,Control left,float b,Control right)=>Split(left,right,Orientation.Vertical,a/(a+b));
    Control Rows(float[] weights,params Control[] controls)
    {if(controls.Length==1)return controls[0];return Split(controls[0],Rows(weights.Skip(1).ToArray(),controls.Skip(1).ToArray()),Orientation.Horizontal,weights[0]/weights.Sum());}
    Control Split(Control first,Control second,Orientation orientation,float fraction)
    {
        string id="split-"+splitSerial++;var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=orientation,SplitterWidth=7,BackColor=Color.FromArgb(233,240,242)};first.Dock=second.Dock=DockStyle.Fill;split.Panel1.Controls.Add(first);split.Panel2.Controls.Add(second);savedSplits[id]=split;
        bool applied=false;split.SizeChanged+=(_,_)=>{int size=orientation==Orientation.Vertical?split.ClientSize.Width:split.ClientSize.Height;if(applied||size<400)return;applied=true;float ratio=float.TryParse(store.Setting("layout-"+id),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var old)?old:fraction;split.SplitterDistance=Math.Clamp((int)(size*ratio),80,size-90);};return split;
    }
    Control Stats()
    {var p=new TableLayoutPanel{Dock=DockStyle.Top,Height=88,ColumnCount=4,RowCount=1,Padding=new Padding(0,0,0,8)};var labels=new Label[4];for(int i=0;i<4;i++){p.ColumnStyles.Add(new(SizeType.Percent,25));labels[i]=new(){Dock=DockStyle.Fill,Padding=new Padding(12),BackColor=Color.White,Margin=new Padding(4),Font=new Font(Font.FontFamily,11),AutoEllipsis=true};p.Controls.Add(labels[i],i,0);}statViews.Add(labels);return p;}
    void UpdateStats(){var g=store.Graph;string[] values=[$"中继段总数\n{g.Edges.Length:N0}",$"连接节点\n{g.Nodes.Length:N0}",$"空闲逐段累计（非可用路由数）\n{g.Edges.Sum(e=>(long)e.Free):N0} 芯",$"已记录长度 / 未知长度\n{g.Edges.Sum(e=>e.Length??0):N2} 米 / {g.Edges.Count(e=>e.Length==null):N0} 段"];foreach(var view in statViews)for(int i=0;i<4;i++)view[i].Text=values[i];}
    Control BuildHeader()
    {
        var header=new FlowLayoutPanel{Dock=DockStyle.Top,Height=76,Padding=new Padding(14,12,6,6),WrapContents=false,BackColor=Color.White};
        var icon=Path.Combine(AppContext.BaseDirectory,"app.ico");if(File.Exists(icon)){Icon=new Icon(icon);header.Controls.Add(new PictureBox{Image=Icon.ToBitmap(),SizeMode=PictureBoxSizeMode.Zoom,Width=42,Height=42});}
        header.Controls.Add(new Label{Text="光路调度智能分析",AutoSize=true,Font=new Font(Font.FontFamily,15,FontStyle.Bold),Margin=new Padding(6,6,24,4),ForeColor=Color.FromArgb(25,45,60)});
        for(int i=0;i<tabs.TabPages.Count;i++){int index=i;var b=new Button{Text=tabs.TabPages[i].Text,Width=112,Height=42,FlatStyle=FlatStyle.Flat,BackColor=Color.White,ForeColor=accent};b.FlatAppearance.BorderSize=0;b.Click+=(_,_)=>tabs.SelectedIndex=index;tabs.SelectedIndexChanged+=(_,_)=>{b.BackColor=tabs.SelectedIndex==index?Color.FromArgb(223,243,243):Color.White;};header.Controls.Add(b);}
        header.Controls.Add(B("设置",ShowSettings));return header;
    }
    sealed record ColumnState(string Name,int Width,int Order,bool Visible);
    sealed record GridState(ColumnState[] Columns,int Height);
    readonly Dictionary<DataGridView,GridState> gridStates=[];
    void InstallLayoutMemory()
    {
        int index=0;foreach(var grid in new[]{routeGrid,segments,resourceGrid,typeGrid,importGrid,typePreview,clusterGrid,assignmentGrid})
        {
            string key="grid-"+index++;grid.Name=key;
            try{if(store.Setting(key) is string json)gridStates[grid]=JsonSerializer.Deserialize<GridState>(json)!;}catch{}
            grid.DataBindingComplete+=(_,_)=>{if(grid.Columns.Count==0)return;remembering=true;if(!gridStates.ContainsKey(grid)){foreach(DataGridViewColumn col in grid.Columns){if(grid==typeGrid&&(col.Name=="来源"||col.Name=="原因"))col.Visible=false;if(grid==resourceGrid&&(col.Name=="内部序号"||col.Name=="ID"||col.Name=="名称"))col.Visible=false;}}var existing=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).ToArray();int available=Math.Max(700,grid.ClientSize.Width-45);foreach(var c in existing){var saved=gridStates.GetValueOrDefault(grid)?.Columns.FirstOrDefault(x=>x.Name==c.Name);c.MinimumWidth=48;c.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;int weight=c.Name.Contains("设施")||c.Name=="名称"||c.Name=="始端"||c.Name=="终端"?3:1;int totalWeight=existing.Sum(col=>col.Name.Contains("设施")||col.Name=="名称"||col.Name=="始端"||col.Name=="终端"?3:1);c.Width=saved?.Width??Math.Max(75,available*weight/Math.Max(1,totalWeight));if(saved!=null){c.Visible=saved.Visible;try{c.DisplayIndex=Math.Min(saved.Order,grid.Columns.Count-1);}catch{}}}int h=gridStates.GetValueOrDefault(grid)?.Height??48;foreach(DataGridViewRow row in grid.Rows)row.Height=Math.Clamp(h,24,400);if(!gridStates.ContainsKey(grid))grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCells);remembering=false;};
            var menu=new ContextMenuStrip();menu.Items.Add("列设置…",null,(_,_)=>ShowColumns(grid));menu.Items.Add("自动适应内容",null,(_,_)=>{grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCells);});menu.Items.Add("恢复默认布局",null,(_,_)=>{gridStates.Remove(grid);store.SetSetting(key,"");foreach(DataGridViewColumn c in grid.Columns){c.Visible=true;c.Width=Math.Max(80,(grid.ClientSize.Width-45)/Math.Max(1,grid.Columns.Count));}foreach(DataGridViewRow row in grid.Rows)row.Height=48;});grid.ContextMenuStrip=menu;
            grid.ColumnWidthChanged+=(_,_)=>RememberGrid(grid);grid.ColumnDisplayIndexChanged+=(_,_)=>RememberGrid(grid);grid.RowHeightChanged+=(_,_)=>RememberGrid(grid);
        }
        FormClosing+=(_,_)=>{if(busy)return;foreach(var pair in gridStates)store.SetSetting(pair.Key.Name,JsonSerializer.Serialize(pair.Value));foreach(var pair in savedSplits){var s=pair.Value;int size=s.Orientation==Orientation.Vertical?s.ClientSize.Width:s.ClientSize.Height;if(size>0)store.SetSetting("layout-"+pair.Key,((float)s.SplitterDistance/size).ToString(System.Globalization.CultureInfo.InvariantCulture));}};
    }
    bool remembering;
    void RememberGrid(DataGridView grid){if(remembering||grid.Columns.Count==0||!Visible)return;remembering=true;try{gridStates[grid]=new(grid.Columns.Cast<DataGridViewColumn>().Select(c=>new ColumnState(c.Name,c.Width,c.DisplayIndex,c.Visible)).ToArray(),grid.CurrentRow?.Height??48);}finally{remembering=false;}}
    void ShowColumns(DataGridView grid){using var d=new Form{Text="列设置 · 拖动表头可调整顺序",Width=420,Height=520,Font=Font};var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(DataGridViewColumn c in grid.Columns)list.Items.Add(c.HeaderText,c.Visible);var ok=B("应用",()=>{for(int i=0;i<grid.Columns.Count;i++)grid.Columns[i].Visible=list.GetItemChecked(i);RememberGrid(grid);d.Close();});ok.Dock=DockStyle.Bottom;d.Controls.Add(list);d.Controls.Add(ok);d.ShowDialog(this);}
    void ShowSettings()
    {
        if(busy){MessageBox.Show("请先等待任务结束。");return;}using var d=new Form{Text="数据与布局设置",Width=650,Height=400,Font=Font};var panel=Stack();var info=L("当前数据库目录："+store.Root);info.MaximumSize=new Size(580,0);panel.Controls.AddRange([info,L("数据库与程序分开保存，可选择D盘；原始Excel不重复留存。"),B("打开数据目录",()=>Process.Start(new ProcessStartInfo(store.Root){UseShellExecute=true})),B("选择数据目录并重启",()=>{using var pick=new FolderBrowserDialog{Description="选择包含resources.db和personal.db的数据文件夹，或新的空文件夹"};if(pick.ShowDialog()!=DialogResult.OK)return;string root=Path.GetFullPath(pick.SelectedPath);_=new Store(root);Program.SaveRoot(root);var restart=new ProcessStartInfo(Application.ExecutablePath){UseShellExecute=false};restart.ArgumentList.Add("--wait-pid");restart.ArgumentList.Add(Environment.ProcessId.ToString());Process.Start(restart);Application.Exit();}),B("恢复表格和分隔区布局",()=>{foreach(var g in gridStates.Keys.ToArray())store.SetSetting(g.Name,"");foreach(var key in savedSplits.Keys)store.SetSetting("layout-"+key,"");gridStates.Clear();MessageBox.Show("已恢复，下次打开应用默认布局。");}),L("R1.3 · 支持普通公网环境，无需运营商内网\n本版未包含云端更新中心。")]);d.Controls.Add(panel);d.ShowDialog(this);
    }
}
public sealed class PathDiagram:ScrollableControl
{
    List<(string Name,string Type,bool Dashed)> nodes=[];
    public PathDiagram(){DoubleBuffered=true;AutoScroll=true;BackColor=Color.White;}
    public void SetRoute(Graph g,Route r)
    {
        nodes=[];void Add(int id,bool dashed)=>nodes.Add((g.Nodes[id].Name,g.Nodes[id].Type,dashed));
        if(r.Steps.Length>0){Add(r.Nodes[0],false);foreach(var s in r.Steps){if(nodes[^1].Name!=g.Nodes[s.From].Name)Add(s.From,true);Add(s.To,false);}foreach(var t in r.Transfers.Where(t=>t.BeforeSegment==r.Edges.Length))Add(t.To,true);}
        else if(r.Transfers.Length>0){Add(r.Transfers[0].From,false);foreach(var t in r.Transfers)Add(t.To,true);}else foreach(var n in r.Nodes)Add(n,false);
        AutoScrollMinSize=new Size(nodes.Count*260+30,180);Invalidate();
    }
    public void Clear(){nodes=[];AutoScrollMinSize=Size.Empty;Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var p=new Pen(Color.FromArgb(8,126,128),2);using var b=new SolidBrush(Color.FromArgb(235,246,246));
        if(nodes.Count==0){e.Graphics.DrawString("选择方案查看连接顺序 · 实线为中继段，虚线为集群关系",Font,Brushes.Gray,14,24);return;}
        for(int i=0;i<nodes.Count;i++){int x=22+i*260;if(i>0){p.DashStyle=nodes[i].Dashed?System.Drawing.Drawing2D.DashStyle.Dash:System.Drawing.Drawing2D.DashStyle.Solid;e.Graphics.DrawLine(p,x-210,35,x,35);}p.DashStyle=System.Drawing.Drawing2D.DashStyle.Solid;e.Graphics.FillRectangle(b,x,12,42,42);e.Graphics.DrawRectangle(p,x+10,20,22,27);e.Graphics.DrawString((i+1).ToString(),Font,Brushes.Teal,x+14,23);e.Graphics.DrawString(nodes[i].Name,Font,Brushes.Black,new RectangleF(x,70,235,75));e.Graphics.DrawString(nodes[i].Type,Font,Brushes.DimGray,x,151);}
    }
}
