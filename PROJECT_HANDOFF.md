# 整合式影像處理軟體 Project Handoff

## 1. 專案與版本狀態

- 專案路徑：`C:\Users\falcomu\Documents\Codex\程式撰寫 專案資料夾\整合式影像處理軟件\my-app-4`
- GitHub：`https://github.com/falcomu-lang/my-app-4`
- 分支：`main`
- Solution：`MyApp4.sln`
- 主專案：`IntegratedImageProcessingApp\IntegratedImageProcessingApp.csproj`
- 技術：C# WinForms、.NET Framework 4.7.2、OpenCvSharp
- 本次文件整理日期：2026-09-22

目前工作樹的程式已加入區塊處理 OpenCV 流程，並已完成 Debug / Any CPU 建置驗證。這一版的重點是把「影像關聯輸出的二值 mask」交給「整合成區塊」的後處理鏈，再顯示到固定的 `區塊處理` 分頁。

## 1.1 本次進度摘要（2026-09-22）

### 已完成：尺寸良品判斷條件的設定層

檢測參數現在有獨立的 `尺寸良品判斷條件` 分頁。規則是掛在單一檢測參數底下，不是所有檢測參數共用同一份清單。這個分頁目前屬於「規則編輯與保存層」，尚未宣稱已完成最終良品判定。

已完成的 UI 行為：

- 可新增、修改、清除、上移、下移、刪除及保存判斷規則。
- 可設定規則名稱、啟用狀態，以及 `A 規`、`B 規` 各自的計算式與規格式。
- 表格顯示規則編號、名稱、啟用狀態與 A/B 規內容。
- 規則右鍵選單可修改、上移、下移、刪除；清單排序不會改變規則 ID。
- 計算式會先做基本格式驗證；支援量測紀錄編號參照，例如 `(1)`、`(2)`，以及基本四則運算、`min`、`max`。
- 規格式可表達單值、上下限與比較條件；計算式與規格式必須成對，錯誤內容不會直接保存。

已完成的資料模型與保存：

- `ObjectDetectionGoodJudgementRuleSettings` 保存 `Id`、`Number`、`Name`、`Enabled`、`CalculationExpression`、`SpecificationExpression`、`AlternativeCalculationExpression`、`AlternativeSpecificationExpression`。
- `ObjectDetectionParameterSettings.GoodJudgementRules` 保存每個檢測參數自己的規則集合。
- `SystemParameters.ini` 使用 `[ObjectDetection]` 下的 `ParameterN.GoodJudgementRuleCount` 與 `ParameterN.GoodJudgementRuleK.*` 欄位保存規則。
- 載入舊設定時會補齊缺少或重複的 GUID 與編號，避免舊檔因新增欄位而無法使用。
- 顯示名稱與編號只是使用者介面資訊；規則、量測紀錄與流程引用仍必須以唯一 ID 為準。

主要程式位置：

- `IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.cs`：規則分頁、表格、右鍵選單、輸入驗證與套用保存流程。
- `IntegratedImageProcessingApp/Forms/MainForm.cs`：檢測參數分頁切換與畫面生命週期接線。
- `IntegratedImageProcessingApp/Services/SystemParameterSettings.cs`：規則資料模型與檢測參數設定。
- `IntegratedImageProcessingApp/Services/SystemParameterIniService.cs`：規則 ID、編號與內容的 INI 讀寫及舊資料補齊。

### 尚未完成的邊界

目前還沒有把規則編輯器直接接到實際量測數值的評估引擎。因此目前可以「建立、驗證、保存、重新載入」規則，但尚不能把規則自動轉成最終 `A規`、`B規`、`C規` 或 `不可判斷` 結果。後續應另做評估服務，不要把評估邏輯塞回影像處理或量測線繪製事件。

建議的後續資料流：

```text
已完成物件定義
  -> 已完成尺寸量測結果快照（量測紀錄 ID/編號 -> 數值）
  -> 解析啟用的尺寸良品判斷條件
  -> 產生每條規則的通過、未通過或不可判斷
  -> 寫入參數結果確認
```

評估時必須遵守以下規則：

- 只讀取已完成且版本相符的量測快取，不因切換分頁、選取規則或改變顯示視圖重新執行影像流程。
- 量測紀錄引用應以穩定 ID 保存；編號只作為使用者可讀的參照。刪除或失效時要顯示警告，不可靜默改綁另一筆量測資料。
- 評估結果要保存使用的規則 ID、量測紀錄 ID、來源 MASK ID/處理 ID 與評估時間，方便追溯。
- 若資料不足、規格式無法解析或引用的量測紀錄不存在，結果應是 `不可判斷`，不能當成良品或不良品。

### 2026-09-16 最新整理

