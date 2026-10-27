namespace FiberRoute.App;

// All options participate in the parent sidebar's scrolling. A nested multi-column
// CheckedListBox otherwise scrolls horizontally and can hide its final rows.
internal sealed class TypeChecklist : FlowLayoutPanel
{
    public TypeChecklist()
    {
        AutoSize=false;Height=28;WrapContents=true;
        FlowDirection=FlowDirection.LeftToRight;AutoScroll=false;Margin=Padding.Empty;
        Padding=Padding.Empty;MinimumSize=new Size(112,0);
    }
    public IEnumerable<string> CheckedTypes=>Controls.OfType<CheckBox>().Where(c=>c.Checked).Select(c=>c.Text);
    public void SetTypes(IEnumerable<string> types,ISet<string> selected)
    {
        SuspendLayout();foreach(Control control in Controls.Cast<Control>().ToArray()){Controls.Remove(control);control.Dispose();}
        foreach(string type in types)Controls.Add(new CheckBox{Text=type,Checked=selected.Contains(type),AutoSize=true,MinimumSize=new Size(104,26),Margin=new Padding(0,0,4,2),ForeColor=Theme.Ink});
        ResumeLayout(true);
    }
    public void SetCheckedTypes(ISet<string> selected)
    {foreach(CheckBox box in Controls)box.Checked=selected.Contains(box.Text);}
    protected override void OnLayout(LayoutEventArgs e)
    {
        base.OnLayout(e);
        int height=Controls.Cast<Control>().Select(c=>c.Bottom+c.Margin.Bottom).DefaultIfEmpty(28).Max()+Padding.Bottom;
        if(Height!=height)Height=height;
    }
}
