# 文件產出與檢查

Markdown 是維護原稿，Word 與 PDF 是對外閱讀版本。程式基準改變或內容調整後，請同步產出，避免只改其中一個版本。

## 產出順序

1. 更新 docs 內 Markdown，並核對引用的方法與 UI 名稱。
2. 使用含 python-docx、Pillow、pypdf、pypdfium2 的 Python 執行 update_source_trace.py。
3. 執行 build_documents.py，重畫流程圖與 20 張圖解、同步圖解索引及章節插圖，再產出兩份 Word。圖解原稿為 build_illustrations.py，章節對照為 sync_illustrations.py；不要只手動修改輸出的 PNG。
4. 在安裝 Microsoft Word 的 Windows，執行 export_word_pdf.ps1，由同份 Word 匯出 PDF。
5. 執行 verify_documents.py，檢查本地連結、Markdown／Word 文字一致性及近空白頁，並輸出 PDF 頁面 PNG。
6. 逐頁檢查 PNG 的文字、表格、跨頁、框線及頁碼；有修正就重新產出。

~~~powershell
python docs/tools/update_source_trace.py
python docs/tools/build_documents.py
powershell -NoProfile -File docs/tools/export_word_pdf.ps1
python docs/tools/verify_documents.py --render-dir C:\Temp\my-app-4-document-review
~~~

本次實際使用 Codex bundled Python 的絕對路徑，未安裝專案套件。上列 python 命令是部署者已配置對應環境時的簡寫。Word 匯出會建立隱藏背景執行個體，完成後關閉該實例；請不要在匯出過程手動操作它。

## 本次轉檔環境

2026-10-30 已嘗試文件技能的 render_docx.py，環境沒有 soffice.exe，無法以其 LibreOffice 路徑轉檔。因此改由 Microsoft Word 原生匯出同一份 DOCX，再以 bundled pypdfium2 光柵化 PDF 作逐頁檢查。沒有使用系統 LibreOffice。

PDF 光柵化圖片僅用於排版檢查；交付的 18 張操作圖解與 2 張架構圖另存 docs/assets/illustrations。圖解依程式繪製，均明確標註為示意圖，配置與數值為說明範例；不是軟體截圖，也不是演算法實測輸出。圖解索引由 catalog.json 產出並列出對應程式來源。

verify_documents.py 的輸出可重現連結與文字檢查；它不能代替人工版面檢查或應用程式的設備驗收。PDF 與 Word 的總頁數需在重建後重新確認。

本次圖解版為操作手冊 27 頁、功能規格書 18 頁。已檢查全部 45 頁與 20 張圖解、58 個程式來源錨點，以及本地連結及 Markdown／Word 文字一致性；連結數與文件雜湊記錄在 docs/DOCUMENTATION_VERIFICATION.json。未發現文字或框線遮擋。48 個軟體驗收案例仍標為待執行。
