"""Draw source-based operation schematics. No company images or UI screenshots."""
from pathlib import Path
import math
import json
from PIL import Image, ImageDraw, ImageFont

ROOT = Path(__file__).resolve().parents[2]
OUT = ROOT / "docs" / "assets" / "illustrations"
S = 2
INK, BLUE, LIGHT, BORDER = "#243748", "#1670C4", "#F1F4F8", "#AFBFCE"
GREEN, YELLOW, RED, GRAY, ORANGE = "#319947", "#F3C42C", "#DA3535", "#8B949E", "#DF7F22"
FONT = "C:/Windows/Fonts/msjh.ttc"
BOLD = "C:/Windows/Fonts/msjhbd.ttc"
CATALOG = []


class Figure:
    def __init__(self, key, title, subtitle):
        self.key, self.title = key, title
        self.image = Image.new("RGB", (1200*S, 860*S), "white")
        self.draw = ImageDraw.Draw(self.image)
        self.text(35, 25, title, 36, bold=True, width=1130, color="#000000")
        self.text(35, 82, subtitle, 24, width=1130, color="#4A6074")
        self.text(35, 820, "操作示意圖｜依 b512791 程式整理；配置及數值為說明範例，非實際截圖。", 19, width=1130, color="#5D6E7E")

    def rect(self, x, y, w, h, fill="white", outline=BORDER, radius=5, line=2):
        self.draw.rounded_rectangle((int(x*S), int(y*S), int((x+w)*S), int((y+h)*S)),
                                    radius=radius*S, fill=fill, outline=outline, width=line*S)

    def line(self, points, color=BORDER, line=2, arrow=False):
        self.draw.line([(int(x*S), int(y*S)) for x, y in points], fill=color, width=line*S)
        if arrow:
            x, y = points[-1]
            px, py = points[-2]
            angle = math.atan2(y-py, x-px)
            head = [(x, y)]
            for turn in (-0.48, 0.48):
                head.append((x-15*math.cos(angle+turn), y-15*math.sin(angle+turn)))
            self.draw.polygon([(int(a*S), int(b*S)) for a, b in head], fill=color)

    def text(self, x, y, value, size=26, bold=False, width=1120, color=INK, align="left"):
        font = ImageFont.truetype(BOLD if bold else FONT, round(size*S))
        lines = []
        for raw in str(value).split("\n"):
            current = ""
            for char in raw:
                if current and self.draw.textlength(current+char, font=font) > width*S:
                    lines.append(current)
                    current = char
                else:
                    current += char
            lines.append(current)
        for i, line in enumerate(lines):
            offset = 0
            if align == "center":
                offset = (width*S-self.draw.textlength(line, font=font))/2
            self.draw.text((x*S+offset, (y+i*size*1.42)*S), line, font=font, fill=color)
        return y+len(lines)*size*1.42

    def card(self, x, y, w, h, title, fill=LIGHT):
        self.rect(x, y, w, h, fill)
        self.text(x+18, y+12, title, 28, True, w-36)
        self.line([(x+12, y+54), (x+w-12, y+54)])

    def button(self, x, y, w, title, active=False, color=None, h=48, size=25, disabled=False):
        fill = "#E6E9ED" if disabled else (color or (BLUE if active else "#FFFFFF"))
        self.rect(x, y, w, h, fill, BLUE if active else BORDER)
        self.text(x+5, y+(h-size*1.42*(title.count("\n")+1))/2-1, title, size, True, w-10,
                  "#8B949E" if disabled else ("#FFFFFF" if active or color in (GREEN, RED, GRAY, ORANGE) else INK), "center")

    def field(self, x, y, w, value, label=None, dropdown=False):
        if label:
            self.text(x, y-34, label, 24, width=w)
        self.rect(x, y, w, 45)
        self.text(x+12, y+5, value, 24, width=w-(47 if dropdown else 24))
        if dropdown:
            self.draw.polygon([((x+w-31)*S,(y+18)*S),((x+w-15)*S,(y+18)*S),
                               ((x+w-23)*S,(y+28)*S)],fill="#61788D")

    def check(self, x, y, title, checked=True, size=25):
        self.rect(x, y+3, 24, 24, BLUE if checked else "white", BLUE)
        if checked:
            self.line([(x+4, y+15), (x+10, y+21), (x+20, y+8)], "#FFFFFF", 3)
        self.text(x+38, y-1, title, size, width=1080-x)

    def step(self, x, y, n):
        self.draw.ellipse(((x-20)*S, (y-20)*S, (x+20)*S, (y+20)*S), fill=BLUE)
        self.text(x-18, y-20, n, 26, True, 36, "#FFFFFF", "center")

    def product(self, x, y, w=105, h=210, number=None, selected=False):
        self.rect(x+w*.20, y, w*.60, h, "#42464B", "#252A30", 10)
        self.rect(x, y+h*.10, w, h*.80, "#B9BEC3", "#343A40", 18, 4)
        if selected:
            self.rect(x-8, y-8, w+16, h+16, None, "#22B34A", 0, 3)
        if number is not None:
            self.text(x+5, y+h*.30, number, 28, True, w-10, "#182B3B", "center")

    def roi(self, x, y, w, h, label, color=BLUE, filled=False):
        if filled:
            overlay = Image.new("RGBA", self.image.size, (0, 0, 0, 0))
            d = ImageDraw.Draw(overlay)
            rgb = tuple(int(color[i:i+2], 16) for i in (1, 3, 5))
            d.rectangle((x*S,y*S,(x+w)*S,(y+h)*S), fill=rgb+(50,))
            self.image = Image.alpha_composite(self.image.convert("RGBA"), overlay).convert("RGB")
            self.draw = ImageDraw.Draw(self.image)
        self.rect(x,y,w,h,None,color,0,3)
        self.text(x+5,y+3,label,23,True,w-10,color)

    def table(self, x, y, widths, headers, rows, rowh=54, selected=None):
        for ri, values in enumerate([headers]+rows):
            left=x
            fill = "#E6EEF6" if ri == 0 else ("#E9F3FF" if ri-1 == selected else "white")
            for ci,(w,value) in enumerate(zip(widths,values)):
                self.rect(left,y+ri*rowh,w,rowh,fill,radius=0,line=1)
                self.text(left+10,y+ri*rowh+10,value,23,ri==0,w-20)
                left += w

    def save(self, chapter, note, sources):
        OUT.mkdir(parents=True,exist_ok=True)
        path=OUT/(self.key+".png")
        self.image.save(path)
        CATALOG.append({"key":self.key,"title":self.title,"chapter":chapter,"note":note,"sources":sources})


