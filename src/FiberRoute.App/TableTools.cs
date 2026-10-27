using System.ComponentModel;
using System.Globalization;
namespace FiberRoute.App;

internal sealed class ModernPager : UserControl
{
    readonly Label summary=new(){Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,AutoEllipsis=true};
    readonly UiComboBox size=new(){Width=78};readonly TextBox jump=new(){Width=42,Text="1",TextAlign=HorizontalAlignment.Center,BorderStyle=BorderStyle.None,Dock=DockStyle.Fill};
    readonly FlowLayoutPanel numbers=new(){AutoSize=true,AutoSizeMode=AutoSizeMode.GrowAndShrink,Size=Size.Empty,WrapContents=false,Margin=Padding.Empty};readonly FlowLayoutPanel actions=new(){Dock=DockStyle.Right,Width=520,WrapContents=false,Padding=new Padding(0,7,0,0)};
    readonly UiButton previous,next;bool suppress;int total;
    public int Page {get;private set;}=1;public int PageSize {get;private set;}=50;
    public event Action? Changed;
    public ModernPager()
    {
        Dock=DockStyle.Bottom;Height=52;BackColor=Color.White;size.Items.AddRange(["20条","50条","100条","200条"]);size.SelectedIndex=1;size.Margin=new Padding(4,2,8,0);
        UiButton Button(string text,Action click,int width=32){var b=new UiButton{Text=text,Width=width,Height=33,MinimumSize=Size.Empty,Margin=new Padding(2,0,2,0),Padding=Padding.Empty};b.Click+=(_,_)=>click();return b;}
        previous=Button("‹",()=>Go(Page-1));next=Button("›",()=>Go(Page+1));var confirm=Button("确定",()=>{if(int.TryParse(jump.Text,out int p))Go(p);else jump.Text=Page.ToString();},52);
        var frame=new Surface{Width=46,Height=33,Padding=new Padding(6,7,6,2),Margin=new Padding(3,0,3,0),BackColor=Color.White};frame.Controls.Add(jump);
        actions.Controls.AddRange([new Label{Text="每页",AutoSize=true,Margin=new Padding(0,7,0,0)},size,previous,numbers,next,new Label{Text="跳至",AutoSize=true,Margin=new Padding(6,7,0,0)},frame,confirm]);jump.KeyDown+=(_,e)=>{if(e.KeyCode==Keys.Enter){confirm.PerformClick();e.SuppressKeyPress=true;}};
        Controls.Add(summary);Controls.Add(actions);size.SelectedIndexChanged+=(_,_)=>{if(suppress)return;PageSize=new[]{20,50,100,200}[Math.Max(0,size.SelectedIndex)];Page=1;Changed?.Invoke();};
    }
    public void Configure(int pageSize){PageSize=new[]{20,50,100,200}.Contains(pageSize)?pageSize:50;suppress=true;size.SelectedIndex=Array.IndexOf(new[]{20,50,100,200},PageSize);suppress=false;}
    public void Set(int count,int page)
    {
        total=count;Page=Math.Clamp(page,1,Math.Max(1,(count+PageSize-1)/PageSize));int pages=Math.Max(1,(count+PageSize-1)/PageSize);summary.Text=count==0?"无匹配记录":$"第 {(Page-1)*PageSize+1:N0}–{Math.Min(Page*PageSize,count):N0} 条，共 {count:N0} 条";jump.Text=Page.ToString();previous.Enabled=count>0&&Page>1;next.Enabled=count>0&&Page<pages;
        foreach(Control c in numbers.Controls.Cast<Control>().ToArray())c.Dispose();numbers.Controls.Clear();float scale=DeviceDpi/96f;var shown=new SortedSet<int>{Math.Max(1,Page-1),Page,Math.Min(pages,Page+1)};if(ClientSize.Width>850*scale){shown.Add(1);shown.Add(pages);}int last=0;
        foreach(int p in shown){if(last>0&&p-last>1)numbers.Controls.Add(new Label{Text="…",AutoSize=true,Margin=new Padding(0,(int)(7*scale),0,0)});int go=p;var b=new UiButton{Text=p.ToString(),Width=(int)((p>99?45:32)*scale),Height=(int)(33*scale),Margin=new Padding((int)(2*scale),0,(int)(2*scale),0),Padding=Padding.Empty,Primary=p==Page,Enabled=count>0};b.Click+=(_,_)=>Go(go);numbers.Controls.Add(b);last=p;}
        actions.Width=Math.Min((int)(650*scale),Math.Max((int)(470*scale),ClientSize.Width-(int)(120*scale)));
    }
    protected override void OnSizeChanged(EventArgs e){base.OnSizeChanged(e);if(actions!=null&&numbers!=null&&IsHandleCreated)Set(total,Page);}
    void Go(int page){int next=Math.Clamp(page,1,Math.Max(1,(total+PageSize-1)/PageSize));if(next==Page)return;Page=next;Changed?.Invoke();}
}

