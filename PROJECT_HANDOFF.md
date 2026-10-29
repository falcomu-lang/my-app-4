# Project Handoff：整合式影像處理軟體

文件更新日：2026-10-29
程式碼基準：main／origin/main 的 6f5054dea2d57029ddc521629a342e3213cbdd48（docs: refresh handoff and secure history replay parameter）
Solution：MyApp4.sln
工作目錄：此文件所在專案根目錄

核對時 main 與 origin/main 指向同一個 6f5054d。工作目錄另有四個尚未提交的程式／設定修改：歷史摘要卡片框線版面、全域設定圖片保留開關、設定檔保存及歷史記錄不保留圖片時仍保存判定明細。新設定對缺少該鍵的舊 INI 預設為保留，以維持既有行為。這些修改尚未推送；狀態以目前工作目錄為準。

## 1. 專案定位與目前結論

這是一套以單台線掃描攝影機為本機取像來源的 Windows 灰階影像檢測程式。單一執行個體控制一台本機攝影機；總控頁可連接多個被控站點，收集各站的相機狀態與逐物件判定。

目前有完整的找物件、Recipe、尺寸／缺陷檢測、結果確認、檢測總覽、相機整合、歷史資料與 TCP/IP 通訊介面。程式仍由 WinForms 主視窗協調大部分流程；相機實機、公司影像、長時間負載、歷史資料保存故障處理與跨設備通訊尚未完成整體驗收。因此目前具備檢測軟體主要流程，但不能只憑程式碼存在或成功建置就認定已達產線驗收狀態。

Solution 目前含兩個專案，沒有獨立的測試專案：

- IntegratedImageProcessingApp：主檢測程式、影像處理、參數、總覽與 TCP/IP 總控。
- CameraCaptureModule：從相機擷取程式整合進來的線掃描相機 UI、Sapera 取像和米輪控制模組。

## 2. 使用流程與資料流

### 2.1 找物件與檢測設定

~~~
讀取影像
  -> 指定影像 ROI
  -> 找尋物件流程（可收合）
       -> 影像前處理
       -> 影像處理
       -> 影像關聯
       -> 整合成區塊
       -> 物件定義
  -> 檢測參數設定
       -> 尺寸量測與尺寸良品條件
       -> 平場校正
       -> 缺陷範圍與 ROI 群組
       -> 平場缺陷核心、條件一至六
       -> 頻域異常一、二
       -> 結果整合
  -> 參數結果確認
  -> 檢測總覽
~~~

「找尋物件流程」之前的影像及 ROI 設定用來建立物件搜尋結果。物件定義包含連通元件、面積／尺寸篩選、排序、編號及格位資料；尺寸與缺陷檢測以找到的每枚物件為單位。影像大小或 Recipe 改變時，相關流程需要重新確認或執行。

### 2.2 尺寸、缺陷與總判定

- 尺寸量測使用檢測參數指定的物件定義與 MASK 來源；可用全域 X/Y mm-per-pixel 轉換尺寸。
- 尺寸良品判斷以每枚物件的量測資料評估。A 規全部符合時判 A；A 未符合後才評估 B；兩者都不符合時判 NG。實際規則仍須以代表性尺寸資料驗收。
- 一般缺陷有平場影像加條件一至六，共七個核心；每核心有啟用開關、用途標籤、影像來源／對比、前處理、淡色缺陷增強、暗／亮門檻、MASK 形態處理、物件篩選與顯示選項。
- 缺陷範圍由全物件共用 ROI 定義和 ROI 群組組成。ROI 具有穩定 ID 和唯一名稱；群組保存 ROI ID 清單。各核心及頻域一／二可選不同群組。群組內重疊 ROI 以聯集方式處理，避免重疊區重複計算。啟用的條件未選有效群組時，流程會拒絕開始。
- 結果整合會依目前設定彙整暗／亮缺陷及已選擇納入的頻域結果。檢測總覽每枚物件分別顯示尺寸、缺陷、綜合判定；任何一枚物件 NG 不會把同張其他物件一併改判 NG。
- 「頻域異常」目前實際計算每格影像的加窗能量，並可選擇比較鄰近格；程式碼沒有 FFT／DFT 計算。名稱沿用既有 UI／參數命名。
- 紋理異常與自動方向線分析目前已從 UI、運算和參數模型移除。

### 2.3 結果確認與結果保留

「參數結果確認」可選檢測參數、載入影像並執行確認。尺寸量測與缺陷分析可同時運算；已啟用且需要整合或鄰格比較的頻域分析也會加入排程，並重用有效結果。各缺陷核心和 ROI 層內仍可能使用平行迴圈。

最近一次成功的結果快照含參數、影像來源、物件、量測／缺陷／整合結果及影像頁檢視狀態。切換主功能後可從記憶體還原。來源影像、參數簽章或相關設定改變時，舊結果需要失效並重新執行。這是程式執行期間的記憶體保存，不是跨程式重啟的歷史資料保存。

### 2.4 相機影格路徑

~~~
CameraCaptureModule / Sapera
  -> 取出 Mono8 8-bit 灰階 frame
  -> CameraGrayscaleFrame（影格 byte[]、寬、高、stride）
  -> LargeImageSource 固定 byte[] 並建立 OpenCV Mat view
  -> 使用目前選取的檢測參數執行物件、尺寸、平場、缺陷與頻域流程
  -> 成功或待確認的逐物件狀態交給檢測總覽
~~~

檢測輸入直接使用記憶體灰階緩衝區，不需先寫 TIFF 再讀回來。相機檢測路徑以 LargeImageSource 的參照生命週期保護影格，並在背景把原始大圖保存到主程式根目錄的 CameraCapture/yyyyMMdd；這份 TIFF 是保存／歷史來源，不會再讀回作為演算法輸入。保存失敗會記錄診斷，不應中斷目前影格的檢測。相機模組自身的自動存圖仍是另一條取像存檔設定。

