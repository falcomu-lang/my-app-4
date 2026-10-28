using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CameraCaptureApp.Services;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private int cameraInspectionFrameInFlight;
        private int cameraInspectionFramesDropped;
        private string latestCameraInspectionFramePath;
        private string latestCameraInspectionFrameIdentity;

        private const string CameraMemoryImageIdentityPrefix = "camera-memory://";

        private void CameraCaptureHostControl_ImageAcquired(
            object sender,
            CameraImageAcquiredEventArgs e)
        {
            if (e == null || IsDisposed || !IsHandleCreated)
            {
                return;
            }

            if (Interlocked.CompareExchange(ref cameraInspectionFrameInFlight, 1, 0) != 0)
            {
                Interlocked.Increment(ref cameraInspectionFramesDropped);
                return;
            }

            CameraGrayscaleFrame frame = e.GrayscaleFrame;
            if (frame == null)
            {
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                SetCameraInspectionStatus("相機沒有提供可用的灰階影像資料。");
                return;
            }
            long sequenceNumber = e.SequenceNumber;
            try
            {
                BeginInvoke(new Action(async delegate
                {
                    await ProcessCameraAcquiredImageAsync(frame, sequenceNumber);
                }));
            }
            catch (ObjectDisposedException)
            {
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
            }
            catch (InvalidOperationException)
            {
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
            }
        }

        private async Task ProcessCameraAcquiredImageAsync(CameraGrayscaleFrame frame, long sequenceNumber)
        {
            string imageIdentity = null;
            string imageSavePath = null;
            LargeImageSource memorySource = null;
            bool returnToInspectionOverview = isInspectionOverviewActive;
            string previousCameraImagePath = latestCameraInspectionFramePath;
            ResultReviewParameterChoice selectedChoice = null;
            if (string.IsNullOrWhiteSpace(previousCameraImagePath) &&
                IsOwnedCameraInspectionImagePath(systemParameters.LastImagePath))
            {
                previousCameraImagePath = systemParameters.LastImagePath;
            }

            try
            {
                string busyReason;
                if (TryGetCameraInspectionBusyReason(out busyReason))
                {
                    SetCameraInspectionStatus("相機影像 #" + sequenceNumber + " 已略過：" + busyReason);
                    return;
                }

                int overviewIndex = functionListBox.Items.IndexOf(InspectionOverviewMenuText);
                returnToInspectionOverview = isInspectionOverviewActive &&
                    overviewIndex >= 0 && functionListBox.SelectedIndex == overviewIndex;
                int resultReviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
                if (!returnToInspectionOverview && !isRemoteControllerInspectionActive &&
                    resultReviewIndex >= 0 &&
                    functionListBox.SelectedIndex != resultReviewIndex)
                {
                    functionListBox.SelectedIndex = resultReviewIndex;
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    bool restoreOverview = returnToInspectionOverview;
                    bool preserveInspectionMode = restoreOverview &&
                        isInspectionOverviewInspectionModeActive;
                    if (restoreOverview && !preserveInspectionMode)
                    {
                        ExitInspectionOverviewMode();
                    }

                    // First-time result-review initialization is an internal page setup,
                    // not an operator request to stop the latched overview inspection mode.
                    ShowObjectDetectionResultReviewPanel();
                    if (restoreOverview)
                    {
                        ShowInspectionOverviewPanel();
                    }
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    SetCameraInspectionStatus("相機影像已取得，但結果確認頁面尚未就緒。");
                    return;
                }

                EnsureInspectionOverviewResultReviewParametersInitialized();
                selectedChoice = objectDetectionResultReviewParameterComboBox == null
                    ? null
                    : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
                Size imageSize = new Size(frame.Width, frame.Height);
                imageIdentity = CameraMemoryImageIdentityPrefix + sequenceNumber.ToString("D10") + "/" +
                    Guid.NewGuid().ToString("N");
                var imageLoadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                objectDetectionResultReviewImageLoadMilliseconds = null;
                objectDetectionResultReviewImageLoadSucceeded = null;
                ResetObjectDetectionResultReviewProcessingTimings();
                UpdateObjectDetectionResultReviewTimingMemo();
                SetObjectDetectionResultReviewStatus(
                    "已收到相機灰階影像 #" + sequenceNumber + "，直接從記憶體執行尺寸與缺陷判定...");

                isLoadingImage = true;
                SetObjectDetectionResultReviewControlsEnabled(false);
                try
                {
                    ClearObjectDetectionResultReviewResults();
                    memorySource = await Task.Run(
                        () => new LargeImageSource(
                            frame.Pixels,
                            frame.Width,
                            frame.Height,
                            frame.Stride));
                    systemParameters.LastImagePath = imageIdentity;
                    objectDetectionResultReviewImagePath = imageIdentity;
                    imageSavePath = CreateCameraInspectionImagePath(sequenceNumber);
                    latestCameraInspectionFramePath = imageSavePath;
                    latestCameraInspectionFrameIdentity = imageIdentity;
                    StartCameraInspectionImageSave(
                        memorySource,
                        imageSavePath,
                        imageIdentity,
                        sequenceNumber);
                    LoadMemoryImageIntoOriginalDisplays(memorySource);
                    if (objectDetectionResultReviewReferenceImageSize.IsEmpty)
                    {
                        objectDetectionResultReviewReferenceImageSize = imageSize;
                    }
                    imageLoadStopwatch.Stop();
                    objectDetectionResultReviewImageLoadMilliseconds = imageLoadStopwatch.ElapsedMilliseconds;
                    objectDetectionResultReviewImageLoadSucceeded = true;
                    objectDetectionResultReviewImageGeneration = imageContentGeneration;
                    objectDetectionResultReviewImageLabel.Text =
                        "相機影像 #" + sequenceNumber + "  (" +
                        imageSize.Width.ToString("N0") + " x " + imageSize.Height.ToString("N0") + ")";
                    RefreshObjectDetectionFlatFieldDisplay();
                    RefreshObjectDetectionMeasurementDisplay();
                    RefreshObjectDetectionDefectDisplay();
                    UpdateObjectDetectionResultReviewTimingMemo();
                }
                finally
                {
                    if (memorySource != null)
                    {
                        memorySource.ReleaseReference();
                        memorySource = null;
                    }
                    isLoadingImage = false;
                    SetObjectDetectionResultReviewControlsEnabled(true);
                }

                if (!string.Equals(previousCameraImagePath, imageSavePath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteCameraInspectionImage(previousCameraImagePath);
                }

                bool showPendingOverviewSnapshot = returnToInspectionOverview &&
                    isInspectionOverviewActive && overviewIndex >= 0 &&
                    functionListBox.SelectedIndex == overviewIndex;
                if (showPendingOverviewSnapshot)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imageIdentity,
                        selectedChoice == null ? null : selectedChoice.Parameter,
                        null,
                        null,
                        "正在執行尺寸與缺陷判定");
                }

                if (selectedChoice == null || selectedChoice.Parameter == null)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imageIdentity, null, null, null, "目前沒有可執行的檢測參數");
                    SetObjectDetectionResultReviewStatus("相機影像已取得；目前沒有可執行的檢測參數。");
                    SetCameraInspectionStatus("相機影像已保留；目前沒有可執行的檢測參數，總覽標示為待確認。");
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                    return;
                }

                if (!objectDetectionResultReviewReferenceImageSize.IsEmpty &&
                    objectDetectionResultReviewReferenceImageSize != imageSize)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imageIdentity,
                        selectedChoice.Parameter,
                        null,
                        null,
                        "影像尺寸與參數參考規格不符");
                    SetObjectDetectionResultReviewStatus("相機影像規格不符，未送入檢測。");
                    SetCameraInspectionStatus(
                        "相機影像規格不符：收到 " + imageSize.Width + " x " + imageSize.Height +
                        "，參數參考規格為 " + objectDetectionResultReviewReferenceImageSize.Width + " x " +
                        objectDetectionResultReviewReferenceImageSize.Height + "。影像保留於總覽並標示待確認。");
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                    return;
                }

                await RunObjectDetectionResultReviewAsync(showPendingOverviewSnapshot);
                bool judgementCompleted = objectDetectionResultReviewSnapshot != null &&
                    objectDetectionResultReviewSnapshot.JudgementComplete &&
                    string.Equals(objectDetectionResultReviewSnapshot.ImagePath,
                        imageIdentity, StringComparison.OrdinalIgnoreCase);
                if (!judgementCompleted)
                {
                    ObjectDetectionParameterSettings parameter = selectedChoice.Parameter;
                    ObjectDefinitionSettings definition =
                        FindObjectDefinition(parameter.ObjectDefinitionId);
                    List<ObjectDefinitionDetectedObject> objects = definition == null
                        ? new List<ObjectDefinitionDetectedObject>()
                        : SnapshotCompletedObjectDefinitionObjects(
                            definition,
                            CreateObjectDefinitionProcessingSignature(definition));
                    string reason = objectDetectionResultReviewStatusLabel == null
                        ? "檢測流程未完成"
                        : objectDetectionResultReviewStatusLabel.Text;
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imageIdentity, parameter, definition, objects, reason);
                    SetCameraInspectionStatus(
                        "相機影像 #" + sequenceNumber + " 已保留，但判定未完成；總覽以灰色待確認顯示。");
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                    return;
                }

                ReturnCameraInspectionToOverview(returnToInspectionOverview);
                SetCameraInspectionStatus(
                    "相機影像 #" + sequenceNumber + " 已由灰階記憶體影像完成判定；原圖同步保存中。");
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(imageIdentity))
                {
                    try
                    {
                        ObjectDetectionParameterSettings parameter = selectedChoice == null
                            ? null
                            : selectedChoice.Parameter;
                        ObjectDefinitionSettings definition = parameter == null
                            ? null
                            : FindObjectDefinition(parameter.ObjectDefinitionId);
                        List<ObjectDefinitionDetectedObject> objects = definition == null
                            ? new List<ObjectDefinitionDetectedObject>()
                            : SnapshotCompletedObjectDefinitionObjects(
                                definition,
                                CreateObjectDefinitionProcessingSignature(definition));
                        await CaptureUndeterminedCameraInspectionSnapshotAsync(
                            imageIdentity, parameter, definition, objects, exception.Message);
                    }
                    catch
                    {
                    }
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                }
                SetCameraInspectionStatus("相機影像處理失敗：" + exception.Message);
                SetObjectDetectionResultReviewStatus("相機影像處理失敗：" + exception.Message);
            }
            finally
            {
                if (memorySource != null)
                {
                    memorySource.ReleaseReference();
                }

                int droppedCount = Interlocked.Exchange(ref cameraInspectionFramesDropped, 0);
                ObjectDetectionResultReviewSnapshot latestSnapshot = objectDetectionResultReviewSnapshot;
                if (latestSnapshot != null && latestSnapshot.JudgementComplete &&
                    string.Equals(latestSnapshot.ImagePath, latestCameraInspectionFrameIdentity,
                        StringComparison.OrdinalIgnoreCase))
                {
                    lastCameraInspectionCompletedUtc = DateTime.UtcNow;
                }
                QueueStationStatusPublish(true);
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                if (droppedCount > 0 && !IsDisposed)
                {
                    string completedStatus = statusLabel == null || statusLabel.IsDisposed
                        ? string.Empty
                        : statusLabel.Text;
                    SetCameraInspectionStatus(
                        (string.IsNullOrWhiteSpace(completedStatus)
                            ? "本次影像流程已結束"
                            : completedStatus) + "；檢測期間略過 " +
                        droppedCount.ToString("N0") + " 張新影像（未排隊）。");
                }
            }
        }

        private void ReturnCameraInspectionToOverview(bool returnToOverview)
        {
            if (!returnToOverview)
            {
                return;
            }

            int overviewIndex = functionListBox.Items.IndexOf(InspectionOverviewMenuText);
            int resultReviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
            bool isStillOnCameraReviewPage = functionListBox.SelectedIndex == resultReviewIndex;
            bool isAlreadyOnOverviewPage = overviewIndex >= 0 &&
                functionListBox.SelectedIndex == overviewIndex;
            if (overviewIndex >= 0 && isStillOnCameraReviewPage)
            {
                functionListBox.SelectedIndex = overviewIndex;
            }
            else if (isAlreadyOnOverviewPage && isInspectionOverviewActive)
            {
                RefreshInspectionOverview();
            }
        }

        private bool TryGetCameraInspectionBusyReason(out string reason)
        {
            if (isLoadingImage)
            {
                reason = "圖片載入中";
                return true;
            }
            if (isObjectDetectionResultReviewRunning)
            {
                reason = "尺寸與缺陷判定尚未完成";
                return true;
            }
            if (isDetectionRecipeAutoRunActive)
            {
                reason = "檢測參數切換後的自動流程尚未完成";
                return true;
            }
            if (parameterApplyInProgress || isPreparingPreprocessedImage ||
                objectJudgementParameterApplyInProgress)
            {
                reason = "前處理、影像處理或區塊判定尚未完成";
                return true;
            }
            if (objectDetectionDefectProcessingRequested || IsAnyObjectDetectionFrequencyAnalysisRunning)
            {
                reason = "缺陷或頻域檢測尚未完成";
                return true;
            }
            if (IsObjectDefinitionProcessingInProgress())
            {
                reason = "物件搜尋尚未完成";
                return true;
            }

            reason = null;
            return false;
        }

        private bool IsObjectDefinitionProcessingInProgress()
        {
            lock (objectDefinitionResultLock)
            {
                return !string.IsNullOrWhiteSpace(pendingObjectDefinitionProcessingSignature) ||
                    objectDefinitionProcessingInProgress;
            }
        }

        private static string CreateCameraInspectionImagePath(long sequenceNumber)
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IntegratedImageProcessingApp",
                "CameraFrames");
            string fileName = "camera_frame_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" +
                sequenceNumber.ToString("D10") + "_" + Guid.NewGuid().ToString("N") + ".tif";
            return Path.Combine(directory, fileName);
        }

        private void StartCameraInspectionImageSave(
            LargeImageSource source,
            string imagePath,
            string imageIdentity,
            long sequenceNumber)
        {
            LargeImageSource saveSource = source.AddReference();
            Task.Run(delegate
            {
                try
                {
                    SaveCameraInspectionImage(saveSource, imagePath);
                }
                catch (Exception exception)
                {
                    TryDeleteCameraInspectionImage(imagePath);
                    System.Diagnostics.Debug.WriteLine(
                        "Camera image save failed; live inspection continued: " + exception);
                    if (string.Equals(
                        Interlocked.CompareExchange(ref latestCameraInspectionFrameIdentity, null, null),
                        imageIdentity,
                        StringComparison.Ordinal))
                    {
                        try
                        {
                            BeginInvoke(new Action(delegate
                            {
                                if (string.Equals(latestCameraInspectionFrameIdentity, imageIdentity,
                                    StringComparison.Ordinal))
                                {
                                    SetCameraInspectionStatus(
                                        "相機影像 #" + sequenceNumber +
                                        " 原圖保存失敗；檢測仍直接使用灰階記憶體影像執行。" +
                                        exception.Message);
                                }
                            }));
                        }
                        catch (ObjectDisposedException) { }
                        catch (InvalidOperationException) { }
                    }
                }
                finally
                {
                    saveSource.ReleaseReference();
                }

                if (!string.Equals(
                    Interlocked.CompareExchange(ref latestCameraInspectionFrameIdentity, null, null),
                    imageIdentity,
                    StringComparison.Ordinal))
                {
                    TryDeleteCameraInspectionImage(imagePath);
                }
            });
        }

        private static void SaveCameraInspectionImage(LargeImageSource source, string imagePath)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            string directory = Path.GetDirectoryName(imagePath);
            Directory.CreateDirectory(directory);
            using (Cv.Mat grayscale = source.CreateGrayscaleMatView(
                new Rectangle(0, 0, source.Width, source.Height)))
            {
                if (!Cv.Cv2.ImWrite(imagePath, grayscale))
                {
                    throw new IOException("OpenCV 無法寫入相機灰階 TIFF 影像。");
                }
            }
        }

        private bool IsActiveCameraMemoryImageIdentity(string imageIdentity)
        {
            return !string.IsNullOrWhiteSpace(imageIdentity) &&
                imageIdentity.StartsWith(CameraMemoryImageIdentityPrefix, StringComparison.Ordinal) &&
                string.Equals(systemParameters.LastImagePath, imageIdentity, StringComparison.Ordinal) &&
                rightOriginalDisplayControl != null &&
                !rightOriginalDisplayControl.IsDisposed &&
                rightOriginalDisplayControl.IsLargeImageMode &&
                rightOriginalDisplayControl.HasImage;
        }

        private bool TryGetActiveImageSize(string imageIdentity, out Size imageSize)
        {
            imageSize = Size.Empty;
            if (!IsActiveCameraMemoryImageIdentity(imageIdentity))
            {
                return false;
            }

            LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source == null)
            {
                return false;
            }

            try
            {
                imageSize = new Size(source.Width, source.Height);
                return true;
            }
            finally
            {
                source.ReleaseReference();
            }
        }

        private bool HasActiveImageSource(string imageIdentity)
        {
            return !string.IsNullOrWhiteSpace(imageIdentity) &&
                (File.Exists(imageIdentity) || IsActiveCameraMemoryImageIdentity(imageIdentity));
        }

        private static bool IsOwnedCameraInspectionImagePath(string path)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return false;
            }

            try
            {
                string directory = Path.GetFullPath(Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                    "IntegratedImageProcessingApp",
                    "CameraFrames"));
                string fullPath = Path.GetFullPath(path);
                string directoryPrefix = directory.TrimEnd(Path.DirectorySeparatorChar) +
                    Path.DirectorySeparatorChar;
                return fullPath.StartsWith(directoryPrefix, StringComparison.OrdinalIgnoreCase) &&
                    Path.GetFileName(fullPath).StartsWith("camera_frame_", StringComparison.OrdinalIgnoreCase);
            }
            catch
            {
                return false;
            }
        }

        private static void TryDeleteCameraInspectionImage(string path)
        {
            if (!IsOwnedCameraInspectionImagePath(path) || !File.Exists(path))
            {
                return;
            }

            try
            {
                File.Delete(path);
            }
            catch (IOException)
            {
            }
            catch (UnauthorizedAccessException)
            {
            }
        }

        private void SetCameraInspectionStatus(string message)
        {
            if (IsDisposed || !IsHandleCreated || statusLabel == null || statusLabel.IsDisposed)
            {
                return;
            }

            if (InvokeRequired)
            {
                try
                {
                    BeginInvoke(new Action<string>(SetCameraInspectionStatus), message);
                }
                catch (InvalidOperationException)
                {
                }
                return;
            }

            statusLabel.Text = message ?? string.Empty;
        }
    }
}
