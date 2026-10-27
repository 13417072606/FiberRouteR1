using FiberRoute.App;
namespace FiberRoute.Brand;
internal static class Program
{
    [STAThread]static int Main(string[] args)
    {
        ApplicationConfiguration.Initialize();
        if(args is ["--apply-existing"]){string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));try{BrandApplication.Install(Path.Combine(root,"资源","品牌原图.png"),root);return 0;}catch(Exception e){try{File.WriteAllText(Path.Combine(root,"品牌替换失败.txt"),e.ToString());}catch{}return 1;}}
        using var form=new Form{Text="更换品牌图标",ClientSize=new Size(680,390),StartPosition=FormStartPosition.CenterScreen,Font=new Font("Microsoft YaHei UI",10),BackColor=Color.FromArgb(242,246,250),AutoScaleDimensions=new SizeF(96,96),AutoScaleMode=AutoScaleMode.Dpi};
        string brandIcon=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","光路调度智能分析.ico"));if(!File.Exists(brandIcon))brandIcon=Path.Combine(AppContext.BaseDirectory,"app.ico");if(File.Exists(brandIcon))form.Icon=new Icon(brandIcon);
        var title=new Label{Text="使用原PNG，等比生成完整图标",Font=new Font(form.Font.FontFamily,19,FontStyle.Bold),Dock=DockStyle.Top,Height=60,Padding=new Padding(16,14,0,0)};
        var description=new Label{Text="请先关闭主程序和管理方发布工具。\n选择你提供的PNG原图后，将同时更新启动器、标题栏、任务栏及快捷方式所用图标。\n保留完整比例、Y形和透明度，不修改原图文件或业务数据库。",Dock=DockStyle.Top,Height=110,Padding=new Padding(18,12,18,0)};
        var state=new Label{Dock=DockStyle.Bottom,Height=70,Padding=new Padding(18),Text="原图和多尺寸ICO保存在运行目录的“资源”文件夹。"};
        var picture=new PictureBox{Dock=DockStyle.Fill,SizeMode=PictureBoxSizeMode.Zoom,BackColor=Color.White};
        var action=new Button{Text="选择PNG并替换图标",Dock=DockStyle.Bottom,Height=48,BackColor=Color.Teal,ForeColor=Color.White,FlatStyle=FlatStyle.Flat};
        var layout=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=1,RowCount=5};layout.ColumnStyles.Add(new(SizeType.Percent,100));foreach(float height in new[]{60f,96f})layout.RowStyles.Add(new(SizeType.Absolute,height));layout.RowStyles.Add(new(SizeType.Percent,100));layout.RowStyles.Add(new(SizeType.Absolute,70));layout.RowStyles.Add(new(SizeType.Absolute,48));layout.Controls.Add(title,0,0);layout.Controls.Add(description,0,1);layout.Controls.Add(picture,0,2);layout.Controls.Add(state,0,3);layout.Controls.Add(action,0,4);form.Controls.Add(layout);
        action.Click+=(_,_)=>
        {
            try
            {
                if(System.Diagnostics.Process.GetProcessesByName("FiberRouteR1").Any()||System.Diagnostics.Process.GetProcessesByName("FiberRoute.Publisher").Any())throw new InvalidOperationException("请先关闭主程序和管理方发布工具，再替换图标。");
                using var pick=new OpenFileDialog{Title="选择原始PNG标识",Filter="PNG原图|*.png"};if(pick.ShowDialog(form)!=DialogResult.OK)return;string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,".."));BrandApplication.Install(pick.FileName,root);picture.Image?.Dispose();using var source=Image.FromFile(pick.FileName);picture.Image=new Bitmap(source);using var icon=new Icon(Path.Combine(root,"资源","光路调度智能分析.ico"));form.Icon=(Icon)icon.Clone();state.Text="替换成功。请从原快捷方式启动主程序；若桌面仍显示缓存，可在设置中重新创建快捷方式。";
            }
            catch(Exception e){MessageBox.Show(form,e.Message,"图标未替换",MessageBoxButtons.OK,MessageBoxIcon.Warning);}
        };
        Application.Run(form);picture.Image?.Dispose();return 0;
    }
}
