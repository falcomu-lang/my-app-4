using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using CameraCaptureApp.Services;

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

                int resultReviewIndex = functionListBox.Items.IndexOf(ObjectDetectionResultReviewMenuText);
                if (resultReviewIndex >= 0 && functionListBox.SelectedIndex != resultReviewIndex)
                {
                    functionListBox.SelectedIndex = resultReviewIndex;
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    ShowObjectDetectionResultReviewPanel();
                }
                if (!IsObjectDetectionResultReviewUiAvailable())
                {
                    SetCameraInspectionStatus("相機影像已取得，但結果確認頁面尚未就緒。");
                    return;
                }

                var selectedChoice = objectDetectionResultReviewParameterComboBox == null
                    ? null
                    : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
                if (selectedChoice == null || selectedChoice.Parameter == null)
                {
                    SetObjectDetectionResultReviewStatus("相機影像已取得；目前沒有可執行的檢測參數。");
                    SetCameraInspectionStatus("相機影像已取得，但目前沒有可執行的檢測參數。");
                    return;
                }

                Size imageSize = new Size(image.Width, image.Height);
                if (!objectDetectionResultReviewReferenceImageSize.IsEmpty &&
                    objectDetectionResultReviewReferenceImageSize != imageSize)
                {
                    SetObjectDetectionResultReviewStatus("相機影像規格不符，未送入檢測。");
                    SetCameraInspectionStatus(
                        "相機影像規格不符：收到 " + imageSize.Width + " x " + imageSize.Height +
                        "，參數參考規格為 " + objectDetectionResultReviewReferenceImageSize.Width + " x " +
                        objectDetectionResultReviewReferenceImageSize.Height + "。該影像未送檢。");
                    return;
                }

                imagePath = await Task.Run(() => SaveCameraInspectionImage(image, sequenceNumber));
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
                    if (objectDetectionResultReviewReferenceImageSize.IsEmpty)
                    {
                        objectDetectionResultReviewReferenceImageSize = imageSize;
                    }

                    await LoadImageIntoOriginalDisplaysAsync(imagePath, CancellationToken.None);
                    imageLoaded = true;
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

                await RunObjectDetectionResultReviewAsync();
                if (returnToInspectionOverview &&
                    objectDetectionResultReviewSnapshot != null &&
                    string.Equals(
                        objectDetectionResultReviewSnapshot.ImagePath,
                        imagePath,
                        StringComparison.OrdinalIgnoreCase))
                {
                    int overviewIndex = functionListBox.Items.IndexOf(InspectionOverviewMenuText);
                    bool isStillOnCameraReviewPage =
                        functionListBox.SelectedIndex == resultReviewIndex;
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
                SetCameraInspectionStatus(
                    "相機影像 #" + sequenceNumber + " 已送入目前檢測參數完成判定。");
            }
            catch (Exception exception)
            {
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
                    SetCameraInspectionStatus(
                        "相機影像檢測期間略過 " + droppedCount.ToString("N0") + " 張新影像；未排隊重複影像。");
                }
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
            if (isDetectionRecipeAutoRunActive || objectDefinitionProcessingRequested ||
                objectJudgementProcessingRequested || preprocessingExecutionRequested ||
                imageProcessingExecutionRequested || objectDetectionDefectProcessingRequested ||
                IsAnyObjectDetectionFrequencyAnalysisRunning)
            {
                reason = "其他影像處理或檢測尚未完成";
                return true;
            }

            reason = null;
            return false;
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