public sealed partial class MainForm
{
    readonly ModernPager resourcePager=new(),typePager=new(),clusterPager=new(),roomPager=new();
    sealed record SortChoice(string Column,bool Descending);
    readonly Dictionary<DataGridView,SortChoice> sortChoices=[];
    readonly Dictionary<DataGridView,Action> tableReloads=[];
    void ConfigurePager(ModernPager pager,string key,Action reload)
    {
        if(int.TryParse(store.Setting("paging-"+key),out int size))pager.Configure(size);
        pager.Changed+=()=>{store.SetSetting("paging-"+key,pager.PageSize.ToString());reload();};
    }
    void Sortable(DataGridView grid,Action reload)
    {
        tableReloads[grid]=reload;
        grid.ColumnHeaderMouseClick+=(_,e)=>{if(e.ColumnIndex<0)return;string column=grid.Columns[e.ColumnIndex].Name;if(column is "操作" or "序号" or "内部序号" or "内部标识")return;var old=sortChoices.GetValueOrDefault(grid);sortChoices[grid]=new(column,old?.Column==column&&!old.Descending);reload();};
        grid.DataBindingComplete+=(_,_)=>{foreach(DataGridViewColumn c in grid.Columns){c.SortMode=DataGridViewColumnSortMode.Programmatic;var sort=sortChoices.GetValueOrDefault(grid);c.HeaderCell.SortGlyphDirection=sort?.Column==c.Name?sort.Descending?SortOrder.Descending:SortOrder.Ascending:SortOrder.None;}};
    }
    T[] Ordered<T>(DataGridView grid,IEnumerable<T> rows)
    {
        if(!sortChoices.TryGetValue(grid,out var choice))return rows.ToArray();var property=typeof(T).GetProperty(choice.Column);if(property==null)return rows.ToArray();
        object? Key(T row){var value=property.GetValue(row);if(choice.Column=="空闲_总芯"&&value is string s&&int.TryParse(s.Split('/')[0].Trim(),out int n))return n;return value;}
        var compare=Comparer<object?>.Create((a,b)=>{if(a==null)return b==null?0:1;if(b==null)return -1;int value=a is IComparable comparable?comparable.CompareTo(b):string.Compare(a.ToString(),b.ToString(),StringComparison.CurrentCulture);return choice.Descending?-value:value;});
        return rows.OrderBy(Key,compare).ToArray();
    }
    static string Area(string value)=>value.Length==0?"未提供":value;
    static void FillChoices(ComboBox box,IEnumerable<string> values,string all)
    {
        string old=box.Text;box.BeginUpdate();box.Items.Clear();box.Items.Add(all);box.Items.AddRange(values.Distinct().Order(StringComparer.CurrentCulture).Cast<object>().ToArray());box.SelectedItem=box.Items.Contains(old)?old:all;box.EndUpdate();
    }
    FlowLayoutPanel StatusChips(Func<string> selected,Action<string> choose)
    {
        var panel=Flow();panel.Padding=new Padding(0,0,0,5);panel.BackColor=Color.White;
        foreach(string status in new[]{"全部","已确认","待确认","需核对"}){var b=new UiButton{Text=status,Tag=status,Tone=status=="全部"?"":status,Pill=true,Selected=selected()==status,Width=115,Height=36,Margin=new Padding(0,0,8,0)};b.Click+=(_,_)=>{choose(status);foreach(UiButton c in panel.Controls){c.Selected=(string?)c.Tag==selected();c.Invalidate();}};panel.Controls.Add(b);}return panel;
    }
    static void UpdateStatusCounts(FlowLayoutPanel? panel,IEnumerable<string> statuses,string selected)
    {
        if(panel==null)return;var values=statuses.ToArray();var counts=values.GroupBy(s=>s).ToDictionary(g=>g.Key,g=>g.Count());foreach(UiButton button in panel.Controls){string state=(string)button.Tag!;button.Text=$"{state} {(state=="全部"?values.Length:counts.GetValueOrDefault(state)):N0}";button.Selected=state==selected;button.Invalidate();}
    }
    string ProvenanceText(string name,string? room=null)
    {
        var rows=store.EvidenceFor(name);if(room!=null)rows=rows.Where(r=>room.Split('；').Contains(r.Room,StringComparer.Ordinal)).ToArray();if(rows.Length==0)return "来源行号：历史数据未记录";
        return string.Join("\n\n",rows.Take(5).Select(r=>$"来源文件：{r.Source}"+(r.Sheet.Length>0?$" · {r.Sheet}":"")+$"\n原表行号：第 {r.Row} 行"+(r.Field.Length>0?$"\n源字段：{r.Field}":"")+(r.Room.Length>0?$"\n机房原值：{r.Room}":"")));
    }
}
