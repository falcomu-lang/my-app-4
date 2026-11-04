# 整合式影像處理軟體程式對照表

文件版本 1.1　核對日期 2026-11-04　程式基準：目前工作目錄，含未提交修改

以下位置由目前檔案實際搜尋取得。連結相對於專案，附 GitHub 行號錨點。行號可能隨後續修改位移；維護時以方法或運算式重新定位。這是靜態對照，不表示已執行驗收。

## 規則與來源

| 編號 | 規則或責任 | 程式位置 | 定位內容 |
|---|---|---|---|
| S01 | 角色範圍與保存 | [MainForm.Navigation.cs:35](../IntegratedImageProcessingApp/Forms/MainForm.Navigation.cs#L35) | private void InitializeMainFormRoleSwitcher |
| S02 | 全域精度與名稱 | [MainForm.GlobalSettings.cs:27](../IntegratedImageProcessingApp/Forms/MainForm.GlobalSettings.cs#L27) | private double GetCameraXMillimetersPerPixel |
| S03 | 設備設定預設 | [CameraDeviceSettingsIniService.cs:10](../IntegratedImageProcessingApp/Services/CameraDeviceSettingsIniService.cs#L10) | public CameraDeviceSettings() |
| S04 | 總控預設 | [ConnectionControllerSettingsService.cs:11](../IntegratedImageProcessingApp/Services/ConnectionControllerSettingsService.cs#L11) | public ConnectionControllerSettings() |
| S05 | 參數模型與一般核心預設 | [SystemParameterSettings.cs:292](../IntegratedImageProcessingApp/Services/SystemParameterSettings.cs#L292) | public ObjectDetectionDefectCoreSettings() |
| S06 | 頻域一預設 | [SystemParameterSettings.cs:466](../IntegratedImageProcessingApp/Services/SystemParameterSettings.cs#L466) | public ObjectDetectionParameterSettings() |
| S07 | 頻域二預設 | [SystemParameterSettings.cs:808](../IntegratedImageProcessingApp/Services/SystemParameterSettings.cs#L808) | public ObjectDetectionFrequencySettings() |
| S08 | 工作內容更新到 Recipe | [MainForm.DetectionParameter.Recipes.cs:204](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Recipes.cs#L204) | private void UpdateCurrentDetectionParameterRecipe |
| S09 | 新建與全域保留 | [MainForm.DetectionParameter.Recipes.cs:271](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Recipes.cs#L271) | private void CreateNewDetectionParameter |
| S10 | Recipe 套用重跑 | [MainForm.DetectionParameter.Recipes.cs:548](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.Recipes.cs#L548) | ReprocessAppliedDetectionRecipeAsync( |
| S11 | Recipe 清單 | [DetectionRecipeDialogs.cs:11](../IntegratedImageProcessingApp/Forms/DetectionRecipeDialogs.cs#L11) | class  |
| S12 | Recipe 快照 | [DetectionRecipeCatalogService.cs:175](../IntegratedImageProcessingApp/Services/DetectionRecipeCatalogService.cs#L175) | SaveRecipeSnapshot( |
| S13 | 共用缺陷 ROI 與群組 UI | [MainForm.DetectionParameter.DefectRegion.cs:137](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectRegion.cs#L137) | Text = "檢測範圍組" |
| S14 | Ctrl 加左鍵編輯 | [MainForm.DetectionParameter.DefectRegion.cs:2156](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectRegion.cs#L2156) | (e.Modifiers & Keys.Control) |
| S15 | MASK 來源 | [MainForm.DetectionParameter.MaskSource.cs:18](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MaskSource.cs#L18) | partial class MainForm |
| S16 | 尺寸 mm 轉換 | [MainForm.DetectionParameter.MeasurementCalculation.cs:693](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.MeasurementCalculation.cs#L693) | private static double ConvertObjectDetectionPixelsToMillimeters |
| S17 | 量測語法與未命中 0 樣本 | [MainForm.DetectionParameter.GoodCondition.cs:346](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.GoodCondition.cs#L346) | private static string BuildObjectDetectionGoodJudgementSyntaxHelpText |
| S18 | A 全通與 A 未知 | [MainForm.DetectionParameter.ResultReview.GoodJudgement.cs:221](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.GoodJudgement.cs#L221) | if (configuredACount > 0 && !hasAFailure |
| S19 | 設定驗證 | [MainForm.DetectionParameter.GoodCondition.cs:578](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.GoodCondition.cs#L578) | ValidateObjectDetectionGoodJudgement |
| S20 | 執行 parser | [MainForm.DetectionParameter.ResultReview.cs:3319](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L3319) | class ObjectDetectionGoodJudgementExpressionParser |
| S21 | 手動讀圖參考尺寸 | [MainForm.DetectionParameter.ResultReview.cs:899](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L899) | dialog.Filter = "圖片檔案 |
| S22 | 讀圖後自動執行 | [MainForm.DetectionParameter.ResultReview.cs:1005](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L1005) | if (imageLoadedSuccessfully && IsObjectDetectionResultReviewUiAvailable()) |
| S23 | 完整確認流程 | [MainForm.DetectionParameter.ResultReview.cs:575](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L575) | RunObjectDetectionResultReviewAsync( |
| S24 | 計時呈現 | [MainForm.DetectionParameter.ResultReview.cs:1668](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L1668) | AppendDebugTimingMemo("影像處理總時間 |
| S25 | 缺陷逐片判定 | [MainForm.DetectionParameter.ResultReview.cs:2137](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.ResultReview.cs#L2137) | GetObjectDetectionResultReviewDefectObjectGrades() |
| S26 | 平場保存資料準備 | [MainForm.FlatFieldCalibration.Correction.cs:141](../IntegratedImageProcessingApp/Forms/MainForm.FlatFieldCalibration.Correction.cs#L141) | ShowSavedObjectDetectionFlatFieldCalibration |
| S27 | 一般核心對比與前處理 | [MainForm.DetectionParameter.DefectProcessing.cs:1207](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs#L1207) | gray.ConvertTo(adjusted |
| S28 | 局部背景差分 | [MainForm.DetectionParameter.DefectProcessing.cs:1418](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs#L1418) | private static Cv.Mat ApplyObjectDetectionDefectEnhancement |
| S29 | CLAHE | [MainForm.DetectionParameter.DefectProcessing.cs:1478](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs#L1478) | private static Cv.Mat ApplyObjectDetectionDefectClahe |
| S30 | 門檻與有效區限制 | [MainForm.DetectionParameter.DefectProcessing.cs:1662](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs#L1662) | Cv.Cv2.InRange( |
| S31 | 缺陷 mm 外接框篩選 | [MainForm.DetectionParameter.DefectProcessing.cs:1695](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectProcessing.cs#L1695) | double widthMillimeters = width |
| S32 | 平場核心 UI 對比限制 | [MainForm.DetectionParameter.DefectCores.cs:679](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectCores.cs#L679) | gain.Enabled = !isFlatFieldCore |
| S33 | 頻域掃描與 score | [MainForm.DetectionParameter.DefectFrequency.cs:2394](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectFrequency.cs#L2394) | cell.Score = Math.Abs(cell.Energy - median) / scale |
| S34 | 尾端完整視窗 | [MainForm.DetectionParameter.DefectFrequency.cs:2703](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectFrequency.cs#L2703) | private static List<int> CreateFrequencyScanStarts |
| S35 | 有效 MASK 與能量 | [MainForm.DetectionParameter.DefectFrequency.cs:2854](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectFrequency.cs#L2854) | private static unsafe bool TryCalculateObjectDetectionFrequencyEnergy |
| S36 | 鄰格分數 | [MainForm.DetectionParameter.DefectFrequency.cs:2601](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectFrequency.cs#L2601) | cell.NeighborScore = Math.Max |
| S37 | 整合與候選合併 | [MainForm.DetectionParameter.DefectIntegration.cs:548](../IntegratedImageProcessingApp/Forms/MainForm.DetectionParameter.DefectIntegration.cs#L548) | MergeObjectDetectionDefectIntegrationCandidates( |
| S38 | 綜合逐片等級 | [MainForm.InspectionOverview.cs:940](../IntegratedImageProcessingApp/Forms/MainForm.InspectionOverview.cs#L940) | private static ResultReviewGoodJudgementGrade GetInspectionOverviewCombinedGrade |
| S39 | 總覽切頁風險 | [MainForm.InspectionOverview.cs:216](../IntegratedImageProcessingApp/Forms/MainForm.InspectionOverview.cs#L216) | private void ExitInspectionOverviewMode |
| S40 | 總覽單雙擊 | [MainForm.InspectionOverview.cs:1127](../IntegratedImageProcessingApp/Forms/MainForm.InspectionOverview.cs#L1127) | private void InspectionOverviewTile_DoubleClick |
| S41 | 相機記憶體入口與 Y 高度限制 | [MainForm.CameraCapture.cs:122](../IntegratedImageProcessingApp/Forms/MainForm.CameraCapture.cs#L122) | bool imageYHeightMismatch |
| S42 | 相機大圖命名 | [MainForm.CameraCapture.cs:412](../IntegratedImageProcessingApp/Forms/MainForm.CameraCapture.cs#L412) | private static string CreateCameraInspectionImagePath |
| S43 | 背景 TIFF 保存 | [MainForm.CameraCapture.cs:424](../IntegratedImageProcessingApp/Forms/MainForm.CameraCapture.cs#L424) | private void StartCameraInspectionImageSave |
| S44 | host 訂閱 | [CameraCaptureHostControl.cs:109](../IntegratedImageProcessingApp/Controls/CameraCaptureHostControl.cs#L109) | UpdateCameraImageAcquiredSubscription |
| S45 | 大圖記憶體 buffer | [LargeImageSource.cs:80](../IntegratedImageProcessingApp/Controls/LargeImageSource.cs#L80) | public LargeImageSource(byte[] grayscaleBuffer |
| S46 | 歷史批次模型 | [InspectionHistoryStore.cs:44](../IntegratedImageProcessingApp/Services/InspectionHistoryStore.cs#L44) | public sealed class InspectionHistoryBatchRecord |
| S47 | 每日 SQLite 與快照 | [InspectionHistoryStore.cs:84](../IntegratedImageProcessingApp/Services/InspectionHistoryStore.cs#L84) | private const string SqliteLibrary |
| S48 | 手動歷史與圖片保存 | [MainForm.InspectionHistory.cs:769](../IntegratedImageProcessingApp/Forms/MainForm.InspectionHistory.cs#L769) | private void SaveManualObjectDetectionResultReviewHistory |
| S49 | 歷史右鍵重跑 | [MainForm.InspectionHistory.cs:473](../IntegratedImageProcessingApp/Forms/MainForm.InspectionHistory.cs#L473) | inspectionHistoryContextMenu.Items.Add("使用當時參數重新檢測" |
| S50 | 歷史 ParameterId 選取 | [MainForm.InspectionHistory.cs:1106](../IntegratedImageProcessingApp/Forms/MainForm.InspectionHistory.cs#L1106) | record.ParameterId |
| S51 | TCP 合約 | [InspectionTcpTransport.cs:16](../IntegratedImageProcessingApp/Services/InspectionTcpTransport.cs#L16) | public sealed class InspectionTcpMessage |
| S52 | TCP peer 清理比對連線實例，避免舊 peer 移除新 peer | [InspectionTcpTransport.cs:602](../IntegratedImageProcessingApp/Services/InspectionTcpTransport.cs#L602) | ReferenceEquals(current, peer) |
| S53 | TCP heartbeat 用於存活；逐筆結果另走 ACK／重送 | [InspectionTcpTransport.cs:467](../IntegratedImageProcessingApp/Services/InspectionTcpTransport.cs#L467) | if (!string.Equals(message.MessageType, "heartbeat" |
| S54 | TCP 狀態節流 | [MainForm.ConnectionController.cs:1605](../IntegratedImageProcessingApp/Forms/MainForm.ConnectionController.cs#L1605) | private void QueueStationStatusPublish |
| S55 | TCP 命令 | [MainForm.ConnectionController.cs:1231](../IntegratedImageProcessingApp/Forms/MainForm.ConnectionController.cs#L1231) | case "start-inspection" |
| S56 | 取像 SDK | [CameraService.cs:15](../CameraCaptureModule/Services/CameraService.cs#L15) | class CameraService |
| S57 | 相機設定 UI | [CameraSettingsForm.Designer.cs:168](../CameraCaptureModule/Forms/CameraSettingsForm.Designer.cs#L168) | this.tabPageConnection.Text |
| S58 | 米輪控制 UI | [MeterWheelControlForm.Designer.cs:172](../CameraCaptureModule/Forms/MeterWheelControlForm.Designer.cs#L172) | this.buttonClearEncoder.Text |
| S59 | TCP command HMAC 驗證、目標／時間戳／重播檢查 | [MainForm.ConnectionController.cs:1645](../IntegratedImageProcessingApp/Forms/MainForm.ConnectionController.cs#L1645) | private bool TryAuthorizeRemoteCommand |
| S60 | 應用程式平行工作共同上限 | [InspectionParallelism.cs:11](../IntegratedImageProcessingApp/Services/InspectionParallelism.cs#L11) | public static class InspectionParallelism |
| S61 | TCP 結果 outbox／inbox 與 ResultId 去重 | [InspectionTcpResultStore.cs:16](../IntegratedImageProcessingApp/Services/InspectionTcpResultStore.cs#L16) | public sealed class InspectionTcpResultStore |

## 核對範圍

目前架構、功能規格與操作手冊的關鍵規則對應上述程式。行號是 2026-11-04 工作目錄核對值，後續修改可能移動；以方法名稱重新定位。完整自有檔案職責與大型方法盤點見根目錄 PROJECT_HANDOFF.md 及 ARCHITECTURE_REVIEW.md。此表不把第三方 SDK 原始碼列為自有程式，也不宣稱全部硬體 feature 都已實測。
