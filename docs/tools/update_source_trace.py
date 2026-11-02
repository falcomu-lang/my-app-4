"""Regenerate verified relative source links for the documentation."""
from pathlib import Path

ROOT = Path(__file__).resolve().parents[2]
FORM = "IntegratedImageProcessingApp/Forms/"
SERVICE = "IntegratedImageProcessingApp/Services/"
CONTROL = "IntegratedImageProcessingApp/Controls/"

ENTRIES = [
    ("角色範圍與保存", FORM + "MainForm.Navigation.cs", "private void InitializeMainFormRoleSwitcher"),
    ("全域精度與名稱", FORM + "MainForm.GlobalSettings.cs", "private double GetCameraXMillimetersPerPixel"),
    ("設備設定預設", SERVICE + "CameraDeviceSettingsIniService.cs", "public CameraDeviceSettings()"),
    ("總控預設", SERVICE + "ConnectionControllerSettingsService.cs", "public ConnectionControllerSettings()"),
    ("參數模型與一般核心預設", SERVICE + "SystemParameterSettings.cs", "public ObjectDetectionDefectCoreSettings()"),
    ("頻域一預設", SERVICE + "SystemParameterSettings.cs", "public ObjectDetectionParameterSettings()"),
    ("頻域二預設", SERVICE + "SystemParameterSettings.cs", "public ObjectDetectionFrequencySettings()"),
    ("工作內容更新到 Recipe", FORM + "MainForm.DetectionParameter.Recipes.cs", "private void UpdateCurrentDetectionParameterRecipe"),
    ("新建與全域保留", FORM + "MainForm.DetectionParameter.Recipes.cs", "private void CreateNewDetectionParameter"),
    ("Recipe 套用重跑", FORM + "MainForm.DetectionParameter.Recipes.cs", "ReprocessAppliedDetectionRecipeAsync("),
    ("Recipe 清單", FORM + "DetectionRecipeDialogs.cs", "class "),
    ("Recipe 快照", SERVICE + "DetectionRecipeCatalogService.cs", "SaveRecipeSnapshot("),
    ("共用缺陷 ROI 與群組 UI", FORM + "MainForm.DetectionParameter.DefectRegion.cs", 'Text = "檢測範圍組"'),
    ("Ctrl 加左鍵編輯", FORM + "MainForm.DetectionParameter.DefectRegion.cs", "(e.Modifiers & Keys.Control)"),
    ("MASK 來源", FORM + "MainForm.DetectionParameter.MaskSource.cs", "partial class MainForm"),
    ("尺寸 mm 轉換", FORM + "MainForm.DetectionParameter.MeasurementCalculation.cs", "private static double ConvertObjectDetectionPixelsToMillimeters"),
    ("量測語法與未命中 0 樣本", FORM + "MainForm.DetectionParameter.GoodCondition.cs", "private static string BuildObjectDetectionGoodJudgementSyntaxHelpText"),
    ("A 全通與 A 未知", FORM + "MainForm.DetectionParameter.ResultReview.GoodJudgement.cs", "if (configuredACount > 0 && !hasAFailure"),
    ("設定驗證", FORM + "MainForm.DetectionParameter.GoodCondition.cs", "ValidateObjectDetectionGoodJudgement"),
    ("執行 parser", FORM + "MainForm.DetectionParameter.ResultReview.cs", "class ObjectDetectionGoodJudgementExpressionParser"),
    ("手動讀圖參考尺寸", FORM + "MainForm.DetectionParameter.ResultReview.cs", 'dialog.Filter = "圖片檔案'),
    ("讀圖後自動執行", FORM + "MainForm.DetectionParameter.ResultReview.cs", "if (imageLoadedSuccessfully && IsObjectDetectionResultReviewUiAvailable())"),
    ("完整確認流程", FORM + "MainForm.DetectionParameter.ResultReview.cs", "RunObjectDetectionResultReviewAsync("),
    ("計時呈現", FORM + "MainForm.DetectionParameter.ResultReview.cs", 'AppendDebugTimingMemo("影像處理總時間'),
    ("缺陷逐片判定", FORM + "MainForm.DetectionParameter.ResultReview.cs", "GetObjectDetectionResultReviewDefectObjectGrades()"),
    ("平場保存資料準備", FORM + "MainForm.FlatFieldCalibration.Correction.cs", "ShowSavedObjectDetectionFlatFieldCalibration"),
    ("一般核心對比與前處理", FORM + "MainForm.DetectionParameter.DefectProcessing.cs", "gray.ConvertTo(adjusted"),
    ("局部背景差分", FORM + "MainForm.DetectionParameter.DefectProcessing.cs", "private static Cv.Mat ApplyObjectDetectionDefectEnhancement"),
    ("CLAHE", FORM + "MainForm.DetectionParameter.DefectProcessing.cs", "private static Cv.Mat ApplyObjectDetectionDefectClahe"),
    ("門檻與有效區限制", FORM + "MainForm.DetectionParameter.DefectProcessing.cs", "Cv.Cv2.InRange("),
    ("缺陷 mm 外接框篩選", FORM + "MainForm.DetectionParameter.DefectProcessing.cs", "double widthMillimeters = width"),
    ("平場核心 UI 對比限制", FORM + "MainForm.DetectionParameter.DefectCores.cs", "gain.Enabled = !isFlatFieldCore"),
    ("頻域掃描與 score", FORM + "MainForm.DetectionParameter.DefectFrequency.cs", "cell.Score = Math.Abs(cell.Energy - median) / scale"),
    ("尾端完整視窗", FORM + "MainForm.DetectionParameter.DefectFrequency.cs", "private static List<int> CreateFrequencyScanStarts"),
    ("有效 MASK 與能量", FORM + "MainForm.DetectionParameter.DefectFrequency.cs", "private static unsafe bool TryCalculateObjectDetectionFrequencyEnergy"),
    ("鄰格分數", FORM + "MainForm.DetectionParameter.DefectFrequency.cs", "cell.NeighborScore = Math.Max"),
    ("整合與候選合併", FORM + "MainForm.DetectionParameter.DefectIntegration.cs", "MergeObjectDetectionDefectIntegrationCandidates("),
    ("綜合逐片等級", FORM + "MainForm.InspectionOverview.cs", "private static ResultReviewGoodJudgementGrade GetInspectionOverviewCombinedGrade"),
    ("總覽切頁風險", FORM + "MainForm.InspectionOverview.cs", "private void ExitInspectionOverviewMode"),
    ("總覽單雙擊", FORM + "MainForm.InspectionOverview.cs", "private void InspectionOverviewTile_DoubleClick"),
    ("相機記憶體入口與 Y 高度限制", FORM + "MainForm.CameraCapture.cs", "bool imageYHeightMismatch"),
    ("相機大圖命名", FORM + "MainForm.CameraCapture.cs", "private static string CreateCameraInspectionImagePath"),
    ("背景 TIFF 保存", FORM + "MainForm.CameraCapture.cs", "private void StartCameraInspectionImageSave"),
    ("host 訂閱", CONTROL + "CameraCaptureHostControl.cs", "UpdateCameraImageAcquiredSubscription"),
    ("大圖記憶體 buffer", CONTROL + "LargeImageSource.cs", "public LargeImageSource(byte[] grayscaleBuffer"),
    ("歷史批次模型", SERVICE + "InspectionHistoryStore.cs", "public sealed class InspectionHistoryBatchRecord"),
    ("每日 SQLite 與快照", SERVICE + "InspectionHistoryStore.cs", 'private const string SqliteLibrary'),
    ("手動歷史與圖片保存", FORM + "MainForm.InspectionHistory.cs", "private void SaveManualObjectDetectionResultReviewHistory"),
    ("歷史右鍵重跑", FORM + "MainForm.InspectionHistory.cs", 'inspectionHistoryContextMenu.Items.Add("使用當時參數重新檢測"'),
    ("歷史 ParameterId 選取", FORM + "MainForm.InspectionHistory.cs", "record.ParameterId"),
    ("TCP 合約", SERVICE + "InspectionTcpTransport.cs", "public sealed class InspectionTcpMessage"),
    ("TCP 重連清理風險", SERVICE + "InspectionTcpTransport.cs", "private void RemovePeer"),
    ("TCP 心跳只保活", SERVICE + "InspectionTcpTransport.cs", 'if (!string.Equals(message.MessageType, "heartbeat"'),
    ("TCP 狀態節流", FORM + "MainForm.ConnectionController.cs", "private void QueueStationStatusPublish"),
    ("TCP 命令", FORM + "MainForm.ConnectionController.cs", 'case "start-inspection"'),
    ("取像 SDK", "CameraCaptureModule/Services/CameraService.cs", "class CameraService"),
    ("相機設定 UI", "CameraCaptureModule/Forms/CameraSettingsForm.Designer.cs", 'this.tabPageConnection.Text'),
    ("米輪控制 UI", "CameraCaptureModule/Forms/MeterWheelControlForm.Designer.cs", 'this.buttonClearEncoder.Text'),
]

