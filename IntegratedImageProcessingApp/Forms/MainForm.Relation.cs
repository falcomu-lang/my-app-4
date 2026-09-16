using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private int selectedImageRelationIndex = -1;
        private bool imageRelationMenuExpanded;
        private string selectedImageRelationGroupId;
        private string activeImageRelationGroupId;
        private readonly HashSet<string> expandedImageRelationGroupIds = new HashSet<string>(StringComparer.Ordinal);
        private Panel imageRelationParameterPanel;
        private string activeImageRelationSourceType;
        private string activeImageRelationSourceId;
        // Preprocessing is normally an isolated request.  This flag is set
        // only when an image relation explicitly needs its preprocessing
        // source before the relation result can be rendered.
        private bool preprocessingExecutionRequestedByImageRelation;
        private readonly object largeRelationSourceCacheLock = new object();
        private readonly Dictionary<string, LargeImageSource> largeRelationSourceCache =
            new Dictionary<string, LargeImageSource>(StringComparer.Ordinal);
        private int largeRelationSourceCacheGeneration = -1;
        private const string ImageRelationMenuText = "影像關聯";

        private sealed class RelationChoice
        {
            public string DisplayText { get; set; }
            public string Type { get; set; }
            public string Id { get; set; }
            public override string ToString() { return DisplayText; }
        }

        private static string CreateImageRelationGroupText(ImageRelationGroupSettings group, int depth)
        {
            string displayName = string.IsNullOrWhiteSpace(group.DisplayName) ? "未命名群組" : group.DisplayName;
            return new string(' ', 4 + (depth * 2)) + "關聯群組(" + displayName + ")";
        }

        private string GetImageRelationGroupId(string menuText)
        {
            if (string.IsNullOrWhiteSpace(menuText))
            {
                return null;
            }

            string trimmedText = menuText.Trim();
            foreach (ImageRelationGroupSettings group in systemParameters.ImageRelationGroups)
            {
                if (string.Equals(trimmedText, CreateImageRelationGroupText(group, 0).Trim(), StringComparison.Ordinal) ||
                    string.Equals(trimmedText, CreateImageRelationGroupText(group, 1).Trim(), StringComparison.Ordinal))
                {
                    return group.Id;
                }
            }

            return null;
        }

        private List<int> GetSelectedImageRelationIndexes()
        {
            var indexes = new List<int>();
            foreach (object selectedItem in functionListBox.SelectedItems)
            {
                int index = GetImageRelationIndex(selectedItem as string);
                if (index >= 0 && !indexes.Contains(index))
                {
                    indexes.Add(index);
                }
            }

            indexes.Sort();
            return indexes;
        }

        private void ShowImageRelationMultiSelectContextMenu(List<int> relationIndexes, Point location)
        {
            if (relationIndexes == null || relationIndexes.Count < 2)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("分組", null, delegate { CreateImageRelationGroup(relationIndexes); });
            menu.Show(functionListBox, location);
        }

        private void CreateImageRelationGroup(List<int> relationIndexes)
        {
            if (relationIndexes == null || relationIndexes.Count < 2)
            {
                return;
            }

            string name = PromptForText("關聯群組名稱", "請輸入關聯群組名稱：", "未命名群組");
            if (name == null)
            {
                return;
            }

            var group = new ImageRelationGroupSettings
            {
                Id = Guid.NewGuid().ToString("N"),
                DisplayName = string.IsNullOrWhiteSpace(name) ? "未命名群組" : name.Trim(),
                ParentGroupId = string.Empty
            };
            systemParameters.ImageRelationGroups.Add(group);
            foreach (int index in relationIndexes)
            {
                if (index >= 0 && index < systemParameters.ImageRelations.Count)
                {
                    systemParameters.ImageRelations[index].GroupId = group.Id;
                }
            }

            SaveSystemParameters();
            RebuildVisibleImageRelations();
            functionListBox.SelectedItem = CreateImageRelationGroupText(group, 0);
            statusLabel.Text = "已建立關聯群組：" + group.DisplayName;
        }

        private void ShowImageRelationGroup(string groupText)
        {
            string groupId = GetImageRelationGroupId(groupText);
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            if (group == null)
            {
                return;
            }

            selectedImageRelationGroupId = group.Id;
            selectedImageRelationIndex = -1;
            HideImageRelationParameterPanel();
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.Text = "此關聯群組會將群組內每個關聯各自對來源影像計算，再以二值 MASK OR 合併顯示。";
            parameterPlaceholderLabel.BringToFront();
            rightPanelTitleLabel.Text = CreateImageRelationGroupText(group, 0).Trim() + " 結果";
            statusLabel.Text = "目前選擇：" + CreateImageRelationGroupText(group, 0).Trim();
        }

        private void ToggleImageRelationGroup(string groupText)
        {
            string groupId = GetImageRelationGroupId(groupText);
            if (string.IsNullOrEmpty(groupId))
            {
                return;
            }

            if (!expandedImageRelationGroupIds.Add(groupId))
            {
                expandedImageRelationGroupIds.Remove(groupId);
            }

            RebuildVisibleImageRelations();
            string rebuiltText = CreateImageRelationGroupText(FindImageRelationGroup(groupId), 0);
            if (functionListBox.Items.Contains(rebuiltText))
            {
                functionListBox.SelectedItem = rebuiltText;
            }
        }

        private ImageRelationGroupSettings FindImageRelationGroup(string groupId)
        {
            if (string.IsNullOrWhiteSpace(groupId))
            {
                return null;
            }

            return systemParameters.ImageRelationGroups.Find(
                group => string.Equals(group.Id, groupId, StringComparison.Ordinal));
        }

        private List<ImageRelationSettings> GetImageRelationGroupRelations(string groupId)
        {
            var groupIds = new HashSet<string>(StringComparer.Ordinal);
            CollectImageRelationGroupIds(groupId, groupIds);
            var relations = new List<ImageRelationSettings>();
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (!string.IsNullOrWhiteSpace(relation.GroupId) && groupIds.Contains(relation.GroupId))
                {
                    relations.Add(relation);
                }
            }

            return relations;
        }

        private void CollectImageRelationGroupIds(string groupId, HashSet<string> groupIds)
        {
            if (string.IsNullOrWhiteSpace(groupId) || !groupIds.Add(groupId))
            {
                return;
            }

            foreach (ImageRelationGroupSettings group in systemParameters.ImageRelationGroups)
            {
                if (string.Equals(group.ParentGroupId, groupId, StringComparison.Ordinal))
                {
                    CollectImageRelationGroupIds(group.Id, groupIds);
                }
            }
        }

        private void ShowImageRelationGroupContextMenu(string groupId, Point location)
        {
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            if (group == null)
            {
                return;
            }

            CloseImageProcessingStepContextMenu();
            var menu = new ContextMenuStrip();
            menu.Items.Add("處理", null, delegate { ProcessImageRelationGroup(group.Id); });
            menu.Items.Add("上移", null, delegate { MoveImageRelationGroup(group.Id, -1); });
            menu.Items.Add("下移", null, delegate { MoveImageRelationGroup(group.Id, 1); });
            menu.Items.Add("命名", null, delegate { RenameImageRelationGroup(group.Id); });
            menu.Items.Add("解除群組", null, delegate { UngroupImageRelations(group.Id); });
            menu.Items.Add("刪除", null, delegate { DeleteImageRelationGroup(group.Id); });
            menu.Show(functionListBox, location);
        }

        private void ProcessImageRelationGroup(string groupId)
        {
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            List<ImageRelationSettings> relations = GetImageRelationGroupRelations(groupId);
            if (group == null || relations.Count == 0)
            {
                statusLabel.Text = "關聯群組沒有可處理的關聯";
                return;
            }

            foreach (ImageRelationSettings relation in relations)
            {
                if (string.IsNullOrWhiteSpace(relation.SourceType) || string.IsNullOrWhiteSpace(relation.ProcessingId))
                {
                    statusLabel.Text = "關聯群組內有尚未設定完成的關聯";
                    return;
                }
            }

            activeImageRelationGroupId = group.Id;
            activeImageRelationSourceType = "Original";
            activeImageRelationSourceId = null;
            selectedImageRelationGroupId = group.Id;
            selectedImageRelationIndex = -1;
            selectedImageProcessingStepIndex = -1;
            selectedImageProcessingGroupId = null;
            imageProcessingExecutionRequested = true;
            preprocessingExecutionRequestedByImageRelation =
                relations.Exists(relation => !string.Equals(relation.SourceType, "Original", StringComparison.Ordinal));
            preprocessingExecutionRequested = preprocessingExecutionRequestedByImageRelation;
            if (!preprocessingExecutionRequestedByImageRelation)
            {
                RestorePreprocessedDisplaysToOriginalSource();
            }

            BeginParameterApplyStatus(false);
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            statusLabel.Text = "已開始處理關聯群組：" + group.DisplayName;
        }

        private void MoveImageRelationGroup(string groupId, int direction)
        {
            int index = systemParameters.ImageRelationGroups.FindIndex(
                item => string.Equals(item.Id, groupId, StringComparison.Ordinal));
            int target = index + direction;
            if (index < 0 || target < 0 || target >= systemParameters.ImageRelationGroups.Count)
            {
                return;
            }

            ImageRelationGroupSettings group = systemParameters.ImageRelationGroups[index];
            systemParameters.ImageRelationGroups.RemoveAt(index);
            systemParameters.ImageRelationGroups.Insert(target, group);
            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private void RenameImageRelationGroup(string groupId)
        {
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            if (group == null)
            {
                return;
            }

            string name = PromptForText("關聯群組名稱", "請輸入關聯群組名稱：", group.DisplayName);
            if (name == null)
            {
                return;
            }

            if (!string.IsNullOrWhiteSpace(name))
            {
                group.DisplayName = name.Trim();
            }

            SaveSystemParameters();
            RebuildVisibleImageRelations();
        }

        private void UngroupImageRelations(string groupId)
        {
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            if (group == null)
            {
                return;
            }

            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            CollectImageRelationGroupIds(groupId, groupIds);
            foreach (ImageRelationSettings relation in systemParameters.ImageRelations)
            {
                if (groupIds.Contains(relation.GroupId))
                {
                    relation.GroupId = string.Empty;
                }
            }

            foreach (ImageRelationGroupSettings child in systemParameters.ImageRelationGroups)
            {
                if (groupIds.Contains(child.Id))
                {
                    child.ParentGroupId = string.Empty;
                }
            }

            systemParameters.ImageRelationGroups.RemoveAll(item => groupIds.Contains(item.Id));
            expandedImageRelationGroupIds.RemoveWhere(item => groupIds.Contains(item));
            if (string.Equals(activeImageRelationGroupId, groupId, StringComparison.Ordinal))
            {
                activeImageRelationGroupId = null;
                MarkProcessedImageDirty();
            }

            SaveSystemParameters();
            RebuildVisibleImageRelations();
            statusLabel.Text = "已解除關聯群組";
        }

        private void DeleteImageRelationGroup(string groupId)
        {
            ImageRelationGroupSettings group = FindImageRelationGroup(groupId);
            if (group == null)
            {
                return;
            }

            if (MessageBox.Show(
                    "刪除群組後，群組內的關聯也會一併刪除，是否繼續？",
                    "刪除關聯群組",
                    MessageBoxButtons.YesNo,
                    MessageBoxIcon.Warning) != DialogResult.Yes)
            {
                return;
            }

            HashSet<string> groupIds = new HashSet<string>(StringComparer.Ordinal);
            CollectImageRelationGroupIds(groupId, groupIds);
            systemParameters.ImageRelations.RemoveAll(relation => groupIds.Contains(relation.GroupId));
            systemParameters.ImageRelationGroups.RemoveAll(item => groupIds.Contains(item.Id));
            expandedImageRelationGroupIds.RemoveWhere(item => groupIds.Contains(item));
            if (string.Equals(activeImageRelationGroupId, groupId, StringComparison.Ordinal))
            {
                activeImageRelationGroupId = null;
                selectedImageRelationGroupId = null;
                MarkProcessedImageDirty();
            }

            SaveSystemParameters();
            RebuildVisibleImageRelations();
            statusLabel.Text = "已刪除關聯群組";
        }

        private bool HasSelectedImageRelationGroupPreviewableSteps()
        {
            if (string.IsNullOrWhiteSpace(activeImageRelationGroupId))
            {
                return false;
            }

            List<ImageRelationSettings> relations = GetImageRelationGroupRelations(activeImageRelationGroupId);
            if (relations.Count == 0)
            {
                return false;
            }

            foreach (ImageRelationSettings relation in relations)
            {
                List<ImageProcessingStepSettings> steps = GetImageProcessingStepsForRelation(relation);
                if (steps.Count == 0)
                {
                    return false;
                }

                foreach (ImageProcessingStepSettings step in steps)
                {
                    if (!IsBinaryMaskProcessingMethod(step.Method))
                    {
                        return false;
                    }
                }
            }

            return true;
        }

        private List<ImageProcessingStepSettings> GetImageProcessingStepsForRelation(ImageRelationSettings relation)
        {
            var steps = new List<ImageProcessingStepSettings>();
            if (relation == null || string.IsNullOrWhiteSpace(relation.ProcessingId))
            {
                return steps;
            }

            if (string.Equals(relation.ProcessingType, "Group", StringComparison.Ordinal))
            {
                CollectImageProcessingGroupSteps(relation.ProcessingId, steps);
                return steps;
            }

            ImageProcessingStepSettings step = systemParameters.ImageProcessingSteps.Find(
                item => string.Equals(item.Id, relation.ProcessingId, StringComparison.Ordinal));
            if (step != null)
            {
                steps.Add(step);
            }

            return steps;
        }

        private Bitmap CreateCurrentRelationGroupProcessedImage()
        {
            if (systemParameters.RoiRegions.Count == 0 || !HasSelectedImageRelationGroupPreviewableSteps())
            {
                return null;
            }

            using (Bitmap original = rightOriginalDisplayControl.CloneImage())
            {
                if (original == null)
                {
                    return null;
                }

                var result = new Bitmap(original);
                foreach (ImageRelationSettings relation in GetImageRelationGroupRelations(activeImageRelationGroupId))
                {
                    using (Bitmap source = CreateRelationSourceBitmap(original, relation))
                    {
                        if (source == null)
                        {
                            continue;
                        }

                        foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                        {
                            Rectangle roi = Rectangle.Intersect(
                                roiRegion.Bounds,
                                new Rectangle(0, 0, source.Width, source.Height));
                            if (roi.Width <= 0 || roi.Height <= 0)
                            {
                                continue;
                            }

                            using (Cv.Mat gray = CreateOpenCvGrayMat(source))
                            using (Cv.Mat roiGray = new Cv.Mat(
                                gray,
                                new Cv.Rect(roi.X, roi.Y, roi.Width, roi.Height)))
                            using (Cv.Mat combined = CreateCombinedImageProcessingGroupMask(
                                roiGray,
                                GetImageProcessingStepsForRelation(relation)))
                            {
                                PaintRedOverlayImage(
                                    result,
                                    roi,
                                    ConvertOpenCvBinaryMask(combined));
                            }
                        }
                    }
                }

                return result;
            }
        }

        private Bitmap CreateRelationSourceBitmap(Bitmap original, ImageRelationSettings relation)
        {
            if (relation == null || string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
            {
                return new Bitmap(original);
            }

            using (Cv.Mat source = CreateOpenCvGrayMat(original))
            {
                PreprocessedImageResult preprocessingResult =
                    CreateOpenCvPreprocessedImageForRelationSource(source, relation);
                using (Cv.Mat preprocessed = preprocessingResult.Image)
                {
                    return CreateBitmapFromGrayMat(preprocessed);
                }
            }
        }

        private string CreateLargeProcessedRelationGroupMaskKey(Rectangle roi)
        {
            return string.Join(
                "|",
                "relation-group",
                activeImageRelationGroupId ?? string.Empty,
                roi.X.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Y.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Width.ToString(System.Globalization.CultureInfo.InvariantCulture),
                roi.Height.ToString(System.Globalization.CultureInfo.InvariantCulture));
        }

        private void PrepareLargeRelationGroupPreview()
        {
            bool needsPreprocessedSource = false;
            foreach (ImageRelationSettings relation in GetImageRelationGroupRelations(activeImageRelationGroupId))
            {
                if (!string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
                {
                    needsPreprocessedSource = true;
                    break;
                }
            }

            if (needsPreprocessedSource && HasConfiguredImagePreprocessingSteps() && preprocessedImageDirty)
            {
                RequestPreprocessedImageUpdate();
                return;
            }

            isSyncingImageView = true;
            LargeImageSource sharedSource = null;
            try
            {
                sharedSource = rightOriginalDisplayControl.GetSharedLargeImageSource();
                if (sharedSource == null)
                {
                    ClearProcessedPreviewImages();
                    return;
                }

                leftProcessedDisplayControl.SetSharedLargeImageSource(sharedSource, true);
                rightProcessedDisplayControl.SetSharedLargeImageSource(sharedSource, true);
                Rectangle? selectedRoi = GetSelectedRoi();
                if (selectedRoi.HasValue)
                {
                    leftProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                    rightProcessedDisplayControl.SetRoiOverlay(selectedRoi.Value);
                }

                foreach (RoiRegionSettings roiRegion in systemParameters.RoiRegions)
                {
                    StartLargeRelationGroupMaskBuild(sharedSource, roiRegion.Bounds);
                }
            }
            finally
            {
                if (sharedSource != null)
                {
                    sharedSource.ReleaseReference();
                }

                isSyncingImageView = false;
            }

            leftProcessedDisplayControl.ScheduleImageViewRefresh();
            rightProcessedDisplayControl.ScheduleImageViewRefresh();
            ApplySharedImageViewStateToVisibleControls();
            RestoreProcessedImageViewState();
            RestorePreprocessedImageViewState();
        }

        private void StartLargeRelationGroupMaskBuild(LargeImageSource originalSource, Rectangle roi)
        {
            if (originalSource == null || roi.Width <= 0 || roi.Height <= 0)
            {
                return;
            }

            string maskKey = CreateLargeProcessedRelationGroupMaskKey(roi);
            int generation;
            lock (largeProcessedMaskLock)
            {
                if (largeProcessedBinaryMasks.ContainsKey(maskKey) ||
                    largeProcessedMaskBuildKeys.Contains(maskKey))
                {
                    return;
                }

                generation = largeProcessedMaskGeneration;
                largeProcessedMaskBuildKeys.Add(maskKey);
            }

            LargeImageSource sourceReference = originalSource.AddReference();
            Task.Run(delegate
            {
                Cv.Mat combined = null;
                try
                {
                    largeNativeProcessingGate.Wait();
                    try
                    {
                        combined = new Cv.Mat(roi.Height, roi.Width, Cv.MatType.CV_8UC1, Cv.Scalar.All(0));
                        foreach (ImageRelationSettings relation in GetImageRelationGroupRelations(activeImageRelationGroupId))
                        {
                            LargeImageSource relationSource = GetLargeRelationSource(sourceReference, relation);
                            try
                            {
                                using (Cv.Mat gray = GetOrCreateLargeRoiOpenCvGrayCache(relationSource, roi))
                                {
                                    using (Cv.Mat relationMask = CreateCombinedImageProcessingGroupMask(
                                        gray,
                                        GetImageProcessingStepsForRelation(relation)))
                                    {
                                        Cv.Cv2.BitwiseOr(combined, relationMask, combined);
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
                    }
                    finally
                    {
                        largeNativeProcessingGate.Release();
                    }

                    Cv.Mat completed = combined;
                    combined = null;
                    BeginInvoke(new Action(delegate
                    {
                        lock (largeProcessedMaskLock)
                        {
                            if (generation != largeProcessedMaskGeneration ||
                                !largeProcessedMaskBuildKeys.Contains(maskKey))
                            {
                                completed.Dispose();
                                return;
                            }

                            largeProcessedBinaryMasks[maskKey] = completed;
                            largeProcessedMaskBuildKeys.Remove(maskKey);
                        }

                        QueueLargeProcessedBinaryOverview(
                            completed,
                            roi,
                            maskKey,
                            generation);
                        statusLabel.Text = "大圖關聯群組 MASK 建立完成";
                        leftProcessedDisplayControl.InvalidateImageView();
                        rightProcessedDisplayControl.InvalidateImageView();
                        InvalidateBlockProcessingDisplays();
                    }));
                }
                catch (Exception ex)
                {
                    Debug.WriteLine(ex);
                    if (combined != null)
                    {
                        combined.Dispose();
                    }

                    BeginInvoke(new Action(delegate
                    {
                        lock (largeProcessedMaskLock)
                        {
                            largeProcessedMaskBuildKeys.Remove(maskKey);
                        }

                        statusLabel.Text = "大圖關聯群組 MASK 建立失敗：" + ex.Message;
                    }));
                }
                finally
                {
                    sourceReference.ReleaseReference();
                }
            });
        }

        private LargeImageSource GetLargeRelationSource(LargeImageSource originalSource, ImageRelationSettings relation)
        {
            if (relation == null || string.Equals(relation.SourceType, "Original", StringComparison.Ordinal))
            {
                return originalSource.AddReference();
            }

            string cacheKey = (relation.SourceType ?? string.Empty) + "|" +
                (relation.SourceId ?? string.Empty);

            lock (largeRelationSourceCacheLock)
            {
                if (largeRelationSourceCacheGeneration == imageSourceGeneration)
                {
                    LargeImageSource cached;
                    if (largeRelationSourceCache.TryGetValue(cacheKey, out cached) && cached != null)
                    {
                        return cached.AddReference();
                    }
                }
                else
                {
                    ClearLargeRelationSourceCacheLocked();
                    largeRelationSourceCacheGeneration = imageSourceGeneration;
                }
            }

            Cv.Mat sourceGray = null;
            Cv.Mat processed = null;
            try
            {
                sourceGray = GetOrCreateLargeRoiOpenCvGrayCache(
                    originalSource,
                    new Rectangle(0, 0, originalSource.Width, originalSource.Height));
                PreprocessedImageResult preprocessingResult =
                    CreateOpenCvPreprocessedImageForRelationSource(sourceGray, relation);
                processed = preprocessingResult.Image;
                preprocessingResult.Image = null;
                var created = new LargeImageSource(processed);
                processed = null;

                lock (largeRelationSourceCacheLock)
                {
                    if (largeRelationSourceCacheGeneration != imageSourceGeneration)
                    {
                        ClearLargeRelationSourceCacheLocked();
                        largeRelationSourceCacheGeneration = imageSourceGeneration;
                    }

                    LargeImageSource existing;
                    if (largeRelationSourceCache.TryGetValue(cacheKey, out existing) && existing != null)
                    {
                        created.ReleaseReference();
                        return existing.AddReference();
                    }

                    largeRelationSourceCache[cacheKey] = created;
                    return created.AddReference();
                }
            }
            finally
            {
                if (sourceGray != null)
                {
                    sourceGray.Dispose();
                }

                if (processed != null)
                {
                    processed.Dispose();
                }
            }
        }

        private void ClearLargeRelationSourceCache()
        {
            lock (largeRelationSourceCacheLock)
            {
                ClearLargeRelationSourceCacheLocked();
                largeRelationSourceCacheGeneration = imageSourceGeneration;
            }
        }

        private void ClearLargeRelationSourceCacheLocked()
        {
            foreach (LargeImageSource source in largeRelationSourceCache.Values)
            {
                if (source != null)
                {
                    source.ReleaseReference();
                }
            }

            largeRelationSourceCache.Clear();
        }

        private void PaintLargeProcessedRelationGroupOverlayForRoi(
            LargeImageOverlayPaintEventArgs e,
            Rectangle roi,
            bool isPanning)
        {
            Rectangle visibleRoi = Rectangle.Intersect(e.VisibleSourceRect, roi);
            if (visibleRoi.Width <= 0 || visibleRoi.Height <= 0)
            {
                return;
            }

            string maskKey = CreateLargeProcessedRelationGroupMaskKey(roi);
            Cv.Mat binaryMask;
            bool isBuilding;
            lock (largeProcessedMaskLock)
            {
                largeProcessedBinaryMasks.TryGetValue(maskKey, out binaryMask);
                isBuilding = largeProcessedMaskBuildKeys.Contains(maskKey);
            }

            if (binaryMask == null)
            {
                if (!isBuilding)
                {
                    StartLargeRelationGroupMaskBuild(e.Source, roi);
                }

                return;
            }

            if (isPanning)
            {
                Bitmap overview;
                if (TryGetLargeProcessedOverlayFromCache("overview|" + maskKey, out overview))
                {
                    DrawLargeProcessedOverlayRegion(e.Graphics, overview, roi, visibleRoi, e.Zoom, e.Offset);
                }

                return;
            }

            var syntheticStep = new ImageProcessingStepSettings
            {
                Id = maskKey,
                Method = "Relation Group",
                Parameters = string.Empty
            };
            PaintLargeProcessedBinaryViewportOverlay(e, roi, visibleRoi, syntheticStep, maskKey, binaryMask);
        }
    }
}
