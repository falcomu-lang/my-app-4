# 整合式影像處理軟體

C# Windows Forms 高解析度影像處理工具，使用 .NET Framework 4.7.2 與 OpenCvSharp。主要流程是：

`原圖 -> 影像前處理 -> Edge Detect / Threshold -> 影像關聯 -> 整合成區塊 -> 區塊處理 -> 區塊結果`

## 目前狀態

目前已完成可建置的影像處理主流程與區塊處理基礎。大圖以 `LargeImageSource` 分塊顯示，但演算法結果仍以完整解析度 OpenCV `Mat` 為準，顯示時才依目前視窗取出需要的區域。

目前兩側檢視分頁固定為：

`原圖 -> 前處理 -> 處理後 -> 區塊處理 -> 區塊結果 -> debug`

已完成的主要功能：

- 讀取 BMP、JPG/JPEG、PNG、TIF/TIFF，並在記憶體中統一為灰階。
- 大型影像 WIC 預覽與原始解析度 tile 顯示，支援 16384 級以上影像。
- 多個 ROI 的建立、顯示全部、刪除、保存與原圖座標定位。
- 原圖、前處理、處理後與區塊處理的縮放、平移、重設視圖及左右同步。
- 影像前處理：`Normalize`、`Gaussian Blur`、`CLAHE`、`Median Blur`、`Sharpen`、`Bilateral Filter`。
- Edge Detect：OpenCV `Canny Edge`、`Sobel Edge`、`Polarity Edge`。
- Threshold：OpenCV `Global Threshold`、`Adaptive Threshold`、`Otsu Threshold`；Global Threshold 支援 Single/Range。
- `影像處理` 流程樹只保留 `Edge Detection` 與 `Threshold` 兩個主分類；形態學、輪廓與物件判定功能改由後續區塊/判定流程負責。
- 影像關聯：以固定 ID 選擇原圖、前處理步驟/群組，以及影像處理步驟/群組。
- 整合成區塊：使用單一關聯或關聯群組的二值結果，再依序套用區塊處理。
- 整合成區塊支援每個區塊的處理步驟展開/收合，也支援多選區塊或多選群組建立階層群組。
- 物件定義：位於整合成區塊下方，可建立多個物件組；每個物件組可指定一個來源區塊，並可建立有唯一 ID 的處理子項目。物件組處理演算法尚待定義。
- 區塊處理 OpenCV 方法：`Dilate`、`Erode`、`Close`、`Open`、`Fill Contour`、`Fill Hole`、`Connect gap / bridge`、`Morphological Reconstruction`、`Merge by distance`、`Convex Hull`。
- 處理結果以紅色 overlay 疊在指定來源影像上；區塊處理結果顯示於 `區塊處理` 分頁。
- 影像處理、前處理與區塊處理都支援背景 `Task.Run`、版本檢查、快取與舊結果淘汰。

## 基本操作

1. 點選左側 `讀取圖片` 載入影像。
2. 在 `指定 ROI` 上按右鍵，選 `新增 ROI`，於原圖拖曳 ROI 框並確認。
3. 在 `影像前處理` 上按右鍵新增前處理；點選步驟後於右側選擇方法與參數，按 `套用` 才保存設定。
4. 在 `影像處理` 上按右鍵新增影像處理；於右側流程樹選擇 Edge Detect 或 Threshold 方法，再設定參數。
5. 若要讓影像處理使用前處理結果，於 `影像關聯` 上按右鍵新增關聯，選擇來源影像與目標影像處理後按 `套用`。
6. 在關聯上按右鍵選 `處理`，或按住 `A`/`a` 後以左鍵點擊，才開始該關聯的運算。
7. 在 `整合成區塊` 上按右鍵新增區塊，選擇單一關聯或關聯群組，再新增區塊處理步驟。
8. 在區塊或區塊處理步驟上按右鍵選 `處理`，結果會顯示於 `區塊處理` 分頁。
9. 在 `物件定義` 上按右鍵新增 `物件組`；選取 `物件組1`、`物件組2` 後，右側會顯示來源區塊下拉選單，按 `確認` 保存來源。
10. 對物件組按右鍵可執行處理、上移、下移、命名、新增處理或刪除；新增的子項目會顯示為 `處理1`、`處理2`，每一項都有獨立 ID。
11. 點擊區塊可展開/收合其處理步驟；選取兩個以上未分組區塊後按右鍵，可建立物件群組；選取兩個以上群組後按右鍵，可再建立上層群組。

選取項目只負責顯示設定與已完成的結果，不應因切換分頁或縮放而重新執行演算法。影像載入時只建立原圖與已保存 ROI 的準備資料，不自動執行影像處理或前處理結果。

## 最新更新（2026-09-17）