總覽的操作語意：

- 「連結攝影機」在相機程式已開啟後連線並啟動 Start Preview；已連線時按鈕變成「斷開連線」。
- Software Trigger 模式連線前會要求確認機台原點，並執行 Meter Wheel Encoder Clear。
- 「連續取像」只更新總覽中的原始影像，不啟動判定。
- 「檢測」開始持續接收並判定後續影格；再次按下「停止檢測」後不再啟動新判定，已開始的影像仍會完成。
- 同一時間只處理一張影像；忙碌時收到的新影格會略過，不進入佇列。這是明確的丟棄策略，尚未以實機最大取像速率驗收。
- 若影格尺寸不符、沒有可用 Recipe 或判定未完成，總覽仍保留影像並以待確認狀態呈現。

### 2.5 TCP/IP 總控與被控

主程式依全域設定啟動本機 TCP listener，也可在「連線總控角色」連接多個被控站。設定可指定本機站點 ID、監聽 IPv4／Port、允許連線、站點 ID／名稱／IP／Port、重連、心跳逾時及進入總控頁自動連線。

目前的訊息以換行分隔 JSON 傳輸，包含 hello、station-status、command、command-ack 等類型。總控端能傳送連續取像開始／停止及檢測命令；被控端回傳連線、取像、Recipe 名稱、判定時間、逐物件格位及尺寸／缺陷／綜合等級。TCP 不傳影像本身。程式尚未提供 TLS、訊息驗證或明確的協定版本欄位，跨電腦、斷線重連及多站同時操作仍需驗收。

## 3. 功能進度與待驗收項目

| 功能 | 程式現況 | 尚待確認 |
|---|---|---|
| 大型影像顯示 | 支援影像分塊、預覽層級、縮放／平移、overlay 及大圖來源參照計數。 | 公司大圖在不同縮放倍率的清晰度、拖移流暢度、峰值記憶體。 |
| 找物件流程 | 前處理、影像處理、關聯、區塊判定、物件定義等流程已接入。 | 不同產品、影像規格、ROI 邊界與誤檢漏檢表現。 |
| Recipe 管理 | 可新建、讀取加入、選擇套用、更新、另存；清單有 ID、名稱、說明和排序。一次只有一組作用中 Recipe。 | 多組差異參數 A→B→A 重複切換、舊檔相容、現場設定保留。 |
| 平場校正 | 平場曲線、校正影像和相關 MASK 可設定及保存於檢測參數。 | 不同照明、不同曝光及重新載入參數後的完整結果。 |
| 尺寸量測 | 可建立量測線、依 MASK 範圍裁切、依設備精度顯示 pixel／mm，並產生量測紀錄。 | 標準件 X/Y 標定、斜線誤差、量測速度及規格邊界。 |
| 尺寸良品判定 | A 優先、A 不符再評估 B 的規則模型及結果表已存在。 | A/B/NG、缺量測、規則錯誤及多物件格位完整 GUI 驗收。 |
| 一般缺陷 | 平場＋條件一至六可個別設定、套用、顯示 MASK／紅框／橘框及併入結果整合。 | 代表性良／不良品的誤報率、漏檢率、時間與記憶體。 |
| ROI 定義／群組 | 全物件共用 ROI、唯一名稱、穩定 ID、群組成員排序及核心群組選擇已接入。 | 物件相對幾何差異、旋轉、比例與群組框位和運算遮罩一致性。 |
| 頻域異常一／二 | 兩組可獨立啟用、設定影像來源／對比、前處理、掃描高度、步距、垂直縮放、門檻及鄰格比較；有熱圖和異常框。 | 目標公司影像檢出率、背景誤報、ROI 邊界、速度及效果穩定性。 |
| 紋理／方向線分析 | 已移除。 | 無。 |
| 參數結果確認 | 影像與處理結果在主功能切換後以記憶體快照保留；支援尺寸／缺陷平行及結果顯示。 | 大圖記憶體生命週期、切頁／改參數／換圖失效流程與全 UI 驗收。 |
| 檢測總覽 | 原始影像、物件格位、尺寸／缺陷／綜合結果、篩選、選取聚焦、回復全圖及相機操作已接入。 | 真正相機觸發、持續檢測壓力、缺件／無效參數與現場顯示。 |
| 相機模組 | 嵌入主程式，保留相機原本設定、波形、米輪彈出視窗；具備 Mono8 frame event。 | Sapera 驅動、相機、米輪、Software／External Trigger 的實機完整驗收。 |
| TCP/IP | 本機 listener、總控多站卡片、心跳、重連、狀態和命令程式碼存在。 | 防火牆、多機、多站 ID、掉線恢復與長時間連線驗收。 |
| 歷史資料 | 已有日期、Recipe、判定狀態查詢、摘要卡、檢測清單、尺寸／缺陷明細及右鍵歷史重跑；相機紀錄自動保存，手動結果確認可獨立設定是否記錄及是否另存圖片。 | 驗收每日 DB、兩種記錄路徑、圖片保存失敗、舊 schema 遷移、長期容量與備份。未保留圖片時仍保存判定明細，但該筆無法用歷史原圖重新檢測。歷史重跑依批次 ParameterId 精確選擇 Recipe 快照中的參數；舊資料含多參數而無 ID 時拒絕猜測。 |

## 4. 平行運算與效能解讀

