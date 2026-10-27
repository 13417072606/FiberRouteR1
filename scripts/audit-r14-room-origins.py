"""Read-only source audit for wall-location room labels in the delivered snapshot."""
from pathlib import Path
from collections import Counter
import csv
import hashlib
import json
import re
import sqlite3
import sys
import zipfile
import xlsxwriter

sources, snapshot, output = map(Path, sys.argv[1:4])
output.mkdir(parents=True, exist_ok=True)
resource = sqlite3.connect(f"file:{snapshot / 'resources.db'}?mode=ro", uri=True)
personal = sqlite3.connect(f"file:{snapshot / 'personal.db'}?mode=ro", uri=True)
types = dict(resource.execute("select name,type from nodes"))
resource_names=set(types)
types.update(dict(personal.execute("select name,type from types")))
targets = set(personal.execute("select name,room,source from room_members where room like '%挂墙%'"))
targets.update((name, room, "中继段表") for name, room in resource.execute("select name,room from rooms where room like '%挂墙%'"))

def eligible(name):
    labels = types.get(name, "").split(";")
    return name in resource_names and ("机房" in labels or "光交箱" in labels or re.search(r"(?:^|[/\-])GJ\d*$", name, re.I) is not None or re.search(r"(?:^|[/\-])(?:ODF|ODB)[A-Z0-9]*(?:$|[/\-])", name, re.I) is not None)

found = {}
occurrences = Counter()
hashes = {}
for path in sorted(sources.rglob("*.CSV")):
    origin = "中继段表" if path.name == "中继段-1.CSV" else "类型表：" + path.name
    if not any(source == origin for _, _, source in targets):
        continue
    hashes[path.name] = hashlib.sha256(path.read_bytes()).hexdigest()
    with path.open(encoding="gb18030", newline="") as stream:
        reader = csv.reader(stream)
        headers = next(reader)
        previous_line = reader.line_num
        index = {name: headers.index(name) for name in headers}
        pairs = [("始端设施", "始端机房"), ("终端设施", "终端机房")] if origin == "中继段表" else [("设施名称", "机房名称")]
        for row_number, row in enumerate(reader, 2):
            start_line = previous_line + 1
            previous_line = reader.line_num
            def value(key):
                col = index.get(key, -1)
                return row[col].strip() if 0 <= col < len(row) else ""
            for name_field, room_field in pairs:
                name, room = value(name_field), value(room_field)
                key = name, room, origin
                if key not in targets:
                    continue
                occurrences[key] += 1
                if key in found:
                    continue
                col = index[room_field]
                city, district, town = value("所属地市"), value("所属区县"), value("所属镇街")
                area = value("始端区域" if name_field == "始端设施" else "终端区域") if origin == "中继段表" else district
                found[key] = {
                    "机房名称原值": room, "设施名称": name, "当前机房归属列表": "已纳入（待复核）" if eligible(name) else "未纳入（资源节点未匹配）" if name not in resource_names else "未纳入（设施类别不适用）",
                    "来源表格": path.name, "原表行号（含表头）": row_number, "原字段": room_field,
                    "原字段列": chr(65 + col) + "列", "所属地市": city, "所属区县或区域": area,
                    "所属镇街": town, "站点名称/位置点": value("站点名称/位置点"), "上联机房原值": value("上联机房"),
                    "CSV物理行起": start_line, "CSV物理行止": reader.line_num,
                    "原始表SHA256": hashes[path.name], "原始表头": headers, "原始记录": row,
                }
assert targets == set(found), f"Unlocated records: {targets - set(found)}"
for key, record in found.items():
    record["同设施归属在该表出现次数"] = occurrences[key]
records = sorted(found.values(), key=lambda r: (not r["当前机房归属列表"].startswith("已纳入"), r["机房名称原值"], r["设施名称"]))
current = [r for r in records if r["当前机房归属列表"].startswith("已纳入")]
screenshot_rooms = ["内充公二期挂墙065", "内充公二期挂墙068", "内充公二期挂墙069", "内充公二期挂墙072", "利宏达挂墙010", "利宏达挂墙011", "利宏达挂墙012", "利宏达挂墙013", "利宏达挂墙014", "利宏达挂墙015"]
selected = [next(r for r in current if r["机房名称原值"] == room) for room in screenshot_rooms]
assert len(current) == 146 and len(records) == 265 and len(selected) == 10

