# Project Handoff：整合式影像處理軟體

文件快照：2026-09-28。此文件描述本機工作樹；本機變更不代表已提交或推送到 GitHub。

## 1. 專案與 Git 狀態

- 工作目錄：`C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\整合式影像處理軟件\my-app-4`
- Solution：`MyApp4.sln`
- 主專案：`IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`
- 技術：C#、Windows Forms、.NET Framework 4.7.2、OpenCvSharp。
- 分支：`main`；目前最新提交 `04f9f33 新增量測紀錄編輯與 MASK 裁切顯示`。
- 工作樹包含已修改及未追蹤的程式檔，涵蓋功能拆分、缺陷檢測、大型影像／OpenCV 服務與本次顯示效能調整；尚未納入最新提交。這次只整理文件，未提交或推送 GitHub。
- 最新 Debug 建置使用獨立輸出 `IntegratedImageProcessingApp\bin\Debug-Codex-Sharp\`，成功。一般 `bin\Debug` 輸出曾因程式執行中鎖住 EXE 而無法覆寫；未關閉使用者程式。
- 本次沒有啟動 GUI 或以實際影像做自動驗收；最新平移／清晰度調整仍待使用者實測。

## 2. 系統流程與責任邊界

```text
原圖／大型影像來源
  -> 前處理
  -> 影像處理（Edge / Threshold）
  -> 影像關聯
  -> 整合區塊、區塊來源處理
  -> 物件定義（CCL、篩選、排序／編號、可選旋轉資訊）
  -> 檢測參數
       ├─ 主參數與攝影機精度
       ├─ 尺寸量測與尺寸良品判斷條件
       ├─ 平場校正
       └─ 缺陷量測設定與四個檢測核心