- Global Threshold 已接入 OpenCV，支援單一門檻與雙邊範圍門檻；Range 模式使用 `Cv2.InRange`。
- 區塊處理的顯示時間改為可見 `區塊處理` 分頁實際刷新後計時，不再以單純 `Invalidate` 的排程時間當作顯示時間；背景不可見分頁不強制刷新。
- 影像檢視狀態修正：處理開始前保存目前可見分頁的 Zoom/Offset，處理完成後不以其他分頁的舊狀態覆蓋目前畫面。
- `ImageDisplayControl` 在影像尺寸相同時，即使 Bitmap 與 `LargeImageSource` 互換，也保留目前縮放與平移位置。
- 前處理狀態只還原前處理控制項，影像處理狀態只還原處理後控制項，避免背景完成通知造成原圖或其他分頁跳位。
- 本次程式修改已通過 Debug / Any CPU 建置；提交前仍要以實際大圖手動確認快速切換、處理、縮放與平移。

### 2026-09-17 最新進度

- `物件定義` 已從 placeholder 擴充為可操作的物件組清單。
- 新增物件組預設名稱為 `物件組1`、`物件組2`；舊版精確的預設名稱 `物件組定義N` 會在啟動時轉為新名稱，使用者自訂名稱不會被覆蓋。
- 選取物件組後，右側顯示「來源區塊」下拉選單；按「確認」後，來源區塊 ID 保存至物件組。
- 物件組右鍵選單包含：處理、上移、下移、命名、新增處理、刪除。
- 物件組下可新增 `處理1`、`處理2` 等子項目；子項目右鍵選單包含：處理、上移、下移、命名、刪除。
- 物件組與其處理子項目均使用 GUID；清單重建、改名、排序後，操作仍透過 ID 找回資料，不依賴顯示名稱。
- `SystemParameters.ini` 已支援物件組來源區塊 ID，以及子處理的 ID、名稱、方法與參數保存/載入；舊 INI 沒有子處理欄位時會以空清單載入。
- 統一處理時間的顯示規則：影像處理全部時間只加總演算法階段，不包含顯示時間；整合成區塊會另外列出區塊處理時間。
- 本次程式與文件修改完成後，必須重新建置並確認執行中的 `IntegratedImageProcessingApp.exe` 已關閉，再提交到 GitHub。

### 2026-09-18 最新進度

- 物件組來源現在支援兩種明確模式：`來源區塊` 與 `來源影像關聯`。來源區塊具有優先權；只有來源區塊未指定時，才使用來源影像關聯或關聯群組。
- `ObjectDefinitionSettings` 已保存 `SourceRelationType` 與 `SourceRelationId`；設定檔讀寫、來源驗證、處理簽名與來源 mask 建立均以唯一 ID 為準。
- 物件組右側在來源區塊已設定時會將來源影像關聯選單反灰，避免兩個來源同時生效造成誤解。
- 修正物件組右鍵「處理」入口仍只檢查 `SourceId` 的問題。現在使用影像關聯作為來源時，右鍵處理也會正常進入物件組流程。
- 物件組執行會先啟動其依賴流程：前處理、處理後、整合區塊處理，最後才更新物件結果。這些中間結果會保留在各自分頁，方便逐段確認來源與 mask。
- 物件組的明確執行會對未選取的處理後預覽控制項做一次全流程結果準備；一般單獨執行影像處理仍維持只更新可見分頁的效能策略。
- 大圖路徑仍透過 `LargeImageSource` 與 OpenCV mask/overlay 準備所有依賴預覽，小圖路徑會同步更新左右兩側處理後 Bitmap；兩條路徑都使用 `preserveView` 保留目前 Zoom/Offset。
- 最新驗證建置成功，輸出在 `IntegratedImageProcessingApp/bin/DebugVerify/IntegratedImageProcessingApp.exe`。若 Visual Studio 或舊版 EXE 尚未關閉，請勿以舊輸出判斷本次行為。

### 2026-09-18 顯示效能與前景資源小結

