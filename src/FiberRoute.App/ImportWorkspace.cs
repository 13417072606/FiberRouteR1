using FiberRoute.Core;
namespace FiberRoute.App;
public sealed partial class MainForm
{
    FlowLayoutPanel? importMappingBody;
    readonly Label mappingStats=new(){Dock=DockStyle.Top,Height=42,ForeColor=Theme.Muted,BackColor=Color.White,Padding=new Padding(8,10,8,4),Text="选择表格后自动识别字段，可手动调整"};
    void BuildImportWorkspace()
    {
        var tab=Tab("数据导入");resourceFormat.Items.AddRange(["标准CSV / Excel","原始导出17列（末列多值）"]);resourceFormat.SelectedIndex=0;encoding.Items.AddRange(["自动","UTF-8","GB18030","GBK","utf-16","utf-16BE"]);encoding.SelectedIndex=0;units.Items.AddRange(["米","千米"]);units.SelectedIndex=0;
        var file=new Surface{Dock=DockStyle.Top,Height=62,Padding=new Padding(12,8,12,8)};var fileRow=new TableLayoutPanel{Dock=DockStyle.Fill,ColumnCount=3,RowCount=1,BackColor=Color.White};fileRow.ColumnStyles.Add(new(SizeType.Percent,100));fileRow.ColumnStyles.Add(new(SizeType.Absolute,180));fileRow.ColumnStyles.Add(new(SizeType.Absolute,152));importPath.PlaceholderText="选择中继段表格 · CSV / XLS / XLSX";fileRow.Controls.Add(InputFrame(importPath),0,0);
        var pick=AsyncButton("选择 / 重新选择",SelectResourceFile,"upload",true);pick.Dock=DockStyle.Fill;pick.Margin=new Padding(8,0,3,0);fileRow.Controls.Add(pick,1,0);var sample=AsyncButton("载入调测数据",async()=>{if(busy)return;importPath.Text=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"..","资源","调测中继段.csv"));encoding.SelectedIndex=0;sheets.Items.Clear();units.SelectedIndex=0;await ReadPreview();});sample.Dock=DockStyle.Fill;sample.Margin=new Padding(6,0,0,0);fileRow.Controls.Add(sample,2,0);file.Controls.Add(fileRow);
        var steps=new Label{Dock=DockStyle.Top,Height=37,Text="① 选择文件     ━━━━━     ② 核对字段     ━━━━━     ③ 预览并导入",TextAlign=ContentAlignment.MiddleCenter,ForeColor=Theme.Teal,Font=new Font(Font.FontFamily,10.5f,FontStyle.Bold)};
        var leftBody=Stack();importMappingBody=leftBody;leftBody.Padding=new Padding(2);mappings.FlowDirection=FlowDirection.TopDown;mappings.WrapContents=false;mappings.Dock=DockStyle.None;mappings.AutoSize=false;mappings.Height=380;mappings.Padding=Padding.Empty;
        mappings.SizeChanged+=(_,_)=>{foreach(Control row in mappings.Controls)row.Width=Math.Max(140,mappings.ClientSize.Width-5);};leftBody.Controls.AddRange([mappingStats,mappings]);
        var advanced=Stack();advanced.Dock=DockStyle.None;advanced.Height=290;advanced.Visible=false;advanced.Controls.AddRange([L("读取格式"),resourceFormat,L("CSV编码（默认自动）"),encoding,inputInfo,L("Excel工作表"),sheets,L("原表长度单位"),units]);
        var toggle=ActionButton("高级读取选项",()=>{advanced.Visible=!advanced.Visible;});toggle.Height=34;leftBody.Controls.Add(toggle);leftBody.Controls.Add(advanced);var refresh=AsyncButton("读取 / 刷新预览",ReadPreview);refresh.Height=36;leftBody.Controls.Add(refresh);
        var left=Card("字段映射",leftBody);var right=new Surface{Dock=DockStyle.Fill,Padding=new Padding(12)};previewStats.Height=60;previewStats.Font=new Font(Font.FontFamily,10);previewStats.Text="尚未选择文件\n先预览，确认后才执行全量校验";
        var info=new Label{Dock=DockStyle.Bottom,Height=61,BackColor=Color.FromArgb(239,248,255),ForeColor=Theme.Muted,Padding=new Padding(9,6,9,4),Text="预览仅展示前20条，不代表全量校验结果。\n确认后全量校验；失败或停止保留原数据。"};right.Controls.Add(importGrid);right.Controls.Add(info);right.Controls.Add(previewStats);right.Controls.Add(new Label{Text="校验与预览",Dock=DockStyle.Top,Height=32,Font=new Font(Font.FontFamily,12,FontStyle.Bold),BackColor=Color.White});
        var body=Columns(35,left,65,right);var actions=new Surface{Dock=DockStyle.Bottom,Height=96,Padding=new Padding(10,6,10,6)};
        var buttons=Flow();buttons.Dock=DockStyle.Bottom;buttons.Padding=Padding.Empty;buttons.Height=44;buttons.Controls.AddRange([B("确认校验并更新资源",ImportData),B("停止导入",()=>job?.Cancel()),B("导出异常",ExportImportErrors),B("回退上一版",RollbackResources)]);
        actions.Controls.Add(buttons);actions.Controls.Add(new Label{Dock=DockStyle.Fill,BackColor=Color.White,ForeColor=Theme.Muted,Padding=new Padding(4,3,0,0),Text="只保存整理后的数据，不复制原始表格；无变化的导入会跳过。确认更新会清除已有路由结果。"});
        tab.Controls.Add(body);tab.Controls.Add(actions);tab.Controls.Add(file);tab.Controls.Add(steps);tab.Controls.Add(PageIntro("导入中继段表","选择文件、核对字段，预览后更新本地资源"));
    }
    async Task SelectResourceFile()
    {
        if(busy)return;using var d=new OpenFileDialog{Filter=TableInput.Filter};if(d.ShowDialog()!=DialogResult.OK)return;
        importPath.Text=d.FileName;sheets.Items.Clear();resourceFormat.SelectedIndex=Path.GetExtension(d.FileName).Equals(".csv",StringComparison.OrdinalIgnoreCase)&&await Task.Run(()=>Csv.IsResourceExport(d.FileName,"自动"))?1:0;await ReadPreview();
    }
    void ExportImportErrors(){if(errorFile==null||!File.Exists(errorFile))throw new InvalidOperationException("暂无异常报告");var p=Save("导入异常.csv");if(p!=null)File.Copy(errorFile,p,true);}
    void RollbackResources(){if(busy)return;if(MessageBox.Show("切换到上一版资源？人工类型及设置保留。","确认回退",MessageBoxButtons.YesNo)==DialogResult.Yes){store.Rollback();store.SetSetting("remoteResourceVersion","");RefreshData();}}
    Control MappingRow(string field,ComboBox combo)
    {
        var row=new TableLayoutPanel{Height=37,ColumnCount=2,RowCount=1,BackColor=Color.White,Margin=new Padding(0,2,0,2)};row.RowStyles.Add(new(SizeType.Percent,100));row.ColumnStyles.Add(new(SizeType.Percent,38));row.ColumnStyles.Add(new(SizeType.Percent,62));
        row.Controls.Add(new Label{Text=field+(Store.Required.Contains(field)?" *":""),Dock=DockStyle.Fill,TextAlign=ContentAlignment.MiddleLeft,ForeColor=Theme.Ink},0,0);combo.Dock=DockStyle.Fill;combo.Margin=new Padding(3,3,0,3);var frame=InputFrame(combo);frame.Dock=DockStyle.Fill;frame.Margin=new Padding(3,1,0,1);row.Controls.Add(frame,1,0);return row;
    }
    void UpdateImportPreviewColumns()
    {
        var fields=new[]{"始端设施","终端设施","长度","空闲数量","中继纤芯数量"};var wanted=fields.Where(map.ContainsKey).Select(x=>map[x].Text).ToHashSet();
        foreach(DataGridViewColumn c in importGrid.Columns)c.Visible=wanted.Contains(c.Name);
        mappingStats.Text=$"必需字段映射 {Store.Required.Count(x=>map.TryGetValue(x,out var c)&&c.Text!="（无）")}/{Store.Required.Length} · 可手动调整";
    }
}
