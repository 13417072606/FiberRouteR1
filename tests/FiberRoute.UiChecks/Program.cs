using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;
using System.Runtime.InteropServices;
internal static class Program
{
    [DllImport("user32.dll")] static extern IntPtr SendMessageW(IntPtr h,int msg,IntPtr w,IntPtr l);
    [DllImport("user32.dll")] static extern bool GetWindowRect(IntPtr h,[Out] int[] rect);
    const BindingFlags Hidden=BindingFlags.Instance|BindingFlags.NonPublic;
    [STAThread] static void Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args.Length==2&&args[0]=="--delivery-check")
        {
            string root=Path.GetFullPath(args[1]);using var launcher=System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo(Path.Combine(root,"光路调度智能分析.exe")){UseShellExecute=false,WorkingDirectory=root})!;
            System.Diagnostics.Process? app=null;
            try
            {
                for(int i=0;i<450;i++){app=System.Diagnostics.Process.GetProcessesByName("FiberRouteR1").FirstOrDefault(p=>p.MainWindowTitle.Contains("R1.3.2"));if(app!=null)break;Thread.Sleep(100);}
                if(app==null)throw new Exception("original launcher did not open R1.3.2");Thread.Sleep(1500);var rect=new int[4];if(GetWindowRect(app.MainWindowHandle,rect)&&rect[2]>rect[0]&&rect[3]>rect[1]){using var bmp=new Bitmap(rect[2]-rect[0],rect[3]-rect[1]);using(var g=Graphics.FromImage(bmp))g.CopyFromScreen(new Point(rect[0],rect[1]),Point.Empty,bmp.Size);bmp.Save(Path.Combine(root,"完整程序启动-运行截图.png"));}
                if(File.Exists(Path.Combine(root,"数据","机房归属基线.json")))throw new Exception("owned baseline was not consumed and cleaned");Console.WriteLine("PASS actual self-contained launcher opens R1.3.2 and consumes the room baseline");
            }
            finally{if(app!=null){app.CloseMainWindow();if(!app.WaitForExit(10000))app.Kill();app.Dispose();}}return;
        }
        if(args.Length>0&&args[0]=="--brand-check")
        {
            var type=typeof(MainForm).Assembly.GetType("FiberRoute.App.BrandAssets")!;string png=(string)type.GetProperty("OriginalPath")!.GetValue(null)!,ico=(string)type.GetProperty("IconPath")!.GetValue(null)!;
            if(File.Exists(png)||File.Exists(ico))throw new Exception("brand fixture would overwrite an existing asset");Directory.CreateDirectory(Path.GetDirectoryName(png)!);
            try
            {
                using(var original=new Bitmap(120,60)){using(var g=Graphics.FromImage(original))g.Clear(Color.Teal);original.Save(png,System.Drawing.Imaging.ImageFormat.Png);}
                var loaded=((Image?,Icon?))type.GetMethod("Load")!.Invoke(null,null)!;using var image=loaded.Item1;using var icon=loaded.Item2;
                if(image?.Width!=120||image.Height!=60||icon==null)throw new Exception("original image or icon not loaded");using var stream=File.OpenRead(ico);using var reader=new BinaryReader(stream);reader.ReadUInt16();reader.ReadUInt16();int count=reader.ReadUInt16();if(count!=7)throw new Exception("multi-size icon count");
                for(int i=0;i<count;i++)
                {
                    stream.Position=6+i*16;int size=reader.ReadByte();if(size==0)size=256;reader.ReadBytes(7);int length=reader.ReadInt32(),offset=reader.ReadInt32();stream.Position=offset;using var iconData=new MemoryStream(reader.ReadBytes(length));using var bitmap=new Bitmap(iconData);
                    int top=size,bottom=-1;for(int y=0;y<size;y++)if(bitmap.GetPixel(size/2,y).A>127){top=Math.Min(top,y);bottom=Math.Max(bottom,y);}
                    if(bottom-top+1>size/2+2||top<size/5)throw new Exception("non-square source was stretched rather than fitted");
                }
                Console.WriteLine("PASS original PNG retained; 7 icon sizes; 2:1 source fits proportionally and centrally at every size");return;
            }
            finally{File.Delete(png);File.Delete(ico);}
        }
        string output=args.Length>0?args[0]:Path.Combine(Path.GetTempPath(),"fiber-ui-checks");Directory.CreateDirectory(output);string data=Path.Combine(output,"data");var s=new Store(data);s.SetSetting("room-cabinet-notice","");
        s.ImportAsync(Path.Combine(AppContext.BaseDirectory,"fixture.csv"),"自动",Store.Fields.ToDictionary(x=>x,x=>x),1,null,default).GetAwaiter().GetResult();
        string startName=s.Graph.Nodes.Single(n=>n.Name.EndsWith("-GF271")).Name,endName=s.Graph.Nodes.Single(n=>n.Name.EndsWith("-GF272")).Name;
        using var form=new MainForm(s);form.Size=new Size((int)(1280*form.DeviceDpi/96f),(int)(800*form.DeviceDpi/96f));int passed=0;var log=new List<string>();T Field<T>(string name)=>(T)typeof(MainForm).GetField(name,Hidden)!.GetValue(form)!;
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
                IntPtr PointAt(int x,int y)=>(IntPtr)((y<<16)|(x&0xffff));SendMessageW(path.Handle,0x201,(IntPtr)1,PointAt(300,10));SendMessageW(path.Handle,0x200,(IntPtr)1,PointAt(140,10));SendMessageW(path.Handle,0x202,IntPtr.Zero,PointAt(140,10));Application.DoEvents();Check(-path.AutoScrollPosition.X>afterWheel,"mouse down move and up drag the diagram horizontally");
                path.ResetLayout();await Idle();var first=path.NodePositions[0];var other=path.NodePositions[1];var before=path.ConnectionGeometry(0);int dpi=form.DeviceDpi;
                SendMessageW(path.Handle,0x201,(IntPtr)1,PointAt((int)(first.X*dpi/96f),(int)(first.Y*dpi/96f)));SendMessageW(path.Handle,0x200,(IntPtr)1,PointAt((int)((first.X+48)*dpi/96f),(int)((first.Y+28)*dpi/96f)));SendMessageW(path.Handle,0x202,IntPtr.Zero,PointAt((int)((first.X+48)*dpi/96f),(int)((first.Y+28)*dpi/96f)));Application.DoEvents();
                var after=path.ConnectionGeometry(0);Check(path.NodePositions[0]!=first&&path.NodePositions[1]==other,"dragging one node moves only that node");Check(before.Start!=after.Start&&before.End!=after.End&&before.Label!=after.Label,"connected endpoints and distance label follow the dragged node in real time");Check(route.Length==700&&route.Edges.Length==7&&es.All(e=>e.Length==100),"diagram drag never mutates real route topology or lengths");path.ResetLayout();Check(path.NodePositions[0]==first,"reset layout restores node positions");
                var grouped=new Graph([new(0,"A/ODF01","机房",true),new(1,"A/GJ01","光交箱",true),new(2,"B/ODF01","机房",true),new(3,"B/ODB01","机房",true)],[new(0,"test","",1,2,2044.87,36,48,"")],"room-view");grouped.Rooms["A机房"]=[0,1];grouped.Rooms["B机房"]=[2,3];grouped.SetClusters([new("A","A机房","机房",["A/ODF01","A/GJ01"],"table","已确认","",true),new("B","B机房","机房",["B/ODF01","B/ODB01"],"table","已确认","",true)]);
                path.SetRoute(grouped,new([0,2],[0],2044.87,0,0,36){Steps=[new(0,1,2)],Transfers=[new(0,1,0),new(2,3,1)]});Check(path.DisplayedNodeCount==2,"same room representative and actual member fold into one visible node");Check(path.RoomWarnings.Any(x=>x.Contains("A/GJ01")&&x.Contains("所属机房：A机房")),"cabinet room reminder is attached to the route node");
                var paused=new Graph(grouped.Nodes,grouped.Edges,"paused-cabinet");paused.RoomAffiliations[1]=new("A/GJ01","A机房","表格","已确认","已人工暂停归并",false,true);path.SetRoute(paused,new([1,2],[0],2044.87,0,0,36){Steps=[new(0,1,2)]});Check(path.RoomWarnings.Any(x=>x.Contains("所属机房：A机房")),"standalone paused cabinet still exposes its recorded room warning without being merged");
                typeof(MainForm).GetMethod("ShowRoute",Hidden)!.Invoke(form,null);Check(!grid.Columns.Cast<DataGridViewColumn>().Any(c=>c.Visible&&c.Name.Contains("ID")),"route resource details omit the ID column");
                var cap=Field<CheckBox>("requireCapacity");Check(cap.Checked,"capacity-only checkbox defaults to checked");
                Field<NumericUpDown>("demand").Value=2;await (Task)typeof(MainForm).GetMethod("SearchRoutes",Hidden)!.Invoke(form,null)!;await Idle();Check(Field<List<Route>>("routes").Count==0,"checked capacity filter hides insufficient routes from the list and map");
                cap.Checked=false;await Idle(600);Check(Field<List<Route>>("routes").Count>0&&Field<Label>("routeMetrics").Text.Contains("芯数不足"),"unchecked capacity filter displays explicitly marked shortage references");
                cap.Checked=true;Check(Field<List<Route>>("routes").All(r=>r.HasCapacity(2)),"rechecking hides old shortage references immediately");await Idle(600);Field<NumericUpDown>("demand").Value=1;await (Task)typeof(MainForm).GetMethod("SearchRoutes",Hidden)!.Invoke(form,null)!;await Idle();
                var modes=Field<ComboBox>("order");Check(modes.Items.Cast<string>().SequenceEqual(new[]{"满足需求","距离最短","跳纤最少","余量最多"}),"candidate modes have no misleading composite score");
                Check(!controls.OfType<Label>().Any(c=>c.Text.Contains("端点豁免")||c.Text.Contains("非地理地图")),"only obsolete display wording is removed");
                tabs.SelectedIndex=1;await Idle();var resources=Field<DataGridView>("resourceGrid");Check(Headers(resources),"resource table header fits actual font");Check(resources.CellBorderStyle==DataGridViewCellBorderStyle.None&&!resources.RowHeadersVisible,"tables are borderless without a dark row-header selection stripe");int col=resources.Columns.Cast<DataGridViewColumn>().First(c=>c.Visible).Index;int originalWidth=resources.Columns[col].Width;resources.Columns[col].Width=250;typeof(MainForm).GetMethod("RememberGrid",Hidden)!.Invoke(form,[resources]);form.Width+=40;await Idle();Check(resources.Columns[col].Width==250,"user column width survives window resize");resources.Columns[col].Width=originalWidth;typeof(MainForm).GetMethod("RememberGrid",Hidden)!.Invoke(form,[resources]);Shot("中继段资源-运行截图");
                tabs.SelectedIndex=2;await Idle();Shot("节点类型-运行截图");var inner=Field<TabControl>("maintenanceTabs");inner.SelectedIndex=2;await Idle();Shot("机房归属-运行截图");tabs.SelectedIndex=3;await Idle();Field<TextBox>("importPath").Text=Path.Combine(AppContext.BaseDirectory,"fixture.csv");await (Task)typeof(MainForm).GetMethod("ReadPreview",Hidden)!.Invoke(form,null)!;await Idle();var preview=Field<DataGridView>("importGrid");Check(preview.Rows.Count==12&&!preview.Columns["ID"].Visible,"import preview reads Excel-compatible workflow without displaying the ID column");Shot("数据导入-运行截图");
                // Exercise popup lifetime while typing using the actual timer and dropdown.
                tabs.SelectedIndex=0;start.Clear();var input=(TextBox)typeof(NodePicker).GetField("input",Hidden)!.GetValue(start)!;input.Focus();input.Text="GF271";await Idle(500);var popup=(ToolStripDropDown)typeof(NodePicker).GetField("dropdown",Hidden)!.GetValue(start)!;Check(popup.Visible&&input.ContainsFocus,"endpoint search popup remains open while typing");
                var list=(ListBox)typeof(NodePicker).GetField("list",Hidden)!.GetValue(start)!;Check(list.Items.Count==1,"endpoint search filters exact fixture result");list.SelectedIndex=0;SendMessageW(list.Handle,0x201,(IntPtr)1,PointAt(15,15));SendMessageW(list.Handle,0x202,IntPtr.Zero,PointAt(15,15));await Idle();Check(start.Selected?.Name==startName&&!popup.Visible,"mouse click commits endpoint and closes dropdown");
                log.Add("ALL PASS: "+passed+" UI checks; DeviceDpi="+form.DeviceDpi+"; Font="+form.Font.Name);File.WriteAllLines(Path.Combine(output,"ui-checks.txt"),log);form.Close();
            }
            catch(Exception e){log.Add("FAIL "+e);File.WriteAllLines(Path.Combine(output,"ui-checks.txt"),log);Environment.ExitCode=1;form.Close();}
        };Application.Run(form);
    }
}
