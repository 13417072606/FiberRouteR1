using System.Data;
using FiberRoute.Core;
namespace FiberRoute.App;

public sealed class TableMappingDialog:Form
{
    public const string Filter="支持的表格|*.csv;*.tsv;*.txt;*.xls;*.xlsx|CSV / 文本|*.csv;*.tsv;*.txt|Excel 工作簿|*.xls;*.xlsx";
    readonly string path;
    readonly ComboBox encoding=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=130},sheet=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=200},nameColumn=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=200},typeColumn=new(){DropDownStyle=ComboBoxStyle.DropDownList,Width=200};
    readonly Label info=new(){AutoSize=true,MaximumSize=new Size(800,0)};
    readonly DataGridView preview=new(){Dock=DockStyle.Fill,ReadOnly=true,AllowUserToAddRows=false,AutoSizeColumnsMode=DataGridViewAutoSizeColumnsMode.DisplayedCells,RowHeadersVisible=false};
    readonly Button accept=new(){Text="生成类型预览",AutoSize=true,Enabled=false};bool loading;
    public string FileEncoding{get;private set;}=Csv.Auto;
    public string? Sheet{get;private set;}
    public string NameColumn=>nameColumn.Text;public string TypeColumn=>typeColumn.Text;
    public TableMappingDialog(string file)
    {
        path=file;Text="节点类型表 · 编码、工作表与字段映射";Width=900;Height=600;MinimumSize=new Size(720,480);StartPosition=FormStartPosition.CenterParent;Font=new Font("Microsoft YaHei UI",10);
        encoding.Items.AddRange([Csv.Auto,"UTF-8","GB18030","GBK","UTF-16LE","UTF-16BE"]);encoding.SelectedIndex=0;
        var top=new FlowLayoutPanel{Dock=DockStyle.Top,AutoSize=true,Padding=new Padding(10)};
        foreach(var item in new Control[]{new Label{Text="编码",AutoSize=true},encoding,new Label{Text="工作表",AutoSize=true},sheet,new Label{Text="名称字段",AutoSize=true},nameColumn,new Label{Text="类型字段",AutoSize=true},typeColumn,accept,info})top.Controls.Add(item);
        Controls.Add(preview);Controls.Add(top);
        encoding.SelectedIndexChanged+=async(_,_)=>{if(!loading)await LoadPreview();};sheet.SelectedIndexChanged+=async(_,_)=>{if(!loading)await LoadPreview();};
        accept.Click+=(_,_)=>{if(!loading&&nameColumn.SelectedIndex>=0&&typeColumn.SelectedIndex>=0){if(NameColumn==TypeColumn){MessageBox.Show("名称与类型请选择不同字段。");return;}DialogResult=DialogResult.OK;}};
        Shown+=async(_,_)=>await LoadPreview();
    }
    async Task LoadPreview()
    {
        if(loading)return;loading=true;accept.Enabled=false;preview.DataSource=null;nameColumn.Items.Clear();typeColumn.Items.Clear();info.Text="正在识别表格…";
        try
        {
            string enc=encoding.Text,chosen=sheet.Text;
            var table=await Task.Run(()=>Tabular.Inspect(path,enc));if(IsDisposed)return;
            sheet.Items.Clear();sheet.Items.AddRange(table.Sheets);sheet.Enabled=table.Sheets.Length>0;encoding.Enabled=table.Sheets.Length==0;
            if(sheet.Enabled)sheet.SelectedItem=table.Sheets.Contains(chosen)?chosen:table.Sheets[0];
            Sheet=sheet.Enabled?sheet.Text:null;FileEncoding=table.Sheets.Length==0?table.Encoding:enc;
            var rows=await Task.Run(()=>Tabular.Read(path,FileEncoding,Sheet).Take(11).ToArray());if(IsDisposed)return;
            if(rows.Length==0)throw new InvalidDataException("所选工作表为空");var headers=Tabular.Headers(rows[0]);
            nameColumn.Items.AddRange(headers);typeColumn.Items.AddRange(headers);nameColumn.SelectedItem=headers.Contains("节点名称")?"节点名称":headers[0];typeColumn.SelectedItem=headers.Contains("节点类型")?"节点类型":headers[^1];
            var data=new DataTable();foreach(var h in headers)data.Columns.Add(h);foreach(var row in rows.Skip(1)){var r=data.NewRow();for(int i=0;i<Math.Min(row.Length,headers.Length);i++)r[i]=row[i];data.Rows.Add(r);}preview.DataSource=data;
            info.Text=$"{table.Format} · {table.Encoding} · 当前为前10行；生成后在节点类型页独立核对并确认。";accept.Enabled=headers.Length>=2;
        }
        catch(Exception e){if(!IsDisposed)info.Text="读取失败："+e.Message;}
        finally{loading=false;}
    }
}
