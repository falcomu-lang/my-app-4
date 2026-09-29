using System;
using System.Collections.Generic;
using System.Drawing;
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

            public ObjectDetectionDefectContour Contour { get; set; }
        }

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
        private NumericUpDown objectDetectionDefectIntegrationDistanceInput;
        private FlowLayoutPanel objectDetectionDefectIntegrationRoiSelector;
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
                Height = 112,
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
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationDarkCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationBrightCheckBox);
            mergeOptions.Controls.Add(objectDetectionDefectIntegrationMixedCheckBox);
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

            var roiGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 108,
                Text = "ROI 結果",
                Padding = new Padding(8, 16, 8, 4)
            };
            objectDetectionDefectIntegrationRoiSelector = new FlowLayoutPanel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                WrapContents = true,
                FlowDirection = FlowDirection.LeftToRight,
                Padding = new Padding(2),
                Margin = Padding.Empty
            };
            roiGroup.Controls.Add(objectDetectionDefectIntegrationRoiSelector);

            var resultsGroup = new GroupBox
            {
                Dock = DockStyle.Top,
                Height = 190,
                Text = "整合結果",
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

            content.Controls.Add(resultsGroup);
            content.Controls.Add(roiGroup);
            content.Controls.Add(distanceGroup);
            content.Controls.Add(mergeGroup);
            content.Controls.Add(actionBar);
            page.Controls.Add(content);
            RefreshObjectDetectionDefectIntegrationRoiSelector(parameter);
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
            grid.Columns.Add("Type", "類型");
            grid.Columns.Add("CandidateCount", "候選數");
            grid.Columns.Add("Sources", "來源條件");
            grid.Columns.Add("X", "X (px)");
            grid.Columns.Add("Y", "Y (px)");
            grid.Columns.Add("Width", "寬 (px)");
            grid.Columns.Add("Height", "高 (px)");
            grid.Columns[0].FillWeight = 45;
            grid.Columns[1].FillWeight = 55;
            grid.Columns[2].FillWeight = 55;
            grid.Columns[3].FillWeight = 125;
            for (int index = 4; index < grid.Columns.Count; index++)
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
            parameter.DefectIntegrationMergeDistancePixels =
                (double)objectDetectionDefectIntegrationDistanceInput.Value;
            InvalidateObjectDetectionDefectIntegrationCache(parameter.Id);
            SaveSystemParameters();
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(4);
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
            objectDetectionDefectIntegrationDistanceInput.Value =
                (decimal)Math.Max(0, Math.Min(
                    1000000,
                    parameter.DefectIntegrationMergeDistancePixels));
            RefreshObjectDetectionDefectIntegrationResults(parameter);
        }

        private void RefreshObjectDetectionDefectIntegrationRoiSelector(
            ObjectDetectionParameterSettings parameter)
        {
            FlowLayoutPanel selector = objectDetectionDefectIntegrationRoiSelector;
            if (selector == null || selector.IsDisposed)
            {
                return;
            }

            selector.SuspendLayout();
            try
            {
                selector.Controls.Clear();
                List<ObjectDefinitionDetectedObject> objects =
                    GetObjectDetectionDefectIntegrationObjects(parameter);
                foreach (int number in objects.Select(item => item.Number)
                    .Where(number => number > 0)
                    .Distinct()
                    .OrderBy(number => number))
                {
                    var button = new Button
                    {
                        Text = number.ToString(CultureInfo.InvariantCulture),
                        Tag = number,
                        Width = 48,
                        Height = 28,
                        Margin = new Padding(2),
                        UseVisualStyleBackColor = true
                    };
                    button.Click += ObjectDetectionDefectIntegrationRoiButton_Click;
                    selector.Controls.Add(button);
                }

                if (selector.Controls.Count == 0)
                {
                    selector.Controls.Add(new Label
                    {
                        AutoSize = true,
                        Text = "尚無已完成的 ROI 物件結果",
                        ForeColor = Color.FromArgb(95, 103, 115),
                        Margin = new Padding(4, 6, 0, 0)
                    });
                }
            }
            finally
            {
                selector.ResumeLayout(true);
            }
            UpdateObjectDetectionDefectIntegrationRoiButtonState();
        }

        private List<ObjectDefinitionDetectedObject> GetObjectDetectionDefectIntegrationObjects(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                return new List<ObjectDefinitionDetectedObject>();
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                return new List<ObjectDefinitionDetectedObject>();
            }

            string signature = CreateObjectDefinitionProcessingSignature(definition);
            return HasCompletedObjectDefinitionResult(definition, signature)
                ? SnapshotCompletedObjectDefinitionObjects(definition, signature)
                : new List<ObjectDefinitionDetectedObject>();
        }

        private void ObjectDetectionDefectIntegrationRoiButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !(button.Tag is int))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                return;
            }

            int number = (int)button.Tag;
            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(definition, number, out detectedObject))
            {
                SetObjectDetectionDefectRegionStatus(
                    "物件" + number.ToString(CultureInfo.InvariantCulture) + " 尚無有效 ROI 結果。");
                return;
            }

            selectedObjectDetectionNumber = number;
            UpdateObjectDetectionNumberButtonState(parameter);
            SelectObjectDetectionDefectDisplayForCore(ObjectDetectionDefectCoreKeys.Length);
            FocusObjectDetectionImage(detectedObject.Bounds);
            RefreshObjectDetectionDefectIntegrationResults(parameter);
            ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(4);
            if (display != null)
            {
                display.InvalidateImageView();
            }
            SetObjectDetectionDefectRegionStatus(
                "結果整合：目前檢視物件" + number.ToString(CultureInfo.InvariantCulture));
        }

        private void UpdateObjectDetectionDefectIntegrationRoiButtonState()
        {
            if (objectDetectionDefectIntegrationRoiSelector == null ||
                objectDetectionDefectIntegrationRoiSelector.IsDisposed)
            {
                return;
            }

            foreach (Control control in objectDetectionDefectIntegrationRoiSelector.Controls)
            {
                Button button = control as Button;
                if (button == null || !(button.Tag is int))
                {
                    continue;
                }

                bool selected = (int)button.Tag == selectedObjectDetectionNumber;
                button.BackColor = selected ? Color.FromArgb(190, 220, 255) : SystemColors.Control;
                button.FlatStyle = selected ? FlatStyle.Flat : FlatStyle.Standard;
            }
        }

        private void RefreshObjectDetectionDefectIntegrationResults(
            ObjectDetectionParameterSettings parameter)
        {
            DataGridView grid = objectDetectionDefectIntegrationResultsGrid;
            if (grid == null || grid.IsDisposed)
            {
                return;
            }

            grid.Rows.Clear();
            if (parameter == null || selectedObjectDetectionNumber <= 0)
            {
                SetObjectDetectionDefectIntegrationResultsStatus("請選擇 ROI 查看整合結果");
                return;
            }

            List<ObjectDetectionDefectIntegrationGroup> groups =
                GetObjectDetectionDefectIntegrationGroups(parameter)
                    .Where(group => group.ObjectNumber == selectedObjectDetectionNumber)
                    .OrderBy(group => group.Bounds.Top)
                    .ThenBy(group => group.Bounds.Left)
                    .ToList();
            for (int index = 0; index < groups.Count; index++)
            {
                ObjectDetectionDefectIntegrationGroup group = groups[index];
                string type = group.Candidates.Any(item => item.Contour.IsBright)
                    ? group.Candidates.Any(item => !item.Contour.IsBright) ? "亮暗" : "亮"
                    : "暗";
                string sources = string.Join("、", group.Candidates
                    .Select(item => GetObjectDetectionDefectCoreLabel(
                        GetObjectDetectionDefectCoreIndex(item.CoreKey)))
                    .Distinct(StringComparer.Ordinal));
                grid.Rows.Add(
                    (index + 1).ToString(CultureInfo.InvariantCulture),
                    type,
                    group.Candidates.Count.ToString(CultureInfo.InvariantCulture),
                    sources,
                    Math.Round(group.Bounds.Left).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Top).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Width).ToString(CultureInfo.InvariantCulture),
                    Math.Round(group.Bounds.Height).ToString(CultureInfo.InvariantCulture));
            }

            SetObjectDetectionDefectIntegrationResultsStatus(
                groups.Count == 0
                    ? "此 ROI 尚無符合目前設定的缺陷結果"
                    : "此 ROI 整合出 " + groups.Count.ToString("N0", CultureInfo.CurrentCulture) + " 組缺陷");
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

        private List<ObjectDetectionDefectIntegrationGroup> GetObjectDetectionDefectIntegrationGroups(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null)
            {
                return new List<ObjectDetectionDefectIntegrationGroup>();
            }

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
                        Contour = contour
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
            for (int index = 0; index < parents.Length; index++)
            {
                parents[index] = index;
            }

            double distance = Math.Max(0, Math.Min(
                1000000,
                parameter.DefectIntegrationMergeDistancePixels));
            double distanceSquared = distance * distance;
            foreach (IGrouping<int, int> roiCandidates in Enumerable.Range(0, candidates.Count)
                .GroupBy(index => candidates[index].Contour.ObjectNumber))
            {
                List<int> orderedIndexes = roiCandidates
                    .OrderBy(index => candidates[index].Contour.Bounds.Left)
                    .ToList();
                var activeIndexes = new List<int>();
                foreach (int currentIndex in orderedIndexes)
                {
                    RectangleF currentBounds = candidates[currentIndex].Contour.Bounds;
                    for (int activeIndex = activeIndexes.Count - 1; activeIndex >= 0; activeIndex--)
                    {
                        int previousIndex = activeIndexes[activeIndex];
                        if (candidates[previousIndex].Contour.Bounds.Right + distance < currentBounds.Left)
                        {
                            activeIndexes.RemoveAt(activeIndex);
                        }
                    }

                    foreach (int previousIndex in activeIndexes)
                    {
                        ObjectDetectionDefectIntegrationCandidate previous = candidates[previousIndex];
                        ObjectDetectionDefectIntegrationCandidate current = candidates[currentIndex];
                        if (!CanMergeObjectDetectionDefectCandidates(parameter, previous, current))
                        {
                            continue;
                        }
                        if (GetObjectDetectionDefectBoundsGapSquared(
                            previous.Contour.Bounds,
                            currentBounds) <= distanceSquared)
                        {
                            UnionObjectDetectionDefectIntegrationParents(
                                parents,
                                previousIndex,
                                currentIndex);
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
                        ObjectNumber = candidates[index].Contour.ObjectNumber,
                        Bounds = candidates[index].Contour.Bounds,
                        Candidates = new List<ObjectDetectionDefectIntegrationCandidate>()
                    };
                    byRoot[root] = group;
                }
                else
                {
                    group.Bounds = RectangleF.Union(group.Bounds, candidates[index].Contour.Bounds);
                }
                group.Candidates.Add(candidates[index]);
            }

            return byRoot.Values
                .OrderBy(group => group.ObjectNumber)
                .ThenBy(group => group.Bounds.Top)
                .ThenBy(group => group.Bounds.Left)
                .ToList();
        }

        private static bool CanMergeObjectDetectionDefectCandidates(
            ObjectDetectionParameterSettings parameter,
            ObjectDetectionDefectIntegrationCandidate first,
            ObjectDetectionDefectIntegrationCandidate second)
        {
            if (first.Contour.IsBright == second.Contour.IsBright)
            {
                return first.Contour.IsBright
                    ? parameter.DefectIntegrationMergeBright
                    : parameter.DefectIntegrationMergeDark;
            }
            return parameter.DefectIntegrationMergeDarkBright;
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
            {
                foreach (ObjectDetectionDefectIntegrationGroup group in
                    GetObjectDetectionDefectIntegrationGroupsForDisplay(parameter))
                {
                    if (!group.Bounds.IntersectsWith(visibleBounds))
                    {
                        continue;
                    }

                    bool showDark = group.Candidates.Any(candidate =>
                        !candidate.Contour.IsBright && candidate.Core.ShowRedBoxes);
                    bool showBright = group.Candidates.Any(candidate =>
                        candidate.Contour.IsBright && candidate.Core.ShowOrangeBoxes);
                    if (!showDark && !showBright)
                    {
                        continue;
                    }

                    Pen pen = showDark && showBright
                        ? mixedPen
                        : showDark ? darkPen : brightPen;
                    graphics.DrawRectangle(
                        pen,
                        offset.X + group.Bounds.X * zoom,
                        offset.Y + group.Bounds.Y * zoom,
                        Math.Max(1f, group.Bounds.Width * zoom),
                        Math.Max(1f, group.Bounds.Height * zoom));
                }
            }
        }
    }
}
