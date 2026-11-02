"""Build the DOCX manuals from maintained Markdown with python-docx and Pillow."""
from pathlib import Path
import re
import sys
from docx import Document
from docx.shared import Cm, Pt, RGBColor
from docx.oxml import OxmlElement
from docx.oxml.ns import qn
from docx.enum.text import WD_ALIGN_PARAGRAPH
from docx.enum.table import WD_TABLE_ALIGNMENT, WD_CELL_VERTICAL_ALIGNMENT
from PIL import Image, ImageDraw, ImageFont
sys.dont_write_bytecode = True

ROOT = Path(__file__).resolve().parents[2]
DOCS, FONT = ROOT / "docs", "Microsoft JhengHei"
EXPORTS, ASSETS = DOCS / "exports", DOCS / "assets"


def font_style(style, size, bold=False):
    style.font.name, style.font.size = FONT, Pt(size)
    style.font.bold, style.font.color.rgb = bold, RGBColor(0, 0, 0)
    props = style.element.get_or_add_rPr()
    fonts = props.find(qn("w:rFonts"))
    if fonts is None:
        fonts = OxmlElement("w:rFonts")
        props.insert(0, fonts)
    for kind in ("ascii", "hAnsi", "eastAsia", "cs"):
        fonts.set(qn("w:" + kind), FONT)
    for attr in list(fonts.attrib):
        if attr.rsplit("}", 1)[-1].lower().endswith("theme"):
            del fonts.attrib[attr]
    for node in list(props):
        if node.tag in {qn("w:spacing"), qn("w:kern")}:
            props.remove(node)
    style.font.cs_bold = bold
    szcs = props.find(qn("w:szCs"))
    if szcs is not None:
        szcs.set(qn("w:val"), str(round(size * 2)))
    ppr = style.element.find(qn("w:pPr"))
    if ppr is not None:
        for node in list(ppr):
            if node.tag == qn("w:pBdr"):
                ppr.remove(node)
    color = props.find(qn("w:color"))
    if color is not None:
        color.attrib.pop(qn("w:themeColor"), None)


def clean(text):
    return re.sub(r"\[([^\]]+)\]\([^)]+\)", r"\1", text).replace("**", "").replace(chr(96), "")


def add_field(paragraph, instruction):
    run = paragraph.add_run()
    begin, field, end = (OxmlElement("w:" + n) for n in ("fldChar", "instrText", "fldChar"))
    begin.set(qn("w:fldCharType"), "begin")
    field.set(qn("xml:space"), "preserve")
    field.text = instruction
    end.set(qn("w:fldCharType"), "end")
    for node in (begin, field, end):
        run._r.append(node)


def bookmark(paragraph, name, number):
    start, end = OxmlElement("w:bookmarkStart"), OxmlElement("w:bookmarkEnd")
    start.set(qn("w:id"), str(number))
    start.set(qn("w:name"), name)
    end.set(qn("w:id"), str(number))
    paragraph._p.insert(0, start)
    paragraph._p.append(end)


def internal_link(paragraph, text, anchor):
    link = OxmlElement("w:hyperlink")
    link.set(qn("w:anchor"), anchor)
    run, props = OxmlElement("w:r"), OxmlElement("w:rPr")
    color = OxmlElement("w:color")
    color.set(qn("w:val"), "000000")
    props.append(color)
    run.append(props)
    text_node = OxmlElement("w:t")
    text_node.text = text
    run.append(text_node)
    link.append(run)
    paragraph._p.append(link)


def format_table(table, values):
    table.alignment, table.autofit = WD_TABLE_ALIGNMENT.CENTER, False
    cols, width = len(values[0]), 17.0
    if cols == 2:
        weights = [0.30, 0.70]
    elif cols == 3 and values[0][0] == "欄位":
        weights = [0.42, 0.15, 0.43]
    elif cols == 3 and values[0][0] == "來源":
        weights = [0.18, 0.40, 0.42]
    else:
        weights = [1 / cols] * cols
    for ri, row in enumerate(table.rows):
        for ci, cell in enumerate(row.cells):
            cell.width = Cm(width * weights[ci])
            cell.vertical_alignment = WD_CELL_VERTICAL_ALIGNMENT.CENTER
            tcpr = cell._tc.get_or_add_tcPr()
            borders = OxmlElement("w:tcBorders")
            for edge in ("top", "left", "bottom", "right"):
                border = OxmlElement("w:" + edge)
                for k, v in {"val": "single", "sz": "4", "color": "D9D9D9"}.items():
                    border.set(qn("w:" + k), v)
                borders.append(border)
            tcpr.append(borders)
            margins = OxmlElement("w:tcMar")
            for edge, value in (("top", 85), ("bottom", 85), ("left", 95), ("right", 95)):
                item = OxmlElement("w:" + edge)
                item.set(qn("w:w"), str(value))
                item.set(qn("w:type"), "dxa")
                margins.append(item)
            tcpr.append(margins)
            if ri == 0:
                shading = OxmlElement("w:shd")
                shading.set(qn("w:fill"), "E7EEF5")
                tcpr.append(shading)
            for paragraph in cell.paragraphs:
                pf = paragraph.paragraph_format
                pf.space_after, pf.line_spacing = Pt(0), 1.12
                pf.keep_with_next = ri == 0 or (len(values) <= 8 and ri < len(values) - 1)
                for run in paragraph.runs:
                    run.font.size, run.bold = Pt(9.5), ri == 0
        props = row._tr.get_or_add_trPr()
        props.append(OxmlElement("w:cantSplit"))
        if ri == 0:
            props.append(OxmlElement("w:tblHeader"))
    for ci, col in enumerate(table.columns):
        col.width = Cm(width * weights[ci])


