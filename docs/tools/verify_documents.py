"""Check document links and source parity, then render PDFs for visual review."""
from pathlib import Path
import argparse
import json
import re
import sys
import tempfile
from urllib.parse import unquote
from docx import Document
from pypdf import PdfReader
import pypdfium2 as pdfium
from PIL import Image, ImageDraw
sys.dont_write_bytecode = True
from build_documents import clean

ROOT = Path(__file__).resolve().parents[2]
DOCS = ROOT / "docs"
parser = argparse.ArgumentParser()
parser.add_argument("--render-dir", type=Path, default=Path(tempfile.gettempdir()) / "my-app-4-document-review")
args = parser.parse_args()
args.render_dir.mkdir(parents=True, exist_ok=True)
errors, checked_links = [], 0
for path in DOCS.rglob("*.md"):
    content = path.read_text(encoding="utf-8")
    if "\ufffd" in content:
        errors.append(f"Replacement character in {path}")
    for target in re.findall(r"!?\[[^\]]*\]\(([^)]+)\)", content):
        if target.startswith(("http:", "https:", "#")):
            continue
        destination = unquote(target.split("#", 1)[0].strip("<>"))
        resolved = (path.parent / destination).resolve()
        if not resolved.exists():
            errors.append(f"Missing link: {path.name}: {target}")
        checked_links += 1
report = {"checked_local_links": checked_links, "documents": [], "errors": errors}
for stem in ("USER_MANUAL", "SYSTEM_SPECIFICATION"):
    document = Document(DOCS / "exports" / (stem + ".docx"))
    docx_text = "\n".join(
        [p.text for p in document.paragraphs] +
        [cell.text for table in document.tables for row in table.rows for cell in row.cells])
    normalized = re.sub(r"\s+", "", docx_text)
    code = False
    for line in (DOCS / (stem + ".md")).read_text(encoding="utf-8").splitlines():
        line = line.strip()
        if line.startswith(("~~~", chr(96) * 3)):
            code = not code
            continue
        if not line or line.startswith(("![","<!--")):
            continue
        if line.startswith("|"):
            cells = [x.strip() for x in line.strip("|").split("|")]
            if all(re.match(r"^:?-+:?$", x) for x in cells):
                continue
            expected = cells
        else:
            expected = [re.sub(r"^#{1,6}\s+", "", line)]
        for item in expected:
            if re.sub(r"\s+", "", clean(item)) not in normalized:
                errors.append(f"DOCX content absent {stem}: {item[:90]}")
    pdf_path = DOCS / "exports" / (stem + ".pdf")
    reader, pdf = PdfReader(pdf_path), pdfium.PdfDocument(str(pdf_path))
    output = args.render_dir / stem
    output.mkdir(exist_ok=True)
    chars = []
    for index, page in enumerate(pdf):
        text = reader.pages[index].extract_text() or ""
        chars.append(len(text))
        if len(text.strip()) < 40 and not reader.pages[index].images:
            errors.append(f"Nearly blank page {stem}: {index + 1}")
        page.render(scale=1.7).to_pil().save(output / f"page-{index+1:02}.png")
    for first in range(1, len(pdf) + 1, 2):
        pictures = [Image.open(output / f"page-{number:02}.png").convert("RGB")
                    for number in range(first, min(first + 2, len(pdf) + 1))]
        width, height = pictures[0].size
        sheet = Image.new("RGB", (width * 2 + 30, height + 45), "#DCE3E9")
        draw = ImageDraw.Draw(sheet)
        for offset, picture in enumerate(pictures):
            sheet.paste(picture, (offset * (width + 15), 40))
            draw.text((offset * (width + 15) + 20, 10), f"{stem} / {first+offset}", fill="black")
        sheet.save(args.render_dir / f"{stem}-{first:02}.png")
    report["documents"].append({"name": stem, "pages": len(pdf), "page_text_lengths": chars})
report["render_directory"] = str(args.render_dir)
(args.render_dir / "verification.json").write_text(json.dumps(report, ensure_ascii=False, indent=2), encoding="utf-8")
print(json.dumps(report, ensure_ascii=True))
if errors:
    raise SystemExit(1)
