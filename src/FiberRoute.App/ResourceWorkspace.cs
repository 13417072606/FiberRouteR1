using System.Text.Json;
using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    int resourcePageSize=100;readonly ComboBox pageSizeChoice=new UiComboBox(){Width=105};
    Control PageIntro(string title,string subtitle)
    {
        var p=new Panel{Dock=DockStyle.Top,Height=70,BackColor=Theme.Canvas};var heading=Title(title,18);heading.Location=new Point(2,2);p.Controls.Add(heading);p.Controls.Add(new Label{Text=subtitle,AutoSize=true,ForeColor=Theme.Muted,Location=new Point(3,40)});return p;
    }
    void ResourceDetail()
    {
        if(resourceGrid.CurrentRow?.Cells["内部序号"].Value is not int ix)return;
        using var f=new Form{Text="中继段完整详情",Width=850,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var t=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both,BorderStyle=BorderStyle.None,BackColor=Color.White};
        t.Text=JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(store.Raw(ix)),new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});f.Controls.Add(t);f.ShowDialog(this);
    }
    void BuildResourceWorkspace()
    {
        var tab=Tab("中继段资源");var surface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};resourceSearch.PlaceholderText="搜索名称、ID、设施、站点或机房";
        var top=new Panel{Dock=DockStyle.Top,Height=102,BackColor=Color.White};
        var first=Flow();first.AutoSize=false;first.Height=50;first.Padding=Padding.Empty;var input=InputFrame(resourceSearch);input.Width=330;
        first.Controls.AddRange([input,L("始端区域"),resourceStartArea,L("终端区域"),resourceEndArea,B("重置",()=>{buildingFilters=true;resourceSearch.Clear();resourceType.SelectedItem="全部";resourceStartArea.SelectedIndex=resourceEndArea.SelectedIndex=resourceBusiness.SelectedIndex=0;minFree.Value=0;buildingFilters=false;page.Value=1;LoadResources();})]);
        var second=Flow();second.AutoSize=false;second.Height=48;second.Padding=Padding.Empty;second.Controls.AddRange([L("端点类型"),resourceType,L("最少空闲芯数"),minFree,L("业务状态"),resourceBusiness,B("导出筛选结果",ExportResources)]);second.Dock=DockStyle.Bottom;top.Controls.Add(second);top.Controls.Add(first);
        ConfigurePager(resourcePager,"resources",()=>{page.Value=resourcePager.Page;LoadResources();});
        Sortable(resourceGrid,()=>{page.Value=1;LoadResources();});
        foreach(var filter in new[]{resourceStartArea,resourceEndArea,resourceBusiness})filter.SelectedIndexChanged+=(_,_)=>{if(!buildingFilters&&IsHandleCreated){page.Value=1;LoadResources();}};
        surface.Controls.Add(resourceGrid);surface.Controls.Add(resourcePager);surface.Controls.Add(top);tab.Controls.Add(surface);tab.Controls.Add(Stats());tab.Controls.Add(PageIntro("中继段资源","查询导入的中继段，核对两端设施与纤芯资源"));
        var timer=new System.Windows.Forms.Timer{Interval=350};resourceSearch.TextChanged+=(_,_)=>{timer.Stop();timer.Start();};timer.Tick+=(_,_)=>{timer.Stop();page.Value=1;LoadResources();};FormClosed+=(_,_)=>timer.Dispose();resourceType.SelectedIndexChanged+=(_,_)=>{if(IsHandleCreated){page.Value=1;LoadResources();}};minFree.ValueChanged+=(_,_)=>{if(IsHandleCreated){page.Value=1;LoadResources();}};
        resourceGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)ResourceDetail();};resourceGrid.CellContentClick+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&resourceGrid.Columns[e.ColumnIndex].Name=="操作")ResourceDetail();};resourceGrid.CellPainting+=PaintResourceFacility;
    }
    void PaintResourceFacility(object? sender,DataGridViewCellPaintingEventArgs e)
    {
        if(e.RowIndex<0||e.ColumnIndex<0)return;var name=resourceGrid.Columns[e.ColumnIndex].Name;
        if(name=="操作"){e.PaintBackground(e.ClipBounds,true);TextRenderer.DrawText(e.Graphics!,"详情",e.CellStyle?.Font??Font,e.CellBounds,Theme.Teal,TextFormatFlags.HorizontalCenter|TextFormatFlags.VerticalCenter);e.Handled=true;return;}
        if(name is not ("始端设施" or "终端设施"))return;
        if(resourceGrid.Rows[e.RowIndex].Cells["内部序号"].Value is not int index||index<0||index>=store.Graph.Edges.Length)return;
        var edge=store.Graph.Edges[index];var node=store.Graph.Nodes[name=="始端设施"?edge.A:edge.B];e.PaintBackground(e.ClipBounds,true);
        var font=e.CellStyle?.Font??Font;int badgeWidth=Math.Min(92*DeviceDpi/96,TextRenderer.MeasureText(node.Type,font).Width+14);var bounds=e.CellBounds;
        var text=new Rectangle(bounds.X+9,bounds.Y+4,Math.Max(36,bounds.Width-badgeWidth-25),bounds.Height-8);TextRenderer.DrawText(e.Graphics!,WrapCellText(node.Name,font,text.Width),font,text,Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.WordBreak|TextFormatFlags.NoPrefix);
        Theme.Badge(e.Graphics!,node.Type,font,new Rectangle(bounds.Right-badgeWidth-7,bounds.Y+(bounds.Height-25*DeviceDpi/96)/2,badgeWidth,25*DeviceDpi/96));e.Handled=true;
    }
}
internal sealed class StatTile:Label
{
    public string Glyph {get;set;}="list";
    public StatTile(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Canvas;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float s=DeviceDpi/96f;g.Clear(Theme.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),8*s);using var white=new SolidBrush(Color.White);using var pen=new Pen(Theme.Line);g.FillPath(white,path);g.DrawPath(pen,path);
        var icon=new Rectangle((int)(13*s),(Height-(int)(44*s))/2,(int)(44*s),(int)(44*s));using(var p=Theme.Round(icon,7*s))using(var b=new SolidBrush(Color.FromArgb(236,247,255)))g.FillPath(b,p);Theme.Glyph(g,Glyph,Rectangle.Inflate(icon,-10*(int)Math.Max(1,s),-10*(int)Math.Max(1,s)),Color.FromArgb(0,112,193));
        var lines=Text.Split('\n');int x=(int)(68*s),width=Math.Max(20,Width-x-(int)(12*s));using var normal=new Font(Font.FontFamily,9.5f);string value=lines.ElementAtOrDefault(1)??"";
        float size=18;Font strong=new(Font.FontFamily,size,FontStyle.Bold);while(TextRenderer.MeasureText(g,value,strong).Width>width&&size>10.5f){strong.Dispose();size-=.5f;strong=new(Font.FontFamily,size,FontStyle.Bold);}
        using(strong){TextRenderer.DrawText(g,lines.ElementAtOrDefault(0)??"",normal,new Rectangle(x,(int)(13*s),width,(int)(24*s)),Theme.Muted,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,value,strong,new Rectangle(x,(int)(39*s),width,(int)(33*s)),Theme.Ink,TextFormatFlags.VerticalCenter|TextFormatFlags.NoPadding);}
        if(lines.Length>2)TextRenderer.DrawText(g,lines[2],normal,new Rectangle(x,(int)(74*s),width,(int)(19*s)),Theme.Muted,TextFormatFlags.EndEllipsis);
    }
}