- 目前已將「影像演算法」與「畫面顯示」分開處理：OpenCV 前處理、影像處理、關聯、區塊與物件組流程可以在背景執行；平移、縮放、tile 預讀與 overlay 繪製則只服務目前前景分頁。
- 左右畫面共用同一個 `Zoom / Offset`，但不在同一個滑鼠事件中強迫兩側同步 Paint。現在由左側作為視覺優先畫面，右側約錯開一個 `16 ms` 更新週期；從右側拖曳時，也會先把位置套用到左側，再讓右側完成後續繪製。
- 平移期間使用 `Bilinear`，停止平移後再回到高品質插值；這是降低繪製成本，不是把影像縮成過低解析度。紅點、紅框、黃框等 overlay 不應改變座標，只在適當的畫面階段繪製。
- 目前前景分頁停止拖曳或縮放約 `120 ms` 後，會依目前倍率向可見範圍的上下左右及四個角落延伸 `4` 個 Tile 做背景預讀；預讀最多排入 `256` 個外圍 Tile，且不會在拖曳過程中大量解碼。
- `LargeImageSource` 的顯示快取上限仍維持 `384` 個 Tile。曾嘗試把快取擴大並在拖曳中預讀大量 Tile，但實測沒有改善且可能增加解碼與 UI 資源競爭，已撤回；不要把那個實驗版本誤當成目前設計。
- 背景分頁不應因左右同步、平移、縮放或外圍 Tile 預讀而持續消耗 UI 資源。切換到其他分頁時，才將共用 `Zoom / Offset` 套用到新的前景控制項，再由新的前景控制項準備自己的預讀範圍。
- `ImageDisplayControl` 會檢查控制項從自身到父層的有效可見性；不可見分頁即使收到處理結果，也不啟動 viewport 預讀。這不會阻止 OpenCV 背景分析，因為分析結果仍需保留給使用者切到前景時查看。
- 目前建議用 `16384 x 50000` 圖片，在 `0.03x`、`0.05x`、`0.06x` 及斜向拖曳下測試：先觀察第一次離開快取範圍的反應，再觀察停止拖曳後 120 ms 是否能讓附近區域變順；同時留意記憶體峰值與是否出現 GDI+/WIC 例外。

### 2026-09-20 上傳前整理小結

- 本地工作樹目前包含物件組來源優先權、物件組依賴預覽、OpenCV 影像流程、顯示同步與大圖 Tile 預讀等尚未上傳的變更；上一個 `origin/main` 尚未包含這一批完整修改。
- 本次要提交的顯示策略是保守版本：前景分頁才做 viewport 預讀，停止操作後延遲約 `120 ms`，外圍四個方向及四個角落延伸 `4` 個 Tile，最多排入 `256` 個預讀工作；整體 Tile 快取仍是 `384` 個。
- 曾試過拖曳中大量預讀、擴大整體 Tile 快取與一次準備更廣區域，但實測沒有穩定改善且可能讓 WIC 解碼和 UI Paint 競爭資源；該方案已撤回，不能與目前的延遲外圍預讀混用。
- 目前左右同步是「左側優先、右側錯開約 `16 ms`」，背景分頁不參與平移/縮放與預讀；切換分頁時才套用共用 `Zoom / Offset`。背景 OpenCV 分析仍可繼續，這兩件事不可混為一談。
- 上傳前建置目標為 `Debug / Any CPU`，輸出使用 `IntegratedImageProcessingApp/bin/DebugVerify/IntegratedImageProcessingApp.exe`。測試時必須先關閉舊版 EXE，確認執行的是本次建置結果。
- 上傳後若要追查平移效能，應分別比較前景原圖、前景帶 overlay 的處理結果，以及背景分頁切換後的第一次預讀；不要只用影像處理時間判斷畫面是否順暢。

### 2026-09-21 最新進度：檢測 MASK 與區塊子處理

- `檢測參數設定 -> 尺寸量測設定` 的 MASK 下拉選單現在只收集目前物件定義實際使用的來源，不會列出其他無關區塊或關聯。
- 直接指定區塊時，選單順序固定為「主要區塊 -> 該區塊的處理1、處理2、... -> 該區塊的影像關聯與影像處理來源」。因此畫面上的 `填滿整個區域` 會與它的 `處理1`、`處理2` 放在一起。
- 來源為區塊群組時，會遞迴列出來源群組、子群組與子區塊；每個子區塊自己的關聯、影像處理群組與群組子步驟也會一起列入。
- 新增 `ObjectJudgementProcessing` 選項與來源 namespace。`處理1` 對應處理1的累積結果；`處理2` 對應處理1再處理2的累積結果，不能回退使用原圖。
- 物件定義、物件區塊群組與單獨區塊處理在建立大圖 ROI mask 時，會保存每個已完成子處理的中間 MASK。相同 ID、ROI、參數組合的舊快取會先 Dispose，再放入新快取，避免反覆測試時 native memory 累積。
- 尺寸量測預覽會從相同的中間 MASK 快取產生粉紅色 overlay；綠色框仍只代表選定物件 ROI，不再用綠色半透明遮住 MASK。
- 若只更新了選單或剛切換到新 EXE，舊執行期間沒有建立中間 MASK；測試前需重新執行來源區塊或物件定義一次，再按「套用來源 MASK」。
- 本次程式修改已通過 `Debug / Any CPU` 建置；文件與目前的選單排序修正會一併提交至 `origin/main`。

#### 檢測 MASK 的資料流對照

檢測參數的尺寸量測只應使用目前物件定義可追溯到的來源。來源解析順序與意義如下：

