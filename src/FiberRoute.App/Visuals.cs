using System.Drawing.Drawing2D;
namespace FiberRoute.App;

// Shared input and list rendering keeps all four workspaces visually consistent.
internal sealed class UiComboBox : ComboBox
{
    public UiComboBox()
    {
        DrawMode=DrawMode.OwnerDrawFixed;FlatStyle=FlatStyle.Flat;BackColor=Color.White;ForeColor=Theme.Ink;
        DropDownStyle=ComboBoxStyle.DropDownList;IntegralHeight=false;DropDownHeight=300;
    }
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(24,(int)(Font.Height+10*DeviceDpi/96f));}
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected=(e.State&DrawItemState.Selected)!=0;using var background=new SolidBrush(selected?Theme.Tint:e.Index==SelectedIndex&&!DroppedDown?BackColor:Color.White);e.Graphics.FillRectangle(background,e.Bounds);
        if(e.Index>=0&&e.Index<Items.Count)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,Rectangle.Inflate(e.Bounds,-9,0),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis);
        if((e.State&DrawItemState.Focus)!=0)ControlPaint.DrawFocusRectangle(e.Graphics,Rectangle.Inflate(e.Bounds,-2,-2),Theme.Teal,selected?Theme.Tint:Color.White);
    }
}
internal sealed class UiCheckBox : CheckBox
{
    public UiCheckBox(){AutoSize=true;ForeColor=Theme.Ink;Cursor=Cursors.Hand;Padding=new Padding(4,5,4,5);Margin=new Padding(0,3,0,3);}
}
internal sealed class StatusLegend : Control
{
    public StatusLegend(){Height=176;DoubleBuffered=true;BackColor=Color.White;}
    protected override void OnPaint(PaintEventArgs e)
    {
        base.OnPaint(e);float s=DeviceDpi/96f;using var strong=new Font(Font,FontStyle.Bold);
        TextRenderer.DrawText(e.Graphics,"确认状态说明",strong,new Rectangle(4,0,Width-8,(int)(30*s)),Theme.Ink);
        string[] titles=["已确认","待确认","需核对"],notes=["表格匹配或人工确认，且无明显异常","仅软件识别，无表格匹配或人工确认","存在明显异常，需要再次核对"];
        for(int i=0;i<3;i++){int y=(int)((32+i*37)*s);Theme.Badge(e.Graphics,titles[i],Font,new Rectangle(4,y,(int)(68*s),(int)(28*s)));TextRenderer.DrawText(e.Graphics,notes[i],Font,new Rectangle((int)(80*s),y,Math.Max(10,Width-(int)(84*s)),(int)(35*s)),Theme.Muted,TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter);}
    }
}
public sealed partial class MainForm
{
    static Control InputFrame(Control input,string placeholder="")
    {
        if(input is TextBox text&&placeholder.Length>0)text.PlaceholderText=placeholder;
        var frame=new Surface{Dock=DockStyle.Fill,Height=40,Padding=new Padding(9,6,9,6),Margin=new Padding(0,3,0,3),BackColor=Color.White};
        if(input is TextBox box)box.BorderStyle=BorderStyle.None;
        input.Dock=DockStyle.None;frame.Controls.Add(input);
        void Place(){input.Width=Math.Max(30,frame.ClientSize.Width-18);input.Location=new Point(9,Math.Max(4,(frame.ClientSize.Height-input.Height)/2));}
        frame.SizeChanged+=(_,_)=>Place();input.FontChanged+=(_,_)=>Place();Place();return frame;
    }
    static void PaintBadges(DataGridView grid)
    {
        grid.CellPainting+=(_,e)=>{
            if(e.RowIndex<0||e.ColumnIndex<0)return;string column=grid.Columns[e.ColumnIndex].Name;
            if(column is not ("类型" or "节点类型" or "确认状态" or "业务状态" or "容量状态" or "路由状态" or "归并状态"))return;
            string value=e.FormattedValue?.ToString()??"";if(value.Length==0)return;e.PaintBackground(e.ClipBounds,true);
            var bounds=new Rectangle(e.CellBounds.X+9,e.CellBounds.Y+Math.Max(3,(e.CellBounds.Height-26*grid.DeviceDpi/96)/2),e.CellBounds.Width-18,26*grid.DeviceDpi/96);
            Theme.Badge(e.Graphics!,value,e.CellStyle?.Font??grid.Font,bounds);e.Handled=true;
        };
    }
    void PrepareGrid(DataGridView grid)
    {
        PaintBadges(grid);
        grid.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0||e.Value is not string text)return;string column=grid.Columns[e.ColumnIndex].Name;if(column is "类型" or "节点类型" or "确认状态" or "业务状态" or "容量状态" or "路由状态" or "归并状态")return;var style=e.CellStyle!;e.Value=WrapCellText(text,style.Font??grid.Font,grid.Columns[e.ColumnIndex].Width-style.Padding.Horizontal-(int)(6*grid.DeviceDpi/96f));e.FormattingApplied=true;};
        // Row headers stay hidden; users can still resize rows at the cell boundary or with
        // the context menu when borderless rows make the grip hard to find.
        var menu=grid.ContextMenuStrip??new ContextMenuStrip();grid.ContextMenuStrip=menu;
    }
}
