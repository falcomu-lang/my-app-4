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

## 介面設計準則

這個軟體是長時間處理大型影像的工具，介面設計要讓使用者隨時知道「目前選到什麼」、「是否真的執行」、「結果來自哪裡」，並且不能因背景工作造成畫面跳動或誤導。

### 清單與操作行為

- 主項目只負責分類與展開/收合；子項目代表實際可設定或可執行的流程。
- 普通左鍵點擊是選取；群組的展開/收合使用普通左鍵，按住 `Ctrl` 或 `Shift` 時不得因選取而展開或收合。
- 右鍵只顯示目前項目能執行的操作。處理、排序、命名、新增子項目、解除群組與刪除要依項目類型提供，不能把不適用的功能放到所有選單。
- `A`/`a` 是明確執行快捷鍵；單純選取項目不應偷偷啟動大型影像運算。
- `Ctrl`/`Shift` 多選時，清單不得因中途重建而清掉已選項目；群組操作要先收集 ID，再一次完成變更。
- 清單更新應只更新受影響的節點與必要的顯示文字，避免整個 ListBox 反覆重建造成閃爍、選取跳動或多次觸發事件。
- 名稱是給人看的；任何關聯、快取、結果或父子關係都必須使用唯一 ID，不可用顯示名稱或目前索引當作永久鍵。

### 參數面板

- 參數修改先留在 pending 狀態；只有按下「套用」才寫入設定並啟動處理。
- 按「取消」要捨棄 pending 值並還原套用前的數字，不可把尚未確認的值寫入 INI 或處理流程。
- 套用、取消與重新選擇方法時，要保留目前圖片的 Zoom、Offset、可見分頁與左右對位狀態。
- 重新選擇方法後，參數控制項只能顯示該方法真正會使用的參數；不支援或只屬於舊流程的參數應從介面移除。
- 參數面板必須清楚顯示目前對象的名稱與方法；不能只顯示泛用的「參數設定」，以免使用者誤改到另一個項目。
- 運算中的狀態要有完成、失敗與取消三種結果；完成後應顯示本次項目的時間，不要持續停留在「影像處理中」。

### 影像檢視

- 原圖、前處理、處理後、區塊處理與結果分頁都必須支援一致的放大、縮小、平移與重設視圖行為。
- 處理前先保存目前真正可見控制項的 Zoom/Offset；背景分頁不應被強制刷新，也不能拿背景分頁的 view state 覆蓋前景。
- 左右同步只同步目前顯示中的分頁；不可為了同步而更新所有背景分頁。背景分頁切到前景時，才依目前來源與 generation 補上內容。
- 影像來源切換時，只要影像尺寸與座標系一致，就必須保留使用者目前的視圖位置；不能因換 Bitmap、換 `LargeImageSource` 或更新 overlay 就回到 fit-to-view。
- 紅線、紅點或其他標記必須疊在正確的底圖上。關聯使用前處理影像時，處理後底圖也要使用同一份前處理輸出，不能用原圖代替。
- 顯示區域若尚未完成預覽，應明確顯示「待顯示」或進度；不可讓空白畫面看起來像演算法沒有結果。

### 狀態與錯誤訊息

- 狀態列只保留最新且對使用者有用的狀態，不能每個 tile、ROI 或 `BeginInvoke` 都刷新一次。
- 狀態文字要區分：已選取、等待套用、影像處理中、產生預覽圖中、已完成、已處理、待顯示與失敗。
- 如果項目已經有相同設定的完成結果，重新點擊應顯示「已處理」並使用快取，不應再次運算。
- 來源不存在、ROI 未建立、參數不完整、關聯或區塊引用失效時，要在操作當下指出缺少的項目，不要只顯示泛用錯誤。
- 刪除被其他關聯、區塊或物件組引用的項目以前，必須列出引用者並要求確認；不能靜默刪除造成後續流程失效。

## 軟體設計與維護準則

### 流程與資料來源