| 選單來源 | ID 對應 | 實際使用的資料 |
| --- | --- | --- |
| 物件定義結果 | `ObjectDefinition` + 物件定義 ID | 物件定義完成 CCL、面積篩選、合併與編號後的結果；用於限制目前物件 ROI |
| 區塊群組 | `ObjectJudgementGroup` + 群組 ID | 群組內所有區塊結果 OR 合併後的 binary mask |
| 區塊 | `ObjectJudgement` + 區塊 ID | 該區塊完整處理鏈最後產生的 binary mask |
| 區塊／處理N | `ObjectJudgementProcessing` + 處理 ID + 區塊 ID namespace | 從第一個處理累積到第 N 個處理的 binary mask |
| 關聯 | `Relation` + 關聯 ID | 該關聯輸出的 binary mask |
| 影像處理群組 | `ImageProcessingGroup` + 群組 ID + 關聯 namespace | 關聯中影像處理群組的合併 mask |
| 影像處理群組／處理N | `ImageProcessingGroupStep` + 步驟 ID + 關聯 namespace | 該關聯流程中的指定影像處理步驟 mask |

直接區塊來源的選單必須保持以下順序：

```text
區塊：主要區塊
區塊：主要區塊／處理1
區塊：主要區塊／處理2
關聯：該區塊的來源關聯
影像處理：該關聯的來源步驟
```

區塊群組來源則要遞迴處理 `ObjectJudgementGroupSettings.ParentGroupId`，將子群組和子區塊列入同一個來源集合；每個區塊的子處理要緊跟在該區塊下方，不能被其他區塊或關聯插入中間。

#### 中間 MASK 快取規則

物件定義或區塊處理執行時，區塊處理鏈會依序產生：

```text
base mask -> 處理1 -> 處理1 + 處理2 -> ... -> 最終區塊 mask
```

每一個已完成的累積結果都會以 `CreateObjectJudgementMaskKey` 建立快取鍵，鍵值包含區塊 ID、ROI 座標與尺寸、處理順序、方法及參數。寫入相同鍵前會先 Dispose 舊的 `Cv.Mat`，確保快速重跑或修改參數時不會持續累積 native memory。

量測預覽不應重新從原圖推導處理1/處理2，也不能因為找不到中間快取就靜默改用最後結果或原圖。找不到時應保持待顯示狀態，並要求先重新執行來源流程。

目前這組中間處理 MASK 主要由大圖 `LargeImageSource` 路徑保存與取用；小圖 Bitmap 路徑目前仍沒有獨立的每一步 `ObjectJudgementProcessing` 快取，若要讓小圖也能逐步選取與顯示，後續需要補上 Bitmap 對應的中間 mask cache，而不是在顯示事件中重新運算。

#### 公司測試清單

1. 關閉舊版 EXE，取得最新 `main` 後重新建置。
2. 載入測試圖片，確認 ROI、影像關聯、區塊與物件定義的套用狀態。
3. 執行來源區塊或物件定義一次，確認每個來源流程完成且沒有 `來源 MASK 尚未建立`。
4. 開啟 `檢測參數設定 -> 尺寸量測設定`，檢查直接區塊底下是否依序看到區塊、處理1、處理2。
5. 選處理1並套用，確認待量測畫面出現粉紅 MASK 與綠色 ROI 外框。
6. 選處理2並套用，確認顯示的是處理1再接處理2的結果。
7. 切換物件序號1至6，確認 MASK 依物件 ROI 移動，沒有殘留上一個序號。
8. 修改處理參數後重新執行，確認舊 MASK 不會覆蓋新結果，且記憶體不會無限制增加。
9. 在 `0.03x`、`0.05x`、`0.06x` 及不同平移位置檢查，確認粉紅 overlay、綠色框與目前 Zoom/Offset 都維持正確。

### 2026-09-22 最新進度：尺寸量測紀錄與像素運算

- `檢測參數設定 -> 尺寸量測設定` 的量測資料表現在支援多筆紀錄；每筆量測資料都有獨立 GUID，不再因第二次套用而覆蓋第一筆。
- 按「開始畫線」會進入新增紀錄模式。完成線段並按「套用量測設定」後，會將新資料追加到 `MeasurementRecords`。
- 點選表格中的既有紀錄會載入該筆資料並進入編輯該筆的狀態；之後重新套用會更新該筆，而不是另開一筆。再次按「開始畫線」會回到新增模式。
- 表格右鍵選單使用滑鼠的螢幕座標顯示，選單位置在游標右下方約 `8 px`，避免選單出現在表格內固定位置而遮住目前操作位置。
- 右鍵選單目前包含：
  - `運算`：使用該筆紀錄保存的物件、ROI 座標、方向、線段模式、線數與來源 MASK ID 執行計算。
  - `刪除`：刪除指定 GUID 的紀錄；若該筆就是目前畫面使用的線段，會同時清除參數中的線段設定、畫面線段與待量測 overlay，其他紀錄不受影響。
