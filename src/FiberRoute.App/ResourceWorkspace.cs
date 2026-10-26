using System.Text.Json;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    Control PageIntro(string title,string subtitle)
    {
        var p=new Panel{Dock=DockStyle.Top,Height=86,BackColor=Theme.Canvas};var heading=Title(title,19);heading.Location=new Point(4,3);p.Controls.Add(heading);p.Controls.Add(new Label{Text=subtitle,AutoSize=true,ForeColor=Theme.Muted,Location=new Point(5,47)});return p;
    }
    void ResourceDetail()
    {
        if(resourceGrid.CurrentRow?.Cells["内部序号"].Value is not int ix)return;using var f=new Form{Text="中继段完整详情",Width=850,Height=650,Font=Font,StartPosition=FormStartPosition.CenterParent};var t=new TextBox{Dock=DockStyle.Fill,Multiline=true,ReadOnly=true,ScrollBars=ScrollBars.Both};t.Text=JsonSerializer.Serialize(JsonSerializer.Deserialize<JsonElement>(store.Raw(ix)),new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});f.Controls.Add(t);f.ShowDialog(this);
    }
    void BuildResourceWorkspace()
    {
        var tab=Tab("中继段资源");var surface=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};resourceSearch.PlaceholderText="搜索名称、ID、设施、站点或机房";resourceSearch.Width=320;
        var top=Flow();top.BackColor=Color.White;top.Controls.AddRange([resourceSearch,L("端点类型"),resourceType,L("最少空闲芯数"),minFree,B("筛选",()=>{page.Value=1;LoadResources();}),B("重置",()=>{resourceSearch.Clear();resourceType.SelectedItem="全部";minFree.Value=0;page.Value=1;LoadResources();}),B("导出筛选结果",ExportResources),B("详情",ResourceDetail)]);
        var pager=Flow();pager.Dock=DockStyle.Bottom;pager.BackColor=Color.White;pager.Controls.AddRange([pageLabel,B("上一页",()=>{if(page.Value>1)page.Value--;LoadResources();}),L("第"),page,L("页"),B("跳转",LoadResources),B("下一页",()=>{page.Value++;LoadResources();})]);surface.Controls.Add(resourceGrid);surface.Controls.Add(pager);surface.Controls.Add(top);
        tab.Controls.Add(surface);tab.Controls.Add(Stats());tab.Controls.Add(PageIntro("中继段资源","查询导入的中继段，核对两端设施与纤芯资源"));resourceGrid.CellDoubleClick+=(_,e)=>{if(e.RowIndex>=0)ResourceDetail();};
    }
}
internal sealed class StatTile:Label
{
    public string Glyph {get;set;}="cabinet";
    public StatTile(){SetStyle(ControlStyles.UserPaint|ControlStyles.OptimizedDoubleBuffer,true);BackColor=Theme.Canvas;}
    protected override void OnPaint(PaintEventArgs e)
    {
        var g=e.Graphics;float scale=DeviceDpi/96f;g.Clear(Theme.Canvas);g.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;using var path=Theme.Round(new RectangleF(1,1,Width-3,Height-3),8*scale);using var b=new SolidBrush(Color.White);using var pen=new Pen(Theme.Line);g.FillPath(b,path);g.DrawPath(pen,path);
        var lines=Text.Split('\n');Theme.Glyph(g,Glyph,new Rectangle((int)(18*scale),(Height-(int)(30*scale))/2,(int)(30*scale),(int)(30*scale)),Theme.Teal);using var normal=new Font(Font.FontFamily,10);using var strong=new Font(Font.FontFamily,17,FontStyle.Bold);TextRenderer.DrawText(g,lines.ElementAtOrDefault(0)??"",normal,new Rectangle((int)(62*scale),(int)(17*scale),Width-(int)(70*scale),(int)(25*scale)),Theme.Muted,TextFormatFlags.EndEllipsis);TextRenderer.DrawText(g,lines.ElementAtOrDefault(1)??"",strong,new Rectangle((int)(60*scale),(int)(44*scale),Width-(int)(68*scale),(int)(36*scale)),Theme.Ink,TextFormatFlags.EndEllipsis);
    }
}