- 明確維持資料流：`原圖 -> 前處理 -> Edge/Threshold -> 影像關聯 -> 整合成區塊 -> 物件組`。
- 每個階段都要明確記錄來源類型與來源 ID；執行時只能從該來源取得影像，不能因快取不存在就退回原圖。
- 前處理群組由上到下串接；影像處理群組依需求合併二值結果；區塊處理則把上一個處理的輸出交給下一個處理。三種群組語意不能混用。
- 所有目前的影像演算法必須走 OpenCvSharp；Legacy 程式碼不能作為無聲 fallback。若真的需要相容舊資料，要在載入或遷移階段處理並留下明確紀錄。
- 參數、來源 ID、順序與顯示名稱要分開管理；改名或排序只能改資料的呈現順序，不可改變流程引用。

### 非同步與取消

- OpenCV 和大型影像處理放在 `Task.Run` 或專用背景流程；UI 執行緒只做控制項更新與必要的短時間狀態變更。
- 每次圖片、ROI、來源、參數或流程變更都要增加 generation/request version。背景工作完成後先驗證版本，過期結果直接釋放，不得覆蓋新結果。
- 新請求到來時，要取消或標記前一個請求過期；不可讓多個舊請求同時回寫同一組 Bitmap、Mat 或狀態列。
- `BeginInvoke` 必須在回呼前再次確認 Form 尚未關閉、控制項尚未 Dispose、請求仍是目前版本；關閉程式時背景回呼只能安靜結束。
- 進度回報採節流策略，例如每 `200~300 ms` 最多更新一次，並只保留最新狀態，避免大量 UI 訊息造成卡頓。

### 記憶體與資源所有權

- 大圖的權威結果以完整解析度 OpenCV `Mat` 保存；viewport 或 tile 只負責擷取可見區域，不得在 Paint 中重新分析整張圖。
- 每個 Bitmap、Mat、`LargeImageSource` 都要有清楚的 owner；誰建立、誰移交、誰在過期或例外時 Dispose 必須可追蹤。
- 共用的 `LargeImageSource` 要透過 reference count 或等價機制管理；背景工作使用期間不能提前釋放。
- cache 內的 Bitmap 不可直接交給多個執行緒繪製；必要時建立短期 clone，並在顯示工作完成後釋放。
- 不要為了取得顯示時間而建立整張巨大 GDI+ 預覽圖；大圖應只建立目前 viewport 所需的 overlay。
- 遇到 `failed to allocate` 時要明確回報記憶體配置失敗，不能偷偷改走精度或座標不一致的舊流程。

### 快取、儲存與相容性

- 快取鍵至少要包含圖片識別、ROI、來源 ID、方法、參數、順序與群組結構；任何影響結果的資料改變都必須使快取失效。
- 新增資料型別或 INI 欄位時，舊設定必須能載入；缺少的新欄位使用安全的空值或預設值。
- 載入設定後要補齊缺少或重複的 GUID，並檢查所有跨項目引用；無效引用應清空並在介面提示，不可保留半失效狀態。
- 關閉或換圖時要清除上一張圖片的 ROI、來源快取、處理結果、overlay 與 generation，防止新圖使用舊圖結果。

### 測試與拆分

- 每次 partial 拆分只移動一個責任範圍，先以 `rg` 搜尋所有呼叫者，再建置；行為確認後才能刪除主檔舊實作。
- 每次修改 OpenCV 流程都要測試小圖、一般圖與 `16384 x 50000` 大圖，至少涵蓋單一步驟、群組、關聯及區塊。
- UI 回歸測試要涵蓋：快速調參、套用/取消、切換項目、換圖、關閉程式、左右同步、縮放/平移、背景分頁切回前景、多選分組與刪除引用項目。
- 結果測試要確認來源圖正確、二值 mask 不缺線、紅色標記與底圖座標一致、群組輸出符合定義，並且重複點擊已完成項目不會重新運算。
- 建議補上 OpenCV mask 的 golden-image 測試、流程 ID/引用測試、快取失效測試與大圖記憶體峰值記錄。
