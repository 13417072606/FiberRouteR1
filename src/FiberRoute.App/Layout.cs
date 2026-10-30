using FiberRoute.Core;
using System.Diagnostics;
using System.Text.Json;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly ComboBox resourceFormat=new UiComboBox(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly ComboBox sheets=new UiComboBox(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label inputInfo=new(){AutoSize=true,MaximumSize=new Size(300,0),Padding=new Padding(4)};
    readonly Label previewStats=new(){Dock=DockStyle.Top,Height=74,Padding=new Padding(8),BackColor=Color.White};
    string? previewKey;string detectedEncoding="自动";
    readonly List<Label[]> statViews=[];int splitSerial;
    readonly Dictionary<string,SplitContainer> savedSplits=[];
    string InputKey()=>importPath.Text+"\n"+encoding.Text+"\n"+sheets.Text+"\n"+resourceFormat.Text;
    static FlowLayoutPanel Stack()
    {
        var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(8),BackColor=Color.White};
        p.SizeChanged+=(_,_)=>{foreach(Control c in p.Controls)c.Width=Math.Max(120,p.ClientSize.Width-38*p.DeviceDpi/96);};p.ControlAdded+=(_,e)=>{if(e.Control!=null)e.Control.Width=Math.Max(120,p.ClientSize.Width-38*p.DeviceDpi/96);};return p;
    }
    static Control Field(string title,Control control){var p=new FlowLayoutPanel{Height=40,Width=285,WrapContents=false};p.Controls.Add(new Label{Text=title,Width=155,Margin=new Padding(3,7,3,3)});p.Controls.Add(control);return p;}
    static Control Card(string title,Control body){var p=new Surface{Dock=DockStyle.Fill,Padding=new Padding(10),Margin=new Padding(4)};body.Dock=DockStyle.Fill;p.Controls.Add(body);p.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=30,AutoEllipsis=true,BackColor=Color.White,Font=new Font("Microsoft YaHei UI",10.5f,FontStyle.Bold),ForeColor=Theme.Ink});return p;}
    Control Columns(float a,Control left,float b,Control right,string? layoutKey=null)=>Split(left,right,Orientation.Vertical,a/(a+b),layoutKey);
    Control Rows(float[] weights,params Control[] controls)
    {if(controls.Length==1)return controls[0];return Split(controls[0],Rows(weights.Skip(1).ToArray(),controls.Skip(1).ToArray()),Orientation.Horizontal,weights[0]/weights.Sum());}
    Control Split(Control first,Control second,Orientation orientation,float fraction,string? layoutKey=null)
    {
        string id=layoutKey??"split-v4-"+splitSerial++;var split=new SplitContainer{Dock=DockStyle.Fill,Orientation=orientation,SplitterWidth=12,BackColor=Theme.Canvas,Panel1MinSize=80,Panel2MinSize=80};first.Dock=second.Dock=DockStyle.Fill;split.Panel1.Controls.Add(first);split.Panel2.Controls.Add(second);savedSplits[id]=split;
        bool applied=false;split.SizeChanged+=(_,_)=>{int size=orientation==Orientation.Vertical?split.ClientSize.Width:split.ClientSize.Height;if(applied||size<400)return;applied=true;float ratio=float.TryParse(store.Setting("layout-"+id),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var old)?old:fraction;split.SplitterDistance=Math.Clamp((int)(size*ratio),80,size-92);};Shown+=(_,_)=>BeginInvoke((Action)(()=>{int size=orientation==Orientation.Vertical?split.ClientSize.Width:split.ClientSize.Height;if(size<200)return;float ratio=float.TryParse(store.Setting("layout-"+id),System.Globalization.NumberStyles.Float,System.Globalization.CultureInfo.InvariantCulture,out var old)?old:fraction;split.SplitterDistance=Math.Clamp((int)(size*ratio),80,size-92);}));return split;
    }
    Control Stats()
    {var p=new TableLayoutPanel{Dock=DockStyle.Top,Height=122,ColumnCount=4,RowCount=1,Padding=new Padding(0,0,0,12)};var labels=new Label[4];for(int i=0;i<4;i++){p.ColumnStyles.Add(new(SizeType.Percent,25));labels[i]=new StatTile{Dock=DockStyle.Fill,Margin=new Padding(i==0?0:6,0,i==3?0:6,0),Glyph=new[]{"link","network","database","ruler"}[i]};p.Controls.Add(labels[i],i,0);}statViews.Add(labels);return p;}
    void UpdateStats(){var g=store.Graph;string[] values=[$"中继段总数\n{g.Edges.Length:N0} 条",$"连接节点\n{g.Nodes.Length:N0} 个",$"空闲纤芯合计\n{g.Edges.Sum(e=>(long)e.Free):N0} 芯\n按中继段累加，非路由数",$"已记录总长度\n{g.Edges.Sum(e=>e.Length??0):N2} 米\n未知长度 {g.Edges.Count(e=>e.Length==null):N0} 段"];foreach(var view in statViews)for(int i=0;i<4;i++)view[i].Text=values[i];}
    Control BuildHeader()
    {
        var header=new TableLayoutPanel{Dock=DockStyle.Top,Height=72,Padding=new Padding(16,4,16,0),ColumnCount=3,RowCount=1,BackColor=Color.White};header.RowStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,300));header.ColumnStyles.Add(new(SizeType.Percent,100));header.ColumnStyles.Add(new(SizeType.Absolute,104));
        var brand=new Panel{Dock=DockStyle.Fill,BackColor=Color.White};var icon=Path.Combine(AppContext.BaseDirectory,"app.ico");var original=BrandAssets.Load();if(original.Icon!=null)Icon=original.Icon;else if(File.Exists(icon))Icon=new Icon(icon);if(Icon!=null){var logo=original.Image??Icon.ToBitmap();brand.Controls.Add(new PictureBox{Image=logo,SizeMode=PictureBoxSizeMode.Zoom,Size=new Size(40,40),Location=new Point(0,10)});FormClosed+=(_,_)=>logo.Dispose();}brand.Controls.Add(new Label{Text="光路调度路由查询",AutoSize=true,Font=new Font(Font.FontFamily,17,FontStyle.Bold),Location=new Point(50,15),ForeColor=Theme.Ink});header.Controls.Add(brand,0,0);
        var nav=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=4,RowCount=1,BackColor=Color.White};nav.RowStyles.Add(new(SizeType.Percent,100));
        for(int i=0;i<tabs.TabPages.Count;i++){int index=i;nav.ColumnStyles.Add(new(SizeType.Percent,25));var b=new UiButton{Text=tabs.TabPages[i].Text,Dock=DockStyle.Fill,Navigation=true,Selected=tabs.SelectedIndex==i,Font=new Font(Font.FontFamily,11,tabs.SelectedIndex==i?FontStyle.Bold:FontStyle.Regular),Margin=new Padding(6,0,6,0)};b.Click+=(_,_)=>tabs.SelectedIndex=index;tabs.SelectedIndexChanged+=(_,_)=>{b.Selected=tabs.SelectedIndex==index;b.Font=new Font(Font.FontFamily,11,b.Selected?FontStyle.Bold:FontStyle.Regular);b.Invalidate();};Shown+=(_,_)=>{b.Selected=tabs.SelectedIndex==index;b.Font=new Font(Font.FontFamily,11,b.Selected?FontStyle.Bold:FontStyle.Regular);b.Invalidate();};nav.Controls.Add(b,i,0);}header.Controls.Add(nav,1,0);
        var settings=ActionButton("设置",ShowSettings,"settings");settings.Dock=DockStyle.Fill;settings.Margin=new Padding(10,11,0,13);settings.Primary=false;header.Controls.Add(settings,2,0);return header;
    }
    sealed record ColumnState(string Name,int Width,int Order,bool Visible);
    sealed record GridState(ColumnState[] Columns,int Height,int Dpi=96);
    readonly Dictionary<DataGridView,GridState> gridStates=[];
    readonly HashSet<DataGridView> gridGestures=[];
    void InstallLayoutMemory()
    {
        int index=0;foreach(var grid in new[]{routeGrid,segments,resourceGrid,typeGrid,importGrid,typePreview,clusterGrid,assignmentGrid,roomGrid})
        {
            string key=(grid==clusterGrid?"grid-v8-":grid==resourceGrid?"grid-v6-":"grid-v4-")+index++;grid.Name=key;
            try{if(store.Setting(key) is string json)gridStates[grid]=JsonSerializer.Deserialize<GridState>(json)!;}catch{}
            PrepareGrid(grid);grid.DataBindingComplete+=(_,_)=>FitGrid(grid);
            grid.SizeChanged+=(_,_)=>{if(!gridStates.ContainsKey(grid))FitGrid(grid);else QueueRowMeasure(grid);};
            grid.FontChanged+=(_,_)=>{SizeGridHeaders(grid);QueueRowMeasure(grid);};grid.ColumnHeadersDefaultCellStyle.Font=new Font(Font.FontFamily,10,FontStyle.Bold);
            var menu=new ContextMenuStrip();menu.Items.Add("列设置…",null,(_,_)=>ShowColumns(grid));menu.Items.Add("自动适应内容",null,(_,_)=>{grid.AutoResizeColumns(DataGridViewAutoSizeColumnsMode.DisplayedCells);grid.AutoResizeRows(DataGridViewAutoSizeRowsMode.DisplayedCells);RememberGrid(grid);});menu.Items.Add("设置选中行高度…",null,(_,_)=>SetRowHeight(grid));menu.Items.Add("恢复默认布局",null,(_,_)=>{gridStates.Remove(grid);store.SetSetting(key,"");FitGrid(grid);});grid.ContextMenuStrip=menu;
            grid.MouseDown+=(_,e)=>{var hit=grid.HitTest(e.X,e.Y);if(e.Button==MouseButtons.Left&&hit.Type is DataGridViewHitTestType.ColumnHeader or DataGridViewHitTestType.RowHeader or DataGridViewHitTestType.TopLeftHeader)gridGestures.Add(grid);};grid.MouseUp+=(_,_)=>{if(gridGestures.Remove(grid))RememberGrid(grid);};
            grid.ColumnWidthChanged+=(_,_)=>{if(gridGestures.Contains(grid))RememberGrid(grid);if(!remembering)QueueRowMeasure(grid);};grid.ColumnDisplayIndexChanged+=(_,_)=>{if(gridGestures.Contains(grid))RememberGrid(grid);};grid.RowHeightChanged+=(_,_)=>{if(gridGestures.Contains(grid))RememberGrid(grid);};
        }
        FormClosing+=(_,_)=>{if(busy)return;foreach(var pair in gridStates)store.SetSetting(pair.Key.Name,JsonSerializer.Serialize(pair.Value));foreach(var pair in savedSplits){var s=pair.Value;int size=s.Orientation==Orientation.Vertical?s.ClientSize.Width:s.ClientSize.Height;if(size>0)store.SetSetting("layout-"+pair.Key,((float)s.SplitterDistance/size).ToString(System.Globalization.CultureInfo.InvariantCulture));}};
    }
    void SizeGridHeaders(DataGridView grid)
    {
        grid.AutoResizeColumnHeadersHeight();if(grid==resourceGrid||grid==clusterGrid)grid.ColumnHeadersHeight=Math.Max(grid.ColumnHeadersHeight,48*grid.DeviceDpi/96);
    }
    bool remembering;
    void FitGrid(DataGridView grid)
    {
        if(remembering||grid.Columns.Count==0)return;remembering=true;
        try
        {
            var savedState=gridStates.GetValueOrDefault(grid);
            float savedScale=grid.DeviceDpi/(float)Math.Max(1,savedState?.Dpi??grid.DeviceDpi);foreach(DataGridViewColumn c in grid.Columns){var saved=savedState?.Columns.FirstOrDefault(x=>x.Name==c.Name);c.MinimumWidth=(int)(48*grid.DeviceDpi/96f);c.AutoSizeMode=DataGridViewAutoSizeColumnMode.None;c.Visible=c.Name is "内部序号" or "内部标识"?false:saved?.Visible??!(grid==typeGrid&&c.Name=="原因");if(saved!=null)c.Width=Math.Max(c.MinimumWidth,(int)(saved.Width*savedScale));}
            if(savedState!=null){int orderIndex=0;foreach(var col in savedState.Columns.OrderBy(c=>c.Order)){if(grid.Columns.Contains(col.Name))grid.Columns[col.Name].DisplayIndex=orderIndex++;}}
            else foreach(DataGridViewColumn c in grid.Columns)c.DisplayIndex=c.Index;
            if(savedState==null)
            {
                var columns=grid.Columns.Cast<DataGridViewColumn>().Where(c=>c.Visible).ToArray();int Weight(DataGridViewColumn c)=>c.Name.Contains("设施")||c.Name is "名称" or "始端" or "终端" or "成员" or "集群名称" or "成员名单"?4:c.Name.Contains("ID")?2:1;int available=Math.Max(320,grid.ClientSize.Width-(grid.RowHeadersVisible?grid.RowHeadersWidth:0)-SystemInformation.VerticalScrollBarWidth-3),total=columns.Sum(Weight);
                int Initial(DataGridViewColumn c)
                {
                    if(grid==clusterGrid)return (int)((c.Name switch{"序号"=>48,"集群名称"=>208,"成员数"=>82,"类型"=>90,"确认状态"=>88,"路由状态"=>88,"原因"=>168,"成员名单"=>400,"所属站点"=>240,"操作"=>62,_=>70})*grid.DeviceDpi/96f);
                    if(grid==resourceGrid)return (int)((c.Name switch{"序号"=>54,"始端设施"=>210,"始端类型" or "终端类型"=>94,"始端区域" or "终端区域"=>90,"终端设施"=>244,"长度米"=>92,"空闲_总芯"=>104,"业务状态"=>88,"操作"=>64,_=>70})*grid.DeviceDpi/96f);
                    return (int)((c.Name.Contains("设施")||c.Name is "名称" or "始端" or "终端" or "成员" or "所属机房"?200:c.Name is "序号" or "操作"?70:c.Name=="空闲_总芯"?116:c.Name=="需求后余量"?120:c.Name is "确认状态" or "类型"?86:c.Name.Contains("ID")?150:100)*grid.DeviceDpi/96f);
                }
                int extra=Math.Max(0,available-columns.Sum(Initial));foreach(var c in columns)c.Width=Initial(c)+extra*Weight(c)/Math.Max(1,total);
            }
            MeasureRows(grid);SizeGridHeaders(grid);
        }finally{remembering=false;}
    }
    void RememberGrid(DataGridView grid){if(remembering||grid.Columns.Count==0||!Visible)return;remembering=true;try{gridStates[grid]=new(grid.Columns.Cast<DataGridViewColumn>().Select(c=>new ColumnState(c.Name,c.Width,c.DisplayIndex,c.Visible)).ToArray(),rowMinimum.GetValueOrDefault(grid,gridStates.GetValueOrDefault(grid)?.Height??38*grid.DeviceDpi/96),grid.DeviceDpi);}finally{remembering=false;}}
    void SetRowHeight(DataGridView grid)
    {
        if(grid.CurrentRow==null)return;using var d=new Form{Text="行高",Width=340,Height=175,Font=Font,StartPosition=FormStartPosition.CenterParent};var n=Num(24,600,Math.Clamp(grid.CurrentRow.Height,24,600));n.Dock=DockStyle.Top;d.Controls.Add(n);var ok=B("应用到本表",()=>{rowMinimum[grid]=(int)n.Value;RememberGrid(grid);MeasureRows(grid);d.Close();});ok.Dock=DockStyle.Bottom;d.Controls.Add(ok);d.ShowDialog(this);
    }
    void ShowColumns(DataGridView grid){using var d=new Form{Text="列设置 · 拖动表头可调整顺序",Width=420,Height=520,Font=Font};var list=new CheckedListBox{Dock=DockStyle.Fill,CheckOnClick=true};foreach(DataGridViewColumn c in grid.Columns)list.Items.Add(c.HeaderText,c.Visible);var ok=B("应用",()=>{for(int i=0;i<grid.Columns.Count;i++)grid.Columns[i].Visible=list.GetItemChecked(i);RememberGrid(grid);d.Close();});ok.Dock=DockStyle.Bottom;d.Controls.Add(list);d.Controls.Add(ok);d.ShowDialog(this);}
    void CreateDesktopShortcut()
    {
        string executable=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","光路调度路由查询.exe"));if(!File.Exists(executable))executable=Application.ExecutablePath;
        Type type=Type.GetTypeFromProgID("WScript.Shell")??throw new InvalidOperationException("系统快捷方式组件不可用");object shell=Activator.CreateInstance(type)!;object? shortcut=null;
        try{dynamic host=shell;shortcut=host.CreateShortcut(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory),"光路调度路由查询.lnk"));dynamic link=shortcut;link.TargetPath=executable;link.WorkingDirectory=Path.GetDirectoryName(executable);link.IconLocation=File.Exists(BrandAssets.IconPath)?BrandAssets.IconPath:executable;link.Save();status.Text="已创建桌面快捷方式";}
        finally{if(shortcut!=null)System.Runtime.InteropServices.Marshal.ReleaseComObject(shortcut);System.Runtime.InteropServices.Marshal.ReleaseComObject(shell);}
    }
    void ShowSettings()
    {
        if(busy){MessageBox.Show("请先等待任务结束。");return;}using var dialog=BuildSettingsDialog();dialog.ShowDialog(this);
    }
}