- 結果確認會把尺寸量測工作和缺陷工作同時啟動；尺寸量測可按物件平行。
- 開啟缺陷平行選項時，一般條件與啟用且需要運算的頻域一／二可一同排程；共用來源準備和鄰格比較相依仍需遵守。
- 一般缺陷、頻域及量測內部也能按物件／ROI 平行。這些是 Task Parallel Library 的工作，不等於固定建立「物件數 × 核心數」條 OS 執行緒。
- 巢狀平行可能讓 CPU、RAM、影像副本及預覽 patch 同時增加。現行程式沒有統一的全流程 CPU／記憶體預算；最大吞吐與最長等待時間需用實際設備量測。
- 頻域每格以 OpenCV 建立的加窗影像能量為基礎，直接遍歷影像資料計算；名稱中的「頻域」不表示目前執行 DFT。
- 舊交接紀錄曾記載 2026-10-23 的合成影像比對與排程檢查。這些是當時版本的證據，不代表目前版本的公司產品影像或實機節拍已驗收。
- 最近一次程式修改已用替代輸出目錄建置成功；因一般 Debug 執行檔正被使用，未覆寫該輸出。這次只更新文件，沒有重新建置或執行 GUI／硬體測試。

## 5. 設定檔、資料夾與保存語意

所有相對路徑以主程式輸出目錄為基準，除非欄位明確標出 LocalAppData。

| 檔案／路徑 | 寫入者與內容 | 重要語意 |
|---|---|---|
| SystemParameters.ini | SystemParameterIniService 保存目前正在編輯的完整檢測設定。 | 新建參數會重設這份設定；它與 recipe 目錄的快照分開。 |
| recipe/parameterList.json | DetectionRecipeCatalogService 保存 Recipe ID、顯示名稱、說明與排序。 | 名稱可編輯，關聯以 ID 為主。 |
| recipe/{ID}.ini | 每個 Recipe 的完整參數快照。 | 一次只有一組載入為作用中設定；套用 Recipe 會以快照更新目前流程。 |
| config/device-settings.ini | CameraDeviceSettingsIniService 保存設備名稱／站點 ID、X/Y mm-per-pixel、換算開關、本機監聽 IP／Port、允許連線、手動結果確認歷史記錄及圖片保留開關。 | 缺檔預設精度 1、換算關閉、ID 1、0.0.0.0:5000、允許連線關閉；圖片保留預設開啟。程式只在輸出目錄缺少時複製初始檔。 |
| config/connection-controller-settings.xml | ConnectionControllerSettingsService 保存總控監控、進入頁面自動連線、重連、timeout、heartbeat 與站點表。 | 舊 XML 沒有 MonitorCamera 時，模型預設為啟用。 |
| captureSetting.ini | CameraCaptureModule 的 SettingsService 保存相機、觸發、預覽、自動存圖、格式與米輪欄位。 | 儲存於執行檔目錄；缺少時嘗試複製同目錄的 settings.ini 或 setting.ini。 |
| CameraCapture/yyyyMMdd | 主程式相機檢測影格保存的原始灰階 TIFF；CameraCaptureModule 也會依其自動存圖設定保存影像。 | 主程式相機流程背景保存，演算法直接用記憶體 frame，不讀回 TIFF。 |
| snapshot/yyyyMMdd | 手動載入圖片執行確認後，供歷史記錄連結與重跑的影像副本。 | 由歷史紀錄功能按需複製；若複製失敗會保留來源路徑並記錄診斷。 |
| history/yyyyMMdd.db | 每日 SQLite 批次與逐物件明細。 | 使用 Windows winsqlite3.dll；啟用 WAL 與交易，schema 會補上 parameter_id 欄位以相容舊 DB。 |
| history/recipe-snapshots/{SHA256}.ini | 歷史批次對應的完整 Recipe／參數快照。 | 正規化後以 SHA-256 去重；歷史重跑以批次保存的 ParameterId 找當時使用的參數。 |
| logs/camera/app.log | CameraCaptureModule 的 AppLogger 記錄相機模組例外。 | 寫在執行檔目錄下 logs/camera。 |

**存圖需求差異：** CameraSettings.SaveFolder 有模型欄位及 INI 讀寫，但目前程式存圖路徑沒有讀取該欄位。一般手動單張圖也沒有日期目錄。若現場要求所有影像固定依日期分流或指定根目錄，需另做程式修改。

## 6. 原始碼檔案職責

下表以專案內相對路徑表示。Designer 檔負責 WinForms 產生的控制項配置；事件與流程邏輯在相對應的非 Designer 檔。舊式 csproj 使用明確 Compile 清單，新增 C# 檔要一併加入專案檔。

### 6.1 Solution 與主程式根檔案

| 檔案 | 職責 |
|---|---|
| MyApp4.sln | 同時載入主程式與相機模組。 |
| .gitignore | 排除 bin／obj、Visual Studio 狀態、使用者本機檔、測試輸出、NuGet packages 及 log。 |
| IntegratedImageProcessingApp/IntegratedImageProcessingApp.csproj | 主程式 x64／.NET Framework 4.7.2 設定、NuGet／SDK／LSI DLL 參考、明確 C# Compile 清單。 |
| CameraCaptureModule/CameraCaptureModule.csproj | CameraCaptureModule x64／.NET Framework 4.7.2 設定及 Sapera managed DLL 參考。 |
| IntegratedImageProcessingApp/packages.config | OpenCvSharp、Windows runtime 與相依套件版本。 |
| IntegratedImageProcessingApp/App.config | .NET Framework runtime 目標設定。 |
| IntegratedImageProcessingApp/Program.cs | 主程式啟動、WinForms 執行環境與例外處理入口。 |
| IntegratedImageProcessingApp/config/device-settings.ini | 設備精度、軟體 ID 及本機網路 listener 初始值。 |
| IntegratedImageProcessingApp/config/connection-controller-settings.xml | 連線總控行為及站點清單初始值。 |