def overview_layout():
    c=Figure("01_workspace","主畫面要先看哪裡","功能選項選流程，圖片區看位置，設定區編輯，左下看時間與狀態。")
    c.rect(35,135,225,650,LIGHT)
    c.text(55,151,"功能選項",28,True,185)
    menu=["參數選擇","檢測參數  ›","讀取圖片","指定 ROI","找尋物件流程  ›","檢測參數設定","參數結果確認","檢測總覽","歷史資料","全域設定"]
    for i,t in enumerate(menu):
        if i==5:c.rect(44,194+i*41,207,39,"#D9EAFB",outline=None)
        c.text(55,199+i*41,t,23,width=190)
    c.field(49,646,197,"工程師",dropdown=True)
    c.rect(49,704,197,60,"white")
    c.text(58,714,"計時 Memo",23,width=178)
    c.card(280,135,445,650,"圖片分頁與影像")
    c.text(298,203,"量測位置｜平場｜缺陷…",23,width=405)
    for i,(x,y) in enumerate([(340,290),(530,290)],1):c.product(x,y,100,280,i)
    c.text(306,640,"滾輪：縮放",27,width=390)
    c.text(306,685,"左鍵拖曳：平移",27,width=390)
    c.card(745,135,420,650,"目前功能的設定／結果")
    c.text(770,225,"選中哪個功能，\n這裡顯示對應內容。",28,width=365)
    c.field(770,385,365,"物件群組 1",label="來源（範例）",dropdown=True)
    c.button(770,470,175,"套用",True)
    c.button(960,470,175,"取消")
    c.text(770,590,"參數不會因為\n縮放圖片而改變。",28,width=365)
    for x,y,n in [(232,169,1),(704,184,2),(1139,184,3),(232,733,4)]:c.step(x,y,n)
    c.save("2","①選功能 ②看圖片位置 ③編輯或查結果 ④看耗時與訊息。",["MainForm.Designer.cs","MainForm.Navigation.cs"])


def global_settings():
    c=Figure("02_global","全域內部參數怎麼設定","設備精度、站點、歷史及影像限制隨軟體保存，各群組各自套用。")
    c.card(35,140,550,255,"攝影機精度")
    c.field(60,242,210,"0.010",label="X  mm/pixel")
    c.field(315,242,210,"0.020",label="Y  mm/pixel")
    c.check(60,307,"啟用 mm 換算")
    c.button(320,333,235,"套用內部參數",True)
    c.step(550,210,1)
    c.card(615,140,550,255,"歷史紀錄")
    c.check(640,217,"執行確認完成後新增歷史",True,24)
    c.check(640,269,"保留確認圖片",True)
    c.button(967,329,165,"套用",True)
    c.card(35,417,550,150,"取像程式名稱")
    c.field(60,492,300,"左側攝影機")
    c.button(375,490,180,"套用名稱",True)
    c.card(615,417,550,185,"合格影像規格")
    c.check(640,487,"是否限制影像條件",True)
    c.field(640,535,280,"50000 pixel")
    c.button(965,535,165,"套用",True)
    c.step(1134,485,2)
    c.card(35,590,550,195,"連線設定")
    c.field(60,668,260,"127.0.0.1")
    c.field(340,668,205,"5000")
    c.check(60,729,"允許總控端連線",False)
    c.card(615,625,550,160,"站點識別")
    c.field(640,700,250,"1",dropdown=True)
    c.button(918,698,214,"套用軟體 ID",True)
    c.save("3","①先用標準件設定精度；示例數值不是推薦值。②目前相機入口檢查 Y 完全相等。",["MainForm.GlobalSettings.cs","CameraDeviceSettingsIniService.cs"])


