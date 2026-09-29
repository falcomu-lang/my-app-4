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
        private void LoadSavedObjectDetectionFlatFieldProfiles()
        {
            savedFlatFieldProfiles.Clear();
            if (systemParameters == null || systemParameters.ObjectDetectionParameters == null)
            {
                return;
            }

            foreach (ObjectDetectionParameterSettings parameter in systemParameters.ObjectDetectionParameters)
            {
                try
                {
                    GetSavedObjectDetectionFlatFieldProfile(parameter);
                }
                catch (Exception exception)
                {
                    Debug.WriteLine("Saved flat-field profile was not loaded: " + exception);
                }
            }
        }

        private FlatFieldCalibrationResult GetSavedObjectDetectionFlatFieldProfile(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.Id) ||
                string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) ||
                !string.Equals(parameter.FlatFieldSavedSettingsSignature,
                    CreateObjectDetectionFlatFieldSettingsSignature(parameter), StringComparison.Ordinal))
            {
                return null;
            }

            SavedFlatFieldProfile cached;
            if (savedFlatFieldProfiles.TryGetValue(parameter.Id, out cached) &&
                cached.Width == parameter.FlatFieldSavedImageWidth &&
                string.Equals(cached.Signature, parameter.FlatFieldSavedSettingsSignature,
                    StringComparison.Ordinal) &&
                string.Equals(cached.Data, parameter.FlatFieldSavedProfileData,
                    StringComparison.Ordinal))
            {
                return cached.Profile;
            }

            FlatFieldCalibrationResult decoded = DecodeObjectDetectionFlatFieldProfile(
                parameter.FlatFieldSavedProfileData, parameter.FlatFieldSavedImageWidth);
            savedFlatFieldProfiles[parameter.Id] = new SavedFlatFieldProfile
            {
                Data = parameter.FlatFieldSavedProfileData,
                Signature = parameter.FlatFieldSavedSettingsSignature,
                Width = parameter.FlatFieldSavedImageWidth,
                Profile = decoded
            };
            return decoded;
        }

        private FlatFieldCalibrationResult CalculateObjectDetectionFlatField(
            LargeImageSource largeSource,
            Bitmap original,
            int imageWidth,
            int imageHeight,
            int bandTop,
            int bandHeight,
            int targetGray,
            string smoothingMode,
            int smoothingWindow,
            int calibrationGeneration,
            int maskGeneration)
        {
            var result = new FlatFieldCalibrationResult();
            Rectangle sampleBounds = new Rectangle(0, bandTop, imageWidth, bandHeight);
            List<ObjectDetectionFlatFieldMaskOverlay> sampleMasks =
                CloneObjectDetectionFlatFieldMasks(sampleBounds);
            try
            {
                if (sampleMasks.Count == 0)
                {
                    throw new InvalidOperationException("取樣帶內沒有指定 MASK；請移動取樣位置或確認 MASK。");
                }

                using (Bitmap sampleBitmap = largeSource != null
                    ? largeSource.CreateRegionBitmapFromTiles(sampleBounds)
                    : CloneBitmapRegionAs24Bpp(original, sampleBounds))
                using (Cv.Mat sampleGray = CreateOpenCvGrayMat(sampleBitmap))
                using (var sampleMask = new Cv.Mat(
                    bandHeight,
                    imageWidth,
                    Cv.MatType.CV_8UC1,
                    Cv.Scalar.All(0)))
                {
                    foreach (ObjectDetectionFlatFieldMaskOverlay item in sampleMasks)
                    {
                        Rectangle destinationBounds = Rectangle.Intersect(item.Bounds, sampleBounds);
                        if (destinationBounds.Width <= 0 || destinationBounds.Height <= 0)
                        {
                            continue;
                        }
                        using (var sourceMask = new Cv.Mat(
                            item.Mask,
                            new Cv.Rect(
                                destinationBounds.X - item.Bounds.X,
                                destinationBounds.Y - item.Bounds.Y,
                                destinationBounds.Width,
                                destinationBounds.Height)))
                        using (var destinationMask = new Cv.Mat(
                            sampleMask,
                            new Cv.Rect(
                                destinationBounds.X - sampleBounds.X,
                                destinationBounds.Y - sampleBounds.Y,
                                destinationBounds.Width,
                                destinationBounds.Height)))
                        {
                            Cv.Cv2.BitwiseOr(destinationMask, sourceMask, destinationMask);
                        }
                    }

                    ThrowIfObjectDetectionFlatFieldCalibrationChanged(
                        calibrationGeneration,
                        maskGeneration);
                    using (var maskedGray = new Cv.Mat())
                    using (var graySums = new Cv.Mat())
                    using (var maskSums = new Cv.Mat())
                    using (var profile = new Cv.Mat())
                    {
                        Cv.Cv2.BitwiseAnd(sampleGray, sampleMask, maskedGray);
                        Cv.Cv2.Reduce(
                            maskedGray,
                            graySums,
                            0,
                            Cv.ReduceTypes.Sum,
                            Cv.MatType.CV_64FC1.ToInt32());
                        Cv.Cv2.Reduce(
                            sampleMask,
                            maskSums,
                            0,
                            Cv.ReduceTypes.Sum,
                            Cv.MatType.CV_64FC1.ToInt32());
                        Cv.Cv2.Divide(graySums, maskSums, profile, 255.0);

                        result.RawProfile = new double[imageWidth];
                        result.MeasuredColumns = new bool[imageWidth];
                        double[] maskPixelCounts = new double[imageWidth];
                        Marshal.Copy(profile.Data, result.RawProfile, 0, imageWidth);
                        Marshal.Copy(maskSums.Data, maskPixelCounts, 0, imageWidth);
                        for (int x = 0; x < imageWidth; x++)
                        {
                            if (maskPixelCounts[x] <= 0.0 ||
                                double.IsNaN(result.RawProfile[x]) ||
                                double.IsInfinity(result.RawProfile[x]) ||
                                result.RawProfile[x] <= 0.0)
                            {
                                result.RawProfile[x] = 0.0;
                                continue;
                            }
                            result.MeasuredColumns[x] = true;
                            result.ValidColumnCount++;
                        }
                    }
                }
            }
            finally
            {
                DisposeObjectDetectionFlatFieldMasks(sampleMasks);
            }

            if (result.ValidColumnCount == 0)
            {
                result.Dispose();
                throw new InvalidOperationException("MASK 在取樣帶內沒有可用的非零灰階樣本。");
            }

            double[] smoothedMeasurements = SmoothObjectDetectionFlatFieldProfile(
                result.RawProfile,
                result.MeasuredColumns,
                smoothingMode,
                smoothingWindow);
            result.SmoothedProfile = FillObjectDetectionFlatFieldProfile(
                smoothedMeasurements, result.MeasuredColumns);
            result.ValidColumns = Enumerable.Repeat(true, imageWidth).ToArray();

            try
            {
                PrepareObjectDetectionFlatFieldCorrectedPreview(
                    result, largeSource, original, imageWidth, imageHeight,
                    targetGray, calibrationGeneration, maskGeneration);
            }
            catch
            {
                result.Dispose();
                throw;
            }

            return result;
        }

        private static string CreateObjectDetectionFlatFieldSettingsSignature(
            ObjectDetectionParameterSettings parameter)
        {
            return string.Join("|", new[]
            {
                parameter.ObjectDefinitionId ?? string.Empty,
                parameter.FlatFieldMaskMode ?? string.Empty,
                parameter.FlatFieldMaskPrimaryType ?? string.Empty,
                parameter.FlatFieldMaskPrimaryId ?? string.Empty,
                parameter.FlatFieldMaskPrimaryNamespace ?? string.Empty,
                parameter.FlatFieldMaskOperation ?? string.Empty,
                parameter.FlatFieldMaskSecondaryType ?? string.Empty,
                parameter.FlatFieldMaskSecondaryId ?? string.Empty,
                parameter.FlatFieldMaskSecondaryNamespace ?? string.Empty,
                parameter.FlatFieldSamplePositionConfigured ? "1" : "0",
                parameter.FlatFieldSampleYRatio.ToString("R", CultureInfo.InvariantCulture),
                parameter.FlatFieldSamplingHeight.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldTargetGray.ToString(CultureInfo.InvariantCulture),
                parameter.FlatFieldSmoothingMode ?? string.Empty,
                parameter.FlatFieldSmoothingWindow.ToString(CultureInfo.InvariantCulture)
            });
        }

        private static string EncodeObjectDetectionFlatFieldProfile(
            double[] raw,
            double[] smoothed,
            bool[] measured,
            bool[] valid)
        {
            if (raw == null || smoothed == null || measured == null || valid == null ||
                raw.Length == 0 || raw.Length != smoothed.Length ||
                raw.Length != measured.Length || raw.Length != valid.Length)
            {
                throw new InvalidOperationException("平場校正曲線資料不完整。");
            }

            var values = new float[checked(raw.Length * 2)];
            for (int x = 0; x < raw.Length; x++)
            {
                values[x * 2] = measured[x] ? (float)raw[x] : float.NaN;
                values[x * 2 + 1] = valid[x] ? (float)smoothed[x] : float.NaN;
                if ((measured[x] && (!valid[x] || float.IsNaN(values[x * 2]) ||
                    float.IsInfinity(values[x * 2]) || values[x * 2] <= 0f ||
                    values[x * 2] > 255f)) ||
                    (valid[x] && (float.IsNaN(values[x * 2 + 1]) ||
                    float.IsInfinity(values[x * 2 + 1]) ||
                    values[x * 2 + 1] <= 0f || values[x * 2 + 1] > 255f)))
                {
                    throw new InvalidOperationException("平場校正曲線含有無效的灰階值。");
                }
            }

            var bytes = new byte[checked(values.Length * sizeof(float))];
            Buffer.BlockCopy(values, 0, bytes, 0, bytes.Length);
            return Convert.ToBase64String(bytes);
        }

        private static FlatFieldCalibrationResult DecodeObjectDetectionFlatFieldProfile(
            string encoded,
            int imageWidth)
        {
            if (imageWidth <= 0 || imageWidth > 1000000)
            {
                throw new InvalidOperationException("保存的平場影像寬度無效。");
            }

            byte[] bytes = Convert.FromBase64String(encoded ?? string.Empty);
            if (bytes.Length != checked(imageWidth * 2 * sizeof(float)))
            {
                throw new InvalidOperationException("保存的平場校正曲線長度與影像寬度不符。");
            }

            var values = new float[imageWidth * 2];
            Buffer.BlockCopy(bytes, 0, values, 0, bytes.Length);
            var result = new FlatFieldCalibrationResult
            {
                RawProfile = new double[imageWidth],
                SmoothedProfile = new double[imageWidth],
                MeasuredColumns = new bool[imageWidth],
                ValidColumns = new bool[imageWidth]
            };
            for (int x = 0; x < imageWidth; x++)
            {
                float raw = values[x * 2];
                float smoothed = values[x * 2 + 1];
                if (float.IsNaN(raw) && float.IsNaN(smoothed))
                {
                    continue;
                }
                if ((!float.IsNaN(raw) && (float.IsInfinity(raw) || raw <= 0f || raw > 255f)) ||
                    float.IsNaN(smoothed) || float.IsInfinity(smoothed) ||
                    smoothed <= 0f || smoothed > 255f)
                {
                    throw new InvalidOperationException("保存的平場校正曲線含有無效資料。");
                }
                if (!float.IsNaN(raw))
                {
                    result.RawProfile[x] = raw;
                    result.MeasuredColumns[x] = true;
                    result.ValidColumnCount++;
                }
                result.SmoothedProfile[x] = smoothed;
                result.ValidColumns[x] = true;
            }
            if (result.ValidColumnCount == 0)
            {
                throw new InvalidOperationException("保存的平場校正曲線沒有有效取樣欄。");
            }
            return result;
        }

        private void SaveObjectDetectionFlatFieldCalibration(
            ObjectDetectionParameterSettings parameter,
            Label resultLabel,
            Button showButton)
        {
            if (parameter == null || resultLabel == null || resultLabel.IsDisposed)
            {
                return;
            }

            int maskGeneration = Interlocked.CompareExchange(
                ref objectDetectionFlatFieldEvaluationGeneration, 0, 0);
            if (!string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal) ||
                !string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id, StringComparison.Ordinal) ||
                objectDetectionFlatFieldProfileImageGeneration != imageSourceGeneration ||
                objectDetectionFlatFieldProfileMaskGeneration != maskGeneration ||
                objectDetectionFlatFieldSmoothedProfile == null ||
                !objectDetectionFlatFieldPreviewIsCorrected ||
                objectDetectionFlatFieldCorrectedImageGeneration != imageSourceGeneration ||
                rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage)
            {
                resultLabel.Text = "請先對目前影像與來源 MASK 計算平場校正，再保存結果。";
                return;
            }

            int imageWidth = objectDetectionFlatFieldPreviewWidth;
            if (imageWidth <= 0 || objectDetectionFlatFieldSmoothedProfile.Length != imageWidth)
            {
                resultLabel.Text = "校正曲線與目前影像寬度不符，請重新計算。";
                return;
            }

            int previousWidth = parameter.FlatFieldSavedImageWidth;
            int previousTarget = parameter.FlatFieldSavedTargetGray;
            string previousData = parameter.FlatFieldSavedProfileData;
            string previousSignature = parameter.FlatFieldSavedSettingsSignature;
            try
            {
                parameter.FlatFieldSavedImageWidth = imageWidth;
                parameter.FlatFieldSavedTargetGray = Math.Max(1, Math.Min(255, parameter.FlatFieldTargetGray));
                parameter.FlatFieldSavedProfileData = EncodeObjectDetectionFlatFieldProfile(
                    objectDetectionFlatFieldRawProfile,
                    objectDetectionFlatFieldSmoothedProfile,
                    objectDetectionFlatFieldMeasuredColumns,
                    objectDetectionFlatFieldValidColumns);
                parameter.FlatFieldSavedSettingsSignature =
                    CreateObjectDetectionFlatFieldSettingsSignature(parameter);
                SaveSystemParameters();
                if (showButton != null && !showButton.IsDisposed)
                {
                    showButton.Enabled = true;
                }
                resultLabel.Text = "平場校正結果已保存；可按「顯示校正結果」查看。";
                statusLabel.Text = parameter.DisplayName + "：平場校正結果已保存";
            }
            catch (Exception exception)
            {
                parameter.FlatFieldSavedImageWidth = previousWidth;
                parameter.FlatFieldSavedTargetGray = previousTarget;
                parameter.FlatFieldSavedProfileData = previousData;
                parameter.FlatFieldSavedSettingsSignature = previousSignature;
                resultLabel.Text = "保存平場校正結果失敗：" + exception.Message;
            }
        }

        private static double[] SmoothObjectDetectionFlatFieldProfile(
            double[] profile,
            bool[] valid,
            string mode,
            int window)
        {
            var values = new double[profile.Length];
            var weights = new double[profile.Length];
            var smoothed = new double[profile.Length];
            bool gaussian = string.Equals(mode, "Gaussian", StringComparison.Ordinal);
            for (int x = 0; x < profile.Length; x++)
            {
                if (valid[x])
                {
                    values[x] = profile[x];
                    weights[x] = 1.0;
                }
            }

            using (var profileMat = new Cv.Mat(1, profile.Length, Cv.MatType.CV_64FC1))
            using (var weightMat = new Cv.Mat(1, profile.Length, Cv.MatType.CV_64FC1))
            using (var smoothedValues = new Cv.Mat())
            using (var smoothedWeights = new Cv.Mat())
            using (var normalized = new Cv.Mat())
            {
                Marshal.Copy(values, 0, profileMat.Data, values.Length);
                Marshal.Copy(weights, 0, weightMat.Data, weights.Length);
                var kernel = new Cv.Size(Math.Max(1, window | 1), 1);
                if (gaussian)
                {
                    Cv.Cv2.GaussianBlur(profileMat, smoothedValues, kernel, 0.0);
                    Cv.Cv2.GaussianBlur(weightMat, smoothedWeights, kernel, 0.0);
                }
                else
                {
                    Cv.Cv2.Blur(profileMat, smoothedValues, kernel);
                    Cv.Cv2.Blur(weightMat, smoothedWeights, kernel);
                }

                Cv.Cv2.Divide(smoothedValues, smoothedWeights, normalized);
                Marshal.Copy(normalized.Data, smoothed, 0, smoothed.Length);
                for (int x = 0; x < smoothed.Length; x++)
                {
                    if (!valid[x] || weights[x] <= 0.0)
                    {
                        smoothed[x] = 0.0;
                    }
                }
            }

            return smoothed;
        }

        private static double[] FillObjectDetectionFlatFieldProfile(
            double[] raw,
            bool[] measured)
        {
            if (raw == null || measured == null || raw.Length != measured.Length)
            {
                throw new ArgumentException("平場校正取樣資料不完整。");
            }

            int first = -1;
            for (int x = 0; x < measured.Length; x++)
            {
                if (measured[x])
                {
                    first = x;
                    break;
                }
            }
            if (first < 0)
            {
                throw new InvalidOperationException("沒有可用的平場校正取樣值。");
            }

            var filled = new double[raw.Length];
            for (int x = 0; x <= first; x++)
            {
                filled[x] = raw[first];
            }

            int previous = first;
            for (int x = first + 1; x < measured.Length; x++)
            {
                if (!measured[x])
                {
                    continue;
                }

                double step = (raw[x] - raw[previous]) / (x - previous);
                for (int gap = previous + 1; gap < x; gap++)
                {
                    filled[gap] = raw[previous] + step * (gap - previous);
                }
                filled[x] = raw[x];
                previous = x;
            }

            for (int x = previous + 1; x < filled.Length; x++)
            {
                filled[x] = raw[previous];
            }
            return filled;
        }
    }
}
