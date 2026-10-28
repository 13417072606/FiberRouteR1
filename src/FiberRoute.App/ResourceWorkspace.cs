using System.Text.Json;
using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    int resourcePageSize=100;readonly ComboBox pageSizeChoice=new UiComboBox(){Width=105};
    Control PageIntro(string title,string subtitle)
    {
        var p=new Panel{Dock=DockStyle.Top,Height=80,BackColor=Theme.Canvas};var heading=Title(title,22);heading.Location=new Point(2,2);p.Controls.Add(heading);p.Controls.Add(new Label{Text=subtitle,AutoSize=true,ForeColor=Theme.Muted,Location=new Point(3,45)});return p;
    }
    void ResourceDetail()
    {
        if(resourceGrid.CurrentRow?.Cells["内部序号"].Value is not int ix)return;
        using var f=new Form{Text="中继段完整详情",Width=850,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var t=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,BorderStyle=BorderStyle.None,BackColor=Color.White};
        t.Text=JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(store.Raw(ix)),new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});f.Controls.Add(t);f.ShowDialog(this);
    }
    void BuildResourceWorkspace()
    {
        var tab=Tab("中继段资源");resourceGrid.ColumnHeadersHeightSizeMode=DataGridViewColumnHeadersHeightSizeMode.DisableResizing;resourceGrid.ColumnHeadersDefaultCellStyle.Padding=new Padding(5,8,5,8);resourceGrid.ColumnHeadersDefaultCellStyle.WrapMode=DataGridViewTriState.False;var surface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};resourceSearch.PlaceholderText="搜索名称、ID、设施、站点或机房";
        var fields=new List<(Panel Panel,Label Label,Control Input,int Width)>();
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,WrapContents=false,BackColor=Color.White,Padding=new Padding(0,4,0,10),Margin=Padding.Empty};
        resourceSearch.PlaceholderText="搜索名称、ID、设施";var input=InputFrame(resourceSearch);input.Width=180;input.Margin=new Padding(0,0,6,0);resourceType.Width=86;foreach(var combo in new[]{resourceType,resourceEndType,resourceStartArea,resourceEndArea,resourceBusiness})combo.Font=new Font(Font.FontFamily,9);
        Control Compact(string caption,Control control,int width)
        {
            var label=new Label{Text=caption,AutoSize=false,TextAlign=ContentAlignment.MiddleLeft,BackColor=Color.White,Dock=DockStyle.Left};label.Font=new Font(Font.FontFamily,9f);label.AutoEllipsis=true;label.Width=(int)Math.Ceiling(TextRenderer.MeasureText(caption,label.Font).Width*96f/DeviceDpi)+10;
            var panel=new Panel{Width=label.Width+width,Height=40,Margin=new Padding(0,0,6,0),BackColor=Color.White};control.Dock=DockStyle.None;control.Width=width;panel.Controls.Add(control);panel.Controls.Add(label);void Place(){control.Left=label.Width;control.Top=Math.Max(0,(panel.Height-control.Height)/2);}panel.SizeChanged+=(_,_)=>Place();fields.Add((panel,label,control,width));Place();return panel;
        }
        minFree.BorderStyle=BorderStyle.None;var count=InputFrame(minFree);count.Width=52;
        var reset=new UiButton{Text="重置",Width=60,Height=40,Margin=new Padding(0,0,6,0)};reset.Click+=(_,_)=>{buildingFilters=true;resourceSearch.Clear();resourceType.SelectedIndex=resourceEndType.SelectedIndex=resourceStartArea.SelectedIndex=resourceEndArea.SelectedIndex=resourceBusiness.SelectedIndex=0;minFree.Value=0;buildingFilters=false;page.Value=1;LoadResources();};
        var export=new UiButton{Text="导出结果",Glyph="export",Primary=true,Width=112,Height=40,Margin=Padding.Empty};export.Click+=async(_,_)=>{try{await ExportResources();}catch(Exception e){MessageBox.Show(e.Message);}};
        top.Controls.AddRange([input,Compact("始端区域",resourceStartArea,100),Compact("始端类型",resourceType,100),Compact("终端区域",resourceEndArea,100),Compact("终端类型",resourceEndType,100),Compact("最少空闲芯数",count,52),Compact("业务状态",resourceBusiness,100),reset,export]);
        bool fitting=false;
        void FitFilters()
        {
            if(fitting||top.IsDisposed)return;fitting=true;try{float scale=top.DeviceDpi/96f;foreach(var f in fields){f.Label.Width=TextRenderer.MeasureText(f.Label.Text,f.Label.Font,Size.Empty,TextFormatFlags.NoPadding).Width+(int)(8*scale);f.Input.Width=(int)(f.Width*scale);f.Panel.Width=f.Label.Width+(int)(f.Width*scale);f.Panel.Height=(int)(42*scale);f.Input.Left=f.Label.Width;f.Input.Top=Math.Max(0,(f.Panel.Height-f.Input.Height)/2);}
                int rest=top.Controls.Cast<Control>().Where(c=>c!=input).Sum(c=>c.Width+c.Margin.Horizontal);int available=top.ClientSize.Width-top.Padding.Horizontal-rest-input.Margin.Horizontal;int minimum=(int)(160*scale);top.WrapContents=available<minimum;input.Width=Math.Max(minimum,available);input.Height=(int)(42*scale);
            }finally{fitting=false;}
        }
        top.Layout+=(_,_)=>FitFilters();top.SizeChanged+=(_,_)=>FitFilters();top.DpiChangedAfterParent+=(_,_)=>FitFilters();Shown+=(_,_)=>FitFilters();
        ConfigurePager(resourcePager,"resources",()=>{page.Value=resourcePager.Page;LoadResources();});
        Sortable(resourceGrid,()=>{page.Value=1;LoadResources();});
        foreach(var filter in new[]{resourceStartArea,resourceEndArea,resourceBusiness,resourceEndType})filter.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters&&IsHandleCreated){page.Value=1;LoadResources();}};
        surface.Controls.Add(resourceGrid);surface.Controls.Add(resourcePager);surface.Controls.Add(top);tab.Controls.Add(surface);tab.Controls.Add(Stats());tab.Controls.Add(PageIntro("中继段资源","查询导入的中继段，核对两端设施与纤芯资源"));
        var timer=new System.Windows.Forms.Timer{Interval=350};resourceSearch.TextChanged+=(_,_)=>{timer.Stop();timer.Start();};timer.Tick+=(_,_)=>{timer.Stop();page.Value=1;LoadResources();};FormClosed+=(_,_)=>timer.Dispose();resourceType.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters&&IsHandleCreated){page.Value=1;LoadResources();}};minFree.ValueChanged+=(_,_)=>{if(!buildingFilters&&IsHandleCreated){page.Value=1;LoadResources();}};
        resourceGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)ResourceDetail();};resourceGrid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&resourceGrid.Columns[e.ColumnIndex].Name=="操作")ResourceDetail();};resourceGrid.CellPainting+=PaintResourceFacility;resourceGrid.RowPostPaint+=(_,e)=>{using var line=new Pen(Color.FromArgb(235,240,246));e.Graphics.DrawLine(line,0,e.RowBounds.Bottom-1,resourceGrid.ClientSize.Width,e.RowBounds.Bottom-1);};
    }
    void PaintTableHeader(DataGridView grid,DataGridViewCellPaintingEventArgs e)
    {
            string name=grid.Columns[e.ColumnIndex].Name;float scale=grid.DeviceDpi/96f;bool sortable=name is not ("操作" or "序号" or "内部序号");using var background=new SolidBrush(Color.FromArgb(239,244,250));e.Graphics!.FillRectangle(background,e.CellBounds);var bounds=new Rectangle(e.CellBounds.X+(int)(9*scale),e.CellBounds.Y,Math.Max(10,e.CellBounds.Width-(int)((sortable?30:18)*scale)),e.CellBounds.Height);TextRenderer.DrawText(e.Graphics,name=="长度米"?"长度\n（米）":grid.Columns[e.ColumnIndex].HeaderText,e.CellStyle?.Font??Font,bounds,Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.WordBreak);
            if(sortable){float x=e.CellBounds.Right-13*scale,y=e.CellBounds.Y+e.CellBounds.Height/2f;var direction=grid.Columns[e.ColumnIndex].HeaderCell.SortGlyphDirection;using var brush=new SolidBrush(direction==SortOrder.None?Theme.Muted:Theme.Teal);if(direction!=SortOrder.Descending)e.Graphics.FillPolygon(brush,new[]{new PointF(x,y-5*scale),new PointF(x-3*scale,y-1*scale),new PointF(x+3*scale,y-1*scale)});if(direction!=SortOrder.Ascending)e.Graphics.FillPolygon(brush,new[]{new PointF(x,y+5*scale),new PointF(x-3*scale,y+1*scale),new PointF(x+3*scale,y+1*scale)});}e.Handled=true;return;
    }
    void PaintResourceFacility(object? sender,DataGridViewCellPaintingEventArgs e)
    {
        if(e.ColumnIndex<0)return;var name=resourceGrid.Columns[e.ColumnIndex].Name;
        if(e.RowIndex<0)
        {
            PaintTableHeader(resourceGrid,e);return;
        }
        if(name=="操作"){e.PaintBackground(e.ClipBounds,true);TextRenderer.DrawText(e.Graphics!,"详情",e.CellStyle?.Font??Font,e.CellBounds,Theme.Teal,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;return;}

    }
}
internal sealed class StatTile:Label
{
    public string Glyph {get;set;}="list";
    public StatTile(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Canvas;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float s=DeviceDpi/96f;g.Clear(Theme.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),8*s);using var white=new SolidBrush(Color.White);using var pen=new Pen(Theme.Line);g.FillPath(white,path);g.DrawPath(pen,path);
        var colors=Glyph switch{"link"=>(Color.FromArgb(210,246,246),Color.FromArgb(0,156,165)),"network"=>(Color.FromArgb(220,234,255),Color.FromArgb(0,112,224)),"database"=>(Color.FromArgb(214,248,234),Color.FromArgb(0,158,106)),_=>(Color.FromArgb(255,236,211),Color.FromArgb(238,142,0))};
        var icon=new Rectangle((int)(16*s),(Height-(int)(58*s))/2,(int)(58*s),(int)(58*s));using(var brush=new SolidBrush(colors.Item1))g.FillEllipse(brush,icon);Theme.Glyph(g,Glyph,Rectangle.Inflate(icon,-(int)(15*s),-(int)(15*s)),colors.Item2);
        var lines=Text.Split('\n');int x=(int)(88*s),width=Math.Max(20,Width-x-(int)(12*s));using var normal=new Font(Font.FontFamily,9f);string value=lines.ElementAtOrDefault(1)??"";
        float size=20;Font strong=new(Font.FontFamily,size,FontStyle.Bold);while(TextRenderer.MeasureText(g,value,strong).Width>width&&size>10.5f){strong.Dispose();size-=.5f;strong=new(Font.FontFamily,size,FontStyle.Bold);}
        using(strong){TextRenderer.DrawText(g,lines.ElementAtOrDefault(0)??"",normal,new Rectangle(x,(int)(13*s),width,(int)(24*s)),Theme.Muted,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,value,strong,new Rectangle(x,(int)(39*s),width,(int)(33*s)),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);}
        if(lines.Length>2)TextRenderer.DrawText(g,lines[2],normal,new Rectangle(x,(int)(74*s),width,(int)(19*s)),Theme.Muted,TextFormatFlags.EndEllipsis);
    }
}