- 左鍵點擊量測紀錄會載入該筆線段，並讓目前量測位置以黃色線/黃色區域閃爍約 `180 ms`，方便在多筆紀錄中辨識位置。
- `運算`目前先以像素為單位：從每條量測線的起點開始取樣，先略過起點前的背景，遇到第一個前景後計算第一段連續前景長度；不會把後續中斷後的其他物件長度接在一起。
- 單線模式會得到一個像素長度，並以相同數值顯示最小、平均、最大；平行線模式會依保存的線數在兩條基準線之間插值，逐條計算第一段連續物件長度，再顯示：

```text
最小：xx px
平均：xx px
最大：xx px
```

- 運算結果目前以游標右下方的提示框顯示，並同步寫入狀態列；目前尚未轉換成毫米等實體單位，也尚未把這次統計值另存回參數檔。
- 量測線仍保存物件 ROI 內的正規化座標、方向、繪製順序、ROI 外端點、平行線數量、旋轉座標框架與來源 MASK 的 `SourceType/Id/Namespace/Operation`。改名或排序不會改變量測紀錄引用的來源。
- 本次程式修改集中於 `IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.cs`，已完成 `Debug / Any CPU` 建置驗證；輸出使用 `IntegratedImageProcessingApp/bin/DebugVerify/IntegratedImageProcessingApp.exe`。

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

`整合成區塊` 下方已新增 `物件定義` 主項目，主項目前有物件圖示。右鍵可新增多個 `物件組`；物件組選取後，右側可選擇一個來源區塊，或在來源區塊未指定時選擇來源影像關聯/關聯群組，再按 `確認` 保存。來源區塊優先，來源關聯只在來源區塊為空時生效；所有引用都以唯一 GUID/ID 保存。

物件組右鍵功能：

- `處理`：依來源區塊或來源影像關聯建立 OpenCV binary mask，接著執行 CCL、面積篩選、距離合併、排序與編號結果顯示。
- `上移`、`下移`：只改變物件組在清單中的順序，不改變 ID。
- `命名`：只改變 DisplayName，不改變 ID，也不會破壞來源。
- `新增處理`：建立具獨立 GUID 的 `處理N` 子項目，並自動展開物件組。
- `刪除`：刪除物件組及其所屬子處理。

物件組處理子項目目前也有自己的唯一 ID、顯示名稱、方法與參數欄位，並可排序、命名及刪除。子處理的實際方法與參數面板仍要等物件組判定規格確認後接入，不應先把區塊形態學方法直接當成物件判定方法。

物件組目前已完成的影像流程：

- OpenCV CCL 連通元件分析。
- 最小/最大物件面積篩選。
- `Merge by distance` 後的群組面積篩選。
- 合併後的由上到下、由左到右編號排序。
- 黃色框線與編號文字繪製到物件結果分頁。

整合成區塊清單現在支援每個區塊獨立展開/收合處理步驟；多選兩個以上尚未分組的區塊後按右鍵可建立物件群組，也可多選兩個以上群組建立上層群組。群組與區塊均以 ID 保存，群組收合時隱藏子區塊與其處理步驟，避免清單佔用過多空間。群組刪除會保留區塊並解除群組歸屬。

### 3.2 物件組執行時的依賴預覽

使用者明確對物件組選擇「處理」時，預期流程如下：

1. 解析物件組來源；有來源區塊時使用區塊，否則使用來源影像關聯或關聯群組。
2. 啟動來源影像關聯的前處理與 `處理後` 預覽。
3. 啟動來源區塊的完整區塊處理鏈，並更新 `區塊處理` 預覽。
4. 物件組再以來源 binary mask 執行 CCL、合併、面積篩選與編號。
5. 將黃色框線與編號寫入 `區塊結果`，並保留前面各階段的預覽供切換檢查。

這是物件組的明確除錯執行，不代表程式啟動或單純切換項目就會自動跑完整流程。一般單獨執行影像處理仍以目前可見分頁優先；物件組執行時，小圖會額外準備未選取的 `處理後` 控制項，大圖則使用共享 `LargeImageSource` 與 ROI overlay。所有結果更新都要使用目前來源/請求版本，舊請求不可覆蓋新畫面。

若測試時只有最後的 `區塊結果` 出現，而前處理、處理後或區塊處理為空，優先檢查：來源是否按下「套用」、關聯是否有影像處理項目、目前執行的 EXE 是否為最新建置，以及是否勾選了「不顯示畫面」。

## 4. 非同步、快取與資源生命週期

- OpenCV 前處理、Canny、Sobel、Polarity、關聯與區塊處理都應在 `Task.Run` 執行。
- UI 執行緒只接收完成結果、替換顯示來源及更新必要狀態。
- 每個流程都要用 generation、項目 ID、目前請求狀態確認結果是否仍有效；換圖片、換 ROI、換參數或切換項目後，舊結果不可覆蓋新結果。
- `LargeImageSource` 透過 reference count 管理共享來源；背景工作完成前不可釋放來源。
- cache-owned Bitmap 不可直接拿去繪製或由另一執行緒 Clone；繪製前使用短期 clone，完成後立即釋放。
- 不要在 Paint 中做 OpenCV 運算，不要每個 tile 重新建立 ROI mask。
- 一般處理只針對可見分頁更新；背景分頁等切到前景時再依目前 generation 補齊。例外是使用者明確執行物件組時，會一次準備該依賴鏈的前處理、處理後、區塊處理與物件結果預覽，方便除錯。
- 狀態列不可每個 tile/ROI 都 `BeginInvoke`；只保留最新狀態並節流到約 200~300 ms。
- 選取已完成項目應讀取快取並觸發必要 repaint，不得因缺少 completion callback 而顯示空白。