WinForms 的 `.resx` 資源檔保存對應表單／控制項的字串、圖示和設計器資源，不承載檢測流程邏輯：

| 檔案 | 職責 |
|---|---|
| `IntegratedImageProcessingApp/Forms/MainForm.resx` | 主檢測程式 MainForm 的 WinForms 資源。 |
| `CameraCaptureModule/Controls/CameraDisplayControl.resx` | 相機顯示控制項資源。 |
| `CameraCaptureModule/Forms/MainForm.resx` | CameraCaptureModule 主視窗資源。 |
| `CameraCaptureModule/Forms/CameraSettingsForm.resx` | 相機設定視窗資源。 |
| `CameraCaptureModule/Forms/MeterWheelControlForm.resx` | 米輪控制視窗資源。 |
| `CameraCaptureModule/Forms/MeterWheelExtensionCompareForm.resx` | 米輪多組 Compare 視窗資源。 |
| `CameraCaptureModule/Sapera/AcqConfigDlg.resx` | Sapera acquisition 設定視窗資源。 |

### 6.2 主程式 Controls

| 檔案 | 職責 |
|---|---|
| IntegratedImageProcessingApp/Controls/ImageDisplayControl.cs | 單張／大型影像檢視、縮放平移、ROI 編輯、overlay、視圖狀態與影像來源生命週期。 |
| IntegratedImageProcessingApp/Controls/ImageDisplayControl.Designer.cs | ImageDisplayControl 的 WinForms 控制項配置。 |
| IntegratedImageProcessingApp/Controls/LargeImageSource.cs | 檔案／Mat／相機 byte[] 大圖來源、tile／preview、Mat view 及參照計數。相機 buffer 由此固定後零拷貝包裝。 |
| IntegratedImageProcessingApp/Controls/CameraCaptureHostControl.cs | 延遲建立並嵌入相機 MainForm；依相機設定、總覽及遠端命令切換影格事件訂閱。 |
| IntegratedImageProcessingApp/Controls/BufferedRenderPanel.cs | 降低自繪 WinForms 面板閃爍的雙緩衝基底。 |

### 6.3 主程式 Forms

| 檔案 | 職責 |
|---|---|
| IntegratedImageProcessingApp/Forms/MainForm.cs | 主視窗狀態、參數服務初始化、功能選單結構、目前影像載入與主要生命週期協調。 |
| IntegratedImageProcessingApp/Forms/MainForm.Designer.cs | 主視窗設計器產生的三欄版面與基本控制項。 |
| IntegratedImageProcessingApp/Forms/MainForm.Navigation.cs | 左側選單導覽、功能切換、鍵盤操作及相同目標的重複選取處理。 |
| IntegratedImageProcessingApp/Forms/MainForm.FunctionTree.cs | 找物件流程、影像處理步驟／群組及 ROI 清單的情境選單與清單操作。 |
| IntegratedImageProcessingApp/Forms/MainForm.GlobalSettings.cs | 全域設定頁、CameraCaptureHostControl、設備精度／IP／Port／站點 ID、歷史紀錄／圖片保留及總控設定 UI。 |
| IntegratedImageProcessingApp/Forms/MainForm.ConnectionController.cs | TCP listener／station 連線、站點卡片、心跳狀態、遠端連續取像與檢測命令、逐物件結果發布。 |
| IntegratedImageProcessingApp/Forms/MainForm.CameraCapture.cs | 接收本機相機影格、忙碌丟棄、建立記憶體影像來源、背景保存原始 TIFF、安排結果確認流程及回報總覽／歷史資料。 |
| IntegratedImageProcessingApp/Forms/MainForm.InspectionOverview.cs | 檢測總覽原圖、逐物件矩陣／選取／篩選、連線與連續取像／持續檢測按鈕。 |
| IntegratedImageProcessingApp/Forms/MainForm.InspectionHistory.cs | 歷史頁 UI、查詢／摘要／明細、手動與相機紀錄建立、手動圖片副本保留控制、歷史參數快照重跑及返回前工作狀態還原。 |
| IntegratedImageProcessingApp/Forms/MainForm.ImageView.cs | 多影像頁縮放／平移同步、視圖狀態更新及顯示模式切換。 |
| IntegratedImageProcessingApp/Forms/MainForm.Preprocessing.cs | 前處理參數 UI、步驟排序及 OpenCV 灰階前處理排程。 |
| IntegratedImageProcessingApp/Forms/MainForm.ImageProcessing.cs | 影像處理步驟運算、遮罩、來源解析與二值 MASK 快取。 |
| IntegratedImageProcessingApp/Forms/MainForm.LargeImageProcessing.cs | 大圖分塊影像處理、處理後大型預覽、MASK 建立及大型 overlay。 |
| IntegratedImageProcessingApp/Forms/MainForm.ImageRelations.cs | 影像關聯清單、步驟新增／選擇／套用 UI。 |
| IntegratedImageProcessingApp/Forms/MainForm.Relation.cs | 影像關聯群組的建置、成員管理、排序及關聯設定。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectJudgement.cs | 整合成區塊的顯示頁、來源 MASK overlay 及區塊判定 UI。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectJudgementProcessing.cs | 區塊處理方法、形態學／參數面板、區塊來源處理與結果更新。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinition.cs | 物件定義清單、物件定義設定與選取管理。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinitionProcessing.cs | 解析影像處理／關聯／區塊來源並啟動物件搜尋流程。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinitionMaskSources.cs | 物件搜尋來源 MASK 解析、合成與快取。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinitionComponents.cs | OpenCV 連通元件建立、尺寸篩選、方向／旋轉資訊及物件排序。 |
| IntegratedImageProcessingApp/Forms/MainForm.ObjectDefinitionDisplay.cs | 物件結果影像、編號框、處理計時及可見檢視更新。 |
| IntegratedImageProcessingApp/Forms/MainForm.RoiEditing.cs | 大圖上的 ROI 新增、選取、修改和座標轉換。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.cs | 檢測參數主頁協調、物件來源有效性及數量／狀態摘要。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Management.cs | 檢測參數清單、多選、選取及動態導覽項目管理。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Recipes.cs | 新建、匯入、套用、更新及另存 Recipe；generation 取消／防過期流程。 |
| IntegratedImageProcessingApp/Forms/DetectionRecipeDialogs.cs | Recipe 名稱、說明、選擇、管理等獨立對話框。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.GoodCondition.cs | 尺寸良品條件規則編輯、算式說明及規則表格。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Measurement.cs | 量測工具與幾何編輯器主面板、量測設定和影像頁整合。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MeasurementRecords.cs | 量測線／量測紀錄建立、更新、排序及表格呈現。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MeasurementCalculation.cs | 尺寸量測計算、MASK 裁切幾何及裁切快取簽章。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MeasurementDrawing.cs | 量測線繪製、方向限制、平行線操作與滑鼠座標轉換。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MaskSource.cs | 尺寸／缺陷共同需要的來源 MASK、影像處理 MASK 與大型 ROI MASK 組合／快取。 |
| IntegratedImageProcessingApp/Forms/MainForm.FlatFieldCalibration.cs | 平場校正頁、取樣狀態、預覽顯示與結果狀態。 |
| IntegratedImageProcessingApp/Forms/MainForm.FlatFieldCalibration.Profile.cs | 平場取樣曲線計算、平滑、設定簽章及保存／載入。 |
| IntegratedImageProcessingApp/Forms/MainForm.FlatFieldCalibration.Correction.cs | 校正後預覽準備、分塊校正與設定／MASK 世代檢查。 |
| IntegratedImageProcessingApp/Forms/MainForm.FlatFieldCalibration.Masks.cs | 平場來源／使用位置 MASK 管理、套用、取消與 overlay。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectDisplay.cs | 各缺陷條件影像分頁的建立、啟用判斷與顯示索引。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectRegion.cs | 共用 ROI 定義、ROI 群組成員／排序、條件指派、有效性驗證及框位顯示。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectCores.cs | 平場及條件一至六的核心設定 UI、對比、前處理、門檻、MASK 及用途標籤。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs | 一般缺陷 MASK 分析、物件尺寸篩選、一般核心／頻域排程及平行協調。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectIntegration.cs | 暗／亮／頻域候選合併、ROI 群組結果表、套用／取消及雙擊聚焦。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectFrequency.cs | 頻域異常一／二 UI、掃描工作區、MASK 視窗查詢、加窗能量、鄰格比較、快取、計時與 overlay。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs | 結果確認 UI、載圖／選參數、執行工作協調、記憶體快照及顯示時間。 |
| IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.GoodJudgement.cs | 結果確認中的逐物件 A/B/NG 尺寸判定、公式運算及狀態按鈕。 |

