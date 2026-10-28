namespace FiberRoute.App;
public sealed partial class MainForm
{
    readonly Dictionary<DataGridView,int> rowMinimum=[];
    readonly HashSet<DataGridView> queuedMeasures=[];
    bool measuringRows;
    void QueueRowMeasure(DataGridView grid)
    {
        if(measuringRows||grid.IsDisposed||!grid.IsHandleCreated||!queuedMeasures.Add(grid))return;
        grid.BeginInvoke((Action)(()=>{queuedMeasures.Remove(grid);if(!grid.IsDisposed)MeasureRows(grid);}));
    }
    internal static string WrapCellText(string text,Font font,int width)
    {
        width=Math.Max(20,width);var lines=new List<string>();
        foreach(string paragraph in text.Replace("\r","").Split('\n'))
        {
            if(paragraph.Length==0){lines.Add("");continue;}int start=0;
            while(start<paragraph.Length){int low=1,high=paragraph.Length-start,fit=1;while(low<=high){int mid=(low+high)/2;int measured=TextRenderer.MeasureText(paragraph.Substring(start,mid),font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix|TextFormatFlags.SingleLine).Width;if(measured<=width){fit=mid;low=mid+1;}else high=mid-1;}if(start+fit<paragraph.Length&&char.IsHighSurrogate(paragraph[start+fit-1])&&fit>1)fit--;lines.Add(paragraph.Substring(start,fit));start+=fit;}
        }
        return string.Join("\n",lines);
    }
    internal static int CellTextHeight(string wrapped,Font font)=>TextRenderer.MeasureText(wrapped.Length==0?" ":wrapped,font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height;
    internal static Rectangle CenterCellText(Rectangle bounds,string wrapped,Font font)
    {
        int height=CellTextHeight(wrapped,font);return new Rectangle(bounds.X,bounds.Y+Math.Max(0,(bounds.Height-height)/2),bounds.Width,height);
    }
    void MeasureRows(DataGridView grid)
    {
        if(measuringRows||grid.Columns.Count==0)return;measuringRows=true;
        try
        {
            float scale=grid.DeviceDpi/96f;var saved=gridStates.GetValueOrDefault(grid);int minimum=rowMinimum.GetValueOrDefault(grid,saved==null?(int)(38*scale):(int)(saved.Height*grid.DeviceDpi/(float)Math.Max(1,saved.Dpi)));
            foreach(DataGridViewRow row in grid.Rows)
            {
                int height=Math.Max((int)((grid==resourceGrid?64:38)*scale),minimum);
                foreach(DataGridViewCell cell in row.Cells)
                {
                    var column=grid.Columns[cell.ColumnIndex];if(!column.Visible)continue;var font=cell.InheritedStyle.Font??grid.Font;int width=column.Width-cell.InheritedStyle.Padding.Horizontal-(int)(6*scale);string text=cell.FormattedValue?.ToString()??"";
                    if(column.Name is "类型" or "始端类型" or "终端类型" or "节点类型")
                    {
                        var tags=(cell.Value?.ToString()??"").Split(';',StringSplitOptions.TrimEntries|StringSplitOptions.RemoveEmptyEntries);int tagHeight=tags.Sum(tag=>CellTextHeight(WrapCellText(tag,font,Math.Max(25,column.Width-(int)(30*scale))),font)+(int)(8*scale))+Math.Max(0,tags.Length-1)*(int)(4*scale)+(int)(8*scale);height=Math.Max(height,tagHeight);continue;
                    }
                    string wrapped=WrapCellText(cell.Value?.ToString()??text,font,width);
                    int breathingRoom=(grid==typeGrid||grid==roomGrid)&&wrapped.Contains('\n')?(int)(8*scale):0;
                    int needed=CellTextHeight(wrapped,font)+cell.InheritedStyle.Padding.Vertical+(int)(8*scale)+breathingRoom;height=Math.Max(height,needed);
                }
                if(row.Height!=height)row.Height=Math.Clamp(height,20,4000);
            }
        }
        finally{measuringRows=false;}
    }
}