處理時間要分開理解：ROI 準備時間、OpenCV 影像處理時間、可見區域預覽/overlay 顯示時間不是同一件事。前處理常需要建立記憶體型顯示來源，所以顯示時間可能比演算法時間長；處理後多半重用原圖 tile，因此顯示時間可能較短。區塊處理只有在區塊分頁可見並實際刷新時才計入顯示時間；不可見背景工作不應被誤報成畫面顯示時間。

目前狀態列時間格式：

- 影像處理或關聯：`影像處理全部時間：xx ms || 影像前處理時間：xx ms || 影像處理時間：xx ms || 顯示時間：xx ms`
- 整合成區塊：`影像處理全部時間：xx ms || 影像前處理時間：xx ms || 影像處理時間：xx ms || 整合成區塊處理：xx ms || 顯示時間：xx ms`
- 影像處理全部時間 = 前處理 + 影像處理；整合成區塊時再加上區塊處理。
- 顯示時間永遠不加入影像處理全部時間，只代表可見結果實際更新的時間。
- 本次沒有重新執行的快取階段顯示 `0 ms`；背景分頁尚未顯示時，不應把尚未發生的顯示工作算入演算法時間。

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
13. 檢測 MASK 的區塊子處理選項必須對應已保存的累積 mask cache；大圖路徑已保存每一步，Bitmap 小圖路徑目前沒有獨立的每一步 cache，不能在 Paint 事件中偷偷重跑。

## 6. 設定保存

設定檔是執行檔旁的 `SystemParameters.ini`。目前重要區段：

- `[System]`：`LastImagePath`
- `[ROIs]` / 舊 `[ROI]`：多 ROI 與相容的單 ROI 設定
- `[ImagePreprocessing]`：步驟、群組、ID、方法與參數
- `[ImageProcessing]`：影像處理步驟、群組、ID、方法與參數
- `[ImageRelations]`：關聯來源/目標 Type 與固定 ID
- 區塊/區塊處理設定：由 `SystemParameterSettings` 對應的物件與步驟資料保存
- 物件組來源關聯：`SourceRelationType`、`SourceRelationId`；來源區塊已指定時，這兩個欄位不參與實際來源解析。

名稱與順序只是 UI 呈現；關聯、快取與結果鍵必須用 ID 加上有效參數組合。改名或排序不可破壞既有關聯。

## 7. 檔案拆分現況

已完成：

- `MainForm.ImageProcessing.cs`：Canny、Sobel、Polarity OpenCV 實作與影像處理入口。
- `MainForm.Preprocessing.cs`：前處理結果型別、參數、群組、OpenCV 運算、Task、快取與顯示還原。
- `MainForm.Relation.cs`：關聯欄位、`RelationChoice` 與基礎資料。
- `MainForm.ObjectJudgement.cs`：整合成區塊 UI、關聯選取、右鍵操作、區塊處理分頁接線。
- `MainForm.ObjectJudgementProcessing.cs`：區塊處理參數、OpenCV 演算法、mask cache 與大圖 overlay。
- `MainForm.ObjectDefinition.cs`：物件組清單、來源區塊/來源影像關聯選擇、唯一 ID 對應、右鍵選單與物件組處理子項目。
- `MainForm.ObjectDefinitionProcessing.cs`：物件組來源 mask、CCL、面積篩選、Merge by Distance、編號、黃色框線/文字結果，以及物件組依賴預覽啟動。

仍集中在 `MainForm.cs` 或建議後續拆出：

1. `MainForm.Relation.cs`：關聯建立/套用、來源與目標下拉選單、清單更新、右鍵選單、執行、刪除引用警告。
2. `MainForm.Display.cs`：六個分頁、縮放/平移/重設、左右同步、全畫面切換與可見分頁更新。
3. `MainForm.Parameters.cs`：共用參數控制、Apply/Cancel、pending 值與時間狀態。
4. `MainForm.Roi.cs`：ROI 編輯、顯示全部、座標保存與 ROI 影像準備。
5. `MainForm.Cache.cs`：各種 cache、generation、取消/淘汰與 Bitmap/Mat ownership。
6. `MainForm.ObjectDefinitionProcessing.cs`：仍可再拆出物件組子處理參數與後續判定演算法；目前來源 mask、CCL、編號與結果顯示已在此檔案。
7. `Services/OpenCvProcessingService.cs`：partial 拆分完成且行為穩定後，再抽離不依賴 UI 的純 OpenCV 函數。