def recipe():
    c=Figure("03_recipe","選中 Recipe 後還要套用","選取清單只查看資料；按「套用此參數」才切換目前流程。")
    c.card(65,140,1070,650,"檢測參數選擇")
    c.table(90,219,[470,260,260],["參數名稱","加入時間","檔案狀態"],
            [["金屬件 A","2026/10/30","可用"],["金屬件 B","2026/10/29","可用"],["金屬件 C","2026/10/28","可用"]],63,selected=0)
    c.step(1087,314,1)
    c.text(90,492,"參數說明",27,True,580)
    c.rect(90,540,580,160)
    c.text(107,558,"左側攝影機使用參數。\n此處是示例說明，可編輯名稱與說明。",25,width=545)
    c.step(641,518,2)
    c.button(700,490,105,"↑ 上移")
    c.button(818,490,105,"↓ 下移")
    c.button(936,490,173,"保存清單順序",size=22)
    c.button(700,555,410,"編輯名稱與說明")
    c.button(700,632,248,"套用此參數",True)
    c.button(963,632,147,"取消")
    c.step(932,710,3)
    c.text(91,732,"修改工作參數後，要更新目前 recipe 或另存新 Recipe。",24,width=1000)
    c.save("4.3","①選列 ②看或編輯說明 ③套用並等待重新處理；清單排序須另外保存。",["DetectionRecipeDialogs.cs","MainForm.DetectionParameter.Recipes.cs"])


def objects():
    c=Figure("04_objects","先找到物件，再建立逐片檢測","物件序號與格位來自參數定義，後續矩陣沿用同一排列。")
    steps=["指定 ROI","影像前處理","影像處理／關聯","整合成區塊","物件定義"]
    for i,label in enumerate(steps):
        y=155+i*111
        c.button(45,y,315,label,h=66)
        if i<4:c.line([(202,y+68),(202,y+108)],BLUE,3,True)
    c.line([(365,411),(470,411)],BLUE,3,True)
    c.card(495,140,660,615,"找到的物件與格位（2 × 2 範例）")
    for n,(x,y) in enumerate([(610,235),(915,235),(610,490),(915,490)],1):
        c.product(x,y,110,200,n,True)
        c.text(x+140,y+70,["A-01","A-02","B-01","B-02"][n-1],25,width=140)
    c.text(50,749,"搜尋 ROI 與每片的缺陷 ROI 是不同層次。",28,True,1100)
    c.save("5.3","先確認物件數、編號與排列正確，尺寸與缺陷才有一致的物件依據。",["MainForm.ObjectDefinition.cs","MainForm.FunctionTree.cs"])


def measurement():
    c=Figure("05_measurement","尺寸量測先選 MASK 再畫線","平行量測會產生多筆樣本；最短樣本黃色，最長樣本紅色。")
    c.card(35,140,530,650,"量測位置（合成物件）")
    c.product(183,236,220,420)
    for i in range(11):
        y=285+i*28
        color=YELLOW if i==2 else (RED if i==7 else "#17AACC")
        start=205 if i==2 else 193
        end=385 if i==2 else (403 if i==7 else 392)
        c.line([(start,y),(end,y)],color,5 if i in (2,7) else 2)
    c.text(62,697,"黃：最短　紅：最長",27,True,465)
    c.card(595,140,570,650,"尺寸量測設定")
    c.field(620,238,520,"影像處理－產品 MASK",label="量測來源 MASK",dropdown=True)
    c.button(620,302,520,"套用來源 MASK",True)
    c.step(1131,224,1)
    c.field(620,418,245,"Parallel",label="量測模式",dropdown=True)
    c.field(890,418,245,"100",label="平行線數")
    c.button(620,495,245,"開始畫線")
    c.button(890,495,245,"套用量測設定",True,size=23)
    c.step(1131,570,2)
    c.check(620,602,"量測線依 MASK 範圍裁切")
    c.button(620,683,520,"保存量測資料",True)
    c.text(620,747,"未命中的平行樣本以 0 納入統計。",23,width=520)
    c.save("6.1","①指定並套用來源 MASK ②建立線位、套用量測設定，再保存量測紀錄。",["MainForm.DetectionParameter.MeasurementDrawing.cs","MainForm.DetectionParameter.MeasurementCalculation.cs"])


