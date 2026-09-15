using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
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
        private static Cv.Mat CreateOpenCvGrayMat(byte[,] gray)
        {
            int width = gray.GetLength(0);
            int height = gray.GetLength(1);
            byte[] pixels = new byte[checked(width * height)];
            Parallel.For(0, height, delegate(int y)
            {
                int rowOffset = y * width;
                for (int x = 0; x < width; x++) pixels[rowOffset + x] = gray[x, y];
            });
            var result = new Cv.Mat(height, width, Cv.MatType.CV_8UC1);
            Marshal.Copy(pixels, 0, result.Data, pixels.Length);
            return result;
        }

        private static Cv.Mat CreateOpenCvGrayMat(Bitmap bitmap)
        {
            Rectangle bounds = new Rectangle(0, 0, bitmap.Width, bitmap.Height);
            BitmapData data = null;
            try
            {
                data = bitmap.LockBits(bounds, ImageLockMode.ReadOnly, bitmap.PixelFormat);
                byte[] gray = new byte[checked(bitmap.Width * bitmap.Height)];
                int bytesPerPixel = Image.GetPixelFormatSize(bitmap.PixelFormat) / 8;
                if (bitmap.PixelFormat == PixelFormat.Format8bppIndexed)
                {
                    for (int y = 0; y < bitmap.Height; y++)
                        Marshal.Copy(data.Scan0 + (y * data.Stride), gray, y * bitmap.Width, bitmap.Width);
                }
                else if (bytesPerPixel >= 3)
                {
                    byte[] row = new byte[Math.Abs(data.Stride)];
                    for (int y = 0; y < bitmap.Height; y++)
                    {
                        Marshal.Copy(data.Scan0 + (y * data.Stride), row, 0, row.Length);
                        int targetOffset = y * bitmap.Width;
                        for (int x = 0; x < bitmap.Width; x++)
                        {
                            int sourceOffset = x * bytesPerPixel;
                            byte b = row[sourceOffset];
                            byte g = row[sourceOffset + 1];
                            byte r = row[sourceOffset + 2];
                            gray[targetOffset + x] = (byte)((r * 299 + g * 587 + b * 114 + 500) / 1000);
                        }
                    }
                }
                else throw new InvalidOperationException("不支援的影像像素格式：" + bitmap.PixelFormat);
                var result = new Cv.Mat(bitmap.Height, bitmap.Width, Cv.MatType.CV_8UC1);
                Marshal.Copy(gray, 0, result.Data, gray.Length);
                return result;
            }
            finally
            {
                if (data != null) bitmap.UnlockBits(data);
            }
        }

        private ImageDisplayControl leftPreprocessedDisplayControl;
        private ImageDisplayControl rightPreprocessedDisplayControl;
        private bool imagePreprocessingMenuExpanded;
        private bool preprocessingExecutionRequested;
        private int selectedImagePreprocessingStepIndex = -1;
        private string selectedImagePreprocessingGroupId;
        private TreeView imagePreprocessingFlowTreeView;
        private readonly HashSet<string> expandedImagePreprocessingGroupIds = new HashSet<string>(StringComparer.Ordinal);
        private readonly Dictionary<ImageProcessingStepSettings, long> imagePreprocessingStepElapsedMilliseconds =
            new Dictionary<ImageProcessingStepSettings, long>();
        private readonly object largePreprocessedImageLock = new object();
        private LargeImageSource largePreprocessedImageSource;
        private Bitmap latestPreprocessedImage;
        private bool preprocessedImageDirty = true;
        private bool isPreparingPreprocessedImage;
        private int preprocessedImageGeneration;
        private Dictionary<string, string> pendingImagePreprocessingParameters;
        private long lastPreprocessingElapsedMilliseconds;
        private bool hasPreprocessedImageViewState;
        private ImageViewState preprocessedImageViewState;
        private const string ImagePreprocessingMenuText = "影像前處理";
        private const string OriginalPreprocessingSourceText = "    原始影像";

        private sealed class PreprocessedImageResult
        {
            public Cv.Mat Image { get; set; }
            public Dictionary<ImageProcessingStepSettings, long> StepElapsedMilliseconds { get; set; }
        }

        private sealed class PreprocessedBitmapResult
        {
            public Bitmap Image { get; set; }
            public Dictionary<ImageProcessingStepSettings, long> StepElapsedMilliseconds { get; set; }
        }

        private async void RequestPreprocessedImageUpdateCore()
        {
            if (!preprocessingExecutionRequested || isPreparingPreprocessedImage || !preprocessedImageDirty ||
                !HasConfiguredImagePreprocessingSteps() || string.IsNullOrWhiteSpace(systemParameters.LastImagePath) ||
                !File.Exists(systemParameters.LastImagePath)) return;

            isPreparingPreprocessedImage = true;
            int generation = preprocessedImageGeneration;
            int sourceGeneration = imageSourceGeneration;
            string sourceFilePath = systemParameters.LastImagePath;
            try
            {
                statusLabel.Text = "影像前處理運算中...使用 OpenCV";
                if (rightOriginalDisplayControl.IsLargeImageMode)
                {
                    LargeImageSource originalSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                    if (originalSource == null) return;
                    try
                    {
                        PreprocessedImageResult preprocessingResult = await Task.Run(delegate
                        {
                            using (Cv.Mat source = GetOrCreateLargeRoiOpenCvGrayCache(originalSource,
                                new Rectangle(0, 0, originalSource.Width, originalSource.Height)))
                                return CreateOpenCvPreprocessedImage(source,
                                    activeImageRelationSourceType == "Step" ? activeImageRelationSourceId : null);
                        });
                        Cv.Mat preprocessed = preprocessingResult.Image;
                        if (generation != preprocessedImageGeneration || sourceGeneration != imageSourceGeneration)
                        {
                            preprocessed.Dispose();
                            return;
                        }
                        RecordImagePreprocessingStepElapsed(preprocessingResult.StepElapsedMilliseconds);
                        var preprocessedSource = new LargeImageSource(preprocessed);
                        lock (largePreprocessedImageLock)
                        {
                            if (generation != preprocessedImageGeneration || sourceGeneration != imageSourceGeneration)
                            {
                                preprocessedSource.ReleaseReference();
                                return;
                            }
                            largePreprocessedImageSource = preprocessedSource;
                        }
                        isSyncingImageView = true;
                        try
                        {
                            Stopwatch previewStopwatch = Stopwatch.StartNew();
                            leftPreprocessedDisplayControl.SetSharedLargeImageSource(preprocessedSource);
                            rightPreprocessedDisplayControl.SetSharedLargeImageSource(preprocessedSource);
                            lastDisplayProcessingElapsedMilliseconds = Math.Max(1, previewStopwatch.ElapsedMilliseconds);
                        }
                        finally { isSyncingImageView = false; }
                        RestorePreprocessedImageViewState();
                    }
                    finally { originalSource.ReleaseReference(); }
                }
                else
                {
                    PreprocessedBitmapResult preprocessingResult = await Task.Run(
                        () => CreateCurrentPreprocessedBitmap(sourceFilePath));
                    Bitmap preprocessed = preprocessingResult.Image;
                    if (generation != preprocessedImageGeneration || sourceGeneration != imageSourceGeneration)
                    {
                        if (preprocessed != null) preprocessed.Dispose();
                        return;
                    }
                    if (latestPreprocessedImage != null) latestPreprocessedImage.Dispose();
                    latestPreprocessedImage = preprocessed;
                    RecordImagePreprocessingStepElapsed(preprocessingResult.StepElapsedMilliseconds);
                    if (preprocessed != null)
                    {
                        isSyncingImageView = true;
                        try
                        {
                            Stopwatch previewStopwatch = Stopwatch.StartNew();
                            leftPreprocessedDisplayControl.SetDisplayImage(new Bitmap(preprocessed), true);
                            rightPreprocessedDisplayControl.SetDisplayImage(new Bitmap(preprocessed), true);
                            lastDisplayProcessingElapsedMilliseconds = Math.Max(1, previewStopwatch.ElapsedMilliseconds);
                        }
                        finally { isSyncingImageView = false; }
                        RestorePreprocessedImageViewState();
                    }
                }
                preprocessedImageDirty = false;
                statusLabel.Text = "影像前處理完成";
                CompleteParameterApplyStatus();
                if (HasSelectedPreviewableImageProcessingSteps()) ScheduleProcessedImageUpdateIfVisible();
            }
            catch (Exception ex)
            {
                Debug.WriteLine("OpenCV preprocessing failed: " + ex);
                statusLabel.Text = "影像前處理失敗";
            }
            finally
            {
                isPreparingPreprocessedImage = false;
                if (preprocessedImageDirty &&
                    (generation != preprocessedImageGeneration || sourceGeneration != imageSourceGeneration))
                    RequestPreprocessedImageUpdate();
            }
        }

        private static string CreateDefaultImagePreprocessingParameters(string method)
        {
            if (method == "Normalize") return "Alpha=0;Beta=255";
            if (method == "Gaussian Blur") return "KernelSize=5;Sigma=1.4";
            if (method == "CLAHE") return "ClipLimit=2.0;TileGridSize=8";
            if (method == "Median Blur") return "KernelSize=5";
            if (method == "Sharpen") return "Amount=1.0;KernelSize=3;Sigma=0";
            if (method == "Bilateral Filter") return "Diameter=5;SigmaColor=50;SigmaSpace=50";
            return string.Empty;
        }

        private static bool IsImagePreprocessingMethod(string method)
        {
            return method == "Normalize" || method == "Gaussian Blur" || method == "CLAHE" ||
                method == "Median Blur" || method == "Sharpen" || method == "Bilateral Filter";
        }

        private bool HasConfiguredImagePreprocessingSteps()
        {
            return systemParameters.ImagePreprocessingSteps.Any(step =>
                step != null && IsImagePreprocessingMethod(step.Method));
        }

        private List<ImageProcessingStepSettings> GetOrderedImagePreprocessingSteps()
        {
            var steps = new List<ImageProcessingStepSettings>();
            if (selectedImagePreprocessingStepIndex >= 0 &&
                selectedImagePreprocessingStepIndex < systemParameters.ImagePreprocessingSteps.Count)
            {
                ImageProcessingStepSettings selectedStep = systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex];
                if (IsImagePreprocessingMethod(selectedStep.Method)) steps.Add(selectedStep);
                return steps;
            }
            if (!string.IsNullOrEmpty(selectedImagePreprocessingGroupId))
            {
                CollectImagePreprocessingGroupSteps(selectedImagePreprocessingGroupId, steps);
                return steps;
            }
            foreach (ImageProcessingStepSettings step in systemParameters.ImagePreprocessingSteps)
                if (string.IsNullOrWhiteSpace(step.GroupId)) steps.Add(step);
            foreach (ImageProcessingGroupSettings group in systemParameters.ImagePreprocessingGroups)
                if (string.IsNullOrWhiteSpace(group.ParentGroupId)) CollectImagePreprocessingGroupSteps(group.Id, steps);
            return steps;
        }

        private void CollectImagePreprocessingGroupSteps(string groupId, List<ImageProcessingStepSettings> steps)
        {
            foreach (ImageProcessingStepSettings step in systemParameters.ImagePreprocessingSteps)
                if (string.Equals(step.GroupId, groupId, StringComparison.Ordinal)) steps.Add(step);
            foreach (ImageProcessingGroupSettings child in systemParameters.ImagePreprocessingGroups)
                if (string.Equals(child.ParentGroupId, groupId, StringComparison.Ordinal))
                    CollectImagePreprocessingGroupSteps(child.Id, steps);
        }

        private List<int> GetSelectedImagePreprocessingStepIndexes()
        {
            var indexes = new List<int>();
            foreach (object item in functionListBox.SelectedItems)
            {
                int index = GetImagePreprocessingStepIndex(item as string);
                if (index >= 0 && !indexes.Contains(index)) indexes.Add(index);
            }
            indexes.Sort();
            return indexes;
        }

        private void ShowImagePreprocessingMultiSelectContextMenu(List<int> stepIndexes, Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("分組", null, delegate { CreateImagePreprocessingGroup(stepIndexes); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu)) imageProcessingStepContextMenu = null;
            };
            menu.Show(functionListBox, location);
        }

        private ImageProcessingGroupSettings FindImagePreprocessingGroup(string groupId)
        {
            return systemParameters.ImagePreprocessingGroups.FirstOrDefault(group =>
                string.Equals(group.Id, groupId, StringComparison.Ordinal));
        }

        private void ToggleImagePreprocessingGroup(string groupText)
        {
            string groupId = GetImagePreprocessingGroupId(groupText);
            if (string.IsNullOrWhiteSpace(groupId)) return;
            if (!expandedImagePreprocessingGroupIds.Remove(groupId)) expandedImagePreprocessingGroupIds.Add(groupId);
            RebuildVisibleImagePreprocessingSteps();
            functionListBox.SelectedItem = groupText;
        }

        private void ShowImagePreprocessingGroupContextMenu(string groupId, Point location)
        {
            ImageProcessingGroupSettings group = FindImagePreprocessingGroup(groupId);
            if (group == null) return;
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate { ProcessImagePreprocessingGroup(group.Id); });
            menu.Items.Add("上移", null, delegate { MoveImagePreprocessingGroup(group.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveImagePreprocessingGroup(group.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameImagePreprocessingGroup(group.Id); });
            menu.Items.Add("解除群組", null, delegate { UngroupImagePreprocessingGroup(group.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteImagePreprocessingGroup(group.Id); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu)) imageProcessingStepContextMenu = null;
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImagePreprocessingGroup(string groupText)
        {
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            selectedImagePreprocessingStepIndex = -1;
            selectedImagePreprocessingGroupId = GetImagePreprocessingGroupId(groupText);
            if (string.IsNullOrEmpty(selectedImagePreprocessingGroupId)) return;
            HideImageProcessingParameterPanel();
            if (imagePreprocessingFlowTreeView != null) imagePreprocessingFlowTreeView.Visible = false;
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text = "前處理群組會依由上而下的順序串接執行。";
            rightPanelTitleLabel.Text = groupText.Trim() + " 結果";
            statusLabel.Text = "目前選擇：" + groupText.Trim();
        }

        private void ProcessImagePreprocessingGroup(ImageProcessingGroupSettings group)
        {
            if (group == null) return;
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = group.Id;
            imageProcessingExecutionRequested = true;
            BeginParameterApplyStatus(false);
            MarkProcessedPreviewDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已開始處理" + group.DisplayName;
        }

        private string CreateImagePreprocessingStepText(int stepNumber)
        {
            return CreateImagePreprocessingStepText(stepNumber, 0);
        }

        private string CreateImagePreprocessingStepText(int stepNumber, int depth)
        {
            ImageProcessingStepSettings step = stepNumber > 0 && stepNumber <= systemParameters.ImagePreprocessingSteps.Count
                ? systemParameters.ImagePreprocessingSteps[stepNumber - 1] : null;
            string method = step == null || string.IsNullOrWhiteSpace(step.Method) ? "未決定" : step.Method;
            string name = step == null || string.IsNullOrWhiteSpace(step.DisplayName)
                ? "前處理" + stepNumber.ToString(CultureInfo.InvariantCulture) : step.DisplayName;
            return new string(' ', 4 + (Math.Max(0, depth) * 2)) + "前處理" +
                stepNumber.ToString(CultureInfo.InvariantCulture) + "(" + name + " - " + method + ")";
        }

        private static bool IsImagePreprocessingGroupMenuItem(string text)
        {
            string trimmed = text == null ? string.Empty : text.Trim();
            return trimmed.StartsWith("前處理群組(", StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal);
        }

        private static string CreateImagePreprocessingGroupText(ImageProcessingGroupSettings group, int depth)
        {
            string name = string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName;
            return new string(' ', 4 + (Math.Max(0, depth) * 2)) + "前處理群組(" + name + ")";
        }

        private string GetImagePreprocessingGroupId(string text)
        {
            string trimmed = text == null ? string.Empty : text.Trim();
            const string prefix = "前處理群組(";
            if (trimmed.StartsWith(prefix, StringComparison.Ordinal) && trimmed.EndsWith(")", StringComparison.Ordinal))
            {
                string displayName = trimmed.Substring(prefix.Length, trimmed.Length - prefix.Length - 1);
                ImageProcessingGroupSettings namedGroup = systemParameters.ImagePreprocessingGroups.FirstOrDefault(group =>
                    string.Equals(string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName,
                        displayName, StringComparison.Ordinal));
                if (namedGroup != null) return namedGroup.Id;
            }
            foreach (ImageProcessingGroupSettings group in systemParameters.ImagePreprocessingGroups)
                if (string.Equals(trimmed, CreateImagePreprocessingGroupText(group, 0).Trim(), StringComparison.Ordinal)) return group.Id;
            return null;
        }

        private static bool IsImagePreprocessingStepMenuItem(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return false;
            string trimmed = text.Trim();
            int openIndex = trimmed.IndexOf('(');
            int number;
            return trimmed.StartsWith("前處理", StringComparison.Ordinal) && openIndex > "前處理".Length &&
                trimmed.IndexOf(')') > openIndex && int.TryParse(
                    trimmed.Substring("前處理".Length, openIndex - "前處理".Length), out number);
        }

        private int GetImagePreprocessingStepIndex(string text)
        {
            if (!IsImagePreprocessingStepMenuItem(text)) return -1;
            string trimmed = text.Trim();
            int openIndex = trimmed.IndexOf('(');
            int number;
            return int.TryParse(trimmed.Substring("前處理".Length, openIndex - "前處理".Length), out number) ? number - 1 : -1;
        }

        private void ToggleImagePreprocessingMenu()
        {
            if (imagePreprocessingMenuExpanded)
            {
                RemoveImagePreprocessingSubMenuItems();
                return;
            }
            imagePreprocessingMenuExpanded = true;
            RebuildVisibleImagePreprocessingSteps();
        }

        private void ShowImagePreprocessingMenuContextMenu(Point location)
        {
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("新增", null, delegate { AddImagePreprocessingStep(); });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu)) imageProcessingStepContextMenu = null;
            };
            menu.Show(functionListBox, location);
        }

        private void ShowImagePreprocessingStepContextMenu(string stepText, Point location)
        {
            int stepIndex = GetImagePreprocessingStepIndex(stepText);
            if (stepIndex < 0) return;
            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            imageProcessingStepContextMenu = menu;
            menu.Items.Add("處理", null, delegate { ProcessImagePreprocessingStep(stepText); });
            menu.Items.Add("上移", null, delegate { MoveImagePreprocessingStep(stepText, -1); });
            menu.Items.Add("下移", null, delegate { MoveImagePreprocessingStep(stepText, 1); });
            menu.Items.Add("命名", null, delegate { RenameImagePreprocessingStep(stepText); });
            menu.Items.Add("刪除", null, delegate
            {
                ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[stepIndex];
                if (MessageBox.Show("是否要刪除「" + (string.IsNullOrWhiteSpace(step.DisplayName) ? step.Method : step.DisplayName) + "」？",
                    "刪除前處理", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
                systemParameters.ImagePreprocessingSteps.RemoveAt(stepIndex);
                selectedImagePreprocessingStepIndex = -1;
                SaveSystemParameters();
                MarkPreprocessedImageDirty();
                RebuildVisibleImagePreprocessingSteps();
                statusLabel.Text = "已刪除影像前處理";
            });
            menu.Closed += delegate
            {
                if (ReferenceEquals(imageProcessingStepContextMenu, menu)) imageProcessingStepContextMenu = null;
            };
            menu.Show(functionListBox, location);
        }

        private void SelectImagePreprocessingFlowNode(string method)
        {
            if (string.IsNullOrWhiteSpace(method) || imagePreprocessingFlowTreeView == null) return;
            foreach (TreeNode node in imagePreprocessingFlowTreeView.Nodes[0].Nodes)
            {
                if (string.Equals(node.Tag as string, method, StringComparison.Ordinal))
                {
                    imagePreprocessingFlowTreeView.SelectedNode = node;
                    return;
                }
            }
        }

        private void ImagePreprocessingFlowTreeView_AfterSelect(object sender, TreeViewEventArgs e)
        {
            if (selectedImagePreprocessingStepIndex < 0 || e.Node.Nodes.Count > 0) return;
            string method = e.Node.Tag as string;
            ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex];
            step.Method = method;
            step.Parameters = CreateDefaultImagePreprocessingParameters(method);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
            ShowImagePreprocessingParameterPanel(method);
            rightPanelTitleLabel.Text = CreateImagePreprocessingStepText(selectedImagePreprocessingStepIndex + 1).Trim() + " 參數";
        }

        private void AddImagePreprocessingNumericParameter(string key, string labelText, string defaultValue)
        {
            var label = CreateParameterLabel(labelText);
            var numeric = new NumericUpDown
            {
                Width = parameterPanel.Width - 18,
                DecimalPlaces = defaultValue.IndexOf('.') >= 0 ? 2 : 0,
                Minimum = 0,
                Maximum = 100000,
                Increment = defaultValue.IndexOf('.') >= 0 ? 0.1M : 1M
            };
            numeric.Value = Math.Min(numeric.Maximum, ParseDecimalOrDefault(
                GetImagePreprocessingParameterValue(key, defaultValue), ParseDecimalOrDefault(defaultValue, 0)));
            numeric.ValueChanged += delegate { SetPendingImagePreprocessingParameter(key, numeric.Value.ToString(CultureInfo.InvariantCulture)); };
            numeric.MouseWheel += NumericParameter_MouseWheel;
            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(numeric);
        }

        private void AddImagePreprocessingComboParameter(string key, string labelText, string[] values, string defaultValue)
        {
            var label = CreateParameterLabel(labelText);
            var combo = new ComboBox
            {
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            combo.Items.AddRange(values);
            string value = GetImagePreprocessingParameterValue(key, defaultValue);
            combo.SelectedItem = values.Contains(value) ? value : defaultValue;
            combo.SelectedIndexChanged += delegate { SetPendingImagePreprocessingParameter(key, combo.SelectedItem as string); };
            imageProcessingParameterPanel.Controls.Add(label);
            imageProcessingParameterPanel.Controls.Add(combo);
        }

        private string GetImagePreprocessingParameterValue(string key, string defaultValue)
        {
            if (selectedImagePreprocessingStepIndex < 0 || selectedImagePreprocessingStepIndex >= systemParameters.ImagePreprocessingSteps.Count)
                return defaultValue;
            Dictionary<string, string> parameters = pendingImagePreprocessingParameters ??
                ParseImageProcessingParameters(systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters);
            string value;
            return parameters.TryGetValue(key, out value) ? value : defaultValue;
        }

        private void SaveImagePreprocessingParameter(string key, string value)
        {
            if (selectedImagePreprocessingStepIndex < 0 || selectedImagePreprocessingStepIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            Dictionary<string, string> parameters = ParseImageProcessingParameters(systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters);
            parameters[key] = value ?? string.Empty;
            systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters = FormatImageProcessingParameters(parameters);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
        }

        private void SetPendingImagePreprocessingParameter(string key, string value)
        {
            if (string.IsNullOrEmpty(key)) return;
            if (pendingImagePreprocessingParameters == null)
                pendingImagePreprocessingParameters = ParseImageProcessingParameters(systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters);
            pendingImagePreprocessingParameters[key] = value ?? string.Empty;
        }

        private void ApplyPendingImagePreprocessingParameters()
        {
            if (selectedImagePreprocessingStepIndex < 0 ||
                selectedImagePreprocessingStepIndex >= systemParameters.ImagePreprocessingSteps.Count ||
                pendingImagePreprocessingParameters == null) return;
            string committed = FormatImageProcessingParameters(pendingImagePreprocessingParameters);
            if (committed == systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters) return;
            systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters = committed;
            SaveSystemParameters();
            preprocessingExecutionRequested = true;
            BeginParameterApplyStatus(true);
            MarkPreprocessedImageDirty();
            statusLabel.Text = "已套用前處理" + (selectedImagePreprocessingStepIndex + 1) + " 參數";
        }

        private void ShowImagePreprocessingFlowTree(string stepText)
        {
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            selectedImagePreprocessingGroupId = null;
            selectedImagePreprocessingStepIndex = GetImagePreprocessingStepIndex(stepText);
            if (selectedImagePreprocessingStepIndex < 0 ||
                selectedImagePreprocessingStepIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            EnsureImagePreprocessingFlowTreeView();
            parameterPlaceholderLabel.Visible = false;
            HideImageProcessingParameterPanel();
            imagePreprocessingFlowTreeView.Visible = true;
            string method = systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Method;
            if (IsImagePreprocessingMethod(method))
            {
                ShowImagePreprocessingParameterPanel(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 參數";
            }
            else
            {
                imagePreprocessingFlowTreeView.BringToFront();
                SelectImagePreprocessingFlowNode(method);
                rightPanelTitleLabel.Text = stepText.Trim() + " 方法";
            }
        }

        private void EnsureImagePreprocessingFlowTreeView()
        {
            if (imagePreprocessingFlowTreeView != null) return;
            imagePreprocessingFlowTreeView = new TreeView
            {
                BorderStyle = BorderStyle.None,
                Dock = DockStyle.Fill,
                FullRowSelect = true,
                HideSelection = false,
                BackColor = parameterPanel.BackColor,
                ForeColor = Color.FromArgb(39, 46, 56)
            };
            imagePreprocessingFlowTreeView.AfterSelect += ImagePreprocessingFlowTreeView_AfterSelect;
            parameterPanel.Controls.Add(imagePreprocessingFlowTreeView);
            imagePreprocessingFlowTreeView.BringToFront();
            TreeNode root = imagePreprocessingFlowTreeView.Nodes.Add("Image Preprocessing");
            root.Tag = "Image Preprocessing";
            foreach (string method in new[] { "Normalize", "Gaussian Blur", "CLAHE", "Median Blur", "Sharpen", "Bilateral Filter" })
            {
                TreeNode node = root.Nodes.Add(method);
                node.Tag = method;
            }
            root.Expand();
        }

        private void RecordImagePreprocessingStepElapsed(
            Dictionary<ImageProcessingStepSettings, long> elapsedMilliseconds)
        {
            imagePreprocessingStepElapsedMilliseconds.Clear();
            lastPreprocessingElapsedMilliseconds = 0;
            if (elapsedMilliseconds == null) return;
            foreach (KeyValuePair<ImageProcessingStepSettings, long> item in elapsedMilliseconds)
            {
                imagePreprocessingStepElapsedMilliseconds[item.Key] = item.Value;
                lastPreprocessingElapsedMilliseconds += item.Value;
            }
        }

        private void RefreshVisibleImagePreprocessingStepText(int stepIndex)
        {
            if (stepIndex < 0 || stepIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            for (int itemIndex = 0; itemIndex < functionListBox.Items.Count; itemIndex++)
            {
                string itemText = functionListBox.Items[itemIndex] as string;
                if (GetImagePreprocessingStepIndex(itemText) != stepIndex) continue;
                int leadingSpaces = itemText.Length - itemText.TrimStart().Length;
                int depth = Math.Max(0, (leadingSpaces - 4) / 2);
                isUpdatingFunctionListText = true;
                int selectedIndex = functionListBox.SelectedIndex;
                int topIndex = functionListBox.TopIndex;
                functionListBox.BeginUpdate();
                try
                {
                    functionListBox.Items[itemIndex] = CreateImagePreprocessingStepText(stepIndex + 1, depth);
                    if (selectedIndex >= 0 && selectedIndex < functionListBox.Items.Count) functionListBox.SelectedIndex = selectedIndex;
                    if (topIndex >= 0 && topIndex < functionListBox.Items.Count) functionListBox.TopIndex = topIndex;
                }
                finally
                {
                    functionListBox.EndUpdate();
                    isUpdatingFunctionListText = false;
                }
                break;
            }
        }

        private void RequestPreprocessedImageUpdate()
        {
            RequestPreprocessedImageUpdateCore();
        }

        private void RestorePreprocessedImageViewState()
        {
            if (!hasPreprocessedImageViewState) return;
            isSyncingImageView = true;
            try
            {
                ApplyImageViewState(GetVisibleLeftImageDisplayControl(), preprocessedImageViewState);
                ApplyImageViewState(GetVisibleRightImageDisplayControl(), preprocessedImageViewState);
                if (isImageViewerMaximized)
                {
                    maximizedImageViewerViewState = preprocessedImageViewState;
                    hasMaximizedImageViewerViewState = true;
                }
                else
                {
                    sharedImageViewState = preprocessedImageViewState;
                    hasSharedImageViewState = true;
                }
            }
            finally { isSyncingImageView = false; }
        }

        private void RestorePreprocessedDisplaysToOriginalSource()
        {
            if (rightOriginalDisplayControl == null || !rightOriginalDisplayControl.IsLargeImageMode) return;
            LargeImageSource source = rightOriginalDisplayControl.GetSharedLargeImageSource();
            if (source == null) return;
            isSyncingImageView = true;
            try
            {
                leftPreprocessedDisplayControl.SetSharedLargeImageSource(source);
                rightPreprocessedDisplayControl.SetSharedLargeImageSource(source);
            }
            finally
            {
                isSyncingImageView = false;
                source.ReleaseReference();
            }
        }

        private void CapturePreprocessedImageViewState()
        {
            ImageDisplayControl source = isImageViewerMaximized
                ? (isLeftImageViewerMaximized ? GetVisibleLeftImageDisplayControl() : GetVisibleRightImageDisplayControl())
                : GetVisibleLeftImageDisplayControl();
            if (source == null || !source.HasImage) source = GetVisibleRightImageDisplayControl();
            if (source == null || !source.HasImage) return;
            preprocessedImageViewState = source.ViewState;
            hasPreprocessedImageViewState = true;
        }

        private void MarkPreprocessedImageDirty()
        {
            CapturePreprocessedImageViewState();
            preprocessedImageDirty = true;
            preprocessedImageGeneration++;
            imagePreprocessingStepElapsedMilliseconds.Clear();
            MarkProcessedImageDirty();
            if (leftPreprocessedDisplayControl != null && rightPreprocessedDisplayControl != null)
            {
                isSyncingImageView = true;
                try
                {
                    leftPreprocessedDisplayControl.ClearImage();
                    rightPreprocessedDisplayControl.ClearImage();
                }
                finally { isSyncingImageView = false; }
            }
            lock (largePreprocessedImageLock)
            {
                if (largePreprocessedImageSource != null)
                {
                    largePreprocessedImageSource.ReleaseReference();
                    largePreprocessedImageSource = null;
                }
            }
            if (latestPreprocessedImage != null)
            {
                latestPreprocessedImage.Dispose();
                latestPreprocessedImage = null;
            }
            RestorePreprocessedDisplaysToOriginalSource();
            RestorePreprocessedImageViewState();
            RequestPreprocessedImageUpdate();
        }

        private PreprocessedBitmapResult CreateCurrentPreprocessedBitmap(string sourceFilePath)
        {
            using (Cv.Mat source = Cv.Cv2.ImRead(sourceFilePath, Cv.ImreadModes.Grayscale))
            {
                PreprocessedImageResult preprocessingResult = CreateOpenCvPreprocessedImage(source);
                using (Cv.Mat result = preprocessingResult.Image)
                {
                    return new PreprocessedBitmapResult
                    {
                        Image = CreateBitmapFromGrayMat(result),
                        StepElapsedMilliseconds = preprocessingResult.StepElapsedMilliseconds
                    };
                }
            }
        }

        private static Bitmap CreateBitmapFromGrayMat(Cv.Mat source)
        {
            var bitmap = new Bitmap(source.Width, source.Height, PixelFormat.Format24bppRgb);
            BitmapData data = bitmap.LockBits(new Rectangle(0, 0, bitmap.Width, bitmap.Height),
                ImageLockMode.WriteOnly, bitmap.PixelFormat);
            try
            {
                byte[] sourceRow = new byte[source.Width];
                byte[] destinationRow = new byte[source.Width * 3];
                long sourceStride = source.Step();
                for (int y = 0; y < source.Height; y++)
                {
                    Marshal.Copy(source.Data + checked((int)(y * sourceStride)), sourceRow, 0, sourceRow.Length);
                    for (int x = 0; x < source.Width; x++)
                    {
                        int offset = x * 3;
                        destinationRow[offset] = sourceRow[x];
                        destinationRow[offset + 1] = sourceRow[x];
                        destinationRow[offset + 2] = sourceRow[x];
                    }
                    Marshal.Copy(destinationRow, 0, data.Scan0 + (y * data.Stride), destinationRow.Length);
                }
            }
            finally
            {
                bitmap.UnlockBits(data);
            }
            return bitmap;
        }

        private PreprocessedImageResult CreateOpenCvPreprocessedImage(Cv.Mat source)
        {
            return CreateOpenCvPreprocessedImage(source, null);
        }

        private PreprocessedImageResult CreateOpenCvPreprocessedImage(Cv.Mat source, string stopAfterStepId)
        {
            Cv.Mat current = source.Clone();
            var elapsed = new Dictionary<ImageProcessingStepSettings, long>();
            try
            {
                foreach (ImageProcessingStepSettings step in GetOrderedImagePreprocessingSteps())
                {
                    if (step == null || !IsImagePreprocessingMethod(step.Method)) continue;
                    Stopwatch stopwatch = Stopwatch.StartNew();
                    Cv.Mat next = ApplyOpenCvPreprocessingStep(current, step.Method,
                        ParseImageProcessingParameters(step.Parameters));
                    elapsed[step] = stopwatch.ElapsedMilliseconds;
                    current.Dispose();
                    current = next;
                    if (!string.IsNullOrWhiteSpace(stopAfterStepId) &&
                        string.Equals(step.Id, stopAfterStepId, StringComparison.Ordinal)) break;
                }
                var result = new PreprocessedImageResult
                {
                    Image = current,
                    StepElapsedMilliseconds = elapsed
                };
                current = null;
                return result;
            }
            finally
            {
                if (current != null) current.Dispose();
            }
        }

        private static Cv.Mat ApplyOpenCvPreprocessingStep(
            Cv.Mat source,
            string method,
            Dictionary<string, string> parameters)
        {
            var destination = new Cv.Mat();
            if (method == "Normalize")
            {
                Cv.Cv2.Normalize(source, destination,
                    GetDoubleParameter(parameters, "Alpha", 0),
                    GetDoubleParameter(parameters, "Beta", 255),
                    Cv.NormTypes.MinMax, Cv.MatType.CV_8UC1.Value);
            }
            else if (method == "Gaussian Blur")
            {
                int kernelSize = EnsureOdd(Math.Max(3, GetIntParameter(parameters, "KernelSize", 5)));
                Cv.Cv2.GaussianBlur(source, destination, new Cv.Size(kernelSize, kernelSize),
                    Math.Max(0, GetDoubleParameter(parameters, "Sigma", 1.4)));
            }
            else if (method == "CLAHE")
            {
                int gridSize = Math.Max(2, GetIntParameter(parameters, "TileGridSize", 8));
                using (Cv.CLAHE clahe = Cv.Cv2.CreateCLAHE(
                    Math.Max(0.1, GetDoubleParameter(parameters, "ClipLimit", 2.0)),
                    new Cv.Size(gridSize, gridSize))) clahe.Apply(source, destination);
            }
            else if (method == "Median Blur")
            {
                Cv.Cv2.MedianBlur(source, destination,
                    EnsureOdd(Math.Max(3, GetIntParameter(parameters, "KernelSize", 5))));
            }
            else if (method == "Sharpen")
            {
                int kernelSize = EnsureOdd(Math.Max(3, GetIntParameter(parameters, "KernelSize", 3)));
                double amount = Math.Max(0, GetDoubleParameter(parameters, "Amount", 1.0));
                using (var blurred = new Cv.Mat())
                {
                    Cv.Cv2.GaussianBlur(source, blurred, new Cv.Size(kernelSize, kernelSize),
                        Math.Max(0, GetDoubleParameter(parameters, "Sigma", 0)));
                    Cv.Cv2.AddWeighted(source, 1.0 + amount, blurred, -amount, 0, destination);
                }
            }
            else if (method == "Bilateral Filter")
            {
                Cv.Cv2.BilateralFilter(source, destination,
                    EnsureOdd(Math.Max(3, GetIntParameter(parameters, "Diameter", 5))),
                    Math.Max(0.1, GetDoubleParameter(parameters, "SigmaColor", 50)),
                    Math.Max(0.1, GetDoubleParameter(parameters, "SigmaSpace", 50)));
            }
            else source.CopyTo(destination);
            return destination;
        }

        private void CreateImagePreprocessingGroup(List<int> stepIndexes)
        {
            if (stepIndexes == null || stepIndexes.Count < 2) return;
            string name;
            if (!TryGetImageProcessingStepName(string.Empty, out name)) return;
            var group = new ImageProcessingGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = string.IsNullOrWhiteSpace(name) ? "未命名群組" : name
            };
            systemParameters.ImagePreprocessingGroups.Add(group);
            foreach (int index in stepIndexes)
            {
                if (index >= 0 && index < systemParameters.ImagePreprocessingSteps.Count)
                    systemParameters.ImagePreprocessingSteps[index].GroupId = group.Id;
            }
            expandedImagePreprocessingGroupIds.Add(group.Id);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
            functionListBox.SelectedItem = CreateImagePreprocessingGroupText(group, 0);
        }

        private void RenameImagePreprocessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImagePreprocessingGroup(groupId);
            string name;
            if (group == null || !TryGetImageProcessingStepName(group.DisplayName, out name) || string.IsNullOrWhiteSpace(name)) return;
            group.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImagePreprocessingSteps();
        }

        private void MoveImagePreprocessingGroup(string groupId, int direction)
        {
            ImageProcessingGroupSettings group = FindImagePreprocessingGroup(groupId);
            if (group == null) return;
            List<ImageProcessingGroupSettings> siblings = systemParameters.ImagePreprocessingGroups
                .Where(candidate => string.Equals(candidate.ParentGroupId, group.ParentGroupId, StringComparison.Ordinal)).ToList();
            int index = siblings.IndexOf(group);
            int targetIndex = index + direction;
            if (index < 0 || targetIndex < 0 || targetIndex >= siblings.Count) return;
            ImageProcessingGroupSettings target = siblings[targetIndex];
            systemParameters.ImagePreprocessingGroups.Remove(group);
            int insertionIndex = systemParameters.ImagePreprocessingGroups.IndexOf(target);
            systemParameters.ImagePreprocessingGroups.Insert(direction > 0 ? insertionIndex + 1 : insertionIndex, group);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
        }

        private void UngroupImagePreprocessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImagePreprocessingGroup(groupId);
            if (group == null) return;
            foreach (ImageProcessingStepSettings step in systemParameters.ImagePreprocessingSteps)
                if (string.Equals(step.GroupId, group.Id, StringComparison.Ordinal)) step.GroupId = group.ParentGroupId;
            foreach (ImageProcessingGroupSettings child in systemParameters.ImagePreprocessingGroups)
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal)) child.ParentGroupId = group.ParentGroupId;
            expandedImagePreprocessingGroupIds.Remove(group.Id);
            systemParameters.ImagePreprocessingGroups.Remove(group);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
        }

        private void DeleteImagePreprocessingGroup(string groupId)
        {
            ImageProcessingGroupSettings group = FindImagePreprocessingGroup(groupId);
            if (group == null || MessageBox.Show("是否要刪除前處理群組「" + group.DisplayName + "」及其項目？", "刪除群組", MessageBoxButtons.YesNo, MessageBoxIcon.Warning) != DialogResult.Yes) return;
            var deletedIds = new HashSet<string>(StringComparer.Ordinal) { group.Id };
            bool changed = true;
            while (changed)
            {
                changed = false;
                foreach (ImageProcessingGroupSettings candidate in systemParameters.ImagePreprocessingGroups)
                    if (deletedIds.Contains(candidate.ParentGroupId) && deletedIds.Add(candidate.Id)) changed = true;
            }
            systemParameters.ImagePreprocessingSteps.RemoveAll(step => deletedIds.Contains(step.GroupId));
            systemParameters.ImagePreprocessingGroups.RemoveAll(candidate => deletedIds.Contains(candidate.Id));
            foreach (string id in deletedIds) expandedImagePreprocessingGroupIds.Remove(id);
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
        }

        private void ShowImagePreprocessingParameterPanel(string method)
        {
            EnsureImageProcessingParameterPanel();
            pendingImagePreprocessingParameters = ParseImageProcessingParameters(
                systemParameters.ImagePreprocessingSteps[selectedImagePreprocessingStepIndex].Parameters);
            imagePreprocessingFlowTreeView.Visible = false;
            imageProcessingParameterPanel.Visible = true;
            imageProcessingParameterPanel.Controls.Clear();
            imageProcessingParameterPanel.Controls.Add(CreateParameterLabel(method));
            AddReselectImagePreprocessingMethodButton();

            if (method == "Normalize")
            {
                AddImagePreprocessingNumericParameter("Alpha", "最小輸出值", "0");
                AddImagePreprocessingNumericParameter("Beta", "最大輸出值", "255");
            }
            else if (method == "Gaussian Blur")
            {
                AddImagePreprocessingComboParameter("KernelSize", "核心大小", KernelSizeOptions, "5");
                AddImagePreprocessingNumericParameter("Sigma", "Sigma", "1.4");
            }
            else if (method == "CLAHE")
            {
                AddImagePreprocessingNumericParameter("ClipLimit", "Clip Limit", "2.0");
                AddImagePreprocessingComboParameter("TileGridSize", "Tile Grid Size", new[] { "4", "8", "16", "32" }, "8");
            }
            else if (method == "Median Blur")
            {
                AddImagePreprocessingComboParameter("KernelSize", "核心大小", KernelSizeOptions, "5");
            }
            else if (method == "Sharpen")
            {
                AddImagePreprocessingNumericParameter("Amount", "強度", "1.0");
                AddImagePreprocessingComboParameter("KernelSize", "核心大小", KernelSizeOptions, "3");
                AddImagePreprocessingNumericParameter("Sigma", "Sigma", "0");
            }
            else if (method == "Bilateral Filter")
            {
                AddImagePreprocessingComboParameter("Diameter", "直徑", KernelSizeOptions, "5");
                AddImagePreprocessingNumericParameter("SigmaColor", "Sigma Color", "50");
                AddImagePreprocessingNumericParameter("SigmaSpace", "Sigma Space", "50");
            }

            AddParameterEditButtons(true);
        }

        private void AddReselectImagePreprocessingMethodButton()
        {
            var button = new Button();
            button.Width = parameterPanel.Width - 18;
            button.Height = 30;
            button.Text = "重新選擇方法";
            button.Click += delegate
            {
                HideImageProcessingParameterPanel();
                imagePreprocessingFlowTreeView.Visible = true;
                rightPanelTitleLabel.Text = "前處理" + (selectedImagePreprocessingStepIndex + 1) + " 方法";
            };
            imageProcessingParameterPanel.Controls.Add(button);
        }

        private void RemoveImagePreprocessingSubMenuItems()
        {
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (string.Equals(itemText == null ? null : itemText.Trim(),
                        OriginalPreprocessingSourceText.Trim(), StringComparison.Ordinal) ||
                    IsImagePreprocessingStepMenuItem(itemText) || IsImagePreprocessingGroupMenuItem(itemText))
                    functionListBox.Items.RemoveAt(index);
            }
            imagePreprocessingMenuExpanded = false;
        }

        private void AddImagePreprocessingStep()
        {
            systemParameters.ImagePreprocessingSteps.Add(new ImageProcessingStepSettings
            {
                Id = Guid.NewGuid().ToString("N")
            });
            SaveSystemParameters();
            imagePreprocessingMenuExpanded = true;
            RebuildVisibleImagePreprocessingSteps();
            functionListBox.SelectedItem = CreateImagePreprocessingStepText(systemParameters.ImagePreprocessingSteps.Count);
            statusLabel.Text = "已新增影像前處理" + systemParameters.ImagePreprocessingSteps.Count.ToString(System.Globalization.CultureInfo.InvariantCulture);
        }

        private void RebuildVisibleImagePreprocessingSteps()
        {
            if (!imagePreprocessingMenuExpanded) return;
            RemoveImagePreprocessingSubMenuItems();
            imagePreprocessingMenuExpanded = true;
            int insertIndex = functionListBox.Items.IndexOf(ImagePreprocessingMenuText) + 1;
            // Remove any stale duplicate source entries before rebuilding the visible list.
            for (int index = functionListBox.Items.Count - 1; index >= 0; index--)
            {
                string itemText = functionListBox.Items[index] as string;
                if (string.Equals(itemText == null ? null : itemText.Trim(),
                        OriginalPreprocessingSourceText.Trim(), StringComparison.Ordinal))
                {
                    functionListBox.Items.RemoveAt(index);
                }
            }
            functionListBox.Items.Insert(insertIndex, OriginalPreprocessingSourceText);
            insertIndex++;
            for (int index = 0; index < systemParameters.ImagePreprocessingSteps.Count; index++)
                if (string.IsNullOrWhiteSpace(systemParameters.ImagePreprocessingSteps[index].GroupId))
                    functionListBox.Items.Insert(insertIndex++, CreateImagePreprocessingStepText(index + 1));
            foreach (ImageProcessingGroupSettings group in systemParameters.ImagePreprocessingGroups)
                if (string.IsNullOrWhiteSpace(group.ParentGroupId)) InsertImagePreprocessingGroup(group, 0, ref insertIndex);
        }

        private void InsertImagePreprocessingGroup(ImageProcessingGroupSettings group, int depth, ref int insertIndex)
        {
            functionListBox.Items.Insert(insertIndex++, CreateImagePreprocessingGroupText(group, depth));
            if (!expandedImagePreprocessingGroupIds.Contains(group.Id)) return;
            for (int index = 0; index < systemParameters.ImagePreprocessingSteps.Count; index++)
                if (string.Equals(systemParameters.ImagePreprocessingSteps[index].GroupId, group.Id, StringComparison.Ordinal))
                    functionListBox.Items.Insert(insertIndex++, CreateImagePreprocessingStepText(index + 1, depth + 1));
            foreach (ImageProcessingGroupSettings child in systemParameters.ImagePreprocessingGroups)
                if (string.Equals(child.ParentGroupId, group.Id, StringComparison.Ordinal)) InsertImagePreprocessingGroup(child, depth + 1, ref insertIndex);
        }

        private void MoveImagePreprocessingStep(string stepText, int direction)
        {
            int stepIndex = GetImagePreprocessingStepIndex(stepText);
            int targetIndex = stepIndex + direction;
            if (stepIndex < 0 || targetIndex < 0 || targetIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[stepIndex];
            ImageProcessingStepSettings target = systemParameters.ImagePreprocessingSteps[targetIndex];
            systemParameters.ImagePreprocessingSteps[stepIndex] = target;
            systemParameters.ImagePreprocessingSteps[targetIndex] = step;
            selectedImagePreprocessingStepIndex = targetIndex;
            SaveSystemParameters();
            MarkPreprocessedImageDirty();
            RebuildVisibleImagePreprocessingSteps();
            functionListBox.SelectedItem = CreateImagePreprocessingStepText(targetIndex + 1);
            statusLabel.Text = direction < 0 ? "已上移前處理" : "已下移前處理";
        }

        private void RenameImagePreprocessingStep(string stepText)
        {
            int stepIndex = GetImagePreprocessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            ImageProcessingStepSettings step = systemParameters.ImagePreprocessingSteps[stepIndex];
            string name;
            if (!TryGetImageProcessingStepName(step.DisplayName, out name)) return;
            step.DisplayName = name;
            SaveSystemParameters();
            RebuildVisibleImagePreprocessingSteps();
            functionListBox.SelectedItem = CreateImagePreprocessingStepText(stepIndex + 1);
            statusLabel.Text = "已命名前處理" + CreateImagePreprocessingStepText(stepIndex + 1).Trim();
        }

        private void ProcessImagePreprocessingStep(string stepText)
        {
            int stepIndex = GetImagePreprocessingStepIndex(stepText);
            if (stepIndex < 0 || stepIndex >= systemParameters.ImagePreprocessingSteps.Count) return;
            selectedImagePreprocessingStepIndex = stepIndex;
            selectedImagePreprocessingGroupId = null;
            activeImageRelationSourceType = "Step";
            activeImageRelationSourceId = systemParameters.ImagePreprocessingSteps[stepIndex].Id;
            preprocessingExecutionRequested = true;
            BeginParameterApplyStatus(true);
            MarkPreprocessedImageDirty();
            statusLabel.Text = "已開始處理前處理" + (stepIndex + 1);
        }

        private void ProcessImagePreprocessingGroup(string groupId)
        {
            if (string.IsNullOrEmpty(groupId) || FindImagePreprocessingGroup(groupId) == null) return;
            selectedImagePreprocessingStepIndex = -1;
            selectedImagePreprocessingGroupId = groupId;
            activeImageRelationSourceType = "Group";
            activeImageRelationSourceId = groupId;
            preprocessingExecutionRequested = true;
            BeginParameterApplyStatus(true);
            MarkPreprocessedImageDirty();
            statusLabel.Text = "已開始處理前處理群組";
        }
    }
}