拆分守則：先用 partial 保持行為不變；每一批移動後建置；用 `rg` 確認參考；最後才刪除重複方法與 using。`MainForm.Designer.cs` 要保留可由 Visual Studio Designer 開啟的狀態。

## 7.1 介面設計注意事項

這是大型影像、長時間運算工具，介面的首要目標是讓使用者知道目前選到的流程、來源影像、執行狀態與結果是否已更新。所有後續 UI 修改都要遵守以下原則。

### 清單層級與操作

- 主項目是分類；子項目才是可設定或可執行的流程。新增的流程要放在正確父項目下，不能把處理項目平鋪到錯誤層級。
- 普通左鍵只負責選取或展開/收合；按 `Ctrl`/`Shift` 多選時不得展開/收合，也不得在中途重建清單而清除選取。
- 右鍵只顯示該類型適用的命令。物件組目前應顯示處理、上移、下移、命名、新增處理、刪除；物件組子處理應顯示處理、上移、下移、命名、刪除。
- `A`/`a` 才是明確執行快捷鍵；單純左鍵選取不能觸發昂貴運算。已完成且設定未變的項目再次執行時應顯示 `已處理`，不應重算。
- 清單的文字是顯示用途，不能作為關聯鍵。所有清單項目、父子關係與跨流程引用都要依 GUID/ID 找資料。
- 改名或排序後，要保留原本的 ID、參數、來源與子項目；不得用新的順序索引重新建立引用。
- 清單刷新應盡量局部更新；重建前要保存選取，重建時要阻擋暫時性的 SelectedIndexChanged，避免右側參數面板閃爍或誤觸發處理。

### 參數與狀態

- 使用者修改數字或選項時只更新 pending 值；按「套用」才寫入設定並開始處理，按「取消」要還原修改前值。
- 套用或取消不能改變使用者目前的 Zoom、Offset、可見分頁與左右對位。
- 右側標題必須帶有目前項目名稱與方法；參數控制項只顯示該方法實際使用的設定，不要保留沒有作用的舊參數。
- 狀態列必須區分 `影像處理中`、`產生預覽圖中`、`完成`、`已處理`、`待顯示`、`失敗`。完成後不可因晚到的舊回呼又改回「處理中」。
- 時間要分階段顯示：前處理、影像處理、整合成區塊、顯示。`影像處理全部時間` 是演算法階段加總，永遠不包含顯示時間。
- 背景分頁尚未真正顯示時，顯示時間不能假報為完成；切到前景並完成刷新後才補上實際顯示時間。

### 影像檢視與同步

- 原圖、前處理、處理後、區塊處理、區塊結果與 debug 分頁要維持一致的縮放、平移、重設視圖體驗。
- 處理前要保存目前真正可見控制項的 view state；完成回呼只能還原對應分頁的 state，不可拿背景分頁或另一側的舊 state 覆蓋目前畫面。
- 左右同步只處理目前正在顯示的分頁；背景控制項不應因同步而持續重繪。背景切到前景時才更新並重新對位。
- Bitmap 與 `LargeImageSource` 互換時，如果影像尺寸和座標系相同，必須保留 Zoom/Offset，不能自動回到 fit-to-view。
- 關聯若使用前處理影像，處理後底圖與分析資料都必須來自同一份前處理輸出；紅線/紅點不得畫在另一個來源上。

## 7.2 軟體設計與維護注意事項

### 流程邊界與來源正確性

- 維持單向資料流：`原圖 -> 前處理 -> Edge/Threshold -> 影像關聯 -> 整合成區塊 -> 物件組`。
- 每個執行請求都要帶來源類型與來源 ID。不能因為來源快取不存在就默默改用原圖，也不能用顯示名稱猜來源。
- 前處理群組是影像串接；影像處理群組依需求合併二值結果；區塊處理是上一個輸出交給下一個。這三種群組語意要維持分離。
- 所有已實作影像演算法都走 OpenCvSharp；Legacy 程式碼不能在錯誤時靜默 fallback。相容舊設定應在載入/遷移階段處理並可追蹤。

### 非同步、取消與版本

- OpenCV、ROI、大圖與區塊處理應在 `Task.Run` 或背景流程執行；UI 執行緒只處理控制項、狀態與結果交換。
- 每次換圖、換 ROI、換來源、改參數或改流程都增加 generation/request version。背景完成時先驗證版本與 ID，過期結果直接釋放。
- 新請求要取消或標記舊請求失效，避免多個請求同時寫入同一個結果欄位、Bitmap、Mat 或狀態列。
- `BeginInvoke` 回 UI 前要檢查 Form/Control 尚未 Dispose，且請求仍是目前請求；關閉程式時晚到回呼只能結束，不能再更新 UI。
- 進度訊息要節流到約 `200~300 ms`，只保留最新狀態，不可每個 tile 或 ROI 都呼叫 `BeginInvoke`。

