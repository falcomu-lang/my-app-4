using System;
using System.Collections.Generic;
using System.Drawing;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Drawing.Imaging;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;
using Interlocked = System.Threading.Interlocked;
using Cv = OpenCvSharp;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private Panel objectDetectionParameterPanel;
        private TabControl objectDetectionParameterTabControl;
        private bool isObjectDetectionParameterImageLayout;
        private string activeObjectDetectionParameterId;
        private Label objectDetectionImageCheckStatusLabel;
        private TableLayoutPanel objectDetectionObjectNumberPanel;
        private TableLayoutPanel objectDetectionDefectObjectNumberPanel;
        private int selectedObjectDetectionNumber = -1;
        private void ShowObjectDetectionParameterPanel(string parameterId)
        {
            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            if (parameter == null)
            {
                return;
            }

            HideImageProcessingFlowTree();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            if (objectDetectionParameterPanel != null)
            {
                parameterPanel.Controls.Remove(objectDetectionParameterPanel);
            }

            parameterPlaceholderLabel.Visible = false;
            var panel = new Panel { Dock = DockStyle.Fill, AutoScroll = true };
            objectDetectionParameterPanel = panel;

            panel.Controls.Add(new Label
            {
                Text = "檢測參數名稱",
                Left = 8,
                Top = 12,
                Width = 250
            });
            var nameBox = new TextBox
            {
                Left = 8,
                Top = 34,
                Width = parameterPanel.Width - 18,
                Text = parameter.DisplayName ?? string.Empty
            };
            panel.Controls.Add(nameBox);

            panel.Controls.Add(new Label
            {
                Text = "參數內容",
                Left = 8,
                Top = 72,
                Width = 250
            });
            var parametersBox = new TextBox
            {
                Left = 8,
                Top = 94,
                Width = parameterPanel.Width - 18,
                Height = 120,
                Multiline = true,
                ScrollBars = ScrollBars.Vertical,
                Text = parameter.Parameters ?? string.Empty
            };
            panel.Controls.Add(parametersBox);

            panel.Controls.Add(new Label
            {
                Text = "物件定義結果關聯",
                Left = 8,
                Top = 228,
                Width = 250
            });
            var objectDefinitionSource = new ComboBox
            {
                Left = 8,
                Top = 250,
                Width = parameterPanel.Width - 18,
                DropDownStyle = ComboBoxStyle.DropDownList
            };
            objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
            {
                DisplayText = "未指定物件定義結果",
                Id = string.Empty
            });
            for (int index = 0; index < systemParameters.ObjectDefinitions.Count; index++)
            {
                ObjectDefinitionSettings definition = systemParameters.ObjectDefinitions[index];
                objectDefinitionSource.Items.Add(new ObjectDetectionDefinitionChoice
                {
                    DisplayText = GetObjectDefinitionDisplayName(definition, index),
                    Id = definition.Id
                });
            }

            objectDefinitionSource.SelectedIndex = 0;
            for (int index = 0; index < objectDefinitionSource.Items.Count; index++)
            {
                ObjectDetectionDefinitionChoice choice =
                    objectDefinitionSource.Items[index] as ObjectDetectionDefinitionChoice;
                if (choice != null && string.Equals(
                        choice.Id,
                        parameter.ObjectDefinitionId ?? string.Empty,
                        StringComparison.Ordinal))
                {
                    objectDefinitionSource.SelectedIndex = index;
                    break;
                }
            }
            panel.Controls.Add(objectDefinitionSource);

            var apply = new Button
            {
                Text = "套用",
                Left = 8,
                Top = 286,
                Width = parameterPanel.Width - 18
            };
            apply.Click += delegate
            {
                parameter.DisplayName = string.IsNullOrWhiteSpace(nameBox.Text)
                        ? GetObjectDetectionParameterDisplayName(
                        parameter,
                        systemParameters.ObjectDetectionParameters.IndexOf(parameter))
                    : nameBox.Text.Trim();
                parameter.Parameters = parametersBox.Text ?? string.Empty;
                ObjectDetectionDefinitionChoice selectedDefinition =
                    objectDefinitionSource.SelectedItem as ObjectDetectionDefinitionChoice;
                parameter.ObjectDefinitionId = selectedDefinition == null
                    ? string.Empty
                    : selectedDefinition.Id;
                SaveSystemParameters();
                RebuildVisibleObjectDetectionParameters();
                SelectObjectDetectionParameter(parameter.Id);
                selectedObjectDetectionNumber = -1;
                statusLabel.Text = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                    ? "已套用" + parameter.DisplayName
                    : "已套用" + parameter.DisplayName + "，已關聯物件定義結果";
                UpdateObjectDetectionParameterTabs(parameter);
                EnsureObjectDetectionParameterSourceProcessed(parameter);
                RefreshObjectDetectionMeasurementDisplay();
            };
            panel.Controls.Add(apply);

            var cancel = new Button
            {
                Text = "取消",
                Left = 8,
                Top = 322,
                Width = parameterPanel.Width - 18
            };
            cancel.Click += delegate { ShowObjectDetectionParameterPanel(parameter.Id); };
            panel.Controls.Add(cancel);

            parameterPanel.Controls.Add(panel);
            panel.BringToFront();
            if (!string.Equals(activeObjectDetectionParameterId, parameter.Id, StringComparison.Ordinal))
            {
                selectedObjectDetectionNumber = -1;
                objectDetectionFlatFieldEvaluationGeneration++;
                ClearObjectDetectionFlatFieldMaskOverlays();
            }
            activeObjectDetectionParameterId = parameter.Id;
            SetObjectDetectionParameterDisplayMode(true);
            UpdateObjectDetectionParameterTabs(parameter);
            EnsureObjectDetectionParameterSourceProcessed(parameter);
        }

        private void EnsureObjectDetectionParameterSourceProcessed(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter == null ? null : parameter.Id,
                    "尚未指定物件定義結果",
                    Color.FromArgb(75, 83, 95));
                return;
            }

            if (suppressObjectDetectionParameterSourceAutoProcessing)
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "設定已載入，尚未重新處理",
                    Color.FromArgb(75, 83, 95));
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(parameter.ObjectDefinitionId);
            if (definition == null)
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義結果不存在，請重新設定關聯",
                    Color.Firebrick);
                return;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                TryApplySavedObjectDetectionFlatFieldCalibration(parameter);
                return;
            }

            if (!HasConfiguredObjectDefinitionSource(definition))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "物件定義尚未設定完整來源",
                    Color.Firebrick);
                ProcessObjectDefinition(definition.Id);
                return;
            }

            if ((rightOriginalDisplayControl == null || !rightOriginalDisplayControl.HasImage) &&
                (leftOriginalDisplayControl == null || !leftOriginalDisplayControl.HasImage))
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "請先載入圖片",
                    Color.Firebrick);
                return;
            }

            UpdateObjectDetectionParameterSourceStatus(
                parameter.Id,
                "進行影像確認，請稍等",
                Color.Firebrick);
            ProcessObjectDefinition(definition.Id);
        }

        private void UpdateObjectDetectionParameterSourceStatus(
            string parameterId,
            string text,
            Color color)
        {
            if (objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameterId,
                    StringComparison.Ordinal))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter = FindObjectDetectionParameter(parameterId);
            string actualCount = string.Equals(text, "進行影像確認，請稍等", StringComparison.Ordinal)
                ? "處理中"
                : "尚未確認";
            if (parameter == null)
            {
                objectDetectionImageCheckStatusLabel.Text = text ?? string.Empty;
            }
            else
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        actualCount,
                        text);
            }
            objectDetectionImageCheckStatusLabel.ForeColor = color;
        }

        private void RefreshObjectDetectionParameterQuantityStatus(
            ObjectDetectionParameterSettings parameter)
        {
            if (parameter == null || objectDetectionImageCheckStatusLabel == null ||
                !string.Equals(
                    activeObjectDetectionParameterId,
                    parameter.Id,
                    StringComparison.Ordinal))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            if (parameter.ColumnCount == 0 && parameter.RowCount == 0)
            {
                objectDetectionImageCheckStatusLabel.Text = "數量檢查：未啟用";
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "數量設定不完整");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.Firebrick;
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                objectDetectionImageCheckStatusLabel.Text =
                    BuildObjectDetectionParameterQuantityStatusText(
                        parameter,
                        "尚未確認",
                        "尚未進行影像確認");
                objectDetectionImageCheckStatusLabel.ForeColor = Color.FromArgb(75, 83, 95);
                return;
            }

            bool isMatch = expectedCount == actualCount;
            objectDetectionImageCheckStatusLabel.Text =
                BuildObjectDetectionParameterQuantityStatusText(
                    parameter,
                    actualCount.ToString(CultureInfo.InvariantCulture),
                    isMatch ? "數量符合" : "數量不符，請確認影像或參數");
            objectDetectionImageCheckStatusLabel.ForeColor =
                isMatch ? Color.ForestGreen : Color.Firebrick;
        }

        private void AppendObjectDetectionParameterQuantityToStatusLabel(string definitionId)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal) ||
                parameter.ColumnCount <= 0 ||
                parameter.RowCount <= 0)
            {
                return;
            }

            ObjectDefinitionSettings definition = FindObjectDefinition(definitionId);
            int actualCount;
            if (definition == null || !TryGetCompletedObjectDefinitionCount(definition, out actualCount))
            {
                return;
            }

            long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
            bool isMatch = expectedCount == actualCount;
            statusLabel.Text += " || 數量確認：" +
                (isMatch ? "符合 " : "不符 ") +
                actualCount.ToString(CultureInfo.InvariantCulture) +
                " / " + expectedCount.ToString(CultureInfo.InvariantCulture);
        }

        private bool TryGetCompletedObjectDefinitionCount(
            ObjectDefinitionSettings definition,
            out int actualCount)
        {
            actualCount = 0;
            if (definition == null)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                actualCount = objectDefinitionResults.Values
                    .Where(items => items != null)
                    .SelectMany(items => items)
                    .Count();
                return true;
            }
        }

        private static string BuildObjectDetectionParameterQuantityStatusText(
            ObjectDetectionParameterSettings parameter,
            string actualCount,
            string resultText)
        {
            string expectedCount = parameter == null
                ? "尚未設定"
                : parameter.ColumnCount > 0 && parameter.RowCount > 0
                    ? ((long)parameter.ColumnCount * parameter.RowCount)
                        .ToString(CultureInfo.InvariantCulture)
                    : "尚未設定";
            return "預期物件數量：" + expectedCount +
                "\r\n實際找到數量：" + (actualCount ?? "尚未確認") +
                "\r\n" + (resultText ?? string.Empty);
        }

        private void NotifyObjectDetectionParameterSourceProcessingCompleted(
            string definitionId,
            bool succeeded,
            string errorMessage)
        {
            if (string.IsNullOrWhiteSpace(activeObjectDetectionParameterId))
            {
                return;
            }

            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null ||
                !string.Equals(parameter.ObjectDefinitionId, definitionId, StringComparison.Ordinal))
            {
                return;
            }

            if (succeeded)
            {
                RefreshObjectDetectionParameterQuantityStatus(parameter);
                AppendObjectDetectionParameterQuantityToStatusLabel(definitionId);
                RefreshObjectDetectionMeasurementDisplay();
                TryApplySavedObjectDetectionFlatFieldCalibration(parameter);
            }
            else
            {
                UpdateObjectDetectionParameterSourceStatus(
                    parameter.Id,
                    "影像確認失敗：" + (errorMessage ?? "未知錯誤"),
                    Color.Firebrick);
            }
        }

        private void EnsureObjectDetectionParameterTabs()
        {
            if (objectDetectionParameterTabControl != null || imageLayoutPanel == null)
            {
                return;
            }

            objectDetectionParameterTabControl = new TabControl
            {
                Dock = DockStyle.Fill,
                Name = "objectDetectionParameterTabControl",
                Visible = false
            };
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "主參數設定",
                    "主參數設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "尺寸量測設定",
                    "尺寸量測設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "尺寸良品判斷條件",
                    "尺寸良品判斷條件將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "平場校正設定",
                    "平場校正設定將在此處配置。"));
            objectDetectionParameterTabControl.TabPages.Add(
                CreateObjectDetectionParameterTabPage(
                    "缺陷量測設定",
                    "缺陷量測設定將在此處配置。"));
            imageLayoutPanel.Controls.Add(objectDetectionParameterTabControl, 1, 0);
            objectDetectionParameterTabControl.BringToFront();
        }

        private static TabPage CreateObjectDetectionParameterTabPage(
            string title,
            string placeholderText)
        {
            var tabPage = new TabPage
            {
                Text = title,
                Padding = new Padding(12),
                BackColor = Color.FromArgb(250, 251, 253)
            };
            var label = new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 36,
                Text = placeholderText,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.FromArgb(75, 83, 95)
            };
            tabPage.Controls.Add(label);
            return tabPage;
        }

        private void UpdateObjectDetectionParameterTabs(ObjectDetectionParameterSettings parameter)
        {
            EnsureObjectDetectionParameterTabs();
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            ObjectDefinitionSettings definition = string.IsNullOrEmpty(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            string sourceName = definition == null
                ? "未指定物件定義結果"
                : GetObjectDefinitionDisplayName(
                    definition,
                    systemParameters.ObjectDefinitions.IndexOf(definition));
            BuildObjectDetectionMainParameterTab(parameter, sourceName);
            BuildObjectDetectionSizeMeasurementTab(parameter, sourceName);
            BuildObjectDetectionGoodJudgementTab(parameter, sourceName);
            BuildObjectDetectionFlatFieldTab(parameter, sourceName);
            BuildObjectDetectionDefectMeasurementTab(parameter, sourceName);
        }

        private void BuildObjectDetectionDefectMeasurementTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[4];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionDefectObjectNumberPanel = null;
                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                Control executionPanel = BuildObjectDetectionDefectExecutionPanel(parameter);
                var pageLayout = new TableLayoutPanel
                {
                    Dock = DockStyle.Fill,
                    ColumnCount = 1,
                    RowCount = 2,
                    Margin = Padding.Empty,
                    Padding = Padding.Empty
                };
                pageLayout.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 100f));
                pageLayout.RowStyles.Add(new RowStyle(SizeType.Absolute, 38f));
                pageLayout.RowStyles.Add(new RowStyle(SizeType.Percent, 100f));
                executionPanel.Dock = DockStyle.Fill;
                pageLayout.Controls.Add(executionPanel, 0, 0);
                pageLayout.Controls.Add(contentPanel, 0, 1);

                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "缺陷量測來源：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                var instruction = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 30,
                    Text = "選擇物件序號，左側影像會聚焦到對應物件。",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };

                if (parameter.ColumnCount <= 0 || parameter.RowCount <= 0)
                {
                    contentPanel.Controls.Add(CreateObjectDetectionQuantityMessage(
                        "請先在主參數設定輸入有效的列數與排數。"));
                }
                else if ((long)parameter.ColumnCount * parameter.RowCount > 10000)
                {
                    contentPanel.Controls.Add(CreateObjectDetectionQuantityMessage(
                        "序號按鈕數量過大，請先縮小列數與排數設定。"));
                }
                else
                {
                    long expectedCount = (long)parameter.ColumnCount * parameter.RowCount;
                    if (selectedObjectDetectionNumber > expectedCount)
                    {
                        selectedObjectDetectionNumber = -1;
                    }

                    objectDetectionDefectObjectNumberPanel =
                        CreateObjectDetectionObjectNumberPanel(parameter);
                    contentPanel.Controls.Add(
                        BuildObjectDetectionDefectCoreTabs(parameter));
                    contentPanel.Controls.Add(
                        BuildObjectDetectionDefectInspectionRegionPanel(parameter));
                    contentPanel.Controls.Add(objectDetectionDefectObjectNumberPanel);
                }

                contentPanel.Controls.Add(instruction);
                contentPanel.Controls.Add(sourceLabel);
                tabPage.Controls.Add(pageLayout);
                UpdateObjectDetectionNumberButtonState(parameter);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private static Label CreateObjectDetectionQuantityMessage(string text)
        {
            return new Label
            {
                Dock = DockStyle.Top,
                AutoSize = false,
                Height = 42,
                Text = text,
                TextAlign = ContentAlignment.TopLeft,
                ForeColor = Color.Firebrick
            };
        }

        private TableLayoutPanel CreateObjectDetectionObjectNumberPanel(
            ObjectDetectionParameterSettings parameter)
        {
            var numberPanel = new TableLayoutPanel
            {
                Dock = DockStyle.Top,
                AutoSize = true,
                AutoSizeMode = AutoSizeMode.GrowAndShrink,
                ColumnCount = parameter.ColumnCount,
                RowCount = parameter.RowCount,
                Padding = new Padding(0, 4, 0, 4),
                CellBorderStyle = TableLayoutPanelCellBorderStyle.Single
            };
            for (int column = 0; column < parameter.ColumnCount; column++)
            {
                numberPanel.ColumnStyles.Add(
                    new ColumnStyle(SizeType.Percent, 100f / parameter.ColumnCount));
            }

            for (int row = 0; row < parameter.RowCount; row++)
            {
                numberPanel.RowStyles.Add(new RowStyle(SizeType.Absolute, 36f));
                for (int column = 0; column < parameter.ColumnCount; column++)
                {
                    int number = (row * parameter.ColumnCount) + column + 1;
                    var button = new Button
                    {
                        Text = number.ToString(CultureInfo.InvariantCulture),
                        Dock = DockStyle.Fill,
                        Margin = new Padding(2),
                        Tag = number,
                        UseVisualStyleBackColor = true
                    };
                    button.Click += ObjectDetectionNumberButton_Click;
                    numberPanel.Controls.Add(button, column, row);
                }
            }

            return numberPanel;
        }

        private void ObjectDetectionNumberButton_Click(object sender, EventArgs e)
        {
            Button button = sender as Button;
            if (button == null || !(button.Tag is int))
            {
                return;
            }

            int number = (int)button.Tag;
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            bool defectSettingsSelected = objectDetectionParameterTabControl != null &&
                objectDetectionParameterTabControl.SelectedIndex == 4;
            bool clearDefectObjectSelection = defectSettingsSelected &&
                selectedObjectDetectionNumber == number;
            selectedObjectDetectionNumber = clearDefectObjectSelection ? -1 : number;
            UpdateObjectDetectionNumberButtonState(
                parameter);

            if (defectSettingsSelected)
            {
                CancelObjectDetectionDefectInspectionRegionDraftForObjectChange(number);
            }
            UpdateObjectDetectionDefectInspectionRegionControls(parameter);
            if (clearDefectObjectSelection)
            {
                if (objectDetectionDefectDisplayControl != null)
                {
                    objectDetectionDefectDisplayControl.InvalidateImageView();
                }
                statusLabel.Text = parameter == null
                    ? "目前未選取物件"
                    : parameter.DisplayName + "：目前未選取物件";
                return;
            }

            ObjectDefinitionSettings definition = parameter == null ||
                string.IsNullOrWhiteSpace(parameter.ObjectDefinitionId)
                ? null
                : FindObjectDefinition(parameter.ObjectDefinitionId);
            ObjectDefinitionDetectedObject detectedObject;
            if (definition == null ||
                !TryGetCompletedObjectDefinitionObject(
                    definition,
                    number,
                    out detectedObject))
            {
                statusLabel.Text = "物件" + number.ToString(CultureInfo.InvariantCulture) +
                    " 尚未找到可用的物件結果";
                return;
            }

            if (parameter.MeasurementClipLinesToMask)
            {
                ObjectDetectionMeasurementRecordSettings record =
                    GetActiveObjectDetectionMeasurementRecord(parameter);
                if (record != null)
                {
                    PrepareObjectDetectionMeasurementClipLines(parameter, record);
                }
            }

            if (leftImageTabControl != null && defectSettingsSelected &&
                objectDetectionDefectDisplayTabPage != null &&
                leftImageTabControl.TabPages.Contains(objectDetectionDefectDisplayTabPage))
            {
                leftImageTabControl.SelectedTab = objectDetectionDefectDisplayTabPage;
                RefreshObjectDetectionDefectDisplay();
            }
            else if (leftImageTabControl != null &&
                objectDetectionMeasurementTabPage != null &&
                leftImageTabControl.TabPages.Contains(objectDetectionMeasurementTabPage))
            {
                leftImageTabControl.SelectedTab = objectDetectionMeasurementTabPage;
                RefreshObjectDetectionMeasurementDisplay();
            }

            FocusObjectDetectionImage(detectedObject.Bounds);
            if (defectSettingsSelected && objectDetectionDefectDisplayControl != null)
            {
                objectDetectionDefectDisplayControl.InvalidateImageView();
            }
            statusLabel.Text = parameter.DisplayName + "：目前顯示物件" +
                number.ToString(CultureInfo.InvariantCulture);
        }

        private void UpdateObjectDetectionNumberButtonState(
            ObjectDetectionParameterSettings parameter)
        {
            if (objectDetectionObjectNumberPanel == null &&
                objectDetectionDefectObjectNumberPanel == null &&
                objectDetectionDefectIntegrationRoiSelector == null)
            {
                return;
            }

            UpdateObjectDetectionNumberButtonState(objectDetectionObjectNumberPanel);
            UpdateObjectDetectionNumberButtonState(objectDetectionDefectObjectNumberPanel);
            UpdateObjectDetectionDefectIntegrationRoiButtonState();
            RefreshObjectDetectionDefectIntegrationResults(parameter);
        }

        private void UpdateObjectDetectionNumberButtonState(TableLayoutPanel numberPanel)
        {
            if (numberPanel == null)
            {
                return;
            }

            foreach (Control control in numberPanel.Controls)
            {
                Button button = control as Button;
                if (button == null || !(button.Tag is int))
                {
                    continue;
                }

                int number = (int)button.Tag;
                button.BackColor = number == selectedObjectDetectionNumber
                    ? Color.FromArgb(190, 220, 255)
                    : SystemColors.Control;
                button.FlatStyle = number == selectedObjectDetectionNumber
                    ? FlatStyle.Flat
                    : FlatStyle.Standard;
            }
        }

        private bool TryGetCompletedObjectDefinitionObject(
            ObjectDefinitionSettings definition,
            int number,
            out ObjectDefinitionDetectedObject detectedObject)
        {
            detectedObject = null;
            if (definition == null || number <= 0)
            {
                return false;
            }

            string processingSignature = CreateObjectDefinitionProcessingSignature(definition);
            if (!HasCompletedObjectDefinitionResult(definition, processingSignature))
            {
                return false;
            }

            lock (objectDefinitionResultLock)
            {
                if (!string.Equals(activeObjectDefinitionResultId, definition.Id, StringComparison.Ordinal) ||
                    !string.Equals(
                        completedObjectDefinitionProcessingSignature,
                        processingSignature,
                        StringComparison.Ordinal))
                {
                    return false;
                }

                foreach (List<ObjectDefinitionDetectedObject> items in objectDefinitionResults.Values)
                {
                    if (items == null)
                    {
                        continue;
                    }

                    detectedObject = items.FirstOrDefault(item => item.Number == number);
                    if (detectedObject != null)
                    {
                        return true;
                    }
                }
            }

            detectedObject = null;
            return false;
        }

        private void FocusObjectDetectionImage(Rectangle objectBounds)
        {
            ImageDisplayControl[] displays = new ImageDisplayControl[]
            {
                leftOriginalDisplayControl,
                leftPreprocessedDisplayControl,
                leftProcessedDisplayControl,
                leftBlockProcessingDisplayControl,
                objectDetectionMeasurementDisplayControl,
                objectDetectionFlatFieldPreviewDisplayControl,
            }.Concat(GetAllObjectDetectionDefectDisplayControls()).ToArray();

            ImageDisplayControl anchor = displays.FirstOrDefault(
                display => display != null && display.HasImage);
            if (anchor == null)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                anchor.FocusOnSourceRectangle(objectBounds);
                ImageViewState viewState = anchor.ViewState;
                sharedImageViewState = viewState;
                hasSharedImageViewState = true;
                foreach (ImageDisplayControl display in displays)
                {
                    if (display != null && display.HasImage)
                    {
                        display.ApplyViewState(viewState);
                    }
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void BuildObjectDetectionMainParameterTab(
            ObjectDetectionParameterSettings parameter,
            string sourceName)
        {
            if (objectDetectionParameterTabControl == null || parameter == null)
            {
                return;
            }

            TabPage tabPage = objectDetectionParameterTabControl.TabPages[0];
            tabPage.SuspendLayout();
            try
            {
                tabPage.Controls.Clear();
                objectDetectionImageCheckStatusLabel = null;
                var contentPanel = new Panel
                {
                    Dock = DockStyle.Fill,
                    AutoScroll = true
                };
                var sourceLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 34,
                    Text = "來源物件定義結果：" + sourceName,
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                tabPage.Controls.Add(sourceLabel);

                var group = new GroupBox
                {
                    Text = "數量設定",
                    Dock = DockStyle.Top,
                    Height = 132,
                    Padding = new Padding(10)
                };

                var columnLabel = new Label
                {
                    Text = "列：",
                    Left = 12,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var columnBox = new NumericUpDown
                {
                    Left = 62,
                    Top = 26,
                    Width = 110,
                    Minimum = 0.000001m,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.ColumnCount))
                };
                var rowLabel = new Label
                {
                    Text = "排：",
                    Left = 198,
                    Top = 28,
                    Width = 48,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var rowBox = new NumericUpDown
                {
                    Left = 248,
                    Top = 26,
                    Width = 110,
                    Minimum = 0.000001m,
                    Maximum = 10000,
                    Value = Math.Max(0, Math.Min(10000, parameter.RowCount))
                };
                group.Controls.Add(columnLabel);
                group.Controls.Add(columnBox);
                group.Controls.Add(rowLabel);
                group.Controls.Add(rowBox);

                var confirm = new Button
                {
                    Text = "確認",
                    Left = 12,
                    Top = 70,
                    Width = 346,
                    Height = 28
                };
                confirm.Click += delegate
                {
                    parameter.ColumnCount = Decimal.ToInt32(columnBox.Value);
                    parameter.RowCount = Decimal.ToInt32(rowBox.Value);
                    SaveSystemParameters();
                    statusLabel.Text = parameter.DisplayName +
                        " 已套用數量設定：列 " + parameter.ColumnCount.ToString(CultureInfo.InvariantCulture) +
                        "、排 " + parameter.RowCount.ToString(CultureInfo.InvariantCulture);
                    UpdateObjectDetectionParameterTabs(parameter);
                    RefreshObjectDetectionParameterQuantityStatus(parameter);
                    AppendObjectDetectionParameterQuantityToStatusLabel(parameter.ObjectDefinitionId);
                };
                group.Controls.Add(confirm);

                var internalParametersGroup = new GroupBox
                {
                    Text = "內部參數",
                    Dock = DockStyle.Top,
                    Height = 176,
                    Padding = new Padding(10)
                };
                var xPrecisionLabel = new Label
                {
                    Text = "攝影機 X 向精度 (mm/pixel)：",
                    Left = 12,
                    Top = 27,
                    Width = 205,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var xPrecisionBox = new NumericUpDown
                {
                    Left = 220,
                    Top = 27,
                    Width = 138,
                    Minimum = 0,
                    Maximum = 1000,
                    DecimalPlaces = 6,
                    Increment = 0.000001m,
                    Value = (decimal)ClampObjectDetectionCameraPrecision(
                        parameter.CameraXMillimetersPerPixel)
                };
                var yPrecisionLabel = new Label
                {
                    Text = "攝影機 Y 向精度 (mm/pixel)：",
                    Left = 12,
                    Top = 58,
                    Width = 205,
                    Height = 24,
                    TextAlign = ContentAlignment.MiddleLeft
                };
                var yPrecisionBox = new NumericUpDown
                {
                    Left = 220,
                    Top = 58,
                    Width = 138,
                    Minimum = 0,
                    Maximum = 1000,
                    DecimalPlaces = 6,
                    Increment = 0.000001m,
                    Value = (decimal)ClampObjectDetectionCameraPrecision(
                        parameter.CameraYMillimetersPerPixel)
                };
                var applyPrecision = new Button
                {
                    Text = "套用內部參數",
                    Left = 12,
                    Top = 94,
                    Width = 346,
                    Height = 28
                };
                var enableMillimeterConversion = new CheckBox
                {
                    Left = 12,
                    Top = 124,
                    Width = 346,
                    Height = 22,
                    Text = "啟用 mm 換算（未啟用時以 pixel 計算）",
                    Checked = parameter.CameraPrecisionConfigured
                };
                applyPrecision.Click += delegate
                {
                    parameter.CameraXMillimetersPerPixel = Decimal.ToDouble(xPrecisionBox.Value);
                    parameter.CameraYMillimetersPerPixel = Decimal.ToDouble(yPrecisionBox.Value);
                    parameter.CameraPrecisionConfigured = enableMillimeterConversion.Checked;
                    SaveSystemParameters();
                    statusLabel.Text = parameter.DisplayName + " 已套用攝影機精度：X " +
                        parameter.CameraXMillimetersPerPixel.ToString("0.######", CultureInfo.InvariantCulture) +
                        "、Y " +
                        parameter.CameraYMillimetersPerPixel.ToString("0.######", CultureInfo.InvariantCulture) +
                        " mm/pixel；尺寸量測以 " +
                        (parameter.CameraPrecisionConfigured ? "mm" : "pixel") + " 顯示";
                };
                internalParametersGroup.Controls.Add(xPrecisionLabel);
                internalParametersGroup.Controls.Add(xPrecisionBox);
                internalParametersGroup.Controls.Add(yPrecisionLabel);
                internalParametersGroup.Controls.Add(yPrecisionBox);
                internalParametersGroup.Controls.Add(applyPrecision);
                internalParametersGroup.Controls.Add(enableMillimeterConversion);

                objectDetectionImageCheckStatusLabel = new Label
                {
                    Dock = DockStyle.Top,
                    AutoSize = false,
                    Height = 64,
                    Text = "尚未進行影像確認",
                    TextAlign = ContentAlignment.TopLeft,
                    ForeColor = Color.FromArgb(75, 83, 95)
                };
                contentPanel.Controls.Add(objectDetectionImageCheckStatusLabel);
                contentPanel.Controls.Add(internalParametersGroup);
                contentPanel.Controls.Add(group);
                contentPanel.Controls.Add(sourceLabel);
                tabPage.Controls.Add(contentPanel);
            }
            finally
            {
                tabPage.ResumeLayout(true);
            }
        }

        private static double ClampObjectDetectionCameraPrecision(double value)
        {
            if (double.IsNaN(value) || double.IsInfinity(value) || value <= 0.0)
            {
                return 1.0;
            }

            return Math.Max(0.000001, Math.Min(1000.0, value));
        }

        private void SetObjectDetectionParameterTabText(int tabIndex, string text)
        {
            if (objectDetectionParameterTabControl == null ||
                tabIndex < 0 ||
                tabIndex >= objectDetectionParameterTabControl.TabPages.Count)
            {
                return;
            }

            Label label = objectDetectionParameterTabControl.TabPages[tabIndex]
                .Controls
                .OfType<Label>()
                .FirstOrDefault();
            if (label != null)
            {
                label.Text = text;
            }
        }

        private void HideObjectDetectionParameterPanel()
        {
            if (objectDetectionParameterPanel != null)
            {
                objectDetectionParameterPanel.Visible = false;
            }
        }

        private void SetObjectDetectionParameterDisplayMode(bool enabled)
        {
            if (imageLayoutPanel == null || leftImageTabControl == null || rightImageTabControl == null)
            {
                return;
            }

            EnsureObjectDetectionMeasurementDisplay();
            EnsureObjectDetectionFlatFieldPreviewDisplay();
            EnsureObjectDetectionDefectDisplay();
            isObjectDetectionParameterImageLayout = enabled;
            if (!enabled)
            {
                InvalidateObjectDetectionMeasurementMaskCache();
            }
            EnsureObjectDetectionParameterTabs();
            if (isImageViewerMaximized)
            {
                SetImageViewerMaximized(false, false);
            }

            imageLayoutPanel.SuspendLayout();
            try
            {
                if (enabled)
                {
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(objectDetectionMeasurementTabPage);
                    leftImageTabControl.TabPages.Add(objectDetectionFlatFieldPreviewTabPage);
                    foreach (TabPage defectPage in objectDetectionDefectDisplayTabPages)
                    {
                        leftImageTabControl.TabPages.Add(defectPage);
                    }
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    objectDetectionMeasurementDisplayControl.TitleText = "左側 待量測";
                    objectDetectionFlatFieldPreviewDisplayControl.TitleText = "左側 平場校正";
                    for (int index = 0; index < objectDetectionDefectDisplayControls.Length; index++)
                    {
                        objectDetectionDefectDisplayControls[index].TitleText =
                            "左側 " + ObjectDetectionDefectDisplayNames[index];
                    }
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = false;
                    objectDetectionParameterTabControl.Visible = true;
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                }
                else
                {
                    ClearObjectDetectionDefectDisplayImage();
                    leftImageTabControl.TabPages.Clear();
                    leftImageTabControl.TabPages.Add(leftOriginalTabPage);
                    leftImageTabControl.TabPages.Add(leftPreprocessedTabPage);
                    leftImageTabControl.TabPages.Add(leftProcessedTabPage);
                    leftImageTabControl.TabPages.Add(leftBlockProcessingTabPage);
                    leftImageTabControl.TabPages.Add(leftObjectsTabPage);
                    leftImageTabControl.TabPages.Add(leftDebugTabPage);
                    leftImageTabControl.SelectedTab = leftOriginalTabPage;
                    if (leftObjectsDisplayControl != null)
                    {
                        leftObjectsDisplayControl.TitleText = "左側 區塊結果";
                    }
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = true;
                    if (objectDetectionParameterTabControl != null)
                    {
                        objectDetectionParameterTabControl.Visible = false;
                    }
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                    imageLayoutPanel.SetColumnSpan(rightImageTabControl, 1);
                }
            }
            finally
            {
                imageLayoutPanel.ResumeLayout(true);
            }

            ImageDisplayControl activeDisplay = GetVisibleLeftImageDisplayControl();
            if (activeDisplay != null)
            {
                activeDisplay.InvalidateImageView();
            }

            if (enabled)
            {
                RefreshObjectDetectionFlatFieldDisplay();
                RefreshObjectDetectionMeasurementDisplay();
            }
        }

        private sealed class ObjectDetectionDefinitionChoice
        {
            public string DisplayText { get; set; }

            public string Id { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDetectionMaskSourceChoice
        {
            public string DisplayText { get; set; }
            public string Id { get; set; }
            public string SourceType { get; set; }
            public string SourceNamespace { get; set; }

            public override string ToString()
            {
                return DisplayText;
            }
        }

        private sealed class ObjectDetectionMeasurementGeometry
        {
            public bool HasFirstLine { get; set; }

            public bool HasSecondLine { get; set; }

            public double StartX { get; set; }

            public double StartY { get; set; }

            public double EndX { get; set; }

            public double EndY { get; set; }

            public double SecondStartX { get; set; }

            public double SecondStartY { get; set; }

            public double SecondEndX { get; set; }

            public double SecondEndY { get; set; }

            public void Reset()
            {
                HasFirstLine = false;
                HasSecondLine = false;
                StartX = 0;
                StartY = 0;
                EndX = 1;
                EndY = 0;
                SecondStartX = 0;
                SecondStartY = 0;
                SecondEndX = 1;
                SecondEndY = 0;
                LineOrder = "LeftToRight";
                SecondLineOrder = "LeftToRight";
                StartOutsideRoi = false;
                EndOutsideRoi = false;
                SecondStartOutsideRoi = false;
                SecondEndOutsideRoi = false;
            }

            public string LineOrder { get; set; }

            public string SecondLineOrder { get; set; }

            public bool StartOutsideRoi { get; set; }

            public bool EndOutsideRoi { get; set; }

            public bool SecondStartOutsideRoi { get; set; }

            public bool SecondEndOutsideRoi { get; set; }
        }

        private sealed class ObjectDetectionMeasurementStatistics
        {
            public double Minimum { get; set; }

            public double Average { get; set; }

            public double Maximum { get; set; }

            public string Unit { get; set; }

            public ObjectDetectionImageLine MinimumLine { get; set; }

            public ObjectDetectionImageLine MaximumLine { get; set; }

            public List<ObjectDetectionImageLine> MeasuredLines { get; set; }

            public List<double> MeasuredLineLengths { get; set; }

            public List<List<ObjectDetectionImageLine>> MeasuredLineSegments { get; set; }

            public int ObjectNumber { get; set; }

            public Rectangle ObjectBounds { get; set; }
        }

        private struct ObjectDetectionMeasurementFrame
        {
            public ObjectDetectionMeasurementFrame(
                PointF center,
                float width,
                float height,
                double angleDegrees)
            {
                Center = center;
                Width = Math.Max(1f, width);
                Height = Math.Max(1f, height);
                double radians = angleDegrees * Math.PI / 180.0;
                Cosine = (float)Math.Cos(radians);
                Sine = (float)Math.Sin(radians);
            }

            public PointF Center { get; private set; }

            public float Width { get; private set; }

            public float Height { get; private set; }

            private float Cosine { get; set; }

            private float Sine { get; set; }

            public PointF ToLocal(float imageX, float imageY)
            {
                float dx = imageX - Center.X;
                float dy = imageY - Center.Y;
                return new PointF(
                    (Cosine * dx) + (Sine * dy),
                    (-Sine * dx) + (Cosine * dy));
            }

            public PointF ToLocal(int imageX, int imageY)
            {
                return ToLocal((float)imageX, (float)imageY);
            }

            public PointF ToLocal(Point imagePoint)
            {
                return ToLocal(imagePoint.X, imagePoint.Y);
            }

            public PointF ToNormalizedLocal(int imageX, int imageY)
            {
                PointF local = ToLocal(imageX, imageY);
                return new PointF(
                    0.5f + (local.X / Width),
                    0.5f + (local.Y / Height));
            }

            public PointF ToImage(double normalizedX, double normalizedY)
            {
                float localX = (float)((normalizedX - 0.5) * Width);
                float localY = (float)((normalizedY - 0.5) * Height);
                return ToImage(localX, localY);
            }

            public PointF ToImage(float localX, float localY)
            {
                return new PointF(
                    Center.X + (Cosine * localX) - (Sine * localY),
                    Center.Y + (Sine * localX) + (Cosine * localY));
            }
        }

        private struct ObjectDetectionImageLine
        {
            public ObjectDetectionImageLine(int x1, int y1, int x2, int y2)
            {
                X1 = x1;
                Y1 = y1;
                X2 = x2;
                Y2 = y2;
            }

            public ObjectDetectionImageLine(PointF start, PointF end)
                : this(
                    (int)Math.Round(start.X),
                    (int)Math.Round(start.Y),
                    (int)Math.Round(end.X),
                    (int)Math.Round(end.Y))
            {
            }

            public int X1 { get; private set; }

            public int Y1 { get; private set; }

            public int X2 { get; private set; }

            public int Y2 { get; private set; }
        }
    }
}