### 6.4 主程式 Services

| 檔案 | 職責 |
|---|---|
| IntegratedImageProcessingApp/Services/SystemParameterSettings.cs | 所有找物件、物件定義、Recipe、尺寸、平場、缺陷、ROI 群組與頻域參數模型。 |
| IntegratedImageProcessingApp/Services/SystemParameterIniService.cs | SystemParameters.ini 與 Recipe INI 的欄位讀寫、預設值、正規化與舊欄位相容處理。 |
| IntegratedImageProcessingApp/Services/DetectionRecipeCatalogService.cs | Recipe 清單 JSON、參數檔加入、名稱／說明／排序及 ID 對應。 |
| IntegratedImageProcessingApp/Services/InspectionHistoryStore.cs | 每日 SQLite schema／遷移、批次與逐物件明細交易、Recipe 快照 SHA-256 去重、路徑轉換與舊資料讀取。 |
| IntegratedImageProcessingApp/Services/CameraDeviceSettingsIniService.cs | 全域設備精度、站點 ID、本機網路、手動歷史記錄與圖片保留設定的 INI 讀寫／預設值。 |
| IntegratedImageProcessingApp/Services/ConnectionControllerSettingsService.cs | 總控站點表、連線行為 XML 讀寫。 |
| IntegratedImageProcessingApp/Services/InspectionTcpTransport.cs | TCP listener/client、JSON line 訊息、peer／heartbeat／連線事件與傳送。 |
| IntegratedImageProcessingApp/Services/OpenCvImageProcessingService.cs | 通用 OpenCV 影像處理函式。 |
| IntegratedImageProcessingApp/Services/OpenCvThresholdService.cs | OpenCV 門檻與二值影像處理函式。 |
| IntegratedImageProcessingApp/Services/OpenCvEdgeDetectionService.cs | OpenCV 邊緣偵測函式。 |
| IntegratedImageProcessingApp/Services/ProcessedBinaryMaskCache.cs | 已處理二值遮罩快取。 |
| IntegratedImageProcessingApp/Services/LargeImageMaskCache.cs | 大型影像 MASK 結果與 generation 快取。 |
| IntegratedImageProcessingApp/Services/LargeRoiMaskBuilder.cs | 建立大型影像 ROI／來源 MASK。 |
| IntegratedImageProcessingApp/Services/LargeRoiGrayscaleCache.cs | 大圖 ROI 灰階來源及解碼快取。 |
| IntegratedImageProcessingApp/Services/LargeImageGrayscaleDecoder.cs | 大圖灰階像素解碼及格式轉換。 |