### Mat、Bitmap 與大型影像記憶體

- 大圖的權威結果使用完整解析度 OpenCV `Mat`；Paint 與 tile 只建立目前 viewport 的顯示 overlay，不可在繪圖事件中重新分析。
- 每個 Mat、Bitmap、`LargeImageSource` 都要有清楚所有權；建立、移交、過期、例外與關閉時的 Dispose 路徑要一致。
- 共用 `LargeImageSource` 要在背景工作使用期間持有 reference；cache 內 Bitmap 不直接交給多執行緒繪製，必要時使用短期 clone。
- `failed to allocate` 要明確當作 native memory 配置失敗處理，不能改走精度或座標不一致的舊備援流程。
- 16384 x 50000 影像要測量 native Mat 峰值；48 GB 記憶體不代表每個運算都能無限制建立多份完整影像複本。

### 設定、引用與快取

- INI 內的名稱、順序、方法、參數與 ID 分開保存；所有跨項目引用使用 GUID。
- 新增欄位要能載入舊 INI，缺少欄位使用安全預設；載入後補齊空白/重複 ID，並清除無效引用。
- 快取鍵要包含圖片識別、ROI、來源 ID、流程 ID、方法、參數、順序與群組結構；任何影響輸出的設定改變都要失效快取。
- 換圖時清除上一張圖的 ROI、前處理輸出、關聯 mask、區塊 mask、overlay 與 generation，防止舊圖結果覆蓋新圖。
- 刪除前檢查是否被關聯、區塊或物件組引用；要列出引用者並要求確認，避免留下半失效流程。

### 測試與修改方式

- 每次拆 partial 只移動一個責任範圍；用 `rg` 找完所有呼叫者後再刪除主檔舊方法，並立即建置。
- OpenCV 改動要測小圖、一般圖和 16384 x 50000 大圖；單步驟、群組、關聯、區塊與快取命中都要測。
- UI 回歸測試至少包含快速調參、套用/取消、換圖、關閉程式、背景分頁、左右同步、縮放/平移、多選分組、改名/排序與刪除引用項目。
- 結果測試要確認來源影像正確、mask 不缺線、紅色 overlay 座標一致、群組輸出符合定義、已完成項目不重算。
- 建議補上 golden-image、ID/引用、快取失效、取消請求、Dispose 與大圖記憶體峰值測試。

## 8. 尚未完成/下一步

- 完整驗證 Threshold 三種方法的 OpenCV mask、參數面板與大圖顯示。
- 定義物件組處理子項目的實際演算法、參數與結果呈現；目前物件組來源 mask、CCL、篩選、排序與結果繪製已完成，子處理仍是後續擴充邊界。
- 定義物件組是否允許多個來源區塊，以及多來源時的合併規則；目前 UI 先指定單一來源，模型保留 ID 清單。
- 確認物件組結果在 `區塊結果` 分頁的黃色框線/編號顯示規則，以及後續是否需要獨立的物件判定分頁。
- 以實際 16384 x 50000 圖片測試多 ROI、多關聯與多區塊的記憶體峰值。
- 大圖路徑已補上區塊處理每一步的累積 MASK 快取與檢測預覽；後續仍要補 Bitmap 小圖路徑的每一步快取，並確認重複點選不會重算。
- 尺寸量測的實際量測線、平行線紀錄、來源 MASK 紀錄與像素統計已完成；後續仍需補上實體單位校正、統計結果持久化與更完整的量測報表輸出。
- 以實際操作確認各分頁在執行單一步驟、群組、關聯與區塊處理時，Zoom/Offset 均維持不變。
- 補上關聯被刪除、來源失效、區塊引用失效時的使用者警告。
- 完成 E/D 等最終物件判定方法；目前區塊處理主要是二值 mask 結合與形態學後處理。
- 完成剩餘 partial 拆分後，再評估純 OpenCV service 化。
- 目前沒有完整自動化 UI/影像 golden-image 測試，正式交付前需要手動驗證縮放、平移、換圖、快速調參與取消流程。
- 新增的物件組依賴預覽需要以小圖、一般圖及 `16384 x 50000` 大圖實測：確認執行物件組後前處理、處理後、區塊處理與物件結果都能切換查看，且不改變目前 Zoom/Offset。

## 9. 建置驗證

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' .\MyApp4.sln /p:Configuration=Debug /p:Platform="Any CPU" /v:minimal
```

本次文件整理前已成功建置，並以獨立輸出目錄 `IntegratedImageProcessingApp/bin/DebugVerify/` 驗證，避免覆蓋使用中的 Debug 輸出。不要提交臨時 `codex-build`、`bin` 或 `obj` 輸出；提交前確認執行中的 `IntegratedImageProcessingApp.exe` 已關閉。