def grading():
    c=Figure("06_grading","一片一片判斷 A 規 B 規 NG","A 規資料未知時先待確認；A 明確不符或未設定 A，才評估 B。")
    c.button(415,142,370,"每片啟用的尺寸條件",h=65)
    c.line([(600,208),(600,250)],BLUE,3,True)
    c.button(415,255,370,"評估 A 規",h=62)
    c.line([(415,285),(190,285),(190,377)],BLUE,3,True)
    c.rect(60,320,280,39,"white",outline=None)
    c.text(60,322,"A 全數符合",26,width=280)
    c.button(70,385,260,"A 規",color=GREEN,h=76,size=32)
    c.line([(785,285),(1005,285),(1005,377)],GRAY,3,True)
    c.rect(841,320,325,38,"white",outline=None)
    c.text(841,322,"A 未知且無明確失敗",24,width=325)
    c.button(870,385,280,"待確認",color=GRAY,h=76,size=32)
    c.line([(600,317),(600,403)],BLUE,3,True)
    c.rect(393,347,420,38,"white",outline=None)
    c.text(393,349,"A 明確不符／未設定 A",24,width=420,align="center")
    c.button(415,410,370,"獨立評估 B 規",h=65)
    for x,label,color in [(70,"B 規",YELLOW),(460,"NG",RED),(850,"待確認",GRAY)]:
        c.line([(600,475),(600,526),(x+140,526),(x+140,590)],BLUE,3,True)
        c.button(x,595,280,label,color=color,h=76,size=32)
    for x in (78,470,862):c.rect(x,546,270,38,"white",outline=None)
    c.text(78,548,"B 全數符合",25,width=270,align="center")
    c.text(470,548,"B 有不符／A 不符且無 B",22,width=270,align="center")
    c.text(862,548,"B 未知／沒有可用條件",23,width=270,align="center")
    c.text(60,725,"綜合：尺寸或缺陷任一 NG → 該片 NG；無 NG 但有未知 → 待確認。",25,True,1080)
    c.save("6.3","示意圖描述每片的條件流程；A／B 可使用不同計算式與規格。",["MainForm.DetectionParameter.ResultReview.GoodJudgement.cs","MainForm.InspectionOverview.cs"])


def flatfield():
    c=Figure("07_flatfield","平場校正與原圖各用在哪裡","合成灰階範例：校正背景亮度後，才進行各條件的缺陷分析。")
    for x,title,corrected in [(40,"原始影像",False),(660,"平場校正後影像",True)]:
        c.card(x,155,500,520,title)
        for j in range(430):
            gray=150 if corrected else int(95+100*j/429)
            c.line([(x+35+j,255),(x+35+j,520)],(gray,gray,gray),1)
        c.text(x+35,553,"背景亮度曲線（示意）",25,width=430)
        c.line([(x+40,630),(x+460,630)],BORDER,2)
        c.line([(x+40,625),(x+460,625 if corrected else 570)],BLUE,4)
    c.line([(550,385),(650,385)],BLUE,4,True)
    c.text(62,718,"檢測總覽：顯示原始圖",28,True,500)
    c.text(676,718,"缺陷各條件：以平場圖為基礎",26,True,480)
    c.text(62,772,"校正曲線及來源保存於 Recipe；這張示意圖沒有代表實際檢出效果。",23,width=1100)
    c.save("7","左右是同一張合成示例的概念，並非公司圖片或演算法實測輸出。",["MainForm.FlatFieldCalibration.Correction.cs","MainForm.InspectionOverview.cs"])


def roi_edit():
    c=Figure("08_roi_edit","在代表物件上建立可重用 ROI","按框選 ROI，以 Ctrl 加左鍵畫框或拖邊，套用後映射至其他物件。")
    c.card(35,145,510,635,"缺陷顯示")
    c.product(185,235,210,435,1,True)
    c.roi(210,320,160,240,"表面區",BLUE,True)
    c.line([(370,430),(430,430)],ORANGE,5,True)
    c.text(315,580,"Ctrl ＋ 左鍵拖邊",22,width=205)
    c.step(408,302,2)
    c.card(575,145,590,635,"檢測範圍")
    c.text(602,222,"物件序號",25,True,520)
    for i in range(4):c.button(607+i*127,270,110,str(i+1),i==0)
    c.field(605,400,520,"表面區",label="ROI 名稱（需唯一）")
    c.button(605,477,245,"框選 ROI",True)
    c.button(880,477,245,"套用",True)
    c.step(1130,385,1)
    c.step(1127,555,3)
    c.text(605,605,"獨立 ID：ROI-001（示例）\n改名保留 ID，群組依 ID 關聯。",26,width=515)
    c.text(605,711,"操作時保留圖片縮放與平移視角。",24,width=515)
    c.save("8.1","①命名並框選 ②微調邊線 ③套用；同一個定義會使用在所有物件上。",["MainForm.DetectionParameter.DefectRegion.cs"])