def flowchart():
    ASSETS.mkdir(parents=True, exist_ok=True)
    image = Image.new("RGB", (1600, 470), "white")
    draw = ImageDraw.Draw(image)
    regular = ImageFont.truetype("C:/Windows/Fonts/msjh.ttc", 28)
    bold = ImageFont.truetype("C:/Windows/Fonts/msjhbd.ttc", 30)
    labels = [
        ("相機或手動載圖", "取得灰階來源"),
        ("輸入與參數確認", "來源尺寸與作用中設定"),
        ("找尋物件", "MASK 與格位"),
        ("共用來源準備", "平場與量測來源"),
        ("尺寸與缺陷計算", "相依完成後平行"),
        ("每片判定與整合", "尺寸 缺陷 綜合"),
        ("結果呈現與記錄", "總覽 歷史 TCP"),
    ]
    positions = [(35, 40), (430, 40), (825, 40), (1220, 40), (35, 275), (625, 275), (1215, 275)]
    for (x, y), (main, sub) in zip(positions, labels):
        draw.rectangle((x, y, x + 345, y + 132), fill="#F0F4F8", outline="#8FA3B5", width=2)
        draw.text((x + 22, y + 22), main, font=bold, fill="#132D46")
        draw.text((x + 22, y + 76), sub, font=regular, fill="#31465A")
    def arrow(points):
        draw.line(points, fill="#4B657E", width=4)
        ex, ey = points[-1]
        px, py = points[-2]
        if abs(ex - px) > abs(ey - py):
            d = 1 if ex > px else -1
            draw.polygon([(ex, ey), (ex - 14 * d, ey - 8), (ex - 14 * d, ey + 8)], fill="#4B657E")
        else:
            d = 1 if ey > py else -1
            draw.polygon([(ex, ey), (ex - 8, ey - 14 * d), (ex + 8, ey - 14 * d)], fill="#4B657E")
    for i in range(3):
        arrow([(positions[i][0] + 345, 106), (positions[i + 1][0], 106)])
    arrow([(1392, 172), (1392, 218), (207, 218), (207, 275)])
    arrow([(380, 341), (625, 341)])
    arrow([(970, 341), (1215, 341)])
    image.save(ASSETS / "inspection_workflow.png")


