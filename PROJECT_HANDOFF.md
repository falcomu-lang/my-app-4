# 整合式影像處理軟體 Project Handoff

## 1. 專案與版本狀態

- 專案路徑：`C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\整合式影像處理軟件\my-app-4`
- GitHub：`https://github.com/falcomu-lang/my-app-4`
- 分支：`main`
- Solution：`MyApp4.sln`
- 主專案：`IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`
- 技術：C# WinForms、.NET Framework 4.7.2、OpenCvSharp
- 本次文件整理日期：2026-09-16

目前工作樹的程式已加入區塊處理 OpenCV 流程，並已完成 Debug / Any CPU 建置驗證。這一版的重點是把「影像關聯輸出的二值 mask」交給「整合成區塊」的後處理鏈，再顯示到固定的 `區塊處理` 分頁。

### 2026-09-16 最新整理

- Global Threshold 已接入 OpenCV，支援單一門檻與雙邊範圍門檻；Range 模式使用 `Cv2.InRange`。
- 區塊處理的顯示時間改為可見 `區塊處理` 分頁實際刷新後計時，不再以單純 `Invalidate` 的排程時間當作顯示時間；背景不可見分頁不強制刷新。
- 影像檢視狀態修正：處理開始前保存目前可見分頁的 Zoom/Offset，處理完成後不以其他分頁的舊狀態覆蓋目前畫面。
- `ImageDisplayControl` 在影像尺寸相同時，即使 Bitmap 與 `LargeImageSource` 互換，也保留目前縮放與平移位置。
- 前處理狀態只還原前處理控制項，影像處理狀態只還原處理後控制項，避免背景完成通知造成原圖或其他分頁跳位。
- 本次程式修改已通過 Debug / Any CPU 建置；提交前仍要以實際大圖手動確認快速切換、處理、縮放與平移。

## 2. 已完成的使用者功能

### 影像與 ROI

- 支援 BMP、JPG/JPEG、PNG、TIF/TIFF。
- 載入邊界統一成灰階；大型影像走 WIC 與 `LargeImageSource`，避免用巨大 GDI+ Bitmap 讀取。
- ROI 可新增、刪除、顯示全部、保存；座標以原始影像座標保存。
- 已保存的圖片與 ROI 會在啟動時還原，但啟動不自動執行影像處理或前處理結果。

### 顯示與操作

左右兩側固定分頁順序：

`原圖 -> 前處理 -> 處理後 -> 區塊處理 -> 區塊結果 -> debug`

兩側支援滾輪縮放、左鍵平移、重設視圖、共用 Zoom/Offset 同步，以及只更新目前可見內容的顯示策略。替換處理結果時必須保存並還原使用者目前的視圖，不得因處理或調參跳回 fit-to-view。

### 影像前處理

已拆至 `Forms\MainForm.Preprocessing.cs`，目前方法：

- Normalize
- Gaussian Blur
- CLAHE
- Median Blur
- Sharpen
- Bilateral Filter

前處理是在全影像灰階來源上用 OpenCV 執行。前處理步驟可排序、命名、刪除、Ctrl 多選及分組；群組內由上到下串接，每一步的輸出是下一步輸入。每個步驟或群組需明確選擇/處理，不能因啟動程式或單純切換分頁而自動大量重算。

### 影像處理與關聯

`Forms\MainForm.ImageProcessing.cs` 已包含 Canny、Sobel、Polarity 的實際 OpenCV mask 流程；`LegacyCreateOpenCvCannyMask`、`LegacyCreateOpenCvSobelMask`、`LegacyCreateOpenCvPolarityMask` 已從主檔移除。

目前 Edge Detect 參數：

- Canny：低/高門檻、核心大小 `3/5/7`、L2 梯度、高斯模糊大小、高斯 Sigma。
- Sobel：方向、核心大小 `1/3/5/7`、門檻。
- Polarity：極性、對比門檻、`CoreWidth`、平滑、高斯 Sigma、搜尋方向、邊界類型。

Threshold 的 `Global Threshold`、`Adaptive Threshold`、`Otsu Threshold` 已接入流程樹與 OpenCV pipeline。Global Threshold 支援 Single 與 Range 兩種模式；Range 使用下限/上限範圍產生二值 mask。

影像處理流程樹目前只保留兩個主分類：`Edge Detection` 與 `Threshold`。形態學、輪廓分析、特徵篩選與物件選擇不再放在影像處理流程樹，應分別由整合成區塊與後續判定物件流程負責；既有 OpenCV 實作與舊 INI 設定仍保留相容性，不在此次介面整理中刪除。

影像關聯以 GUID/ID 儲存，不以顯示名稱或索引作為鍵：

- 來源可為原始影像、單一前處理步驟或前處理群組。
- 目標可為單一影像處理步驟或影像處理群組。
- 群組關聯會將各子處理得到的二值 mask 以 OR 合併。
- 關聯若使用前處理輸出，演算法來源與 `處理後` 底圖必須是同一份前處理影像。
- 關聯支援處理、排序、命名、刪除與處理時間/結果快取；刪除被區塊引用的關聯時應保留引用檢查與警告。