def roi_group():
    c=Figure("09_roi_group","多個獨立 ROI 組成檢測範圍組","群組勾選的是 ROI 定義；各條件用下拉選單指定要使用的群組。")
    c.card(35,140,540,520,"檢測範圍組：表面檢測")
    for i,(name,color) in enumerate([("ROI A  表面上部",BLUE),("ROI B  表面下部",ORANGE),("ROI C  邊緣", "#9758BA")]):
        c.check(65,225+i*83,name,True)
        c.text(95,260+i*83,"固定 ID；名稱可修改",21,width=420,color="#5B7083")
    c.button(65,525,95,"↑")
    c.button(178,525,95,"↓")
    c.button(300,525,110,"套用",True)
    c.button(430,525,110,"取消")
    c.step(537,222,1)
    c.card(620,140,545,520,"選中的物件上顯示三個 ROI")
    c.product(803,225,180,360,3,True)
    c.roi(820,270,145,145,"A",BLUE,True)
    c.roi(820,370,145,155,"B",ORANGE,True)
    c.roi(790,300,55,240,"C","#9758BA",True)
    c.text(640,593,"A／B 重疊區只在同一條件分析一次。",23,width=505)
    c.field(65,727,510,"表面檢測",label="條件一：使用 ROI 組",dropdown=True)
    c.line([(576,742),(680,742)],BLUE,3,True)
    c.text(700,700,"條件依群組 ID 取得 ROI。\n未指定有效群組時不執行。",25,True,430)
    c.save("8.2","①只點勾選框才改變成員；群組內取聯集，各檢測條件仍分別運算。",["MainForm.DetectionParameter.DefectRegion.cs"])


def defect_core():
    c=Figure("10_defect_core","條件設定完成後按套用看結果","條件一至六可各自設定，單頁套用只更新該條件。")
    c.card(35,140,570,650,"條件一（範例）")
    c.check(60,211,"是否使用")
    c.field(263,207,310,"表面檢測",dropdown=True)
    c.text(60,274,"用途標籤：點狀缺陷",24,width=510)
    c.check(60,329,"啟用對比調整")
    c.field(400,325,172,"1.50")
    c.field(263,395,310,"Gaussian 模糊",label=None,dropdown=True)
    c.text(60,403,"前處理",25,width=190)
    c.text(60,471,"淡色缺陷增強",24,width=190)
    c.field(263,463,310,"不處理",dropdown=True)
    c.check(60,534,"暗部 <= 110")
    c.check(320,534,"亮部 >= 200",True)
    c.check(60,594,"侵蝕 E",False)
    c.check(320,594,"膨脹 D",False)
    c.text(60,652,"面積與 X/Y 篩選依實際規格設定。",24,width=510)
    c.button(282,714,140,"套用",True)
    c.button(440,714,132,"取消")
    c.step(570,698,1)
    c.card(635,140,530,650,"處理圖與符合條件的候選")
    c.product(795,248,200,390)
    c.roi(825,300,135,250,"有效 ROI",BLUE)
    c.draw.ellipse((850*S,385*S,900*S,435*S),fill="#E3A3C3")
    c.rect(843,378,64,64,None,RED,0,4)
    c.draw.ellipse((920*S,480*S,945*S,505*S),fill="#FFE575")
    c.rect(913,473,40,40,None,ORANGE,0,4)
    c.text(657,684,"粉紅 MASK／紅框：暗缺陷\n黃色 MASK／橘框：亮缺陷",24,width=490)
    c.save("8.3","①套用後看對應圖片頁；顯示 MASK 和框的開關不等於啟用判定。",["MainForm.DetectionParameter.DefectCores.cs","MainForm.DetectionParameter.DefectProcessing.cs"])


def frequency_ui():
    c=Figure("11_frequency_ui","頻域異常頁先選範圍再調掃描","目前用加窗能量比較，不是 FFT／DFT；門檻越小越容易檢出。")
    c.card(40,145,1120,645,"頻域異常一／二（兩組各自設定）")
    c.check(65,215,"是否使用")
    c.field(695,210,420,"表面檢測",dropdown=True)
    c.text(440,218,"使用 ROI 組",25,width=220)
    c.check(65,286,"啟用對比調整")
    c.field(390,281,200,"2.00")
    c.text(637,292,"前處理",25,width=180)
    c.field(812,281,303,"Gaussian 模糊",dropdown=True)
    c.field(65,409,300,"500",label="掃描高度 H（pixel）")
    c.field(440,409,300,"0.5X",label="垂直縮放倍率",dropdown=True)
    c.field(815,409,300,"3.0",label="異常敏感度／門檻")
    c.check(65,492,"啟用鄰近良品比較",False)
    c.check(65,566,"顯示能量熱圖")
    c.check(620,566,"顯示異常框")
    c.check(65,617,"顯示鄰格熱圖")
    c.check(620,617,"顯示鄰格異常框")
    c.check(65,668,"顯示健康參考格",False)
    c.button(585,722,160,"開始分析",True)
    c.button(768,722,160,"套用")
    c.button(950,722,165,"取消")
    c.step(1114,386,1)
    c.step(552,748,2)
    c.save("9.1","①調整掃描與門檻 ②開始分析；套用保存設定，熱圖與框可分別顯示。",["MainForm.DetectionParameter.DefectFrequency.cs"])


