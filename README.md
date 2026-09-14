# 整合式影像處理軟體

以 C# Windows Forms 開發的高解析度影像檢視與 ROI 影像處理工具。軟體以灰階影像處理為核心，支援大尺寸圖片的切圖顯示、ROI 管理，以及 OpenCV 邊緣偵測結果的即時檢視。

## 目前功能

- 讀取 BMP、JPG/JPEG、PNG、TIF/TIFF 圖片。
- 彩色圖片會在記憶體中轉為灰階；灰階圖片直接使用。
- 支援大型影像的 WIC 切圖顯示，不需建立整張 GDI+ Bitmap。
- 大圖 OpenCV 運算固定以 64 位元行程執行，避免完整 ROI Mat 受到 32 位元位址空間限制。
- 左右檢視器同步縮放、平移與重設視圖。
- 可建立多個 ROI，ROI 以原圖座標保存。
- 可新增、命名、刪除、排序影像處理步驟，並以群組管理多個處理。
- 可新增 OpenCV 影像前處理：Normalize、Gaussian Blur、CLAHE、Median Blur、Sharpen、Bilateral Filter。
- 前處理可 Ctrl 多選後建立群組；群組內由上而下串接，前一步輸出會交給下一步。
- 前處理在全影像執行，邊緣偵測只在 ROI 內執行；Canny、Polarity Edge、Sobel Edge 使用前處理後影像作為來源。
- 大圖前處理結果以全解析度記憶體 Tile 顯示，不需要暫存輸出檔；修改前處理或邊緣參數會保留目前縮放與平移位置。
- 處理後畫面保留完整原圖；ROI 內偵測到的邊緣以紅色疊加顯示。
- Canny、Polarity Edge、Sobel Edge 結果以完整解析度 OpenCV Mask 保存；放大、平移與重設視圖不會重新執行演算法。
- Canny、Sobel、Polarity 的實際 OpenCV mask 實作已整理至 `Forms/MainForm.ImageProcessing.cs`；主表單只保留流程協調與參數/顯示管理。
- 已完成實際運算的左側處理項目會顯示時間，例如 `處理1(Canny Edge) - 475 ms`；快取重畫不會重新計時，程式重開也不保留舊時間。
- 處理步驟、ROI 與上次開啟的圖片路徑會保存至 `SystemParameters.ini`。
- 支援建立 `影像關聯`，可指定原圖、前處理項目或前處理群組作為來源，再連接影像處理項目或影像處理群組。
- 影像關聯使用固定 GUID 儲存來源與目標，不依賴顯示名稱或排序；因此改名、上移、下移不會破壞關聯。
- 關聯項目支援右鍵 `處理`、`上移`、`下移`、`命名`、`刪除`；按住 `A` 或 `a` 左鍵點擊也會直接處理。

## 基本操作流程

1. 點選左側 `讀取圖片` 載入影像。
2. 在 `指定 ROI` 按右鍵，選擇 `新增 ROI`，並在原圖上拖曳綠色框。
3. 在 `影像處理` 按右鍵，選擇 `新增影像處理`。
4. 可先在 `影像前處理` 按右鍵新增前處理，點選 `前處理N(未決定)` 後從右側選擇方法。
5. 點選 `處理N(未決定)`，從右側流程樹選擇邊緣偵測方法。
6. 點選處理項目後才開始建立該項目的 ROI Mask；讀圖本身不會自動開始邊緣偵測。
7. 切換到左右任一側的 `前處理` 或 `處理後` 分頁檢視結果，可使用滾輪縮放、左鍵拖曳平移，或按 `重設視圖`。
8. 若要使用前處理結果進行邊緣偵測，在 `影像關聯` 右鍵新增關聯，選擇來源影像與目標處理項目後按 `套用`，再右鍵選擇 `處理`。

## 影像關聯

- 選擇 `原始影像` 時，演算法與處理後底圖都使用原圖。
- 選擇前處理項目時，會使用該項目前處理輸出作為演算法來源與處理後底圖。
- 選擇前處理群組時，前處理依序串接，最後輸出作為影像處理來源。
- 處理後分頁會以關聯指定的來源影像作為底圖，再疊加 ROI 內的紅色結果；不會將前處理結果計算後又畫回較深的原圖。
- 關聯設定面板與前處理/影像處理參數面板分開管理，切換項目不會清除其他面板。
- 關聯目前已完成資料保存、來源/目標選擇與處理入口；每個來源的獨立結果快取、完整群組合併策略、處理時間顯示與刪除來源後的失效提示仍在補強中。

