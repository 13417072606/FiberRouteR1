using FiberRoute.App;
using FiberRoute.Core;
using System.Reflection;

static class R144UiChecks
{
    public static async Task Run(MainForm form,Store store,string output,Action<bool,string> check,Func<int,Task> idle,Action<string> shot)
    {
        const BindingFlags hidden=BindingFlags.Instance|BindingFlags.NonPublic;
        T Field<T>(string n)=>(T)typeof(MainForm).GetField(n,hidden)!.GetValue(form)!;
        void Call(string n)=>typeof(MainForm).GetMethod(n,hidden)!.Invoke(form,null);
        string name="汕头市澄海区张建鑫玩具装配-ONU001-HW-MA5671(1)-跨行完整后缀";
        string room="汕头市澄海区广益街道玩具厂汇聚点一楼机房-跨行完整后缀";
        string csv=Path.Combine(output,"r144.csv");Csv.Write(csv,new object?[][]{
            ["ID","始端设施","终端设施","长度","空闲数量","中继纤芯数量","始端区域","终端区域","始端机房","终端机房"],
            ["streets",name,"达濠街道/GJ01",10,2,4,"广益街道","达濠街道",room,""],
            ["missing","潮南区家美社区FG/GJ07","完全没有区域线索的设施",10,2,4,"未提供","","",""]});
        await store.ImportAsync(csv,"自动",Store.Fields.ToDictionary(f=>f,f=>f),1,null,default);Call("RefreshData");await idle(500);
        var tabs=Field<TabControl>("tabs");var inner=Field<TabControl>("maintenanceTabs");tabs.SelectedIndex=2;inner.SelectedIndex=0;
        Field<TextBox>("typeSearch").Text="跨行完整后缀";Field<ComboBox>("nodeArea").SelectedIndex=0;Field<ComboBox>("stateFilter").SelectedIndex=0;Field<ComboBox>("nodeType").SelectedItem="全部";Call("LoadTypes");await idle(500);
        var node=Field<DataGridView>("typeGrid");node.Columns["名称"].Width=250*form.DeviceDpi/96;node.Columns["操作"].DisplayIndex=0;node.FirstDisplayedScrollingColumnIndex=node.Columns["操作"].Index;await idle(500);
        void Fits(DataGridView grid,string column,string expected)
        {
            var row=grid.Rows.Cast<DataGridViewRow>().Single(r=>r.Cells[column].Value?.ToString()==expected);var cell=row.Cells[column];var font=cell.InheritedStyle.Font??grid.Font;
            int width=grid.Columns[column].Width-cell.InheritedStyle.Padding.Horizontal-6*form.DeviceDpi/96;
            string wrapped=(string)typeof(MainForm).GetMethod("WrapCellText",BindingFlags.NonPublic|BindingFlags.Static)!.Invoke(null,[expected,font,width])!;
            int measured=TextRenderer.MeasureText(wrapped,font,new Size(int.MaxValue,int.MaxValue),TextFormatFlags.NoPadding|TextFormatFlags.NoPrefix).Height;
            check(wrapped.Replace("\n","")==expected&&wrapped.Contains('\n')&&row.Height>=measured+cell.InheritedStyle.Padding.Vertical+16*form.DeviceDpi/96,"R144 multiline "+column+" retains complete suffix and extra vertical space at DPI "+form.DeviceDpi);
        }
        Fits(node,"名称",name);
        // Inspect the actual rendered operation text, independent of layout calculations.
        node.Refresh();await idle(300);var rect=node.GetCellDisplayRectangle(node.Columns["操作"].Index,0,true);
        using(var image=new Bitmap(rect.Width,rect.Height))
        {
            using(var g=Graphics.FromImage(image))g.CopyFromScreen(node.PointToScreen(rect.Location),Point.Empty,rect.Size);
            var ink=new List<int>();for(int y=1;y<image.Height-1;y++)for(int x=1;x<image.Width-1;x++){var c=image.GetPixel(x,y);if(c.R<90&&c.G<180&&c.B<220)ink.Add(y);}
            check(ink.Count>0&&Math.Abs((ink.Min()+ink.Max())/2.0-image.Height/2.0)<8*form.DeviceDpi/96.0,"R144 actual 修改 glyph pixels are vertically centered in the multiline row at DPI "+form.DeviceDpi);
        }
        shot("R1.4.4-节点跨行与修改居中");inner.SelectedIndex=2;Field<TextBox>("roomSearch").Text=room;Call("LoadRooms");await idle(500);var rooms=Field<DataGridView>("roomGrid");rooms.Columns["所属机房"].Width=230*form.DeviceDpi/96;await idle(400);Fits(rooms,"所属机房",room);shot("R1.4.4-机房跨行");
        tabs.SelectedIndex=1;Field<TextBox>("resourceSearch").Clear();foreach(string f in new[]{"resourceStartArea","resourceEndArea","resourceType","resourceEndType","resourceBusiness"})Field<ComboBox>(f).SelectedIndex=0;Call("LoadResources");await idle(800);
        foreach(string f in new[]{"resourceStartArea","resourceEndArea"}){var box=Field<ComboBox>(f);check(box.Items.Count==9&&ShantouRegions.Districts.All(d=>box.Items.Contains(d))&&box.Items.Contains("未提供")&&!box.Items.Contains("广益街道"),"R144 "+f+" offers six districts, one county and unresolved at DPI "+form.DeviceDpi);}
        Field<ComboBox>("resourceStartArea").SelectedItem="澄海区";await idle(700);var resource=Field<DataGridView>("resourceGrid");check(resource.Rows.Count==1&&resource.Rows[0].Cells["始端区域"].Value?.ToString()=="澄海区"&&resource.Rows[0].Cells["终端区域"].Value?.ToString()=="濠江区","R144 district filtering includes street-level source rows");shot("R1.4.4-始终端行政区归并");
        Field<ComboBox>("resourceStartArea").SelectedIndex=0;Field<ComboBox>("resourceEndArea").SelectedItem="未提供";await idle(700);check(resource.Rows.Count==1&&resource.Rows[0].Cells["始端区域"].Value?.ToString()=="潮南区"&&resource.Rows[0].Cells["终端区域"].Value?.ToString()=="未提供","R144 unknown filter preserves independent endpoint inference");
        check(form.Text.StartsWith("光路调度路由查询 R1.4.4"),"R144 application title identifies the new patch");
    }
}
