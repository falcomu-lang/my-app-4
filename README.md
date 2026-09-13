# 整合式影像處理軟體

以 C# Windows Forms 開發的高解析度影像檢視與 ROI 影像處理工具。軟體以灰階影像處理為核心，支援大尺寸圖片的切圖顯示、ROI 管理，以及 OpenCV 邊緣偵測結果的即時檢視。

## 目前功能

- 讀取 BMP、JPG/JPEG、PNG、TIF/TIFF 圖片。
- 彩色圖片會在記憶體中轉為灰階；灰階圖片直接使用。
- 支援大型影像的 WIC 切圖顯示，不需建立整張 GDI+ Bitmap。
- 左右檢視器同步縮放、平移與重設視圖。
- 可建立多個 ROI，ROI 以原圖座標保存。
- 可新增、命名、刪除、排序影像處理步驟，並以群組管理多個處理。
- 處理後畫面保留完整原圖；ROI 內偵測到的邊緣以紅色疊加顯示。
- Canny、Polarity Edge、Sobel Edge 結果以完整解析度 OpenCV Mask 保存；放大、平移與重設視圖不會重新執行演算法。
- 已完成實際運算的左側處理項目會顯示時間，例如 `處理1(Canny Edge) - 475 ms`；快取重畫不會重新計時，程式重開也不保留舊時間。
- 處理步驟、ROI 與上次開啟的圖片路徑會保存至 `SystemParameters.ini`。

## 基本操作流程

1. 點選左側 `讀取圖片` 載入影像。
2. 在 `指定 ROI` 按右鍵，選擇 `新增 ROI`，並在原圖上拖曳綠色框。
3. 在 `影像處理` 按右鍵，選擇 `新增影像處理`。
4. 點選 `處理N(未決定)`，從右側流程樹選擇處理方法。
5. 點選處理項目後才開始建立該項目的 ROI Mask；讀圖本身不會自動開始影像處理。
6. 切換到左右任一側的 `處理後` 分頁檢視紅色結果，可使用滾輪縮放、左鍵拖曳平移，或按 `重設視圖`。

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

- 原圖、處理後畫面共用同一個大圖來源，避免重複讀取檔案。
- 預覽影像最大為 `2048 x 2048`；需要細節時會改由原始解析度 tile 顯示。
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