## 3. 最新區塊處理功能

檔案：`Forms\MainForm.ObjectJudgement.cs`、`Forms\MainForm.ObjectJudgementProcessing.cs`。

左側主項目叫 `整合成區塊`，子項目叫 `區塊1`、`區塊2`。每個區塊可以選單一關聯或關聯群組，取得完整二值結果後，再依區塊內處理步驟由上到下執行。

區塊與區塊處理步驟的右鍵選單：

- 區塊：處理、上移、下移、命名、新增處理、刪除。
- 區塊處理步驟：處理、上移、下移、命名、刪除。

目前已用 OpenCvSharp 實作：

- `Dilate`、`Erode`、`Close`、`Open`：`Cv2.Dilate`、`Cv2.Erode`、`Cv2.MorphologyEx`。
- `Fill Contour`：`FindContours` 加 `DrawContours` 填滿外部輪廓。
- `Fill Hole`：padding 後 `FloodFill` 找背景，反轉後與原 mask 合併。
- `Connect gap / bridge`：以方向性 kernel 做 closing，可選 All/Horizontal/Vertical。
- `Morphological Reconstruction`：opening-by-reconstruction，反覆 dilate 並與原 mask AND，直到穩定。
- `Merge by distance`：以距離建立膨脹 kernel，再將相鄰外部輪廓合併。
- `Convex Hull`：逐輪廓 `Cv2.ConvexHull` 後填滿。

形態學方法參數是核心大小、迭代次數、形狀 `Rect/Ellipse/Cross`；bridge 是間隙大小與方向；merge 是距離。沒有額外作用的參數不要重新放回 UI。

區塊結果會以原圖/關聯來源影像為底，將區塊最後 binary mask 畫成紅色，顯示於左右兩側固定的 `區塊處理` 分頁。小圖使用背景 Task 產生 Bitmap；大圖使用 ROI 範圍的 OpenCV mask 與 viewport overlay，不建立整張區塊結果 GDI+ Bitmap。

### 3.1 物件定義入口

`整合成區塊` 下方已新增 `物件定義` 主項目，主項目前有物件圖示，右鍵可新增多個 `物件組定義`。目前物件組定義先保存固定 ID、顯示名稱與預留的區塊 ID 清單；尚未接入區塊選擇、合併方式與 E/D 等最終判定規則，不應自行假設為影像處理流程。

整合成區塊清單現在支援每個區塊獨立展開/收合處理步驟；多選兩個以上尚未分組的區塊後按右鍵可建立物件群組，也可多選兩個以上群組建立上層群組。群組與區塊均以 ID 保存，群組收合時隱藏子區塊與其處理步驟，避免清單佔用過多空間。群組刪除會保留區塊並解除群組歸屬。

## 4. 非同步、快取與資源生命週期

- OpenCV 前處理、Canny、Sobel、Polarity、關聯與區塊處理都應在 `Task.Run` 執行。
- UI 執行緒只接收完成結果、替換顯示來源及更新必要狀態。
- 每個流程都要用 generation、項目 ID、目前請求狀態確認結果是否仍有效；換圖片、換 ROI、換參數或切換項目後，舊結果不可覆蓋新結果。
- `LargeImageSource` 透過 reference count 管理共享來源；背景工作完成前不可釋放來源。
- cache-owned Bitmap 不可直接拿去繪製或由另一執行緒 Clone；繪製前使用短期 clone，完成後立即釋放。
- 不要在 Paint 中做 OpenCV 運算，不要每個 tile 重新建立 ROI mask。
- 顯示只針對可見分頁更新；背景分頁等切到前景時再依目前 generation 補齊。
- 狀態列不可每個 tile/ROI 都 `BeginInvoke`；只保留最新狀態並節流到約 200~300 ms。
- 選取已完成項目應讀取快取並觸發必要 repaint，不得因缺少 completion callback 而顯示空白。

處理時間要分開理解：ROI 準備時間、OpenCV 影像處理時間、可見區域預覽/overlay 顯示時間不是同一件事。前處理常需要建立記憶體型顯示來源，所以顯示時間可能比演算法時間長；處理後多半重用原圖 tile，因此顯示時間可能較短。區塊處理只有在區塊分頁可見並實際刷新時才計入顯示時間；不可見背景工作不應被誤報成畫面顯示時間。

## 5. 已知問題與曾經踩過的坑