## 架構整理進度

- 已完成 `MainForm.ImageProcessing.cs`：影像處理執行入口，以及 Canny、Sobel、Polarity 的 OpenCV mask 演算法。
- 下一步建議依序拆分：`MainForm.Preprocessing.cs`、`MainForm.Relations.cs`、`MainForm.Parameters.cs`、`MainForm.Roi.cs`、`MainForm.Preview.cs`、`MainForm.Cache.cs`。
- 拆分優先使用 `partial class MainForm`，先保持行為不變，再逐步將純演算法與快取移至獨立服務。
- 大圖處理要避免整張 Bitmap 複製；演算法應使用完整 ROI 的 OpenCV Mat，顯示才使用 tile。
- 影像來源必須明確區分原圖與前處理結果；不可只用 `latestPreprocessedImage != null` 判斷來源。
- 讀取新圖片、修改 ROI 或參數時，必須清除對應快取與 generation，避免顯示上一張圖片的結果。

## 邊緣偵測

Canny 使用 OpenCV 原生流程：

`Gaussian Blur -> Canny -> Binary Mask -> Red Overlay`

可設定參數：

- 低門檻
- 高門檻
- 核心大小：`3`、`5`、`7`
- L2 梯度
- 高斯模糊大小
- 高斯 Sigma

`Strongest`、`Longest`、最小邊緣長度與最大斷點間距不屬於標準 Canny，因此不在 Canny 介面中提供。後續應在輪廓分析或特徵篩選階段處理這類條件。

Polarity Edge 使用 OpenCV Gaussian blur、Sobel 梯度與對比判斷。可設定極性、對比門檻、核心大小、平滑、高斯 Sigma、灰階變化方向與 ROI 邊界處理。

Sobel Edge 使用 OpenCV 原生 Sobel。可設定灰階變化方向、核心大小（`1`、`3`、`5`、`7`）與邊緣門檻。

三種方法的左側項目即使已附加處理時間，仍可正常開啟右側參數並顯示已快取的紅色結果。

左下角狀態列會分開顯示：

- `ROI 處理時間`：ROI 灰階 tile 與 OpenCV Mat 建立時間。
- `影像處理時間`：目前選用演算法的實際 OpenCV 運算時間。
- `顯示時間`：完成 Mask 轉為目前可見紅色 overlay 的時間。

## 大圖處理原則

- 原圖與處理後畫面共用同一個大圖來源；前處理完成後使用記憶體型 Tile 來源呈現全解析度結果，避免重複開啟輸出檔。
- 預覽影像最大為 `2048 x 2048`；需要細節時會改由原始解析度 tile 顯示。
- 快取 Tile 的原件只由 `LargeImageSource` 持有；檢視器繪製使用短期複本，避免快速調參時出現 GDI+ 的並行使用例外。
- ROI Mask 建立完成後會留在記憶體。再次選取相同且參數未改變的處理時，直接讀取快取結果。
- 修改 ROI、處理方法或有效處理參數時，該處理的 Mask 才會重新建立。

## 開發環境

- Visual Studio 2019 或更新版本
- .NET Framework 4.7.2 Developer Pack
- NuGet 套件依 `IntegratedImageProcessingApp/packages.config` 還原，包含 OpenCvSharp 與其 .NET Framework 相依套件。

## 建置與執行

使用 Visual Studio 開啟 `MyApp4.sln`，還原 NuGet 套件後建置並執行 `IntegratedImageProcessingApp`。

也可在 Developer PowerShell 執行：

```powershell
& 'C:\Program Files (x86)\Microsoft Visual Studio\2019\Community\MSBuild\Current\Bin\MSBuild.exe' .\MyApp4.sln /p:Configuration=Debug /p:Platform="Any CPU"
```

## 專案結構

- `IntegratedImageProcessingApp/Forms/MainForm.cs`：主介面、ROI、處理步驟、OpenCV Canny 與處理後 overlay 協調。
- `IntegratedImageProcessingApp/Controls/ImageDisplayControl.cs`：圖片檢視、縮放、平移、ROI 繪製與左右視圖同步。
- `IntegratedImageProcessingApp/Controls/LargeImageSource.cs`：大型圖片 WIC 預覽與原始解析度 tile 來源。
- `PROJECT_HANDOFF.md`：提供後續開發者的實作細節、已知限制與待辦事項。
