using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ObjectDetectionDefectContour
        {
            public int ObjectNumber { get; set; }

            public bool IsBright { get; set; }

            public int ComponentLabel { get; set; }

            public PointF[] Points { get; set; }

            public RectangleF Bounds { get; set; }
        }

        private sealed class ObjectDetectionDefectCoreResult
        {
            public string Signature { get; set; }

            public long ElapsedMilliseconds { get; set; }

            public long TotalElapsedMilliseconds { get; set; }

            public long RoiPreparationElapsedMilliseconds { get; set; }

            public long ContrastAdjustmentElapsedMilliseconds { get; set; }

            public long PreprocessingElapsedMilliseconds { get; set; }

            public long DefectAnalysisElapsedMilliseconds { get; set; }

            public long PreviewGenerationElapsedMilliseconds { get; set; }

            public bool ParallelExecutionEnabled { get; set; }

            public int DetectedComponentCount { get; set; }

            public List<ObjectDetectionDefectContour> Contours { get; set; }

            public List<ObjectDetectionDefectProcessedPatch> ProcessedPatches { get; set; }
        }

        private sealed class ObjectDetectionDefectProcessedPatch
        {
            public Rectangle Bounds { get; set; }

            public PointF[] InspectionPolygon { get; set; }

            public Bitmap ProcessedImage { get; set; }
        }

        private sealed class ObjectDetectionDefectPerObjectResult
        {
            public List<ObjectDetectionDefectContour>[] ContoursByCore { get; set; }

            public long[] ElapsedTicksByCore { get; set; }

            public long RoiPreparationElapsedTicks { get; set; }

            public long[] ContrastAdjustmentElapsedTicksByCore { get; set; }

            public long[] PreprocessingElapsedTicksByCore { get; set; }

            public long[] DefectAnalysisElapsedTicksByCore { get; set; }

            public long[] PreviewGenerationElapsedTicksByCore { get; set; }

            public ObjectDetectionDefectProcessedPatch[] ProcessedPatchesByCore { get; set; }
        }

        private bool objectDetectionDefectProcessingRequested;
        private bool objectDetectionDefectLastRunCompleted;
        private readonly Dictionary<string, Dictionary<string, ObjectDetectionDefectCoreResult>>
            objectDetectionDefectCoreResults =
                new Dictionary<string, Dictionary<string, ObjectDetectionDefectCoreResult>>(StringComparer.Ordinal);

        private async Task RunObjectDetectionDefectProcessingAsync(
            string parameterId,
            string coreKey)
        {
            if (objectDetectionDefectProcessingRequested)
            {
                return;
            }
            objectDetectionDefectProcessingRequested = true;
            objectDetectionDefectLastRunCompleted = false;
            if (objectDetectionDefectRunButton != null)
            {
                objectDetectionDefectRunButton.Enabled = false;
            }
            if (objectDetectionDefectParallelExecutionCheckBox != null)
            {
                objectDetectionDefectParallelExecutionCheckBox.Enabled = false;
            }
            if (objectDetectionDefectCoreTabs != null && !objectDetectionDefectCoreTabs.IsDisposed)
            {
                objectDetectionDefectCoreTabs.Enabled = false;
            }

            try
            {
                if (statusLabel != null)
                {
                    statusLabel.Text = "缺陷檢測：正在準備運算...";
                }
                SetObjectDetectionDefectRegionStatus("正在準備缺陷檢測；請稍候...");
                await Task.Yield();
                await RunObjectDetectionDefectProcessingCoreAsync(parameterId, coreKey);
            }
            finally
            {
                objectDetectionDefectProcessingRequested = false;
                if (!IsDisposed)
                {
                    if (objectDetectionDefectCoreTabs != null && !objectDetectionDefectCoreTabs.IsDisposed)
                    {
                        objectDetectionDefectCoreTabs.Enabled = true;
                    }
                    ObjectDetectionParameterSettings current =
                        FindObjectDetectionParameter(activeObjectDetectionParameterId);
                    UpdateObjectDetectionDefectInspectionRegionControls(current);
                    if (objectDetectionDefectLastRunCompleted)
                    {
                        objectDetectionDefectLastRunCompleted = false;
                        UpdateObjectDetectionDefectProcessingStatus();
                    }
                }
            }
        }

        private async Task RunObjectDetectionDefectProcessingCoreAsync(
            string parameterId,
            string coreKey)
        {

            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                statusLabel.Text = "找不到指定的檢測參數。";
                return;
            }

            if (!parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                statusLabel.Text = "請先框選並套用缺陷檢測範圍。";
                SetObjectDetectionDefectRegionStatus("尚未設定有效的檢測範圍。請先框選並套用。");
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                statusLabel.Text = "請先在主參數設定指定物件定義。";
                SetObjectDetectionDefectRegionStatus("尚未指定物件定義來源。");
                return;
            }

            bool runAllCores = coreKey == null;

            string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, definitionSignature))
            {
                statusLabel.Text = "請先完成物件定義處理，再執行缺陷檢測。";
                SetObjectDetectionDefectRegionStatus("物件定義結果尚未就緒，請先執行物件定義。");
                return;
            }

            EnsureObjectDetectionDefectCores(parameter);
            if (isObjectDetectionParameterImageLayout &&
                leftImageTabControl != null &&
                    leftImageTabControl.TabPages.Contains(
                    GetObjectDetectionDefectDisplayTabPage(
                        runAllCores
                            ? Math.Max(0, selectedObjectDetectionDefectCoreIndex)
                            : GetObjectDetectionDefectCoreIndex(coreKey))))
            {
                leftImageTabControl.SelectedTab = GetObjectDetectionDefectDisplayTabPage(
                    runAllCores
                        ? Math.Max(0, selectedObjectDetectionDefectCoreIndex)
                        : GetObjectDetectionDefectCoreIndex(coreKey));
            }
            if (!IsCurrentObjectDetectionFlatFieldImage(parameter) ||
                objectDetectionFlatFieldCorrectedLargeSource == null)
            {
                bool hasCurrentProfile = objectDetectionFlatFieldSmoothedProfile != null &&
                    string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id,
                        StringComparison.Ordinal) &&
                    objectDetectionFlatFieldProfileImageGeneration == imageSourceGeneration &&
                    objectDetectionFlatFieldProfileMaskGeneration ==
                        objectDetectionFlatFieldEvaluationGeneration;
                bool hasSavedProfile = !string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) &&
                    string.Equals(parameter.FlatFieldSavedSettingsSignature,
                        CreateObjectDetectionFlatFieldSettingsSignature(parameter), StringComparison.Ordinal);
                if (!hasCurrentProfile && !hasSavedProfile)
                {
                    statusLabel.Text = "請先計算或載入有效的平場校正結果。";
                    SetObjectDetectionDefectRegionStatus("缺少有效平場校正值，尚未開始缺陷運算。");
                    return;
                }

                if (objectDetectionFlatFieldResultLabel == null)
                {
                    statusLabel.Text = "平場校正結果尚未準備完成。";
                    return;
                }

                SetObjectDetectionDefectRegionStatus("正在準備平場校正影像，完成後接續缺陷運算...");
                if (objectDetectionDefectBasePreparationPending)
                {
                    int waitCount = 0;
                    while (objectDetectionDefectBasePreparationPending && waitCount++ < 1800)
                    {
                        await Task.Delay(100);
                    }
                }
                else
                {
                    await ShowSavedObjectDetectionFlatFieldCalibration(
                        parameter,
                        objectDetectionFlatFieldResultLabel,
                        null,
                        hasCurrentProfile);
                }
            }

            LargeImageSource correctedSource = objectDetectionFlatFieldCorrectedLargeSource;
            if (correctedSource == null && IsCurrentObjectDetectionFlatFieldImage(parameter))
            {
                try
                {
                    correctedSource = TryCreateSmallObjectDetectionDefectSource();
                }
                catch (OutOfMemoryException)
                {
                    statusLabel.Text = "缺陷檢測失敗：可用記憶體不足，無法準備平場校正影像。";
                    SetObjectDetectionDefectRegionStatus(
                        "可用記憶體不足，請縮小影像範圍或釋出系統記憶體後再試。 ");
                    return;
                }
                if (correctedSource != null)
                {
                    objectDetectionFlatFieldCorrectedLargeSource = correctedSource;
                }
            }
            if (!IsCurrentObjectDetectionFlatFieldImage(parameter) || correctedSource == null)
            {
                statusLabel.Text = "平場校正影像尚未就緒，缺陷運算未開始。";
                SetObjectDetectionDefectRegionStatus("無法取得目前影像的平場校正來源。");
                return;
            }
            RefreshObjectDetectionDefectDisplay();

            List<ObjectDefinitionDetectedObject> objects =
                SnapshotCompletedObjectDefinitionObjects(definition, definitionSignature);
            if (objects.Count == 0)
            {
                statusLabel.Text = "物件定義結果中沒有可供檢測的 ROI。";
                return;
            }

            Rectangle imageBounds = new Rectangle(0, 0, correctedSource.Width, correctedSource.Height);
            RectangleF normalizedRegion = RectangleF.FromLTRB(
                ClampUnit((float)parameter.DefectInspectionRegionLeft),
                ClampUnit((float)parameter.DefectInspectionRegionTop),
                ClampUnit((float)parameter.DefectInspectionRegionRight),
                ClampUnit((float)parameter.DefectInspectionRegionBottom));
            if (normalizedRegion.Width <= 0 || normalizedRegion.Height <= 0)
            {
                statusLabel.Text = "檢測範圍無效，請重新框選。";
                return;
            }

            IEnumerable<ObjectDetectionDefectCoreSettings> configuredCores = parameter.DefectDetectionCores
                .Take(4);
            if (!runAllCores)
            {
                configuredCores = configuredCores.Where(core =>
                    string.Equals(core.CoreKey, coreKey, StringComparison.Ordinal));
            }
            var coreSettings = configuredCores
                .Select(CloneObjectDetectionDefectCoreSettings)
                .ToList();
            if (coreSettings.Count == 0)
            {
                statusLabel.Text = "找不到指定的缺陷處理核心。";
                return;
            }
            var signatures = new Dictionary<string, string>(StringComparer.Ordinal);
            for (int index = 0; index < coreSettings.Count; index++)
            {
                signatures[coreSettings[index].CoreKey] =
                    CreateObjectDetectionDefectCoreSignature(
                        parameter,
                        coreSettings[index],
                        definitionSignature);
            }

            int capturedImageGeneration = imageSourceGeneration;
            int capturedFlatFieldGeneration = objectDetectionFlatFieldEvaluationGeneration;
            int capturedCalibrationGeneration = objectDetectionFlatFieldCalibrationGeneration;
            int flatFieldTargetGray = objectDetectionFlatFieldCorrectedTargetGray;
            bool runParallel = parameter.DefectParallelExecutionEnabled;
            correctedSource = correctedSource.AddReference();
            foreach (ObjectDetectionDefectCoreSettings core in coreSettings)
            {
                RemoveObjectDetectionDefectCoreResult(parameter.Id, core.CoreKey);
            }
            string processingScope = runAllCores
                ? "四核心"
                : GetObjectDetectionDefectCoreLabel(GetObjectDetectionDefectCoreIndex(coreKey));
            statusLabel.Text = parameter.DisplayName + "：" + processingScope + "缺陷檢測運算中...";
            SetObjectDetectionDefectRegionStatus(runParallel
                ? runAllCores
                    ? "所有物件 ROI 同時排程；每個物件的四個核心也同時運算..."
                    : "所有物件 ROI 的「" + processingScope + "」同時運算..."
                : runAllCores
                    ? "每個物件 ROI 的四個核心依序運算..."
                    : "所有物件 ROI 的「" + processingScope + "」依序運算...");

            Dictionary<string, ObjectDetectionDefectCoreResult> results = null;
            bool resultsStored = false;
            try
            {
                IProgress<string> progress = new Progress<string>(message =>
                {
                    if (!IsDisposed && string.Equals(activeObjectDetectionParameterId,
                        parameter.Id, StringComparison.Ordinal))
                    {
                        SetObjectDetectionDefectRegionStatus(message);
                    }
                });
                Stopwatch totalStopwatch = Stopwatch.StartNew();
                results = await Task.Run(
                    delegate
                    {
                        var output = new Dictionary<string, ObjectDetectionDefectCoreResult>(StringComparer.Ordinal);
                        for (int coreIndex = 0; coreIndex < coreSettings.Count; coreIndex++)
                        {
                            ObjectDetectionDefectCoreSettings core = coreSettings[coreIndex];
                            output[core.CoreKey] = new ObjectDetectionDefectCoreResult
                            {
                                Signature = signatures[core.CoreKey],
                                Contours = new List<ObjectDetectionDefectContour>(),
                                ProcessedPatches = new List<ObjectDetectionDefectProcessedPatch>()
                            };
                        }

                        var roiCoreContours = new List<ObjectDetectionDefectContour>[coreSettings.Count, objects.Count];
                        var roiCorePatches = new ObjectDetectionDefectProcessedPatch[coreSettings.Count, objects.Count];
                        var coreElapsedTicks = new long[coreSettings.Count];
                        var contrastAdjustmentElapsedTicks = new long[coreSettings.Count];
                        var preprocessingElapsedTicks = new long[coreSettings.Count];
                        var defectAnalysisElapsedTicks = new long[coreSettings.Count];
                        var previewGenerationElapsedTicks = new long[coreSettings.Count];
                        long roiPreparationElapsedTicks = 0;
                        int completedRois = 0;
                        Action<int> processRoi = delegate(int objectIndex)
                        {
                            ObjectDetectionDefectPerObjectResult objectResult =
                                ProcessObjectDetectionDefectObject(
                                    correctedSource,
                                    objects[objectIndex],
                                    normalizedRegion,
                                    imageBounds,
                                    flatFieldTargetGray,
                                    parameter.CameraXMillimetersPerPixel,
                                    parameter.CameraYMillimetersPerPixel,
                                    coreSettings,
                                    runParallel,
                                    progress,
                                    objectIndex,
                                    objects.Count);
                            for (int coreIndex = 0; coreIndex < coreSettings.Count; coreIndex++)
                            {
                                roiCoreContours[coreIndex, objectIndex] =
                                    objectResult.ContoursByCore[coreIndex];
                                roiCorePatches[coreIndex, objectIndex] =
                                    objectResult.ProcessedPatchesByCore[coreIndex];
                                System.Threading.Interlocked.Add(
                                    ref contrastAdjustmentElapsedTicks[coreIndex],
                                    objectResult.ContrastAdjustmentElapsedTicksByCore[coreIndex]);
                                System.Threading.Interlocked.Add(
                                    ref preprocessingElapsedTicks[coreIndex],
                                    objectResult.PreprocessingElapsedTicksByCore[coreIndex]);
                                System.Threading.Interlocked.Add(
                                    ref defectAnalysisElapsedTicks[coreIndex],
                                    objectResult.DefectAnalysisElapsedTicksByCore[coreIndex]);
                                System.Threading.Interlocked.Add(
                                    ref previewGenerationElapsedTicks[coreIndex],
                                    objectResult.PreviewGenerationElapsedTicksByCore[coreIndex]);
                                System.Threading.Interlocked.Add(
                                    ref coreElapsedTicks[coreIndex],
                                    objectResult.ElapsedTicksByCore[coreIndex]);
                            }
                            System.Threading.Interlocked.Add(
                                ref roiPreparationElapsedTicks,
                                objectResult.RoiPreparationElapsedTicks);

                            int completed = System.Threading.Interlocked.Increment(ref completedRois);
                            string coreProgress = runAllCores
                                ? "四個核心"
                                : "「" + processingScope + "」核心";
                            progress.Report(
                                "物件 ROI 完成 " +
                                completed.ToString(CultureInfo.CurrentCulture) + "/" +
                                objects.Count.ToString(CultureInfo.CurrentCulture) +
                                "；" + coreProgress +
                                (runParallel ? "運算完成..." : "依序完成..."));
                        };

                        try
                        {
                            if (runParallel && objects.Count > 1)
                            {
                                Parallel.For(
                                    0,
                                    objects.Count,
                                    new ParallelOptions
                                    {
                                        MaxDegreeOfParallelism = objects.Count
                                    },
                                    processRoi);
                            }
                            else
                            {
                                for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                                {
                                    processRoi(objectIndex);
                                }
                            }
                        }
                        catch
                        {
                            foreach (ObjectDetectionDefectProcessedPatch patch in roiCorePatches)
                            {
                                DisposeObjectDetectionDefectProcessedPatch(patch);
                            }
                            throw;
                        }

                        for (int coreIndex = 0; coreIndex < coreSettings.Count; coreIndex++)
                        {
                            ObjectDetectionDefectCoreSettings core = coreSettings[coreIndex];
                            ObjectDetectionDefectCoreResult coreResult = output[core.CoreKey];
                            coreResult.ElapsedMilliseconds = ConvertObjectDetectionDefectTicksToMilliseconds(
                                coreElapsedTicks[coreIndex]);
                            coreResult.RoiPreparationElapsedMilliseconds =
                                ConvertObjectDetectionDefectTicksToMilliseconds(roiPreparationElapsedTicks);
                            coreResult.ContrastAdjustmentElapsedMilliseconds =
                                ConvertObjectDetectionDefectTicksToMilliseconds(
                                    contrastAdjustmentElapsedTicks[coreIndex]);
                            coreResult.PreprocessingElapsedMilliseconds =
                                ConvertObjectDetectionDefectTicksToMilliseconds(
                                    preprocessingElapsedTicks[coreIndex]);
                            coreResult.DefectAnalysisElapsedMilliseconds =
                                ConvertObjectDetectionDefectTicksToMilliseconds(
                                    defectAnalysisElapsedTicks[coreIndex]);
                            coreResult.PreviewGenerationElapsedMilliseconds =
                                ConvertObjectDetectionDefectTicksToMilliseconds(
                                    previewGenerationElapsedTicks[coreIndex]);
                            coreResult.ParallelExecutionEnabled = runParallel;
                            for (int objectIndex = 0; objectIndex < objects.Count; objectIndex++)
                            {
                                if (roiCoreContours[coreIndex, objectIndex] != null)
                                {
                                    coreResult.Contours.AddRange(roiCoreContours[coreIndex, objectIndex]);
                                }
                                if (roiCorePatches[coreIndex, objectIndex] != null)
                                {
                                    coreResult.ProcessedPatches.Add(roiCorePatches[coreIndex, objectIndex]);
                                }
                            }
                            coreResult.DetectedComponentCount = coreResult.Contours
                                .Select(contour => contour.ObjectNumber.ToString(CultureInfo.InvariantCulture) +
                                    ":" + (contour.IsBright ? "B" : "D") + ":" +
                                    contour.ComponentLabel.ToString(CultureInfo.InvariantCulture))
                                .Distinct(StringComparer.Ordinal)
                                .Count();
                        }
                        return output;
                    });
                totalStopwatch.Stop();
                long totalElapsedMilliseconds = (long)Math.Round(totalStopwatch.Elapsed.TotalMilliseconds);
                foreach (ObjectDetectionDefectCoreResult coreResult in results.Values)
                {
                    coreResult.TotalElapsedMilliseconds = totalElapsedMilliseconds;
                }

                if (IsDisposed || capturedImageGeneration != imageSourceGeneration ||
                    capturedFlatFieldGeneration != objectDetectionFlatFieldEvaluationGeneration ||
                    capturedCalibrationGeneration != objectDetectionFlatFieldCalibrationGeneration ||
                    !string.Equals(CreateObjectDefinitionProcessingSignature(definition),
                        definitionSignature, StringComparison.Ordinal) ||
                    !HasCompletedObjectDefinitionResult(definition, definitionSignature) ||
                    !string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal))
                {
                    if (!IsDisposed)
                    {
                        statusLabel.Text = "缺陷檢測已完成計算，但期間影像或設定已變更，結果未套用。";
                        SetObjectDetectionDefectRegionStatus(
                            "計算期間來源影像、平場校正、物件定義或目前參數已變更；請重新執行缺陷檢測。 ");
                    }
                    return;
                }

                Dictionary<string, ObjectDetectionDefectCoreResult> currentResults;
                if (!objectDetectionDefectCoreResults.TryGetValue(parameter.Id, out currentResults))
                {
                    currentResults = new Dictionary<string, ObjectDetectionDefectCoreResult>(StringComparer.Ordinal);
                    objectDetectionDefectCoreResults[parameter.Id] = currentResults;
                }
                foreach (KeyValuePair<string, ObjectDetectionDefectCoreResult> item in results)
                {
                    ObjectDetectionDefectCoreResult previous;
                    if (currentResults.TryGetValue(item.Key, out previous))
                    {
                        DisposeObjectDetectionDefectCoreResult(previous);
                    }
                    currentResults[item.Key] = item.Value;
                }
                resultsStored = true;
                objectDetectionDefectLastRunCompleted = true;
                foreach (string updatedCoreKey in results.Keys)
                {
                    ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                        GetObjectDetectionDefectCoreIndex(updatedCoreKey));
                    if (display != null)
                    {
                        display.InvalidateImageView();
                    }
                }
            }
            catch (OutOfMemoryException)
            {
                if (!IsDisposed)
                {
                    statusLabel.Text = "缺陷檢測失敗：可用記憶體不足，無法完成目前的檢測範圍。";
                    SetObjectDetectionDefectRegionStatus(
                        "可用記憶體不足；程式沒有固定像素上限，但此檢測範圍超出目前可用資源。 ");
                }
            }
            catch (Exception exception)
            {
                if (!IsDisposed)
                {
                    AggregateException aggregate = exception as AggregateException;
                    Exception reported = aggregate == null
                        ? exception
                        : aggregate.Flatten().InnerExceptions.FirstOrDefault() ?? exception;
                    if (reported is OutOfMemoryException)
                    {
                        statusLabel.Text = "缺陷檢測失敗：可用記憶體不足，無法完成目前的檢測範圍。";
                        SetObjectDetectionDefectRegionStatus(
                            "可用記憶體不足；程式沒有固定像素上限，但目前資源無法同時處理排程工作。 ");
                    }
                    else
                    {
                        statusLabel.Text = "缺陷檢測失敗：" + reported.Message;
                        SetObjectDetectionDefectRegionStatus("缺陷檢測失敗：" + reported.Message);
                    }
                }
            }
            finally
            {
                if (!resultsStored && results != null)
                {
                    DisposeObjectDetectionDefectCoreResults(results);
                }
                correctedSource.ReleaseReference();
            }
        }

        private LargeImageSource TryCreateSmallObjectDetectionDefectSource()
        {
            if (objectDetectionFlatFieldPreviewDisplayControl == null ||
                !objectDetectionFlatFieldPreviewDisplayControl.HasImage ||
                objectDetectionFlatFieldPreviewDisplayControl.IsLargeImageMode)
            {
                return null;
            }

            using (Bitmap bitmap = objectDetectionFlatFieldPreviewDisplayControl.CloneImage())
            {
                if (bitmap == null)
                {
                    return null;
                }

                Cv.Mat gray = CreateOpenCvGrayMat(bitmap);
                try
                {
                    return new LargeImageSource(gray);
                }
                catch
                {
                    gray.Dispose();
                    throw;
                }
            }
        }

        private List<ObjectDefinitionDetectedObject> SnapshotCompletedObjectDefinitionObjects(
            ObjectDefinitionSettings definition,
            string processingSignature)
        {
            var result = new List<ObjectDefinitionDetectedObject>();
            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(completedObjectDefinitionProcessingSignature,
                        processingSignature, StringComparison.Ordinal))
                {
                    return result;
                }

                var byNumber = new Dictionary<int, ObjectDefinitionDetectedObject>();
                foreach (List<ObjectDefinitionDetectedObject> items in objectDefinitionResults.Values)
                {
                    if (items == null)
                    {
                        continue;
                    }
                    foreach (ObjectDefinitionDetectedObject item in items)
                    {
                        if (item == null || item.Number <= 0 || item.Bounds.Width <= 0 ||
                            item.Bounds.Height <= 0 || byNumber.ContainsKey(item.Number))
                        {
                            continue;
                        }

                        byNumber.Add(item.Number, CloneObjectDefinitionDetectedObject(item));
                    }
                }

                result.AddRange(byNumber.Values.OrderBy(item => item.Number));
            }
            return result;
        }

        private static ObjectDefinitionDetectedObject CloneObjectDefinitionDetectedObject(
            ObjectDefinitionDetectedObject source)
        {
            return new ObjectDefinitionDetectedObject
            {
                Number = source.Number,
                Bounds = source.Bounds,
                Area = source.Area,
                HasRotationGeometry = source.HasRotationGeometry,
                RotationAngleDegrees = source.RotationAngleDegrees,
                RotationCenter = source.RotationCenter,
                RotationSize = source.RotationSize,
                RotationCorners = source.RotationCorners == null
                    ? null
                    : source.RotationCorners.ToArray()
            };
        }

        private static ObjectDetectionDefectCoreSettings CloneObjectDetectionDefectCoreSettings(
            ObjectDetectionDefectCoreSettings source)
        {
            return new ObjectDetectionDefectCoreSettings
            {
                Id = source.Id,
                CoreKey = source.CoreKey,
                ContrastGain = source.ContrastGain,
                PreprocessMethod = source.PreprocessMethod,
                GaussianKernelWidth = source.GaussianKernelWidth,
                GaussianKernelHeight = source.GaussianKernelHeight,
                GaussianSigmaX = source.GaussianSigmaX,
                GaussianSigmaY = source.GaussianSigmaY,
                MedianKernelSize = source.MedianKernelSize,
                DarkThresholdEnabled = source.DarkThresholdEnabled,
                DarkThreshold = source.DarkThreshold,
                BrightThresholdEnabled = source.BrightThresholdEnabled,
                BrightThreshold = source.BrightThreshold,
                ErodeKernelSize = source.ErodeKernelSize,
                ErodeIterations = source.ErodeIterations,
                DilateKernelSize = source.DilateKernelSize,
                DilateIterations = source.DilateIterations,
                MinimumArea = source.MinimumArea,
                MinimumWidthMillimeters = source.MinimumWidthMillimeters,
                MaximumWidthMillimeters = source.MaximumWidthMillimeters,
                MinimumHeightMillimeters = source.MinimumHeightMillimeters,
                MaximumHeightMillimeters = source.MaximumHeightMillimeters,
                ShowMask = source.ShowMask,
                ShowRedBoxes = source.ShowRedBoxes,
                ShowOrangeBoxes = source.ShowOrangeBoxes
            };
        }

        private string CreateObjectDetectionDefectCoreSignature(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionDefectCoreSettings core,
            string definitionSignature)
        {
            return string.Join("|", new[]
            {
                parameter.Id ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                objectDetectionFlatFieldEvaluationGeneration.ToString(CultureInfo.InvariantCulture),
                objectDetectionFlatFieldCorrectedImageGeneration.ToString(CultureInfo.InvariantCulture),
                objectDetectionFlatFieldCorrectedTargetGray.ToString(CultureInfo.InvariantCulture),
                objectDetectionFlatFieldCalibrationGeneration.ToString(CultureInfo.InvariantCulture),
                parameter.ObjectDefinitionId ?? string.Empty,
                definitionSignature ?? string.Empty,
                parameter.FlatFieldSavedSettingsSignature ?? string.Empty,
                parameter.DefectInspectionRegionId ?? string.Empty,
                parameter.DefectInspectionRegionObjectDefinitionId ?? string.Empty,
                parameter.DefectInspectionRegionReferenceObjectNumber.ToString(CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionLeft.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionTop.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionRight.ToString("R", CultureInfo.InvariantCulture),
                parameter.DefectInspectionRegionBottom.ToString("R", CultureInfo.InvariantCulture),
                core.CoreKey ?? string.Empty,
                core.ContrastGain.ToString("R", CultureInfo.InvariantCulture),
                core.PreprocessMethod ?? string.Empty,
                core.GaussianKernelWidth.ToString(CultureInfo.InvariantCulture),
                core.GaussianKernelHeight.ToString(CultureInfo.InvariantCulture),
                core.GaussianSigmaX.ToString("R", CultureInfo.InvariantCulture),
                core.GaussianSigmaY.ToString("R", CultureInfo.InvariantCulture),
                core.MedianKernelSize.ToString(CultureInfo.InvariantCulture),
                core.DarkThresholdEnabled ? "1" : "0",
                core.DarkThreshold.ToString(CultureInfo.InvariantCulture),
                core.BrightThresholdEnabled ? "1" : "0",
                core.BrightThreshold.ToString(CultureInfo.InvariantCulture),
                core.ErodeKernelSize.ToString(CultureInfo.InvariantCulture),
                core.ErodeIterations.ToString(CultureInfo.InvariantCulture),
                core.DilateKernelSize.ToString(CultureInfo.InvariantCulture),
                core.DilateIterations.ToString(CultureInfo.InvariantCulture),
                core.MinimumArea.ToString("R", CultureInfo.InvariantCulture),
                core.MinimumWidthMillimeters.ToString("R", CultureInfo.InvariantCulture),
                core.MaximumWidthMillimeters.ToString("R", CultureInfo.InvariantCulture),
                core.MinimumHeightMillimeters.ToString("R", CultureInfo.InvariantCulture),
                core.MaximumHeightMillimeters.ToString("R", CultureInfo.InvariantCulture),
                GetObjectDetectionDefectDimensionScale(parameter.CameraXMillimetersPerPixel)
                    .ToString("R", CultureInfo.InvariantCulture),
                GetObjectDetectionDefectDimensionScale(parameter.CameraYMillimetersPerPixel)
                    .ToString("R", CultureInfo.InvariantCulture)
            });
        }

        private static ObjectDetectionDefectPerObjectResult ProcessObjectDetectionDefectObject(
            LargeImageSource source,
            ObjectDefinitionDetectedObject detectedObject,
            RectangleF normalizedRegion,
            Rectangle imageBounds,
            int pivotGray,
            double xMillimetersPerPixel,
            double yMillimetersPerPixel,
            IList<ObjectDetectionDefectCoreSettings> cores,
            bool runParallel,
            IProgress<string> progress,
            int objectIndex,
            int objectCount)
        {
            var result = new ObjectDetectionDefectPerObjectResult
            {
                ContoursByCore = new List<ObjectDetectionDefectContour>[cores.Count],
                ElapsedTicksByCore = new long[cores.Count],
                ContrastAdjustmentElapsedTicksByCore = new long[cores.Count],
                PreprocessingElapsedTicksByCore = new long[cores.Count],
                DefectAnalysisElapsedTicksByCore = new long[cores.Count],
                PreviewGenerationElapsedTicksByCore = new long[cores.Count],
                ProcessedPatchesByCore = new ObjectDetectionDefectProcessedPatch[cores.Count]
            };
            for (int coreIndex = 0; coreIndex < cores.Count; coreIndex++)
            {
                result.ContoursByCore[coreIndex] = new List<ObjectDetectionDefectContour>();
            }

            PointF[] corners = CreateObjectDetectionDefectRegionImageCorners(
                detectedObject,
                normalizedRegion);
            float minX = corners.Min(point => point.X);
            float minY = corners.Min(point => point.Y);
            float maxX = corners.Max(point => point.X);
            float maxY = corners.Max(point => point.Y);
            int left = (int)Math.Floor(minX);
            int top = (int)Math.Floor(minY);
            int right = (int)Math.Ceiling(maxX);
            int bottom = (int)Math.Ceiling(maxY);
            Rectangle crop = Rectangle.Intersect(
                Rectangle.Intersect(
                    Rectangle.FromLTRB(left, top, right, bottom),
                    detectedObject.Bounds),
                imageBounds);
            if (crop.Width <= 0 || crop.Height <= 0)
            {
                return result;
            }
            try
            {
                progress?.Report("ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
                    objectCount.ToString(CultureInfo.CurrentCulture) + "：正在準備 ROI 影像區塊...");
                Stopwatch roiPreparationStopwatch = Stopwatch.StartNew();
                using (Cv.Mat gray = CreateObjectDetectionDefectGrayRegionMat(source, crop))
                using (var polygonMask = new Cv.Mat(
                    crop.Height,
                    crop.Width,
                    Cv.MatType.CV_8UC1,
                    Cv.Scalar.Black))
                {
                    Cv.Point[] polygon = corners.Select(point => new Cv.Point(
                        (int)Math.Round(point.X - crop.X),
                        (int)Math.Round(point.Y - crop.Y))).ToArray();
                    Cv.Cv2.FillPoly(polygonMask, new[] { polygon }, Cv.Scalar.White);
                    roiPreparationStopwatch.Stop();
                    result.RoiPreparationElapsedTicks = roiPreparationStopwatch.ElapsedTicks;

                Action<int> processCore = delegate(int coreIndex)
                {
                    ObjectDetectionDefectCoreSettings core = cores[coreIndex];
                    string roiLabel = "ROI " + (objectIndex + 1).ToString(CultureInfo.CurrentCulture) + "/" +
                        objectCount.ToString(CultureInfo.CurrentCulture);
                    string coreLabel = "核心 " + (coreIndex + 1).ToString(CultureInfo.CurrentCulture);
                    progress?.Report(roiLabel + "／" + coreLabel + "：對比調整與影像前處理...");
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    Bitmap processedBitmap = null;
                     try
                     {
                        using (var adjusted = new Cv.Mat())
                        {
                            double gain = double.IsNaN(core.ContrastGain) || double.IsInfinity(core.ContrastGain)
                                ? 1.0
                                : Math.Max(0.1, Math.Min(5.0, core.ContrastGain));
                            double beta = Math.Max(1, Math.Min(255, pivotGray)) * (1.0 - gain);
                            Stopwatch contrastStopwatch = Stopwatch.StartNew();
                            gray.ConvertTo(adjusted, Cv.MatType.CV_8UC1, gain, beta);
                            contrastStopwatch.Stop();
                            result.ContrastAdjustmentElapsedTicksByCore[coreIndex] =
                                contrastStopwatch.ElapsedTicks;

                            Stopwatch preprocessingStopwatch = Stopwatch.StartNew();
                            using (Cv.Mat preprocessed = ApplyObjectDetectionDefectPreprocessing(adjusted, core))
                            {
                                preprocessingStopwatch.Stop();
                                result.PreprocessingElapsedTicksByCore[coreIndex] =
                                    preprocessingStopwatch.ElapsedTicks;
                                Stopwatch previewStopwatch = Stopwatch.StartNew();
                                processedBitmap = CreateObjectDetectionDefectPreviewBitmap(preprocessed);
                                previewStopwatch.Stop();
                                result.PreviewGenerationElapsedTicksByCore[coreIndex] =
                                    previewStopwatch.ElapsedTicks;
                                Stopwatch analysisStopwatch = Stopwatch.StartNew();
                                progress?.Report(roiLabel + "／" + coreLabel + "：門檻分割、連通元件與輪廓分析...");
                                if (core.DarkThresholdEnabled)
                                {
                                    AddObjectDetectionDefectMaskContours(
                                        preprocessed,
                                        polygonMask,
                                        crop,
                                        detectedObject.Number,
                                        core.DarkThreshold,
                                        false,
                                        core,
                                        xMillimetersPerPixel,
                                        yMillimetersPerPixel,
                                        result.ContoursByCore[coreIndex]);
                                }
                                if (core.BrightThresholdEnabled)
                                {
                                    AddObjectDetectionDefectMaskContours(
                                        preprocessed,
                                        polygonMask,
                                        crop,
                                        detectedObject.Number,
                                        core.BrightThreshold,
                                        true,
                                        core,
                                        xMillimetersPerPixel,
                                        yMillimetersPerPixel,
                                        result.ContoursByCore[coreIndex]);
                                }
                                analysisStopwatch.Stop();
                                result.DefectAnalysisElapsedTicksByCore[coreIndex] =
                                    analysisStopwatch.ElapsedTicks;
                            }
                        }

                        result.ProcessedPatchesByCore[coreIndex] = new ObjectDetectionDefectProcessedPatch
                        {
                            Bounds = crop,
                            InspectionPolygon = corners.ToArray(),
                            ProcessedImage = processedBitmap
                        };
                        processedBitmap = null;
                    }
                    finally
                    {
                        if (processedBitmap != null)
                        {
                            processedBitmap.Dispose();
                        }
                    }

                    stopwatch.Stop();
                    result.ElapsedTicksByCore[coreIndex] = stopwatch.ElapsedTicks;
                    progress?.Report(roiLabel + "／" + coreLabel + "：完成。");
                };

                    if (runParallel && cores.Count > 1)
                    {
                        Parallel.For(
                            0,
                            cores.Count,
                            new ParallelOptions
                            {
                                MaxDegreeOfParallelism = cores.Count
                            },
                            processCore);
                    }
                    else
                    {
                        for (int coreIndex = 0; coreIndex < cores.Count; coreIndex++)
                        {
                            processCore(coreIndex);
                        }
                    }
                }
            }
            catch
            {
                foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatchesByCore)
                {
                    DisposeObjectDetectionDefectProcessedPatch(patch);
                }
                throw;
            }

            return result;
        }

        private static Cv.Mat CreateObjectDetectionDefectGrayRegionMat(
            LargeImageSource source,
            Rectangle sourceRect)
        {
            if (source.IsMemoryBacked)
            {
                return source.CreateGrayscaleMatView(sourceRect);
            }

            using (Bitmap bitmap = source.CreateRegionBitmapFromTiles(sourceRect))
            {
                return CreateOpenCvGrayMat(bitmap);
            }
        }

        private static Cv.Mat ApplyObjectDetectionDefectPreprocessing(
            Cv.Mat source,
            ObjectDetectionDefectCoreSettings core)
        {
            string mode = core.PreprocessMethod ?? string.Empty;
            if (string.Equals(mode, "GaussianBlur", StringComparison.OrdinalIgnoreCase))
            {
                var result = new Cv.Mat();
                try
                {
                    Cv.Cv2.GaussianBlur(
                        source,
                        result,
                        new Cv.Size(
                            NormalizeDefectKernel(core.GaussianKernelWidth),
                            NormalizeDefectKernel(core.GaussianKernelHeight)),
                        NormalizeDefectSigma(core.GaussianSigmaX),
                        NormalizeDefectSigma(core.GaussianSigmaY));
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }
            if (string.Equals(mode, "MedianBlur", StringComparison.OrdinalIgnoreCase))
            {
                var result = new Cv.Mat();
                try
                {
                    Cv.Cv2.MedianBlur(
                        source,
                        result,
                        NormalizeDefectKernel(core.MedianKernelSize));
                    return result;
                }
                catch
                {
                    result.Dispose();
                    throw;
                }
            }

            return source.Clone();
        }

        private static Bitmap CreateObjectDetectionDefectPreviewBitmap(Cv.Mat source)
        {
            if (source == null || source.Empty() || source.Type() != Cv.MatType.CV_8UC1)
            {
                throw new ArgumentException("缺陷預覽必須是有效的 8-bit 灰階影像。", "source");
            }

            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format8bppIndexed);
            ColorPalette palette = bitmap.Palette;
            for (int index = 0; index < palette.Entries.Length; index++)
            {
                palette.Entries[index] = Color.FromArgb(index, index, index);
            }
            bitmap.Palette = palette;

            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format8bppIndexed);
            bool copied = false;
            try
            {
                byte[] row = new byte[source.Width];
                long sourceStride = source.Step();
                for (int y = 0; y < source.Height; y++)
                {
                    Marshal.Copy(
                        source.Data + checked((int)(y * sourceStride)),
                        row,
                        0,
                        row.Length);
                    IntPtr destination = data.Stride >= 0
                        ? data.Scan0 + (y * data.Stride)
                        : data.Scan0 + ((source.Height - 1 - y) * Math.Abs(data.Stride));
                    Marshal.Copy(row, 0, destination, row.Length);
                }
                copied = true;
            }
            finally
            {
                bitmap.UnlockBits(data);
                if (!copied)
                {
                    bitmap.Dispose();
                }
            }

            return bitmap;
        }

        private static void DisposeObjectDetectionDefectCoreResult(ObjectDetectionDefectCoreResult result)
        {
            if (result == null || result.ProcessedPatches == null)
            {
                return;
            }

            foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
            {
                DisposeObjectDetectionDefectProcessedPatch(patch);
            }
            result.ProcessedPatches.Clear();
        }

        private static void DisposeObjectDetectionDefectProcessedPatch(
            ObjectDetectionDefectProcessedPatch patch)
        {
            if (patch != null && patch.ProcessedImage != null)
            {
                patch.ProcessedImage.Dispose();
                patch.ProcessedImage = null;
            }
        }

        private static void DisposeObjectDetectionDefectCoreResults(
            IDictionary<string, ObjectDetectionDefectCoreResult> results)
        {
            if (results == null)
            {
                return;
            }
            foreach (ObjectDetectionDefectCoreResult result in results.Values)
            {
                DisposeObjectDetectionDefectCoreResult(result);
            }
            results.Clear();
        }

        private void RemoveObjectDetectionDefectCoreResult(string parameterId, string coreKey)
        {
            Dictionary<string, ObjectDetectionDefectCoreResult> byCore;
            if (string.IsNullOrWhiteSpace(parameterId) ||
                !objectDetectionDefectCoreResults.TryGetValue(parameterId, out byCore))
            {
                return;
            }

            ObjectDetectionDefectCoreResult result;
            if (byCore.TryGetValue(coreKey ?? string.Empty, out result))
            {
                DisposeObjectDetectionDefectCoreResult(result);
                byCore.Remove(coreKey ?? string.Empty);
            }
            if (byCore.Count == 0)
            {
                objectDetectionDefectCoreResults.Remove(parameterId);
            }
        }

        private void RemoveObjectDetectionDefectCoreResults(string parameterId)
        {
            Dictionary<string, ObjectDetectionDefectCoreResult> byCore;
            if (string.IsNullOrWhiteSpace(parameterId) ||
                !objectDetectionDefectCoreResults.TryGetValue(parameterId, out byCore))
            {
                return;
            }

            DisposeObjectDetectionDefectCoreResults(byCore);
            objectDetectionDefectCoreResults.Remove(parameterId);
        }

        private void ClearObjectDetectionDefectCoreResults()
        {
            foreach (Dictionary<string, ObjectDetectionDefectCoreResult> byCore in
                objectDetectionDefectCoreResults.Values)
            {
                DisposeObjectDetectionDefectCoreResults(byCore);
            }
            objectDetectionDefectCoreResults.Clear();
        }

        private static void AddObjectDetectionDefectMaskContours(
            Cv.Mat source,
            Cv.Mat polygonMask,
            Rectangle crop,
            int objectNumber,
            int threshold,
            bool bright,
            ObjectDetectionDefectCoreSettings core,
            double xMillimetersPerPixel,
            double yMillimetersPerPixel,
            List<ObjectDetectionDefectContour> output)
        {
            using (var thresholdMask = new Cv.Mat())
            using (var clippedMask = new Cv.Mat())
            {
                int clampedThreshold = Math.Max(0, Math.Min(255, threshold));
                Cv.Cv2.InRange(
                    source,
                    bright ? new Cv.Scalar(clampedThreshold) : new Cv.Scalar(0),
                    bright ? new Cv.Scalar(255) : new Cv.Scalar(clampedThreshold),
                    thresholdMask);
                Cv.Cv2.BitwiseAnd(thresholdMask, polygonMask, clippedMask);

                using (Cv.Mat morphed = ApplyObjectDetectionDefectMorphology(clippedMask, core))
                using (var regionConstrainedMask = new Cv.Mat())
                {
                    Cv.Cv2.BitwiseAnd(morphed, polygonMask, regionConstrainedMask);
                    using (var labels = new Cv.Mat())
                    using (var stats = new Cv.Mat())
                    using (var centroids = new Cv.Mat())
                    {
                        int count = Cv.Cv2.ConnectedComponentsWithStats(
                            regionConstrainedMask,
                            labels,
                            stats,
                            centroids,
                            Cv.PixelConnectivity.Connectivity8,
                            Cv.MatType.CV_32SC1);
                        double minimumArea = double.IsNaN(core.MinimumArea) ||
                            double.IsInfinity(core.MinimumArea)
                            ? 0.0
                            : Math.Max(0.0, core.MinimumArea);
                        for (int label = 1; label < count; label++)
                        {
                            int left = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Left);
                            int top = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Top);
                            int area = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Area);
                            int width = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Width);
                            int height = stats.At<int>(label, (int)Cv.ConnectedComponentsTypes.Height);
                            double widthMillimeters = width * GetObjectDetectionDefectDimensionScale(xMillimetersPerPixel);
                            double heightMillimeters = height * GetObjectDetectionDefectDimensionScale(yMillimetersPerPixel);
                            if (area < minimumArea ||
                                !IsWithinObjectDetectionDefectDimensionRange(
                                    widthMillimeters,
                                    core.MinimumWidthMillimeters,
                                    core.MaximumWidthMillimeters) ||
                                !IsWithinObjectDetectionDefectDimensionRange(
                                    heightMillimeters,
                                    core.MinimumHeightMillimeters,
                                    core.MaximumHeightMillimeters))
                            {
                                continue;
                            }

                            var componentBounds = new Cv.Rect(left, top, width, height);
                            using (var labelRegion = new Cv.Mat(labels, componentBounds))
                            using (var componentMask = new Cv.Mat())
                            {
                                Cv.Cv2.Compare(labelRegion, label, componentMask, Cv.CmpType.EQ);
                                Cv.Point[][] foundContours;
                                Cv.HierarchyIndex[] hierarchy;
                                Cv.Cv2.FindContours(
                                    componentMask,
                                    out foundContours,
                                    out hierarchy,
                                    Cv.RetrievalModes.External,
                                    Cv.ContourApproximationModes.ApproxSimple);
                                if (foundContours == null)
                                {
                                    continue;
                                }

                                foreach (Cv.Point[] contour in foundContours)
                                {
                                    if (contour == null || contour.Length < 3)
                                    {
                                        continue;
                                    }
                                    var points = new PointF[contour.Length];
                                    float minX = float.MaxValue;
                                    float minY = float.MaxValue;
                                    float maxX = float.MinValue;
                                    float maxY = float.MinValue;
                                    for (int pointIndex = 0; pointIndex < contour.Length; pointIndex++)
                                    {
                                        points[pointIndex] = new PointF(
                                            crop.X + left + contour[pointIndex].X,
                                            crop.Y + top + contour[pointIndex].Y);
                                        minX = Math.Min(minX, points[pointIndex].X);
                                        minY = Math.Min(minY, points[pointIndex].Y);
                                        maxX = Math.Max(maxX, points[pointIndex].X);
                                        maxY = Math.Max(maxY, points[pointIndex].Y);
                                    }
                                    output.Add(new ObjectDetectionDefectContour
                                    {
                                        ObjectNumber = objectNumber,
                                        IsBright = bright,
                                        ComponentLabel = label,
                                        Points = points,
                                        Bounds = RectangleF.FromLTRB(minX, minY, maxX + 1f, maxY + 1f)
                                    });
                                }
                            }
                        }
                    }
                }
            }
        }

        private static bool IsWithinObjectDetectionDefectDimensionRange(
            double dimensionMillimeters,
            double minimumMillimeters,
            double maximumMillimeters)
        {
            return (minimumMillimeters <= 0.0 || dimensionMillimeters >= minimumMillimeters) &&
                (maximumMillimeters <= 0.0 || dimensionMillimeters <= maximumMillimeters);
        }

        private static Cv.Mat ApplyObjectDetectionDefectMorphology(
            Cv.Mat source,
            ObjectDetectionDefectCoreSettings core)
        {
            Cv.Mat current = source.Clone();
            try
            {
                if (core.ErodeIterations > 0)
                {
                    using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                        Cv.MorphShapes.Rect,
                        new Cv.Size(NormalizeDefectKernel(core.ErodeKernelSize),
                            NormalizeDefectKernel(core.ErodeKernelSize))))
                    {
                        var next = new Cv.Mat();
                        try
                        {
                            Cv.Cv2.Erode(current, next, kernel, null,
                                Math.Max(0, Math.Min(20, core.ErodeIterations)));
                            current.Dispose();
                            current = next;
                        }
                        catch
                        {
                            next.Dispose();
                            throw;
                        }
                    }
                }
                if (core.DilateIterations > 0)
                {
                    using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                        Cv.MorphShapes.Rect,
                        new Cv.Size(NormalizeDefectKernel(core.DilateKernelSize),
                            NormalizeDefectKernel(core.DilateKernelSize))))
                    {
                        var next = new Cv.Mat();
                        try
                        {
                            Cv.Cv2.Dilate(current, next, kernel, null,
                                Math.Max(0, Math.Min(20, core.DilateIterations)));
                            current.Dispose();
                            current = next;
                        }
                        catch
                        {
                            next.Dispose();
                            throw;
                        }
                    }
                }

                return current;
            }
            catch
            {
                current.Dispose();
                throw;
            }
        }

        private static int NormalizeDefectKernel(int value)
        {
            int normalized = Math.Max(1, Math.Min(99, value));
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }

        private static double NormalizeDefectSigma(double value)
        {
            return double.IsNaN(value) || double.IsInfinity(value)
                ? 0.0
                : Math.Max(0.0, Math.Min(100.0, value));
        }

        private bool TryGetCurrentObjectDetectionDefectCoreResult(
            ObjectDetectionParameterSettings parameter,
            out ObjectDetectionDefectCoreResult result)
        {
            return TryGetObjectDetectionDefectCoreResult(
                parameter,
                GetSelectedObjectDetectionDefectCoreKey(),
                out result);
        }

        private bool TryGetObjectDetectionDefectCoreResult(
            ObjectDetectionParameterSettings parameter,
            string coreKey,
            out ObjectDetectionDefectCoreResult result)
        {
            result = null;
            if (parameter == null || !parameter.DefectInspectionRegionConfigured ||
                !string.Equals(parameter.DefectInspectionRegionObjectDefinitionId,
                    parameter.ObjectDefinitionId, StringComparison.Ordinal))
            {
                return false;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                return false;
            }
            string definitionSignature = CreateObjectDefinitionProcessingSignature(definition);
            EnsureObjectDetectionDefectCores(parameter);
            ObjectDetectionDefectCoreSettings core = parameter.DefectDetectionCores
                .FirstOrDefault(item => string.Equals(item.CoreKey,
                    coreKey, StringComparison.Ordinal));
            Dictionary<string, ObjectDetectionDefectCoreResult> byCore;
            if (core == null || !objectDetectionDefectCoreResults.TryGetValue(parameter.Id, out byCore) ||
                !byCore.TryGetValue(core.CoreKey, out result))
            {
                result = null;
                return false;
            }

            if (!string.Equals(result.Signature,
                CreateObjectDetectionDefectCoreSignature(parameter, core, definitionSignature),
                StringComparison.Ordinal))
            {
                result = null;
                return false;
            }
            return true;
        }

        private void DrawObjectDetectionDefectCoreResult(
            Graphics graphics,
            float zoom,
            PointF offset,
            Rectangle visibleSourceRect,
            string coreKey)
        {
            if (graphics == null || zoom <= 0 ||
                !isObjectDetectionParameterImageLayout)
            {
                return;
            }
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            ObjectDetectionDefectCoreResult result;
            if (!TryGetObjectDetectionDefectCoreResult(parameter, coreKey, out result) ||
                result.Contours == null || result.ProcessedPatches == null)
            {
                return;
            }

            ObjectDetectionDefectCoreSettings core = parameter.DefectDetectionCores
                .FirstOrDefault(item => string.Equals(item.CoreKey, coreKey, StringComparison.Ordinal));
            if (core == null)
            {
                return;
            }

            RectangleF visibleBounds = visibleSourceRect;
            foreach (ObjectDetectionDefectProcessedPatch patch in result.ProcessedPatches)
            {
                if (patch == null || patch.ProcessedImage == null ||
                    !patch.Bounds.IntersectsWith(Rectangle.Ceiling(visibleBounds)))
                {
                    continue;
                }

                PointF[] polygon = patch.InspectionPolygon;
                GraphicsState state = graphics.Save();
                try
                {
                    if (polygon != null && polygon.Length >= 3)
                    {
                        var screenPolygon = new PointF[polygon.Length];
                        for (int index = 0; index < polygon.Length; index++)
                        {
                            screenPolygon[index] = new PointF(
                                offset.X + polygon[index].X * zoom,
                                offset.Y + polygon[index].Y * zoom);
                        }
                        using (var clip = new GraphicsPath())
                        {
                            clip.AddPolygon(screenPolygon);
                            graphics.SetClip(clip, System.Drawing.Drawing2D.CombineMode.Intersect);
                        }
                    }

                    var destination = new RectangleF(
                        offset.X + patch.Bounds.X * zoom,
                        offset.Y + patch.Bounds.Y * zoom,
                        patch.Bounds.Width * zoom,
                        patch.Bounds.Height * zoom);
                    graphics.DrawImage(patch.ProcessedImage, destination);
                }
                finally
                {
                    graphics.Restore(state);
                }
            }

            if (core.ShowMask)
            {
                using (var darkMaskFill = new SolidBrush(Color.FromArgb(112, Color.HotPink)))
                using (var brightMaskFill = new SolidBrush(Color.FromArgb(112, Color.Yellow)))
                {
                    foreach (ObjectDetectionDefectContour contour in result.Contours)
                    {
                        if (contour.Points == null || contour.Points.Length < 3 ||
                            !contour.Bounds.IntersectsWith(visibleBounds))
                        {
                            continue;
                        }

                        PointF[] screenPoints = TransformObjectDetectionDefectPoints(
                            contour.Points,
                            zoom,
                            offset);
                        graphics.FillPolygon(
                            contour.IsBright ? brightMaskFill : darkMaskFill,
                            screenPoints);
                    }
                }
            }

            if (core.ShowRedBoxes || core.ShowOrangeBoxes)
            {
                using (var darkDefectPen = new Pen(Color.Red, Math.Max(1f, 2f * zoom)))
                using (var brightDefectPen = new Pen(Color.DarkOrange, Math.Max(1f, 2f * zoom)))
                {
                    foreach (ObjectDetectionDefectContour contour in result.Contours)
                    {
                        if (!(contour.IsBright ? core.ShowOrangeBoxes : core.ShowRedBoxes) ||
                            !contour.Bounds.IntersectsWith(visibleBounds))
                        {
                            continue;
                        }

                        RectangleF bounds = contour.Bounds;
                        graphics.DrawRectangle(
                            contour.IsBright ? brightDefectPen : darkDefectPen,
                            offset.X + bounds.X * zoom,
                            offset.Y + bounds.Y * zoom,
                            Math.Max(1f, bounds.Width * zoom),
                            Math.Max(1f, bounds.Height * zoom));
                    }
                }
            }
        }

        private static PointF[] TransformObjectDetectionDefectPoints(
            PointF[] points,
            float zoom,
            PointF offset)
        {
            var transformed = new PointF[points.Length];
            for (int index = 0; index < points.Length; index++)
            {
                transformed[index] = new PointF(
                    offset.X + points[index].X * zoom,
                    offset.Y + points[index].Y * zoom);
            }
            return transformed;
        }

        private static long ConvertObjectDetectionDefectTicksToMilliseconds(long elapsedTicks)
        {
            return (long)Math.Round(elapsedTicks * 1000.0 / Stopwatch.Frequency);
        }

        private void UpdateObjectDetectionDefectProcessingStatus()
        {
            if (statusLabel == null || objectDetectionDefectProcessingRequested)
            {
                return;
            }
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                return;
            }
            ObjectDetectionDefectCoreResult result;
            if (TryGetCurrentObjectDetectionDefectCoreResult(parameter, out result))
            {
                string executionMode = result.ParallelExecutionEnabled ? "平行" : "依序";
                statusLabel.Text = parameter.DisplayName + "：" +
                    GetSelectedObjectDetectionDefectCoreKey() + " 檢出 " +
                    result.DetectedComponentCount.ToString("N0", CultureInfo.CurrentCulture) +
                    " 個；缺陷檢測完成；" + executionMode + "實際耗時 " +
                    result.TotalElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms（不含畫面顯示）";
                SetObjectDetectionDefectRegionStatus(
                    executionMode + "運算實際經過時間 " +
                    result.TotalElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) +
                    " ms（不含畫面顯示）\r\n" +
                    "以下是工作時間加總；平行時不代表實際等待時間：\r\n" +
                    "ROI 準備 " + result.RoiPreparationElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) +
                    "；對比 " + result.ContrastAdjustmentElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) +
                    "；前處理 " + result.PreprocessingElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms\r\n" +
                    "分析 " + result.DefectAnalysisElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) +
                    "；預覽 " + result.PreviewGenerationElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) +
                    "；此核心各 ROI 耗時加總 " + result.ElapsedMilliseconds.ToString("N0", CultureInfo.CurrentCulture) + " ms");
            }
            else
            {
                statusLabel.Text = parameter.DisplayName + "：目前核心尚未有符合現行參數的結果";
            }
        }
    }
}