1. 大圖不能回到舊的「每 tile 重新分析」流程，否則放大/平移會慢、紅線不完整，甚至看起來無止境處理。
2. 16384 x 50000 之類的全 ROI 單次 OpenCV 運算會同時配置多個大型 native Mat；`failed to allocate` 是實際配置失敗，不應用舊流程偷偷 fallback。
3. Canny 核心大小只接受 3、5、7；9 以上會造成不必要的高成本或不符合 Canny 介面。Sobel 的大核心是另一個方法的參數。
4. Polarity 的 `CoreWidth` 是 Sobel derivative aperture，不是要尋找的線寬。`Strongest`、`Longest`、最小邊緣長度、最大斷點間距應留在輪廓/特徵階段。
5. 前處理輸出與關聯來源若沒有共用同一個來源，會出現「演算法對淡圖、畫面底圖卻是深色原圖」的錯位視覺結果。
6. 切換項目時不能用 `latestPreprocessedImage != null` 猜來源；必須明確區分 Original、Preprocessing Step、Preprocessing Group。
7. 切換關聯時不能清空整個共用 parameter panel，否則其他項目的參數面板會消失。
8. `MainForm.Designer.cs` 的區塊處理分頁目前是固定設計器元件；不要又改回只靠執行期動態插入，否則分頁可能不出現。
9. 快速調參曾造成 GDI+ `ObjectDisposedException`、`InvalidOperationException`、`ArgumentException`；必須正確保留來源參考、clone 繪製 Bitmap 並在 UI 執行緒替換控制項。
10. 讀取新圖片時必須清除上一張圖的 ROI、前處理、關聯、mask、overlay 與 generation，避免拿舊圖片分析。
11. 影像處理或區塊處理時若發生視圖跳回 fit-to-view，先檢查是否切換了 Bitmap/`LargeImageSource` 類型，以及是否在處理前更新了 `sharedImageViewState`；不可直接在完成 callback 套用另一個分頁的 view state。
12. `InvalidateImageView()` 只代表排程重繪，不代表畫面已經完成；需要顯示時間時，必須對可見控制項使用同步刷新後再停止計時。

## 6. 設定保存

設定檔是執行檔旁的 `SystemParameters.ini`。目前重要區段：

- `[System]`：`LastImagePath`
- `[ROIs]` / 舊 `[ROI]`：多 ROI 與相容的單 ROI 設定
- `[ImagePreprocessing]`：步驟、群組、ID、方法與參數
- `[ImageProcessing]`：影像處理步驟、群組、ID、方法與參數
- `[ImageRelations]`：關聯來源/目標 Type 與固定 ID
- 區塊/區塊處理設定：由 `SystemParameterSettings` 對應的物件與步驟資料保存

名稱與順序只是 UI 呈現；關聯、快取與結果鍵必須用 ID 加上有效參數組合。改名或排序不可破壞既有關聯。

## 7. 檔案拆分現況

已完成：

- `MainForm.ImageProcessing.cs`：Canny、Sobel、Polarity OpenCV 實作與影像處理入口。
- `MainForm.Preprocessing.cs`：前處理結果型別、參數、群組、OpenCV 運算、Task、快取與顯示還原。
- `MainForm.Relation.cs`：關聯欄位、`RelationChoice` 與基礎資料。
- `MainForm.ObjectJudgement.cs`：整合成區塊 UI、關聯選取、右鍵操作、區塊處理分頁接線。
- `MainForm.ObjectJudgementProcessing.cs`：區塊處理參數、OpenCV 演算法、mask cache 與大圖 overlay。

仍集中在 `MainForm.cs` 或建議後續拆出：

1. `MainForm.Relation.cs`：關聯建立/套用、來源與目標下拉選單、清單更新、右鍵選單、執行、刪除引用警告。
2. `MainForm.Display.cs`：六個分頁、縮放/平移/重設、左右同步、全畫面切換與可見分頁更新。
3. `MainForm.Parameters.cs`：共用參數控制、Apply/Cancel、pending 值與時間狀態。
4. `MainForm.Roi.cs`：ROI 編輯、顯示全部、座標保存與 ROI 影像準備。
5. `MainForm.Cache.cs`：各種 cache、generation、取消/淘汰與 Bitmap/Mat ownership。
6. `Services/OpenCvProcessingService.cs`：partial 拆分完成且行為穩定後，再抽離不依賴 UI 的純 OpenCV 函數。

拆分守則：先用 partial 保持行為不變；每一批移動後建置；用 `rg` 確認參考；最後才刪除重複方法與 using。`MainForm.Designer.cs` 要保留可由 Visual Studio Designer 開啟的狀態。

## 8. 尚未完成/下一步

- 完整驗證 Threshold 三種方法的 OpenCV mask、參數面板與大圖顯示。
- 以實際 16384 x 50000 圖片測試多 ROI、多關聯與多區塊的記憶體峰值。
- 補上區塊處理每一步結果的獨立快取顯示，確認重複點選不會重算；目前整個區塊鏈的處理與可見顯示計時已完成。
- 以實際操作確認各分頁在執行單一步驟、群組、關聯與區塊處理時，Zoom/Offset 均維持不變。
- 補上關聯被刪除、來源失效、區塊引用失效時的使用者警告。
- 完成 E/D 等最終物件判定方法；目前區塊處理主要是二值 mask 結合與形態學後處理。
- 完成剩餘 partial 拆分後，再評估純 OpenCV service 化。
- 目前沒有完整自動化 UI/影像 golden-image 測試，正式交付前需要手動驗證縮放、平移、換圖、快速調參與取消流程。

## 9. 建置驗證

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' .\MyApp4.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

本次文件整理前已成功建置。不要提交臨時 `codex-build`、`bin` 或 `obj` 輸出；提交前確認執行中的 `IntegratedImageProcessingApp.exe` 已關閉。
