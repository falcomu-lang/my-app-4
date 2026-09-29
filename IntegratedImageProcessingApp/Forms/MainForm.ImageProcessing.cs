using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private readonly ProcessedBinaryMaskCache processedBinaryMaskCache =
            new ProcessedBinaryMaskCache();

        private Cv.Mat CreateCombinedImageProcessingGroupMask(
            Cv.Mat source,
            Rectangle roi,
            IEnumerable<ImageProcessingStepSettings> steps,
            string sourceNamespace)
        {
            if (source == null)
            {
                throw new ArgumentNullException("source");
            }

            var orderedSteps = new List<ImageProcessingStepSettings>();
            foreach (ImageProcessingStepSettings step in steps ??
                new ImageProcessingStepSettings[0])
            {
                if (step != null && IsBinaryMaskProcessingMethod(step.Method))
                {
                    orderedSteps.Add(step);
                }
            }

            var combined = new Cv.Mat(
                source.Rows,
                source.Cols,
                Cv.MatType.CV_8UC1,
                Cv.Scalar.All(0));
            try
            {
                foreach (ImageProcessingStepSettings step in orderedSteps)
                {
                    // The cache owns its Mat. The helper returns a clone so
                    // the cache can be cleared safely while this task runs.
                    using (Cv.Mat next = GetOrCreateProcessedBinaryMask(
                        source,
                        roi,
                        step,
                        sourceNamespace))
                    {
                        Cv.Cv2.BitwiseOr(combined, next, combined);
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

        private Cv.Mat GetOrCreateProcessedBinaryMask(
            Cv.Mat source,
            Rectangle roi,
            ImageProcessingStepSettings step,
            string sourceNamespace)
        {
            if (source == null || step == null)
            {
                throw new ArgumentNullException(source == null ? "source" : "step");
            }

            string cacheKey = CreateProcessedBinaryMaskCacheKey(
                "step",
                roi,
                sourceNamespace,
                new[] { step });
            return processedBinaryMaskCache.GetOrCreateClone(
                cacheKey,
                () => CreateNativeLargeEdgeBinaryMask(
                    source,
                    step.Method,
                    ParseImageProcessingParameters(step.Parameters)));
        }

        private bool TryGetCachedProcessedBinaryMask(
            Rectangle roi,
            ImageProcessingStepSettings step,
            string sourceNamespace,
            out Cv.Mat mask)
        {
            mask = null;
            if (step == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return false;
            }

            string cacheKey = CreateProcessedBinaryMaskCacheKey(
                "step",
                roi,
                sourceNamespace,
                new[] { step });
            return processedBinaryMaskCache.TryGetClone(
                cacheKey,
                roi.Height,
                roi.Width,
                out mask);
        }

        private string CreateProcessedBinaryMaskCacheKey(
            string maskKind,
            Rectangle roi,
            string sourceNamespace,
            IEnumerable<ImageProcessingStepSettings> steps)
        {
            var parts = new List<string>
            {
                maskKind ?? string.Empty,
                imageSourceGeneration.ToString(System.Globalization.CultureInfo.InvariantCulture),
                systemParameters == null ? string.Empty : systemParameters.LastImagePath ?? string.Empty,
                sourceNamespace ?? string.Empty,
                roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture)
            };

            foreach (ImageProcessingStepSettings step in steps ??
                new ImageProcessingStepSettings[0])
            {
                if (step == null)
                {
                    continue;
                }

                parts.Add(step.Id ?? string.Empty);
                parts.Add(step.Method ?? string.Empty);
                parts.Add(step.Parameters ?? string.Empty);
            }

            return string.Join("|", parts.ToArray());
        }

        private void ClearProcessedBinaryMaskCache()
        {
            processedBinaryMaskCache.Clear();
        }

        private string CreateImageProcessingSourceNamespace()
        {
            return string.Join(
                "|",
                "image-processing",
                displayedImageRelationSourceType ?? activeImageRelationSourceType ?? "Original",
                displayedImageRelationSourceId ?? activeImageRelationSourceId ?? string.Empty);
        }

        private static string CreateImageRelationSourceNamespace(ImageRelationSettings relation)
        {
            if (relation == null)
            {
                return "relation|unknown";
            }

            return string.Join(
                "|",
                "relation",
                relation.Id ?? string.Empty,
                relation.SourceType ?? "Original",
                relation.SourceId ?? string.Empty);
        }

        private void ProcessImageProcessingStep(string stepText)
        {
            objectDefinitionDependencyPreviewRequested = false;
            int stepIndex = GetImageProcessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                statusLabel.Text = "影像處理項目不存在，請重新選擇影像處理項目";
                return;
            }

            selectedImageProcessingStepIndex = stepIndex;
            selectedImageProcessingGroupId = null;
            activeImageRelationGroupId = null;
            displayedImageProcessingStepIndex = stepIndex;
            displayedImageProcessingGroupId = null;
            displayedImageRelationGroupId = null;
            displayedImageRelationSourceType = string.IsNullOrWhiteSpace(activeImageRelationSourceType)
                ? "Original"
                : activeImageRelationSourceType;
            displayedImageRelationSourceId = activeImageRelationSourceId;
            imageProcessingExecutionRequested = true;
            explicitProcessedImageUpdateRequested = true;
            bool hasCachedResult = HasCachedProcessedImageForCurrentSelection();
            if (hasCachedResult)
            {
                SetParameterApplyStatus("已處理，使用快取結果");
            }
            else
            {
                BeginParameterApplyStatus(false);
            }
            MarkProcessedPreviewDirty();
            // Selecting "處理" again must reuse the matching result.  A
            // parameter/image/ROI change still performs the full invalidation
            // through the default MarkProcessedImageDirty() path elsewhere.
            MarkProcessedImageDirty(false);
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = hasCachedResult
                ? "已處理" + stepText.Trim() + "，使用快取結果"
                : "已開始處理" + stepText.Trim();
        }

        private void ProcessImageProcessingGroup(string groupId)
        {
            objectDefinitionDependencyPreviewRequested = false;
            ImageProcessingGroupSettings group = FindImageProcessingGroup(groupId);
            if (group == null)
            {
                statusLabel.Text = "影像處理群組不存在，請重新選擇影像處理群組";
                return;
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = group.Id;
            activeImageRelationGroupId = null;
            displayedImageProcessingStepIndex = -1;
            displayedImageProcessingGroupId = group.Id;
            displayedImageRelationGroupId = null;
            displayedImageRelationSourceType = string.IsNullOrWhiteSpace(activeImageRelationSourceType)
                ? "Original"
                : activeImageRelationSourceType;
            displayedImageRelationSourceId = activeImageRelationSourceId;
            imageProcessingExecutionRequested = true;
            explicitProcessedImageUpdateRequested = true;
            bool hasCachedResult = HasCachedProcessedImageForCurrentSelection();
            if (hasCachedResult)
            {
                SetParameterApplyStatus("已處理，使用快取結果");
            }
            else
            {
                BeginParameterApplyStatus(false);
            }
            MarkProcessedPreviewDirty();
            // A group has its own cache key. Keep existing step/group masks so
            // returning to an unchanged group can reuse them immediately.
            MarkProcessedImageDirty(false);
            RequestExplicitProcessedImageUpdate();
            statusLabel.Text = hasCachedResult
                ? "已處理" + group.DisplayName + "，使用快取結果"
                : "已開始處理" + group.DisplayName;
        }
        private void ShowImageProcessingFlowTree(string stepText)
        {
            selectedImageProcessingGroupId = null;
            selectedImageProcessingStepIndex = GetImageProcessingStepIndex(stepText);
            if (selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                HideImageProcessingFlowTree();
                return;
            }

            EnsureImageProcessingFlowTreeView();
            parameterPlaceholderLabel.Visible = false;

            string method = systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method;
            if (IsBinaryMaskProcessingMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 參數";
            }
            else
            {
                HideImageProcessingParameterPanel();
                imageProcessingFlowTreeView.Visible = true;
                imageProcessingFlowTreeView.BringToFront();
                SelectImageProcessingFlowNode(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 流程";
            }

            statusLabel.Text = "目前選擇：" + stepText.Trim();
        }

        private void ShowImageProcessingGroup(string groupText)
        {
            string groupId = GetImageProcessingGroupId(groupText);
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = groupId;
            HideImageProcessingParameterPanel();
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }

            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text = "群組結果會疊加顯示群組內每一個處理對原圖的結果。";
            parameterPlaceholderLabel.BringToFront();
            rightPanelTitleLabel.Text = groupText.Trim() + " 結果";
            statusLabel.Text = "目前選擇：" + groupText.Trim();
        }

        private void HideImageProcessingFlowTree()
        {
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            selectedImagePreprocessingStepIndex = -1;
            selectedImagePreprocessingGroupId = null;
            parameterPlaceholderLabel.Visible = true;
            if (imageProcessingFlowTreeView != null)
            {
                imageProcessingFlowTreeView.Visible = false;
            }

            if (imagePreprocessingFlowTreeView != null)
            {
                imagePreprocessingFlowTreeView.Visible = false;
            }

            HideImageProcessingParameterPanel();
        }

        private void EnsureImageProcessingFlowTreeView()
        {
            if (imageProcessingFlowTreeView != null)
            {
                return;
            }

            imageProcessingFlowTreeView = new TreeView();
            imageProcessingFlowTreeView.BorderStyle = BorderStyle.None;
            imageProcessingFlowTreeView.Dock = DockStyle.Fill;
            imageProcessingFlowTreeView.FullRowSelect = true;
            imageProcessingFlowTreeView.HideSelection = false;
            imageProcessingFlowTreeView.BackColor = parameterPanel.BackColor;
            imageProcessingFlowTreeView.ForeColor = Color.FromArgb(39, 46, 56);
            imageProcessingFlowTreeView.AfterSelect += ImageProcessingFlowTreeView_AfterSelect;
            parameterPanel.Controls.Add(imageProcessingFlowTreeView);
            imageProcessingFlowTreeView.BringToFront();

            AddImageProcessingFlowNode("Edge Detection", "Polarity Edge", "Canny Edge", "Sobel Edge");
            AddImageProcessingFlowNode("Threshold", "Global Threshold", "Adaptive Threshold", "Otsu Threshold");
            imageProcessingFlowTreeView.ExpandAll();
        }

        private void AddImageProcessingFlowNode(string category, params string[] methods)
        {
            TreeNode categoryNode = imageProcessingFlowTreeView.Nodes.Add(category);
            categoryNode.Tag = category;
            foreach (string method in methods)
            {
                TreeNode methodNode = categoryNode.Nodes.Add(method);
                methodNode.Tag = method;
            }
        }

        private void SelectImageProcessingFlowNode(string method)
        {
            isUpdatingImageProcessingFlowTree = true;
            try
            {
                imageProcessingFlowTreeView.SelectedNode = FindImageProcessingFlowNode(method);
            }
            finally
            {
                isUpdatingImageProcessingFlowTree = false;
            }
        }

        private TreeNode FindImageProcessingFlowNode(string method)
        {
            if (string.IsNullOrWhiteSpace(method))
            {
                return null;
            }

            foreach (TreeNode node in imageProcessingFlowTreeView.Nodes)
            {
                TreeNode foundNode = FindImageProcessingFlowNode(node, method);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        private static TreeNode FindImageProcessingFlowNode(TreeNode node, string method)
        {
            if (string.Equals(node.Tag as string, method, StringComparison.Ordinal))
            {
                return node;
            }

            foreach (TreeNode childNode in node.Nodes)
            {
                TreeNode foundNode = FindImageProcessingFlowNode(childNode, method);
                if (foundNode != null)
                {
                    return foundNode;
                }
            }

            return null;
        }

        private void ImageProcessingFlowTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (isUpdatingImageProcessingFlowTree ||
                selectedImageProcessingStepIndex < 0 ||
                selectedImageProcessingStepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            string method = e.Node.Tag as string;
            if (e.Node.Nodes.Count > 0)
            {
                return;
            }

            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method = method;
            systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Parameters = CreateDefaultImageProcessingParameters(method);
            SaveSystemParameters();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            UpdateVisibleImageProcessingStepText(selectedImageProcessingStepIndex);
            if (IsBinaryMaskProcessingMethod(method))
            {
                ShowImageProcessingParameterPanel(method);
                rightPanelTitleLabel.Text = "處理" + (selectedImageProcessingStepIndex + 1) + "(" + method + ") 參數";
            }

            statusLabel.Text = "已設定處理" + (selectedImageProcessingStepIndex + 1) + "：" + method;
        }

        private void UpdateVisibleImageProcessingStepText(int stepIndex)
        {
            RefreshVisibleImageProcessingStepText(stepIndex, true);
        }

        private void RefreshVisibleImageProcessingStepText(int stepIndex)
        {
            RefreshVisibleImageProcessingStepText(stepIndex, false);
        }

        private void RefreshVisibleImageProcessingStepText(int stepIndex, bool restoreSelection)
        {
            if (stepIndex < 0 || stepIndex >= systemParameters.ImageProcessingSteps.Count)
            {
                return;
            }

            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                string itemText = functionListBox.Items[itemIndex] as string;
                if (GetImageProcessingStepIndex(itemText) == stepIndex)
                {
                    int leadingSpaces = itemText.Length - itemText.TrimStart().Length;
                    int depth = Math.Max(0, (leadingSpaces - 4) / 2);
                    ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps[stepIndex];
                    visibleImageProcessingStepIds.Remove(itemText);
                    string updatedText = RegisterVisibleImageProcessingStep(step, stepIndex + 1, depth);
                    functionListBox.Items[itemIndex] = updatedText;
                    if (restoreSelection)
                    {
                        functionListBox.SelectedIndex = itemIndex;
                    }

                    break;
                }
            }
        }

        private void ShowImageProcessingParameterPanel(string method)
        {
            EnsureImageProcessingParameterPanel();
            pendingImageProcessingParameters = ParseImageProcessingParameters(GetSelectedImageProcessingStepParameters());
            imageProcessingFlowTreeView.Visible = false;
            parameterPlaceholderLabel.Visible = false;
            imageProcessingParameterPanel.Visible = true;
            imageProcessingParameterPanel.Controls.Clear();
            imageProcessingParameterPanel.BringToFront();

            isLoadingImageProcessingParameters = true;
            try
            {
                AddParameterHeader(method);
                AddReselectMethodButton();

                if (method == "Polarity Edge")
                {
                    AddComboParameter("Polarity", "邊緣方向", new[] { "BrightToDark", "DarkToBright", "Any" }, "Any");
                    AddNumericParameter("ContrastThreshold", "對比門檻", "20");
                    AddComboParameter("CoreWidth", "核心大小", new[] { "3", "5", "7" }, "3");
                    AddNumericParameter("Smoothing", "平滑", "1");
                    AddNumericParameter("GaussianSigma", "Gaussian Sigma", "0.5");
                    AddComboParameter("SearchDirection", "灰階變化方向", new[] { "X", "Y", "Any" }, "Any");
                    AddComboParameter("BorderType", "ROI 邊界處理", new[] { "Reflect", "Replicate", "Constant" }, "Reflect");
                }
                else if (method == "Canny Edge")
                {
                    AddNumericParameter("LowThreshold", "低門檻", "50");
                    AddNumericParameter("HighThreshold", "高門檻", "150");
                    AddComboParameter("KernelSize", "核心大小", CannyKernelSizeOptions, "3");
                    AddCheckParameter("L2Gradient", "L2 梯度", false);
                    AddComboParameter("GaussianBlurSize", "高斯模糊大小", KernelSizeOptions, "5");
                    AddNumericParameter("GaussianSigma", "高斯 Sigma", "1.4");
                }
                else if (method == "Sobel Edge")
                {
                    AddComboParameter("Direction", "灰階變化方向", new[] { "X", "Y", "Any" }, "Any");
                    AddComboParameter("KernelSize", "核心大小", SobelKernelSizeOptions, "3");
                    AddNumericParameter("Threshold", "邊緣門檻", "30");
                }
                else if (method == "Global Threshold")
                {
                    AddComboParameter("ThresholdMode", "門檻模式", new[] { "Single", "Range" }, "Single");
                    AddNumericParameter("Threshold", "門檻值", "128");
                    AddNumericParameter("LowerThreshold", "範圍下限", "0");
                    AddNumericParameter("UpperThreshold", "範圍上限", "255");
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                    SetGlobalThresholdParameterVisibility(
                        GetImageProcessingParameterValue("ThresholdMode", "Single"));
                }
                else if (method == "Adaptive Threshold")
                {
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("AdaptiveMethod", "自適應方法", new[] { "MeanC", "GaussianC" }, "GaussianC");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                    AddNumericParameter("BlockSize", "區塊大小", "11");
                    AddNumericParameter("C", "常數 C", "2");
                }
                else if (method == "Otsu Threshold")
                {
                    AddNumericParameter("MaxValue", "輸出最大值", "255");
                    AddComboParameter("ThresholdType", "二值化方向", new[] { "Binary", "BinaryInv" }, "Binary");
                }

                AddParameterEditButtons(false);
            }
            finally
            {
                isLoadingImageProcessingParameters = false;
            }
        }

        private void EnsureImageProcessingParameterPanel()
        {
            if (imageProcessingParameterPanel != null)
            {
                return;
            }

            imageProcessingParameterPanel = new FlowLayoutPanel();
            imageProcessingParameterPanel.Dock = DockStyle.Fill;
            imageProcessingParameterPanel.FlowDirection = FlowDirection.TopDown;
            imageProcessingParameterPanel.WrapContents = false;
            imageProcessingParameterPanel.AutoScroll = true;
            imageProcessingParameterPanel.BackColor = parameterPanel.BackColor;
            parameterPanel.Controls.Add(imageProcessingParameterPanel);
            imageProcessingParameterPanel.BringToFront();
        }

        private void HideImageProcessingParameterPanel()
        {
            if (imageProcessingParameterPanel != null)
            {
                imageProcessingParameterPanel.Visible = false;
            }
        }

        private void AddParameterHeader(string method)
        {
            var label = new Label();
            label.AutoSize = false;
            label.Width = parameterPanel.Width - 12;
            label.Height = 32;
            label.Font = new Font(Font, FontStyle.Bold);
            label.Text = method;
            imageProcessingParameterPanel.Controls.Add(label);
        }

        private void AddReselectMethodButton()
        {
            var button = new Button();
            button.Width = parameterPanel.Width - 18;
            button.Height = 30;
            button.Text = "重新選擇方法";
            button.Click += delegate
            {
                HideImageProcessingParameterPanel();
                imageProcessingFlowTreeView.Visible = true;
                rightPanelTitleLabel.Text = "處理" + (selectedImageProcessingStepIndex + 1) + " 流程";
            };
            imageProcessingParameterPanel.Controls.Add(button);
        }

        private void AddNumericParameter(string key, string labelText, string defaultValue)
        {
            decimal defaultNumber = ParseDecimalOrDefault(defaultValue, 0);
            decimal value = ParseDecimalOrDefault(GetImageProcessingParameterValue(key, defaultValue), defaultNumber);
            decimal minimum;
            decimal maximum;
            decimal increment;
            GetNumericParameterRange(key, out minimum, out maximum, out increment);

            var label = CreateParameterLabel(labelText);
            label.Tag = key;
            var numericUpDown = new NumericUpDown();
            numericUpDown.Width = parameterPanel.Width - 18;
            numericUpDown.Minimum = minimum;
            numericUpDown.Maximum = maximum;
            numericUpDown.Increment = increment;
            numericUpDown.DecimalPlaces = key == "Scale" || key == "GaussianSigma" || key == "C" ? 2 : 0;
            numericUpDown.Value = Clamp(value, minimum, maximum);
            numericUpDown.Tag = key;
            numericUpDown.ValueChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    var control = (NumericUpDown)sender;
                    SetPendingImageProcessingParameter(control.Tag as string, control.Value.ToString(CultureInfo.InvariantCulture));
                }
            };
            numericUpDown.MouseWheel += NumericParameter_MouseWheel;

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(numericUpDown);
        }

        private void AddComboParameter(string key, string labelText, string[] values, string defaultValue)
        {
            var label = CreateParameterLabel(labelText);
            label.Tag = key;
            var comboBox = new ComboBox();
            comboBox.Width = parameterPanel.Width - 18;
            comboBox.DropDownStyle = ComboBoxStyle.DropDownList;
            comboBox.Items.AddRange(values);
            comboBox.Tag = key;
            comboBox.SelectedItem = GetImageProcessingParameterValue(key, defaultValue);
            if (comboBox.SelectedIndex < 0)
            {
                comboBox.SelectedItem = defaultValue;
            }

            comboBox.SelectedIndexChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    SetPendingImageProcessingParameter(((ComboBox)sender).Tag as string, ((ComboBox)sender).SelectedItem as string);
                }
            };

            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(comboBox);

            if (key == "ThresholdMode")
            {
                comboBox.SelectedIndexChanged += delegate(object sender, EventArgs e)
                {
                    if (!isLoadingImageProcessingParameters)
                    {
                        SetGlobalThresholdParameterVisibility(
                            ((ComboBox)sender).SelectedItem as string);
                    }
                };
            }
        }

        private void SetGlobalThresholdParameterVisibility(string mode)
        {
            bool isRange = string.Equals(mode, "Range", StringComparison.OrdinalIgnoreCase);
            if (imageProcessingParameterPanel == null)
            {
                return;
            }

            foreach (Control control in imageProcessingParameterPanel.Controls)
            {
                string key = control.Tag as string;
                if (key == "Threshold")
                {
                    control.Visible = !isRange;
                }
                else if (key == "LowerThreshold" || key == "UpperThreshold")
                {
                    control.Visible = isRange;
                }
                else if (key == "ThresholdType")
                {
                    control.Visible = !isRange;
                }
            }
        }

        private string RegisterVisibleImageProcessingStep(
            ImageProcessingStepSettings step,
            int stepNumber,
            int depth)
        {
            string text = CreateImageProcessingStepText(stepNumber, depth);
            if (step != null && !string.IsNullOrWhiteSpace(step.Id))
            {
                visibleImageProcessingStepIds[text] = step.Id;
            }

            return text;
        }

        private void AddCheckParameter(string key, string labelText, bool defaultValue)
        {
            var checkBox = new CheckBox();
            checkBox.Width = parameterPanel.Width - 18;
            checkBox.Height = 28;
            checkBox.Text = labelText;
            checkBox.Tag = key;
            checkBox.Checked = string.Equals(GetImageProcessingParameterValue(key, defaultValue ? "true" : "false"), "true", StringComparison.OrdinalIgnoreCase);
            checkBox.CheckedChanged += delegate(object sender, EventArgs e)
            {
                if (!isLoadingImageProcessingParameters)
                {
                    SetPendingImageProcessingParameter(((CheckBox)sender).Tag as string, ((CheckBox)sender).Checked ? "true" : "false");
                }
            };
            imageProcessingParameterPanel.Controls.Add(checkBox);
        }

        private void AddParameterEditButtons(bool preprocessing)
        {
            var apply = new Button();
            apply.Width = parameterPanel.Width - 18;
            apply.Height = 30;
            apply.Text = "套用";
            apply.Margin = new Padding(3, 12, 3, 0);
            apply.Click += delegate
            {
                if (preprocessing)
                {
                    ApplyPendingImagePreprocessingParameters();
                }
                else
                {
                    ApplyPendingImageProcessingParameters();
                }
            };
            imageProcessingParameterPanel.Controls.Add(apply);

            var cancel = new Button();
            cancel.Width = parameterPanel.Width - 18;
            cancel.Height = 30;
            cancel.Text = "取消";
            cancel.Click += delegate
            {
                if (preprocessing)
                {
                    ImageProcessingStepSettings step;
                    if (TryGetSelectedImagePreprocessingStep(out step))
                    {
                        ShowImagePreprocessingParameterPanel(step.Method);
                    }
                    else
                    {
                        HideImageProcessingParameterPanel();
                    }
                }
                else
                {
                    ShowImageProcessingParameterPanel(
                        systemParameters.ImageProcessingSteps[selectedImageProcessingStepIndex].Method);
                }
            };
            imageProcessingParameterPanel.Controls.Add(cancel);

            parameterApplyStatusLabel = new Label();
            parameterApplyStatusLabel.Width = parameterPanel.Width - 18;
            parameterApplyStatusLabel.AutoSize = false;
            parameterApplyStatusLabel.Height = 52;
            parameterApplyStatusLabel.Margin = new Padding(3, 8, 3, 0);
            parameterApplyStatusLabel.Text = string.Empty;
            parameterApplyStatusLabel.ForeColor = Color.FromArgb(55, 64, 76);
            imageProcessingParameterPanel.Controls.Add(parameterApplyStatusLabel);
        }


        private Label CreateParameterLabel(string text)
        {
            var label = new Label();
            label.AutoSize = false;
            label.Width = parameterPanel.Width - 18;
            label.Height = 22;
            label.Margin = new Padding(3, 8, 3, 0);
            label.Text = text;
            return label;
        }

        private void NumericParameter_MouseWheel(object sender, MouseEventArgs e)
        {
            var numericUpDown = sender as NumericUpDown;
            if (numericUpDown == null)
            {
                return;
            }

            ((HandledMouseEventArgs)e).Handled = true;
            decimal multiplier = (ModifierKeys & Keys.Shift) == Keys.Shift ? 10 : 1;
            decimal delta = e.Delta > 0 ? numericUpDown.Increment * multiplier : -numericUpDown.Increment * multiplier;
            numericUpDown.Value = Clamp(numericUpDown.Value + delta, numericUpDown.Minimum, numericUpDown.Maximum);
        }

        private static void GetNumericParameterRange(string key, out decimal minimum, out decimal maximum, out decimal increment)
        {
            minimum = 0;
            maximum = 99999;
            increment = 1;

            if (key == "ContrastThreshold" || key == "LowThreshold" || key == "HighThreshold" || key == "Threshold")
            {
                maximum = 255;
            }
            else if (key == "CoreWidth")
            {
                minimum = 1;
                maximum = 99;
            }
            else if (key == "Smoothing")
            {
                maximum = 31;
            }
            else if (key == "MaxGap")
            {
                maximum = 999;
            }
            else if (key == "Scale")
            {
                maximum = 100;
                increment = 0.1M;
            }
            else if (key == "GaussianSigma")
            {
                maximum = 100;
                increment = 0.1M;
            }
            else if (key == "MaxValue")
            {
                minimum = 1;
                maximum = 255;
            }
            else if (key == "BlockSize")
            {
                minimum = 3;
                maximum = 999;
            }
            else if (key == "C")
            {
                minimum = -255;
                maximum = 255;
                increment = 0.1M;
            }
            else if (key == "Delta")
            {
                minimum = -255;
                maximum = 255;
            }
        }

        private static decimal ParseDecimalOrDefault(string value, decimal defaultValue)
        {
            decimal parsedValue;
            return decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out parsedValue)
                ? parsedValue
                : defaultValue;
        }

        private static decimal Clamp(decimal value, decimal minimum, decimal maximum)
        {
            if (value < minimum)
            {
                return minimum;
            }

            if (value > maximum)
            {
                return maximum;
            }

            return value;
        }


    }
}