def frequency_windows():
    c=Figure("12_frequency_windows","原圖取樣範圍與計算縮放要分開看","邊界向內補齊完整窗；垂直壓縮不會改變原圖 ROI 或結果框的位置。")
    c.card(35,150,550,595,"原圖上的有效 ROI")
    c.roi(75,260,470,345,"有效 ROI",BLUE)
    for x,y,color in [(100,320,BLUE),(220,320,BLUE),(340,320,ORANGE),(100,435,BLUE),(340,435,ORANGE)]:
        c.rect(x,y,165,145,None,color,0,3)
    c.text(73,639,"尾端落點向前／向上補足，\n完整窗不讀 ROI 外的黑區。",25,width=470)
    c.card(615,150,550,595,"同一取樣格的計算影像")
    c.rect(670,255,180,180,"#C3C7CA")
    c.line([(687,307),(829,377)],"#737A80",4)
    c.text(652,456,"500 × 500",25,True,220,align="center")
    c.line([(865,345),(925,345)],BLUE,3,True)
    c.rect(949,300,180,90,"#C3C7CA")
    c.line([(964,326),(1110,360)],"#737A80",4)
    c.text(925,456,"500 × 250",25,True,220,align="center")
    c.text(655,529,"0.5X：寬度不變，高度減半。",26,width=470)
    c.text(655,593,"顯示框仍指向原圖\n500 × 500 的取樣區。",25,width=470)
    c.text(55,772,"範圍太窄或有孔洞時可能沒有完整有效格，須降低 H 或調整範圍。",25,True,1090)
    c.save("9.2","左圖的色框是多個取樣落點示意，不表示以不完整視窗或補黑影像計算。",["MainForm.DetectionParameter.DefectFrequency.cs"])


def integration():
    c=Figure("13_integration","雙擊整合結果可聚焦缺陷","合併依每片與距離規則進行；聚焦時保留周邊影像方便判讀。")
    c.card(35,145,560,640,"結果整合")
    c.check(60,220,"暗合併")
    c.check(330,220,"亮合併")
    c.check(60,273,"亮暗合併",False)
    c.check(60,326,"納入頻域異常一")
    c.check(60,379,"納入頻域異常二",False)
    c.field(60,486,210,"12 pixel",label="合併距離（範例）")
    c.button(300,484,130,"套用",True)
    c.button(446,484,120,"取消")
    c.table(60,575,[110,190,235],["物件","來源","結果"],
            [["2","條件一＋頻域一","整合缺陷 1"]],64,0)
    c.step(553,704,1)
    c.line([(598,651),(660,651)],BLUE,3,True)
    c.card(680,145,485,640,"聚焦後仍有周邊空間")
    c.rect(720,255,405,400,"#C1C4C7")
    c.draw.ellipse((896*S,429*S,921*S,454*S),fill="#464B50")
    c.rect(884,416,48,50,None,RED,0,4)
    c.text(717,691,"整合框是成員範圍的聯集。",23,width=405)
    c.save("9.4","①雙擊一列，聚焦該缺陷；選中的小黑點周圍仍保留背景。",["MainForm.DetectionParameter.DefectIntegration.cs"])


