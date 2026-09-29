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
        private readonly Dictionary<string, SavedFlatFieldProfile> savedFlatFieldProfiles =
            new Dictionary<string, SavedFlatFieldProfile>(StringComparer.Ordinal);

        private Label objectDetectionFlatFieldResultLabel;
        private int objectDetectionFlatFieldEvaluationGeneration;
        private int objectDetectionFlatFieldAutoMaskGeneration = -1;
        private int objectDetectionFlatFieldCalibrationGeneration;
        private readonly object objectDetectionFlatFieldMaskLock = new object();
        private TabPage objectDetectionFlatFieldPreviewTabPage;
        private Panel objectDetectionFlatFieldPreviewDisplayHostPanel;
        private ImageDisplayControl objectDetectionFlatFieldPreviewDisplayControl;
        private int objectDetectionFlatFieldPreviewImageGeneration = -1;
        private int objectDetectionFlatFieldPreviewWidth;
        private int objectDetectionFlatFieldPreviewHeight;
        private bool objectDetectionFlatFieldPreviewIsCorrected;
        private bool objectDetectionFlatFieldShowMaskOverlay = true;
        private CheckBox objectDetectionFlatFieldShowMaskCheckBox;
        private int objectDetectionFlatFieldCorrectedImageGeneration = -1;
        private LargeImageSource objectDetectionFlatFieldCorrectedLargeSource;
        private int objectDetectionFlatFieldCorrectedTargetGray = 128;
        private Label objectDetectionFlatFieldSamplingStatusLabel;
        private Label objectDetectionFlatFieldCalibrationStatusLabel;
        private Label objectDetectionFlatFieldCorrectionTimingLabel;
        private Panel objectDetectionFlatFieldProfilePanel;
        private double[] objectDetectionFlatFieldRawProfile;
        private double[] objectDetectionFlatFieldSmoothedProfile;
        private bool[] objectDetectionFlatFieldMeasuredColumns;
        private bool[] objectDetectionFlatFieldValidColumns;
        private string objectDetectionFlatFieldProfileParameterId;
        private int objectDetectionFlatFieldProfileImageGeneration = -1;
        private int objectDetectionFlatFieldProfileMaskGeneration = -1;
        private List<ObjectDetectionFlatFieldMaskOverlay> objectDetectionFlatFieldMaskOverlays =
            new List<ObjectDetectionFlatFieldMaskOverlay>();
        private List<ObjectDetectionFlatFieldMaskOverlay> objectDetectionFlatFieldUseMaskOverlays =
            new List<ObjectDetectionFlatFieldMaskOverlay>();
        private bool objectDetectionFlatFieldUseMaskConfigured;

        private sealed class SavedFlatFieldProfile
        {
            public string Data;
            public string Signature;
            public int Width;
            public FlatFieldCalibrationResult Profile;
        }

        private void ObjectDetectionFlatFieldPreviewDisplayControl_ImageMouseDown(
            object sender,
            ImageMouseEventArgs e)
        {
            if (e == null || e.Button != MouseButtons.Left || !e.IsInsideImage ||
                (e.Modifiers & Keys.Control) != Keys.Control ||
                !isObjectDetectionParameterImageLayout)
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                return;
            }

            int imageHeight = objectDetectionFlatFieldPreviewHeight;
            if (imageHeight <= 1)
            {
                return;
            }

            parameter.FlatFieldSamplePositionConfigured = true;
            parameter.FlatFieldSampleYRatio = Math.Max(0.0, Math.Min(1.0,
                e.ImageLocation.Y / (double)(imageHeight - 1)));
            SaveSystemParameters();
            InvalidateObjectDetectionFlatFieldCalibration(parameter, true);
            UpdateObjectDetectionFlatFieldSamplingStatus(parameter);
            if (objectDetectionFlatFieldPreviewDisplayControl != null)
            {
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
            if (objectDetectionFlatFieldCalibrationStatusLabel != null &&
                !objectDetectionFlatFieldCalibrationStatusLabel.IsDisposed)
            {
                objectDetectionFlatFieldCalibrationStatusLabel.Text =
                    "取樣位置已更新；請確認取樣帶與 MASK 後，再計算校正。";
            }
            statusLabel.Text = parameter.DisplayName + "：已更新平場取樣位置（全寬水平帶）";
            e.Handled = true;
        }

        private void UpdateObjectDetectionFlatFieldSamplingStatus(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionFlatFieldSamplingStatusLabel == null ||
                objectDetectionFlatFieldSamplingStatusLabel.IsDisposed || parameter == null)
            {
                return;
            }

            if (!parameter.FlatFieldSamplePositionConfigured ||
                objectDetectionFlatFieldPreviewHeight <= 1)
            {
                objectDetectionFlatFieldSamplingStatusLabel.Text =
                    "尚未選取取樣位置；請在左側平場校正影像按住 Ctrl 點一下。";
                return;
            }

            int centerY = GetObjectDetectionFlatFieldSampleCenterY(
                parameter,
                objectDetectionFlatFieldPreviewHeight);
            int requestedHeight = Math.Max(50, Math.Min(1000, parameter.FlatFieldSamplingHeight));
            int top = Math.Max(0, centerY - requestedHeight / 2);
            int bottom = Math.Min(objectDetectionFlatFieldPreviewHeight, top + requestedHeight);
            objectDetectionFlatFieldSamplingStatusLabel.Text =
                "取樣中心 Y=" + centerY.ToString("N0", CultureInfo.CurrentCulture) +
                "；高度=" + Math.Max(0, bottom - top).ToString("N0", CultureInfo.CurrentCulture) +
                " px；由影像最左延伸至最右（僅採 MASK 內像素）。";
        }

        private static int GetObjectDetectionFlatFieldSampleCenterY(
            ObjectDetectionParameterSettings parameter,
            int imageHeight)
        {
            if (parameter == null || imageHeight <= 1)
            {
                return 0;
            }

            double ratio = parameter.FlatFieldSampleYRatio;
            if (double.IsNaN(ratio) || double.IsInfinity(ratio))
            {
                ratio = 0.5;
            }
            ratio = Math.Max(0.0, Math.Min(1.0, ratio));
            return (int)Math.Round(ratio * (imageHeight - 1));
        }

        private void InvalidateObjectDetectionFlatFieldCalibration(
            ObjectDetectionParameterSettings parameter,
            bool resetPreview)
        {
            Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            objectDetectionFlatFieldRawProfile = null;
            objectDetectionFlatFieldSmoothedProfile = null;
            objectDetectionFlatFieldMeasuredColumns = null;
            objectDetectionFlatFieldValidColumns = null;
            objectDetectionFlatFieldProfileParameterId = null;
            objectDetectionFlatFieldProfileImageGeneration = -1;
            objectDetectionFlatFieldProfileMaskGeneration = -1;
            if (objectDetectionFlatFieldProfilePanel != null &&
                !objectDetectionFlatFieldProfilePanel.IsDisposed)
            {
                objectDetectionFlatFieldProfilePanel.Invalidate();
            }

            if (resetPreview)
            {
                ClearObjectDetectionFlatFieldCorrectionPreview();
            }

            UpdateObjectDetectionFlatFieldSamplingStatus(parameter);
            if (objectDetectionFlatFieldCalibrationStatusLabel != null &&
                !objectDetectionFlatFieldCalibrationStatusLabel.IsDisposed)
            {
                objectDetectionFlatFieldCalibrationStatusLabel.Text = "設定已變更；尚未重新計算校正。";
            }
        }

        private void ClearObjectDetectionFlatFieldCorrectionPreview()
        {
            Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            bool wasCorrected = objectDetectionFlatFieldPreviewIsCorrected;
            objectDetectionFlatFieldPreviewIsCorrected = false;
            objectDetectionFlatFieldCorrectedImageGeneration = -1;
            objectDetectionFlatFieldRawProfile = null;
            objectDetectionFlatFieldSmoothedProfile = null;
            objectDetectionFlatFieldMeasuredColumns = null;
            objectDetectionFlatFieldValidColumns = null;
            objectDetectionFlatFieldProfileParameterId = null;
            objectDetectionFlatFieldProfileImageGeneration = -1;
            objectDetectionFlatFieldProfileMaskGeneration = -1;
            if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
            {
                objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：待套用";
            }
            if (wasCorrected)
            {
                objectDetectionFlatFieldPreviewImageGeneration = -1;
                RefreshObjectDetectionFlatFieldDisplay();
            }
            ClearObjectDetectionDefectDisplayImage();
            ReleaseObjectDetectionFlatFieldCorrectedLargeSource();
            if (objectDetectionFlatFieldProfilePanel != null &&
                !objectDetectionFlatFieldProfilePanel.IsDisposed)
            {
                objectDetectionFlatFieldProfilePanel.Invalidate();
            }
        }

        private void ClearObjectDetectionFlatFieldCorrectedImage()
        {
            objectDetectionFlatFieldPreviewIsCorrected = false;
            objectDetectionFlatFieldCorrectedImageGeneration = -1;
            objectDetectionFlatFieldPreviewImageGeneration = -1;
            RefreshObjectDetectionFlatFieldDisplay();
            ClearObjectDetectionDefectDisplayImage();
            ReleaseObjectDetectionFlatFieldCorrectedLargeSource();
            if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
            {
                objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：待套用";
            }
        }

        private void ReleaseObjectDetectionFlatFieldCorrectedLargeSource()
        {
            LargeImageSource source = objectDetectionFlatFieldCorrectedLargeSource;
            objectDetectionFlatFieldCorrectedLargeSource = null;
            if (source != null)
            {
                source.ReleaseReference();
            }
        }

        private void DisplayObjectDetectionFlatFieldResult(
            FlatFieldCalibrationResult result,
            int targetGray)
        {
            if (result.CorrectedLargeSource != null)
            {
                objectDetectionFlatFieldPreviewDisplayControl.SetSharedLargeImageSource(
                    result.CorrectedLargeSource, true);
            }
            else if (result.CorrectedBitmap != null)
            {
                objectDetectionFlatFieldPreviewDisplayControl.SetDisplayImage(
                    result.CorrectedBitmap, true);
            }
            else
            {
                throw new InvalidOperationException("平場校正沒有可顯示的影像。");
            }

            LargeImageSource previousSource = objectDetectionFlatFieldCorrectedLargeSource;
            objectDetectionFlatFieldCorrectedLargeSource = result.CorrectedLargeSource;
            objectDetectionFlatFieldCorrectedTargetGray = Math.Max(1, Math.Min(255, targetGray));
            result.CorrectedLargeSource = null;
            result.CorrectedBitmap = null;
            if (previousSource != null)
            {
                previousSource.ReleaseReference();
            }
        }

        private void UpdateObjectDetectionFlatFieldCorrectionTiming(
            FlatFieldCalibrationResult result)
        {
            if (result == null || objectDetectionFlatFieldCorrectionTimingLabel == null ||
                objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
            {
                return;
            }

            objectDetectionFlatFieldCorrectionTimingLabel.Text =
                "補正總計：" + result.CorrectionElapsedMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " ms（不含顯示）" + Environment.NewLine +
                "Clone：" + result.CorrectionCloneMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " | MASK：" + result.CorrectionMaskPreparationMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " | 係數：" + result.CorrectionCoefficientPreparationMilliseconds.ToString("F1", CultureInfo.CurrentCulture) + " ms" + Environment.NewLine +
                "浮點：" + result.CorrectionFloatConversionMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " | 相乘：" + result.CorrectionMultiplyMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " | 寫回：" + result.CorrectionMaskedWriteMilliseconds.ToString("F1", CultureInfo.CurrentCulture) +
                " | 其他：" + result.CorrectionOtherMilliseconds.ToString("F1", CultureInfo.CurrentCulture) + " ms";
        }

        private void DrawObjectDetectionFlatFieldSamplingBand(
            Graphics graphics,
            Rectangle visibleSourceRect,
            float zoom,
            PointF offset)
        {
            if (objectDetectionFlatFieldPreviewIsCorrected ||
                graphics == null || zoom <= 0f || objectDetectionFlatFieldPreviewWidth <= 0 ||
                objectDetectionFlatFieldPreviewHeight <= 0)
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null || !parameter.FlatFieldSamplePositionConfigured)
            {
                return;
            }

            int centerY = GetObjectDetectionFlatFieldSampleCenterY(
                parameter,
                objectDetectionFlatFieldPreviewHeight);
            int requestedHeight = Math.Max(50, Math.Min(1000, parameter.FlatFieldSamplingHeight));
            Rectangle band = new Rectangle(
                0,
                Math.Max(0, centerY - requestedHeight / 2),
                objectDetectionFlatFieldPreviewWidth,
                requestedHeight);
            band.Height = Math.Min(
                objectDetectionFlatFieldPreviewHeight - band.Y,
                band.Height);
            Rectangle visibleBand = Rectangle.Intersect(band, visibleSourceRect);
            if (visibleBand.Width <= 0 || visibleBand.Height <= 0)
            {
                return;
            }

            RectangleF displayBounds = new RectangleF(
                offset.X + visibleBand.X * zoom,
                offset.Y + visibleBand.Y * zoom,
                visibleBand.Width * zoom,
                visibleBand.Height * zoom);
            using (var fill = new SolidBrush(Color.FromArgb(24, 0, 145, 220)))
            using (var outline = new Pen(Color.FromArgb(235, 0, 125, 200), 1f))
            {
                graphics.FillRectangle(fill, displayBounds);
                graphics.DrawRectangle(
                    outline,
                    displayBounds.X,
                    displayBounds.Y,
                    Math.Max(1f, displayBounds.Width),
                    Math.Max(1f, displayBounds.Height));
            }
        }

        private void ObjectDetectionFlatFieldProfilePanel_Paint(object sender, PaintEventArgs e)
        {
            Panel panel = sender as Panel;
            if (panel == null || panel.ClientSize.Width < 20 || panel.ClientSize.Height < 20)
            {
                return;
            }

            double[] raw = objectDetectionFlatFieldRawProfile;
            double[] smoothed = objectDetectionFlatFieldSmoothedProfile;
            bool[] measured = objectDetectionFlatFieldMeasuredColumns;
            bool[] valid = objectDetectionFlatFieldValidColumns;
            if (raw == null || smoothed == null || valid == null || raw.Length == 0)
            {
                TextRenderer.DrawText(
                    e.Graphics,
                    "尚未產生校正曲線",
                    panel.Font,
                    panel.ClientRectangle,
                    Color.FromArgb(115, 123, 135),
                    TextFormatFlags.HorizontalCenter | TextFormatFlags.VerticalCenter);
                return;
            }

            const int left = 34;
            const int right = 8;
            const int top = 8;
            const int bottom = 19;
            var plot = new Rectangle(
                left,
                top,
                Math.Max(1, panel.ClientSize.Width - left - right),
                Math.Max(1, panel.ClientSize.Height - top - bottom));
            double minimum = 255.0;
            double maximum = 0.0;
            for (int x = 0; x < raw.Length; x++)
            {
                if (!valid[x])
                {
                    continue;
                }
                minimum = Math.Min(minimum, smoothed[x]);
                maximum = Math.Max(maximum, smoothed[x]);
                if (measured != null && measured[x])
                {
                    minimum = Math.Min(minimum, raw[x]);
                    maximum = Math.Max(maximum, raw[x]);
                }
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            int target = parameter == null ? 128 : parameter.FlatFieldTargetGray;
            minimum = Math.Min(minimum, target);
            maximum = Math.Max(maximum, target);
            if (maximum - minimum < 1.0)
            {
                minimum = Math.Max(0.0, minimum - 1.0);
                maximum = Math.Min(255.0, maximum + 1.0);
            }

            using (var axis = new Pen(Color.FromArgb(210, 215, 222)))
            using (var rawPen = new Pen(Color.FromArgb(105, 150, 190), 1f))
            using (var smoothPen = new Pen(Color.FromArgb(220, 100, 55), 1.5f))
            using (var targetPen = new Pen(Color.FromArgb(135, 135, 135), 1f))
            using (var legendBrush = new SolidBrush(Color.FromArgb(80, 88, 100)))
            {
                targetPen.DashStyle = System.Drawing.Drawing2D.DashStyle.Dash;
                e.Graphics.DrawRectangle(axis, plot);
                float targetY = (float)(plot.Bottom -
                    (target - minimum) / (maximum - minimum) * plot.Height);
                e.Graphics.DrawLine(targetPen, plot.Left, targetY, plot.Right, targetY);
                DrawObjectDetectionFlatFieldProfileCurve(
                    e.Graphics, plot, raw, measured ?? valid, minimum, maximum, rawPen);
                DrawObjectDetectionFlatFieldProfileCurve(
                    e.Graphics, plot, smoothed, valid, minimum, maximum, smoothPen);
                e.Graphics.DrawString("實測", panel.Font, legendBrush, plot.Left + 4, plot.Top + 2);
                e.Graphics.DrawString("平滑/補值", panel.Font, smoothPen.Brush, plot.Left + 42, plot.Top + 2);
            }
        }

        private static void DrawObjectDetectionFlatFieldProfileCurve(
            Graphics graphics,
            Rectangle plot,
            double[] values,
            bool[] valid,
            double minimum,
            double maximum,
            Pen pen)
        {
            if (values == null || values.Length == 0)
            {
                return;
            }

            Point? previous = null;
            int lastX = -1;
            int width = Math.Max(1, plot.Width - 1);
            for (int x = 0; x < values.Length; x++)
            {
                if (!valid[x])
                {
                    previous = null;
                    continue;
                }

                int displayX = plot.Left + (int)Math.Round(
                    x * (double)width / Math.Max(1, values.Length - 1));
                if (displayX == lastX)
                {
                    continue;
                }
                lastX = displayX;
                int displayY = plot.Bottom - (int)Math.Round(
                    (values[x] - minimum) / (maximum - minimum) * Math.Max(1, plot.Height - 1));
                var current = new Point(displayX, displayY);
                if (previous.HasValue)
                {
                    graphics.DrawLine(pen, previous.Value, current);
                }
                previous = current;
            }
        }

        private async void RunObjectDetectionFlatFieldCalibration(
            ObjectDetectionParameterSettings parameter,
            Label resultLabel,
            Label samplingLabel,
            Panel profilePanel,
            Button runButton)
        {
            if (parameter == null || resultLabel == null || samplingLabel == null ||
                runButton == null || resultLabel.IsDisposed)
            {
                return;
            }
            if (!parameter.FlatFieldSamplePositionConfigured)
            {
                resultLabel.Text = "請先按住 Ctrl，在左側平場校正影像點選取樣位置。";
                return;
            }
            if (string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId))
            {
                resultLabel.Text = "請先選擇並套用平場校正來源 MASK。";
                return;
            }
            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage)
            {
                resultLabel.Text = "尚未載入原始影像。";
                return;
            }

            int maskCount;
            lock (objectDetectionFlatFieldMaskLock)
            {
                maskCount = objectDetectionFlatFieldMaskOverlays.Count;
            }
            if (maskCount == 0)
            {
                resultLabel.Text = "請先按「套用來源 MASK」，建立目前物件群的 MASK 預覽。";
                return;
            }
            lock (objectDetectionFlatFieldMaskLock)
            {
                if (objectDetectionFlatFieldUseMaskConfigured &&
                    objectDetectionFlatFieldUseMaskOverlays.Count == 0)
                {
                    resultLabel.Text = "使用位置 MASK 未命中任何 ROI，無法產生補正預覽。";
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
            int centerY = GetObjectDetectionFlatFieldSampleCenterY(parameter, imageHeight);
            int requestedHeight = Math.Max(50, Math.Min(1000, parameter.FlatFieldSamplingHeight));
            int bandTop = Math.Max(0, centerY - requestedHeight / 2);
            int bandHeight = Math.Min(requestedHeight, imageHeight - bandTop);
            int generation = Interlocked.Increment(ref objectDetectionFlatFieldCalibrationGeneration);
            int maskGeneration = Interlocked.CompareExchange(
                ref objectDetectionFlatFieldEvaluationGeneration,
                0,
                0);
            int capturedImageGeneration = imageSourceGeneration;
            string parameterId = parameter.Id;
            string displayName = parameter.DisplayName;
            int targetGray = Math.Max(1, Math.Min(255, parameter.FlatFieldTargetGray));
            int smoothingWindow = Math.Max(1, Math.Min(501, parameter.FlatFieldSmoothingWindow));
            if (smoothingWindow % 2 == 0)
            {
                smoothingWindow++;
            }
            string smoothingMode = parameter.FlatFieldSmoothingMode;
            runButton.Enabled = false;
            resultLabel.Text = "正在以 MASK 內像素計算全寬灰階輪廓與校正預覽...";
            if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
            {
                objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：計算中...";
            }
            samplingLabel.Text = "取樣帶：Y=" + centerY.ToString("N0", CultureInfo.CurrentCulture) +
                "，高度=" + bandHeight.ToString("N0", CultureInfo.CurrentCulture) + " px，全影像寬度。";
            statusLabel.Text = displayName + "：平場校正運算中...";

            FlatFieldCalibrationResult result = null;
            Stopwatch stopwatch = Stopwatch.StartNew();
            try
            {
                result = await Task.Run(delegate
                {
                    return CalculateObjectDetectionFlatField(
                        largeSource,
                        original,
                        imageWidth,
                        imageHeight,
                        bandTop,
                        bandHeight,
                        targetGray,
                        smoothingMode,
                        smoothingWindow,
                        generation,
                        maskGeneration);
                });
                stopwatch.Stop();

                bool stillCurrent = !IsDisposed &&
                    generation == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldCalibrationGeneration,
                        0,
                        0) &&
                    maskGeneration == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldEvaluationGeneration,
                        0,
                        0) &&
                    capturedImageGeneration == imageSourceGeneration &&
                    string.Equals(activeObjectDetectionParameterId, parameterId, StringComparison.Ordinal) &&
                    !resultLabel.IsDisposed;
                if (!stillCurrent)
                {
                    return;
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
                int validColumns = result.ValidColumnCount;
                resultLabel.Text = "校正預覽完成：實測欄 " +
                    validColumns.ToString("N0", CultureInfo.CurrentCulture) + " / " +
                    imageWidth.ToString("N0", CultureInfo.CurrentCulture) +
                    "；補值欄 " + (imageWidth - validColumns).ToString("N0", CultureInfo.CurrentCulture) +
                    "。校正目標 " + targetGray.ToString(CultureInfo.CurrentCulture) +
                    "，運算 " + stopwatch.ElapsedMilliseconds.ToString(CultureInfo.CurrentCulture) + " ms。";
                statusLabel.Text = displayName + " 平場校正完成：" +
                    stopwatch.ElapsedMilliseconds.ToString(CultureInfo.CurrentCulture) + " ms；有效欄 " +
                    validColumns.ToString("N0", CultureInfo.CurrentCulture) +
                    "，補值欄 " + (imageWidth - validColumns).ToString("N0", CultureInfo.CurrentCulture);
                objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
            }
            catch (OperationCanceledException)
            {
                if (!IsDisposed && !resultLabel.IsDisposed &&
                    generation == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldCalibrationGeneration,
                        0,
                        0))
                {
                    resultLabel.Text = "校正已取消，設定或來源 MASK 已變更。";
                    if (objectDetectionFlatFieldCorrectionTimingLabel != null &&
                        !objectDetectionFlatFieldCorrectionTimingLabel.IsDisposed)
                    {
                        objectDetectionFlatFieldCorrectionTimingLabel.Text = "補正時間：未完成";
                    }
                }
            }
            catch (Exception exception)
            {
                if (!IsDisposed && !resultLabel.IsDisposed &&
                    generation == Interlocked.CompareExchange(
                        ref objectDetectionFlatFieldCalibrationGeneration,
                        0,
                        0))
                {
                    resultLabel.Text = "平場校正失敗：" + exception.Message;
                    statusLabel.Text = "平場校正失敗：" + exception.Message;
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
                if (!IsDisposed && runButton != null && !runButton.IsDisposed)
                {
                    runButton.Enabled = true;
                }
            }
        }

        private void EnsureObjectDetectionFlatFieldPreviewDisplay()
        {
            if (objectDetectionFlatFieldPreviewDisplayControl != null ||
                leftImageTabControl == null)
            {
                return;
            }

            objectDetectionFlatFieldPreviewTabPage = CreateImageTabPage(
                "leftObjectDetectionFlatFieldPreviewTabPage",
                "平場校正",
                out objectDetectionFlatFieldPreviewDisplayHostPanel);
            objectDetectionFlatFieldPreviewDisplayControl = CreateImageDisplayControl(
                objectDetectionFlatFieldPreviewDisplayHostPanel,
                "左側 平場校正");
            objectDetectionFlatFieldPreviewDisplayControl.ImageMouseDown +=
                ObjectDetectionFlatFieldPreviewDisplayControl_ImageMouseDown;
            objectDetectionFlatFieldPreviewDisplayControl.ImageOverlayPaint +=
                ObjectDetectionFlatFieldPreviewDisplayControl_ImageOverlayPaint;
            objectDetectionFlatFieldPreviewDisplayControl.LargeImageOverlayPaint +=
                ObjectDetectionFlatFieldPreviewDisplayControl_LargeImageOverlayPaint;
        }


        private void BuildObjectDetectionFlatFieldTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[3];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionFlatFieldEvaluationGeneration++;
                ClearObjectDetectionFlatFieldMaskOverlays();
                ClearObjectDetectionFlatFieldCorrectionPreview();
                objectDetectionFlatFieldResultLabel = null;
                objectDetectionFlatFieldSamplingStatusLabel = null;
                objectDetectionFlatFieldCalibrationStatusLabel = null;
                objectDetectionFlatFieldCorrectionTimingLabel = null;
                objectDetectionFlatFieldProfilePanel = null;
                objectDetectionFlatFieldShowMaskCheckBox = null;

                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };

                var maskGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    Height = 176,
                    Text = "平場校正來源 MASK",
                    Padding = new Padding(8)
                };
                var maskLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                maskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
                maskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
                maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29f));
                maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29f));
                maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                maskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));

                var primaryMask = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    primaryMask.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    primaryMask,
                    parameter.FlatFieldMaskPrimaryType,
                    parameter.FlatFieldMaskPrimaryId,
                    parameter.FlatFieldMaskPrimaryNamespace);

                var maskOperation = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                maskOperation.Items.Add(new ObjectDefinitionOption("直接使用主要 MASK", "None"));
                maskOperation.Items.Add(new ObjectDefinitionOption("OR（A 加 B）", "Or"));
                maskOperation.Items.Add(new ObjectDefinitionOption("AND（A 與 B 交集）", "And"));
                maskOperation.Items.Add(new ObjectDefinitionOption("排除（A AND NOT B）", "Subtract"));
                maskOperation.Items.Add(new ObjectDefinitionOption("XOR（A 與 B 不同處）", "Xor"));
                maskOperation.Items.Add(new ObjectDefinitionOption("NOT（反相主要 MASK）", "Not"));
                SelectObjectDefinitionOption(maskOperation, parameter.FlatFieldMaskOperation, "None");

                var secondaryMask = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    secondaryMask.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(
                    secondaryMask,
                    parameter.FlatFieldMaskSecondaryType,
                    parameter.FlatFieldMaskSecondaryId,
                    parameter.FlatFieldMaskSecondaryNamespace);

                var maskHint = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(75, 83, 95),
                    Text = "套用後會將 MASK 疊加到所有已找到的 ROI 物件。"
                };
                var applyMask = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = "套用來源 MASK"
                };
                applyMask.Click += delegate
                {
                    ObjectDetectionMaskSourceChoice primary =
                        primaryMask.SelectedItem as ObjectDetectionMaskSourceChoice;
                    ObjectDefinitionOption operation =
                        maskOperation.SelectedItem as ObjectDefinitionOption;
                    ObjectDetectionMaskSourceChoice secondary =
                        secondaryMask.SelectedItem as ObjectDetectionMaskSourceChoice;
                    if (primary == null || string.IsNullOrWhiteSpace(primary.SourceType) ||
                        string.IsNullOrWhiteSpace(primary.Id))
                    {
                        statusLabel.Text = "請先指定平場校正的主要 MASK";
                        return;
                    }

                    string operationValue = operation == null
                        ? "None"
                        : Convert.ToString(operation.Value, CultureInfo.InvariantCulture);
                    bool requiresSecondary = !string.Equals(operationValue, "None", StringComparison.Ordinal) &&
                        !string.Equals(operationValue, "Not", StringComparison.Ordinal);
                    if (requiresSecondary &&
                        (secondary == null || string.IsNullOrWhiteSpace(secondary.SourceType) ||
                         string.IsNullOrWhiteSpace(secondary.Id)))
                    {
                        statusLabel.Text = "請先指定平場校正的次要 MASK";
                        return;
                    }

                    parameter.FlatFieldMaskMode = string.Equals(
                        operationValue,
                        "None",
                        StringComparison.Ordinal) ? "Direct" : "Composite";
                    parameter.FlatFieldMaskPrimaryType = primary.SourceType;
                    parameter.FlatFieldMaskPrimaryId = primary.Id;
                    parameter.FlatFieldMaskPrimaryNamespace = primary.SourceNamespace;
                    parameter.FlatFieldMaskOperation = operationValue;
                    parameter.FlatFieldMaskSecondaryType = requiresSecondary && secondary != null
                        ? secondary.SourceType
                        : string.Empty;
                    parameter.FlatFieldMaskSecondaryId = requiresSecondary && secondary != null
                        ? secondary.Id
                        : string.Empty;
                    parameter.FlatFieldMaskSecondaryNamespace = requiresSecondary && secondary != null
                        ? secondary.SourceNamespace
                        : string.Empty;
                    parameter.FlatFieldMaskDisplayName = primary.DisplayText;
                    if (!string.Equals(operationValue, "None", StringComparison.Ordinal))
                    {
                        parameter.FlatFieldMaskDisplayName += " / " + operation.DisplayText;
                    }
                    if (requiresSecondary && secondary != null)
                    {
                        parameter.FlatFieldMaskDisplayName += " + " + secondary.DisplayText;
                    }

                    SaveSystemParameters();
                    objectDetectionFlatFieldResultLabel.Text = "正在將來源 MASK 套用到所有已找到的 ROI...";
                    ApplyObjectDetectionFlatFieldMask(parameter, objectDetectionFlatFieldResultLabel, applyMask);
                };

                maskLayout.Controls.Add(primaryMask, 0, 0);
                maskLayout.Controls.Add(maskOperation, 1, 0);
                maskLayout.Controls.Add(secondaryMask, 0, 1);
                maskLayout.SetColumnSpan(secondaryMask, 2);
                maskLayout.Controls.Add(maskHint, 0, 2);
                maskLayout.SetColumnSpan(maskHint, 2);
                maskLayout.Controls.Add(applyMask, 0, 3);
                maskLayout.SetColumnSpan(applyMask, 2);
                maskGroup.Controls.Add(maskLayout);

                objectDetectionFlatFieldResultLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 54,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(45, 53, 65),
                    Text = string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId)
                        ? "尚未套用來源 MASK。"
                        : "已保存 MASK：" + parameter.FlatFieldMaskDisplayName +
                            "\r\n請按「套用來源 MASK」更新所有 ROI 預覽。"
                };
                contentPanel.Controls.Add(objectDetectionFlatFieldResultLabel);

                var calibrationGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    Height = 270,
                    Text = "取樣與平場校正",
                    Padding = new Padding(8)
                };
                var calibrationLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 4,
                    RowCount = 6,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                calibrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 82f));
                calibrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                calibrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Absolute, 74f));
                calibrationLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 31f));
                calibrationLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));

                var samplingInstruction = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(75, 83, 95),
                    Text = "在左側「平場校正」影像按住 Ctrl 點選取樣 Y 位置；再點一次會取代原位置。"
                };
                calibrationLayout.Controls.Add(samplingInstruction, 0, 0);
                calibrationLayout.SetColumnSpan(samplingInstruction, 4);

                var samplingHeightBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 50,
                    Maximum = 1000,
                    Increment = 50,
                    Value = Math.Max(50, Math.Min(1000,
                        (int)Math.Round(parameter.FlatFieldSamplingHeight / 50.0) * 50))
                };
                calibrationLayout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "校正高度 (px)"
                }, 0, 1);
                calibrationLayout.Controls.Add(samplingHeightBox, 1, 1);

                var smoothingModeBox = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                smoothingModeBox.Items.Add(new ObjectDefinitionOption("移動平均", "MovingAverage"));
                smoothingModeBox.Items.Add(new ObjectDefinitionOption("Gaussian 平滑", "Gaussian"));
                SelectObjectDefinitionOption(
                    smoothingModeBox,
                    parameter.FlatFieldSmoothingMode,
                    "MovingAverage");
                var smoothingWindowBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 1,
                    Maximum = 501,
                    Increment = 2,
                    Value = Math.Max(1, Math.Min(501,
                        parameter.FlatFieldSmoothingWindow % 2 == 0
                            ? parameter.FlatFieldSmoothingWindow + 1
                            : parameter.FlatFieldSmoothingWindow))
                };
                calibrationLayout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "波形平滑"
                }, 0, 2);
                calibrationLayout.Controls.Add(smoothingModeBox, 1, 2);
                calibrationLayout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "視窗"
                }, 2, 2);
                calibrationLayout.Controls.Add(smoothingWindowBox, 3, 2);

                var targetGrayBox = new NumericUpDown
                {
                    Dock = DockStyle.Fill,
                    Minimum = 1,
                    Maximum = 255,
                    Value = Math.Max(1, Math.Min(255, parameter.FlatFieldTargetGray))
                };
                calibrationLayout.Controls.Add(new Label
                {
                    Dock = DockStyle.Fill,
                    TextAlign = ContentAlignment.MiddleLeft,
                    Text = "目標灰階"
                }, 0, 3);
                calibrationLayout.Controls.Add(targetGrayBox, 1, 3);

                var calibrateButton = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = "計算校正並預覽"
                };
                calibrationLayout.Controls.Add(calibrateButton, 2, 3);
                calibrationLayout.SetColumnSpan(calibrateButton, 2);

                objectDetectionFlatFieldSamplingStatusLabel = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                calibrationLayout.Controls.Add(objectDetectionFlatFieldSamplingStatusLabel, 0, 4);
                calibrationLayout.SetColumnSpan(objectDetectionFlatFieldSamplingStatusLabel, 4);

                objectDetectionFlatFieldProfilePanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    BorderStyle = BorderStyle.FixedSingle,
                    BackColor = Color.White
                };
                objectDetectionFlatFieldProfilePanel.Paint +=
                    ObjectDetectionFlatFieldProfilePanel_Paint;
                calibrationLayout.Controls.Add(objectDetectionFlatFieldProfilePanel, 0, 5);
                calibrationLayout.SetColumnSpan(objectDetectionFlatFieldProfilePanel, 4);
                calibrationGroup.Controls.Add(calibrationLayout);

                objectDetectionFlatFieldCalibrationStatusLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(45, 53, 65),
                    Text = string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData)
                        ? "尚未計算平場校正。"
                        : string.Equals(parameter.FlatFieldSavedSettingsSignature,
                            CreateObjectDetectionFlatFieldSettingsSignature(parameter),
                            StringComparison.Ordinal)
                            ? "已載入保存的校正值；來源影像與 MASK 就緒後自動套用。"
                            : "平場設定已變更，請重新計算並保存校正結果。"
                };

                var useMaskGroup = new GroupBox
                {
                    Dock = DockStyle.Top,
                    Height = 176,
                    Text = "校正值使用位置 MASK",
                    Padding = new Padding(8)
                };
                var useMaskLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 2,
                    RowCount = 4,
                    Margin = new Padding(0),
                    Padding = new Padding(0)
                };
                useMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 56f));
                useMaskLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 44f));
                useMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29f));
                useMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 29f));
                useMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 28f));
                useMaskLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 32f));
                var usePrimaryMask = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    if (usePrimaryMask.Items.Count == 0)
                    {
                        choice.DisplayText = "沿用平場校正來源 MASK";
                    }
                    usePrimaryMask.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(usePrimaryMask,
                    parameter.FlatFieldUseMaskPrimaryType,
                    parameter.FlatFieldUseMaskPrimaryId,
                    parameter.FlatFieldUseMaskPrimaryNamespace);
                var useMaskOperation = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                useMaskOperation.Items.Add(new ObjectDefinitionOption("直接使用主要 MASK", "None"));
                useMaskOperation.Items.Add(new ObjectDefinitionOption("OR（A 加 B）", "Or"));
                useMaskOperation.Items.Add(new ObjectDefinitionOption("AND（A 與 B 交集）", "And"));
                useMaskOperation.Items.Add(new ObjectDefinitionOption("排除（A AND NOT B）", "Subtract"));
                useMaskOperation.Items.Add(new ObjectDefinitionOption("XOR（A 與 B 不同處）", "Xor"));
                useMaskOperation.Items.Add(new ObjectDefinitionOption("NOT（反相主要 MASK）", "Not"));
                SelectObjectDefinitionOption(useMaskOperation,
                    parameter.FlatFieldUseMaskOperation, "None");
                var useSecondaryMask = new ComboBox
                {
                    Dock = DockStyle.Fill,
                    DropDownStyle = ComboBoxStyle.DropDownList
                };
                foreach (ObjectDetectionMaskSourceChoice choice in
                    CreateObjectDetectionMaskSourceChoices(parameter))
                {
                    useSecondaryMask.Items.Add(choice);
                }
                SelectObjectDetectionMaskSource(useSecondaryMask,
                    parameter.FlatFieldUseMaskSecondaryType,
                    parameter.FlatFieldUseMaskSecondaryId,
                    parameter.FlatFieldUseMaskSecondaryNamespace);
                var useMaskHint = new Label
                {
                    Dock = DockStyle.Fill,
                    AutoEllipsis = true,
                    TextAlign = ContentAlignment.MiddleLeft,
                    ForeColor = Color.FromArgb(75, 83, 95),
                    Text = string.IsNullOrWhiteSpace(parameter.FlatFieldUseMaskPrimaryId)
                        ? "未指定時沿用來源 MASK；不改變已產生的校正值。"
                        : "使用位置：" + parameter.FlatFieldUseMaskDisplayName
                };
                var applyUseMask = new Button
                {
                    Dock = DockStyle.Fill,
                    Text = "套用使用位置 MASK"
                };
                applyUseMask.Click += delegate
                {
                    ObjectDetectionMaskSourceChoice primary =
                        usePrimaryMask.SelectedItem as ObjectDetectionMaskSourceChoice;
                    ObjectDefinitionOption operation =
                        useMaskOperation.SelectedItem as ObjectDefinitionOption;
                    ObjectDetectionMaskSourceChoice secondary =
                        useSecondaryMask.SelectedItem as ObjectDetectionMaskSourceChoice;
                    bool inheritSource = primary == null || string.IsNullOrWhiteSpace(primary.Id);
                    string operationValue = inheritSource || operation == null
                        ? "None"
                        : Convert.ToString(operation.Value, CultureInfo.InvariantCulture);
                    bool requiresSecondary = !string.Equals(operationValue, "None", StringComparison.Ordinal) &&
                        !string.Equals(operationValue, "Not", StringComparison.Ordinal);
                    if (requiresSecondary &&
                        (secondary == null || string.IsNullOrWhiteSpace(secondary.Id)))
                    {
                        statusLabel.Text = "請先指定校正值使用位置的次要 MASK";
                        return;
                    }

                    parameter.FlatFieldUseMaskMode = requiresSecondary ||
                        string.Equals(operationValue, "Not", StringComparison.Ordinal)
                        ? "Composite" : "Direct";
                    parameter.FlatFieldUseMaskPrimaryType = inheritSource ? string.Empty : primary.SourceType;
                    parameter.FlatFieldUseMaskPrimaryId = inheritSource ? string.Empty : primary.Id;
                    parameter.FlatFieldUseMaskPrimaryNamespace = inheritSource ? string.Empty : primary.SourceNamespace;
                    parameter.FlatFieldUseMaskOperation = operationValue;
                    parameter.FlatFieldUseMaskSecondaryType = requiresSecondary ? secondary.SourceType : string.Empty;
                    parameter.FlatFieldUseMaskSecondaryId = requiresSecondary ? secondary.Id : string.Empty;
                    parameter.FlatFieldUseMaskSecondaryNamespace = requiresSecondary
                        ? secondary.SourceNamespace : string.Empty;
                    parameter.FlatFieldUseMaskDisplayName = inheritSource
                        ? "沿用平場校正來源 MASK" : primary.DisplayText;
                    if (!inheritSource && !string.Equals(operationValue, "None", StringComparison.Ordinal))
                    {
                        parameter.FlatFieldUseMaskDisplayName += " / " + operation.DisplayText;
                    }
                    if (requiresSecondary)
                    {
                        parameter.FlatFieldUseMaskDisplayName += " + " + secondary.DisplayText;
                    }
                    SaveSystemParameters();
                    useMaskHint.Text = "使用位置：" + parameter.FlatFieldUseMaskDisplayName;
                    if (!string.IsNullOrWhiteSpace(parameter.FlatFieldMaskPrimaryId))
                    {
                        ApplyObjectDetectionFlatFieldMask(parameter,
                            objectDetectionFlatFieldResultLabel, applyUseMask, true);
                    }
                };
                useMaskLayout.Controls.Add(usePrimaryMask, 0, 0);
                useMaskLayout.Controls.Add(useMaskOperation, 1, 0);
                useMaskLayout.Controls.Add(useSecondaryMask, 0, 1);
                useMaskLayout.SetColumnSpan(useSecondaryMask, 2);
                useMaskLayout.Controls.Add(useMaskHint, 0, 2);
                useMaskLayout.SetColumnSpan(useMaskHint, 2);
                useMaskLayout.Controls.Add(applyUseMask, 0, 3);
                useMaskLayout.SetColumnSpan(applyUseMask, 2);
                useMaskGroup.Controls.Add(useMaskLayout);

                var saveCalibrationButton = new Button
                {
                    Dock = DockStyle.Top,
                    Height = 32,
                    Text = "保存校正結果"
                };
                var showCalibrationButton = new Button
                {
                    Dock = DockStyle.Top,
                    Height = 32,
                    Text = "顯示校正結果",
                    Enabled = !string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData)
                };
                objectDetectionFlatFieldShowMaskCheckBox = new CheckBox
                {
                    Dock = DockStyle.Top,
                    Height = 28,
                    Padding = new Padding(8, 0, 0, 0),
                    Text = string.IsNullOrWhiteSpace(parameter.FlatFieldUseMaskPrimaryId)
                        ? "顯示來源 MASK" : "顯示使用位置 MASK",
                    Checked = objectDetectionFlatFieldShowMaskOverlay
                };
                objectDetectionFlatFieldShowMaskCheckBox.CheckedChanged += delegate
                {
                    objectDetectionFlatFieldShowMaskOverlay =
                        objectDetectionFlatFieldShowMaskCheckBox.Checked;
                    if (objectDetectionFlatFieldPreviewDisplayControl != null &&
                        !objectDetectionFlatFieldPreviewDisplayControl.IsDisposed)
                    {
                        objectDetectionFlatFieldPreviewDisplayControl.InvalidateImageView();
                    }
                };
                objectDetectionFlatFieldCorrectionTimingLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 58,
                    AutoEllipsis = false,
                    Padding = new Padding(2, 2, 0, 0),
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(45, 53, 65),
                    Text = "補正時間：待套用"
                };
                saveCalibrationButton.Click += delegate
                {
                    SaveObjectDetectionFlatFieldCalibration(
                        parameter,
                        objectDetectionFlatFieldCalibrationStatusLabel,
                        showCalibrationButton);
                };
                showCalibrationButton.Click += async delegate
                {
                    await ShowSavedObjectDetectionFlatFieldCalibration(
                        parameter,
                        objectDetectionFlatFieldCalibrationStatusLabel,
                        showCalibrationButton);
                };

                UpdateObjectDetectionFlatFieldSamplingStatus(parameter);
                samplingHeightBox.ValueChanged += delegate
                {
                    int value = Decimal.ToInt32(samplingHeightBox.Value);
                    int snappedValue = Math.Max(50, Math.Min(1000,
                        (int)Math.Round(value / 50.0) * 50));
                    if (snappedValue != value)
                    {
                        samplingHeightBox.Value = snappedValue;
                        return;
                    }
                    parameter.FlatFieldSamplingHeight = snappedValue;
                    SaveSystemParameters();
                    InvalidateObjectDetectionFlatFieldCalibration(parameter, true);
                };
                smoothingModeBox.SelectedIndexChanged += delegate
                {
                    ObjectDefinitionOption selected =
                        smoothingModeBox.SelectedItem as ObjectDefinitionOption;
                    if (selected == null)
                    {
                        return;
                    }
                    parameter.FlatFieldSmoothingMode = Convert.ToString(
                        selected.Value,
                        CultureInfo.InvariantCulture);
                    SaveSystemParameters();
                    InvalidateObjectDetectionFlatFieldCalibration(parameter, true);
                };
                smoothingWindowBox.ValueChanged += delegate
                {
                    int value = Decimal.ToInt32(smoothingWindowBox.Value);
                    if (value % 2 == 0)
                    {
                        smoothingWindowBox.Value = Math.Min(
                            smoothingWindowBox.Maximum,
                            value + 1);
                        return;
                    }
                    parameter.FlatFieldSmoothingWindow = value;
                    SaveSystemParameters();
                    InvalidateObjectDetectionFlatFieldCalibration(parameter, true);
                };
                targetGrayBox.ValueChanged += delegate
                {
                    parameter.FlatFieldTargetGray = Decimal.ToInt32(targetGrayBox.Value);
                    SaveSystemParameters();
                    InvalidateObjectDetectionFlatFieldCalibration(parameter, true);
                };
                calibrateButton.Click += delegate
                {
                    RunObjectDetectionFlatFieldCalibration(
                        parameter,
                        objectDetectionFlatFieldCalibrationStatusLabel,
                        objectDetectionFlatFieldSamplingStatusLabel,
                        objectDetectionFlatFieldProfilePanel,
                        calibrateButton);
                };

                contentPanel.Controls.Add(objectDetectionFlatFieldCorrectionTimingLabel);
                contentPanel.Controls.Add(objectDetectionFlatFieldShowMaskCheckBox);
                contentPanel.Controls.Add(showCalibrationButton);
                contentPanel.Controls.Add(new Panel { Dock = DockStyle.Top, Height = 8 });
                contentPanel.Controls.Add(saveCalibrationButton);
                contentPanel.Controls.Add(useMaskGroup);
                contentPanel.Controls.Add(objectDetectionFlatFieldCalibrationStatusLabel);
                contentPanel.Controls.Add(calibrationGroup);
                contentPanel.Controls.Add(maskGroup);
                contentPanel.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    AutoEllipsis = true,
                    Text = "平場校正來源：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                });
                contentPanel.Controls.Add(new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 42,
                    Text = "此設定套用至物件定義找到的全部 ROI，不依賴 1–6 序號。\r\n" +
                        "校正影像限定在已找到的 ROI 與所選 MASK 內。",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                });
                tabPage.Controls.Add(contentPanel);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }


    }
}
