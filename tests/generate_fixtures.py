# Development-only fixtures: pip install xlwt==1.3.0 openpyxl==3.1.5
from pathlib import Path
import xlwt,openpyxl
root=Path(__file__).parent/'fixtures'
header=['资源编号','名称','长度','空闲数量','中继纤芯数量','始端设施','终端设施']
rows=[header,['000123456789012345678901','含逗号,及"引号"\n换行',0,1,2,' 甲-设施 ','乙-设施'],['0002','缺失长度',None,2,3,'乙-设施','丙设施']]
for ext in ['xls','xlsx']:
 if ext=='xls':
  book=xlwt.Workbook()
  def add(name,rows):
   s=book.add_sheet(name)
   for i,row in enumerate(rows):
    for j,v in enumerate(row):
     if v is not None:s.write(i,j,v)
 else:
  book=openpyxl.Workbook();book.remove(book.active)
  def add(name,rows):
   s=book.create_sheet(name)
   for row in rows:s.append(row)
 add('说明',[['这是说明页，请选择资源表']]);add('资源表',rows)
 add('精度风险',[header,[1234567890123456789,'数值长ID',1,1,2,'甲','乙']])
 add('类型表',[['设施','类别'],['甲-设施','机房'],['乙-设施','光交箱'],['乙-设施','站点'],['未知','机房'],['丙设施','']])
 book.save(str(root/f'import.{ext}'))
