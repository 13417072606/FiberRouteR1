"""Build a review workbook from an existing route CSV; do not infer missing links."""
import csv
import sys
from collections import defaultdict
from decimal import Decimal
from pathlib import Path
import xlsxwriter

source, destination = map(Path, sys.argv[1:3])
rows = list(csv.DictReader(source.open(encoding="utf-8-sig", newline="")))
routes = defaultdict(list)
for row in rows:
    routes[row["方案"]].append(row)
for group in routes.values():
    group.sort(key=lambda r: int(r["段序"]))
destination.parent.mkdir(parents=True, exist_ok=True)
book = xlsxwriter.Workbook(destination, {"strings_to_formulas": False, "strings_to_urls": False})
book.set_properties({"title": "路由方案导出模板 · 待确认", "comments": "由用户提供的路由方案.csv生成；仅调整组织方式，不推断缺失数据。"})
ink, teal, muted = "#142B46", "#008D98", "#65778E"
fmt = {}
def style(name, **kw):
    fmt[name] = book.add_format({"font_name": "微软雅黑", "font_size": 11, "font_color": ink, "valign": "vcenter", **kw})
style("title", font_size=22, bold=True, font_color=teal)
style("subtitle", font_color=muted, text_wrap=True)
style("section", font_size=13, bold=True, bg_color="#E3F5F5")
style("head", bold=True, bg_color="#EEF3F9", text_wrap=True)
style("text", text_wrap=True)
style("stripe", bg_color="#F5FAFC", text_wrap=True)
style("number", num_format="0.##", align="center")
style("number_stripe", num_format="0.##", align="center", bg_color="#F5FAFC")
style("good", font_color="#00796D", bg_color="#DFF6EE", align="center", text_wrap=True)
style("note", font_color="#A55A00", bg_color="#FFF3DD", text_wrap=True)
style("link", font_color=teal, underline=True, align="center")
style("id", num_format="@", text_wrap=True)
def setup(sheet, widths, landscape=True):
    sheet.hide_gridlines(2)
    sheet.set_zoom(85)
    for i, width in enumerate(widths):
        sheet.set_column(i, i, width)
    sheet.set_default_row(26)
    sheet.set_landscape() if landscape else sheet.set_portrait()
    sheet.set_paper(9)
    sheet.fit_to_pages(1, 0)
    sheet.set_margins(.3, .3, .45, .45)
    sheet.set_footer("&L路由方案 · 格式确认稿&R第 &P / &N 页")

overview = book.add_worksheet("路由总览")
setup(overview, [12, 42, 42, 12, 15, 12, 17, 17, 20, 12, 14])
overview.merge_range(0, 0, 0, 10, "路由方案导出模板 · 待确认", fmt["title"])
overview.set_row(0, 42)
overview.merge_range(1, 0, 1, 10, f"基于本次 CSV 的 {len(routes)} 条路由、{len(rows)} 段中继段。每条路由单独一页；点击“查看详情”可跳转。", fmt["subtitle"])
overview.merge_range(2, 0, 2, 10, "最小空闲芯数＝该路由各中继段空闲芯数的最小值；不累加。数值为资源快照，不代表现场已核验可接续。", fmt["subtitle"])
headers = ["路由编号", "起点设施", "终点设施", "需求芯数", "总长度（米）", "跳纤（次）", "最小空闲芯数", "需求后最小余量", "容量状态", "中继段数", "逐段明细"]
overview.write_row(4, 0, headers, fmt["head"])
overview.set_row(4, 38)
trace = book.add_worksheet("资源追溯")
setup(trace, [12, 10, 34, 65])
trace.merge_range(0, 0, 0, 3, "资源追溯 · 长 ID 按文本保存", fmt["title"])
trace.write_row(2, 0, ["路由编号", "段序", "原始 ID（文本）", "中继段名称"], fmt["head"])
trace_row = 3
for index, (key, group) in enumerate(routes.items()):
    first = group[0]
    name = f"方案{int(key):02}"
    demand = int(first["需求芯数"])
    length = sum(Decimal(r["长度米"] or "0") for r in group)
    minimum = min(int(r["空闲"]) for r in group)
    assert length.quantize(Decimal("0.01")) == Decimal(first["总已记录米"]).quantize(Decimal("0.01"))
    assert minimum == int(first.get("路由最小空闲芯数", first.get("瓶颈空闲")))
    values = [name, first["始端"], group[-1]["终端"], demand, float(length), int(first["跳纤"]), minimum, minimum-demand, first["容量状态"], len(group)]
    for col, value in enumerate(values):
        overview.write(5+index, col, value, fmt["number" if isinstance(value, (int, float)) else "text"])
    overview.write_url(5+index, 10, f"internal:'{name}'!A1", fmt["link"], "查看详情")
    overview.set_row(5+index, 60)
    sheet = book.add_worksheet(name)
    setup(sheet, [8, 42, 42, 14, 12, 12, 12, 16, 22])
    sheet.merge_range(0, 0, 0, 7, f"{name} · 路由方案明细", fmt["title"])
    sheet.write_url(0, 8, "internal:'路由总览'!A1", fmt["link"], "返回总览")
    sheet.set_row(0, 42)
    sheet.merge_range(2, 0, 2, 8, f"{first['容量状态']}    |    需求 {demand} 芯    |    总长 {length} 米    |    跳纤 {first['跳纤']} 次    |    最小空闲 {minimum} 芯    |    需求后最小余量 {minimum-demand} 芯", fmt["section"])
    sheet.set_row(2, 36)
    sheet.merge_range(4, 0, 4, 8, "起点："+first["始端"], fmt["text"])
    sheet.merge_range(5, 0, 5, 8, "终点："+group[-1]["终端"], fmt["text"])
    sequence = [first["始端"]]
    for j, row in enumerate(group):
        if j and row["始端"] != group[j-1]["终端"]:
            sequence.append("【内部衔接待核对】"+row["始端"])
        sequence.append(row["终端"])
    sheet.merge_range(7, 0, 8, 8, "实际设施连接顺序：\n"+" → ".join(sequence), fmt["subtitle"])
    sheet.set_row(7, 30)
    sheet.set_row(8, 34)
    detail_headers = ["段序", "始端设施", "终端设施", "长度（米）", "空闲芯数", "总芯数", "需求芯数", "需求后余量", "芯数校核"]
    sheet.write_row(10, 0, detail_headers, fmt["head"])
    sheet.set_row(10, 36)
    line = 11
    for j, row in enumerate(group):
        if j and row["始端"] != group[j-1]["终端"]:
            sheet.merge_range(line, 0, line, 8, "内部衔接待核对："+group[j-1]["终端"]+" → "+row["始端"]+"。原 CSV 未提供所属机房/集群依据及内部可用芯数，不计作实长中继段。", fmt["note"])
            sheet.set_row(line, 52)
            line += 1
        base = "stripe" if j%2 else "text"
        numeric = "number_stripe" if j%2 else "number"
        values = [int(row["段序"]), row["始端"], row["终端"], float(row["长度米"]) if row["长度米"] else None, int(row["空闲"]), int(row["总芯"]), demand, int(row["空闲"])-demand, "满足需求" if int(row["空闲"])>=demand else "芯数不足"]
        for col, value in enumerate(values):
            sheet.write(line, col, value, fmt["good" if col==8 and values[-1]=="满足需求" else numeric if isinstance(value, (int, float)) else base])
        sheet.set_row(line, 64)
        trace.write_row(trace_row, 0, [name, int(row["段序"])], fmt["text"])
        trace.write_string(trace_row, 2, row["原始ID"], fmt["id"])
        trace.write_string(trace_row, 3, row["中继段"], fmt["text"])
        trace.set_row(trace_row, 42)
        trace_row += 1
        line += 1
    sheet.merge_range(line+1, 0, line+1, 8, f"本路由合计：{len(group)} 段 · {length} 米；空闲芯数不累加；最小空闲 {minimum} 芯，扣除本次需求后最小余量 {minimum-demand} 芯。", fmt["section"])
    sheet.set_row(line+1, 36)
    sheet.merge_range(line+3, 0, line+4, 8, "说明：当前数据只有空闲/总芯数量，未提供可分配的具体纤芯编号，不编造芯号。同机房/集群内部接续的可用芯数需另外核验。完整设备名称保留；原始长 ID 见“资源追溯”。", fmt["subtitle"])
    sheet.freeze_panes(11, 1)
    sheet.print_area(0, 0, line+4, 8)
    sheet.repeat_rows(10)