### 6.5 CameraCaptureModule Controls、Forms、Models、Services

| 檔案或檔案組 | 職責 |
|---|---|
| CameraCaptureModule/Controls/CameraDisplayControl.cs、CameraCaptureModule/Controls/CameraDisplayControl.Designer.cs | 相機即時影像、取像顯示與原相機操作區 UI。 |
| CameraCaptureModule/Controls/LargeImageSource.cs | CameraCaptureModule 自己的分塊／大圖顯示來源；與主程式 Controls/LargeImageSource 是不同專案中的兩個類別。 |
| CameraCaptureModule/Controls/BufferedRenderPanel.cs | 相機畫面雙緩衝面板。 |
| CameraCaptureModule/Forms/MainForm.cs、CameraCaptureModule/Forms/MainForm.Designer.cs | 相機連線、預覽、擷取、相機設定／波形／米輪視窗、事件轉發及模組內存圖控制。 |
| CameraCaptureModule/Forms/CameraSettingsForm.cs、CameraCaptureModule/Forms/CameraSettingsForm.Designer.cs | 觸發模式、曝光、增益、影像格式、自動保存、顯示預覽、轉送檢測等設定 UI。 |
| CameraCaptureModule/Forms/MeterWheelControlForm.cs、CameraCaptureModule/Forms/MeterWheelControlForm.Designer.cs | 米輪連線、Encoder、Compare 等一般控制。 |
| CameraCaptureModule/Forms/MeterWheelExtensionCompareForm.cs、CameraCaptureModule/Forms/MeterWheelExtensionCompareForm.Designer.cs | 多組米輪 Compare 輸出設定。 |
| CameraCaptureModule/Forms/GrayScaleWaveformForm.cs、CameraCaptureModule/Forms/GrayScaleWaveformForm.Designer.cs | 灰階波形與取像結果檢視。 |
| CameraCaptureModule/Forms/GrayScaleWaveformSelectForm.cs、CameraCaptureModule/Forms/GrayScaleWaveformSelectForm.Designer.cs | 灰階波形／取樣位置選擇。 |
| CameraCaptureModule/Forms/GrayWaveformConfirmForm.cs、CameraCaptureModule/Forms/GrayWaveformConfirmForm.Designer.cs | 波形選擇確認流程。 |
| CameraCaptureModule/Forms/SaveProgressForm.cs | 相機程式存圖進度視窗。 |
| CameraCaptureModule/Sapera/AcqConfigDlg.cs、CameraCaptureModule/Sapera/AcqConfigDlg.Designer.cs | Sapera acquisition 設定視窗。 |
| CameraCaptureModule/Models/CameraSettings.cs | 相機、觸發、自動存圖、影像顯示與米輪設定模型。 |
| CameraCaptureModule/Models/CameraStatus.cs | 相機連線、擷取、預覽與錯誤狀態模型。 |
| CameraCaptureModule/Models/ImageSaveFormat.cs | 影像存檔格式列舉。 |
| CameraCaptureModule/Models/RollingCaptureDirection.cs | 滾動取像的方向列舉。 |
| CameraCaptureModule/Models/TriggerMode.cs | Continuous、Software、External、Single Frame 觸發模式。 |
| CameraCaptureModule/Native/Lsi8181Native.cs | LSI8181 測速輪 native DLL P/Invoke 宣告。 |
| CameraCaptureModule/Services/CameraService.cs | Sapera 相機生命週期、SDK 取像、Mono8 buffer 轉 frame、狀態及 SDK 特徵設定。 |
| CameraCaptureModule/Services/ICameraService.cs | 相機服務操作與事件介面。 |
| CameraCaptureModule/Services/ICameraImageSource.cs | 完整影格事件來源介面。 |
| CameraCaptureModule/Services/CameraImageAcquiredEventArgs.cs | 完整灰階 frame、寬高／stride、序號／時間及 Bitmap 複本產生。 |
| CameraCaptureModule/Services/CameraFrameEventArgs.cs | 相機預覽 Bitmap 事件資料。 |
| CameraCaptureModule/Services/FrameRecorder.cs | 最新／滾動影格緩衝、合成與灰階影像存檔。 |
| CameraCaptureModule/Services/IGrayPixelSource.cs | FrameRecorder 使用的灰階像素來源抽象。 |
| CameraCaptureModule/Services/SettingsService.cs | captureSetting.ini 讀寫與舊設定檔遷移。 |
| CameraCaptureModule/Services/ISettingsService.cs | 相機設定保存介面。 |
| CameraCaptureModule/Services/Lsi8181MeterWheelService.cs | LSI 米輪卡初始化、Encoder、Compare 及硬體輸出封裝。 |
| CameraCaptureModule/Services/GrayWaveformSelectionEventArgs.cs | 波形選取事件資料。 |
| CameraCaptureModule/Services/AppLogger.cs | logs/camera/app.log 檔案記錄。 |
| CameraCaptureModule/Services/UiExceptionHandler.cs | CameraCaptureModule 未處理 UI 例外的集中處理。 |
| CameraCaptureModule/Properties/AssemblyInfo.cs | 相機模組組件中繼資料。 |

## 7. 架構觀察與主要風險

### 已具備的基礎

- 主要設定使用型別化模型；ROI、Recipe、站點使用穩定 ID，減少只靠畫面文字做關聯的問題。
- 大型影像、灰階來源、OpenCV 服務、ROI／MASK 快取及相機模組已拆成獨立檔案或類別。
- 相機 Mono8 frame 有序號、時間、尺寸與 stride；LargeImageSource 固定來源 buffer，透過 OpenCV Mat view 讀取。
- Recipe、目前編輯設定、設備內部設定及總控設定有不同保存檔。

