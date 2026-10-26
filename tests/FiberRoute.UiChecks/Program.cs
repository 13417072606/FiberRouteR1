using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;
using System.Runtime.InteropServices;
internal static class Program
{
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h,int msg,IntPtr w,IntPtr l);
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();string output=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"fiber-ui-checks");Directory.CreateDirectory(output);string data=Path.Combine(output,"data");var s=new Store(data);s.SetSetting("room-cabinet-notice","");
        s.ImportAsync(Path.Combine(AppContext.BaseDirectory,"fixture.csv"),"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default).GetAwaiter().GetResult();
        string startName=s.Graph.Nodes.Single(n=>n.Name.EndsWith("-GF271")).Name,endName=s.Graph.Nodes.Single(n=>n.Name.EndsWith("-GF272")).Name;
        using var form=new MainForm(s);form.Size=new Size((int)(1386*form.DeviceDpi/96f),(int)(913*form.DeviceDpi/96f));int passed=0;var log=new List<string>();T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,Hidden)!.GetValue(form)!;
        void Check(bool ok,string label){if(!ok)throw new Exception(label);passed++;log.Add("PASS "+label);}
        void Shot(string name){form.Refresh();Application.DoEvents();using var bmp=new Bitmap(form.ClientSize.Width,form.ClientSize.Height);using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(form.PointToScreen(Point.Empty),Point.Empty,form.ClientSize);bmp.Save(Path.Combine(output,name+".png"));}
        async Task Idle(int ms=300){await Task.Delay(ms);Application.DoEvents();}
        form.Shown+=async(_,_)=>{
            try
            {
                await Idle(800);Shot("初始界面");var tabs=Field<TabControl>("tabs");var controls=new List<Control>();void Walk(Control c){controls.Add(c);foreach(Control child in c.Controls)Walk(child);}Walk(form);
                var settings=controls.OfType<Button>().Single(b=>b.Text=="设置");Check(!(bool)settings.GetType().GetProperty("Primary")!.GetValue(settings)!&&!(bool)settings.GetType().GetProperty("Selected")!.GetValue(settings)!,"settings is a neutral utility button");
                Check(controls.OfType<Button>().Single(b=>b.Text=="路由配置").GetType().GetProperty("Selected")!.GetValue(controls.OfType<Button>().Single(b=>b.Text=="路由配置")) is true,"current route navigation has selected state from first paint");
                var start=Field<NodePicker>("start");var end=Field<NodePicker>("end");start.SelectNode(s.Graph.Nodes.Single(n=>n.Name==startName));end.SelectNode(s.Graph.Nodes.Single(n=>n.Name==endName));
                await (Task)typeof(MainForm).GetMethod("SearchRoutes",Hidden)!.Invoke(form,null)!;await Idle();var grid=Field<DataGridView>("segments");Check(grid.Rows.Count==2,"route search populates oriented real segment table");
                bool Headers(DataGridView g)=>g.ColumnHeadersHeight>=TextRenderer.MeasureText("测试",g.ColumnHeadersDefaultCellStyle.Font??g.Font).Height+g.ColumnHeadersDefaultCellStyle.Padding.Vertical&&g.ColumnHeadersHeight<=3*TextRenderer.MeasureText("测试",g.ColumnHeadersDefaultCellStyle.Font??g.Font).Height+g.ColumnHeadersDefaultCellStyle.Padding.Vertical;
                Check(Headers(grid),"segment headers fit actual font and padding at "+form.DeviceDpi+" DPI");Shot("路由配置-运行截图");
                var path=Field<PathDiagram>("pathView");var ns=Enumerable.Range(0,8).Select(i=>new Node(i,"设施"+i+"/ODF0"+i,"机房",true)).ToArray();var es=Enumerable.Range(0,7).Select(i=>new Edge(i,i.ToString(),"",i,i+1,100,2,4,"")).ToArray();var graph=new Graph(ns,es,"test");var route=new Route(Enumerable.Range(0,8).ToArray(),Enumerable.Range(0,7).ToArray(),700,0,0,2){Steps=es.Select(e=>new RouteStep(e.Index,e.A,e.B)).ToArray()};path.SetRoute(graph,route);await Idle();
                SendMessageW(path.Handle,0x20a,unchecked((IntPtr)((uint)(ushort)(short)-120<<16)),IntPtr.Zero);Application.DoEvents();int afterWheel=-path.AutoScrollPosition.X;Check(afterWheel>0,"real WM_MOUSEWHEEL horizontally scrolls the connection diagram");
                IntPtr PointAt(int x,int y)=>(IntPtr)((y<<16)|(x&0xffff));SendMessageW(path.Handle,0x201,(IntPtr)1,PointAt(300,80));SendMessageW(path.Handle,0x200,(IntPtr)1,PointAt(140,80));SendMessageW(path.Handle,0x202,IntPtr.Zero,PointAt(140,80));Application.DoEvents();Check(-path.AutoScrollPosition.X>afterWheel,"mouse down move and up drag the diagram horizontally");
                tabs.SelectedIndex=1;await Idle();var resources=Field<DataGridView>("resourceGrid");Check(Headers(resources),"resource table header fits actual font");int col=resources.Columns.Cast<DataGridViewColumn>().First(c=>c.Visible).Index;resources.Columns[col].Width=250;typeof(MainForm).GetMethod("RememberGrid",Hidden)!.Invoke(form,[resources]);form.Width+=40;await Idle();Check(resources.Columns[col].Width==250,"user column width survives window resize");Shot("中继段资源-运行截图");
                tabs.SelectedIndex=2;await Idle();Shot("节点类型-运行截图");var inner=Field<TabControl>("maintenanceTabs");inner.SelectedIndex=2;await Idle();Shot("机房归属-运行截图");tabs.SelectedIndex=3;await Idle();Shot("数据导入-运行截图");
                // Exercise popup lifetime while typing using the actual timer and dropdown.
                tabs.SelectedIndex=0;start.Clear();var input=(TextBox)typeof(NodePicker).GetField("input",Hidden)!.GetValue(start)!;input.Focus();input.Text="GF271";await Idle(500);var popup=(ToolStripDropDown)typeof(NodePicker).GetField("dropdown",Hidden)!.GetValue(start)!;Check(popup.Visible&&input.ContainsFocus,"endpoint search popup remains open while typing");
                var list=(ListBox)typeof(NodePicker).GetField("list",Hidden)!.GetValue(start)!;Check(list.Items.Count==1,"endpoint search filters exact fixture result");list.SelectedIndex=0;SendMessageW(list.Handle,0x201,(IntPtr)1,PointAt(15,15));SendMessageW(list.Handle,0x202,IntPtr.Zero,PointAt(15,15));await Idle();Check(start.Selected?.Name==startName&&!popup.Visible,"mouse click commits endpoint and closes dropdown");
                log.Add("ALL PASS: "+passed+" UI checks; DeviceDpi="+form.DeviceDpi+"; Font="+form.Font.Name);File.WriteAllLines(Path.Combine(output,"ui-checks.txt"),log);form.Close();
            }
            catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines(Path.Combine(output,"ui-checks.txt"),log);Environment.ExitCode=1;form.Close();}
        };Application.Run(form);
    }
}
