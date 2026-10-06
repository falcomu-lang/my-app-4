using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Drawing2D;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private sealed class ObjectDetectionDefectIntegrationCandidate
        {
            public string CoreKey { get; set; }

            public ObjectDetectionDefectCoreSettings Core { get; set; }

            public int ObjectNumber { get; set; }

            public RectangleF Bounds { get; set; }

            public bool? IsBright { get; set; }

            public bool IsFrequency { get; set; }

            public bool IsLineTexture { get; set; }

            public bool HasLineSegment { get; set; }

            public PointF LineStart { get; set; }

            public PointF LineEnd { get; set; }
        }

        private const byte ObjectDetectionDefectIntegrationDarkFlag = 1;
        private const byte ObjectDetectionDefectIntegrationBrightFlag = 2;
        private const byte ObjectDetectionDefectIntegrationFrequencyFlag = 4;
        private const byte ObjectDetectionDefectIntegrationLineTextureFlag = 8;

        private sealed class ObjectDetectionDefectIntegrationGroup
        {
            public int ObjectNumber { get; set; }

            public RectangleF Bounds { get; set; }

            public List<ObjectDetectionDefectIntegrationCandidate> Candidates { get; set; }
        }

        private readonly Dictionary<string, List<ObjectDetectionDefectIntegrationGroup>>
            objectDetectionDefectIntegrationCache =
                new Dictionary<string, List<ObjectDetectionDefectIntegrationGroup>>(StringComparer.Ordinal);

        private CheckBox objectDetectionDefectIntegrationDarkCheckBox;
        private CheckBox objectDetectionDefectIntegrationBrightCheckBox;
        private CheckBox objectDetectionDefectIntegrationMixedCheckBox;
        private CheckBox objectDetectionDefectIntegrationFrequencyCheckBox;
        private CheckBox objectDetectionDefectIntegrationLineTextureCheckBox;
        private NumericUpDown objectDetectionDefectIntegrationDistanceInput;
        private DataGridView objectDetectionDefectIntegrationResultsGrid;
        private Label objectDetectionDefectIntegrationResultsStatus;
        private string objectDetectionDefectIntegrationDraftParameterId;

        private TabPage BuildObjectDetectionDefectIntegrationTab(
            ObjectDetectionParameterSettings parameter)
        {
            var page = new TabPage("結果整合");
            var content = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(250, 251, 253)
            };
            objectDetectionDefectIntegrationDraftParameterId = parameter.Id;

            var mergeGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 160,
                Text = "缺陷合併方式",
                Padding = new Padding(8, 16, 8, 4)
            };
            var mergeOptions = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.TopDown,
                WrapContents = false,
                Padding = new Padding(2, 0, 0, 0),
                Margin = Padding.Empty
            };
            objectDetectionDefectIntegrationDarkCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "暗合併",
                Checked = parameter.DefectIntegrationMergeDark,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionDefectIntegrationBrightCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "亮合併",
                Checked = parameter.DefectIntegrationMergeBright,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionDefectIntegrationMixedCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "亮暗合併",
                Checked = parameter.DefectIntegrationMergeDarkBright,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionDefectIntegrationFrequencyCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "納入頻域異常",
                Checked = parameter.DefectIntegrationIncludeFrequency,
                Margin = new Padding(2, 1, 0, 1)
            };
            objectDetectionDefectIntegrationLineTextureCheckBox = new CheckBox
            {
                AutoSize = true,
                Text = "納入線狀紋理異常",
                Checked = parameter.DefectIntegrationIncludeLineTexture,
                Margin = new Padding(2, 1, 0, 1)
            };
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationDarkCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationBrightCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationMixedCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationFrequencyCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationLineTextureCheckBox);
            mergeGroup.Controls.Add(mergeOptions);

            var distanceGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 66,
                Text = "附近判定",
                Padding = new Padding(8, 16, 8, 4)
            };
            distanceGroup.Controls.Add(new Label
            {
                AutoSize = true,
                Text = "合併距離 (px)",
                Location = new Point(8, 23)
            });
            objectDetectionDefectIntegrationDistanceInput = new NumericUpDown
            {
                Minimum = 0,
                Maximum = 1000000,
                DecimalPlaces = 1,
                Increment = 1,
                Value = (decimal)Math.Max(0, Math.Min(
                    1000000,
                    parameter.DefectIntegrationMergeDistancePixels)),
                Width = 112,
                Location = new Point(116, 19),
                Anchor = AnchorStyles.Top | AnchorStyles.Left
            };
            distanceGroup.Controls.Add(objectDetectionDefectIntegrationDistanceInput);

            var resultsGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 250,
                Text = "ROI 整合結果",
                Padding = new Padding(6, 16, 6, 6)
            };
            objectDetectionDefectIntegrationResultsGrid = CreateObjectDetectionDefectIntegrationResultsGrid();
            objectDetectionDefectIntegrationResultsStatus = new Label
            {
                Dock = DockStyle.Bottom,
                Height = 20,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95),
                TextAlign = ContentAlignment.MiddleLeft
            };
            resultsGroup.Controls.Add(objectDetectionDefectIntegrationResultsGrid);
            resultsGroup.Controls.Add(objectDetectionDefectIntegrationResultsStatus);
            var actionBar = new FlowLayoutPanel
            {
                Dock = DockStyle.Bottom,
                Height = 38,
                FlowDirection = FlowDirection.RightToLeft,
                WrapContents = false,
                Padding = new Padding(2, 4, 2, 2),
                Margin = Padding.Empty
            };
            var cancel = new Button
            {
                Text = "取消",
                Width = 78,
                Height = 27,
                Margin = new Padding(3, 0, 0, 0)
            };
            var apply = new Button
            {
                Text = "套用",
                Width = 78,
                Height = 27,
                Margin = new Padding(3, 0, 0, 0)
            };
            cancel.Click += delegate { CancelObjectDetectionDefectIntegration(parameter); };
            apply.Click += delegate { ApplyObjectDetectionDefectIntegration(parameter); };
            actionBar.Controls.Add(cancel);
            actionBar.Controls.Add(apply);
            resultsGroup.Controls.Add(actionBar);

            content.Controls.Add(resultsGroup);
            content.Controls.Add(distanceGroup);
            content.Controls.Add(mergeGroup);
            page.Controls.Add(content);
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            return page;
        }

        private static DataGridView CreateObjectDetectionDefectIntegrationResultsGrid()
        {
            var grid = new DataGridView
            {
                Dock = DockStyle.Fill,
                ReadOnly = true,
                AllowUserToAddRows = false,
                AllowUserToDeleteRows = false,
                AllowUserToResizeRows = false,
                RowHeadersVisible = false,
                MultiSelect = false,
                SelectionMode = DataGridViewSelectionMode.FullRowSelect,
                AutoSizeColumnsMode = DataGridViewAutoSizeColumnsMode.Fill,
                BackgroundColor = Color.White,
                BorderStyle = BorderStyle.FixedSingle
            };
            grid.Columns.Add("Number", "編號");
            grid.Columns.Add("ObjectNumber", "ROI");
            grid.Columns.Add("Type", "類型");
            grid.Columns.Add("CandidateCount", "候選數");
            grid.Columns.Add("Sources", "來源條件");
            grid.Columns.Add("X", "X (px)");
            grid.Columns.Add("Y", "Y (px)");
            grid.Columns.Add("Width", "寬 (px)");
            grid.Columns.Add("Height", "高 (px)");
            grid.Columns[0].FillWeight = 45;
            grid.Columns[1].FillWeight = 45;
            grid.Columns[2].FillWeight = 55;
            grid.Columns[3].FillWeight = 55;
            grid.Columns[4].FillWeight = 125;
            for (int index = 5; index < grid.Columns.Count; index++)
            {
                grid.Columns[index].FillWeight = 65;
            }
            return grid;
        }

        private void ApplyObjectDetectionDefectIntegration(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionDefectIntegrationDraftParameterId,
                    parameter.Id, StringComparison.Ordinal) ||
                objectDetectionDefectIntegrationDarkCheckBox == null ||
                objectDetectionDefectIntegrationBrightCheckBox == null ||
                objectDetectionDefectIntegrationMixedCheckBox == null ||
                objectDetectionDefectIntegrationFrequencyCheckBox == null ||
                objectDetectionDefectIntegrationLineTextureCheckBox == null ||
                objectDetectionDefectIntegrationDistanceInput == null)
            {
                return;
            }

            parameter.DefectIntegrationMergeDark =
                objectDetectionDefectIntegrationDarkCheckBox.Checked;
            parameter.DefectIntegrationMergeBright =
                objectDetectionDefectIntegrationBrightCheckBox.Checked;
            parameter.DefectIntegrationMergeDarkBright =
                objectDetectionDefectIntegrationMixedCheckBox.Checked;
            parameter.DefectIntegrationIncludeFrequency =
                objectDetectionDefectIntegrationFrequencyCheckBox.Checked;
            parameter.DefectIntegrationIncludeLineTexture =
                objectDetectionDefectIntegrationLineTextureCheckBox.Checked;
            parameter.DefectIntegrationMergeDistancePixels =
                (double)objectDetectionDefectIntegrationDistanceInput.Value;
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            SaveSystemParameters();
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(
                ObjectDetectionDefectIntegratedDisplayIndex);
            if (display != null)
            {
                display.InvalidateImageView();
            }
            SetObjectDetectionDefectRegionStatus("已套用結果整合設定；核心檢測結果未重新運算。");
        }

        private void CancelObjectDetectionDefectIntegration(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null ||
                !string.Equals(objectDetectionDefectIntegrationDraftParameterId,
                    parameter.Id, StringComparison.Ordinal))
            {
                return;
            }

            objectDetectionDefectIntegrationDarkCheckBox.Checked =
                parameter.DefectIntegrationMergeDark;
            objectDetectionDefectIntegrationBrightCheckBox.Checked =
                parameter.DefectIntegrationMergeBright;
            objectDetectionDefectIntegrationMixedCheckBox.Checked =
                parameter.DefectIntegrationMergeDarkBright;
            objectDetectionDefectIntegrationFrequencyCheckBox.Checked =
                parameter.DefectIntegrationIncludeFrequency;
            objectDetectionDefectIntegrationLineTextureCheckBox.Checked =
                parameter.DefectIntegrationIncludeLineTexture;
            objectDetectionDefectIntegrationDistanceInput.Value =
                (decimal)Math.Max(0, Math.Min(
                    1000000,
                    parameter.DefectIntegrationMergeDistancePixels));
            RefreshObjectDetectionDefectIntegrationResults(parameter);
        }

        private void RefreshObjectDetectionDefectIntegrationResults(
            ObjectDetectionParameterSettings parameter,
            Action<long> computationElapsed = null)
        {
            DataGridView grid = objectDetectionDefectIntegrationResultsGrid;
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            grid.Rows.Clear();
            if (parameter == null)
            {
                return;
            }

            Stopwatch computationStopwatch = Stopwatch.StartNew();
            List<ObjectDetectionDefectIntegrationGroup> allGroups =
                GetObjectDetectionDefectIntegrationGroups(parameter);
            computationStopwatch.Stop();
            if (computationElapsed != null)
            {
                computationElapsed(computationStopwatch.ElapsedMilliseconds);
            }
            List<ObjectDetectionDefectIntegrationGroup> groups = allGroups
                    .Where(group => selectedObjectDetectionNumber <= 0 ||
                        group.ObjectNumber == selectedObjectDetectionNumber)
                    .OrderBy(group => group.ObjectNumber)
                    .ThenBy(group => group.Bounds.Top)
                    .ThenBy(group => group.Bounds.Left)
                    .ToList();
            for (int index = 0; index < groups.Count; index++)
            {
                ObjectDetectionDefectIntegrationGroup group = groups[index];
                bool hasDark = group.Candidates.Any(item => item.IsBright == false);
                bool hasBright = group.Candidates.Any(item => item.IsBright == true);
                bool hasFrequency = group.Candidates.Any(item => item.IsFrequency);
                bool hasLineTexture = group.Candidates.Any(item => item.IsLineTexture);
                var typeParts = new List<string>();
                if (hasDark && hasBright) typeParts.Add("亮暗");
                else if (hasBright) typeParts.Add("亮");
                else if (hasDark) typeParts.Add("暗");
                if (hasFrequency) typeParts.Add("頻域");
                if (hasLineTexture) typeParts.Add("線狀紋理");
                string type = string.Join("+", typeParts);
                string sources = string.Join("、", group.Candidates
                    .Select(item => item.IsFrequency
                        ? "頻域異常"
                        : item.IsLineTexture
                        ? "線狀紋理異常"
                            : GetObjectDetectionDefectCoreLabel(
                                GetObjectDetectionDefectCoreIndex(item.CoreKey)))
                    .Distinct(StringComparer.Ordinal));
                grid.Rows.Add(
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    group.ObjectNumber.ToString(CultureInfo.InvariantCulture),
                    type,
                    group.Candidates.Count.ToString(CultureInfo.InvariantCulture),
                    sources,
                    Math.Round(group.Bounds.Left).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Top).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Width).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Height).ToString(CultureInfo.InvariantCulture));
            }

            bool frequencyRequired = IsObjectDetectionDefectFrequencyIntegrationRequired(parameter);
            ObjectDetectionFrequencyResult frequencyResult;
            bool frequencyReady = !frequencyRequired ||
                TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult);
            bool lineTextureRequired = IsObjectDetectionDefectLineTextureIntegrationRequired(parameter);
            ObjectDetectionLineTextureResult lineTextureResult;
            bool lineTextureReady = !lineTextureRequired || TryGetCurrentObjectDetectionLineTextureResult(parameter, out lineTextureResult);
            string pendingAnalysisMessage = !frequencyReady && !lineTextureReady
                ? "頻域異常與線狀紋理分析尚未完成；整合判定待確認。"
                : !frequencyReady
                    ? "已勾選納入頻域異常，但目前影像的頻域分析尚未完成；整合判定待確認。"
                    : !lineTextureReady
                        ? "已勾選納入線狀紋理異常，但目前影像的分析尚未完成；整合判定待確認。"
                        : null;
            SetObjectDetectionDefectIntegrationResultsStatus(
                pendingAnalysisMessage != null
                    ? pendingAnalysisMessage
                    : parameter.DefectIntegrationIncludeFrequency && !parameter.DefectFrequencyEnabled
                    ? "已勾選納入頻域異常，但頻域分析目前停用；本次僅整合其他缺陷來源。"
                    : parameter.DefectIntegrationIncludeLineTexture && !parameter.DefectLineTextureEnabled
                    ? "已勾選納入線狀紋理異常，但該分析目前停用；本次僅整合其他缺陷來源。"
                    : groups.Count == 0
                    ? selectedObjectDetectionNumber > 0
                        ? "物件" + selectedObjectDetectionNumber.ToString(CultureInfo.InvariantCulture) +
                            " 尚無符合目前設定的缺陷結果"
                        : "目前沒有符合設定的 ROI 整合缺陷結果"
                    : selectedObjectDetectionNumber > 0
                        ? "物件" + selectedObjectDetectionNumber.ToString(CultureInfo.InvariantCulture) +
                            " 整合出 " + groups.Count.ToString("N0", CultureInfo.CurrentCulture) + " 組缺陷"
                        : "全部 ROI 共整合出 " + groups.Count.ToString("N0", CultureInfo.CurrentCulture) + " 組缺陷");
        }

        private void SetObjectDetectionDefectIntegrationResultsStatus(string text)
        {
            if (objectDetectionDefectIntegrationResultsStatus != null &&
                !objectDetectionDefectIntegrationResultsStatus.IsDisposed)
            {
                objectDetectionDefectIntegrationResultsStatus.Text = text ?? string.Empty;
            }
        }

        private void InvalidateObjectDetectionDefectIntegrationCache(string parameterId)
        {
            if (string.IsNullOrWhiteSpace(parameterId))
            {
                objectDetectionDefectIntegrationCache.Clear();
            }
            else
            {
                objectDetectionDefectIntegrationCache.Remove(parameterId);
            }
        }

        private static bool IsObjectDetectionDefectFrequencyIntegrationRequired(
            ObjectDetectionParameterSettings parameter)
        {
            return parameter != null && parameter.DefectIntegrationIncludeFrequency &&
                parameter.DefectFrequencyEnabled;
        }

        private List<ObjectDetectionDefectIntegrationGroup> GetObjectDetectionDefectIntegrationGroups(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return new List<ObjectDetectionDefectIntegrationGroup>();
            }

            bool includeFrequency = IsObjectDetectionDefectFrequencyIntegrationRequired(parameter);
            ObjectDetectionFrequencyResult frequencyResult = null;
            bool frequencyResultAvailable = includeFrequency &&
                TryGetCurrentObjectDetectionFrequencyResult(parameter, out frequencyResult);
            List<ObjectDetectionDefectIntegrationGroup> cached;
            if (objectDetectionDefectIntegrationCache.TryGetValue(parameter.Id, out cached))
            {
                return cached;
            }

            EnsureObjectDetectionDefectCores(parameter);
            var candidates = new List<ObjectDetectionDefectIntegrationCandidate>();
            foreach (ObjectDetectionDefectCoreSettings core in parameter.DefectDetectionCores
                .Take(ObjectDetectionDefectCoreKeys.Length)
                .Where(item => item != null &&
                    (string.Equals(item.CoreKey, "FlatField", StringComparison.Ordinal) || item.Enabled)))
            {
                ObjectDetectionDefectCoreResult result;
                if (!TryGetObjectDetectionDefectCoreResult(parameter, core.CoreKey, out result) ||
                    result.Contours == null)
                {
                    continue;
                }

                candidates.AddRange(result.Contours
                    .Where(contour => contour != null && contour.ObjectNumber > 0)
                    .Select(contour => new ObjectDetectionDefectIntegrationCandidate
                    {
                        CoreKey = core.CoreKey,
                        Core = core,
                        ObjectNumber = contour.ObjectNumber,
                        Bounds = contour.Bounds,
                        IsBright = contour.IsBright
                    }));
            }

            if (frequencyResultAvailable)
            {
                if (frequencyResult.Cells != null)
                {
                    candidates.AddRange(frequencyResult.Cells
                        .Where(cell => cell != null && cell.IsAnomaly && cell.ObjectNumber > 0)
                        .Select(cell => new ObjectDetectionDefectIntegrationCandidate
                        {
                            CoreKey = "Frequency",
                            ObjectNumber = cell.ObjectNumber,
                            Bounds = cell.ValidBounds,
                            IsFrequency = true
                        }));
                }
            }

            bool includeLineTexture = IsObjectDetectionDefectLineTextureIntegrationRequired(parameter);
            ObjectDetectionLineTextureResult lineTextureResult = null;
            bool lineTextureResultAvailable = includeLineTexture &&
                TryGetCurrentObjectDetectionLineTextureResult(parameter, out lineTextureResult);
            if (lineTextureResultAvailable && lineTextureResult.Lines != null)
            {
                candidates.AddRange(lineTextureResult.Lines
                    .Where(line => line != null && line.IsAnomaly && line.ObjectNumber > 0)
                    .Select(line => new ObjectDetectionDefectIntegrationCandidate
                    {
                        CoreKey = "LineTexture",
                        ObjectNumber = line.ObjectNumber,
                        Bounds = line.Bounds,
                        IsLineTexture = true,
                        HasLineSegment = true,
                        LineStart = line.Start,
                        LineEnd = line.End
                    }));
            }

            List<ObjectDetectionDefectIntegrationGroup> groups =
                MergeObjectDetectionDefectIntegrationCandidates(parameter, candidates);
            objectDetectionDefectIntegrationCache[parameter.Id] = groups;
            return groups;
        }

        private static List<ObjectDetectionDefectIntegrationGroup>
            MergeObjectDetectionDefectIntegrationCandidates(
                ObjectDetectionParameterSettings parameter,
                List<ObjectDetectionDefectIntegrationCandidate> candidates)
        {
            if (candidates == null || candidates.Count == 0)
            {
                return new List<ObjectDetectionDefectIntegrationGroup>();
            }

            int[] parents = new int[candidates.Count];
            var sourceFlags = new byte[candidates.Count];
            for (int index = 0; index < parents.Length; index++)
            {
                parents[index] = index;
                ObjectDetectionDefectIntegrationCandidate candidate = candidates[index];
                sourceFlags[index] = candidate.IsFrequency
                    ? ObjectDetectionDefectIntegrationFrequencyFlag
                    : candidate.IsLineTexture
                        ? ObjectDetectionDefectIntegrationLineTextureFlag
                    : candidate.IsBright == true
                        ? ObjectDetectionDefectIntegrationBrightFlag
                        : ObjectDetectionDefectIntegrationDarkFlag;
            }

            double distance = Math.Max(0, Math.Min(
                1000000,
                parameter.DefectIntegrationMergeDistancePixels));
            double distanceSquared = distance * distance;
            foreach (IGrouping<int, int> roiCandidates in Enumerable.Range(0, candidates.Count)
                .GroupBy(index => candidates[index].ObjectNumber))
            {
                List<int> orderedIndexes = roiCandidates
                    .OrderBy(index => candidates[index].Bounds.Left)
                    .ToList();
                var activeIndexes = new List<int>();
                foreach (int currentIndex in orderedIndexes)
                {
                    RectangleF currentBounds = candidates[currentIndex].Bounds;
                    for (int activeIndex = activeIndexes.Count - 1; activeIndex >= 0; activeIndex--)
                    {
                        int previousIndex = activeIndexes[activeIndex];
                        if (candidates[previousIndex].Bounds.Right + distance < currentBounds.Left)
                        {
                            activeIndexes.RemoveAt(activeIndex);
                        }
                    }

                    foreach (int previousIndex in activeIndexes)
                    {
                        int previousRoot = FindObjectDetectionDefectIntegrationParent(
                            parents,
                            previousIndex);
                        int currentRoot = FindObjectDetectionDefectIntegrationParent(
                            parents,
                            currentIndex);
                        if (previousRoot == currentRoot ||
                            !CanMergeObjectDetectionDefectIntegrationRoots(
                                parameter,
                                sourceFlags[previousRoot],
                                sourceFlags[currentRoot]))
                        {
                            continue;
                        }
                        if (GetObjectDetectionDefectCandidateGapSquared(
                            candidates[previousIndex],
                            candidates[currentIndex]) <= distanceSquared)
                        {
                            byte mergedFlags = (byte)(sourceFlags[previousRoot] |
                                sourceFlags[currentRoot]);
                            UnionObjectDetectionDefectIntegrationParents(
                                parents,
                                previousRoot,
                                currentRoot);
                            int mergedRoot = FindObjectDetectionDefectIntegrationParent(
                                parents,
                                previousRoot);
                            sourceFlags[mergedRoot] = mergedFlags;
                        }
                    }

                    activeIndexes.Add(currentIndex);
                }
            }

            var byRoot = new Dictionary<int, ObjectDetectionDefectIntegrationGroup>();
            for (int index = 0; index < candidates.Count; index++)
            {
                int root = FindObjectDetectionDefectIntegrationParent(parents, index);
                ObjectDetectionDefectIntegrationGroup group;
                if (!byRoot.TryGetValue(root, out group))
                {
                    group = new ObjectDetectionDefectIntegrationGroup
                    {
                        ObjectNumber = candidates[index].ObjectNumber,
                        Bounds = candidates[index].Bounds,
                        Candidates = new List<ObjectDetectionDefectIntegrationCandidate>()
                    };
                    byRoot[root] = group;
                }
                else
                {
                    group.Bounds = RectangleF.Union(group.Bounds, candidates[index].Bounds);
                }
                group.Candidates.Add(candidates[index]);
            }

            return byRoot.Values
                .OrderBy(group => group.ObjectNumber)
                .ThenBy(group => group.Bounds.Top)
                .ThenBy(group => group.Bounds.Left)
                .ToList();
        }

        private static bool CanMergeObjectDetectionDefectIntegrationRoots(
            ObjectDetectionParameterSettings parameter,
            byte firstFlags,
            byte secondFlags)
        {
            bool firstHasDark = (firstFlags & ObjectDetectionDefectIntegrationDarkFlag) != 0;
            bool firstHasBright = (firstFlags & ObjectDetectionDefectIntegrationBrightFlag) != 0;
            bool secondHasDark = (secondFlags & ObjectDetectionDefectIntegrationDarkFlag) != 0;
            bool secondHasBright = (secondFlags & ObjectDetectionDefectIntegrationBrightFlag) != 0;
            if (firstHasDark && secondHasDark && !parameter.DefectIntegrationMergeDark)
            {
                return false;
            }
            if (firstHasBright && secondHasBright && !parameter.DefectIntegrationMergeBright)
            {
                return false;
            }
            bool hasCrossPolarity = firstHasDark && secondHasBright ||
                firstHasBright && secondHasDark;
            return !hasCrossPolarity || parameter.DefectIntegrationMergeDarkBright;
        }

        private static double GetObjectDetectionDefectBoundsGapSquared(
            RectangleF first,
            RectangleF second)
        {
            double dx = first.Right < second.Left
                ? second.Left - first.Right
                : second.Right < first.Left ? first.Left - second.Right : 0;
            double dy = first.Bottom < second.Top
                ? second.Top - first.Bottom
                : second.Bottom < first.Top ? first.Top - second.Bottom : 0;
            return (dx * dx) + (dy * dy);
        }

        private static double GetObjectDetectionDefectCandidateGapSquared(
            ObjectDetectionDefectIntegrationCandidate first,
            ObjectDetectionDefectIntegrationCandidate second)
        {
            if (first.HasLineSegment && second.HasLineSegment)
            {
                return GetObjectDetectionLineSegmentGapSquared(
                    first.LineStart, first.LineEnd, second.LineStart, second.LineEnd);
            }
            if (first.HasLineSegment)
            {
                return GetObjectDetectionLineRectangleGapSquared(
                    first.LineStart, first.LineEnd, second.Bounds);
            }
            if (second.HasLineSegment)
            {
                return GetObjectDetectionLineRectangleGapSquared(
                    second.LineStart, second.LineEnd, first.Bounds);
            }
            return GetObjectDetectionDefectBoundsGapSquared(first.Bounds, second.Bounds);
        }

        private static double GetObjectDetectionLineSegmentGapSquared(
            PointF firstStart,
            PointF firstEnd,
            PointF secondStart,
            PointF secondEnd)
        {
            if (DoObjectDetectionLineSegmentsIntersect(firstStart, firstEnd, secondStart, secondEnd)) return 0;
            return Math.Min(
                Math.Min(GetObjectDetectionPointSegmentDistanceSquared(firstStart, secondStart, secondEnd),
                    GetObjectDetectionPointSegmentDistanceSquared(firstEnd, secondStart, secondEnd)),
                Math.Min(GetObjectDetectionPointSegmentDistanceSquared(secondStart, firstStart, firstEnd),
                    GetObjectDetectionPointSegmentDistanceSquared(secondEnd, firstStart, firstEnd)));
        }

        private static double GetObjectDetectionLineRectangleGapSquared(
            PointF start,
            PointF end,
            RectangleF rectangle)
        {
            if (rectangle.Contains(start) || rectangle.Contains(end)) return 0;
            PointF topLeft = new PointF(rectangle.Left, rectangle.Top);
            PointF topRight = new PointF(rectangle.Right, rectangle.Top);
            PointF bottomLeft = new PointF(rectangle.Left, rectangle.Bottom);
            PointF bottomRight = new PointF(rectangle.Right, rectangle.Bottom);
            if (DoObjectDetectionLineSegmentsIntersect(start, end, topLeft, topRight) ||
                DoObjectDetectionLineSegmentsIntersect(start, end, topRight, bottomRight) ||
                DoObjectDetectionLineSegmentsIntersect(start, end, bottomRight, bottomLeft) ||
                DoObjectDetectionLineSegmentsIntersect(start, end, bottomLeft, topLeft)) return 0;

            double best = Math.Min(GetObjectDetectionPointRectangleDistanceSquared(start, rectangle),
                GetObjectDetectionPointRectangleDistanceSquared(end, rectangle));
            best = Math.Min(best, GetObjectDetectionPointSegmentDistanceSquared(topLeft, start, end));
            best = Math.Min(best, GetObjectDetectionPointSegmentDistanceSquared(topRight, start, end));
            best = Math.Min(best, GetObjectDetectionPointSegmentDistanceSquared(bottomLeft, start, end));
            return Math.Min(best, GetObjectDetectionPointSegmentDistanceSquared(bottomRight, start, end));
        }

        private static double GetObjectDetectionPointRectangleDistanceSquared(PointF point, RectangleF rectangle)
        {
            double dx = point.X < rectangle.Left ? rectangle.Left - point.X
                : point.X > rectangle.Right ? point.X - rectangle.Right : 0;
            double dy = point.Y < rectangle.Top ? rectangle.Top - point.Y
                : point.Y > rectangle.Bottom ? point.Y - rectangle.Bottom : 0;
            return dx * dx + dy * dy;
        }

        private static double GetObjectDetectionPointSegmentDistanceSquared(
            PointF point,
            PointF start,
            PointF end)
        {
            double dx = end.X - start.X;
            double dy = end.Y - start.Y;
            double lengthSquared = dx * dx + dy * dy;
            if (lengthSquared < 1e-12)
            {
                double px = point.X - start.X;
                double py = point.Y - start.Y;
                return px * px + py * py;
            }
            double amount = ((point.X - start.X) * dx + (point.Y - start.Y) * dy) / lengthSquared;
            amount = Math.Max(0, Math.Min(1, amount));
            double distanceX = point.X - (start.X + amount * dx);
            double distanceY = point.Y - (start.Y + amount * dy);
            return distanceX * distanceX + distanceY * distanceY;
        }

        private static bool DoObjectDetectionLineSegmentsIntersect(
            PointF firstStart,
            PointF firstEnd,
            PointF secondStart,
            PointF secondEnd)
        {
            double first = GetObjectDetectionCrossProduct(firstStart, firstEnd, secondStart);
            double second = GetObjectDetectionCrossProduct(firstStart, firstEnd, secondEnd);
            double third = GetObjectDetectionCrossProduct(secondStart, secondEnd, firstStart);
            double fourth = GetObjectDetectionCrossProduct(secondStart, secondEnd, firstEnd);
            const double epsilon = 1e-6;
            if (Math.Abs(first) <= epsilon && IsObjectDetectionPointOnSegment(secondStart, firstStart, firstEnd)) return true;
            if (Math.Abs(second) <= epsilon && IsObjectDetectionPointOnSegment(secondEnd, firstStart, firstEnd)) return true;
            if (Math.Abs(third) <= epsilon && IsObjectDetectionPointOnSegment(firstStart, secondStart, secondEnd)) return true;
            if (Math.Abs(fourth) <= epsilon && IsObjectDetectionPointOnSegment(firstEnd, secondStart, secondEnd)) return true;
            bool firstCrosses = first > epsilon && second < -epsilon || first < -epsilon && second > epsilon;
            bool secondCrosses = third > epsilon && fourth < -epsilon || third < -epsilon && fourth > epsilon;
            return firstCrosses && secondCrosses;
        }

        private static double GetObjectDetectionCrossProduct(PointF origin, PointF end, PointF point)
        {
            return (end.X - origin.X) * (point.Y - origin.Y) -
                (end.Y - origin.Y) * (point.X - origin.X);
        }

        private static bool IsObjectDetectionPointOnSegment(PointF point, PointF start, PointF end)
        {
            const double epsilon = 1e-6;
            return point.X >= Math.Min(start.X, end.X) - epsilon &&
                point.X <= Math.Max(start.X, end.X) + epsilon &&
                point.Y >= Math.Min(start.Y, end.Y) - epsilon &&
                point.Y <= Math.Max(start.Y, end.Y) + epsilon;
        }

        private static int FindObjectDetectionDefectIntegrationParent(int[] parents, int index)
        {
            int root = index;
            while (parents[root] != root)
            {
                root = parents[root];
            }
            while (parents[index] != index)
            {
                int next = parents[index];
                parents[index] = root;
                index = next;
            }
            return root;
        }

        private static void UnionObjectDetectionDefectIntegrationParents(
            int[] parents,
            int first,
            int second)
        {
            int firstRoot = FindObjectDetectionDefectIntegrationParent(parents, first);
            int secondRoot = FindObjectDetectionDefectIntegrationParent(parents, second);
            if (firstRoot != secondRoot)
            {
                parents[secondRoot] = firstRoot;
            }
        }

        private List<ObjectDetectionDefectIntegrationGroup> GetObjectDetectionDefectIntegrationGroupsForDisplay(
            ObjectDetectionParameterSettings parameter)
        {
            return GetObjectDetectionDefectIntegrationGroups(parameter);
        }

        private void DrawObjectDetectionDefectIntegrationGroups(
            Graphics graphics,
            float zoom,
            PointF offset,
            Rectangle visibleSourceRect,
            ObjectDetectionParameterSettings parameter)
        {
            if (graphics == null || parameter == null || zoom <= 0)
            {
                return;
            }

            RectangleF visibleBounds = visibleSourceRect;
            using (var darkPen = new Pen(Color.Red, Math.Max(1f, 2f * zoom)))
            using (var brightPen = new Pen(Color.DarkOrange, Math.Max(1f, 2f * zoom)))
            using (var mixedPen = new Pen(Color.Magenta, Math.Max(1f, 2f * zoom)))
            using (var frequencyPen = new Pen(Color.DeepSkyBlue, Math.Max(1f, 2f * zoom)))
            using (var darkFrequencyPen = new Pen(Color.Red, Math.Max(1f, 2f * zoom))
            {
                DashStyle = DashStyle.Dash
            })
            using (var brightFrequencyPen = new Pen(Color.DarkOrange, Math.Max(1f, 2f * zoom))
            {
                DashStyle = DashStyle.Dash
            })
            using (var mixedFrequencyPen = new Pen(Color.Magenta, Math.Max(1f, 2f * zoom))
            {
                DashStyle = DashStyle.Dash
            })
            using (var lineTexturePen = new Pen(Color.LimeGreen, Math.Max(1f, 2f * zoom))
            {
                DashStyle = DashStyle.DashDot
            })
            using (var lineTexturePolarityPen = new Pen(Color.Magenta, Math.Max(1f, 2f * zoom))
            {
                DashStyle = DashStyle.DashDot
            })
            {
                foreach (ObjectDetectionDefectIntegrationGroup group in
                    GetObjectDetectionDefectIntegrationGroupsForDisplay(parameter))
                {
                    if (!group.Bounds.IntersectsWith(visibleBounds))
                    {
                        continue;
                    }

                    bool showDark = group.Candidates.Any(candidate =>
                        candidate.IsBright == false && candidate.Core != null &&
                        candidate.Core.ShowRedBoxes);
                    bool showBright = group.Candidates.Any(candidate =>
                        candidate.IsBright == true && candidate.Core != null &&
                        candidate.Core.ShowOrangeBoxes);
                    bool showFrequency = group.Candidates.Any(candidate => candidate.IsFrequency);
                    bool showLineTexture = group.Candidates.Any(candidate => candidate.IsLineTexture);
                    if (!showDark && !showBright && !showFrequency && !showLineTexture)
                    {
                        continue;
                    }

                    Pen pen = showDark && showBright
                        ? showLineTexture ? lineTexturePolarityPen : showFrequency ? mixedFrequencyPen : mixedPen
                        : showDark ? showLineTexture ? lineTexturePolarityPen : showFrequency ? darkFrequencyPen : darkPen
                        : showBright ? showLineTexture ? lineTexturePolarityPen : showFrequency ? brightFrequencyPen : brightPen
                        : showLineTexture ? lineTexturePen : frequencyPen;
                    graphics.DrawRectangle(
                        pen,
                        offset.X + group.Bounds.X * zoom,
                        offset.Y + group.Bounds.Y * zoom,
                        Math.Max(1f, group.Bounds.Width * zoom),
                        Math.Max(1f, group.Bounds.Height * zoom));
                    foreach (ObjectDetectionDefectIntegrationCandidate candidate in group.Candidates.Where(item => item.HasLineSegment))
                    {
                        graphics.DrawLine(
                            pen,
                            offset.X + candidate.LineStart.X * zoom,
                            offset.Y + candidate.LineStart.Y * zoom,
                            offset.X + candidate.LineEnd.X * zoom,
                            offset.Y + candidate.LineEnd.Y * zoom);
                    }
                }
            }
        }
    }
}