def review():
    c=Figure("14_review","在參數結果確認追查每一片","載圖後自動執行；修改設定後可按執行確認重新判定。")
    c.card(35,145,520,640,"量測位置｜平場｜缺陷…｜整合")
    for n,(x,y) in enumerate([(130,255),(335,255),(130,490),(335,490)],1):
        c.product(x,y,95,200,n,n==3)
    c.line([(132,570),(223,570)],YELLOW,5)
    c.card(580,145,585,640,"參數結果確認")
    c.field(605,235,520,"左側檢測參數",label="檢測參數",dropdown=True)
    c.button(605,302,240,"載入圖片",True)
    c.button(875,302,250,"執行確認")
    c.text(605,382,"量測資料｜尺寸良品判定｜缺陷判定",22,True,520)
    for i,(label,color) in enumerate([("1 A",GREEN),("2 B",YELLOW),("3 NG",RED),("4 待確認",GRAY)]):
        c.button(605+(i%2)*270,435+(i//2)*62,250,label,color=color,h=50)
    c.table(605,590,[160,190,170],["條件","計算值","結果"],
            [["寬度","12.27 mm","不符合"]],57,0)
    c.check(605,724,"量測線依 MASK 範圍裁切",True,23)
    c.step(1131,300,1)
    c.step(1131,550,2)
    c.save("10.2","①選參數及圖片 ②選片看明細；再點同一序號取消選取，回到總覽。",["MainForm.DetectionParameter.ResultReview.cs","MainForm.DetectionParameter.ResultReview.GoodJudgement.cs"])


def inspection_overview():
    c=Figure("15_overview","檢測總覽把每片三種結果排在一起","左側原始圖不疊缺陷位置；右側矩陣依參數中的列與行配置。")
    c.card(35,145,440,640,"原始影像")
    for n,(x,y) in enumerate([(100,240),(285,240),(100,460),(285,460)],1):c.product(x,y,90,175,n,n==2)
    c.button(60,659,390,"送至參數結果確認",True,size=24)
    c.button(60,727,126,"斷開連線",size=20)
    c.button(193,727,126,"連續取像",size=21)
    c.button(326,727,125,"檢測",size=22)
    c.card(500,145,665,640,"每片結果（2 × 2 範例）")
    c.text(527,218,"● 相機已連線     ● TCP/IP 已連線",25,width=610,color=GREEN)
    grades=[("A","A","A"),("B","A","B"),("NG","A","NG"),("待確認","待確認","待確認")]
    colors={"A":GREEN,"B":YELLOW,"NG":RED,"待確認":GRAY}
    for n,group in enumerate(grades,1):
        x=527+(n-1)%2*313;y=290+(n-1)//2*180
        c.card(x,y,290,157,"物件 "+str(n),fill="white")
        if n==2:c.rect(x-3,y-3,296,163,None,BLUE,3,5)
        for j,(t,g) in enumerate(zip(["尺寸","缺陷","綜合"],group)):
            c.text(x+8+j*94,y+64,t,20,width=88,align="center")
            c.button(x+7+j*94,y+95,88,g,color=colors[g],h=44,size=18 if g=="待確認" else 23)
    c.text(530,683,"單擊聚焦；雙擊取消並回全圖。",26,True,600)
    c.text(530,737,"活動模式再按同一按鈕停止。",25,width=600)
    c.save("11.3","每片各自判定，物件 3 的 NG 不會把物件 1、2 一起改為 NG。",["MainForm.InspectionOverview.cs"])


def camera():
    c=Figure("16_camera","取得影像後事件控制預覽與處理交接","原本相機顯示區由同一組設定控制；米輪與波形仍使用獨立視窗。")
    c.card(35,145,1130,420,"全域設定 > 與線掃描取像結合 > Camera Settings")
    c.text(60,219,"Basic Connection｜Image｜Trigger｜Saving｜取得影像後事件",25,True,1080)
    c.check(70,309,"顯示攝影機影像",False,28)
    c.text(110,358,"取消勾選會停止原相機預覽區的顯示更新。",26,width=1000)
    c.check(70,425,"將取得的影像送入影像處理",True,28)
    c.button(685,493,145,"Apply",True)
    c.button(848,493,145,"OK")
    c.button(1011,493,120,"Cancel",size=23)
    stages=[("連結攝影機",35),("Software Trigger\n確認機台原點",315),("是 → Encoder Clear",595),("Connect 與 Preview",875)]
    for text,x in stages:c.button(x,645,260,text,h=83,size=22)
    for i in range(3):c.line([(295+i*280,687),(310+i*280,687)],BLUE,3,True)
    c.text(55,760,"選「否」不繼續；機台是否在原點需由現場確認。",27,True,1090)
    c.save("12","兩個勾選功能互相獨立；連續取像與檢測模式的差異請見檢測總覽。",["CameraSettingsForm.cs","MainForm.InspectionOverview.cs"])


def history():
    c=Figure("17_history","先選日期查詢，再點紀錄看明細","上表每列是一張大圖；右鍵可使用當時 Recipe 快照重新檢測。")
    c.card(35,145,1130,640,"歷史資料")
    c.field(60,247,265,"2026/10/30",label="檢測日期")
    c.rect(287,258,24,22,"#FFFFFF",BLUE,radius=1,line=1)
    c.line([(287,264),(311,264)],BLUE,2)
    c.line([(292,254),(292,263)],BLUE,2)
    c.line([(306,254),(306,263)],BLUE,2)
    c.field(350,247,320,"全部 Recipe",label="Recipe",dropdown=True)
    c.field(695,247,270,"全部狀態",label="判定狀態",dropdown=True)
    c.button(987,245,145,"查詢",True)
    c.step(1135,216,1)
    c.table(60,336,[170,190,245,130,150,185],
            ["時間","影像序號","Recipe","片數","尺寸","綜合"],
            [["10:42:18","#125","金屬件 A","4","A 1 B 1","NG 1"],["10:39:52","#124","金屬件 A","4","A 4","A 4"]],60,0)
    c.step(1116,473,2)
    c.text(60,548,"尺寸判定｜缺陷判定",27,True,550)
    c.table(60,596,[130,200,260,220,260],["物件","項目","實測","允許範圍","結果"],
            [["2","寬度","12.27 mm","11.8～12.2","不符合"]],61)
    c.rect(650,448,465,105,"#FFFFFF",BLUE)
    c.text(670,464,"右鍵：使用當時參數重新檢測",24,True,425,color=BLUE)
    c.text(670,507,"需有大圖與快照，並匹配 ParameterId。",22,width=425)
    c.text(62,747,"同一筆已載入的明細，再點一次不必重讀。缺大圖時會提示不能重跑。",24,width=1070)
    c.save("13.1","①選日期與篩選後查詢 ②點一列更新下方明細；右鍵開始歷史重跑。",["MainForm.InspectionHistory.cs","InspectionHistoryStore.cs"])


def controller():
    c=Figure("18_controller","總控收到各站結果與運行狀態","TCP/IP 不傳大圖；總控的最新畫面不能替代各站歷史資料。")
    for i,(station,connected) in enumerate([("站點 1",True),("站點 2",True),("站點 3",False)]):
        x=35+i*385
        c.card(x,155,360,500,station)
        c.text(x+18,234,"TCP/IP  "+("已連線" if connected else "未連線"),25,True,324,color=GREEN if connected else GRAY)
        c.text(x+18,286,"相機  "+("已連線" if connected else "未連線"),25,True,324,color=GREEN if connected else RED)
        c.text(x+18,342,"Recipe：產品 A" if connected else "等待站點狀態",24,width=324)
        for n,label in enumerate(["A","A","NG","A"] if connected else ["待確認"]*4):
            c.button(x+18+(n%2)*166,414+(n//2)*80,155,label,color=(GREEN if label=="A" else RED if label=="NG" else GRAY),h=62,size=28)
        c.text(x+18,592,"模式：檢測中" if connected else "要求運行但相機離線",23,width=324)
    c.button(235,707,340,"連續取像",disabled=True)
    c.button(625,707,340,"停止檢測",True)
    c.text(53,773,"未要求運行時，離線相機保持灰色；要求運行但未連線以紅色提醒。",24,width=1100)
    c.save("14","圖為要求檢測時的狀態示例；連續取像與檢測互斥，另一模式會停用。",["MainForm.ConnectionController.cs"])


def architectures():
    for target in [False,True]:
        c=Figure("20_target_architecture" if target else "19_current_architecture",
                 "建議目標架構" if target else "目前架構的主要依賴",
                 "建議設計尚未實作；以共同請求與結果逐步分離頁面。" if target else "主視窗仍協調主要流程，相機與歷史重跑會借用結果確認。")
        if target:
            labels=[("操作頁面／相機／歷史入口",400,155,440,80),
                    ("InspectionRunner／Station Runtime",325,300,590,90),
                    ("找物件／尺寸／平場／缺陷／頻域",275,455,690,90)]
            for t,x,y,w,h in labels:c.button(x,y,w,t,h=h,size=26)
            c.line([(620,235),(620,295)],BLUE,3,True)
            c.line([(620,390),(620,450)],BLUE,3,True)
            c.button(425,595,390,"共同 InspectionResult",True,h=75)
            c.line([(620,545),(620,590)],BLUE,3,True)
            for x,t in [(35,"畫面顯示"),(425,"歷史與存檔"),(815,"TCP 發布")]:
                c.button(x,730,350,t,h=55)
                c.line([(620,670),(620,700),(x+175,700),(x+175,727)],BLUE,3,True)
        else:
            c.button(45,160,320,"相機模組／host",h=70)
            c.button(835,160,320,"手動圖片／歷史",h=70)
            c.button(345,300,510,"MainForm 結果確認與共享狀態",True,h=90,size=26)
            c.line([(205,230),(205,345),(340,345)],BLUE,3,True)
            c.line([(995,230),(995,345),(860,345)],BLUE,3,True)
            c.button(235,460,730,"找物件 → 平場／尺寸／缺陷／頻域",h=80,size=27)
            c.line([(600,390),(600,455)],BLUE,3,True)
            c.button(345,595,510,"MainForm 結果快照",h=70)
            c.line([(600,540),(600,590)],BLUE,3,True)
            for x,t in [(35,"結果頁與總覽"),(425,"歷史／部分讀表格"),(815,"TCP 狀態")]:
                c.button(x,735,350,t,h=55,size=24)
                c.line([(600,665),(600,700),(x+175,700),(x+175,730)],BLUE,3,True)
        c.save("目標架構" if target else "目前架構",
               "圖中目標類別尚未建立。" if target else "圖示呈現現有依賴，並非重構完成狀態。",
               ["TARGET_ARCHITECTURE.md" if target else "CURRENT_ARCHITECTURE.md"])


def build_all():
    CATALOG.clear()
    for fn in [overview_layout,global_settings,recipe,objects,measurement,grading,flatfield,
               roi_edit,roi_group,defect_core,frequency_ui,frequency_windows,integration,review,
               inspection_overview,camera,history,controller,architectures]:
        fn()
    (OUT/"catalog.json").write_text(json.dumps(CATALOG,ensure_ascii=False,indent=2),encoding="utf-8")
    print(f"Built {len(CATALOG)} illustrated diagrams")


if __name__=="__main__":
    build_all()
