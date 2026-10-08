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
        private void PrepareObjectDetectionFlatFieldCorrectedPreview(
            FlatFieldCalibrationResult result,
            LargeImageSource largeSource,
            Bitmap original,
            int imageWidth,
            int imageHeight,
            int targetGray,
            int calibrationGeneration,
            int maskGeneration)
        {
            Rectangle imageBounds = new Rectangle(0, 0, imageWidth, imageHeight);
            using (Cv.Mat source = largeSource != null
                ? GetOrCreateLargeRoiOpenCvGrayCache(largeSource, imageBounds)
                : CreateOpenCvGrayMat(original))
            {
                ThrowIfObjectDetectionFlatFieldCalibrationChanged(
                    calibrationGeneration, maskGeneration);
                Stopwatch correctionStopwatch = Stopwatch.StartNew();
                Stopwatch maskPreparationStopwatch = Stopwatch.StartNew();
                List<ObjectDetectionFlatFieldMaskOverlay> masks =
                    CloneObjectDetectionFlatFieldCorrectionMasks(imageBounds);
                maskPreparationStopwatch.Stop();
                result.CorrectionMaskPreparationMilliseconds +=
                    maskPreparationStopwatch.Elapsed.TotalMilliseconds;
                try
                {
                    Stopwatch cloneStopwatch = Stopwatch.StartNew();
                    Cv.Mat corrected = source.Clone();
                    cloneStopwatch.Stop();
                    result.CorrectionCloneMilliseconds +=
                        cloneStopwatch.Elapsed.TotalMilliseconds;
                    try
                    {
                        Stopwatch coefficientPreparationStopwatch = Stopwatch.StartNew();
                        float[] correctionFactors = new float[imageWidth];
                        for (int x = 0; x < imageWidth; x++)
                        {
                            double reference = x < result.SmoothedProfile.Length
                                ? result.SmoothedProfile[x]
                                : 0.0;
                            correctionFactors[x] = x < result.ValidColumns.Length &&
                                result.ValidColumns[x] && reference > 0.0
                                ? (float)(targetGray / reference)
                                : 1.0f;
                        }

                        using (var correctionFactorsRow = new Cv.Mat(
                            1, imageWidth, Cv.MatType.CV_32FC1))
                        {
                            Marshal.Copy(
                                correctionFactors,
                                0,
                                correctionFactorsRow.Data,
                                correctionFactors.Length);

                            coefficientPreparationStopwatch.Stop();
                            result.CorrectionCoefficientPreparationMilliseconds +=
                                coefficientPreparationStopwatch.Elapsed.TotalMilliseconds;
                            const int stripHeight = 1024;
                            foreach (ObjectDetectionFlatFieldMaskOverlay item in masks)
                            {
                                if (item.Mask == null || item.Mask.Empty())
                                {
                                    continue;
                                }

                                int endY = item.Bounds.Bottom;
                                for (int y = item.Bounds.Top; y < endY; y += stripHeight)
                                {
                                    ThrowIfObjectDetectionFlatFieldCalibrationChanged(
                                        calibrationGeneration, maskGeneration);
                                    int height = Math.Min(stripHeight, endY - y);
                                    Rectangle stripBounds = new Rectangle(
                                        0, y, imageWidth, height);
                                    ApplyObjectDetectionFlatFieldCorrectionToChunk(
                                        source, corrected, stripBounds, item,
                                        correctionFactorsRow, result);
                                }
                            }
                        }

                        correctionStopwatch.Stop();
                        result.CorrectionElapsedMilliseconds =
                            correctionStopwatch.Elapsed.TotalMilliseconds;
                        double measuredStageMilliseconds =
                            result.CorrectionCloneMilliseconds +
                            result.CorrectionMaskPreparationMilliseconds +
                            result.CorrectionCoefficientPreparationMilliseconds +
                            result.CorrectionFloatConversionMilliseconds +
                            result.CorrectionMultiplyMilliseconds +
                            result.CorrectionMaskedWriteMilliseconds;
                        result.CorrectionOtherMilliseconds = Math.Max(
                            0.0,
                            result.CorrectionElapsedMilliseconds - measuredStageMilliseconds);
                        ThrowIfObjectDetectionFlatFieldCalibrationChanged(
                            calibrationGeneration, maskGeneration);
                        if (largeSource != null)
                        {
                            result.CorrectedLargeSource = new LargeImageSource(corrected);
                            corrected = null;
                        }
                        else
                        {
                            result.CorrectedBitmap = CreateBitmapFromOpenCvGrayMat(corrected);
                        }
                    }
                    finally
                    {
                        if (corrected != null)
                        {
                            corrected.Dispose();
                        }
                    }
                }
                finally
                {
                    DisposeObjectDetectionFlatFieldMasks(masks);
                }
            }
        }

        private async Task ShowSavedObjectDetectionFlatFieldCalibration(
            ObjectDetectionParameterSettings parameter,
            Label resultLabel,
            Button showButton,
            bool useCurrentProfile = false,
            Action<double> correctionComputed = null)
        {
            if (parameter == null || resultLabel == null || resultLabel.IsDisposed ||
                (showButton != null && !showButton.Enabled))
            {
                return;
            }
            if (!useCurrentProfile &&
                (string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) ||
                 !string.Equals(parameter.FlatFieldSavedSettingsSignature,
                     CreateObjectDetectionFlatFieldSettingsSignature(parameter), StringComparison.Ordinal)))
            {
                resultLabel.Text = "平場設定已變更，請重新計算並保存校正結果。";
                return;
            }
            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage ||
                objectDetectionFlatFieldPreviewDisplayControl == null)
            {
                resultLabel.Text = "請先載入原始影像。";
                return;
            }
            int maskCount;
            lock (objectDetectionFlatFieldMaskLock)
            {
                maskCount = objectDetectionFlatFieldMaskOverlays.Count;
            }
            if (maskCount == 0)
            {
                resultLabel.Text = "請先按「套用來源 MASK」，再顯示已保存的校正結果。";
                return;
            }
            lock (objectDetectionFlatFieldMaskLock)
            {
                if (objectDetectionFlatFieldUseMaskConfigured &&
                    objectDetectionFlatFieldUseMaskOverlays.Count == 0)
                {
                    resultLabel.Text = "使用位置 MASK 未命中任何 ROI，無法顯示補正結果。";
                    return;
                }
            }

            LargeImageSource largeSource = null;
            Bitmap original = null;
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
                return;
            }

            int imageWidth = largeSource == null ? original.Width : largeSource.Width;
            int imageHeight = largeSource == null ? original.Height : largeSource.Height;
            if (imageWidth != (useCurrentProfile
                ? objectDetectionFlatFieldSmoothedProfile == null
                    ? 0 : objectDetectionFlatFieldSmoothedProfile.Length
                : parameter.FlatFieldSavedImageWidth))
            {
                if (original != null)
                {
                    original.Dispose();
                }
                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
                resultLabel.Text = "影像寬度與保存的校正結果不符，請重新計算。";
                return;
            }

            int generation = Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            int maskGeneration = Interlocked.CompareExchange(
                ref objectDetectionFlatFieldEvaluationGeneration, 0, 0);
            int capturedImageGeneration = imageSourceGeneration;
            string parameterId = parameter.Id;
            int targetGray = useCurrentProfile
                ? parameter.FlatFieldTargetGray : parameter.FlatFieldSavedTargetGray;
            if (targetGray < 1 || targetGray > 255)
            {
                if (original != null)
                {
                    original.Dispose();
                }
                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
                resultLabel.Text = "保存的目標灰階無效，請重新計算平場校正。";
                return;
            }
            FlatFieldCalibrationResult savedProfile;
            try
            {
                savedProfile = useCurrentProfile
                    ? new FlatFieldCalibrationResult
                    {
                        RawProfile = objectDetectionFlatFieldRawProfile,
                        SmoothedProfile = objectDetectionFlatFieldSmoothedProfile,
                        MeasuredColumns = objectDetectionFlatFieldMeasuredColumns,
                        ValidColumns = objectDetectionFlatFieldValidColumns,
                        ValidColumnCount = objectDetectionFlatFieldMeasuredColumns == null
                            ? 0 : objectDetectionFlatFieldMeasuredColumns.Count(value => value)
                    }
                    : GetSavedObjectDetectionFlatFieldProfile(parameter);
                if (savedProfile == null)
                {
                    throw new InvalidOperationException("保存的校正設定已變更。");
                }
            }
            catch (Exception exception)
            {
                if (original != null)
                {
                    original.Dispose();
                }
                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
                resultLabel.Text = "無法載入平場校正結果：" + exception.Message;
                return;
            }
            if (showButton != null)
            {
                showButton.Enabled = false;
            }
            resultLabel.Text = "正在顯示保存的平場校正結果...";
            if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
            {
                objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：計算中...";
            }
            FlatFieldCalibrationResult result = null;
            try
            {
                result = await Task.Run(delegate
                {
                    var decoded = new FlatFieldCalibrationResult
                    {
                        RawProfile = savedProfile.RawProfile,
                        SmoothedProfile = savedProfile.SmoothedProfile,
                        MeasuredColumns = savedProfile.MeasuredColumns,
                        ValidColumns = savedProfile.ValidColumns,
                        ValidColumnCount = savedProfile.ValidColumnCount
                    };
                    try
                    {
                        PrepareObjectDetectionFlatFieldCorrectedPreview(
                            decoded, largeSource, original, imageWidth, imageHeight,
                            targetGray, generation, maskGeneration);
                        return decoded;
                    }
                    catch (OperationCanceledException)
                    {
                        decoded.Dispose();
                        return null;
                    }
                    catch
                    {
                        decoded.Dispose();
                        throw;
                    }
                });
                if (result == null)
                {
                    return;
                }
                if (IsDisposed || resultLabel.IsDisposed ||
                    !isObjectDetectionParameterImageLayout ||
                    generation != Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldCalibrationGeneration, 0, 0) ||
                    maskGeneration != Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldEvaluationGeneration, 0, 0) ||
                    capturedImageGeneration != imageSourceGeneration ||
                    !string.Equals(activeObjectDetectionParameterId, parameterId, StringComparison.Ordinal))
                {
                    return;
                }

                if (correctionComputed != null)
                {
                    correctionComputed(result.CorrectionElapsedMilliseconds);
                }

                objectDetectionFlatFieldRawProfile = result.RawProfile;
                objectDetectionFlatFieldSmoothedProfile = result.SmoothedProfile;
                objectDetectionFlatFieldMeasuredColumns = result.MeasuredColumns;
                objectDetectionFlatFieldValidColumns = result.ValidColumns;
                objectDetectionFlatFieldProfileParameterId = parameterId;
                objectDetectionFlatFieldProfileImageGeneration = capturedImageGeneration;
                objectDetectionFlatFieldProfileMaskGeneration = maskGeneration;
                if (objectDetectionFlatFieldProfilePanel != null &&
                    !objectDetectionFlatFieldProfilePanel.IsDisposed)
                {
                    objectDetectionFlatFieldProfilePanel.Invalidate();
                }
                DisplayObjectDetectionFlatFieldResult(result, targetGray);
                UpdateObjectDetectionFlatFieldCorrectionTiming(result);
                objectDetectionFlatFieldPreviewWidth = imageWidth;
                objectDetectionFlatFieldPreviewHeight = imageHeight;
                objectDetectionFlatFieldPreviewImageGeneration = capturedImageGeneration;
                objectDetectionFlatFieldPreviewIsCorrected = true;
                objectDetectionFlatFieldCorrectedImageGeneration = capturedImageGeneration;
                RefreshObjectDetectionDefectDisplay();
                SetObjectDetectionFlatFieldMaskOverlayVisible(false);
                int filledColumnCount = 0;
                for (int x = 0; x < imageWidth; x++)
                {
                    if (result.ValidColumns[x] && !result.MeasuredColumns[x])
                    {
                        filledColumnCount++;
                    }
                }
                resultLabel.Text = "已顯示校正結果：實測 " +
                    result.ValidColumnCount.ToString("N0", CultureInfo.CurrentCulture) +
                    " 欄，補值 " + filledColumnCount.ToString("N0", CultureInfo.CurrentCulture) +
                    " 欄；補正位置：" +
                    (string.IsNullOrWhiteSpace(parameter.FlatFieldUseMaskPrimaryId)
                        ? "來源 MASK" : parameter.FlatFieldUseMaskDisplayName) + "。";
                statusLabel.Text = parameter.DisplayName + "：校正結果已顯示（實測 " +
                    result.ValidColumnCount.ToString("N0", CultureInfo.CurrentCulture) +
                    "，補值 " + filledColumnCount.ToString("N0", CultureInfo.CurrentCulture) + " 欄）";
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
            catch (OperationCanceledException)
            {
                // A newer MASK or calibration request superseded this preview.
            }
            catch (Exception exception)
            {
                bool isCurrentOperation = generation == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldCalibrationGeneration, 0, 0) &&
                    maskGeneration == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldEvaluationGeneration, 0, 0) &&
                    capturedImageGeneration == imageSourceGeneration &&
                    string.Equals(activeObjectDetectionParameterId, parameterId, StringComparison.Ordinal);
                if (!IsDisposed && !resultLabel.IsDisposed && isCurrentOperation)
                {
                    resultLabel.Text = "顯示平場校正結果失敗：" + exception.Message;
                    if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                        !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
                    {
                        objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：未完成";
                    }
                }
            }
            finally
            {
                if (result != null)
                {
                    result.Dispose();
                }
                if (original != null)
                {
                    original.Dispose();
                }
                if (largeSource != null)
                {
                    largeSource.ReleaseReference();
                }
                if (!IsDisposed && showButton != null && !showButton.IsDisposed)
                {
                    showButton.Enabled = true;
                }
            }
        }

        private static void ApplyObjectDetectionFlatFieldCorrectionToChunk(
            Cv.Mat source,
            Cv.Mat destination,
            Rectangle chunkBounds,
            ObjectDetectionFlatFieldMaskOverlay maskOverlay,
            Cv.Mat correctionFactorsRow,
            FlatFieldCalibrationResult timingResult)
        {
            if (source == null || destination == null || maskOverlay == null ||
                maskOverlay.Mask == null || maskOverlay.Mask.Empty() ||
                correctionFactorsRow == null || correctionFactorsRow.Empty() ||
                timingResult == null)
            {
                return;
            }

            Stopwatch maskPreparationStopwatch = Stopwatch.StartNew();
            Rectangle intersection = Rectangle.Intersect(maskOverlay.Bounds, chunkBounds);
            if (intersection.Width <= 0 || intersection.Height <= 0)
            {
                maskPreparationStopwatch.Stop();
                timingResult.CorrectionMaskPreparationMilliseconds +=
                    maskPreparationStopwatch.Elapsed.TotalMilliseconds;
                return;
            }

            int maskX = intersection.X - maskOverlay.Bounds.X;
            int maskY = intersection.Y - maskOverlay.Bounds.Y;
            using (var sourceChunk = new Cv.Mat(
                source,
                new Cv.Rect(intersection.X, intersection.Y,
                    intersection.Width, intersection.Height)))
            using (var destinationChunk = new Cv.Mat(
                destination,
                new Cv.Rect(intersection.X, intersection.Y,
                    intersection.Width, intersection.Height)))
            using (var sourceMask = new Cv.Mat(
                maskOverlay.Mask,
                new Cv.Rect(maskX, maskY, intersection.Width, intersection.Height)))
            using (var factorRowChunk = new Cv.Mat(
                correctionFactorsRow,
                new Cv.Rect(intersection.X, 0, intersection.Width, 1)))
            {
                maskPreparationStopwatch.Stop();
                timingResult.CorrectionMaskPreparationMilliseconds +=
                    maskPreparationStopwatch.Elapsed.TotalMilliseconds;

                using (var stripFactors = new Cv.Mat())
                using (var sourceFloat = new Cv.Mat())
                using (var correctedFloat = new Cv.Mat())
                using (var correctedByte = new Cv.Mat())
                {
                    Stopwatch coefficientExpansionStopwatch = Stopwatch.StartNew();
                    Cv.Cv2.Repeat(factorRowChunk, sourceChunk.Rows, 1, stripFactors);
                    coefficientExpansionStopwatch.Stop();
                    timingResult.CorrectionCoefficientPreparationMilliseconds +=
                        coefficientExpansionStopwatch.Elapsed.TotalMilliseconds;

                    Stopwatch floatConversionStopwatch = Stopwatch.StartNew();
                    sourceChunk.ConvertTo(sourceFloat, Cv.MatType.CV_32FC1);
                    floatConversionStopwatch.Stop();
                    timingResult.CorrectionFloatConversionMilliseconds +=
                        floatConversionStopwatch.Elapsed.TotalMilliseconds;

                    Stopwatch multiplyStopwatch = Stopwatch.StartNew();
                    Cv.Cv2.Multiply(sourceFloat, stripFactors, correctedFloat);
                    multiplyStopwatch.Stop();
                    timingResult.CorrectionMultiplyMilliseconds +=
                        multiplyStopwatch.Elapsed.TotalMilliseconds;

                    floatConversionStopwatch.Restart();
                    correctedFloat.ConvertTo(correctedByte, Cv.MatType.CV_8UC1);
                    floatConversionStopwatch.Stop();
                    timingResult.CorrectionFloatConversionMilliseconds +=
                        floatConversionStopwatch.Elapsed.TotalMilliseconds;

                    Stopwatch maskedWriteStopwatch = Stopwatch.StartNew();
                    correctedByte.CopyTo(destinationChunk, sourceMask);
                    maskedWriteStopwatch.Stop();
                    timingResult.CorrectionMaskedWriteMilliseconds +=
                        maskedWriteStopwatch.Elapsed.TotalMilliseconds;
                }
            }
        }

        private void ThrowIfObjectDetectionFlatFieldCalibrationChanged(
            int calibrationGeneration,
            int maskGeneration)
        {
            if (IsDisposed || calibrationGeneration != Interlocked.CompareExchange(
                    ref objectDetectionFlatFieldCalibrationGeneration,
                    0,
                    0) ||
                maskGeneration != Interlocked.CompareExchange(
                    ref objectDetectionFlatFieldEvaluationGeneration,
                    0,
                    0))
            {
                throw new OperationCanceledException("平場校正設定或 MASK 已變更。");
            }
        }

        private static Bitmap CloneBitmapRegionAs24Bpp(Bitmap source, Rectangle bounds)
        {
            var result = new Bitmap(bounds.Width, bounds.Height, PixelFormat.Format24bppRgb);
            using (Graphics graphics = Graphics.FromImage(result))
            {
                graphics.DrawImage(
                    source,
                    new Rectangle(0, 0, bounds.Width, bounds.Height),
                    bounds,
                    GraphicsUnit.Pixel);
            }
            return result;
        }

        private static Bitmap CreateBitmapFromOpenCvGrayMat(Cv.Mat grayscale)
        {
            var bitmap = new Bitmap(grayscale.Cols, grayscale.Rows, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(
                new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly,
                PixelFormat.Format24bppRgb);
            bool locked = true;
            try
            {
                byte[] grayRow = new byte[grayscale.Cols];
                byte[] colorRow = new byte[Math.Abs(data.Stride)];
                long step = grayscale.Step();
                for (int y = 0; y < grayscale.Rows; y++)
                {
                    Array.Clear(colorRow, 0, colorRow.Length);
                    Marshal.Copy(
                        IntPtr.Add(grayscale.Data, checked((int)(y * step))),
                        grayRow,
                        0,
                        grayRow.Length);
                    for (int x = 0; x < grayscale.Cols; x++)
                    {
                        int offset = x * 3;
                        colorRow[offset] = grayRow[x];
                        colorRow[offset + 1] = grayRow[x];
                        colorRow[offset + 2] = grayRow[x];
                    }
                    Marshal.Copy(colorRow, 0, data.Scan0 + y * data.Stride, colorRow.Length);
                }
            }
            catch
            {
                bitmap.UnlockBits(data);
                locked = false;
                bitmap.Dispose();
                throw;
            }
            finally
            {
                if (locked)
                {
                    bitmap.UnlockBits(data);
                }
            }

            return bitmap;
        }

        private sealed class FlatFieldCalibrationResult : IDisposable
        {
            public double CorrectionElapsedMilliseconds { get; set; }

            public double CorrectionCloneMilliseconds { get; set; }

            public double CorrectionMaskPreparationMilliseconds { get; set; }

            public double CorrectionCoefficientPreparationMilliseconds { get; set; }

            public double CorrectionFloatConversionMilliseconds { get; set; }

            public double CorrectionMultiplyMilliseconds { get; set; }

            public double CorrectionMaskedWriteMilliseconds { get; set; }

            public double CorrectionOtherMilliseconds { get; set; }

            public double[] RawProfile { get; set; }

            public double[] SmoothedProfile { get; set; }

            public bool[] MeasuredColumns { get; set; }

            public bool[] ValidColumns { get; set; }

            public int ValidColumnCount { get; set; }

            public Bitmap CorrectedBitmap { get; set; }

            public LargeImageSource CorrectedLargeSource { get; set; }

            public void Dispose()
            {
                if (CorrectedBitmap != null)
                {
                    CorrectedBitmap.Dispose();
                    CorrectedBitmap = null;
                }
                if (CorrectedLargeSource != null)
                {
                    CorrectedLargeSource.ReleaseReference();
                    CorrectedLargeSource = null;
                }
            }
        }
    }
}
