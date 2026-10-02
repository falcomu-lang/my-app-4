using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.Linq;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private enum ResultReviewGoodJudgementGrade
        {
            Pending,
            A,
            B,
            Ng
        }

        private enum ResultReviewGoodJudgementConditionState
        {
            NotConfigured,
            Passed,
            Failed,
            Unknown,
            Skipped,
            Disabled
        }

        private sealed class ResultReviewGoodJudgementCondition
        {
            public ObjectDetectionGoodJudgementRuleSettings Rule { get; set; }
            public bool HasA { get; set; }
            public bool HasB { get; set; }
            public ResultReviewGoodJudgementConditionState AState { get; set; }
            public ResultReviewGoodJudgementConditionState BState { get; set; }
            public double? AValue { get; set; }
            public double? BValue { get; set; }
            public string AError { get; set; }
            public string BError { get; set; }
        }

        private sealed class ResultReviewGoodJudgementObject
        {
            public int ObjectNumber { get; set; }
            public int GridRow { get; set; }
            public int GridColumn { get; set; }
            public ResultReviewGoodJudgementGrade Grade { get; set; }
            public string Reason { get; set; }
            public List<ResultReviewGoodJudgementCondition> Conditions { get; set; }
        }

        private sealed class ResultReviewObjectGridRow
        {
            public List<ObjectDefinitionDetectedObject> Objects { get; } =
                new List<ObjectDefinitionDetectedObject>();

            public double CenterY { get; set; }
        }

        private Panel objectDetectionResultReviewGoodJudgementObjectButtonsHost;
        private TableLayoutPanel objectDetectionResultReviewGoodJudgementObjectButtonsPanel;
        private Label objectDetectionResultReviewGoodJudgementSummaryLabel;
        private List<ObjectDetectionGoodJudgementRuleSettings> objectDetectionResultReviewGoodJudgementRules =
            new List<ObjectDetectionGoodJudgementRuleSettings>();
        private List<ResultReviewGoodJudgementObject> objectDetectionResultReviewGoodJudgementResults =
            new List<ResultReviewGoodJudgementObject>();
        private int? objectDetectionResultReviewSelectedGoodJudgementObjectNumber;
        private int objectDetectionResultReviewOverviewObjectNumber;

        private Control CreateObjectDetectionResultReviewGoodJudgementContent(DataGridView grid)
        {
            var layout = new TableLayoutPanel
            {
                Dock = DockStyle.Fill,
                ColumnCount = 1,
                RowCount = 4,
                Margin = Padding.Empty,
                Padding = Padding.Empty
            };
            layout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 22F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 78F));
            layout.RowStyles.Add(new RowStyle(SizeType.Absolute, 26F));
            layout.RowStyles.Add(new RowStyle(SizeType.Percent, 100F));

            var legend = new Label
            {
                Dock = DockStyle.Fill,
                Text = "物件序號色彩：綠色 A 規　黃色 B 規　紅色 NG　灰色待確認；再次點同一片回總覽",
                TextAlign = ContentAlignment.MiddleLeft,
                ForeColor = Color.FromArgb(75, 83, 95),
                Padding = new Padding(4, 0, 2, 0),
                Margin = Padding.Empty,
                AutoEllipsis = true
            };

            objectDetectionResultReviewGoodJudgementObjectButtonsHost = new Panel
            {
                Dock = DockStyle.Fill,
                AutoScroll = true,
                BackColor = Color.FromArgb(248, 250, 252),
                Margin = Padding.Empty
            };
            objectDetectionResultReviewGoodJudgementObjectButtonsPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                ColumnCount = 3,
                RowCount = 1,
                Height = 34,
                Margin = Padding.Empty,
                Padding = new Padding(2)
            };
            for (int column = 0; column < 3; column++)
            {
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100F / 3F));
            }
            objectDetectionResultReviewGoodJudgementObjectButtonsHost.Controls.Add(
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel);
            objectDetectionResultReviewGoodJudgementSummaryLabel = new Label
            {
                Dock = DockStyle.Fill,
                Text = "尚未執行結果確認。",
                TextAlign = ContentAlignment.MiddleLeft,
                AutoEllipsis = true,
                ForeColor = Color.FromArgb(75, 83, 95),
                Padding = new Padding(4, 0, 2, 0),
                Margin = Padding.Empty
            };

            layout.Controls.Add(legend, 0, 0);
            layout.Controls.Add(objectDetectionResultReviewGoodJudgementObjectButtonsHost, 0, 1);
            layout.Controls.Add(objectDetectionResultReviewGoodJudgementSummaryLabel, 0, 2);
            layout.Controls.Add(grid, 0, 3);
            return layout;
        }

        private void CalculateObjectDetectionResultReviewGoodJudgements(
            ObjectDetectionParameterSettings parameter,
            IList<ObjectDefinitionDetectedObject> objects,
            IDictionary<int, ResultReviewMeasurementContext> contexts)
        {
            objectDetectionResultReviewGoodJudgementRules =
                (parameter == null || parameter.GoodJudgementRules == null
                    ? new List<ObjectDetectionGoodJudgementRuleSettings>()
                    : parameter.GoodJudgementRules)
                .Where(rule => rule != null)
                .OrderBy(rule => rule.Number)
                .ToList();
            objectDetectionResultReviewGoodJudgementResults = new List<ResultReviewGoodJudgementObject>();
            objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
            objectDetectionResultReviewOverviewObjectNumber = objects != null && objects.Count > 0
                ? objects[0].Number
                : 0;
            Dictionary<int, Point> gridPositions =
                CreateObjectDetectionResultReviewObjectGridPositions(objects);

            foreach (ObjectDefinitionDetectedObject detectedObject in
                objects ?? new List<ObjectDefinitionDetectedObject>())
            {
                ResultReviewMeasurementContext context;
                contexts.TryGetValue(detectedObject.Number, out context);
                Point gridPosition;
                if (!gridPositions.TryGetValue(detectedObject.Number, out gridPosition))
                {
                    gridPosition = new Point(
                        objectDetectionResultReviewGoodJudgementResults.Count % 3,
                        objectDetectionResultReviewGoodJudgementResults.Count / 3);
                }
                var result = new ResultReviewGoodJudgementObject
                {
                    ObjectNumber = detectedObject.Number,
                    GridRow = gridPosition.Y,
                    GridColumn = gridPosition.X,
                    Grade = ResultReviewGoodJudgementGrade.Pending,
                    Conditions = new List<ResultReviewGoodJudgementCondition>()
                };

                foreach (ObjectDetectionGoodJudgementRuleSettings rule in
                    objectDetectionResultReviewGoodJudgementRules)
                {
                    bool hasA = !string.IsNullOrWhiteSpace(rule.CalculationExpression) ||
                        !string.IsNullOrWhiteSpace(rule.SpecificationExpression);
                    bool hasB = !string.IsNullOrWhiteSpace(rule.AlternativeCalculationExpression) ||
                        !string.IsNullOrWhiteSpace(rule.AlternativeSpecificationExpression);
                    var condition = new ResultReviewGoodJudgementCondition
                    {
                        Rule = rule,
                        HasA = hasA,
                        HasB = hasB,
                        AState = rule.Enabled
                            ? ResultReviewGoodJudgementConditionState.NotConfigured
                            : ResultReviewGoodJudgementConditionState.Disabled,
                        BState = rule.Enabled
                            ? ResultReviewGoodJudgementConditionState.NotConfigured
                            : ResultReviewGoodJudgementConditionState.Disabled
                    };

                    if (rule.Enabled && hasA)
                    {
                        double? value;
                        string error;
                        condition.AState = EvaluateObjectDetectionResultReviewGoodJudgementCondition(
                            rule.CalculationExpression,
                            rule.SpecificationExpression,
                            context,
                            "A",
                            out value,
                            out error);
                        condition.AValue = value;
                        condition.AError = error;
                    }
                    result.Conditions.Add(condition);
                }

                int configuredACount = result.Conditions.Count(condition =>
                    condition.Rule.Enabled && condition.HasA);
                bool hasAFailure = result.Conditions.Any(condition =>
                    condition.AState == ResultReviewGoodJudgementConditionState.Failed);
                bool hasAUnknown = result.Conditions.Any(condition =>
                    condition.AState == ResultReviewGoodJudgementConditionState.Unknown);

                if (configuredACount > 0 && !hasAFailure && !hasAUnknown)
                {
                    result.Grade = ResultReviewGoodJudgementGrade.A;
                    result.Reason = "A 規條件全數符合";
                    MarkObjectDetectionResultReviewBConditionsSkipped(
                        result,
                        "未執行（A 規已全數符合）");
                }
                else if (configuredACount > 0 && !hasAFailure && hasAUnknown)
                {
                    result.Grade = ResultReviewGoodJudgementGrade.Pending;
                    result.Reason = "A 規有條件無法判定";
                    MarkObjectDetectionResultReviewBConditionsSkipped(
                        result,
                        "未執行（A 規尚待確認）");
                }
                else
                {
                    bool hasConfiguredB = false;
                    foreach (ResultReviewGoodJudgementCondition condition in result.Conditions)
                    {
                        if (!condition.Rule.Enabled || !condition.HasB)
                        {
                            continue;
                        }

                        hasConfiguredB = true;
                        double? value;
                        string error;
                        condition.BState = EvaluateObjectDetectionResultReviewGoodJudgementCondition(
                            condition.Rule.AlternativeCalculationExpression,
                            condition.Rule.AlternativeSpecificationExpression,
                            context,
                            "B",
                            out value,
                            out error);
                        condition.BValue = value;
                        condition.BError = error;
                    }

                    if (!hasConfiguredB)
                    {
                        result.Grade = configuredACount > 0
                            ? ResultReviewGoodJudgementGrade.Ng
                            : ResultReviewGoodJudgementGrade.Pending;
                        result.Reason = configuredACount > 0
                            ? "A 規不符合，且未設定 B 規條件"
                            : "沒有啟用的尺寸良品判斷條件";
                    }
                    else if (result.Conditions.Any(condition =>
                        condition.BState == ResultReviewGoodJudgementConditionState.Failed))
                    {
                        result.Grade = ResultReviewGoodJudgementGrade.Ng;
                        result.Reason = "B 規至少一項條件不符合";
                    }
                    else if (result.Conditions.Any(condition =>
                        condition.BState == ResultReviewGoodJudgementConditionState.Unknown))
                    {
                        result.Grade = ResultReviewGoodJudgementGrade.Pending;
                        result.Reason = "B 規有條件無法判定";
                    }
                    else
                    {
                        result.Grade = ResultReviewGoodJudgementGrade.B;
                        result.Reason = configuredACount > 0
                            ? "A 規未全數符合，B 規條件全數符合"
                            : "未設定 A 規條件，B 規條件全數符合";
                    }
                }

                objectDetectionResultReviewGoodJudgementResults.Add(result);
            }
        }

        private static Dictionary<int, Point> CreateObjectDetectionResultReviewObjectGridPositions(
            IList<ObjectDefinitionDetectedObject> objects)
        {
            List<ObjectDefinitionDetectedObject> items = (objects ??
                new List<ObjectDefinitionDetectedObject>())
                .Where(item => item != null && item.Bounds.Width > 0 && item.Bounds.Height > 0)
                .ToList();
            var positions = new Dictionary<int, Point>();
            if (items.Count == 0)
            {
                return positions;
            }

            double typicalWidth = GetObjectDetectionResultReviewTypicalDimension(
                items.Select(item => item.Bounds.Width));
            double typicalHeight = GetObjectDetectionResultReviewTypicalDimension(
                items.Select(item => item.Bounds.Height));
            double columnTolerance = Math.Max(1.0, typicalWidth * 0.5);
            double rowTolerance = Math.Max(1.0, typicalHeight * 0.5);

            var rows = new List<ResultReviewObjectGridRow>();
            foreach (ObjectDefinitionDetectedObject item in items
                .OrderBy(candidate => candidate.Bounds.Top + candidate.Bounds.Height / 2.0)
                .ThenBy(candidate => candidate.Bounds.Left))
            {
                double centerY = item.Bounds.Top + item.Bounds.Height / 2.0;
                ResultReviewObjectGridRow row = rows
                    .OrderBy(candidate => Math.Abs(candidate.CenterY - centerY))
                    .FirstOrDefault(candidate =>
                        Math.Abs(candidate.CenterY - centerY) <= rowTolerance);
                if (row == null)
                {
                    row = new ResultReviewObjectGridRow();
                    rows.Add(row);
                }

                row.Objects.Add(item);
                row.CenterY = row.Objects.Average(candidate =>
                    candidate.Bounds.Top + candidate.Bounds.Height / 2.0);
            }
            rows = rows.OrderBy(row => row.CenterY).ToList();

            var columnCenters = new List<List<double>>();
            foreach (ObjectDefinitionDetectedObject item in items
                .OrderBy(candidate => candidate.Bounds.Left + candidate.Bounds.Width / 2.0))
            {
                double centerX = item.Bounds.Left + item.Bounds.Width / 2.0;
                List<double> lastColumn = columnCenters.LastOrDefault();
                double lastColumnCenter = lastColumn == null ? 0 : lastColumn.Average();
                if (lastColumn == null || Math.Abs(lastColumnCenter - centerX) > columnTolerance)
                {
                    columnCenters.Add(new List<double> { centerX });
                }
                else
                {
                    lastColumn.Add(centerX);
                }
            }
            List<double> columns = columnCenters.Select(column => column.Average()).ToList();

            for (int rowIndex = 0; rowIndex < rows.Count; rowIndex++)
            {
                foreach (ObjectDefinitionDetectedObject item in rows[rowIndex].Objects)
                {
                    double centerX = item.Bounds.Left + item.Bounds.Width / 2.0;
                    int columnIndex = 0;
                    double closestDistance = double.MaxValue;
                    for (int index = 0; index < columns.Count; index++)
                    {
                        double distance = Math.Abs(columns[index] - centerX);
                        if (distance < closestDistance)
                        {
                            columnIndex = index;
                            closestDistance = distance;
                        }
                    }
                    positions[item.Number] = new Point(columnIndex, rowIndex);
                }
            }

            return positions;
        }

        private static double GetObjectDetectionResultReviewTypicalDimension(
            IEnumerable<int> dimensions)
        {
            List<int> sorted = (dimensions ?? Enumerable.Empty<int>())
                .Where(value => value > 0)
                .OrderBy(value => value)
                .ToList();
            return sorted.Count == 0 ? 1.0 : sorted[sorted.Count / 2];
        }

        private ResultReviewGoodJudgementConditionState EvaluateObjectDetectionResultReviewGoodJudgementCondition(
            string calculation,
            string specification,
            ResultReviewMeasurementContext context,
            string gradeName,
            out double? value,
            out string error)
        {
            value = null;
            error = string.Empty;
            if (string.IsNullOrWhiteSpace(calculation) || string.IsNullOrWhiteSpace(specification))
            {
                error = gradeName + " 規計算式或規格尚未完整";
                return ResultReviewGoodJudgementConditionState.Unknown;
            }
            if (context == null)
            {
                error = "找不到此物件的量測資料";
                return ResultReviewGoodJudgementConditionState.Unknown;
            }

            double calculatedValue;
            if (!TryEvaluateObjectDetectionGoodJudgementExpression(
                calculation, context, out calculatedValue, out error))
            {
                if (string.IsNullOrWhiteSpace(error))
                {
                    error = gradeName + " 規計算式無法判定";
                }
                return ResultReviewGoodJudgementConditionState.Unknown;
            }

            bool passed;
            if (!TryEvaluateObjectDetectionGoodJudgementSpecification(
                specification, calculatedValue, out passed))
            {
                error = gradeName + " 規格格式或計算結果無法判定";
                return ResultReviewGoodJudgementConditionState.Unknown;
            }

            value = calculatedValue;
            return passed
                ? ResultReviewGoodJudgementConditionState.Passed
                : ResultReviewGoodJudgementConditionState.Failed;
        }

        private static void MarkObjectDetectionResultReviewBConditionsSkipped(
            ResultReviewGoodJudgementObject result,
            string reason)
        {
            foreach (ResultReviewGoodJudgementCondition condition in result.Conditions)
            {
                if (condition.Rule.Enabled && condition.HasB)
                {
                    condition.BState = ResultReviewGoodJudgementConditionState.Skipped;
                    condition.BError = reason;
                }
            }
        }

        private void RefreshObjectDetectionResultReviewGoodJudgementView()
        {
            RefreshObjectDetectionResultReviewGoodJudgementObjectButtons();
            if (objectDetectionResultReviewSelectedGoodJudgementObjectNumber.HasValue)
            {
                RenderObjectDetectionResultReviewGoodJudgementObject(
                    objectDetectionResultReviewSelectedGoodJudgementObjectNumber.Value);
            }
            else
            {
                RenderObjectDetectionResultReviewGoodJudgementOverview();
            }
        }

        private void RefreshObjectDetectionResultReviewGoodJudgementObjectButtons()
        {
            if (objectDetectionResultReviewGoodJudgementObjectButtonsPanel == null)
            {
                return;
            }

            objectDetectionResultReviewGoodJudgementObjectButtonsPanel.SuspendLayout();
            try
            {
                foreach (Control control in objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Controls
                    .Cast<Control>().ToList())
                {
                    objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Controls.Remove(control);
                    control.Dispose();
                }
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.RowStyles.Clear();
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.ColumnStyles.Clear();
                int columnCount = Math.Max(1, objectDetectionResultReviewGoodJudgementResults
                    .Select(result => result.GridColumn).DefaultIfEmpty(0).Max() + 1);
                int rowCount = Math.Max(1, objectDetectionResultReviewGoodJudgementResults
                    .Select(result => result.GridRow).DefaultIfEmpty(0).Max() + 1);
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.ColumnCount = columnCount;
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.RowCount = rowCount;
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Height = rowCount * 34 + 4;
                for (int column = 0; column < columnCount; column++)
                {
                    objectDetectionResultReviewGoodJudgementObjectButtonsPanel.ColumnStyles.Add(
                        new ColumnStyle(SizeType.Percent, 100F / columnCount));
                }
                for (int row = 0; row < rowCount; row++)
                {
                    objectDetectionResultReviewGoodJudgementObjectButtonsPanel.RowStyles.Add(
                        new RowStyle(SizeType.Absolute, 34F));
                }

                for (int index = 0;
                    index < objectDetectionResultReviewGoodJudgementResults.Count;
                    index++)
                {
                    ResultReviewGoodJudgementObject result =
                        objectDetectionResultReviewGoodJudgementResults[index];
                    var button = new Button
                    {
                        Text = result.ObjectNumber.ToString(CultureInfo.CurrentCulture),
                        Tag = result.ObjectNumber,
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2),
                        FlatStyle = FlatStyle.Flat,
                        UseVisualStyleBackColor = false,
                        BackColor = GetObjectDetectionResultReviewGoodJudgementColor(result.Grade),
                        ForeColor = result.Grade == ResultReviewGoodJudgementGrade.B
                            ? Color.FromArgb(35, 35, 35)
                            : Color.White,
                        AccessibleName = "物件 " + result.ObjectNumber.ToString(CultureInfo.CurrentCulture),
                        AccessibleDescription = GetObjectDetectionResultReviewGoodJudgementGradeText(result.Grade) +
                            "；" + result.Reason
                    };
                    button.FlatAppearance.BorderColor =
                        objectDetectionResultReviewSelectedGoodJudgementObjectNumber == result.ObjectNumber
                            ? Color.DodgerBlue
                            : Color.FromArgb(160, 160, 160);
                    button.FlatAppearance.BorderSize =
                        objectDetectionResultReviewSelectedGoodJudgementObjectNumber == result.ObjectNumber ? 3 : 1;
                    button.Click += ObjectDetectionResultReviewGoodJudgementObjectButton_Click;
                    objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Controls.Add(
                        button,
                        result.GridColumn,
                        result.GridRow);
                }
            }
            finally
            {
                objectDetectionResultReviewGoodJudgementObjectButtonsPanel.ResumeLayout();
            }
        }

        private void ObjectDetectionResultReviewGoodJudgementObjectButton_Click(object sender, EventArgs e)
        {
            if (isObjectDetectionResultReviewRunning)
            {
                return;
            }

            var button = sender as Button;
            if (button == null || !(button.Tag is int))
            {
                return;
            }

            int objectNumber = (int)button.Tag;
            if (objectDetectionResultReviewSelectedGoodJudgementObjectNumber == objectNumber)
            {
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
                if (objectDetectionResultReviewOverviewObjectNumber > 0)
                {
                    selectedObjectDetectionNumber = objectDetectionResultReviewOverviewObjectNumber;
                    RefreshObjectDetectionResultReviewSelectedObjectDisplays();
                }
            }
            else
            {
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = objectNumber;
                selectedObjectDetectionNumber = objectNumber;
                ClearObjectDetectionResultReviewMeasurementHighlight();
                RefreshObjectDetectionResultReviewSelectedObjectDisplays();
            }

            objectDetectionResultReviewSelectedDefectObjectNumber =
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber;
            RefreshObjectDetectionResultReviewDefectObjectButtons();
            var choice = objectDetectionResultReviewParameterComboBox == null
                ? null
                : objectDetectionResultReviewParameterComboBox.SelectedItem as ResultReviewParameterChoice;
            if (choice != null && choice.Parameter != null && objectDetectionResultReviewDefectsGrid != null)
            {
                RenderObjectDetectionResultReviewDefectRows(
                    choice.Parameter,
                    objectDetectionResultReviewSelectedDefectObjectNumber);
            }

            RefreshObjectDetectionResultReviewGoodJudgementView();
        }

        private void ClearObjectDetectionResultReviewMeasurementHighlight()
        {
            if (objectDetectionResultReviewHighlightTimer != null)
            {
                objectDetectionResultReviewHighlightTimer.Stop();
            }
            if (objectDetectionResultReviewSelectedMeasurement != null)
            {
                objectDetectionResultReviewSelectedMeasurement.StatisticsHighlightsVisible = false;
                objectDetectionResultReviewSelectedMeasurement = null;
            }
        }

        private void RefreshObjectDetectionResultReviewSelectedObjectDisplays()
        {
            RefreshObjectDetectionFlatFieldDisplay();
            RefreshObjectDetectionMeasurementDisplay();
            RefreshObjectDetectionDefectDisplay();
        }

        private void RenderObjectDetectionResultReviewGoodJudgementOverview()
        {
            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Columns[4].HeaderText = "A 規結果統計";
            objectDetectionResultReviewConditionsGrid.Columns[7].HeaderText = "B 規結果統計";

            foreach (ObjectDetectionGoodJudgementRuleSettings rule in
                objectDetectionResultReviewGoodJudgementRules)
            {
                if (!rule.Enabled)
                {
                    objectDetectionResultReviewConditionsGrid.Rows.Add(
                        rule.Number, rule.Name, rule.CalculationExpression, rule.SpecificationExpression,
                        "停用", rule.AlternativeCalculationExpression,
                        rule.AlternativeSpecificationExpression, "停用");
                    continue;
                }

                List<ResultReviewGoodJudgementCondition> conditions =
                    objectDetectionResultReviewGoodJudgementResults
                        .Select(result => result.Conditions.FirstOrDefault(condition =>
                            ReferenceEquals(condition.Rule, rule)))
                        .Where(condition => condition != null)
                        .ToList();
                objectDetectionResultReviewConditionsGrid.Rows.Add(
                    rule.Number,
                    rule.Name,
                    rule.CalculationExpression,
                    rule.SpecificationExpression,
                    SummarizeObjectDetectionResultReviewGoodJudgementStates(conditions, true),
                    rule.AlternativeCalculationExpression,
                    rule.AlternativeSpecificationExpression,
                    SummarizeObjectDetectionResultReviewGoodJudgementStates(conditions, false));
            }

            if (objectDetectionResultReviewGoodJudgementRules.Count == 0)
            {
                objectDetectionResultReviewConditionsGrid.Rows.Add(
                    string.Empty, "未設定尺寸良品判斷條件", string.Empty, string.Empty,
                    "待確認", string.Empty, string.Empty, "待確認");
            }

            objectDetectionResultReviewGoodJudgementSummaryLabel.Text =
                BuildObjectDetectionResultReviewGoodJudgementOverviewText();
            ApplyObjectDetectionResultReviewGoodJudgementStyles();
        }

        private string SummarizeObjectDetectionResultReviewGoodJudgementStates(
            IList<ResultReviewGoodJudgementCondition> conditions,
            bool isA)
        {
            bool hasConfigured = conditions.Any(condition => condition.Rule.Enabled &&
                (isA ? condition.HasA : condition.HasB));
            if (!hasConfigured)
            {
                return "未設定";
            }

            int passed = conditions.Count(condition => condition.Rule.Enabled &&
                (isA ? condition.AState : condition.BState) == ResultReviewGoodJudgementConditionState.Passed);
            int failed = conditions.Count(condition => condition.Rule.Enabled &&
                (isA ? condition.AState : condition.BState) == ResultReviewGoodJudgementConditionState.Failed);
            int unknown = conditions.Count(condition => condition.Rule.Enabled &&
                (isA ? condition.AState : condition.BState) == ResultReviewGoodJudgementConditionState.Unknown);
            int skipped = conditions.Count(condition => condition.Rule.Enabled &&
                (isA ? condition.AState : condition.BState) == ResultReviewGoodJudgementConditionState.Skipped);
            return "符合 " + passed.ToString(CultureInfo.CurrentCulture) +
                " / 不符合 " + failed.ToString(CultureInfo.CurrentCulture) +
                " / 待確認 " + unknown.ToString(CultureInfo.CurrentCulture) +
                (isA ? string.Empty : " / 未執行 " + skipped.ToString(CultureInfo.CurrentCulture));
        }

        private string BuildObjectDetectionResultReviewGoodJudgementOverviewText()
        {
            if (objectDetectionResultReviewGoodJudgementResults.Count == 0)
            {
                return "尚未執行結果確認。";
            }

            int aCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.A);
            int bCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.B);
            int ngCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.Ng);
            int pendingCount = objectDetectionResultReviewGoodJudgementResults.Count(result =>
                result.Grade == ResultReviewGoodJudgementGrade.Pending);
            return "總覽：A 規 " + aCount.ToString(CultureInfo.CurrentCulture) +
                " 片　B 規 " + bCount.ToString(CultureInfo.CurrentCulture) +
                " 片　NG " + ngCount.ToString(CultureInfo.CurrentCulture) +
                " 片　待確認 " + pendingCount.ToString(CultureInfo.CurrentCulture) + " 片";
        }

        private void RenderObjectDetectionResultReviewGoodJudgementObject(int objectNumber)
        {
            ResultReviewGoodJudgementObject result =
                objectDetectionResultReviewGoodJudgementResults.FirstOrDefault(item =>
                    item.ObjectNumber == objectNumber);
            if (result == null)
            {
                objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
                RenderObjectDetectionResultReviewGoodJudgementOverview();
                return;
            }

            objectDetectionResultReviewConditionsGrid.Rows.Clear();
            objectDetectionResultReviewConditionsGrid.Columns[4].HeaderText = "A 規計算結果";
            objectDetectionResultReviewConditionsGrid.Columns[7].HeaderText = "B 規計算結果";
            foreach (ResultReviewGoodJudgementCondition condition in result.Conditions)
            {
                objectDetectionResultReviewConditionsGrid.Rows.Add(
                    condition.Rule.Number,
                    condition.Rule.Name,
                    condition.Rule.CalculationExpression,
                    condition.Rule.SpecificationExpression,
                    FormatObjectDetectionResultReviewGoodJudgementCondition(
                        condition.AState, condition.AValue, condition.AError),
                    condition.Rule.AlternativeCalculationExpression,
                    condition.Rule.AlternativeSpecificationExpression,
                    FormatObjectDetectionResultReviewGoodJudgementCondition(
                        condition.BState, condition.BValue, condition.BError));
            }

            if (result.Conditions.Count == 0)
            {
                objectDetectionResultReviewConditionsGrid.Rows.Add(
                    string.Empty, "未設定尺寸良品判斷條件", string.Empty, string.Empty,
                    "待確認", string.Empty, string.Empty, "待確認");
            }

            objectDetectionResultReviewGoodJudgementSummaryLabel.Text =
                "物件 " + objectNumber.ToString(CultureInfo.CurrentCulture) +
                "　最終判定：" + GetObjectDetectionResultReviewGoodJudgementGradeText(result.Grade) +
                "　" + result.Reason;
            ApplyObjectDetectionResultReviewGoodJudgementStyles();
        }

        private string FormatObjectDetectionResultReviewGoodJudgementCondition(
            ResultReviewGoodJudgementConditionState state,
            double? value,
            string error)
        {
            switch (state)
            {
                case ResultReviewGoodJudgementConditionState.Passed:
                    return "符合　x=" + FormatObjectDetectionReviewValue(value.GetValueOrDefault());
                case ResultReviewGoodJudgementConditionState.Failed:
                    return "不符合　x=" + FormatObjectDetectionReviewValue(value.GetValueOrDefault());
                case ResultReviewGoodJudgementConditionState.Unknown:
                    return "待確認" + (string.IsNullOrWhiteSpace(error) ? string.Empty : "：" + error);
                case ResultReviewGoodJudgementConditionState.Skipped:
                    return error ?? "未執行";
                case ResultReviewGoodJudgementConditionState.Disabled:
                    return "停用";
                default:
                    return "未設定";
            }
        }

        private void ApplyObjectDetectionResultReviewGoodJudgementStyles()
        {
            if (objectDetectionResultReviewConditionsGrid == null)
            {
                return;
            }

            foreach (DataGridViewRow row in objectDetectionResultReviewConditionsGrid.Rows)
            {
                row.DefaultCellStyle.ForeColor = Color.FromArgb(75, 83, 95);
                if (!objectDetectionResultReviewSelectedGoodJudgementObjectNumber.HasValue)
                {
                    continue;
                }

                ApplyObjectDetectionResultReviewGoodJudgementCellStyle(row.Cells[4]);
                ApplyObjectDetectionResultReviewGoodJudgementCellStyle(row.Cells[7]);
            }
        }

        private static void ApplyObjectDetectionResultReviewGoodJudgementCellStyle(DataGridViewCell cell)
        {
            string text = Convert.ToString(cell.Value, CultureInfo.CurrentCulture);
            cell.Style.ForeColor = text.StartsWith("符合", StringComparison.Ordinal)
                ? Color.ForestGreen
                : text.StartsWith("不符合", StringComparison.Ordinal)
                    ? Color.Firebrick
                    : text.StartsWith("待確認", StringComparison.Ordinal)
                        ? Color.DarkOrange
                        : Color.FromArgb(75, 83, 95);
        }

        private static Color GetObjectDetectionResultReviewGoodJudgementColor(
            ResultReviewGoodJudgementGrade grade)
        {
            switch (grade)
            {
                case ResultReviewGoodJudgementGrade.A:
                    return Color.FromArgb(46, 125, 50);
                case ResultReviewGoodJudgementGrade.B:
                    return Color.FromArgb(255, 193, 7);
                case ResultReviewGoodJudgementGrade.Ng:
                    return Color.FromArgb(198, 40, 40);
                default:
                    return Color.FromArgb(117, 117, 117);
            }
        }

        private static string GetObjectDetectionResultReviewGoodJudgementGradeText(
            ResultReviewGoodJudgementGrade grade)
        {
            switch (grade)
            {
                case ResultReviewGoodJudgementGrade.A:
                    return "A 規";
                case ResultReviewGoodJudgementGrade.B:
                    return "B 規";
                case ResultReviewGoodJudgementGrade.Ng:
                    return "NG";
                default:
                    return "待確認";
            }
        }

        private void ClearObjectDetectionResultReviewGoodJudgementState()
        {
            objectDetectionResultReviewGoodJudgementRules =
                new List<ObjectDetectionGoodJudgementRuleSettings>();
            objectDetectionResultReviewGoodJudgementResults =
                new List<ResultReviewGoodJudgementObject>();
            objectDetectionResultReviewSelectedGoodJudgementObjectNumber = null;
            objectDetectionResultReviewOverviewObjectNumber = 0;
            if (objectDetectionResultReviewGoodJudgementObjectButtonsPanel != null)
            {
                foreach (Control control in objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Controls
                    .Cast<Control>().ToList())
                {
                    objectDetectionResultReviewGoodJudgementObjectButtonsPanel.Controls.Remove(control);
                    control.Dispose();
                }
            }
            if (objectDetectionResultReviewGoodJudgementSummaryLabel != null)
            {
                objectDetectionResultReviewGoodJudgementSummaryLabel.Text = "尚未執行結果確認。";
            }
        }
    }
}