lines = [
    "# 整合式影像處理軟體程式對照表", "",
    "文件版本 1.0　核對日期 2026-10-30　程式基準 b512791", "",
    "以下位置由目前檔案實際搜尋取得。連結相對於專案，附 GitHub 行號錨點。行號可能隨後續修改位移；維護時以方法或運算式重新定位。這是靜態對照，不表示已執行驗收。", "",
    "## 規則與來源", "",
    "| 編號 | 規則或責任 | 程式位置 | 定位內容 |",
    "|---|---|---|---|",
]
for n, (rule, relative, anchor) in enumerate(ENTRIES, 1):
    source = (ROOT / relative).read_text(encoding="utf-8-sig")
    pos = source.find(anchor)
    if pos < 0:
        raise ValueError(f"Missing source anchor: {relative}: {anchor}")
    line = source.count("\n", 0, pos) + 1
    filename = Path(relative).name
    lines.append(f"| S{n:02d} | {rule} | [{filename}:{line}](../{relative}#L{line}) | {anchor.replace('|', '/')} |")
lines += ["", "## 核對範圍", "",
          "目前架構、功能規格與操作手冊的關鍵規則對應上述程式。完整自有檔案職責與大型方法盤點見根目錄 PROJECT_HANDOFF.md 及 ARCHITECTURE_REVIEW.md。此表不把第三方 SDK 原始碼列為自有程式，也不宣稱全部硬體 feature 都已實測。", ""]
(ROOT / "docs" / "SOURCE_TRACE.md").write_text("\n".join(lines), encoding="utf-8")
print(f"Verified {len(ENTRIES)} source anchors")