### 需要持續處理的風險

1. **主流程仍與 WinForms UI 緊密耦合。** MainForm partial 共享欄位和可變狀態。partial 只是分檔，尚未形成可單獨重用／測試的 pipeline。
2. **少數檔案承載過多責任。** 目前有 107 個 C# 檔案；MainForm 有 44 個 partial 宣告。主要大檔約為 ResultReview 3,372 行、CameraService 3,303 行、DefectFrequency 3,216 行、DefectRegion 2,877 行、SystemParameterIniService 2,763 行、MainForm 2,658 行。這些行數是 2026-10-28 檢視值；拆分應按責任界線，不設硬性行數門檻。
3. **巢狀平行沒有全域資源預算。** 外層條件、ROI、物件和量測可能同時建立工作；需要用現場影像量測 CPU、記憶體峰值及 UI 延遲，再設全域最大併發。
4. **相機忙碌策略會丟影格。** 目前沒有等待佇列；這符合「忙碌時跳過新影像」的需求，但要驗收設備訊號頻率和略過計數。
5. **相機檢測路徑仍依賴結果確認頁狀態。** MainForm.CameraCapture 目前直接安排結果確認頁的物件／尺寸／缺陷流程，再將結果交給總覽與歷史紀錄。影像演算法輸入已是記憶體 frame，另在背景保存 CameraCapture/yyyyMMdd 原圖。後續應逐步抽出接受不可變 frame 與 Recipe／Parameter snapshot、回傳 InspectionResult 的共用服務，避免把 UI 頁面當成執行引擎。
6. **設定檔缺少明確 schema 版本。** SystemParameterIniService 手動維護許多鍵；Recipe 欄位新增時要檢查舊檔預設、遷移及往返讀寫。
7. **歷史功能已接入，但仍需營運級驗收。** 每日 SQLite、逐物件明細、Recipe 快照、圖片路徑及 ParameterId 重跑已存在；尚需驗收 DB 損毀復原、硬碟滿、圖片遺失、跨日切檔、保留期限、備份與多個程式同時存取。
8. **TCP 尚缺部署級協定保護。** 現有 line-delimited JSON 沒有 TLS、身分驗證、協定版本和重複訊息持久化策略；需依整合程式規格決定。
9. **存圖設定與實作有差異。** CameraSettings.SaveFolder 會讀寫但目前存圖路徑不使用它；一般手動單張存圖也沒有日期目錄。
10. **測試與硬體證據不足。** Solution 沒有測試專案。離線合成資料不能代表公司影像；成功編譯也不代表相機、米輪、TCP 或算法品質已驗收。

## 8. 接手後建議順序

1. 先以代表性影像驗證找物件、ROI 群組映射、尺寸標定、缺陷誤報／漏檢和逐物件判定。
2. 用目標相機與米輪驗收連線、Software Trigger 原點確認／Encoder Clear、External Trigger、預覽開關、連續取像、持續檢測與忙碌略過。
3. 用兩台以上實際執行個體驗收 TCP 站點 ID、listener IP／port、命令 ack、重連、心跳與每站逐物件資料。
4. 驗收已實作的歷史資料流程：相機成功／待確認紀錄、手動結果確認勾選保存、每日資料庫、圖片路徑、Recipe 快照、ParameterId 遷移及多參數歷史重跑。
5. 先建立明確的檢測輸入／輸出資料契約，再抽離相機、手動載圖共用的 InspectionPipeline；禁止讓頁面切換決定檢測是否執行。
6. 設定全流程併發與影像副本上限；以真實大圖測量每張牆鐘時間、CPU、RAM、落後／丟棄影格數。
7. 逐步拆出純計算／序列化元件，並為 Recipe 舊檔、尺寸公式、ROI 群組、缺陷結果和歷史重跑建立可重複驗證。
8. 確認相機 SaveFolder 實際使用方式、影像保存失敗行為、歷史圖片保留期限及現場 config 更新策略。

## 9. 建置與維護

環境需求：

- Visual Studio／MSBuild 與 .NET Framework 4.7.2 Developer Pack。
- NuGet 套件；OpenCvSharp4 與 OpenCvSharp4.runtime.win 版本為 4.10.0.20240616。
- Sapera SDK 的 DALSA.SaperaLT.SapClassBasic.dll。
- LSI8181_64.dll 與相容的相機／米輪驅動和硬體。

兩個專案在 Any CPU solution configuration 下仍設定為 x64。預設 SDK 搜尋位置位於這個 repository 的相鄰使用者專案路徑；換開發機時應設定 CameraSdkDirectory 和 LsiNativeDirectory。

~~~powershell
msbuild .\MyApp4.sln /t:Build /p:Configuration=Debug /p:Platform="Any CPU" /p:CameraSdkDirectory="C:\path\to\Sapera" /p:LsiNativeDirectory="C:\path\to\LSI\x64"
~~~

維護注意事項：

- 修改 SystemParameterSettings 時，同步檢查 SystemParameterIniService、Recipe 序列化、預設值、舊檔相容及 UI 綁定。
- 新增 .cs 時同步更新對應 csproj 的 Compile 清單。
- OpenCV Mat、Bitmap、大型影像來源和外部 pinned buffer 必須明確釋放；勿讓預覽、overlay 或快取長期持有不必要的大圖。
- 套用 Recipe、切換圖片或修改 ROI 後，確認相關 generation／signature／結果快照確實失效。
- 設備 config 位於執行檔旁；部署更新要避免覆蓋現場值。缺少精度時 1 mm/pixel 是預設值，不是實際校正值。
- 建置／檢視文件不等同現場驗收。本次文件更新沒有重新執行建置、GUI、相機或影像測試；最近一次程式版本曾在替代輸出目錄建置成功。