overview.freeze_panes(5, 1)
overview.autofilter(4, 0, 4+len(routes), 9)
overview.print_area(0, 0, 4+len(routes), 10)
trace.freeze_panes(3, 2)
trace.autofilter(2, 0, trace_row-1, 3)
notes = book.add_worksheet("字段说明")
setup(notes, [26, 100], False)
notes.merge_range(0, 0, 0, 1, "导出格式说明 · 待确认", fmt["title"])
notes.set_row(0, 42)
definitions = [
    ("组织方式", "路由总览一条路由一行；方案01～08分别列出各自中继段。资源追溯保存原始 ID，避免长数字被 Excel 截断。"),
    ("最小空闲芯数", "各实长中继段空闲芯数的最小值。如空闲为40、21、38、9芯，则该路由最小空闲为9芯。"),
    ("需求后余量", "逐段：该段空闲芯数减本次需求。路由层面：路由最小空闲芯数减本次需求。不是分配后的实际占用记录。"),
    ("路由总长度", "只累加各段已知长度，不把机房或集群内部衔接计成额外中继段。缺失长度不得写作0；本次样例各段均有已知长度。"),
    ("内部衔接", "原 CSV 只包含中继段，未完整导出内部跳接依据。方案07存在相邻段端点名称不同，模板明确标注待核对，不推断它们已连通。正式导出将从路由步骤中取得机房/集群关系。"),
    ("具体纤芯编号", "当前 CSV 只含芯数，不含可用芯号。模板仅展示数量，不推断可分配的具体纤芯编号。"),
    ("容量状态", "本样例全部满足输入的2芯需求；仍受资源快照准确性、现场占用及内部接续条件限制。"),
    ("原始 ID", "所有原始 ID 按 Excel 文本单元格存储。主明细不展示 ID，资源追溯表可供查验。"),
    ("查询条件", f"排序：{rows[0]['排序方式']}；需求：{rows[0]['需求芯数']}芯；最大跳纤：{rows[0]['最大跳纤']}；规避类型：{rows[0]['规避类型']}；规避具体节点：{rows[0]['规避节点'] or '无'}。"),
    ("数据版本", rows[0]["资源版本"]+"（沿用原 CSV 的时区及时间，未改写为当前数据）"),
    ("当前状态", "这是使用本次 CSV 制作的格式确认稿；尚未把 Excel 导出格式替换进软件，待用户确认后实现。"),
]
for i, pair in enumerate(definitions, 2):
    notes.write(i, 0, pair[0], fmt["head"])
    notes.write(i, 1, pair[1], fmt["text"])
    notes.set_row(i, 66)
book.close()
print(destination)
print(f"{len(routes)} routes / {len(rows)} segments / {len(routes)+3} sheets")
