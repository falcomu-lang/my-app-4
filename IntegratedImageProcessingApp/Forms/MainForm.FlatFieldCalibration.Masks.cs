using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private void SetObjectDetectionFlatFieldMaskOverlayVisible(bool visible)
        {
            objectDetectionFlatFieldShowMaskOverlay = visible;
            if (objectDetectionFlatFieldShowMaskCheckBox != null &&
                !objectDetectionFlatFieldShowMaskCheckBox.IsDisposed)
            {
                objectDetectionFlatFieldShowMaskCheckBox.Checked = visible;
            }
            if (objectDetectionFlatFieldPreviewDisplayControl != null &&
                !objectDetectionFlatFieldPreviewDisplayControl.IsDisposed)
            {
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
        }

        private List<ObjectDetectionFlatFieldMaskOverlay> CloneObjectDetectionFlatFieldMasks(
            Rectangle bounds)
        {
            lock (objectDetectionFlatFieldMaskLock)
            {
                return CloneObjectDetectionFlatFieldMasks(
                    objectDetectionFlatFieldMaskOverlays, bounds);
            }
        }

        private List<ObjectDetectionFlatFieldMaskOverlay> CloneObjectDetectionFlatFieldCorrectionMasks(
            Rectangle bounds)
        {
            lock (objectDetectionFlatFieldMaskLock)
            {
                return CloneObjectDetectionFlatFieldMasks(
                    objectDetectionFlatFieldUseMaskConfigured
                        ? objectDetectionFlatFieldUseMaskOverlays
                        : objectDetectionFlatFieldMaskOverlays,
                    bounds);
            }
        }

        private static List<ObjectDetectionFlatFieldMaskOverlay> CloneObjectDetectionFlatFieldMasks(
            IEnumerable<ObjectDetectionFlatFieldMaskOverlay> sourceMasks,
            Rectangle bounds)
        {
            var copies = new List<ObjectDetectionFlatFieldMaskOverlay>();
            if (bounds.Width <= 0 || bounds.Height <= 0)
            {
                return copies;
            }

            try
            {
                foreach (ObjectDetectionFlatFieldMaskOverlay item in sourceMasks)
                {
                    if (item.Mask == null || item.Mask.Empty() ||
                        item.Mask.Cols != item.Bounds.Width || item.Mask.Rows != item.Bounds.Height)
                    {
                        continue;
                    }

                    Rectangle intersection = Rectangle.Intersect(item.Bounds, bounds);
                    if (intersection.Width <= 0 || intersection.Height <= 0)
                    {
                        continue;
                    }

                    using (var view = new Cv.Mat(
                        item.Mask,
                        new Cv.Rect(
                            intersection.X - item.Bounds.X,
                            intersection.Y - item.Bounds.Y,
                            intersection.Width,
                            intersection.Height)))
                    {
                        Cv.Mat copy = view.Clone();
                        try
                        {
                            copies.Add(new ObjectDetectionFlatFieldMaskOverlay
                            {
                                Number = item.Number,
                                Bounds = intersection,
                                Mask = copy
                            });
                        }
                        catch
                        {
                            copy.Dispose();
                            throw;
                        }
                    }
                }
            }
            catch
            {
                DisposeObjectDetectionFlatFieldMasks(copies);
                throw;
            }

            return copies;
        }

        private static void DisposeObjectDetectionFlatFieldMasks(
            IEnumerable<ObjectDetectionFlatFieldMaskOverlay> masks)
        {
            if (masks == null)
            {
                return;
            }

            foreach (ObjectDetectionFlatFieldMaskOverlay item in masks)
            {
                if (item != null && item.Mask != null)
                {
                    item.Mask.Dispose();
                }
            }
        }

        private async void ApplyObjectDetectionFlatFieldMask(
            ObjectDetectionParameterSettings parameter,
            Label resultLabel,
            Button applyButton,
            bool preserveCurrentProfile = false)
        {
            if (parameter == null || resultLabel == null ||
                string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId))
            {
                if (resultLabel != null)
                {
                    resultLabel.Text = "請先選擇並套用平場校正來源 MASK。";
                }
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            string processingSignature = definition == null
                ? string.Empty
                : CreateObjectDefinitionProcessingSignature(definition);
            if (definition == null ||
                !HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                resultLabel.Text = "請先完成此檢測參數所關聯的物件定義處理。";
                statusLabel.Text = parameter.DisplayName + " 平場預覽尚未更新：請先完成物件定義處理";
                return;
            }

            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage)
            {
                resultLabel.Text = "尚未載入原始影像。";
                statusLabel.Text = "平場 MASK 預覽失敗：尚未載入原始影像";
                return;
            }

            var objects = new List<ObjectDefinitionDetectedObject>();
            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(completedObjectDefinitionProcessingSignature, processingSignature, StringComparison.Ordinal))
                {
                    resultLabel.Text = "物件定義結果已變更，請重新處理後再套用 MASK。";
                    return;
                }

                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = roiRegion.Bounds;
                    if (roi.Width <= 0 || roi.Height <= 0)
                    {
                        continue;
                    }

                    List<ObjectDefinitionDetectedObject> roiObjects;
                    if (objectDefinitionResults.TryGetValue(
                        CreateObjectDefinitionResultKey(definition.Id, roi),
                        out roiObjects) && roiObjects != null)
                    {
                        objects.AddRange(roiObjects.Select(item => new ObjectDefinitionDetectedObject
                        {
                            Number = item.Number,
                            Bounds = item.Bounds
                        }));
                    }
                }
            }

            if (objects.Count == 0)
            {
                ClearObjectDetectionFlatFieldMaskOverlays();
                resultLabel.Text = "物件定義目前沒有找到可套用 MASK 的 ROI 物件。";
                statusLabel.Text = parameter.DisplayName + " 平場 MASK 預覽完成：找到 0 個物件";
                return;
            }

            LargeImageSource largeSource = null;
            Bitmap original = null;
            var createdOverlays = new List<ObjectDetectionFlatFieldMaskOverlay>();
            var createdUseOverlays = new List<ObjectDetectionFlatFieldMaskOverlay>();
            bool canReuseCurrentProfile = preserveCurrentProfile &&
                objectDetectionFlatFieldSmoothedProfile != null &&
                objectDetectionFlatFieldValidColumns != null &&
                objectDetectionFlatFieldProfileImageGeneration == imageSourceGeneration &&
                objectDetectionFlatFieldProfileMaskGeneration == objectDetectionFlatFieldEvaluationGeneration &&
                string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id,
                    StringComparison.Ordinal);
            int generation = ++objectDetectionFlatFieldEvaluationGeneration;
            if (applyButton == null)
            {
                objectDetectionFlatFieldAutoMaskGeneration = generation;
            }
            int capturedImageGeneration = imageSourceGeneration;
            int capturedObjectDefinitionGeneration = objectDefinitionResultGeneration;
            string parameterId = parameter.Id;
            try
            {
                if (rightOriginalDisplayControl.IsLargeImageMode)
                {
                    largeSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                }
                else
                {
                    original = rightOriginalDisplayControl.CloneImage();
                }

                if (largeSource == null && original == null)
                {
                    resultLabel.Text = "無法取得原始影像資料。";
                    statusLabel.Text = "平場 MASK 預覽失敗：無法取得原始影像資料";
                    return;
                }

                ObjectDetectionParameterSettings maskParameter =
                    CreateObjectDetectionFlatFieldMaskParameter(parameter, false);
                ObjectDetectionParameterSettings useMaskParameter =
                    string.IsNullOrWhiteSpace(parameter.FlatFieldUseMaskPrimaryId)
                        ? null : CreateObjectDetectionFlatFieldMaskParameter(parameter, true);
                resultLabel.Text = "正在準備 " + objects.Count.ToString("N0", CultureInfo.CurrentCulture) +
                    " 個 ROI 的 MASK 預覽...";
                if (applyButton != null)
                {
                    applyButton.Enabled = false;
                }
                statusLabel.Text = parameter.DisplayName + "：正在準備所有 ROI 的平場 MASK 預覽...";

                int[] failureCounts = new int[2];
                bool completed = await Task.Run(
                    delegate
                    {
                        if (!BuildObjectDetectionFlatFieldMaskOverlays(
                            objects, maskParameter, largeSource, original,
                            generation, createdOverlays, out failureCounts[0]))
                        {
                            return false;
                        }
                        if (useMaskParameter != null &&
                            !BuildObjectDetectionFlatFieldMaskOverlays(
                                objects, useMaskParameter, largeSource, original,
                                generation, createdUseOverlays, out failureCounts[1]))
                        {
                            return false;
                        }
                        return generation == objectDetectionFlatFieldEvaluationGeneration;
                    });
                if (!completed)
                {
                    return;
                }
                int failedCount = failureCounts[0];
                int failedUseCount = failureCounts[1];

                if (!IsDisposed && isObjectDetectionParameterImageLayout &&
                    generation == objectDetectionFlatFieldEvaluationGeneration &&
                    capturedImageGeneration == imageSourceGeneration &&
                    capturedObjectDefinitionGeneration == objectDefinitionResultGeneration &&
                    string.Equals(activeObjectDetectionParameterId, parameterId, StringComparison.Ordinal) &&
                    HasCompletedObjectDefinitionResult(definition, processingSignature) &&
                    !resultLabel.IsDisposed)
                {
                    ReplaceObjectDetectionFlatFieldMaskOverlays(
                        createdOverlays, createdUseOverlays, useMaskParameter != null);
                    createdOverlays = new List<ObjectDetectionFlatFieldMaskOverlay>();
                    createdUseOverlays = new List<ObjectDetectionFlatFieldMaskOverlay>();
                    ClearObjectDetectionFlatFieldCorrectedImage();
                    if (objectDetectionFlatFieldShowMaskCheckBox != null &&
                        !objectDetectionFlatFieldShowMaskCheckBox.IsDisposed)
                    {
                        objectDetectionFlatFieldShowMaskCheckBox.Text = useMaskParameter == null
                            ? "顯示來源 MASK" : "顯示使用位置 MASK";
                    }
                    SetObjectDetectionFlatFieldMaskOverlayVisible(true);
                    int renderedCount = objectDetectionFlatFieldMaskOverlays.Count;
                    resultLabel.Text = "來源 MASK：" + parameter.FlatFieldMaskDisplayName +
                        "\r\n已套用到 " + renderedCount.ToString("N0", CultureInfo.CurrentCulture) +
                        " / " + objects.Count.ToString("N0", CultureInfo.CurrentCulture) +
                        " 個已找到的 ROI 物件" +
                        (failedCount > 0 ? "；" + failedCount.ToString("N0", CultureInfo.CurrentCulture) + " 個未取得 MASK" : "") +
                        (useMaskParameter == null ? "" : "\r\n使用位置 MASK：" +
                            objectDetectionFlatFieldUseMaskOverlays.Count.ToString("N0", CultureInfo.CurrentCulture) +
                            " 個 ROI" + (failedUseCount > 0 ? "，未命中 " + failedUseCount.ToString("N0", CultureInfo.CurrentCulture) : ""));
                    statusLabel.Text = parameter.DisplayName + " 平場 MASK 預覽完成：" +
                        renderedCount.ToString("N0", CultureInfo.CurrentCulture) + " 個物件" +
                        (failedCount > 0 ? "，未命中 " + failedCount.ToString("N0", CultureInfo.CurrentCulture) : string.Empty);
                    RefreshObjectDetectionFlatFieldDisplay();
                    objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
                    bool hasUseMasks = useMaskParameter == null ||
                        objectDetectionFlatFieldUseMaskOverlays.Count > 0;
                    if (!hasUseMasks &&
                        objectDetectionFlatFieldCalibrationStatusLabel != null &&
                        !objectDetectionFlatFieldCalibrationStatusLabel.IsDisposed)
                    {
                        objectDetectionFlatFieldCalibrationStatusLabel.Text =
                            "使用位置 MASK 未命中任何 ROI，尚未產生補正預覽。";
                    }
                    if (renderedCount > 0 && hasUseMasks &&
                        !string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) &&
                        string.Equals(parameter.FlatFieldSavedSettingsSignature,
                            CreateObjectDetectionFlatFieldSettingsSignature(parameter),
                            StringComparison.Ordinal) &&
                        objectDetectionFlatFieldCalibrationStatusLabel != null &&
                        !objectDetectionFlatFieldCalibrationStatusLabel.IsDisposed)
                    {
                        await ShowSavedObjectDetectionFlatFieldCalibration(
                            parameter, objectDetectionFlatFieldCalibrationStatusLabel, null);
                    }
                    else if (canReuseCurrentProfile && renderedCount > 0 && hasUseMasks &&
                        objectDetectionFlatFieldCalibrationStatusLabel != null &&
                        !objectDetectionFlatFieldCalibrationStatusLabel.IsDisposed)
                    {
                        objectDetectionFlatFieldProfileMaskGeneration = generation;
                        await ShowSavedObjectDetectionFlatFieldCalibration(
                            parameter, objectDetectionFlatFieldCalibrationStatusLabel, null, true);
                    }
                }
            }
            catch (Exception exception)
            {
                if (!IsDisposed && generation == objectDetectionFlatFieldEvaluationGeneration &&
                    resultLabel != null && !resultLabel.IsDisposed)
                {
                    resultLabel.Text = "平場 MASK 預覽失敗：" + exception.Message;
                }
                if (!IsDisposed && generation == objectDetectionFlatFieldEvaluationGeneration)
                {
                    statusLabel.Text = "平場 MASK 預覽失敗：" + exception.Message;
                }
            }
            finally
            {
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in createdOverlays)
                {
                    overlay.Mask.Dispose();
                }
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in createdUseOverlays)
                {
                    overlay.Mask.Dispose();
                }
                if (original != null)
                {
                    original.Dispose();
                }
                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
                bool isCurrentGeneration =
                    generation == objectDetectionFlatFieldEvaluationGeneration;
                if (isCurrentGeneration)
                {
                    InvalidateObjectDetectionMeasurementMaskCache();
                    if (!IsDisposed && applyButton != null && !applyButton.IsDisposed)
                    {
                        applyButton.Enabled = true;
                    }
                }
                if (applyButton == null && objectDetectionFlatFieldAutoMaskGeneration == generation)
                {
                    objectDetectionFlatFieldAutoMaskGeneration = -1;
                }
            }
        }

        private static ObjectDetectionParameterSettings CreateObjectDetectionFlatFieldMaskParameter(
            ObjectDetectionParameterSettings parameter,
            bool usePosition)
        {
            return new ObjectDetectionParameterSettings
            {
                Id = (parameter.Id ?? string.Empty) + (usePosition ? ":FlatFieldUse" : ":FlatField"),
                ObjectDefinitionId = parameter.ObjectDefinitionId,
                SourceMaskMode = usePosition ? parameter.FlatFieldUseMaskMode : parameter.FlatFieldMaskMode,
                SourceMaskPrimaryType = usePosition ? parameter.FlatFieldUseMaskPrimaryType : parameter.FlatFieldMaskPrimaryType,
                SourceMaskPrimaryId = usePosition ? parameter.FlatFieldUseMaskPrimaryId : parameter.FlatFieldMaskPrimaryId,
                SourceMaskPrimaryNamespace = usePosition ? parameter.FlatFieldUseMaskPrimaryNamespace : parameter.FlatFieldMaskPrimaryNamespace,
                SourceMaskOperation = usePosition ? parameter.FlatFieldUseMaskOperation : parameter.FlatFieldMaskOperation,
                SourceMaskSecondaryType = usePosition ? parameter.FlatFieldUseMaskSecondaryType : parameter.FlatFieldMaskSecondaryType,
                SourceMaskSecondaryId = usePosition ? parameter.FlatFieldUseMaskSecondaryId : parameter.FlatFieldMaskSecondaryId,
                SourceMaskSecondaryNamespace = usePosition ? parameter.FlatFieldUseMaskSecondaryNamespace : parameter.FlatFieldMaskSecondaryNamespace
            };
        }

        private bool BuildObjectDetectionFlatFieldMaskOverlays(
            IEnumerable<ObjectDefinitionDetectedObject> objects,
            ObjectDetectionParameterSettings maskParameter,
            LargeImageSource largeSource,
            Bitmap original,
            int generation,
            List<ObjectDetectionFlatFieldMaskOverlay> overlays,
            out int failed)
        {
            failed = 0;
            foreach (ObjectDefinitionDetectedObject item in objects)
            {
                if (generation != objectDetectionFlatFieldEvaluationGeneration)
                {
                    return false;
                }

                Cv.Mat mask;
                if (!TryGetObjectDetectionMeasurementMask(maskParameter, item.Number,
                    item.Bounds, largeSource, original, out mask) || mask == null ||
                    mask.Empty() || mask.Rows != item.Bounds.Height ||
                    mask.Cols != item.Bounds.Width)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                    failed++;
                    continue;
                }

                Cv.Mat normalizedMask = null;
                Cv.Mat binaryMask = null;
                try
                {
                    if (mask.Type() != Cv.MatType.CV_8UC1)
                    {
                        normalizedMask = new Cv.Mat();
                        mask.ConvertTo(normalizedMask, Cv.MatType.CV_8UC1);
                        mask.Dispose();
                        mask = normalizedMask;
                        normalizedMask = null;
                    }
                    binaryMask = new Cv.Mat();
                    Cv.Cv2.Threshold(mask, binaryMask, 0, 255, Cv.ThresholdTypes.Binary);
                    mask.Dispose();
                    mask = binaryMask;
                    binaryMask = null;
                    overlays.Add(new ObjectDetectionFlatFieldMaskOverlay
                    {
                        Number = item.Number,
                        Bounds = item.Bounds,
                        Mask = mask
                    });
                    mask = null;
                }
                finally
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                    if (normalizedMask != null)
                    {
                        normalizedMask.Dispose();
                    }
                    if (binaryMask != null)
                    {
                        binaryMask.Dispose();
                    }
                }
            }
            return generation == objectDetectionFlatFieldEvaluationGeneration;
        }

        private void TryApplySavedObjectDetectionFlatFieldCalibration(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(activeObjectDetectionParameterId, parameter.Id,
                    StringComparison.Ordinal) ||
                string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId) ||
                string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) ||
                !string.Equals(parameter.FlatFieldSavedSettingsSignature,
                    CreateObjectDetectionFlatFieldSettingsSignature(parameter),
                    StringComparison.Ordinal) ||
                objectDetectionFlatFieldResultLabel == null ||
                objectDetectionFlatFieldResultLabel.IsDisposed ||
                rightOriginalDisplayControl == null ||
                !rightOriginalDisplayControl.HasImage)
            {
                return;
            }

            lock (objectDetectionFlatFieldMaskLock)
            {
                if (objectDetectionFlatFieldMaskOverlays.Count != 0)
                {
                    return;
                }
            }

            if (objectDetectionFlatFieldAutoMaskGeneration ==
                objectDetectionFlatFieldEvaluationGeneration)
            {
                return;
            }

            ApplyObjectDetectionFlatFieldMask(
                parameter, objectDetectionFlatFieldResultLabel, null);
        }

        private void RefreshObjectDetectionFlatFieldDisplay()
        {
            if (objectDetectionFlatFieldPreviewDisplayControl == null ||
                IsImageDisplayUpdateSuppressed)
            {
                return;
            }

            ImageDisplayControl source = rightOriginalDisplayControl != null &&
                rightOriginalDisplayControl.HasImage
                ? rightOriginalDisplayControl
                : leftOriginalDisplayControl;
            if (source == null || !source.HasImage)
            {
                return;
            }

            if (objectDetectionFlatFieldPreviewIsCorrected &&
                objectDetectionFlatFieldCorrectedImageGeneration == imageSourceGeneration &&
                objectDetectionFlatFieldPreviewDisplayControl.HasImage)
            {
                return;
            }

            if (objectDetectionFlatFieldPreviewIsCorrected &&
                objectDetectionFlatFieldCorrectedImageGeneration != imageSourceGeneration)
            {
                objectDetectionFlatFieldPreviewIsCorrected = false;
                objectDetectionFlatFieldCorrectedImageGeneration = -1;
                ReleaseObjectDetectionFlatFieldCorrectedLargeSource();
            }

            if (objectDetectionFlatFieldPreviewImageGeneration == imageSourceGeneration &&
                !objectDetectionFlatFieldPreviewIsCorrected &&
                objectDetectionFlatFieldPreviewDisplayControl.HasImage)
            {
                return;
            }

            if (source.IsLargeImageMode)
            {
                LargeImageSource largeSource = source.GetSharedLargeImageSource();
                if (largeSource == null)
                {
                    return;
                }

                try
                {
                    objectDetectionFlatFieldPreviewDisplayControl.SetSharedLargeImageSource(
                        largeSource,
                        true);
                    objectDetectionFlatFieldPreviewWidth = largeSource.Width;
                    objectDetectionFlatFieldPreviewHeight = largeSource.Height;
                }
                finally
                {
                    largeSource.ReleaseReference();
                }
            }
            else
            {
                Bitmap image = source.CloneImage();
                if (image == null)
                {
                    return;
                }

                objectDetectionFlatFieldPreviewDisplayControl.SetDisplayImage(image, true);
                objectDetectionFlatFieldPreviewWidth = image.Width;
                objectDetectionFlatFieldPreviewHeight = image.Height;
            }

            objectDetectionFlatFieldPreviewImageGeneration = imageSourceGeneration;
            UpdateObjectDetectionFlatFieldSamplingStatus(
                FindObjectDetectionParameter(activeObjectDetectionParameterId));
        }

        private void ClearObjectDetectionFlatFieldMaskOverlays()
        {
            Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            lock (objectDetectionFlatFieldMaskLock)
            {
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in objectDetectionFlatFieldMaskOverlays)
                {
                    if (overlay.Mask != null)
                    {
                        overlay.Mask.Dispose();
                    }
                }

                objectDetectionFlatFieldMaskOverlays.Clear();
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in objectDetectionFlatFieldUseMaskOverlays)
                {
                    if (overlay.Mask != null)
                    {
                        overlay.Mask.Dispose();
                    }
                }
                objectDetectionFlatFieldUseMaskOverlays.Clear();
                objectDetectionFlatFieldUseMaskConfigured = false;
            }

            if (objectDetectionFlatFieldPreviewDisplayControl != null &&
                !objectDetectionFlatFieldPreviewDisplayControl.IsDisposed)
            {
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
        }

        private void ReplaceObjectDetectionFlatFieldMaskOverlays(
            List<ObjectDetectionFlatFieldMaskOverlay> overlays,
            List<ObjectDetectionFlatFieldMaskOverlay> useOverlays,
            bool useMaskConfigured)
        {
            Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            lock (objectDetectionFlatFieldMaskLock)
            {
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in objectDetectionFlatFieldMaskOverlays)
                {
                    if (overlay.Mask != null)
                    {
                        overlay.Mask.Dispose();
                    }
                }
                objectDetectionFlatFieldMaskOverlays = overlays ??
                    new List<ObjectDetectionFlatFieldMaskOverlay>();
                foreach (ObjectDetectionFlatFieldMaskOverlay overlay in objectDetectionFlatFieldUseMaskOverlays)
                {
                    if (overlay.Mask != null)
                    {
                        overlay.Mask.Dispose();
                    }
                }
                objectDetectionFlatFieldUseMaskOverlays = useOverlays ??
                    new List<ObjectDetectionFlatFieldMaskOverlay>();
                objectDetectionFlatFieldUseMaskConfigured = useMaskConfigured;
            }
        }

        private void ObjectDetectionFlatFieldPreviewDisplayControl_ImageOverlayPaint(
            object sender,
            ImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display == null || display.IsPanning || e == null ||
                !isObjectDetectionParameterImageLayout)
            {
                return;
            }

            if (objectDetectionFlatFieldShowMaskOverlay)
            {
                DrawObjectDetectionFlatFieldMaskOverlays(
                    e.Graphics,
                    e.VisibleSourceRect,
                    e.Zoom,
                    e.Offset);
            }
            DrawObjectDetectionFlatFieldSamplingBand(
                e.Graphics,
                e.VisibleSourceRect,
                e.Zoom,
                e.Offset);
        }

        private void ObjectDetectionFlatFieldPreviewDisplayControl_LargeImageOverlayPaint(
            object sender,
            LargeImageOverlayPaintEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display == null || display.IsPanning || e == null ||
                !isObjectDetectionParameterImageLayout)
            {
                return;
            }

            if (objectDetectionFlatFieldShowMaskOverlay)
            {
                DrawObjectDetectionFlatFieldMaskOverlays(
                    e.Graphics,
                    e.VisibleSourceRect,
                    e.Zoom,
                    e.Offset);
            }
            DrawObjectDetectionFlatFieldSamplingBand(
                e.Graphics,
                e.VisibleSourceRect,
                e.Zoom,
                e.Offset);
        }

        private void DrawObjectDetectionFlatFieldMaskOverlays(
            Graphics graphics,
            Rectangle visibleSourceRect,
            float zoom,
            PointF offset)
        {
            if (graphics == null || zoom <= 0f || visibleSourceRect.Width <= 0 ||
                visibleSourceRect.Height <= 0)
            {
                return;
            }

            double displayedWidth = visibleSourceRect.Width * (double)zoom;
            double displayedHeight = visibleSourceRect.Height * (double)zoom;
            double previewScale = Math.Min(1.0, Math.Min(
                2048.0 / Math.Max(1.0, displayedWidth),
                2048.0 / Math.Max(1.0, displayedHeight)));
            int targetWidth = Math.Max(1, (int)Math.Ceiling(displayedWidth * previewScale));
            int targetHeight = Math.Max(1, (int)Math.Ceiling(displayedHeight * previewScale));
            double scaleX = targetWidth / (double)visibleSourceRect.Width;
            double scaleY = targetHeight / (double)visibleSourceRect.Height;

            using (var composedMask = new Cv.Mat(
                targetHeight,
                targetWidth,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0)))
            {
                lock (objectDetectionFlatFieldMaskLock)
                {
                    IEnumerable<ObjectDetectionFlatFieldMaskOverlay> visibleMasks =
                        objectDetectionFlatFieldUseMaskConfigured
                            ? objectDetectionFlatFieldUseMaskOverlays
                            : objectDetectionFlatFieldMaskOverlays;
                    foreach (ObjectDetectionFlatFieldMaskOverlay item in visibleMasks)
                    {
                        if (item.Mask == null || item.Mask.Empty() ||
                            item.Mask.Cols != item.Bounds.Width || item.Mask.Rows != item.Bounds.Height)
                        {
                            continue;
                        }

                        Rectangle visibleMaskBounds = Rectangle.Intersect(item.Bounds, visibleSourceRect);
                        if (visibleMaskBounds.Width <= 0 || visibleMaskBounds.Height <= 0)
                        {
                            continue;
                        }

                        int localX = visibleMaskBounds.X - item.Bounds.X;
                        int localY = visibleMaskBounds.Y - item.Bounds.Y;
                        using (var visibleMask = new Cv.Mat(
                            item.Mask,
                            new Cv.Rect(localX, localY, visibleMaskBounds.Width, visibleMaskBounds.Height)))
                        {
                        int destinationX = Math.Max(0, (int)Math.Floor(
                            (visibleMaskBounds.Left - visibleSourceRect.Left) * scaleX));
                        int destinationY = Math.Max(0, (int)Math.Floor(
                            (visibleMaskBounds.Top - visibleSourceRect.Top) * scaleY));
                        int destinationRight = Math.Min(targetWidth, (int)Math.Ceiling(
                            (visibleMaskBounds.Right - visibleSourceRect.Left) * scaleX));
                        int destinationBottom = Math.Min(targetHeight, (int)Math.Ceiling(
                            (visibleMaskBounds.Bottom - visibleSourceRect.Top) * scaleY));
                        int destinationWidth = destinationRight - destinationX;
                        int destinationHeight = destinationBottom - destinationY;
                        if (destinationWidth <= 0 || destinationHeight <= 0)
                        {
                            continue;
                        }

                        Cv.Mat scaledMask = null;
                        try
                        {
                            Cv.Mat maskToMerge = visibleMask;
                            if (visibleMask.Cols != destinationWidth ||
                                visibleMask.Rows != destinationHeight)
                            {
                                scaledMask = new Cv.Mat();
                                Cv.Cv2.Resize(
                                    visibleMask,
                                    scaledMask,
                                    new Cv.Size(destinationWidth, destinationHeight),
                                    0,
                                    0,
                                    destinationWidth < visibleMask.Cols ||
                                        destinationHeight < visibleMask.Rows
                                        ? Cv.InterpolationFlags.Area
                                        : Cv.InterpolationFlags.Nearest);
                                maskToMerge = scaledMask;
                            }

                            using (var destination = new Cv.Mat(
                                composedMask,
                                new Cv.Rect(
                                    destinationX,
                                    destinationY,
                                    destinationWidth,
                                    destinationHeight)))
                            {
                                Cv.Cv2.BitwiseOr(destination, maskToMerge, destination);
                            }
                        }
                        finally
                        {
                            if (scaledMask != null)
                            {
                                scaledMask.Dispose();
                            }
                        }
                        }
                    }
                }

                using (Bitmap overlay = CreateObjectDetectionMaskOverlay(
                    composedMask,
                    ObjectDetectionMeasurementMaskColor))
                {
                    DrawLargeProcessedOverlayTile(
                        graphics,
                        overlay,
                        visibleSourceRect,
                        zoom,
                        offset);
                }
            }
        }


        private sealed class ObjectDetectionFlatFieldMaskOverlay
        {
            public int Number { get; set; }

            public Rectangle Bounds { get; set; }

            public Cv.Mat Mask { get; set; }
        }
    }
}
