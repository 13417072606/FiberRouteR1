using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly ComboBox sheets=new(){Width=260,DropDownStyle=ComboBoxStyle.DropDownList};
    readonly Label inputInfo=new(){AutoSize=true,MaximumSize=new Size(300,0),Padding=new Padding(4)};
    readonly Label previewStats=new(){Dock=DockStyle.Top,Height=80,Padding=new Padding(12),BackColor=Color.White};
    string? previewKey;string detectedEncoding="自动";
    readonly List<Label[]> statViews=[];
    string InputKey()=>importPath.Text+"\n"+encoding.Text+"\n"+sheets.Text;
    static FlowLayoutPanel Stack()
    {
        var p=new FlowLayoutPanel{Dock=DockStyle.Fill,AutoScroll=true,FlowDirection=FlowDirection.TopDown,WrapContents=false,Padding=new Padding(12),BackColor=Color.White};
        p.SizeChanged+=(_,_)=>{foreach(Control c in p.Controls)c.Width=Math.Max(120,p.ClientSize.Width-44);};
        p.ControlAdded+=(_,e)=>{if(e.Control!=null)e.Control.Width=Math.Max(120,p.ClientSize.Width-44);};
        return p;
    }
    static Control Field(string title,Control control)
    {var p=new FlowLayoutPanel{Height=42,Width=285,WrapContents=false};p.Controls.Add(L(title));p.Controls.Add(control);return p;}
    static Control Card(string title,Control body)
    {
        var p=new Panel{Dock=DockStyle.Fill,Padding=new Padding(10),Margin=new Padding(6),BackColor=Color.White};
        body.Dock=DockStyle.Fill;p.Controls.Add(body);p.Controls.Add(new Label{Text=title,Dock=DockStyle.Top,Height=34,AutoEllipsis=true,Font=new Font("Microsoft YaHei UI",10,FontStyle.Bold)});return p;
    }
    static TableLayoutPanel Columns(float a,Control left,float b,Control right)
    {
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=2,RowCount=1};p.ColumnStyles.Add(new(SizeType.Percent,a));p.ColumnStyles.Add(new(SizeType.Percent,b));
        left.Dock=right.Dock=DockStyle.Fill;p.Controls.Add(left,0,0);p.Controls.Add(right,1,0);return p;
    }
    static TableLayoutPanel Rows(float[] weights,params Control[] controls)
    {
        var p=new TableLayoutPanel{Dock=DockStyle.Fill,RowCount=weights.Length,ColumnCount=1};
        for(int i=0;i<weights.Length;i++){p.RowStyles.Add(new(SizeType.Percent,weights[i]));controls[i].Dock=DockStyle.Fill;p.Controls.Add(controls[i],0,i);}return p;
    }
    Control Stats()
    {
        var p=new TableLayoutPanel{Dock=DockStyle.Top,Height=90,ColumnCount=4,RowCount=1,Padding=new Padding(0,0,0,8)};var labels=new Label[4];
        for(int i=0;i<4;i++){p.ColumnStyles.Add(new(SizeType.Percent,25));labels[i]=new(){Dock=DockStyle.Fill,Padding=new Padding(14,12,8,6),BackColor=Color.White,Margin=new Padding(5),Font=new Font(Font.FontFamily,11),AutoEllipsis=true};p.Controls.Add(labels[i],i,0);}statViews.Add(labels);return p;
    }
    void UpdateStats()
    {
        var g=store.Graph;string[] values=[$"中继段总数\n{g.Edges.Length:N0}",$"设施节点数\n{g.Nodes.Length:N0}",$"空闲逐段累计（非可用路由数）\n{g.Edges.Sum(e=>(long)e.Free):N0} 芯",$"已记录长度 / 未知长度段\n{g.Edges.Sum(e=>e.Length??0):N2} 米 / {g.Edges.Count(e=>e.Length==null):N0}"];
        foreach(var view in statViews)for(int i=0;i<4;i++)view[i].Text=values[i];
    }
    void ShowUpdateSettings()
    {
        using var d=new Form{Text="在线更新配置",Width=650,Height=430,StartPosition=FormStartPosition.CenterParent,Font=Font};
        var p=Stack();p.Controls.AddRange([L("未配置服务源时，保持本地离线使用"),L("HTTPS签名清单地址"),updateUrl,L("RSA公钥PEM"),updateKey,
            B("保存配置",()=>{if(updateUrl.Text.Length>0){var uri=new Uri(updateUrl.Text);if(uri.Scheme!="https"||uri.UserInfo.Length>0)throw new ArgumentException("必须为无凭据的HTTPS地址");using var rsa=System.Security.Cryptography.RSA.Create();rsa.ImportFromPem(updateKey.Text);}store.SetSetting("update",System.Text.Json.JsonSerializer.Serialize(new UpdateConfig(updateUrl.Text.Trim(),updateKey.Text.Trim())));updateStatus.Text=updateUrl.Text.Length==0?"未配置更新源":"已配置，等待检查";d.Close();})]);d.Controls.Add(p);d.ShowDialog(this);
        // Keep reusable controls alive when the temporary dialog is disposed.
        p.Controls.Remove(updateUrl);p.Controls.Remove(updateKey);
    }
}
public sealed class PathDiagram:ScrollableControl
{
    string[] names=[],types=[];
    public PathDiagram(){DoubleBuffered=true;BackColor=Color.White;AutoScroll=true;}
    public void SetRoute(Graph g,Route r){names=r.Nodes.Select(i=>g.Nodes[i].Name).ToArray();types=r.Nodes.Select(i=>g.Nodes[i].Type).ToArray();AutoScrollMinSize=new Size(names.Length*250+40,150);Invalidate();}
    public void Clear(){names=[];types=[];AutoScrollMinSize=Size.Empty;Invalidate();}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);e.Graphics.TranslateTransform(AutoScrollPosition.X,AutoScrollPosition.Y);e.Graphics.SmoothingMode=System.Drawing.Drawing2D.SmoothingMode.AntiAlias;
        if(names.Length==0){e.Graphics.DrawString("选择候选方案后显示节点和连线",Font,Brushes.Gray,16,30);return;}
        using var pen=new Pen(Color.FromArgb(8,126,128),2);using var brush=new SolidBrush(Color.FromArgb(8,126,128));
        for(int i=0;i<names.Length;i++)
        {
            int x=35+i*250;
            if(i<names.Length-1){e.Graphics.DrawLine(pen,x+9,28,x+241,28);e.Graphics.DrawLine(pen,x+230,22,x+241,28);e.Graphics.DrawLine(pen,x+230,34,x+241,28);}
            e.Graphics.FillEllipse(brush,x-8,20,16,16);
            e.Graphics.DrawString($"{i+1}. {names[i]}",Font,Brushes.Black,new RectangleF(x-16,48,225,72));
            e.Graphics.DrawString(types[i],Font,Brushes.DimGray,new RectangleF(x-16,121,225,38));
        }
    }
}