```

WinForms 的 `MainForm` 仍承擔主要 UI 與工作流程協調；以 `partial MainForm` 將功能拆在多個檔案，不代表已完全轉成 MVVM 或服務導向架構。大型影像顯示、灰階解碼、ROI 快取和部分 OpenCV 運算已有獨立服務／控制項。

## 3. 目前功能狀態

### 3.1 大型影像與顯示

- `LargeImageSource` 負責大影像來源及分塊讀取路徑；影像顯示控制項支援縮放、平移、裁切顯示、overlay 與緩衝繪製。
- 最近針對大圖平移進行顯示路徑調整：平移時可繪製的快取磚塊上限設為 64；可視磚塊超過上限時使用預覽圖，避免低倍率每幀重畫大量磚塊。放開滑鼠後照常載入高解析磚塊。
- 預覽層級最高長邊由 2048 提高到 3072，供低倍率平移使用，降低 0.06X 左右預覽被放大時的模糊。平移時是否能使用清晰磚塊取決於當前可視磚塊數及快取命中；`0.03X` 順、`0.04–0.06X` 卡及 `0.14X` 模糊是使用者回報，最新 64 塊上限尚待同圖驗證。
- ROI 灰階內容與 mask builder 有快取／建置服務；遮罩也有已處理二值 MASK 的快取服務。
- 主視窗只有顯示中的工作視圖應積極刷新；切換畫面再套用該視圖狀態，避免背景頁面持續做顯示用更新。
- 外框與各影像頁籤的繪製一致性曾有偏移問題。程式已整理共用顯示路徑，但仍應以原圖、待量測、平場與四個缺陷預覽實際比對，不能只以成功建置視為視覺驗收。

### 3.2 物件定義

- 物件來源可由整合區塊或影像關聯流程取得，依設定執行連通元件分析、篩選、排序／編號及 MASK 保留。
- 可選擇計算物件旋轉／方向資訊；後續 ROI 外框與缺陷選區應依物件幾何方向對齊，不應把原始影像旋轉成正向。
- 旋轉與輪廓計算的耗時應在左下角處理 memo 中可辨識；仍需大圖、多物件實測確認成本。

### 3.3 檢測參數與尺寸量測

- 尺寸量測記錄可保存多條量測定義、名稱、測量模式／方向、平行線數、繪製方向、ROI 外資訊、MASK 來源及穩定識別資訊；載入參數時讀回。
- 量測位置以 ROI 相對座標保存，以面對物件大小、縮放與旋轉的差異。結果支援像素及依 X/Y 精度換算的 mm；斜向距離使用兩軸精度分別換算。
- 1000 條量測線的顯示會依縮放抽樣以降低遠景繪製負擔，完整線數仍用於實際量測；合併線段繪圖時已明確分開各路徑，避免 GDI+ 額外連接相鄰線段。此顯示需實機確認線距與端點外觀。
- 良品條件編輯器支援尺寸量測編號語法，包括不區分大小寫的 `MIN(n)`、`AVG(n)`、`MAX(n)`，並兼容專案舊語法。要把條件編輯／保存，與完整產品判定流程的端到端驗收分開看待。
- 主參數有相機 X、Y 向 `mm/pixel` 精度設定。缺少舊參數時以 1 作為顯示預設，但換算須維持未啟用，避免把預設誤認為已校正精度；實際 mm 值仍需相機標定資料。

### 3.4 平場校正

- 平場校正可選來源 MASK、取樣高度與位置、波形平滑方式、目標灰階，產生校正值並提供預覽。
- 中間無有效校正值的區段可線性補值；最左／最右端則按既定規則延伸最近有效值。
- 校正值可保存並在重新載入參數後恢復；校正值來源 MASK 與實際套用位置 MASK 是不同設定。
- 可在 ROI／指定 MASK 範圍預覽校正影像，並記錄原始影像補正耗時（不含顯示）。速度數值依機器、影像大小和 MASK 分布而異，尚未建立可重複的效能基準。

### 3.5 缺陷檢測

- 每一個缺陷量測設定有四個分支：平場校正影像、對比一、對比二、對比三。平場分支以平場校正後灰階影像為底；其他分支使用各自對比設定。
- 每個分支可設定影像前處理（例如 Gaussian／Median）、灰階門檻、侵蝕／膨脹、最小面積，以及 X/Y 實際尺寸篩選。尺寸輸入以 mm 時需依 X/Y mm-per-pixel 顯示換算 pixel 值；精度未啟用時採像素尺度。
- 檢測範圍是依選定物件 ROI 幾何方向定義的矩形；範圍應以 ROI 相對比例保存，使物件縮放與角度略變時可對應到各物件。需驗證編輯選區時可平移圖片且 viewport 不被重置。
- 預覽底圖是實際處理後的灰階圖。亮／白缺陷 MASK 以半透明黃色呈現，暗／黑缺陷 MASK 以半透明粉紅色呈現；只有符合缺陷定義的連通元件才顯示紅框。
- 每個分支有自己的 MASK 與紅框顯示勾選框。切換 overlay 可見性只改變繪圖，不應重新跑影像運算。
- 分支「套用」只重新處理／更新目前分支；「執行四核心檢測」才更新全部四個分支。這是 UI 更新與昂貴運算的分界，必須保留。
- 平行運算選項把每個物件 ROI 的四個核心分支作為可並行工作單位；物件數乘四是工作數概念，不代表會無上限地建立相同數目的 OS 執行緒。應確認並行度受控、取消與例外狀況能正常收尾。
- 重新執行、載入圖片、刪除檢測參數及關閉視窗時會釋放舊結果預覽等資源；仍需以大圖反覆操作觀察記憶體回收。

## 4. 重要設定與資料

- 專案參數由 `SystemParameterSettings` 建模、`SystemParameterIniService` 序列化到 `SystemParameters.ini`。參數格式變更需同時檢查預設值、載入相容性、保存與讀回。
- 量測 MASK／影像處理階段不能只依顯示名稱或會變動的索引定位；應保存穩定 ID 及其來源脈絡，並在重命名、刪除來源、重新載入時驗證解析。
- 每個處理階段的 MASK 以可重用資料供後續選取。這不等同於所有中間影像都永久存到磁碟；新增階段時要確認 MASK 是否實際產生、保留期限及快取失效策略。
- `.csproj` 使用明確 Compile 清單；加入或移動 C# 檔案時務必更新專案檔，否則 Visual Studio／MSBuild 可能不會編譯該檔。

## 5. 檔案職責與行數快照

行數是 2026-09-28 本機工作樹的快照，後續修改即會變動。`obj`、`bin` 產生檔不列入；Designer 檔雖主要由設計器維護，仍列出供定位。

### 專案入口

| 檔案 | 行數 | 職責 |
|---|---:|---|
| `IntegratedImageProcessingApp/Program.cs` | 16 | 程式進入點與 WinForms 啟動。 |
| `IntegratedImageProcessingApp/IntegratedImageProcessingApp.csproj` | — | .NET Framework 目標、NuGet／組件參考、明確 C# 編譯清單。 |
| `IntegratedImageProcessingApp/App.config` | — | 應用程式執行期組態。 |
| `IntegratedImageProcessingApp/packages.config` | — | 舊式 .NET Framework 專案的 NuGet 套件宣告。 |

### Controls

| 檔案 | 行數 | 職責 |
|---|---:|---|
| `Controls/BufferedRenderPanel.cs` | 12 | 降低 WinForms 面板重繪閃爍。 |
| `Controls/ImageDisplayControl.cs` | 2,010 | 影像顯示、縮放平移、座標轉換、overlay、ROI 編輯及平移時預覽／快取磚塊選擇。 |
| `Controls/ImageDisplayControl.Designer.cs` | 144 | 影像顯示控制項的設計器產物。 |
| `Controls/LargeImageSource.cs` | 1,365 | 大型影像來源、分塊／ROI 讀取、512–3072 長邊預覽層級及快取。 |

### Forms：主流程與影像處理

| 檔案 | 行數 | 職責 |
|---|---:|---|
| `Forms/MainForm.cs` | 2,375 | 主視窗主要狀態、UI 佈局協調及跨功能流程協調；共用大圖 MASK 快取狀態已委派至服務。 |
| `Forms/MainForm.Navigation.cs` | 809 | 主視窗鍵盤、功能清單選取與導覽事件。 |
| `Forms/MainForm.ImageRelations.cs` | 441 | 左側影像關聯清單的新增、編輯、刪除及選單 UI。 |
| `Forms/MainForm.FunctionTree.cs` | 961 | ROI、影像處理群組／步驟的清單操作與右鍵選單。 |
| `Forms/MainForm.Designer.cs` | 1,525 | 主視窗設計器控制項宣告／初始化。 |
| `Forms/MainForm.ImageProcessing.cs` | 820 | 影像處理設定與 Edge／Threshold 等流程協調。 |
| `Forms/MainForm.LargeImageProcessing.cs` | 1,552 | 大圖 MASK 建置排程及 overlay 顯示協調。 |
| `Forms/MainForm.ImageView.cs` | 625 | 原圖、待測等視圖顯示更新與視圖狀態協調；檢測參數單影像版面避免刷新隱藏檢視器。 |
| `Forms/MainForm.Preprocessing.cs` | 1,318 | 前處理設定及執行協調。 |
| `Forms/MainForm.Relation.cs` | 763 | 影像關聯／MASK 組合相關設定與執行；關聯群組大圖 MASK 使用共用快取服務。 |
| `Forms/MainForm.ObjectJudgement.cs` | 1,259 | 物件判定功能 UI 與設定協調。 |
| `Forms/MainForm.ObjectJudgementProcessing.cs` | 2,109 | 物件判定實際處理流程。 |
| `Forms/MainForm.ObjectDefinition.cs` | 1,028 | 物件定義 UI、篩選與旋轉相關設定。 |
| `Forms/MainForm.ObjectDefinitionProcessing.cs` | 1,018 | 物件定義處理協調、來源簽章、結果失效與流程計時。 |
| `Forms/MainForm.ObjectDefinitionMaskSources.cs` | 1,254 | 物件定義來源 MASK 的解析、建立與功能層快取。 |
| `Forms/MainForm.ObjectDefinitionComponents.cs` | 617 | CCL 元件、面積篩選、輪廓／旋轉、保留 MASK、排序與合併。 |
| `Forms/MainForm.ObjectDefinitionDisplay.cs` | 502 | 物件預覽、MASK overlay、大圖繪製、點選命中與顯示計時。 |
| `Forms/MainForm.RoiEditing.cs` | 126 | ROI 編輯模式及主視窗層級互動協調。 |

### Forms：檢測參數與平場／缺陷

| 檔案 | 行數 | 職責 |
|---|---:|---|
| `Forms/MainForm.DetectionParameter.cs` | 1,283 | 檢測參數頁籤與主要設定 UI 協調。 |
| `Forms/MainForm.DetectionParameter.Management.cs` | 529 | 檢測參數新增、刪除、選取及生命週期。 |
| `Forms/MainForm.DetectionParameter.Measurement.cs` | 966 | 尺寸量測頁面、控制項與共用量測狀態。 |
| `Forms/MainForm.DetectionParameter.MeasurementRecords.cs` | 565 | 量測紀錄表格、新增／編輯／刪除、保存與讀回。 |
| `Forms/MainForm.DetectionParameter.MeasurementCalculation.cs` | 667 | MASK 裁切快取、長度統計、連續／忽略斷線邏輯與 mm 換算。 |
| `Forms/MainForm.DetectionParameter.MeasurementDrawing.cs` | 1,165 | Ctrl 畫線互動、ROI 相對幾何、平行線、顯示抽樣與結果 overlay。 |
| `Forms/MainForm.DetectionParameter.MaskSource.cs` | 1,387 | 處理階段 MASK 來源選擇、來源解析與設定套用。 |
| `Forms/MainForm.DetectionParameter.GoodCondition.cs` | 977 | 尺寸良品判斷條件的編輯、保存與語法協調。 |
| `Forms/MainForm.FlatFieldCalibration.cs` | 1,293 | 平場校正 UI、取樣互動、流程協調與狀態管理。 |
| `Forms/MainForm.FlatFieldCalibration.Profile.cs` | 462 | 校正曲線計算、平滑／空洞補值、設定簽章與校正設定保存／讀取。 |
| `Forms/MainForm.FlatFieldCalibration.Correction.cs` | 568 | 校正影像預覽、保存結果套用、分塊像素補正與計時資料。 |
| `Forms/MainForm.FlatFieldCalibration.Masks.cs` | 800 | 校正來源／使用位置 MASK 套用、ROI MASK 建立、遮罩快取及預覽 overlay。 |
| `Forms/MainForm.DetectionParameter.DefectDisplay.cs` | 360 | 缺陷顯示分頁、預覽圖層及顯示狀態。 |
| `Forms/MainForm.DetectionParameter.DefectRegion.cs` | 1,006 | 缺陷檢測矩形的建立、編輯與 ROI 相對範圍設定。 |
| `Forms/MainForm.DetectionParameter.DefectCores.cs` | 803 | 四核心設定頁、每核心控制項及單核心／全核心命令。 |
| `Forms/MainForm.DetectionParameter.DefectProcessing.cs` | 1,341 | 缺陷核心運算、篩選、並行工作與結果套用。 |

### Services：影像運算、快取、參數

| 檔案 | 行數 | 職責 |
|---|---:|---|
| `Services/OpenCvEdgeDetectionService.cs` | 169 | OpenCvSharp Edge 偵測運算服務。 |
| `Services/OpenCvThresholdService.cs` | 129 | OpenCvSharp 灰階門檻／二值化服務。 |
| `Services/OpenCvImageProcessingService.cs` | 118 | OpenCvSharp 影像處理共用運算服務。 |
| `Services/ProcessedBinaryMaskCache.cs` | 100 | 已處理二值 MASK 的快取與重用。 |
| `Services/LargeImageMaskCache.cs` | 228 | 影像處理步驟與關聯群組共用的大圖 MASK 快取、建置狀態、世代失效及 Cv.Mat 生命週期。 |
| `Services/LargeImageGrayscaleDecoder.cs` | 89 | 大圖灰階解碼支援。 |
| `Services/LargeRoiGrayscaleCache.cs` | 125 | 大圖 ROI 灰階資料快取。 |
| `Services/LargeRoiMaskBuilder.cs` | 187 | 依大型 ROI 建立或裁切對應 MASK。 |
| `Services/SystemParameterSettings.cs` | 474 | 檢測參數及應用設定的資料模型。 |
| `Services/SystemParameterIniService.cs` | 1,963 | INI 參數讀寫、預設值與相容性序列化。 |

`LargeImageMaskCache` 目前只管理影像處理步驟與關聯群組共用的大圖 MASK。物件判定、物件定義、量測和平場校正的快取仍由各自功能管理，因為它們的結果型態、key 和失效時機不同；後續若要共用，需先確認語意和生命週期一致。

## 6. 建置與產物

根目錄 solution：`MyApp4.sln`；主專案目標為 .NET Framework 4.7.2。

```powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU"
```

Visual Studio 或一般 MSBuild 的輸出路徑可能依組態而異；`DebugLayout` 是先前使用的輸出資料夾，不代表目前最新測試版。

2026-09-28 本機效能調整版輸出為 `IntegratedImageProcessingApp\bin\Debug-Codex-Sharp\IntegratedImageProcessingApp.exe`。此為獨立 Debug 輸出；目前一般 `bin\Debug` EXE 曾被執行中的程式鎖定，建置新版本後需重新啟動測試版才能載入新程式碼。

## 7. 驗證清單

### 已做

- 2026-09-28 最新 Debug-Codex-Sharp 獨立輸出建置成功。
- 平移時快取磚塊上限調為 64、預覽長邊上限與預覽層級增為 3072；改動目標是低倍率流暢度與中低倍率清晰度折衷，尚無本機 FPS 數據。
- 尺寸量測疊圖改為快取 `GraphicsPath`、依螢幕像素密度抽樣顯示，並以 `StartFigure` 保持每條線獨立；實際量測資料不抽樣。
- 檢查影像檢視同步路徑，在檢測參數的單影像版面不再於每個平移事件刷新隱藏的右側檢視器。
- 確認主專案 Compile 清單含目前拆出的缺陷 UI／運算、大型 ROI 及 OpenCV 服務檔案。
- 2026-09-27 Debug 建置成功；共用大圖 MASK 的字典、建置 key、鎖與世代狀態已移入 `LargeImageMaskCache`，舊的 `MainForm` 直接存取點已清除。
- 盤點所有非產生 `.cs` 檔並更新本文件行數與職責。

### 尚待手動／端到端驗收

- 啟動程式，檢查原圖、待測、平場和四個缺陷預覽外框、影像位置、縮放平移一致性。
- 使用最新版 `Debug-Codex-Sharp` 對同一張大圖測試 `0.03X`、`0.04X`、`0.05X`、`0.06X`、`0.14X` 平移：分別記錄流暢度、平移中清晰度、放開滑鼠後清晰度及遮罩／ROI 對位；確認 64 塊上限不會在較大視窗或不同影像尺寸下重新造成卡頓。
- 在待量測頁測試 1000 條線的遠景顯示，確認沒有多餘的垂直／斜向連接線、線距觀感可接受，且縮放後顯示與實際統計一致。
- 選一個物件建立缺陷矩形，再切換其他物件，確認 ROI 相對座標與旋轉方向對應正確；拖曳編輯時可平移且不意外縮放／重置視圖。
- 分別按各核心「套用」，確認僅該核心結果更新；再按「執行四核心檢測」，確認四個核心都更新。確認 MASK／紅框勾選不觸發重新運算。
- 用含亮缺陷、暗缺陷、門檻邊界和面積邊界的測試影像，檢查黃／粉紅 MASK、紅框與 component 尺寸篩選正確。
- 開關平行運算比較時間與結果一致性；測試取消、例外與重複執行時資源釋放。不要假定 ROI 數 × 4 就是固定執行緒數。
- 使用相機實際 X/Y mm-per-pixel 驗證尺寸換算、斜線量測及物件 X/Y 尺寸篩選，並以舊 INI 檔驗證相容性。
- 儲存、關閉、重新開啟參數，確認平場曲線、MASK 穩定 ID、量測資料、缺陷核心設定與檢測範圍完整回復。
- 對良品條件既有語法與 `MIN(n)`／`AVG(n)`／`MAX(n)` 做解析與實際結果測試；確認單筆量測限制及未命中 MASK 的定義一致。
- 用接近實際尺寸的大圖重複處理，記錄耗時、峰值記憶體與 UI 回應；當前建置成功不代表大圖效能已驗收。

## 8. 後續建議順序

1. 先用真實產品影像完成缺陷四核心視覺與結果驗收，記錄每核心的輸入圖、門檻／形態處理結果、MASK 及合格元件數。
2. 以代表性 ROI 數量做序列與平行模式對照；量測吞吐、CPU、記憶體與取消響應，確定合理的並行上限。
3. 建立參數往返測試：新舊 INI、MASK 穩定識別、量測、平場補值與四核心結果設定。
4. 完成端到端尺寸良品判定與 mm 校正驗收，補上測試資料與可重複步驟。
5. 再評估把大型流程協調從 `MainForm` 拆成服務；每次只搬一個責任並保持結果對照，避免只為降低行數而大規模重構。
6. 完成驗收後再整理提交與 GitHub 發布；目前工作樹尚未提交／推送。

## 9. 維護注意事項

- 不要把尚未驗證的 GUI／影像結果描述成已驗收；build、UI 顯示、影像演算法正確性是三種不同的驗證。
- 新增 `.cs` 必須更新 `IntegratedImageProcessingApp.csproj` 的 Compile 清單。
- 修改 `SystemParameterSettings` 時，同步檢查 INI 讀寫、舊參數預設、保存／重新載入和 UI 綁定。
- OpenCvSharp `Mat`、`Bitmap`、ROI buffer 和平行工作產生的暫存資料要明確管理生命週期；勿讓 UI overlay 或快取無限保留完整大圖複本。
- 影像運算不要阻塞 UI 執行緒；但也要限制並行度及同時存在的完整影像副本，避免記憶體壓力。
- MASK 名稱只是顯示資訊；跨參數文件保存時要以穩定 ID 和來源脈絡解析，處理來源不存在或 ID 過期的情況。
