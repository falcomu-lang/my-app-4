using System;
using System.Collections.Generic;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CameraCaptureApp.Services;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private int cameraInspectionFrameInFlight;
        private int cameraInspectionFramesDropped;
        private string latestCameraInspectionFramePath;

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

            Bitmap image;
            try
            {
                image = e.CreateImageCopy();
            }
            catch (Exception exception)
            {
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
                SetCameraInspectionStatus("相機影像複製失敗：" + exception.Message);
                return;
            }

            long sequenceNumber = e.SequenceNumber;
            try
            {
                BeginInvoke(new Action(async delegate
                {
                    await ProcessCameraAcquiredImageAsync(image, sequenceNumber);
                }));
            }
            catch (ObjectDisposedException)
            {
                image.Dispose();
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
            }
            catch (InvalidOperationException)
            {
                image.Dispose();
                Interlocked.Exchange(ref cameraInspectionFrameInFlight, 0);
            }
        }

        private async Task ProcessCameraAcquiredImageAsync(Bitmap image, long sequenceNumber)
        {
            string imagePath = null;
            bool imageLoaded = false;
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
                if (!returnToInspectionOverview && resultReviewIndex >= 0 &&
                    functionListBox.SelectedIndex != resultReviewIndex)
                {
                    functionListBox.SelectedIndex = resultReviewIndex;
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    bool restoreOverview = returnToInspectionOverview;
                    if (restoreOverview)
                    {
                        ExitInspectionOverviewMode();
                    }
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

                selectedChoice = objectDetectionResultReviewParameterComboBox == null
                    ? null
                    : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
                Size imageSize = new Size(image.Width, image.Height);
                imagePath = await Task.Run(() => SaveCameraInspectionImage(image, sequenceNumber));
                latestCameraInspectionFramePath = imagePath;
                var imageLoadStopwatch = System.Diagnostics.Stopwatch.StartNew();
                objectDetectionResultReviewImageLoadMilliseconds = null;
                objectDetectionResultReviewImageLoadSucceeded = null;
                ResetObjectDetectionResultReviewProcessingTimings();
                UpdateObjectDetectionResultReviewTimingMemo();
                SetObjectDetectionResultReviewStatus(
                    "已收到相機影像 #" + sequenceNumber + "，正在載入並執行尺寸與缺陷判定...");

                isLoadingImage = true;
                SetObjectDetectionResultReviewControlsEnabled(false);
                try
                {
                    ClearObjectDetectionResultReviewResults();
                    systemParameters.LastImagePath = imagePath;
                    objectDetectionResultReviewImagePath = imagePath;
                    await LoadImageIntoOriginalDisplaysAsync(imagePath, CancellationToken.None);
                    imageLoaded = true;
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
                    isLoadingImage = false;
                    SetObjectDetectionResultReviewControlsEnabled(true);
                }

                latestCameraInspectionFramePath = imagePath;
                if (!string.Equals(previousCameraImagePath, imagePath, StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteCameraInspectionImage(previousCameraImagePath);
                }

                bool showPendingOverviewSnapshot = returnToInspectionOverview &&
                    isInspectionOverviewActive && overviewIndex >= 0 &&
                    functionListBox.SelectedIndex == overviewIndex;
                if (showPendingOverviewSnapshot)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imagePath,
                        selectedChoice == null ? null : selectedChoice.Parameter,
                        null,
                        null,
                        "正在執行尺寸與缺陷判定");
                }

                if (selectedChoice == null || selectedChoice.Parameter == null)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imagePath, null, null, null, "目前沒有可執行的檢測參數");
                    SetObjectDetectionResultReviewStatus("相機影像已取得；目前沒有可執行的檢測參數。");
                    SetCameraInspectionStatus("相機影像已保留；目前沒有可執行的檢測參數，總覽標示為待確認。");
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                    return;
                }

                if (!objectDetectionResultReviewReferenceImageSize.IsEmpty &&
                    objectDetectionResultReviewReferenceImageSize != imageSize)
                {
                    await CaptureUndeterminedCameraInspectionSnapshotAsync(
                        imagePath,
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
                        imagePath, StringComparison.OrdinalIgnoreCase);
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
                        imagePath, parameter, definition, objects, reason);
                    SetCameraInspectionStatus(
                        "相機影像 #" + sequenceNumber + " 已保留，但判定未完成；總覽以灰色待確認顯示。");
                    ReturnCameraInspectionToOverview(returnToInspectionOverview);
                    return;
                }

                ReturnCameraInspectionToOverview(returnToInspectionOverview);
                SetCameraInspectionStatus(
                    "相機影像 #" + sequenceNumber + " 已送入目前檢測參數完成判定。");
            }
            catch (Exception exception)
            {
                if (!string.IsNullOrWhiteSpace(imagePath))
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
                            imagePath, parameter, definition, objects, exception.Message);
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
                if (image != null)
                {
                    image.Dispose();
                }
                if (!imageLoaded && !string.Equals(
                    systemParameters.LastImagePath,
                    imagePath,
                    StringComparison.OrdinalIgnoreCase))
                {
                    TryDeleteCameraInspectionImage(imagePath);
                }

                int droppedCount = Interlocked.Exchange(ref cameraInspectionFramesDropped, 0);
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

        private static string SaveCameraInspectionImage(Bitmap image, long sequenceNumber)
        {
            string directory = Path.Combine(
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
                "IntegratedImageProcessingApp",
                "CameraFrames");
            Directory.CreateDirectory(directory);
            string fileName = "camera_frame_" + DateTime.UtcNow.ToString("yyyyMMdd_HHmmss_fff") + "_" +
                sequenceNumber.ToString("D10") + "_" + Guid.NewGuid().ToString("N") + ".tif";
            string path = Path.Combine(directory, fileName);
            using (var stream = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.Read))
            {
                image.Save(stream, ImageFormat.Tiff);
            }

            return path;
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