columns = ["机房名称原值", "设施名称", "当前机房归属列表", "来源表格", "原表行号（含表头）", "原字段", "原字段列", "所属地市", "所属区县或区域", "所属镇街", "站点名称/位置点", "上联机房原值", "同设施归属在该表出现次数"]
path = output / "Room-Affiliation-Origin-Audit.xlsx"
book = xlsxwriter.Workbook(path, {"strings_to_formulas": False, "strings_to_urls": False})
header = book.add_format({"font_name": "微软雅黑", "bold": True, "bg_color": "#E3F4F6", "font_color": "#17304D", "text_wrap": True, "valign": "vcenter"})
body = book.add_format({"font_name": "微软雅黑", "font_color": "#17304D", "text_wrap": True, "valign": "vcenter"})
number = book.add_format({"font_name": "微软雅黑", "num_format": "0", "valign": "vcenter", "align": "center"})
title = book.add_format({"font_name": "微软雅黑", "bold": True, "font_size": 17, "font_color": "#008B95"})
for name, data in [("截图10条", selected), ("当前归属146条", current), ("全部挂墙候选265条", records)]:
    sheet = book.add_worksheet(name)
    sheet.hide_gridlines(2)
    sheet.set_zoom(85)
    sheet.merge_range(0, 0, 0, len(columns)-1, "机房归属来源复核 · 保留原表原值", title)
    sheet.set_row(0, 36)
    sheet.write_row(2, 0, columns, header)
    sheet.set_row(2, 38)
    for i, row in enumerate(data, 3):
        for col, key in enumerate(columns):
            value = row[key]
            sheet.write(i, col, value, number if isinstance(value, int) else body)
        sheet.set_row(i, 66)
    for col, width in enumerate([27, 63, 25, 26, 16, 18, 12, 12, 20, 20, 36, 36, 18]):
        sheet.set_column(col, col, width)
    sheet.freeze_panes(3, 2)
    sheet.autofilter(2, 0, len(data)+2, len(columns)-1)

sheet = book.add_worksheet("复核说明")
sheet.hide_gridlines(2)
sheet.set_column(0, 0, 25)
sheet.set_column(1, 1, 115)
notes = [
    ("数据范围", "依据此前交付的数据快照与原始上传表，只读取分析，没有修改数据库或原始表格。"),
    ("截图结论", "截图中10条都出自类型表的H列“机房名称”：6条在光分纤箱-1.CSV，4条在光终端盒-1.CSV；不是按设施名称猜测，也不是读取上联机房。"),
    ("当前规则缺口", "软件把非空且无多重归属冲突的值自动作为已确认归属。名称表示安装位置等明显异常时，也缺少再次核对检查。R1.4已保留原值与来源、标需核对并暂停归并，不据设备名称猜写真实机房。"),
    ("候选范围", "源表有154条含“挂墙”的候选（光分纤箱96、光终端盒58），其中146条匹配当前资源节点、进入归属列表；8条当前节点未匹配，仍在全部候选表备查。候选不等于已现场判错。中继段表另有111条此类归属原值，按当前设施类别条件没有进入机房归属列表；列在全部候选表备查。"),
    ("行号口径", "原表行号按CSV记录计数并包含第一行表头，便于Excel/WPS导入后定位。引号内跨行可能使物理文本行号不同，完整证据JSON另记物理行号。"),
    ("真实机房", "本次不推断正确机房名称。请用户核对原始台账或现场资料后修订源表；同一项目名称、设施名中的机房字样不自动视为归属依据。"),
]
for i, (label, note) in enumerate(notes):
    sheet.write(i, 0, label, header)
    sheet.write(i, 1, note, body)
    sheet.set_row(i, 75)
book.close()

evidence = {"snapshot": str(snapshot), "raw_file_sha256": hashes, "current_affiliations": len(current), "all_candidates": len(records), "records": records}
(output / "room-origin-evidence.json").write_text(json.dumps(evidence, ensure_ascii=False, indent=2), encoding="utf-8")
csv_path = output / "Room-Affiliation-Origin-Audit.csv"
with csv_path.open("w", encoding="utf-8-sig", newline="") as stream:
    writer = csv.DictWriter(stream, columns, extrasaction="ignore")
    writer.writeheader()
    writer.writerows(records)
with zipfile.ZipFile(output / "Room-Affiliation-Origin-Audit.zip", "w", zipfile.ZIP_DEFLATED) as archive:
    archive.write(path, path.name)
    archive.write(csv_path, csv_path.name)
print(json.dumps({"current": len(current), "all_candidates": len(records), "selected": [{k: r[k] for k in ["机房名称原值", "来源表格", "原表行号（含表头）"]} for r in selected]}, ensure_ascii=False))
