# 整合式影像處理軟體

本 repository 是以 Windows WinForms 為主的灰階線掃描影像檢測程式，包含本機相機取像、物件搜尋、Recipe、尺寸與缺陷判定、結果總覽、歷史資料及 TCP/IP 總控／被控通訊。

- 文件核對日期：2026-10-29
- Git 基準：main／origin/main 的 6f5054d（docs: refresh handoff and secure history replay parameter）
- 完整架構、流程、檔案職責、風險與拆檔建議：[PROJECT_HANDOFF.md](PROJECT_HANDOFF.md)
- 工作目錄另有歷史頁版面與圖片保存選項修改，尚未推送到 GitHub。

## 專案狀態

主要檢測流程與歷史資料頁已有程式實作。相機、米輪、公司影像檢出率、長時間連續運轉及多站網路通訊仍需在目標設備與影像上驗收；程式碼存在或成功建置不等於產線驗收完成。

| 功能 | 現況 |
|---|---|
| 找物件 | 前處理、影像處理、影像關聯、區塊整合與物件定義流程。 |
| 檢測參數／Recipe | 多參數管理、匯入／套用／更新／另存及參數結果確認。 |
| 尺寸判定 | 依參數指定 MASK 量測，可使用全域 X/Y mm-per-pixel；A 未通過時再判 B，否則 NG。 |
| 缺陷判定 | 平場影像與條件一至六，共七個一般核心；另有頻域異常一、二及結果整合。 |
| 結果確認 | 物件搜尋、尺寸與缺陷運算、顯示與記憶體快照。切換主頁時可保留結果；程式關閉後不保留此工作階段快照。 |
| 檢測總覽 | 顯示原始大圖及逐片尺寸、缺陷、綜合狀態；相機可連續取像或持續檢測。 |
| 歷史資料 | 每日 SQLite 檢測紀錄、逐物件明細、日期／Recipe／狀態查詢，以及以當時參數快照重新檢測。 |
| 相機 | Sapera 線掃描模組嵌入主程式；Mono8 frame 由記憶體直接進檢測，存檔在背景並行執行。 |
| TCP/IP | 本機被控 listener 與總控多站連線、狀態與命令交換；不傳送影像本身。 |

## 檢測資料流

~~~text
載入圖片或取得相機 Mono8 frame
  -> 指定 ROI／找物件流程
  -> 物件定義與格位
  -> 目前作用中的檢測參數
      -> 平場校正與尺寸量測／尺寸良品判定
      -> 平場缺陷、條件一至六、頻域異常一／二
      -> 結果整合
  -> 參數結果確認／檢測總覽
  -> 可選保存歷史紀錄
~~~

相機影像直接由灰階記憶體 buffer 建立大型影像來源供演算法使用，不會先存 TIFF 再讀回來。另一路背景存檔把相機原圖寫入每日資料夾。每次最多處理一張；處理忙碌時新影格會略過，不排隊。

「頻域異常」目前使用掃描視窗的加窗能量和可選鄰格比較，沒有 FFT／DFT。紋理異常與自動方向線分析已移除。

## 歷史紀錄

- 相機檢測完成或待確認的結果會寫入主程式根目錄下 history/yyyyMMdd.db。
- 手動「參數結果確認」的結果，須在全域設定 > 內部參數 > 歷史紀錄中啟用「執行確認後新增歷史資料」並套用；「保留參數結果確認的圖片」獨立控制是否另存圖片副本。關閉圖片保存時仍記錄判定與明細，但該筆不能用歷史原圖重新檢測。
- 歷史紀錄包含整張檢測統計、逐物件判定與尺寸／缺陷明細；Recipe 快照以 SHA-256 去重，放在 history/recipe-snapshots。
- 相機原圖位於 CameraCapture/yyyyMMdd；外部載入圖片若需歷史重跑，會複製到 snapshot/yyyyMMdd。歷史記錄保存相對路徑以連結圖片與參數快照。
- 歷史重跑會以紀錄中的 ParameterId 從當時 Recipe 快照選定參數。舊紀錄若沒有 ParameterId，只有快照恰有一個參數時才可安全推定；多參數時拒絕猜測。

## 執行目錄中的主要資料

路徑相對於主程式輸出目錄：

| 路徑 | 用途 |
|---|---|
| SystemParameters.ini | 目前工作中的檢測設定。 |
| recipe/parameterList.json、recipe/{ID}.ini | Recipe 名稱／說明／排序與每份完整參數快照。 |
| config/device-settings.ini | 相機精度、程式名稱、站點 ID、本機監聽 IP／Port、允許連線、手動歷史記錄／圖片保留開關，以及上次選取的介面角色。 |
| config/connection-controller-settings.xml | 總控連線行為與被控站點清單。 |
| captureSetting.ini | 相機取像、觸發、顯示、存圖與米輪設定。 |
| CameraCapture/yyyyMMdd | 相機取像原始灰階 TIFF 的每日保存目錄。 |
| snapshot/yyyyMMdd | 手動載圖執行結果確認時，供歷史重跑使用的影像副本。 |
| history/yyyyMMdd.db | 每日檢測批次及逐物件歷史資料。 |
| history/recipe-snapshots/{hash}.ini | 去重後的歷史 Recipe／檢測參數快照。 |

相機精度缺檔預設為 1 mm/pixel，但 mm 換算預設關閉；這是初始值，不代表已完成實際標定。更新程式時應保留輸出目錄中的 config。

## 建置

需要 Visual Studio／MSBuild、.NET Framework 4.7.2 Developer Pack、NuGet 套件、Sapera SDK managed assembly，以及 LSI 米輪 x64 native DLL。Solution 組態雖使用 Any CPU，兩個專案實際都設定為 x64。SDK 預設路徑指向相鄰的使用者專案資料夾；換開發機時請覆寫：

~~~powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:CameraSdkDirectory="C:\path\to\Sapera" /p:LsiNativeDirectory="C:\path\to\LSI\x64"
~~~

這是舊式 .NET Framework 專案，C# 檔案使用 csproj 明確 Compile 清單。新增 .cs 檔時必須加入對應專案檔。Repository 沒有獨立自動化測試專案。硬體與影像品質仍需在目標設備和代表性影像上驗收。
