using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private static readonly string[] ObjectJudgementProcessingMethods =
        {
            "Dilate",
            "Erode",
            "Close",
            "Open",
            "Fill Contour",
            "Fill Hole",
            "Connect gap / bridge",
            "Morphological Reconstruction",
            "Merge by distance",
            "Convex Hull"
        };

        private readonly object objectJudgementMaskLock = new object();
        private readonly Dictionary<string, Cv.Mat> objectJudgementLargeMasks =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
        private readonly HashSet<string> objectJudgementLargeMaskBuildKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<string, Cv.Mat> objectJudgementGroupLargeMasks =
            new Dictionary<string, Cv.Mat>(StringComparer.Ordinal);
        private readonly HashSet<string> objectJudgementGroupLargeMaskBuildKeys =
            new HashSet<string>(StringComparer.Ordinal);
        private int objectJudgementMaskGeneration;
        private string activeObjectJudgementId;
        private string activeObjectJudgementGroupId;
        private bool objectJudgementProcessingRequested;
        private Bitmap latestObjectJudgementImage;
        private Label objectJudgementParameterApplyStatusLabel;
        private bool objectJudgementParameterApplyInProgress;
        private int objectJudgementPendingLargeMaskBuilds;
        private long objectJudgementAccumulatedProcessingMilliseconds;
        private string objectJudgementTimingName;
        private int activeObjectJudgementProcessingIndex = -1;
        private string activeObjectJudgementProcessingSignature;
        private string completedObjectJudgementProcessingSignature;
        private string activeObjectJudgementGroupProcessingSignature;
        private string completedObjectJudgementGroupProcessingSignature;
        private string requestedObjectJudgementDisplaySignature;

        private void BuildObjectJudgementProcessingParameterPanel(
            Panel panel,
            ObjectJudgementProcessingSettings processing,
            int objectIndex,
            int processingIndex)
        {
            panel.Controls.Add(new Label { Text = "處理方式", Left = 8, Top = 12, Width = 250 });
            var methodCombo = new ComboBox
            {
                Left = 8,
                Top = 34,
                Width = Math.Max(250, parameterPanel.Width - 18),
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            methodCombo.Items.AddRange(ObjectJudgementProcessingMethods);
            int methodIndex = Array.IndexOf(
                ObjectJudgementProcessingMethods,
                NormalizeObjectJudgementProcessingMethod(processing.Method));
            methodCombo.SelectedIndex = methodIndex >= 0 ? methodIndex : 0;
            panel.Controls.Add(methodCombo);

            NumericUpDown kernelSizeControl = null;
            NumericUpDown iterationsControl = null;
            NumericUpDown distanceControl = null;
            ComboBox shapeCombo = null;
            var optionsPanel = new Panel
            {
                Left = 0,
                Top = 68,
                Width = Math.Max(260, parameterPanel.Width - 8),
                Height = 140,
                AutoScroll = true
            };
            panel.Controls.Add(optionsPanel);
            var applyButton = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 220,
                Width = Math.Max(250, parameterPanel.Width - 18)
            };
            panel.Controls.Add(applyButton);

            var cancelButton = new Button
            {
                Text = "取消",
                Left = 8,
                Width = Math.Max(250, parameterPanel.Width - 18)
            };
            panel.Controls.Add(cancelButton);

            var applyStatusLabel = new Label
            {
                Left = 8,
                Width = Math.Max(250, parameterPanel.Width - 18),
                Height = 52,
                AutoSize = false,
                ForeColor = Color.FromArgb(55, 64, 76),
                Text = string.Empty
            };
            objectJudgementParameterApplyStatusLabel = applyStatusLabel;
            panel.Controls.Add(applyStatusLabel);

            Action rebuildOptions = delegate
            {
                optionsPanel.Controls.Clear();
                kernelSizeControl = null;
                iterationsControl = null;
                distanceControl = null;
                shapeCombo = null;
                Dictionary<string, string> values = ParseImageProcessingParameters(processing.Parameters);
                string method = methodCombo.SelectedItem as string;
                int top = 4;

                if (IsMorphologyObjectProcessingMethod(method) ||
                    string.Equals(method, "Morphological Reconstruction", StringComparison.OrdinalIgnoreCase))
                {
                    kernelSizeControl = AddObjectJudgementNumericControl(
                        optionsPanel, "核心大小", top, GetIntParameter(values, "KernelSize", 3), 1, 99);
                    top += 42;
                    iterationsControl = AddObjectJudgementNumericControl(
                        optionsPanel, "迭代次數", top, GetIntParameter(values, "Iterations", 1), 1, 100);
                    top += 42;
                    optionsPanel.Controls.Add(new Label { Text = "形狀", Left = 8, Top = top + 4, Width = 80 });
                    shapeCombo = new ComboBox
                    {
                        Left = 88,
                        Top = top,
                        Width = 150,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    shapeCombo.Items.AddRange(new object[] { "Rect", "Ellipse", "Cross" });
                    string shape = GetStringParameter(values, "Shape", "Rect");
                    int shapeIndex = shapeCombo.Items.IndexOf(shape);
                    shapeCombo.SelectedIndex = shapeIndex >= 0 ? shapeIndex : 0;
                    optionsPanel.Controls.Add(shapeCombo);
                    top += 42;
                }
                else if (string.Equals(method, "Connect gap / bridge", StringComparison.OrdinalIgnoreCase))
                {
                    distanceControl = AddObjectJudgementNumericControl(
                        optionsPanel, "間隙大小（像素）", top, GetIntParameter(values, "GapSize", 3), 1, 999);
                    top += 42;
                    optionsPanel.Controls.Add(new Label { Text = "方向", Left = 8, Top = top + 4, Width = 80 });
                    shapeCombo = new ComboBox
                    {
                        Left = 88,
                        Top = top,
                        Width = 150,
                        DropDownStyle = ComboBoxStyle.DropDownList
                    };
                    shapeCombo.Items.AddRange(new object[] { "All", "Horizontal", "Vertical" });
                    string direction = GetStringParameter(values, "Direction", "All");
                    int directionIndex = shapeCombo.Items.IndexOf(direction);
                    shapeCombo.SelectedIndex = directionIndex >= 0 ? directionIndex : 0;
                    optionsPanel.Controls.Add(shapeCombo);
                    top += 42;
                }
                else if (string.Equals(method, "Merge by distance", StringComparison.OrdinalIgnoreCase))
                {
                    distanceControl = AddObjectJudgementNumericControl(
                        optionsPanel, "距離（像素）", top, GetIntParameter(values, "Distance", 3), 1, 999);
                    top += 42;
                }

                optionsPanel.Height = Math.Max(48, top + 4);
                applyButton.Top = optionsPanel.Top + optionsPanel.Height + 12;
                cancelButton.Top = applyButton.Bottom + 8;
                applyStatusLabel.Top = cancelButton.Bottom + 8;
                panel.AutoScrollMinSize = new Size(0, applyStatusLabel.Bottom + 8);
            };

            applyButton.Click += delegate
            {
                string method = methodCombo.SelectedItem as string;
                if (string.IsNullOrWhiteSpace(method))
                {
                    return;
                }

                var values = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
                if (IsMorphologyObjectProcessingMethod(method) ||
                    string.Equals(method, "Morphological Reconstruction", StringComparison.OrdinalIgnoreCase))
                {
                    values["KernelSize"] = NormalizeObjectJudgementKernelSize(
                        kernelSizeControl == null ? 3 : Decimal.ToInt32(kernelSizeControl.Value))
                        .ToString(CultureInfo.InvariantCulture);
                    values["Iterations"] = Math.Max(
                        1, iterationsControl == null ? 1 : Decimal.ToInt32(iterationsControl.Value))
                        .ToString(CultureInfo.InvariantCulture);
                    values["Shape"] = shapeCombo == null || shapeCombo.SelectedItem == null
                        ? "Rect"
                        : shapeCombo.SelectedItem.ToString();
                }
                else if (string.Equals(method, "Connect gap / bridge", StringComparison.OrdinalIgnoreCase))
                {
                    values["GapSize"] = Math.Max(
                        1, distanceControl == null ? 3 : Decimal.ToInt32(distanceControl.Value))
                        .ToString(CultureInfo.InvariantCulture);
                    values["Direction"] = shapeCombo == null || shapeCombo.SelectedItem == null
                        ? "All"
                        : shapeCombo.SelectedItem.ToString();
                }
                else if (string.Equals(method, "Merge by distance", StringComparison.OrdinalIgnoreCase))
                {
                    values["Distance"] = Math.Max(
                        1, distanceControl == null ? 3 : Decimal.ToInt32(distanceControl.Value))
                        .ToString(CultureInfo.InvariantCulture);
                }

                // Store the canonical method name so settings created by older
                // versions cannot break a later step in the same chain.
                processing.Method = NormalizeObjectJudgementProcessingMethod(method);
                processing.Parameters = FormatImageProcessingParameters(values);
                SaveSystemParameters();
                InvalidateObjectJudgementProcessingResults();
                // Applying step N evaluates step 1 through N. Do not include
                // later, unfinished steps when the user is editing this step.
                StartObjectJudgementProcessing(objectIndex, processingIndex);
                if (!objectJudgementProcessingRequested)
                {
                    FailObjectJudgementParameterApply("區塊尚未設定完整的來源或處理方式");
                }
            };

            cancelButton.Click += delegate
            {
                objectJudgementParameterApplyStatusLabel.Text = string.Empty;
                string appliedMethod = NormalizeObjectJudgementProcessingMethod(processing.Method);
                int appliedMethodIndex = Array.IndexOf(ObjectJudgementProcessingMethods, appliedMethod);
                methodCombo.SelectedIndex = appliedMethodIndex >= 0 ? appliedMethodIndex : 0;
                rebuildOptions();
                statusLabel.Text = "已取消修改" + GetObjectJudgementProcessingDisplayName(processing, processingIndex);
            };

            methodCombo.SelectedIndexChanged += delegate { rebuildOptions(); };
            rebuildOptions();
        }

        private static NumericUpDown AddObjectJudgementNumericControl(
            Control parent, string labelText, int top, int value, int minimum, int maximum)
        {
            parent.Controls.Add(new Label { Text = labelText, Left = 8, Top = top + 4, Width = 80 });
            var control = new NumericUpDown
            {
                Left = 88,
                Top = top,
                Width = 150,
                Minimum = minimum,
                Maximum = maximum,
                Value = Math.Max(minimum, Math.Min(maximum, value))
            };
            parent.Controls.Add(control);
            return control;
        }

        private static bool IsMorphologyObjectProcessingMethod(string method)
        {
            return string.Equals(method, "Dilate", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Erode", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Close", StringComparison.OrdinalIgnoreCase) ||
                string.Equals(method, "Open", StringComparison.OrdinalIgnoreCase);
        }

        private static string NormalizeObjectJudgementProcessingMethod(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                return string.Empty;
            }

            return ObjectJudgementProcessingMethods.FirstOrDefault(
                item => string.Equals(item, method.Trim(), StringComparison.OrdinalIgnoreCase)) ?? method.Trim();
        }

        private static bool IsSupportedObjectJudgementProcessingMethod(string method)
        {
            string normalizedMethod = NormalizeObjectJudgementProcessingMethod(method);
            return ObjectJudgementProcessingMethods.Any(
                item => string.Equals(item, normalizedMethod, StringComparison.Ordinal));
        }

        private static int NormalizeObjectJudgementKernelSize(int value)
        {
            int normalized = Math.Max(1, Math.Min(99, value));
            return normalized % 2 == 0 ? normalized + 1 : normalized;
        }

        private void BeginObjectJudgementParameterApplyStatus(int objectIndex)
        {
            ResetPipelineTiming();
            objectJudgementParameterApplyInProgress = true;
            objectJudgementAccumulatedProcessingMilliseconds = 0;
            objectJudgementPendingLargeMaskBuilds = 0;
            objectJudgementTimingName = objectIndex >= 0 &&
                objectIndex < systemParameters.ObjectJudgements.Count
                ? GetObjectJudgementDisplayName(
                    systemParameters.ObjectJudgements[objectIndex], objectIndex)
                : "區塊";
            SetObjectJudgementParameterApplyStatus("影像處理中...");
        }

        private void BeginObjectJudgementGroupApplyStatus(string groupId)
        {
            ResetPipelineTiming();
            objectJudgementParameterApplyInProgress = true;
            objectJudgementAccumulatedProcessingMilliseconds = 0;
            objectJudgementPendingLargeMaskBuilds = 0;
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            objectJudgementTimingName = group == null || string.IsNullOrWhiteSpace(group.DisplayName)
                ? "群組"
                : group.DisplayName.Trim();
            SetObjectJudgementParameterApplyStatus("影像處理中...");
        }

        private void SetObjectJudgementParameterApplyStatus(string text)
        {
            if (objectJudgementParameterApplyStatusLabel != null &&
                !objectJudgementParameterApplyStatusLabel.IsDisposed)
            {
                objectJudgementParameterApplyStatusLabel.Text = text ?? string.Empty;
            }
        }

        private void FailObjectJudgementParameterApply(string message)
        {
            objectJudgementParameterApplyInProgress = false;
            SetObjectJudgementParameterApplyStatus("處理失敗：" + message);
        }

        private void CompleteObjectJudgementParameterApplyStatus(
            long processingElapsedMilliseconds,
            long previewElapsedMilliseconds)
        {
            if (!objectJudgementParameterApplyInProgress)
            {
                return;
            }

            objectJudgementParameterApplyInProgress = false;
            long processing = Math.Max(0, processingElapsedMilliseconds);
            long preview = Math.Max(0, previewElapsedMilliseconds);
            lastObjectJudgementElapsedMilliseconds = processing;
            lastDisplayProcessingElapsedMilliseconds = preview;
            string objectName = string.IsNullOrWhiteSpace(objectJudgementTimingName)
                ? "區塊"
                : objectJudgementTimingName;
            SetObjectJudgementParameterApplyStatus(string.Format(
                CultureInfo.InvariantCulture,
                "完成：{0}\r\n{1}處理時間：{2} ms || 顯示處理時間：{3} ms",
                BuildPipelineTimingText(true, true),
                objectName,
                processing,
                preview));
            statusLabel.Text = objectName + "：" + BuildPipelineTimingText(true, true);
        }

        private void SetActiveObjectJudgement(ObjectJudgementSettings objectJudgement)
        {
            string id = objectJudgement == null ? null : objectJudgement.Id;
            if (string.Equals(activeObjectJudgementId, id, StringComparison.Ordinal))
            {
                activeObjectJudgementGroupId = null;
                return;
            }

            activeObjectJudgementId = id;
            activeObjectJudgementGroupId = null;
            InvalidateObjectJudgementProcessingResults();
            RestoreObjectJudgementDisplayToOriginal();
        }

        private void InvalidateObjectJudgementProcessingResults()
        {
            InvalidateObjectDefinitionResults();
            objectJudgementParameterApplyInProgress = false;
            activeObjectJudgementProcessingIndex = -1;
            activeObjectJudgementProcessingSignature = null;
            completedObjectJudgementProcessingSignature = null;
            activeObjectJudgementGroupProcessingSignature = null;
            completedObjectJudgementGroupProcessingSignature = null;
            ClearLargeRelationSourceCache();
            lock (objectJudgementMaskLock)
            {
                foreach (Cv.Mat mask in objectJudgementLargeMasks.Values)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                }

                objectJudgementLargeMasks.Clear();
                objectJudgementLargeMaskBuildKeys.Clear();
                foreach (Cv.Mat mask in objectJudgementGroupLargeMasks.Values)
                {
                    if (mask != null)
                    {
                        mask.Dispose();
                    }
                }

                objectJudgementGroupLargeMasks.Clear();
                objectJudgementGroupLargeMaskBuildKeys.Clear();
                objectJudgementMaskGeneration++;
            }

            objectJudgementProcessingRequested = false;
            if (latestObjectJudgementImage != null)
            {
                latestObjectJudgementImage.Dispose();
                latestObjectJudgementImage = null;
            }

            if (leftBlockProcessingDisplayControl != null)
            {
                leftBlockProcessingDisplayControl.InvalidateImageView();
            }

            if (rightBlockProcessingDisplayControl != null)
            {
                rightBlockProcessingDisplayControl.InvalidateImageView();
            }
        }

        private void RestoreObjectJudgementDisplayToOriginal()
        {
            if (rightOriginalDisplayControl == null || rightOriginalDisplayControl.IsLargeImageMode)
            {
                return;
            }

            using (Bitmap original = rightOriginalDisplayControl.CloneImage())
            {
                if (original == null)
                {
                    return;
                }

                isSyncingImageView = true;
                try
                {
                    leftBlockProcessingDisplayControl.SetDisplayImage(new Bitmap(original), true);
                    rightBlockProcessingDisplayControl.SetDisplayImage(new Bitmap(original), true);
                }
                finally
                {
                    isSyncingImageView = false;
                }
            }
        }

        private void StartObjectJudgementProcessing(int objectIndex)
        {
            StartObjectJudgementProcessing(objectIndex, -1);
        }

        private void StartObjectJudgementGroupProcessing(string groupId)
        {
            ObjectJudgementGroupSettings group = FindObjectJudgementGroup(groupId);
            if (group == null)
            {
                return;
            }

            List<ObjectJudgementSettings> objectJudgements = GetObjectJudgementsInGroup(groupId);
            if (objectJudgements.Count == 0)
            {
                statusLabel.Text = group.DisplayName + " 尚未包含任何區塊";
                return;
            }

            foreach (ObjectJudgementSettings objectJudgement in objectJudgements)
            {
                List<ImageRelationSettings> relations = GetObjectJudgementRelations(objectJudgement);
                if (relations.Count == 0 ||
                    relations.Any(relation => GetImageProcessingStepsForRelation(relation).Count == 0))
                {
                    statusLabel.Text = GetObjectJudgementDisplayName(
                        objectJudgement,
                        systemParameters.ObjectJudgements.IndexOf(objectJudgement)) +
                        " 尚未設定完整的影像關聯或影像處理";
                    return;
                }

                List<ObjectJudgementProcessingSettings> processingSteps =
                    GetObjectJudgementProcessingChain(objectJudgement, -1);
                // A block may intentionally be relation-only. In that case its
                // relation binary mask is the block result and can participate in
                // the group's OR merge without an extra morphology step.
                if (processingSteps.Any(step =>
                    step == null || string.IsNullOrWhiteSpace(step.Method)))
                {
                    statusLabel.Text = GetObjectJudgementDisplayName(
                        objectJudgement,
                        systemParameters.ObjectJudgements.IndexOf(objectJudgement)) +
                        " 尚未設定完整的區塊處理方式";
                    return;
                }

                ObjectJudgementProcessingSettings unsupportedStep = processingSteps.FirstOrDefault(
                    step => !IsSupportedObjectJudgementProcessingMethod(step.Method));
                if (unsupportedStep != null)
                {
                    statusLabel.Text = "區塊群組處理失效，不支援的區塊處理方式：" + unsupportedStep.Method;
                    return;
                }
            }

            string processingSignature = CreateObjectJudgementGroupProcessingSignature(
                groupId,
                objectJudgements);
            if (HasCompletedObjectJudgementGroupResult(
                    groupId,
                    objectJudgements,
                    processingSignature))
            {
                statusLabel.Text = "已處理";
                SetObjectJudgementParameterApplyStatus("已處理");
                return;
            }

            // A group can contain several blocks and therefore several
            // relation sources. Keep the first relation as the foreground
            // preview, while the block pipeline still evaluates every
            // relation in the background.
            RequestObjectJudgementRelatedImageDisplays(objectJudgements[0]);

            InvalidateObjectJudgementProcessingResults();
            activeObjectJudgementId = null;
            activeObjectJudgementGroupId = groupId;
            objectJudgementProcessingRequested = true;
            activeObjectJudgementGroupProcessingSignature = processingSignature;
            BeginObjectJudgementGroupApplyStatus(groupId);
            statusLabel.Text = "區塊群組處理中...使用 OpenCV";

            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                StartLargeObjectJudgementGroupProcessing(
                    groupId,
                    objectJudgements,
                    processingSignature);
                return;
            }

            Bitmap original = rightOriginalDisplayControl.CloneImage();
            if (original == null)
            {
                statusLabel.Text = "請先載入圖片";
                FailObjectJudgementParameterApply("請先載入圖片");
                return;
            }

            int generation;
            lock (objectJudgementMaskLock)
            {
                generation = objectJudgementMaskGeneration;
            }

            Task.Run(delegate
            {
                Bitmap result = null;
                try
                {
                    Stopwatch processingStopwatch = Stopwatch.StartNew();
                    result = CreateObjectJudgementGroupImage(original, objectJudgements);
                    processingStopwatch.Stop();
                    long processingElapsedMilliseconds = processingStopwatch.ElapsedMilliseconds;
                    BeginInvoke(new Action(delegate
                    {
                        if (!IsObjectJudgementGroupProcessingCurrent(groupId, generation))
                        {
                            result.Dispose();
                            result = null;
                            return;
                        }

                        if (latestObjectJudgementImage != null)
                        {
                            latestObjectJudgementImage.Dispose();
                        }

                        latestObjectJudgementImage = result;
                        result = null;
                        completedObjectJudgementGroupProcessingSignature =
                            activeObjectJudgementGroupProcessingSignature;
                        isSyncingImageView = true;
                        try
                        {
                            leftBlockProcessingDisplayControl.SetDisplayImage(
                                new Bitmap(latestObjectJudgementImage), true);
                            rightBlockProcessingDisplayControl.SetDisplayImage(
                                new Bitmap(latestObjectJudgementImage), true);
                        }
                        finally
                        {
                            isSyncingImageView = false;
                        }

                        statusLabel.Text = "區塊群組處理完成，已使用 OpenCV";
                        ApplySharedImageViewStateToVisibleControls();
                        long previewElapsedMilliseconds = RefreshVisibleObjectJudgementDisplays();
                        CompleteObjectJudgementParameterApplyStatus(
                            processingElapsedMilliseconds,
                            previewElapsedMilliseconds);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (IsObjectJudgementGroupProcessingCurrent(groupId, generation))
                        {
                            statusLabel.Text = "區塊群組處理失敗：" + ex.Message;
                            FailObjectJudgementParameterApply(ex.Message);
                        }
                    }));
                }
                finally
                {
                    original.Dispose();
                    if (result != null)
                    {
                        result.Dispose();
                    }
                }
            });
        }

        private void StartObjectJudgementProcessing(int objectIndex, int processingIndex)
        {
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            List<ImageRelationSettings> relations = GetObjectJudgementRelations(objectJudgement);
            if (relations.Count == 0 || relations.Any(relation => GetImageProcessingStepsForRelation(relation).Count == 0))
            {
                statusLabel.Text = GetObjectJudgementDisplayName(objectJudgement, objectIndex) +
                    " 尚未設定完整的影像關聯或影像處理";
                return;
            }

            List<ObjectJudgementProcessingSettings> processingSteps =
                GetObjectJudgementProcessingChain(objectJudgement, processingIndex);
            // A block may be relation-only. With no extra block-processing step,
            // the relation's binary mask is the block result directly.
            int incompleteStepIndex = processingSteps.FindIndex(
                step => step == null || string.IsNullOrWhiteSpace(step.Method));
            if (incompleteStepIndex >= 0)
            {
                statusLabel.Text = GetObjectJudgementDisplayName(objectJudgement, objectIndex) +
                    " 的處理" + (incompleteStepIndex + 1).ToString(CultureInfo.InvariantCulture) +
                    " 尚未設定處理方式";
                FocusObjectJudgementProcessing(objectIndex, incompleteStepIndex);
                return;
            }

            ObjectJudgementProcessingSettings unsupportedStep = processingSteps.FirstOrDefault(
                step => !IsSupportedObjectJudgementProcessingMethod(step.Method));
            if (unsupportedStep != null)
            {
                statusLabel.Text = GetObjectJudgementDisplayName(objectJudgement, objectIndex) +
                    " 區塊處理失效，不支援的區塊處理方式：" + unsupportedStep.Method;
                return;
            }

            string processingSignature = CreateObjectJudgementProcessingSignature(
                objectJudgement,
                processingSteps);
            if (HasCompletedObjectJudgementResult(objectJudgement, processingSteps, processingSignature))
            {
                statusLabel.Text = "已處理";
                SetObjectJudgementParameterApplyStatus("已處理");
                return;
            }

            RequestObjectJudgementRelatedImageDisplays(objectJudgement);

            // "處理" means a fresh run. Clear completed masks and relation
            // source caches so this block cannot analyze a stale preprocessing result.
            InvalidateObjectJudgementProcessingResults();
            SetActiveObjectJudgement(objectJudgement);
            ActivateObjectJudgementRelation(objectJudgement);
            objectJudgementProcessingRequested = true;
            activeObjectJudgementProcessingIndex = processingIndex;
            activeObjectJudgementProcessingSignature = processingSignature;
            BeginObjectJudgementParameterApplyStatus(objectIndex);
            string processingChainText = processingSteps.Count == 0
                ? "關聯二值結果"
                : string.Join(
                    " -> ",
                    processingSteps.Select((step, index) =>
                        GetObjectJudgementProcessingDisplayName(step, index)).ToArray());
            statusLabel.Text = GetObjectJudgementDisplayName(objectJudgement, objectIndex) +
                " 處理中... " + processingChainText + "（OpenCV）";

            if (rightOriginalDisplayControl.IsLargeImageMode)
            {
                StartLargeObjectJudgementProcessing(objectJudgement, processingSteps);
                return;
            }

            Bitmap original = rightOriginalDisplayControl.CloneImage();
            if (original == null)
            {
                statusLabel.Text = "請先載入圖片";
                FailObjectJudgementParameterApply("請先載入圖片");
                return;
            }

            int generation;
            lock (objectJudgementMaskLock)
            {
                generation = objectJudgementMaskGeneration;
            }

            Task.Run(delegate
            {
                Bitmap result = null;
                try
                {
                    Stopwatch processingStopwatch = Stopwatch.StartNew();
                    result = CreateObjectJudgementImage(original, objectJudgement, processingSteps);
                    processingStopwatch.Stop();
                    long processingElapsedMilliseconds = processingStopwatch.ElapsedMilliseconds;
                    BeginInvoke(new Action(delegate
                    {
                        if (!IsObjectJudgementProcessingCurrent(objectJudgement.Id, generation))
                        {
                            result.Dispose();
                            result = null;
                            return;
                        }

                        if (latestObjectJudgementImage != null)
                        {
                            latestObjectJudgementImage.Dispose();
                        }

                        latestObjectJudgementImage = result;
                        result = null;
                        completedObjectJudgementProcessingSignature =
                            activeObjectJudgementProcessingSignature;
                        isSyncingImageView = true;
                        try
                        {
                            leftBlockProcessingDisplayControl.SetDisplayImage(
                                new Bitmap(latestObjectJudgementImage), true);
                            rightBlockProcessingDisplayControl.SetDisplayImage(
                                new Bitmap(latestObjectJudgementImage), true);
                        }
                        finally
                        {
                            isSyncingImageView = false;
                        }

                        statusLabel.Text = "區塊處理完成，已使用 OpenCV";
                        ApplySharedImageViewStateToVisibleControls();
                        long previewElapsedMilliseconds = RefreshVisibleObjectJudgementDisplays();
                        CompleteObjectJudgementParameterApplyStatus(
                            processingElapsedMilliseconds,
                            previewElapsedMilliseconds);
                    }));
                }
                catch (Exception ex)
                {
                    BeginInvoke(new Action(delegate
                    {
                        if (IsObjectJudgementProcessingCurrent(objectJudgement.Id, generation))
                        {
                            statusLabel.Text = "區塊處理失敗：" + ex.Message;
                            FailObjectJudgementParameterApply(ex.Message);
                        }
                    }));
                }
                finally
                {
                    original.Dispose();
                    if (result != null)
                    {
                        result.Dispose();
                    }
                }
            });
        }

        private void StartLargeObjectJudgementProcessing(
            ObjectJudgementSettings objectJudgement,
            List<ObjectJudgementProcessingSettings> processingSteps)
        {
            LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source == null)
            {
                statusLabel.Text = "請先載入圖片";
                FailObjectJudgementParameterApply("請先載入圖片");
                return;
            }

            int generation;
            lock (objectJudgementMaskLock)
            {
                generation = objectJudgementMaskGeneration;
            }

            List<Rectangle> validRois = OrderRoiRectanglesForVisibleArea(systemParameters.RoiRegions
                .Where(roiRegion => roiRegion.Bounds.Width > 0 && roiRegion.Bounds.Height > 0)
                .Select(roiRegion => roiRegion.Bounds)
                .ToList());
            lock (objectJudgementMaskLock)
            {
                objectJudgementPendingLargeMaskBuilds = objectJudgementParameterApplyInProgress
                    ? validRois.Count
                    : 0;
                objectJudgementAccumulatedProcessingMilliseconds = 0;
            }

            if (validRois.Count == 0)
            {
                FailObjectJudgementParameterApply("尚未設定有效的 ROI");
                source.ReleaseReference();
                return;
            }

            foreach (Rectangle roi in validRois)
            {
                StartLargeObjectJudgementMaskBuild(
                    source, objectJudgement, processingSteps, roi, generation);
            }

            source.ReleaseReference();
            leftBlockProcessingDisplayControl.InvalidateImageView();
            rightBlockProcessingDisplayControl.InvalidateImageView();
        }

        private void StartLargeObjectJudgementGroupProcessing(
            string groupId,
            IList<ObjectJudgementSettings> objectJudgements,
            string processingSignature)
        {
            LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source == null)
            {
                statusLabel.Text = "請先載入圖片";
                FailObjectJudgementParameterApply("請先載入圖片");
                return;
            }

            int generation;
            lock (objectJudgementMaskLock)
            {
                generation = objectJudgementMaskGeneration;
            }

            List<Rectangle> validRois = OrderRoiRectanglesForVisibleArea(systemParameters.RoiRegions
                .Where(roiRegion => roiRegion.Bounds.Width > 0 && roiRegion.Bounds.Height > 0)
                .Select(roiRegion => roiRegion.Bounds)
                .ToList());
            lock (objectJudgementMaskLock)
            {
                objectJudgementPendingLargeMaskBuilds = objectJudgementParameterApplyInProgress
                    ? validRois.Count
                    : 0;
                objectJudgementAccumulatedProcessingMilliseconds = 0;
            }

            if (validRois.Count == 0)
            {
                FailObjectJudgementParameterApply("尚未設定有效的 ROI");
                source.ReleaseReference();
                return;
            }

            foreach (Rectangle roi in validRois)
            {
                StartLargeObjectJudgementGroupMaskBuild(
                    source,
                    groupId,
                    objectJudgements,
                    processingSignature,
                    roi,
                    generation);
            }

            source.ReleaseReference();
            leftBlockProcessingDisplayControl.InvalidateImageView();
            rightBlockProcessingDisplayControl.InvalidateImageView();
        }

        private void StartLargeObjectJudgementGroupMaskBuild(
            LargeImageSource originalSource,
            string groupId,
            IList<ObjectJudgementSettings> objectJudgements,
            string processingSignature,
            Rectangle roi,
            int generation)
        {
            if (roi.Width <= 0 || roi.Height <= 0)
            {
                return;
            }

            string maskKey = CreateObjectJudgementGroupMaskKey(processingSignature, roi);
            lock (objectJudgementMaskLock)
            {
                if (objectJudgementGroupLargeMasks.ContainsKey(maskKey) ||
                    objectJudgementGroupLargeMaskBuildKeys.Contains(maskKey))
                {
                    return;
                }

                objectJudgementGroupLargeMaskBuildKeys.Add(maskKey);
            }

            LargeImageSource sourceReference = originalSource.AddReference();
            Task.Run(delegate
            {
                Cv.Mat result = null;
                try
                {
                    largeNativeProcessingGate.Wait();
                    try
                    {
                        if (!IsObjectJudgementGroupProcessingCurrent(groupId, generation))
                        {
                            return;
                        }

                        Stopwatch processingStopwatch = Stopwatch.StartNew();
                        result = CreateLargeObjectJudgementGroupMask(
                            sourceReference,
                            objectJudgements,
                            roi);
                        processingStopwatch.Stop();
                        long processingElapsedMilliseconds = processingStopwatch.ElapsedMilliseconds;

                        Cv.Mat completed = result;
                        result = null;
                        BeginInvoke(new Action(delegate
                        {
                            if (!IsObjectJudgementGroupProcessingCurrent(groupId, generation))
                            {
                                completed.Dispose();
                                return;
                            }

                            lock (objectJudgementMaskLock)
                            {
                                Cv.Mat previous;
                                if (objectJudgementGroupLargeMasks.TryGetValue(maskKey, out previous))
                                {
                                    previous.Dispose();
                                }

                                objectJudgementGroupLargeMasks[maskKey] = completed;
                                objectJudgementGroupLargeMaskBuildKeys.Remove(maskKey);
                            }

                            statusLabel.Text = "區塊群組處理完成，已使用 OpenCV";
                            if (objectJudgementParameterApplyInProgress)
                            {
                                objectJudgementAccumulatedProcessingMilliseconds += processingElapsedMilliseconds;
                                objectJudgementPendingLargeMaskBuilds--;
                                if (objectJudgementPendingLargeMaskBuilds <= 0)
                                {
                                    completedObjectJudgementGroupProcessingSignature =
                                        activeObjectJudgementGroupProcessingSignature;
                                    long previewElapsedMilliseconds =
                                        RefreshVisibleObjectJudgementDisplays();
                                    CompleteObjectJudgementParameterApplyStatus(
                                        objectJudgementAccumulatedProcessingMilliseconds,
                                        previewElapsedMilliseconds);
                                }
                                else
                                {
                                    leftBlockProcessingDisplayControl.InvalidateImageView();
                                    rightBlockProcessingDisplayControl.InvalidateImageView();
                                }
                            }
                            else
                            {
                                RefreshVisibleObjectJudgementDisplays();
                            }
                        }));
                    }
                    finally
                    {
                        largeNativeProcessingGate.Release();
                    }
                }
                catch (Exception ex)
                {
                    if (result != null)
                    {
                        result.Dispose();
                    }

                    BeginInvoke(new Action(delegate
                    {
                        lock (objectJudgementMaskLock)
                        {
                            objectJudgementGroupLargeMaskBuildKeys.Remove(maskKey);
                        }

                        if (IsObjectJudgementGroupProcessingCurrent(groupId, generation))
                        {
                            statusLabel.Text = "區塊群組處理失敗：" + ex.Message;
                            FailObjectJudgementParameterApply(ex.Message);
                        }
                    }));
                }
                finally
                {
                    sourceReference.ReleaseReference();
                }
            });
        }

        private Cv.Mat CreateLargeObjectJudgementGroupMask(
            LargeImageSource originalSource,
            IList<ObjectJudgementSettings> objectJudgements,
            Rectangle roi,
            ObjectDefinitionSourceTiming timing = null)
        {
            var combined = new Cv.Mat(
                roi.Height,
                roi.Width,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ObjectJudgementSettings objectJudgement in objectJudgements)
                {
                    List<ObjectJudgementProcessingSettings> processingSteps =
                        GetObjectJudgementProcessingChain(objectJudgement, -1);
                    Cv.Mat cachedObjectMask;
                    if (TryGetCachedObjectJudgementMask(
                        objectJudgement,
                        processingSteps,
                        roi,
                        out cachedObjectMask))
                    {
                        using (cachedObjectMask)
                        {
                            Cv.Cv2.BitwiseOr(combined, cachedObjectMask, combined);
                        }
                        continue;
                    }

                    using (Cv.Mat baseMask = CreateLargeObjectJudgementBaseMask(
                        originalSource,
                        objectJudgement,
                        roi,
                        timing))
                    {
                        Stopwatch objectProcessingStopwatch = timing == null
                            ? null
                            : Stopwatch.StartNew();
                        using (Cv.Mat objectMask = ApplyObjectJudgementProcessingOpenCv(
                            baseMask,
                            processingSteps,
                            timing))
                        {
                            if (objectProcessingStopwatch != null)
                            {
                                objectProcessingStopwatch.Stop();
                                timing.ObjectProcessingMilliseconds += objectProcessingStopwatch.ElapsedMilliseconds;
                            }

                        Stopwatch mergeStopwatch = timing == null
                            ? null
                            : Stopwatch.StartNew();
                            Cv.Cv2.BitwiseOr(combined, objectMask, combined);
                            if (mergeStopwatch != null)
                            {
                                mergeStopwatch.Stop();
                                timing.MaskMergeMilliseconds += mergeStopwatch.ElapsedMilliseconds;
                            }
                        }
                    }
                }

                return combined;
            }
            catch
            {
                combined.Dispose();
                throw;
            }
        }

        private void StartLargeObjectJudgementMaskBuild(
            LargeImageSource originalSource,
            ObjectJudgementSettings objectJudgement,
            List<ObjectJudgementProcessingSettings> processingSteps,
            Rectangle roi,
            int generation)
        {
            if (roi.Width <= 0 || roi.Height <= 0)
            {
                return;
            }

            string maskKey = CreateObjectJudgementMaskKey(objectJudgement, processingSteps, roi);
            lock (objectJudgementMaskLock)
            {
                if (objectJudgementLargeMasks.ContainsKey(maskKey) ||
                    objectJudgementLargeMaskBuildKeys.Contains(maskKey))
                {
                    return;
                }

                objectJudgementLargeMaskBuildKeys.Add(maskKey);
            }

            LargeImageSource sourceReference = originalSource.AddReference();
            Task.Run(delegate
            {
                Cv.Mat result = null;
                try
                {
                    largeNativeProcessingGate.Wait();
                    try
                    {
                        if (!IsObjectJudgementProcessingCurrent(objectJudgement.Id, generation))
                        {
                            return;
                        }

                        Stopwatch processingStopwatch = Stopwatch.StartNew();
                        using (Cv.Mat baseMask = CreateLargeObjectJudgementBaseMask(
                            sourceReference, objectJudgement, roi))
                        {
                            result = ApplyObjectJudgementProcessingOpenCv(
                                baseMask, processingSteps);
                        }
                        processingStopwatch.Stop();
                        long processingElapsedMilliseconds = processingStopwatch.ElapsedMilliseconds;

                        Cv.Mat completed = result;
                        result = null;
                        BeginInvoke(new Action(delegate
                        {
                            if (!IsObjectJudgementProcessingCurrent(objectJudgement.Id, generation))
                            {
                                completed.Dispose();
                                return;
                            }

                            lock (objectJudgementMaskLock)
                            {
                                Cv.Mat previous;
                                if (objectJudgementLargeMasks.TryGetValue(maskKey, out previous))
                                {
                                    previous.Dispose();
                                }

                                objectJudgementLargeMasks[maskKey] = completed;
                                objectJudgementLargeMaskBuildKeys.Remove(maskKey);
                            }

                            statusLabel.Text = "區塊處理完成，已使用 OpenCV";
                            if (objectJudgementParameterApplyInProgress)
                            {
                                objectJudgementAccumulatedProcessingMilliseconds += processingElapsedMilliseconds;
                                objectJudgementPendingLargeMaskBuilds--;
                                if (objectJudgementPendingLargeMaskBuilds <= 0)
                                {
                                    completedObjectJudgementProcessingSignature =
                                        activeObjectJudgementProcessingSignature;
                                    long previewElapsedMilliseconds = RefreshVisibleObjectJudgementDisplays();
                                    CompleteObjectJudgementParameterApplyStatus(
                                        objectJudgementAccumulatedProcessingMilliseconds,
                                        previewElapsedMilliseconds);
                                }
                                else
                                {
                                    leftBlockProcessingDisplayControl.InvalidateImageView();
                                    rightBlockProcessingDisplayControl.InvalidateImageView();
                                }
                            }
                            else
                            {
                                RefreshVisibleObjectJudgementDisplays();
                            }
                        }));
                    }
                    finally
                    {
                        largeNativeProcessingGate.Release();
                    }
                }
                catch (Exception ex)
                {
                    if (result != null)
                    {
                        result.Dispose();
                    }

                    BeginInvoke(new Action(delegate
                    {
                        lock (objectJudgementMaskLock)
                        {
                            objectJudgementLargeMaskBuildKeys.Remove(maskKey);
                        }

                        if (IsObjectJudgementProcessingCurrent(objectJudgement.Id, generation))
                        {
                            statusLabel.Text = "區塊處理失敗：" + ex.Message;
                            FailObjectJudgementParameterApply(ex.Message);
                        }
                    }));
                }
                finally
                {
                    sourceReference.ReleaseReference();
                }
            });
        }

        private long RefreshVisibleObjectJudgementDisplays()
        {
            if (SkipImageDisplayUpdate())
            {
                return 0;
            }

            bool refreshed = false;
            Stopwatch previewStopwatch = Stopwatch.StartNew();

            if (leftImageTabControl.Visible &&
                leftImageTabControl.SelectedTab == leftBlockProcessingTabPage &&
                leftBlockProcessingDisplayControl != null &&
                leftBlockProcessingDisplayControl.HasImage)
            {
                leftBlockProcessingDisplayControl.RefreshImageViewNow();
                refreshed = true;
            }

            if (rightImageTabControl.Visible &&
                rightImageTabControl.SelectedTab == rightBlockProcessingTabPage &&
                rightBlockProcessingDisplayControl != null &&
                rightBlockProcessingDisplayControl.HasImage)
            {
                rightBlockProcessingDisplayControl.RefreshImageViewNow();
                refreshed = true;
            }

            previewStopwatch.Stop();
            return refreshed ? Math.Max(1, previewStopwatch.ElapsedMilliseconds) : 0;
        }

        private bool IsObjectJudgementProcessingCurrent(string objectId, int generation)
        {
            lock (objectJudgementMaskLock)
            {
                return objectJudgementProcessingRequested &&
                    generation == objectJudgementMaskGeneration &&
                    string.Equals(activeObjectJudgementId, objectId, StringComparison.Ordinal);
            }
        }

        private bool IsObjectJudgementGroupProcessingCurrent(string groupId, int generation)
        {
            lock (objectJudgementMaskLock)
            {
                return objectJudgementProcessingRequested &&
                    generation == objectJudgementMaskGeneration &&
                    string.Equals(activeObjectJudgementGroupId, groupId, StringComparison.Ordinal);
            }
        }

        private List<ObjectJudgementProcessingSettings> GetObjectJudgementProcessingChain(
            ObjectJudgementSettings objectJudgement,
            int processingIndex)
        {
            var steps = new List<ObjectJudgementProcessingSettings>();
            if (objectJudgement == null)
            {
                return steps;
            }

            int lastIndex = processingIndex < 0
                ? objectJudgement.ProcessingSteps.Count - 1
                : Math.Min(processingIndex, objectJudgement.ProcessingSteps.Count - 1);
            for (int index = 0; index <= lastIndex; index++)
            {
                steps.Add(objectJudgement.ProcessingSteps[index]);
            }

            return steps;
        }

        private string CreateObjectJudgementMaskKey(
            ObjectJudgementSettings objectJudgement,
            IEnumerable<ObjectJudgementProcessingSettings> processingSteps,
            Rectangle roi)
        {
            var parts = new List<string>
            {
                "object-judgement",
                objectJudgement == null ? string.Empty : objectJudgement.Id ?? string.Empty,
                roi.X.ToString(CultureInfo.InvariantCulture),
                roi.Y.ToString(CultureInfo.InvariantCulture),
                roi.Width.ToString(CultureInfo.InvariantCulture),
                roi.Height.ToString(CultureInfo.InvariantCulture)
            };
            if (objectJudgement != null)
            {
                parts.Add(objectJudgement.RelationType ?? string.Empty);
                parts.Add(objectJudgement.RelationId ?? string.Empty);
                foreach (ObjectJudgementProcessingSettings step in processingSteps ??
                    Enumerable.Empty<ObjectJudgementProcessingSettings>())
                {
                    parts.Add(step.Method ?? string.Empty);
                    parts.Add(step.Parameters ?? string.Empty);
                }
            }

            return string.Join("|", parts.ToArray());
        }

        private string CreateObjectJudgementProcessingSignature(
            ObjectJudgementSettings objectJudgement,
            IEnumerable<ObjectJudgementProcessingSettings> processingSteps)
        {
            var parts = new List<string>
            {
                "object-judgement-result",
                systemParameters.LastImagePath ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                preprocessedImageGeneration.ToString(CultureInfo.InvariantCulture),
                objectJudgement == null ? string.Empty : objectJudgement.Id ?? string.Empty,
                objectJudgement == null ? string.Empty : objectJudgement.RelationType ?? string.Empty,
                objectJudgement == null ? string.Empty : objectJudgement.RelationId ?? string.Empty
            };

            foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
            {
                parts.Add("relation");
                parts.Add(relation.Id ?? string.Empty);
                parts.Add(relation.SourceType ?? string.Empty);
                parts.Add(relation.SourceId ?? string.Empty);
                parts.Add(relation.ProcessingType ?? string.Empty);
                parts.Add(relation.ProcessingId ?? string.Empty);
                parts.Add(relation.GroupId ?? string.Empty);

                foreach (ImageProcessingStepSettings step in GetImageProcessingStepsForRelation(relation))
                {
                    parts.Add("relation-step");
                    parts.Add(step.Id ?? string.Empty);
                    parts.Add(step.Method ?? string.Empty);
                    parts.Add(step.Parameters ?? string.Empty);
                }
            }

            foreach (ObjectJudgementProcessingSettings step in processingSteps ??
                Enumerable.Empty<ObjectJudgementProcessingSettings>())
            {
                parts.Add("object-step");
                parts.Add(step.Id ?? string.Empty);
                parts.Add(step.Method ?? string.Empty);
                parts.Add(step.Parameters ?? string.Empty);
            }

            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle bounds = roiRegion.Bounds;
                parts.Add("roi");
                parts.Add(bounds.X.ToString(CultureInfo.InvariantCulture));
                parts.Add(bounds.Y.ToString(CultureInfo.InvariantCulture));
                parts.Add(bounds.Width.ToString(CultureInfo.InvariantCulture));
                parts.Add(bounds.Height.ToString(CultureInfo.InvariantCulture));
            }

            return string.Join("|", parts.ToArray());
        }

        private string CreateObjectJudgementGroupProcessingSignature(
            string groupId,
            IList<ObjectJudgementSettings> objectJudgements)
        {
            var parts = new List<string>
            {
                "object-judgement-group-result",
                groupId ?? string.Empty,
                systemParameters.LastImagePath ?? string.Empty,
                imageSourceGeneration.ToString(CultureInfo.InvariantCulture),
                preprocessedImageGeneration.ToString(CultureInfo.InvariantCulture)
            };

            foreach (ObjectJudgementSettings objectJudgement in objectJudgements ??
                new List<ObjectJudgementSettings>())
            {
                parts.Add(objectJudgement == null ? string.Empty : objectJudgement.Id ?? string.Empty);
                parts.Add(CreateObjectJudgementProcessingSignature(
                    objectJudgement,
                    objectJudgement == null
                        ? Enumerable.Empty<ObjectJudgementProcessingSettings>()
                        : objectJudgement.ProcessingSteps));
            }

            return string.Join("|", parts.ToArray());
        }

        private bool HasCompletedObjectJudgementResult(
            ObjectJudgementSettings objectJudgement,
            IList<ObjectJudgementProcessingSettings> processingSteps,
            string processingSignature)
        {
            if (!objectJudgementProcessingRequested ||
                !string.Equals(activeObjectJudgementId, objectJudgement == null ? null : objectJudgement.Id,
                    StringComparison.Ordinal) ||
                !string.Equals(activeObjectJudgementProcessingSignature, processingSignature,
                    StringComparison.Ordinal) ||
                !string.Equals(completedObjectJudgementProcessingSignature, processingSignature,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.IsLargeImageMode)
            {
                return latestObjectJudgementImage != null;
            }

            List<Rectangle> validRois = systemParameters.RoiRegions
                .Where(roiRegion => roiRegion.Bounds.Width > 0 && roiRegion.Bounds.Height > 0)
                .Select(roiRegion => roiRegion.Bounds)
                .ToList();
            if (validRois.Count == 0)
            {
                return false;
            }

            lock (objectJudgementMaskLock)
            {
                foreach (Rectangle roi in validRois)
                {
                    string maskKey = CreateObjectJudgementMaskKey(
                        objectJudgement,
                        processingSteps,
                        roi);
                    if (!objectJudgementLargeMasks.ContainsKey(maskKey) ||
                        objectJudgementLargeMaskBuildKeys.Contains(maskKey))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private bool HasCompletedObjectJudgementGroupResult(
            string groupId,
            IList<ObjectJudgementSettings> objectJudgements,
            string processingSignature)
        {
            if (!objectJudgementProcessingRequested ||
                !string.Equals(activeObjectJudgementGroupId, groupId, StringComparison.Ordinal) ||
                !string.Equals(activeObjectJudgementGroupProcessingSignature, processingSignature,
                    StringComparison.Ordinal) ||
                !string.Equals(completedObjectJudgementGroupProcessingSignature, processingSignature,
                    StringComparison.Ordinal))
            {
                return false;
            }

            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.IsLargeImageMode)
            {
                return latestObjectJudgementImage != null;
            }

            List<Rectangle> validRois = systemParameters.RoiRegions
                .Where(roiRegion => roiRegion.Bounds.Width > 0 && roiRegion.Bounds.Height > 0)
                .Select(roiRegion => roiRegion.Bounds)
                .ToList();
            if (validRois.Count == 0)
            {
                return false;
            }

            lock (objectJudgementMaskLock)
            {
                foreach (Rectangle roi in validRois)
                {
                    string maskKey = CreateObjectJudgementGroupMaskKey(
                        processingSignature, roi);
                    if (!objectJudgementGroupLargeMasks.ContainsKey(maskKey) ||
                        objectJudgementGroupLargeMaskBuildKeys.Contains(maskKey))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private List<ObjectJudgementSettings> GetObjectJudgementsInGroup(string groupId)
        {
            var result = new List<ObjectJudgementSettings>();
            var visitedGroups = new HashSet<string>(StringComparer.Ordinal);
            CollectObjectJudgementsInGroup(groupId, result, visitedGroups);
            return result;
        }

        private void CollectObjectJudgementsInGroup(
            string groupId,
            List<ObjectJudgementSettings> result,
            HashSet<string> visitedGroups)
        {
            if (string.IsNullOrWhiteSpace(groupId) || !visitedGroups.Add(groupId))
            {
                return;
            }

            foreach (ObjectJudgementSettings objectJudgement in systemParameters.ObjectJudgements)
            {
                if (string.Equals(objectJudgement.GroupId, groupId, StringComparison.Ordinal))
                {
                    result.Add(objectJudgement);
                }
            }

            foreach (ObjectJudgementGroupSettings childGroup in systemParameters.ObjectJudgementGroups)
            {
                if (string.Equals(childGroup.ParentGroupId, groupId, StringComparison.Ordinal))
                {
                    CollectObjectJudgementsInGroup(childGroup.Id, result, visitedGroups);
                }
            }
        }

        private static string CreateObjectJudgementGroupMaskKey(
            string processingSignature,
            Rectangle roi)
        {
            return string.Join(
                "|",
                new[]
                {
                    "object-judgement-group-mask",
                    processingSignature ?? string.Empty,
                    roi.X.ToString(CultureInfo.InvariantCulture),
                    roi.Y.ToString(CultureInfo.InvariantCulture),
                    roi.Width.ToString(CultureInfo.InvariantCulture),
                    roi.Height.ToString(CultureInfo.InvariantCulture)
                });
        }

        private List<ImageRelationSettings> GetObjectJudgementRelations(ObjectJudgementSettings objectJudgement)
        {
            if (objectJudgement == null || string.IsNullOrWhiteSpace(objectJudgement.RelationId))
            {
                return new List<ImageRelationSettings>();
            }

            if (string.Equals(objectJudgement.RelationType, "Group", StringComparison.Ordinal))
            {
                return GetImageRelationGroupRelations(objectJudgement.RelationId);
            }

            ImageRelationSettings relation = systemParameters.ImageRelations.Find(
                item => string.Equals(item.Id, objectJudgement.RelationId, StringComparison.Ordinal));
            return relation == null
                ? new List<ImageRelationSettings>()
                : new List<ImageRelationSettings> { relation };
        }

        private Bitmap CreateObjectJudgementGroupImage(
            Bitmap original,
            IList<ObjectJudgementSettings> objectJudgements)
        {
            var result = new Bitmap(original);
            using (Cv.Mat originalGray = CreateOpenCvGrayMat(original))
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = Rectangle.Intersect(
                        roiRegion.Bounds,
                        new Rectangle(0, 0, original.Width, original.Height));
                    if (roi.Width <= 0 || roi.Height <= 0)
                    {
                        continue;
                    }

                    using (var combined = new Cv.Mat(
                        roi.Height,
                        roi.Width,
                        Cv.MatType.CV_8UC1,
                        Cv.Scalar.All(0)))
                    {
                        foreach (ObjectJudgementSettings objectJudgement in objectJudgements)
                        {
                            List<ObjectJudgementProcessingSettings> processingSteps =
                                GetObjectJudgementProcessingChain(objectJudgement, -1);
                            using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                                original,
                                originalGray,
                                objectJudgement,
                                roi))
                            using (Cv.Mat objectMask = ApplyObjectJudgementProcessingOpenCv(
                                baseMask,
                                processingSteps))
                            {
                                Cv.Cv2.BitwiseOr(combined, objectMask, combined);
                            }
                        }

                        PaintRedOverlayImage(result, roi, ConvertOpenCvBinaryMask(combined));
                    }
                }
            }

            return result;
        }

        private Bitmap CreateObjectJudgementImage(
            Bitmap original,
            ObjectJudgementSettings objectJudgement,
            IEnumerable<ObjectJudgementProcessingSettings> processingSteps)
        {
            var result = new Bitmap(original);
            using (Cv.Mat originalGray = CreateOpenCvGrayMat(original))
            {
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle roi = Rectangle.Intersect(
                        roiRegion.Bounds,
                        new Rectangle(0, 0, original.Width, original.Height));
                    if (roi.Width <= 0 || roi.Height <= 0)
                    {
                        continue;
                    }

                    using (Cv.Mat baseMask = CreateObjectJudgementBaseMaskFromBitmap(
                        original, originalGray, objectJudgement, roi))
                    using (Cv.Mat finalMask = ApplyObjectJudgementProcessingOpenCv(
                        baseMask, processingSteps))
                    {
                        PaintRedOverlayImage(result, roi, ConvertOpenCvBinaryMask(finalMask));
                    }
                }
            }

            return result;
        }

        private Cv.Mat CreateLargeObjectJudgementBaseMask(
            LargeImageSource originalSource,
            ObjectJudgementSettings objectJudgement,
            Rectangle roi,
            ObjectDefinitionSourceTiming timing = null)
        {
            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                {
                    Stopwatch relationSourceStopwatch = timing == null
                        ? null
                        : Stopwatch.StartNew();
                    LargeImageSource relationSource = GetLargeRelationSource(originalSource, relation);
                    if (relationSourceStopwatch != null)
                    {
                        relationSourceStopwatch.Stop();
                        timing.RelationSourceMilliseconds += relationSourceStopwatch.ElapsedMilliseconds;
                    }
                    try
                    {
                        Stopwatch grayStopwatch = timing == null
                            ? null
                            : Stopwatch.StartNew();
                        using (Cv.Mat gray = GetOrCreateLargeRoiOpenCvGrayCache(relationSource, roi))
                        {
                            if (grayStopwatch != null)
                            {
                                grayStopwatch.Stop();
                                timing.GrayPreparationMilliseconds += grayStopwatch.ElapsedMilliseconds;
                            }

                            Stopwatch processingStopwatch = timing == null
                                ? null
                                : Stopwatch.StartNew();
                            using (Cv.Mat relationMask = CreateCombinedImageProcessingGroupMask(
                                gray,
                                GetImageProcessingStepsForRelation(relation)))
                            {
                                if (processingStopwatch != null)
                                {
                                    processingStopwatch.Stop();
                                    timing.ImageProcessingMilliseconds += processingStopwatch.ElapsedMilliseconds;
                                }

                                Stopwatch mergeStopwatch = timing == null
                                    ? null
                                    : Stopwatch.StartNew();
                                Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                                if (mergeStopwatch != null)
                                {
                                    mergeStopwatch.Stop();
                                    timing.MaskMergeMilliseconds += mergeStopwatch.ElapsedMilliseconds;
                                }
                            }
                        }
                    }
                    finally
                    {
                        if (relationSource != null)
                        {
                            relationSource.ReleaseReference();
                        }
                    }
                }

                return combined;
            }
            catch
            {
                combined.Dispose();
                throw;
            }
        }

        private Cv.Mat CreateObjectJudgementBaseMaskFromBitmap(
            Bitmap original,
            Cv.Mat originalGray,
            ObjectJudgementSettings objectJudgement,
            Rectangle roi)
        {
            var combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
            try
            {
                foreach (ImageRelationSettings relation in GetObjectJudgementRelations(objectJudgement))
                {
                    Bitmap relationBitmap = null;
                    Cv.Mat relationGray = originalGray;
                    try
                    {
                        if (!string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
                        {
                            relationBitmap = CreateRelationSourceBitmap(original, relation);
                            relationGray = CreateOpenCvGrayMat(relationBitmap);
                        }

                        using (Cv.Mat roiGray = new Cv.Mat(
                            relationGray,
                            new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height)))
                        {
                            using (Cv.Mat relationMask = CreateCombinedImageProcessingGroupMask(
                                roiGray,
                                GetImageProcessingStepsForRelation(relation)))
                            {
                                Cv.Cv2.BitwiseOr(combined, relationMask, combined);
                            }
                        }
                    }
                    finally
                    {
                        if (!ReferenceEquals(relationGray, originalGray) && relationGray != null)
                        {
                            relationGray.Dispose();
                        }

                        if (relationBitmap != null)
                        {
                            relationBitmap.Dispose();
                        }
                    }
                }

                return combined;
            }
            catch
            {
                combined.Dispose();
                throw;
            }
        }

        private static Cv.Mat ApplyObjectJudgementProcessingOpenCv(
            Cv.Mat source,
            IEnumerable<ObjectJudgementProcessingSettings> processingSteps,
            ObjectDefinitionSourceTiming timing = null)
        {
            Stopwatch cloneStopwatch = timing == null
                ? null
                : Stopwatch.StartNew();
            Cv.Mat current = source.Clone();
            if (cloneStopwatch != null)
            {
                cloneStopwatch.Stop();
                timing.InitialCloneMilliseconds += cloneStopwatch.ElapsedMilliseconds;
                timing.ObjectProcessingDetails.Add(
                    "處理前 Clone：" + cloneStopwatch.ElapsedMilliseconds + " ms");
            }
            try
            {
                foreach (ObjectJudgementProcessingSettings processing in processingSteps)
                {
                    if (processing == null)
                    {
                        throw new InvalidOperationException("區塊處理步驟不存在");
                    }

                    string normalizedMethod = NormalizeObjectJudgementProcessingMethod(processing.Method);
                    Stopwatch stepStopwatch = timing == null
                        ? null
                        : Stopwatch.StartNew();
                    Cv.Mat next = ApplyObjectJudgementProcessingStepOpenCv(
                        current, normalizedMethod, ParseImageProcessingParameters(processing.Parameters));
                    if (stepStopwatch != null)
                    {
                        stepStopwatch.Stop();
                        long elapsedMilliseconds = stepStopwatch.ElapsedMilliseconds;
                        timing.ObjectProcessingDetails.Add(
                            normalizedMethod + "：" + elapsedMilliseconds + " ms");
                    }
                    current.Dispose();
                    current = next;
                }

                return current;
            }
            catch
            {
                current.Dispose();
                throw;
            }
        }

        private static Cv.Mat ApplyObjectJudgementProcessingStepOpenCv(
            Cv.Mat source, string method, Dictionary<string, string> parameters)
        {
            string normalizedMethod = NormalizeObjectJudgementProcessingMethod(method);
            if (IsMorphologyObjectProcessingMethod(normalizedMethod))
            {
                int kernelSize = NormalizeObjectJudgementKernelSize(
                    GetIntParameter(parameters, "KernelSize", 3));
                int iterations = Math.Max(1, GetIntParameter(parameters, "Iterations", 1));
                Cv.MorphShapes shape = GetObjectJudgementMorphShape(
                    GetStringParameter(parameters, "Shape", "Rect"));
                using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                    shape, new Cv.Size(kernelSize, kernelSize)))
                {
                    var result = new Cv.Mat();
                    if (string.Equals(normalizedMethod, "Dilate", StringComparison.OrdinalIgnoreCase))
                    {
                        Cv.Cv2.Dilate(source, result, kernel, null, iterations);
                    }
                    else if (string.Equals(normalizedMethod, "Erode", StringComparison.OrdinalIgnoreCase))
                    {
                        Cv.Cv2.Erode(source, result, kernel, null, iterations);
                    }
                    else if (string.Equals(normalizedMethod, "Close", StringComparison.OrdinalIgnoreCase))
                    {
                        Cv.Cv2.MorphologyEx(source, result, Cv.MorphTypes.Close, kernel, null, iterations);
                    }
                    else
                    {
                        Cv.Cv2.MorphologyEx(source, result, Cv.MorphTypes.Open, kernel, null, iterations);
                    }

                    return result;
                }
            }

            if (string.Equals(normalizedMethod, "Fill Contour", StringComparison.OrdinalIgnoreCase))
            {
                return FillObjectContoursOpenCv(source);
            }

            if (string.Equals(normalizedMethod, "Fill Hole", StringComparison.OrdinalIgnoreCase))
            {
                return FillObjectHolesOpenCv(source);
            }

            if (string.Equals(normalizedMethod, "Connect gap / bridge", StringComparison.OrdinalIgnoreCase))
            {
                return ConnectObjectGapsOpenCv(
                    source,
                    GetIntParameter(parameters, "GapSize", 3),
                    GetStringParameter(parameters, "Direction", "All"));
            }

            if (string.Equals(normalizedMethod, "Morphological Reconstruction", StringComparison.OrdinalIgnoreCase))
            {
                return ReconstructObjectMaskOpenCv(
                    source,
                    GetIntParameter(parameters, "KernelSize", 3),
                    GetStringParameter(parameters, "Shape", "Rect"));
            }

            if (string.Equals(normalizedMethod, "Merge by distance", StringComparison.OrdinalIgnoreCase))
            {
                return MergeObjectComponentsByDistanceOpenCv(
                    source, GetIntParameter(parameters, "Distance", 3));
            }

            if (string.Equals(normalizedMethod, "Convex Hull", StringComparison.OrdinalIgnoreCase))
            {
                return CreateObjectConvexHullOpenCv(source);
            }

            throw new InvalidOperationException("不支援的區塊處理方式：" + method);
        }

        private static Cv.MorphShapes GetObjectJudgementMorphShape(string shape)
        {
            if (string.Equals(shape, "Ellipse", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.MorphShapes.Ellipse;
            }

            if (string.Equals(shape, "Cross", StringComparison.OrdinalIgnoreCase))
            {
                return Cv.MorphShapes.Cross;
            }

            return Cv.MorphShapes.Rect;
        }

        private static Cv.Mat FillObjectContoursOpenCv(Cv.Mat source)
        {
            using (Cv.Mat contourInput = source.Clone())
            {
                Cv.Point[][] contours;
                Cv.HierarchyIndex[] hierarchy;
                Cv.Cv2.FindContours(
                    contourInput, out contours, out hierarchy,
                    Cv.RetrievalModes.External, Cv.ContourApproximationModes.ApproxSimple);
                var result = new Cv.Mat(
                    source.Rows, source.Cols, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                if (contours.Length > 0)
                {
                    Cv.Cv2.DrawContours(result, contours, -1, Cv.Scalar.White, -1);
                }

                return result;
            }
        }

        private static Cv.Mat FillObjectHolesOpenCv(Cv.Mat source)
        {
            using (var padded = new Cv.Mat())
            using (var floodMask = new Cv.Mat())
            using (var inverted = new Cv.Mat())
            using (var holes = new Cv.Mat())
            {
                Cv.Cv2.CopyMakeBorder(
                    source,
                    padded,
                    1,
                    1,
                    1,
                    1,
                    Cv.BorderTypes.Constant,
                    Cv.Scalar.Black);
                floodMask.Create(padded.Rows + 2, padded.Cols + 2, Cv.MatType.CV_8UC1);
                floodMask.SetTo(Cv.Scalar.All(0));
                Cv.Cv2.FloodFill(padded, floodMask, new Cv.Point(0, 0), Cv.Scalar.White);
                Cv.Cv2.BitwiseNot(padded, inverted);
                using (Cv.Mat holeView = new Cv.Mat(
                    inverted,
                    new Cv.Rect(1, 1, source.Width, source.Height)))
                {
                    holeView.CopyTo(holes);
                }

                var result = new Cv.Mat();
                Cv.Cv2.BitwiseOr(source, holes, result);
                return result;
            }
        }

        private static Cv.Mat ConnectObjectGapsOpenCv(Cv.Mat source, int gapSize, string direction)
        {
            int normalizedGap = Math.Max(1, Math.Min(999, gapSize));
            int length = Math.Min(1999, (normalizedGap * 2) + 1);
            Cv.Size kernelSize;
            if (string.Equals(direction, "Horizontal", StringComparison.OrdinalIgnoreCase))
            {
                kernelSize = new Cv.Size(length, 1);
            }
            else if (string.Equals(direction, "Vertical", StringComparison.OrdinalIgnoreCase))
            {
                kernelSize = new Cv.Size(1, length);
            }
            else
            {
                kernelSize = new Cv.Size(length, length);
            }

            using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                Cv.MorphShapes.Rect, kernelSize))
            {
                var result = new Cv.Mat();
                Cv.Cv2.MorphologyEx(source, result, Cv.MorphTypes.Close, kernel);
                return result;
            }
        }

        private static Cv.Mat ReconstructObjectMaskOpenCv(
            Cv.Mat source,
            int kernelSize,
            string shape)
        {
            int normalizedKernelSize = NormalizeObjectJudgementKernelSize(kernelSize);
            Cv.MorphShapes morphShape = GetObjectJudgementMorphShape(shape);
            using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                morphShape,
                new Cv.Size(normalizedKernelSize, normalizedKernelSize)))
            using (var marker = new Cv.Mat())
            {
                Cv.Cv2.Erode(source, marker, kernel);
                while (true)
                {
                    using (var dilated = new Cv.Mat())
                    using (var next = new Cv.Mat())
                    using (var difference = new Cv.Mat())
                    {
                        Cv.Cv2.Dilate(marker, dilated, kernel);
                        Cv.Cv2.BitwiseAnd(dilated, source, next);
                        Cv.Cv2.BitwiseXor(next, marker, difference);
                        bool unchanged = Cv.Cv2.CountNonZero(difference) == 0;
                        next.CopyTo(marker);
                        if (unchanged)
                        {
                            return marker.Clone();
                        }
                    }
                }
            }
        }

        private static Cv.Mat MergeObjectComponentsByDistanceOpenCv(Cv.Mat source, int distance)
        {
            int normalizedDistance = Math.Max(1, Math.Min(999, distance));
            int kernelSize = Math.Min(1999, (normalizedDistance * 2) + 1);
            using (Cv.Mat kernel = Cv.Cv2.GetStructuringElement(
                Cv.MorphShapes.Ellipse, new Cv.Size(kernelSize, kernelSize)))
            using (var expanded = new Cv.Mat())
            {
                Cv.Cv2.Dilate(source, expanded, kernel);
                using (Cv.Mat contourInput = expanded.Clone())
                {
                    Cv.Point[][] contours;
                    Cv.HierarchyIndex[] hierarchy;
                    Cv.Cv2.FindContours(
                        contourInput, out contours, out hierarchy,
                        Cv.RetrievalModes.External, Cv.ContourApproximationModes.ApproxSimple);
                    var result = new Cv.Mat(
                        source.Rows, source.Cols, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                    if (contours.Length > 0)
                    {
                        Cv.Cv2.DrawContours(result, contours, -1, Cv.Scalar.White, -1);
                    }

                    return result;
                }
            }
        }

        private static Cv.Mat CreateObjectConvexHullOpenCv(Cv.Mat source)
        {
            using (Cv.Mat contourInput = source.Clone())
            {
                Cv.Point[][] contours;
                Cv.HierarchyIndex[] hierarchy;
                Cv.Cv2.FindContours(
                    contourInput, out contours, out hierarchy,
                    Cv.RetrievalModes.External, Cv.ContourApproximationModes.ApproxSimple);
                var result = new Cv.Mat(
                    source.Rows, source.Cols, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                foreach (Cv.Point[] contour in contours)
                {
                    if (contour == null || contour.Length < 3)
                    {
                        continue;
                    }

                    Cv.Point[] hull = Cv.Cv2.ConvexHull(contour, true);
                    if (hull != null && hull.Length >= 3)
                    {
                        Cv.Cv2.FillPoly(result, new[] { hull }, Cv.Scalar.White);
                    }
                }

                return result;
            }
        }

        private void PaintLargeObjectJudgementOverlay(object sender, LargeImageOverlayPaintEventArgs e)
        {
            string selectedGroupId = activeObjectJudgementGroupId;
            if (objectJudgementProcessingRequested &&
                !string.IsNullOrEmpty(activeObjectJudgementGroupId) &&
                string.Equals(activeObjectJudgementGroupId, selectedGroupId, StringComparison.Ordinal))
            {
                List<ObjectJudgementSettings> objectJudgements =
                    GetObjectJudgementsInGroup(selectedGroupId);
                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roiRegion.Bounds);
                    if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
                    {
                        continue;
                    }

                    string maskKey = CreateObjectJudgementGroupMaskKey(
                        activeObjectJudgementGroupProcessingSignature,
                        roiRegion.Bounds);
                    Cv.Mat mask;
                    bool building;
                    lock (objectJudgementMaskLock)
                    {
                        objectJudgementGroupLargeMasks.TryGetValue(maskKey, out mask);
                        building = objectJudgementGroupLargeMaskBuildKeys.Contains(maskKey);
                    }

                    if (mask == null)
                    {
                        if (!building)
                        {
                            StartLargeObjectJudgementGroupProcessing(
                                selectedGroupId,
                                objectJudgements,
                                activeObjectJudgementGroupProcessingSignature);
                        }

                        continue;
                    }

                    var syntheticStep = new ImageProcessingStepSettings
                    {
                        Id = maskKey,
                        Method = "Object Judgement Group",
                        Parameters = objectJudgements.Count.ToString(CultureInfo.InvariantCulture)
                    };
                    PaintLargeProcessedBinaryViewportOverlay(
                        e,
                        roiRegion.Bounds,
                        visibleRoi,
                        syntheticStep,
                        maskKey,
                        mask);
                }

                return;
            }

            int objectIndex = systemParameters.ObjectJudgements.FindIndex(
                item => string.Equals(item.Id, activeObjectJudgementId, StringComparison.Ordinal));
            if (objectIndex < 0 || objectIndex >= systemParameters.ObjectJudgements.Count)
            {
                return;
            }

            ObjectJudgementSettings objectJudgement = systemParameters.ObjectJudgements[objectIndex];
            if (!objectJudgementProcessingRequested ||
                !string.Equals(activeObjectJudgementId, objectJudgement.Id, StringComparison.Ordinal))
            {
                return;
            }

            foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
            {
                Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roiRegion.Bounds);
                if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
                {
                    continue;
                }

                int processingIndex = activeObjectJudgementProcessingIndex;

                List<ObjectJudgementProcessingSettings> processingSteps =
                    GetObjectJudgementProcessingChain(objectJudgement, processingIndex);
                string maskKey = CreateObjectJudgementMaskKey(
                    objectJudgement,
                    processingSteps,
                    roiRegion.Bounds);
                Cv.Mat mask;
                bool building;
                lock (objectJudgementMaskLock)
                {
                    objectJudgementLargeMasks.TryGetValue(maskKey, out mask);
                    building = objectJudgementLargeMaskBuildKeys.Contains(maskKey);
                }

                if (mask == null)
                {
                    if (!building)
                    {
                        StartLargeObjectJudgementMaskBuild(
                            e.Source,
                            objectJudgement,
                            processingSteps,
                            roiRegion.Bounds,
                            objectJudgementMaskGeneration);
                    }

                    continue;
                }

                var syntheticStep = new ImageProcessingStepSettings
                {
                    Id = maskKey,
                    Method = "Object Judgement",
                    Parameters = objectJudgement.ProcessingSteps.Count.ToString(CultureInfo.InvariantCulture)
                };
                PaintLargeProcessedBinaryViewportOverlay(
                    e, roiRegion.Bounds, visibleRoi, syntheticStep, maskKey, mask);
            }
        }
    }
}