- `物件定義` 已由 placeholder 擴充為物件組清單；預設名稱為 `物件組1`、`物件組2`。
- 物件組右側新增「來源區塊」下拉選單與「確認」按鈕，來源以區塊 GUID 儲存，不依賴顯示名稱或目前索引。
- 物件組右鍵選單已加入處理、上移、下移、命名、新增處理、刪除；物件組處理子項目支援處理、上移、下移、命名、刪除。
- 物件組與物件組處理項目均使用獨立 GUID，改名或排序不會改變引用對象。
- `SystemParameters.ini` 已保存物件組處理項目的 ID、名稱、方法與參數，舊設定可向下相容載入。
- 統一狀態列時間格式；影像處理全部時間現在不包含顯示時間，整合成區塊會額外列出區塊處理時間。

- `Global Threshold` 已使用 OpenCV 實作，支援單一門檻與雙邊範圍門檻；範圍模式使用 `Cv2.InRange`。
- 區塊處理的顯示時間現在以可見 `區塊處理` 分頁實際刷新完成為準，不再把 `InvalidateImageView()` 的排程時間當成顯示時間。
- 處理前會保存目前實際可見分頁的縮放與平移狀態；處理完成不會把原圖、前處理或其他分頁套成錯誤的舊位置。
- 只要新舊影像尺寸相同，Bitmap 與 `LargeImageSource` 互換時也會保留 Zoom/Offset。
- 背景不可見分頁不強制刷新，切回前景時才更新，避免處理流程造成介面卡頓。

## OpenCV 與處理規則

所有目前已實作的影像演算法都必須走 OpenCvSharp，不使用舊的 managed/tile 演算法作為無聲備援。

- Canny：`GaussianBlur -> Cv2.Canny -> binary mask`。
- Sobel：`Cv2.Sobel` 產生梯度，再依方向與門檻形成 binary mask。
- Polarity：OpenCV Gaussian blur、Sobel 梯度與極性/對比判斷。
- 前處理：各方法使用對應的 OpenCV 函數；前處理輸出是後續影像處理的來源。
- 區塊處理：形態學、輪廓、填洞、凸包及間隙連接均使用 `Cv2` 函數。
- 關聯群組：各關聯取得的二值結果以 `BitwiseOr` 合併；區塊內處理步驟則以上一步輸出作為下一步輸入。

影像處理的結果只在 ROI 內產生，處理後/區塊處理畫面則以關聯指定的來源影像作底圖，再疊加紅色結果。若來源是前處理步驟或群組，底圖與演算法都必須使用同一份前處理輸出，不能拿較深的原圖覆蓋結果。

### 目前參數

- Canny：低門檻、高門檻、核心大小 `3/5/7`、L2 梯度、高斯模糊大小、高斯 Sigma。
- Sobel：灰階變化方向、核心大小 `1/3/5/7`、邊緣門檻。
- Polarity：極性、對比門檻、`CoreWidth`、平滑、高斯 Sigma、搜尋方向、邊界類型。
- 前處理：各方法只顯示會被實際 OpenCV 運算使用的參數；Bilateral Filter 使用直徑、SigmaColor、SigmaSpace。
- 區塊形態學：核心大小、迭代次數、形狀 `Rect/Ellipse/Cross`。
- Connect gap / bridge：間隙大小與方向 `All/Horizontal/Vertical`。
- Merge by distance：距離像素。

Canny 不再提供 `Strongest`、`Longest`、最小邊緣長度與最大斷點間距。這些是輪廓/特徵篩選條件，不是標準 Canny 參數；未來應放在獨立的輪廓分析或特徵篩選階段。

`Morphological Reconstruction` 目前實作為 opening-by-reconstruction：先侵蝕取得 marker，再反覆膨脹並以原始 mask 限制，直到結果穩定。`Merge by distance` 會擴張相鄰區域後合併，因此距離過大會改變區域外形。

## 大圖與效能注意事項

- 演算法不應在 Paint 或每個可見 tile 中重新執行；tile 只負責顯示已完成的 mask。
- 大圖使用完整解析度 OpenCV `Mat` 保存權威結果，放大、縮小、平移只重新產生目前視窗的 overlay。
- 前處理顯示時間可能高於 OpenCV 運算時間，因為需要建立新的記憶體型顯示來源；這不代表演算法重新讀取圖片。
- `LargeImageSource` 由共享來源持有 tile，繪製前使用短期複本，避免快速平移或調參時發生 GDI+ 物件並行使用例外。
- 背景工作必須保留來源參考，完成時檢查 image generation、項目 ID 與目前請求，過期結果直接釋放。
- 不要讓每個 tile 或 ROI 更新一次狀態列；狀態更新需節流，避免大量 `BeginInvoke` 造成 UI 鈍化。
- `InvalidateImageView()` 只代表排程重繪；若要計算顯示時間，必須對可見控制項完成同步刷新後再停止計時。不可見背景分頁不應被誤報為已完成顯示。
- 16384 x 50000 類型的大圖會需要數個大型 native temporary Mat；目前優先保留連續、精準的全 ROI 結果，實際記憶體需求仍需以目標圖片壓力測試。
- 使用 64 位元行程；專案的 `Prefer32Bit` 必須維持 `false`。

## 建置

開發環境：