def build(stem):
    lines = (DOCS / (stem + ".md")).read_text(encoding="utf-8").splitlines()
    document = Document()
    section = document.sections[0]
    section.page_width, section.page_height = Cm(21), Cm(29.7)
    section.top_margin, section.bottom_margin = Cm(1.9), Cm(1.9)
    section.left_margin, section.right_margin = Cm(2), Cm(2)
    section.footer_distance = Cm(0.8)
    for name, size, bold in [("Normal", 10.5, False), ("Title", 25, True),
                             ("Subtitle", 11, False), ("Heading 1", 16, True),
                             ("Heading 2", 12.5, True), ("Heading 3", 11, True)]:
        font_style(document.styles[name], size, bold)
    normal = document.styles["Normal"].paragraph_format
    normal.line_spacing, normal.space_after, normal.widow_control = 1.2, Pt(6), True
    for name in ("Heading 1", "Heading 2", "Heading 3"):
        pf = document.styles[name].paragraph_format
        pf.space_before, pf.space_after = Pt(13), Pt(6)
        pf.keep_with_next, pf.keep_together = True, True
    title = lines[0].removeprefix("# ")
    document.add_paragraph(title, "Title")
    document.core_properties.title = title
    document.core_properties.subject = "單攝影機灰階影像檢測"
    document.core_properties.author = "專案文件"
    index = next(i for i, line in enumerate(lines) if line.startswith("## "))
    version_match = re.search(r"文件版本\s+([0-9.]+)", "\n".join(lines[:index]))
    document_version = version_match.group(1) if version_match else "1.1"
    for line in lines[1:index]:
        if line.strip():
            document.add_paragraph(clean(line))
    document.add_paragraph("內容導覽", "Heading 2")
    anchors = {}
    for n, (i, heading) in enumerate(((i, s[3:]) for i, s in enumerate(lines) if s.startswith("## ")), 1):
        anchors[i] = "chapter_" + str(n)
        p = document.add_paragraph()
        p.paragraph_format.space_after = Pt(3)
        internal_link(p, heading, anchors[i])
    document.add_page_break()
    i, bookmark_id = index, 1
    while i < len(lines):
        line = lines[i].strip()
        if not line or line.startswith("<!--"):
            i += 1
            continue
        if line.startswith("~~~") or line.startswith(chr(96) * 3):
            fence, code = line[:3], []
            i += 1
            while i < len(lines) and not lines[i].startswith(fence):
                code.append(lines[i])
                i += 1
            p = document.add_paragraph()
            p.paragraph_format.space_before = Pt(4)
            p.paragraph_format.keep_together = True
            if len(document.paragraphs) > 1:
                document.paragraphs[-2].paragraph_format.keep_with_next = True
            for j, value in enumerate(code):
                r = p.add_run(value + ("\n" if j < len(code) - 1 else ""))
                r.font.name, r.font.size = "Consolas", Pt(9)
                r._r.get_or_add_rPr().rFonts.set(qn("w:eastAsia"), FONT)
            i += 1
            continue
        if line.startswith("|"):
            values = []
            while i < len(lines) and lines[i].strip().startswith("|"):
                row = [clean(x.strip()) for x in lines[i].strip().strip("|").split("|")]
                if not all(re.match(r"^:?-+:?$", x) for x in row):
                    values.append(row)
                i += 1
            if len(values) <= 8 and document.paragraphs:
                document.paragraphs[-1].paragraph_format.keep_with_next = True
            table = document.add_table(rows=len(values), cols=len(values[0]))
            for ri, row in enumerate(values):
                for ci, value in enumerate(row):
                    table.cell(ri, ci).text = value
            format_table(table, values)
            gap = document.add_paragraph()
            pf = gap.paragraph_format
            pf.space_after, pf.space_before, pf.line_spacing = Pt(2), Pt(0), 0.4
            continue
        if line.startswith("## "):
            p = document.add_paragraph(clean(line[3:]), "Heading 1")
            bookmark(p, anchors[i], bookmark_id)
            bookmark_id += 1
        elif line.startswith("### "):
            document.add_paragraph(clean(line[4:]), "Heading 2")
        elif line.startswith("!["):
            match = re.match(r"!\[([^]]*)\]\(([^)]+)\)", line)
            if match:
                picture_paragraph = document.add_paragraph()
                picture_paragraph.paragraph_format.keep_with_next = True
                picture_paragraph.add_run().add_picture(str(DOCS / match[2]), width=Cm(17))
                p = document.add_paragraph(match[1])
                p.alignment = WD_ALIGN_PARAGRAPH.CENTER
                p.paragraph_format.keep_with_next = True
                for run in p.runs:
                    run.font.size = Pt(9)
        elif re.match(r"^\d+\. ", line):
            p = document.add_paragraph(clean(line))
            p.paragraph_format.left_indent, p.paragraph_format.first_line_indent = Cm(0.4), Cm(-0.4)
            if i + 1 < len(lines) and re.match(r"^\d+\. ", lines[i + 1].strip()):
                p.paragraph_format.keep_with_next = True
        elif line.startswith("- "):
            p = document.add_paragraph("• " + clean(line[2:]))
            p.paragraph_format.left_indent, p.paragraph_format.first_line_indent = Cm(0.35), Cm(-0.35)
        else:
            document.add_paragraph(clean(line))
        i += 1
    footer = section.footer.paragraphs[0]
    footer.alignment = WD_ALIGN_PARAGRAPH.RIGHT
    footer.add_run("版本 "+document_version+"    ").font.size = Pt(8)
    add_field(footer, "PAGE")
    for run in footer.runs:
        run.font.size = Pt(8)
    EXPORTS.mkdir(parents=True, exist_ok=True)
    document.save(EXPORTS / (stem + ".docx"))
    print(str(EXPORTS / (stem + ".docx")))


if __name__ == "__main__":
    flowchart()
    from build_illustrations import build_all
    build_all()
    import runpy
    runpy.run_path(str(Path(__file__).with_name("sync_illustrations.py")),run_name="__main__")
    build("USER_MANUAL")
    build("SYSTEM_SPECIFICATION")