## 10. 大檔案拆分評估與建議

### 結論

可以拆，而且值得逐步拆。現在的主要問題不是單純「檔案多」，而是少數檔案同時包 UI 建構、流程協調、計算、快取／生命週期和資料保存。另一方面，44 個 MainForm partial 仍共用同一批欄位；只把方法搬到另一個 partial 或換檔名，會減少單檔行數，但不會降低模組耦合。

因此建議先抽出有明確輸入／輸出的純計算或持久化責任，讓 UI 收集參數、呼叫服務、顯示結果。每一步都保留既有參數與畫面行為，避免一次重寫檢測流程。現有方案沒有自動化測試專案，拆分演算法前應先建立少量可重播的合成影像與歷史資料案例；這是降低回歸風險的前置工作，不應把「編譯成功」當作結果等價證明。

### 建議優先順序

| 優先 | 目標檔案／責任 | 建議邊界 | 風險 |
|---|---|---|---|
| 1 | MainForm.DetectionParameter.DefectFrequency.cs（3,216 行） | 將掃描格座標、加窗能量、統計尺度、鄰格比較抽成 FrequencyAnomalyAnalyzer；輸入明確的來源、ROI work items 與設定，輸出候選格／分數／計時資料。WinForms 控制項、預覽和繪圖留在頁面層。先搬資料型別，再搬純計算，保持 ROI 邊界與縮放規則不變。 | 中高。這是影像檢出核心，任何座標、邊界填補、計分順序差異都可能改變結果。 |
| 2 | MainForm.DetectionParameter.ResultReview.cs（3,372 行） | 先抽「尺寸量測計算」與良品公式 parser 為獨立服務；再把執行確認協調抽成 ResultReviewRunner，回傳不可變結果快照。頁面只負責參數選擇、進度、取消與呈現。歷史 ParameterId 固定後再動這條主線。 | 高。它控制取消世代、參數 ID、物件搜尋等待、尺寸／缺陷平行、影像 generation 和 UI snapshot；不宜一次搬整檔。 |
| 3 | SystemParameterIniService.cs（2,763 行） | 保留外部 Load／Save façade，內部分成 INI 文件 parser／writer、Object Definition codec、Detection Parameter codec、共用 migration／reference repair。先抽不改格式的 helper，再驗證舊檔載入與保存往返一致。 | 中高。預設值、舊鍵遷移、ID 修復及引用關聯都集中在此；需要舊 Recipe 範例作回歸。 |
| 4 | MainForm.DetectionParameter.DefectRegion.cs（2,877 行） | 把 ROI group membership、名稱唯一性、ID 解析和啟用條件驗證抽成純 DefectRoiGroupResolver／Validator；畫框、拖曳、overlay 再考慮移到專用 UserControl。 | 中。ROI 編輯座標和運算範圍必須一致；先抽資料規則比搬滑鼠互動安全。 |
| 5 | CameraCaptureModule/Services/CameraService.cs（3,303 行） | 分開 Sapera 裝置探索／診斷、相機 feature 設定、取像生命週期；CameraService 留作協調器。不要只按行數切 helper，先用實機覆蓋 Software／External Trigger、Encoder Clear、connect／disconnect 和故障復原。 | 很高。SDK 資源生命週期與硬體設定相依，必須有可用相機／米輪才能可靠驗證。 |
| 6 | MainForm.cs、ImageDisplayControl.cs、ObjectJudgementProcessing.cs（約 2,100–2,658 行） | MainForm 保持視窗組裝與導航，將共享狀態逐步移到功能控制器／狀態物件；影像控制項分開 renderer、viewport input 與 overlay；區塊處理分開純 OpenCV 運算與 UI。 | 高。畫面狀態、快取、影像參照計數及跨頁同步容易被破壞。 |

歷史頁目前已分成 MainForm.InspectionHistory.cs（1,331 行 UI／協調）與 InspectionHistoryStore.cs（508 行 SQLite／資料模型）；這個界線比把 DB SQL 留在 MainForm 更清楚。下一步若再拆，可把歷史記錄 DTO 獨立成 model，但不應為了湊小檔而過度拆成大量單用途檔。

### 不建議的拆法與實作順序

- 不要為降低行數，把 private helper 搬到另一個 MainForm partial 後宣稱已完成模組化；它仍然共享主視窗全部狀態。
- 不要一次重寫頻域、ResultReview、CameraService。先從 pure function／DTO／codec 開始，維持公開呼叫介面。
- Designer 產生檔（如 MainForm.Designer.cs）不應手動大幅重排；版面重構應用 WinForms designer 或新增專用 UserControl。
- 新 .cs 檔須加入舊式 csproj 的 Compile 清單，並一併更新此文件檔案職責表。
- 建議每次只拆一個責任，先記錄代表性輸入的結果與計時，再搬移、比對輸出、建置並用實際 UI／影像驗收。先把 History 的 ParameterId 選擇與舊 DB 遷移驗收完成，再碰共用參數／Recipe 持久化。

### 本次程式檔案修改

目前工作目錄未提交的程式／設定修改：

- MainForm.InspectionHistory.cs：增加摘要卡下方留白並使用單一框線；手動結果確認關閉圖片保留時，仍寫入歷史判定及明細但不另存圖片副本。
- MainForm.GlobalSettings.cs：歷史紀錄群組新增「保留參數結果確認的圖片」核取方塊及套用保存。
- CameraDeviceSettingsIniService.cs、config/device-settings.ini：保存圖片保留設定；舊設定檔缺少此鍵時預設保留，以延續原本行為。

以上修改仍在本機工作目錄，尚未提交／推送；需建置及檢查實際畫面後再確認。