- Visual Studio 2019 或更新版本
- .NET Framework 4.7.2 Developer Pack
- NuGet 套件依 `IntegratedImageProcessingApp/packages.config` 還原
- OpenCvSharp4 及其 .NET Framework 相依套件

使用 Visual Studio 開啟 `MyApp4.sln`，還原套件後建置 `IntegratedImageProcessingApp`。也可以使用：

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' .\MyApp4.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

目前文件更新前的建置驗證已成功通過；提交前仍應確認 Visual Studio 執行中的舊版 exe 已關閉。

## 程式結構與後續拆分

已拆出的 partial 檔案：

- `IntegratedImageProcessingApp/Forms/MainForm.ImageProcessing.cs`：Canny、Sobel、Polarity 的 OpenCV 實作與影像處理流程。
- `IntegratedImageProcessingApp/Forms/MainForm.Preprocessing.cs`：前處理方法、群組、參數、快取、背景執行與結果顯示。
- `IntegratedImageProcessingApp/Forms/MainForm.Relation.cs`：影像關聯的資料欄位、ID 與基礎模型。
- `IntegratedImageProcessingApp/Forms/MainForm.ObjectJudgement.cs`：整合成區塊的清單、右鍵選單、關聯選擇與區塊處理分頁。
- `IntegratedImageProcessingApp/Forms/MainForm.ObjectJudgementProcessing.cs`：區塊處理參數、OpenCV 形態學/輪廓方法、背景運算與 mask overlay。
- `IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinition.cs`：物件組清單、來源區塊設定、唯一 ID 對應、右鍵選單與物件組處理子項目。

主要共用檔案：

- `IntegratedImageProcessingApp/Forms/MainForm.cs`：主介面協調、圖片載入、ROI、關聯流程與共用顯示行為。
- `IntegratedImageProcessingApp/Forms/MainForm.Designer.cs`：可由 Visual Studio Designer 編輯的主框架與固定分頁。
- `IntegratedImageProcessingApp/Controls/ImageDisplayControl.cs`：縮放、平移、ROI 繪製、視圖同步與 tile 顯示。
- `IntegratedImageProcessingApp/Controls/LargeImageSource.cs`：大型圖片 WIC Gray8 預覽及原始解析度 tile 來源。
- `IntegratedImageProcessingApp/Services/SystemParameterSettings.cs`、`SystemParameterIniService.cs`：設定模型與 INI 保存。

預計後續拆分順序：

1. 將完整影像關聯建立/套用/執行/右鍵選單移入 `MainForm.Relation.cs`。
2. 建立 `MainForm.Display.cs`，集中分頁、縮放、平移、左右同步與全畫面切換。
3. 建立 `MainForm.Parameters.cs`，集中共用參數面板、套用/取消與狀態文字。
4. 建立 `MainForm.Roi.cs`，集中 ROI 建立、刪除、顯示全部與 ROI 準備。
5. 建立 `MainForm.Cache.cs`，集中 ROI、前處理、mask、overlay 與 generation 生命週期。
6. 將物件組的實際判定處理與結果顯示獨立到 `MainForm.ObjectDefinitionProcessing.cs`，但要先確認演算法規格。
7. partial 拆分穩定後，再評估把純 OpenCV 函數抽至 `Services/OpenCvProcessingService.cs`。

拆分時先維持 `partial class MainForm` 與既有行為，每移動一批就建置；確認所有呼叫者後才刪除舊方法或未使用 using。

## 物件組目前邊界

目前 `物件定義` 先建立「物件組」的資料與來源選擇，不代表物件組的最終判定演算法已完成。每個物件組可指定一個來源區塊，來源儲存的是區塊的 GUID；物件組可再新增 `處理1`、`處理2` 等子項目，子項目同樣具有自己的 GUID、名稱、方法與參數欄位。

目前已完成的物件組操作：

- 新增物件組，預設名稱為 `物件組1`、`物件組2`。
- 選擇來源區塊並按 `確認` 保存。
- 物件組處理、上移、下移、命名、新增處理與刪除。
- 物件組處理項目的處理、上移、下移、命名與刪除。
- 物件組與子處理在清單重建、排序、改名後仍以 ID 對應。
- INI 讀寫包含物件組與子處理的 ID、名稱、方法、參數及來源區塊 ID。

尚未完成的部分：

- 物件組處理子項目的實際影像演算法與參數面板。
- 物件組結果在區塊結果/物件結果分頁的實際繪製。
- 多來源區塊合併、物件輪廓篩選及 E/D 等最終判定規則。

## 流程計時規則

狀態列的 `影像處理全部時間` 只代表演算法流程，不包含顯示結果的時間。

影像處理或關聯：

`影像處理全部時間 = 影像前處理時間 + 影像處理時間`

整合成區塊：

`影像處理全部時間 = 影像前處理時間 + 影像處理時間 + 整合成區塊處理時間`

`顯示時間` 會另外列出，代表可見分頁實際更新所花的時間。若背景分頁尚未顯示，不能把尚未發生的顯示工作誤報為已完成；切到前景後才補上顯示時間。快取命中且該階段沒有重新運算時，該次階段時間顯示為 `0 ms`。
