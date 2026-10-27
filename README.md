# 整合式影像處理軟體

以高解析度灰階影像為主的 Windows 檢測與影像處理桌面程式。程式碼基準為 2026-10-27 的主分支提交 917531e；完整接手資訊請見 [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md)。

## 目前狀態

| 範圍 | 程式現況 |
|---|---|
| 找物件流程 | 提供前處理、影像處理、影像關聯、區塊判定與物件定義。 |
| 檢測參數 | 支援 Recipe 管理、平場校正、尺寸量測與良品判斷、七個一般缺陷分支、兩組頻域異常分析、結果整合及 ROI 群組。 |
| 參數結果確認 | 使用所選參數執行尺寸與缺陷判定；結果和各影像頁的縮放／平移狀態可在切換主功能後保留於記憶體。 |
| 線掃描相機 | 相機模組已嵌入全域設定；總覽可連線、連續取像及持續檢測。相機灰階影格可直接從記憶體交給檢測流程。實機驗收仍待完成。 |
| TCP/IP | 有被控端監聽、多站連線、狀態／逐物件判定傳送，以及連續取像／檢測命令。跨設備互通仍待驗收。 |
| 歷史資料 | 左側有入口，內容目前仍是「尚未實作」佔位頁；沒有每日檢測資料庫。 |

程式功能已有相當完整的設定與執行流程，仍需完成相機實機、公司影像判定品質、長時間負載、資料持久化及多站 TCP/IP 驗收，才能確認產線使用條件。

## 主要流程

~~~
讀入或擷取灰階影像
  -> 前處理
  -> 影像處理（例如 Edge、Threshold）
  -> 影像關聯
  -> 區塊判定／整合
  -> 物件定義與排序
  -> Recipe 參數
       ├─ 平場校正
       ├─ 尺寸量測與尺寸良品判定
       ├─ 缺陷檢測範圍組
       ├─ 平場影像＋條件一至六
       └─ 頻域異常一、二
  -> 結果確認
  -> 檢測總覽（每片物件的尺寸／缺陷／綜合判定）
~~~

一個 ROI 定義可被多個 ROI 群組引用。ROI 以穩定 ID 關聯，名稱需唯一；同一群組內重疊範圍以聯集處理。啟用的檢測條件沒有有效 ROI 群組時，程式會阻止運算。

目前名為「頻域異常」的功能，以掃描小格的加窗影像能量及可選的鄰格比較找異常；程式沒有 FFT／DFT 實作。紋理異常與自動方向線分析已移除。

## 相機影格與運算

相機傳來的 Mono8 灰階緩衝區會被固定在記憶體中，包成大型影像來源供處理使用；檢測不需先讀取 TIFF。檢測同時會在背景產生一份暫存 TIFF，作為目前相機影像的暫存保存。每次只執行一張；處理尚未完成時，新影格會略過，不排隊。

總覽的「連續取像」只更新原始影像；「檢測」按一次開始持續檢測，再按一次停止接收後續檢測。停止時已開始的那張會完成。

## 設定與影像存放

以下路徑以主程式執行檔所在目錄為根目錄：

| 路徑 | 用途 |
|---|---|
| SystemParameters.ini | 目前正在編輯的檢測流程設定。 |
| recipe/parameterList.json、recipe/{ID}.ini | Recipe 清單、名稱／說明／排序及各 Recipe 參數快照。一次只有一組作用中參數。 |
| config/device-settings.ini | 攝影機 X/Y mm-per-pixel、軟體 ID、本機監聽 IP／Port、允許連線開關。缺檔時精度預設為 1，mm 換算預設關閉。 |
| config/connection-controller-settings.xml | 總控連線行為與被控站點清單。 |
| captureSetting.ini | 相機取像、顯示、觸發及自動存圖設定；舊 settings.ini／setting.ini 可在缺少新檔時遷移。 |
| CameraCapture/yyyyMMdd | 相機程式自動存圖使用的每日目錄。 |
| snapshot/ | 相機程式手動單張存圖目前寫在此目錄；手動滾動影像則寫入 snapshot/yyyyMMdd。 |
| %LOCALAPPDATA%/IntegratedImageProcessingApp/CameraFrames | 檢測用相機影格的暫存 TIFF。這份檔案不是檢測輸入來源。 |

**目前存圖注意事項：** 相機設定模型有 SaveFolder 欄位，但現行存檔路徑沒有使用該欄位。一般手動單張圖也尚未建立日期子資料夾；若部署要求所有手動圖片都依日期保存，需另行修正並驗收。

設備精度及網路設定位於輸出目錄的 config/device-settings.ini。建置時只會在輸出目錄缺少該檔時複製初始檔，更新程式時應保留現場設定。

## 建置

需要 Visual Studio／MSBuild、.NET Framework 4.7.2 Developer Pack、NuGet 套件，以及相機端的 Sapera SDK 與 LSI 測速輪 x64 DLL。Solution 雖以 Any CPU 組態建置，兩個專案實際目標都是 x64。Sapera 與 LSI 參考來源預設指向相鄰的使用者資料夾；在其他電腦上請覆寫下列屬性：

~~~powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:CameraSdkDirectory="C:\path\to\Sapera" /p:LsiNativeDirectory="C:\path\to\LSI\x64"
~~~

舊式 .NET Framework 專案使用明確的 Compile 清單。新增 C# 檔案時，要同步修改對應的 .csproj。Repository 目前沒有獨立自動化測試專案；建置成功也不能取代 GUI、影像資料及硬體驗收。

## 文件導覽

- [PROJECT_HANDOFF.md](PROJECT_HANDOFF.md)：架構與資料流、目前進度、所有主要原始檔職責、持久化路徑、已知限制及接手驗收順序。
