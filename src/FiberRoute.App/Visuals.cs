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
    protected override void OnFontChanged(EventArgs e){base.OnFontChanged(e);ItemHeight=Math.Max(28,(int)(Font.Height+14*DeviceDpi/96f));}
    protected override void WndProc(ref Message message)
    {
        base.WndProc(ref message);if(message.Msg==0xF&&IsHandleCreated){using var g=CreateGraphics();float scale=DeviceDpi/96f;g.Clear(BackColor);g.SmoothingMode=SmoothingMode.AntiAlias;using var outline=Theme.Round(new RectangleF(0,0,Width-1,Height-1),5*scale);using var pen=new Pen(Focused?Theme.Teal:Theme.Line);g.DrawPath(pen,outline);int arrow=(int)(21*scale);TextRenderer.DrawText(g,Text,Font,new Rectangle((int)(8*scale),0,Math.Max(5,Width-arrow-(int)(12*scale)),Height),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);Theme.Glyph(g,"chevron",new Rectangle(Width-arrow,(Height-(int)(14*scale))/2,(int)(14*scale),(int)(14*scale)),Theme.Ink);}
    }
    protected override void OnDrawItem(DrawItemEventArgs e)
    {
        bool selected=(e.State&DrawItemState.Selected)!=0;using var background=new SolidBrush(selected?Theme.Tint:e.Index==SelectedIndex&&!DroppedDown?BackColor:Color.White);e.Graphics.FillRectangle(background,e.Bounds);
        if(e.Index>=0&&e.Index<Items.Count)TextRenderer.DrawText(e.Graphics,GetItemText(Items[e.Index]),Font,Rectangle.Inflate(e.Bounds,-6,0),Enabled?Theme.Ink:Theme.Muted,TextFormatFlags.Left|TextFormatFlags.VerticalCenter|TextFormatFlags.EndEllipsis|TextFormatFlags.NoPadding);

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
        var frame=new Surface{Dock=DockStyle.Fill,Height=42,Padding=new Padding(9,4,9,4),Margin=new Padding(0,3,0,3),BackColor=Color.White};
        if(input is TextBox box){box.BorderStyle=BorderStyle.None;box.BackColor=Color.White;if(!box.Multiline)box.AutoSize=false;}
        input.Dock=DockStyle.None;input.Margin=Padding.Empty;frame.Controls.Add(input);bool placing=false;
        void Place()
        {
            if(placing||frame.IsDisposed)return;placing=true;try{float scale=frame.DeviceDpi/96f;int inset=(int)(9*scale),height=input.Height;
                if(input is TextBox tb&&!tb.Multiline)height=Math.Max(tb.PreferredHeight,tb.Font.Height)+(int)(4*scale);
                else if(input is NumericUpDown number)height=Math.Max(number.PreferredHeight,number.Font.Height)+(int)(4*scale);
                if(input.Height!=height)input.Height=height;input.Width=Math.Max(30,frame.ClientSize.Width-2*inset);input.Location=new Point(inset,Math.Max(0,(frame.ClientSize.Height-input.Height)/2));
            }finally{placing=false;}
        }
        frame.Layout+=(_,_)=>Place();frame.SizeChanged+=(_,_)=>Place();frame.DpiChangedAfterParent+=(_,_)=>Place();input.FontChanged+=(_,_)=>Place();input.HandleCreated+=(_,_)=>Place();Place();return frame;
    }

    void PaintBadges(DataGridView grid)
    {
        grid.CellPainting+=(_,e)=>{
            if(e.RowIndex<0||e.ColumnIndex<0)return;string column=grid.Columns[e.ColumnIndex].Name;
            if(column is not ("类型" or "始端类型" or "终端类型" or "节点类型" or "确认状态" or "业务状态" or "容量状态" or "路由状态" or "归并状态"))return;
            string value=e.FormattedValue?.ToString()??"";if(value.Length==0)return;e.PaintBackground(e.ClipBounds,true);
            if(grid==resourceGrid&&column is "始端类型" or "终端类型")
            {
                var font=e.CellStyle?.Font??grid.Font;float scale=grid.DeviceDpi/96f;int width=Math.Max(25,e.CellBounds.Width-(int)(30*scale));var tags=value.Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries).Select(tag=>(Tag:tag,Text:WrapCellText(tag,font,width))).ToArray();int total=tags.Sum(t=>t.Text.Split('\n').Length*font.Height+(int)(8*scale))+Math.Max(0,tags.Length-1)*(int)(4*scale),y=e.CellBounds.Y+Math.Max(3,(e.CellBounds.Height-total)/2);
                foreach(var tag in tags){var colors=tag.Tag=="光交箱"?(Color.FromArgb(212,249,238),Color.FromArgb(0,136,111)):tag.Tag=="光终端盒"?(Color.FromArgb(213,244,248),Color.FromArgb(0,128,142)):Theme.BadgeColors(tag.Tag);int height=tag.Text.Split('\n').Length*font.Height+(int)(8*scale),badge=Math.Min(e.CellBounds.Width-(int)(18*scale),TextRenderer.MeasureText(tag.Tag,font,Size.Empty,TextFormatFlags.NoPadding).Width+(int)(14*scale));var rect=new Rectangle(e.CellBounds.X+(int)(9*scale),y,badge,height);using var rounded=Theme.Round(rect,5*scale);using var fill=new SolidBrush(colors.Item1);e.Graphics!.FillPath(fill,rounded);TextRenderer.DrawText(e.Graphics,tag.Text,font,Rectangle.Inflate(rect,-(int)(5*scale),0),colors.Item2,TextFormatFlags.WordBreak|TextFormatFlags.VerticalCenter|TextFormatFlags.NoPrefix|TextFormatFlags.NoPadding);y+=height+(int)(4*scale);}e.Handled=true;return;
            }
            var bounds=new Rectangle(e.CellBounds.X+9,e.CellBounds.Y+Math.Max(3,(e.CellBounds.Height-26*grid.DeviceDpi/96)/2),e.CellBounds.Width-18,26*grid.DeviceDpi/96);
            (Color Back,Color Fore)? palette=grid==resourceGrid&&value=="光交箱"?(Color.FromArgb(212,249,238),Color.FromArgb(0,136,111)):grid==resourceGrid&&value=="光终端盒"?(Color.FromArgb(213,244,248),Color.FromArgb(0,128,142)):null;Theme.Badge(e.Graphics!,value,e.CellStyle?.Font??grid.Font,bounds,palette);e.Handled=true;
        };
    }
    void PrepareGrid(DataGridView grid)
    {
        PaintBadges(grid);
        grid.CellToolTipTextNeeded+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0)e.ToolTipText=grid.Rows[e.RowIndex].Cells[e.ColumnIndex].Value?.ToString()??"";};
        grid.DpiChangedAfterParent+=(_,_)=>QueueRowMeasure(grid);
        grid.DataBindingComplete+=(_,_)=>QueueRowMeasure(grid);
        grid.ColumnWidthChanged+=(_,_)=>QueueRowMeasure(grid);
        grid.SizeChanged+=(_,_)=>QueueRowMeasure(grid);
        grid.VisibleChanged+=(_,_)=>{if(grid.Visible)QueueRowMeasure(grid);};
        grid.CellPainting+=(_,e)=>{if(e.RowIndex>=0&&e.ColumnIndex>=0&&!e.Handled){e.PaintBackground(e.ClipBounds,true);e.Paint(e.ClipBounds,DataGridViewPaintParts.Border);
            var cell=grid.Rows[e.RowIndex].Cells[e.ColumnIndex];var style=e.CellStyle!;float scale=grid.DeviceDpi/96f;
            var bounds=new Rectangle(e.CellBounds.X+style.Padding.Left+(int)(3*scale),e.CellBounds.Y+style.Padding.Top+(int)(4*scale),Math.Max(20,e.CellBounds.Width-style.Padding.Horizontal-(int)(6*scale)),Math.Max(1,e.CellBounds.Height-style.Padding.Vertical-(int)(8*scale)));
            string text=WrapCellText(cell.Value is string raw?raw:e.FormattedValue?.ToString()??"",style.Font??grid.Font,bounds.Width);
            TextRenderer.DrawText(e.Graphics!,text,style.Font??grid.Font,bounds,(e.State&DataGridViewElementStates.Selected)!=0?style.SelectionForeColor:style.ForeColor,TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.Top|TextFormatFlags.Left);e.Handled=true;}};
        grid.CellFormatting+=(_,e)=>{if(e.RowIndex<0||e.ColumnIndex<0||e.Value is not string text)return;string column=grid.Columns[e.ColumnIndex].Name;if(column is "类型" or "始端类型" or "终端类型" or "节点类型" or "确认状态" or "业务状态" or "容量状态" or "路由状态" or "归并状态")return;var style=e.CellStyle!;e.Value=WrapCellText(text,style.Font??grid.Font,grid.Columns[e.ColumnIndex].Width-style.Padding.Horizontal-(int)(6*grid.DeviceDpi/96f));e.FormattingApplied=true;};
        // Row headers stay hidden; users can still resize rows at the cell boundary or with
        // the context menu when borderless rows make the grip hard to find.
        var menu=grid.ContextMenuStrip??new ContextMenuStrip();grid.ContextMenuStrip=menu;
    }
}
