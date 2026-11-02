"""Insert maintained illustration blocks and rebuild the browsable gallery."""
from pathlib import Path
import json
import re

ROOT=Path(__file__).resolve().parents[2]
DOCS=ROOT/"docs"
items=json.loads((DOCS/"assets"/"illustrations"/"catalog.json").read_text(encoding="utf-8"))
by_key={x["key"]:x for x in items}

MANUAL={
    "## 2 功能選項與基本操作":"01_workspace",
    "## 3 全域設定":"02_global",
    "### 4.3 選擇與套用":"03_recipe",
    "### 5.3 物件定義":"04_objects",
    "### 6.1 建立量測紀錄":"05_measurement",
    "### 6.3 每片的 A B NG":"06_grading",
    "## 7 平場校正":"07_flatfield",
    "### 8.1 建立共用 ROI 定義":"08_roi_edit",
    "### 8.2 建立檢測範圍組":"09_roi_group",
    "### 8.3 平場核心與條件一至六":"10_defect_core",
    "### 9.1 頻域一與二":"11_frequency_ui",
    "### 9.2 掃描設定":"12_frequency_windows",
    "### 9.4 結果整合":"13_integration",
    "### 10.2 查看逐片結果":"14_review",
    "### 11.3 判定矩陣":"15_overview",
    "## 12 相機設定與輔助視窗":"16_camera",
    "### 13.1 查詢與明細":"17_history",
    "## 14 連線總控":"18_controller",
}
SPEC={
    "### 4.2 缺陷範圍":"09_roi_group",
    "### 5.3 尺寸逐片規則":"06_grading",
    "### 7.2 計算順序":"10_defect_core",
    "### 8.2 掃描與邊界":"12_frequency_windows",
    "### 11.4 歷史重跑":"17_history",
    "### 12.1 傳輸與識別":"18_controller",
}


def insert(path,mapping):
    text=path.read_text(encoding="utf-8")
    text=re.sub(r"\n<!-- illustration:[^>]+ -->\n.*?\n<!-- /illustration -->\n", "\n",text,flags=re.S)
    for number,(anchor,key) in enumerate(mapping.items(),1):
        item=by_key[key]
        assert text.count(anchor+"\n")==1,(path,anchor)
        block=(f"\n<!-- illustration:{key} -->\n"
               f"![圖 {number} {item['title']}](assets/illustrations/{key}.png)\n\n"
               f"{item['note']}\n"
               "<!-- /illustration -->\n")
        text=text.replace(anchor+"\n",anchor+"\n"+block,1)
    text=text.replace("文件版本 1.0","文件版本 1.1")
    path.write_text(text,encoding="utf-8")


insert(DOCS/"USER_MANUAL.md",MANUAL)
insert(DOCS/"SYSTEM_SPECIFICATION.md",SPEC)
insert(DOCS/"CURRENT_ARCHITECTURE.md",{"## 2 現有模組與依賴":"19_current_architecture"})
insert(DOCS/"TARGET_ARCHITECTURE.md",{"## 2 建議責任分層":"20_target_architecture"})
manual=DOCS/"USER_MANUAL.md"
text=manual.read_text(encoding="utf-8")
lead="本版另附 18 張操作圖解，涵蓋畫面配置與操作順序。圖中位置、產品及數值為說明範例；按鈕名稱依目前程式，圖面並非實際軟體截圖。圖內數字對應旁邊的步驟說明。"
if lead not in text:
    text=text.replace("## 1 開始使用",lead+"\n\n## 1 開始使用",1)
manual.write_text(text,encoding="utf-8")

gallery=[
    "# 整合式影像處理軟體圖解操作指南","",
    "文件版本 1.1　核對日期 2026-10-30　程式基準 b512791","",
    "這裡集中列出 18 張操作圖解與 2 張架構圖。請配合完整操作手冊的相同章節使用。圖中配置、產品與數值為說明範例，並非實際截圖；目標架構尚未實作。","",
    "[回到文件總覽](README.md)　[完整操作手冊](USER_MANUAL.md)","",
]
source_files=list((ROOT/"IntegratedImageProcessingApp").rglob("*.cs"))+list((ROOT/"CameraCaptureModule").rglob("*.cs"))
for number,item in enumerate(items,1):
    gallery.extend([f"## {number} {item['title']}","",
                    f"![圖 {number} {item['title']}](assets/illustrations/{item['key']}.png)","",
                    item["note"],""])
    sources=[]
    for name in item["sources"]:
        found=next((p for p in source_files if p.name==name),None)
        if found:
            sources.append(f"[{name}](../{found.relative_to(ROOT).as_posix()})")
        elif (DOCS/name).exists():
            sources.append(f"[{name}]({name})")
        else:
            raise ValueError(name)
    gallery.extend(["對照章節："+item["chapter"]+"。程式依據："+"、".join(sources)+"。",""])
(DOCS/"ILLUSTRATED_GUIDE.md").write_text("\n".join(gallery),encoding="utf-8")
print("Inserted manual 18 / specification 6 / architecture 2 figures; gallery 20")
