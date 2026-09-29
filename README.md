# 整合式影像處理軟體

以高解析度灰階影像為主的 Windows 桌面影像處理與檢測工具。技術採用 C# WinForms、.NET Framework 4.7.2 與 OpenCvSharp。

## 專案狀態

- 文件快照：2026-09-28。
- 本機分支 `main` 的 HEAD 為 `04f9f33`。目前工作目錄另有已修改及未追蹤的程式檔；尚未提交或推送到 GitHub。
- 最新 Debug 測試版建置成功，輸出位於 `IntegratedImageProcessingApp\bin\Debug-Codex-Sharp\IntegratedImageProcessingApp.exe`。
- 最新大圖平移與清晰度調整已編譯，但仍待以實際影像驗收；建置成功不代表拖曳效能或影像品質已確認。

## 主要流程

```text
原圖
  -> 前處理
  -> 影像處理（Edge / Threshold）
  -> 影像關聯
  -> 整合區塊與區塊處理
  -> 物件定義（連通元件、篩選、編號、可選旋轉資訊）
  -> 檢測參數（平場、尺寸量測、良品條件、缺陷四核心）
```

檢測參數包含尺寸量測與尺寸良品條件、平場校正，以及平場／對比一／對比二／對比三四個缺陷檢測核心。各缺陷核心有獨立設定與預覽；單核心「套用」只更新該核心，「執行四核心檢測」才執行全部核心。影像 MASK 來源、檢測範圍及結果保存方式詳見專案交接文件。

## 最近顯示效能調整

- 大圖平移時，依可視區域大小限制快取磚塊繪製數；上限目前為 64 塊，超過時以預覽圖保持拖曳流暢，放開後再載入細節。
- 預覽長邊上限提高至 3072，改善中低倍率預覽被放大時的模糊。
- 待量測畫面的平行線疊圖會依縮放抽樣顯示；量測計算仍使用完整線數。獨立線段繪圖也已避免路徑意外相連。
- 使用者回報 `0.03X` 拖曳順、`0.04–0.06X` 曾卡頓，`0.14X` 曾模糊。64 塊上限與 3072 預覽是最新折衷，尚待使用者用同一張大圖確認流暢度、清晰度與 MASK 對位。

## 建置

以 Visual Studio 開啟根目錄的 `MyApp4.sln`，還原 NuGet 套件後選擇 `Debug` / `Any CPU` 建置。也可使用：

```powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"
```

主專案為 `IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`。此舊式 .NET Framework 專案使用明確的 `<Compile Include=...>` 清單；新增 C# 檔案時要一併更新專案檔。

## 程式結構

- `IntegratedImageProcessingApp\Controls`：影像顯示、縮放平移、大型影像來源與預覽／分塊繪製。
- `IntegratedImageProcessingApp\Forms`：WinForms 主視窗與各功能流程；`MainForm` 以 partial 檔分工，仍負責主要 UI 協調。
- `IntegratedImageProcessingApp\Services`：OpenCV 運算、INI 參數、灰階解碼及 ROI／MASK 快取與建置。

完整檔案職責、功能進度、資料保存語意、建置狀態、已知限制與接手驗收步驟請見 [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md)。

## 驗收狀態

最新獨立 Debug 輸出已成功建置；本次未啟動 GUI，也未執行自動影像測試。請以代表性大圖驗證 `0.03X`、`0.04X`、`0.05X`、`0.06X`、`0.14X` 平移時的流暢度與清晰度，並檢查放開滑鼠後的細節載入、ROI／MASK 對位及量測線顯示。
