using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.Globalization;
using System.IO;
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
        private void MainForm_KeyDown(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Control)
            {
                return;
            }

            isControlKeyDown = true;
            RefreshObjectAreaToolTip();
        }

        private void MainForm_KeyUp(object sender, KeyEventArgs e)
        {
            if (e.KeyCode != Keys.Control)
            {
                return;
            }

            isControlKeyDown = false;
            HideObjectAreaToolTip();
        }

        private void ObjectDefinitionDisplayControl_ImagePointerMoved(
            object sender,
            ImagePointerMovedEventArgs e)
        {
            ImageDisplayControl display = sender as ImageDisplayControl;
            if (display == null)
            {
                return;
            }

            if (objectAreaTooltipDisplay != null && !ReferenceEquals(objectAreaTooltipDisplay, display))
            {
                objectAreaToolTip.Hide(objectAreaTooltipDisplay);
            }

            objectAreaTooltipDisplay = display;
            objectAreaTooltipPointer = e;
            RefreshObjectAreaToolTip();
        }

        private void RefreshObjectAreaToolTip()
        {
            ImageDisplayControl display = objectAreaTooltipDisplay;
            ImagePointerMovedEventArgs pointer = objectAreaTooltipPointer;
            bool controlHeld = isControlKeyDown ||
                (Control.ModifierKeys & Keys.Control) == Keys.Control;
            if (display == null || pointer == null || !controlHeld ||
                !pointer.IsInsideViewer || !pointer.IsInsideImage || pointer.IsPanning)
            {
                HideObjectAreaToolTip();
                return;
            }

            ObjectDefinitionDetectedObject hit = FindObjectDefinitionObjectAtPoint(pointer.ImageLocation);
            if (hit == null)
            {
                HideObjectAreaToolTip();
                return;
            }

            string text = "面積：" + hit.Area.ToString("0.##", CultureInfo.InvariantCulture) + " px^2";
            Point location = new Point(pointer.ControlLocation.X + 12, pointer.ControlLocation.Y + 12);
            objectAreaToolTip.Show(text, display, location, 30000);
        }

        private void HideObjectAreaToolTip()
        {
            if (objectAreaToolTip == null)
            {
                return;
            }

            if (objectAreaTooltipDisplay != null)
            {
                objectAreaToolTip.Hide(objectAreaTooltipDisplay);
            }
            else
            {
                objectAreaToolTip.Hide(this);
            }
        }

        private void VisibleImageTabControl_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (ReferenceEquals(sender, leftImageTabControl) &&
                isObjectDetectionResultReviewMode && hasSharedImageViewState)
            {
                ImageDisplayControl newlySelectedDisplay = GetVisibleLeftImageDisplayControl();
                if (newlySelectedDisplay != null)
                {
                    if (!newlySelectedDisplay.HasImage)
                    {
                        objectDetectionResultReviewPendingViewRestores[newlySelectedDisplay] =
                            sharedImageViewState;
                    }
                    else
                    {
                        objectDetectionResultReviewPendingViewRestores.Remove(newlySelectedDisplay);
                    }
                }
            }

            if (ReferenceEquals(sender, leftImageTabControl) &&
                IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
            {
                UpdateActiveObjectDetectionDefectDisplayAlias();
                int coreIndex = GetObjectDetectionDefectDisplayIndex(leftImageTabControl.SelectedTab);
                if (objectDetectionDefectCoreTabs != null &&
                    coreIndex >= 0 && coreIndex < ObjectDetectionDefectCoreKeys.Length &&
                    objectDetectionDefectCoreTabs.SelectedIndex != coreIndex)
                {
                    objectDetectionDefectCoreTabs.SelectedIndex = coreIndex;
                }
                RefreshObjectDetectionDefectDisplay();
            }
            if (ReferenceEquals(sender, leftImageTabControl) &&
                leftImageTabControl.SelectedTab == objectDetectionFlatFieldPreviewTabPage)
            {
                RefreshObjectDetectionFlatFieldDisplay();
            }
            RequestPreprocessedImageUpdate();
            UpdateVisibleProcessedImageIfNeeded();
            if (ReferenceEquals((sender as TabControl)?.SelectedTab, leftBlockProcessingTabPage) ||
                ReferenceEquals((sender as TabControl)?.SelectedTab, rightBlockProcessingTabPage))
            {
                InvalidateBlockProcessingDisplays();
            }
            UpdateObjectDefinitionDisplayTimingIfNeeded();
            if (isImageViewerMaximized)
            {
                ApplyMaximizedSideViewState(sender as TabControl);
                return;
            }

            BeginInvoke(new Action(ApplySharedImageViewStateToVisibleControls));
        }

        private void MainForm_Load(object sender, EventArgs e)
        {
            WindowState = FormWindowState.Maximized;
            functionListBox.SelectedIndex = functionListBox.Items.IndexOf(LoadImageMenuText);
            statusLabel.Text = "介面框架準備就緒";
            BeginInvoke(new Action(async () => await RestoreSystemParametersAsync()));
        }

        private void FunctionListBox_SelectedIndexChanged(object sender, EventArgs e)
        {
            if (isUpdatingFunctionListText ||
                isImportingDetectionParameterSettings ||
                isRebuildingImagePreprocessingMenu ||
                isRebuildingObjectJudgementMenu ||
                isRebuildingObjectDefinitionMenu ||
                isRebuildingObjectDetectionParameterMenu)
            {
                return;
            }

            string selectedFunction = functionListBox.SelectedItem as string;
            if (string.IsNullOrEmpty(selectedFunction))
            {
                ResetRightFunctionPanel();
                rightPanelTitleLabel.Text = "參數設定";
                return;
            }

            ResetRightFunctionPanel();
            rightPanelTitleLabel.Text = selectedFunction + " 參數";
            if (selectedFunction == DetectionParameterLoadMenuText)
            {
                parameterPlaceholderLabel.Text = "選取已保存的檢測參數設定檔，覆蓋目前流程設定。";
            }
            else if (selectedFunction == LoadImageMenuText)
            {
                parameterPlaceholderLabel.Text = "點選左側「讀取圖片」後，選擇要載入的圖片。";
            }
            else if (selectedFunction == RoiMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "展開「指定 ROI」後可新增 ROI。";
            }
            else if (selectedFunction == FindObjectFlowMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "依序設定影像前處理、影像處理、影像關聯、區塊整合與物件定義。";
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = selectedFunction.Trim() + " 是目前選用的 ROI。影像處理會套用這個 ROI。";
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                HideImageProcessingFlowTree();
                parameterPlaceholderLabel.Text = "展開「影像處理」後，可新增影像處理流程。";
            }
            else if (selectedFunction == ImageRelationMenuText)
            {
                parameterPlaceholderLabel.Text = "右鍵選擇「新增關聯」，設定來源影像與影像處理項目。";
            }
            else if (selectedFunction == ObjectJudgementMenuText)
            {
                parameterPlaceholderLabel.Text = "這裡會顯示整合成區塊的設定與結果。";
            }
            else if (selectedFunction == ObjectDefinitionMenuText)
            {
                parameterPlaceholderLabel.Text = "右鍵選擇「新增物件組」，建立物件組項目。";
            }
            else if (selectedFunction == ObjectDetectionParameterMenuText)
            {
                parameterPlaceholderLabel.Text = "右鍵選擇「新增檢測參數」，建立檢測參數設定。";
            }
            else if (selectedFunction == ObjectDetectionResultReviewMenuText)
            {
                HideImageProcessingFlowTree();
                ShowObjectDetectionResultReviewPanel();
                return;
            }
            else if (GetObjectJudgementGroupId(selectedFunction) != null)
            {
                parameterPlaceholderLabel.Text = "目前選擇物件群組，可展開或收合其中的區塊。";
                if (IsAKeyDown())
                {
                    ProcessObjectJudgementGroup(GetObjectJudgementGroupId(selectedFunction));
                    aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                }
            }
            else
            {
                string objectDetectionParameterId;
                if (TryGetObjectDetectionParameterLocation(
                        functionListBox.SelectedIndex,
                        selectedFunction,
                        out objectDetectionParameterId))
                {
                    ShowObjectDetectionParameterPanel(objectDetectionParameterId);
                    return;
                }

                string objectDefinitionId;
                int objectDefinitionProcessingIndex;
                if (TryGetObjectDefinitionProcessingLocation(
                        functionListBox.SelectedIndex,
                        out objectDefinitionId,
                        out objectDefinitionProcessingIndex))
                {
                    ShowObjectDefinitionProcessingParameterPanel(
                        objectDefinitionId,
                        objectDefinitionProcessingIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectDefinitionProcessing(
                            objectDefinitionId,
                            GetObjectDefinitionProcessingId(functionListBox.SelectedIndex));
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                    return;
                }

                objectDefinitionId = GetObjectDefinitionId(
                    functionListBox.SelectedIndex,
                    selectedFunction);
                if (!string.IsNullOrEmpty(objectDefinitionId))
                {
                    ShowObjectDefinitionParameterPanel(objectDefinitionId);
                    if (IsAKeyDown())
                    {
                        ProcessObjectDefinition(objectDefinitionId);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                    return;
                }

                int objectIndex;
                int processingIndex;
                if (TryGetObjectJudgementProcessingLocation(
                        functionListBox.SelectedIndex,
                        selectedFunction,
                        out objectIndex,
                        out processingIndex))
                {
                    ShowObjectJudgementProcessingParameterPanel(objectIndex, processingIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectJudgementProcessing(objectIndex, processingIndex);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                }
                else if (GetObjectJudgementIndex(selectedFunction) >= 0)
                {
                    int selectedObjectIndex = GetObjectJudgementIndex(selectedFunction);
                    ShowObjectJudgementParameterPanel(selectedObjectIndex);
                    if (IsAKeyDown())
                    {
                        ProcessObjectJudgement(selectedObjectIndex);
                        aKeyProcessedFunctionListIndex = functionListBox.SelectedIndex;
                    }
                }
                else if (GetObjectDefinitionIndex(selectedFunction) >= 0)
                {
                    ShowObjectDefinitionParameterPanel(
                        GetObjectDefinitionId(functionListBox.SelectedIndex, selectedFunction));
                }
                else if (GetImageRelationGroupId(selectedFunction) != null)
                {
                    string imageRelationGroupId = GetImageRelationGroupId(selectedFunction);
                    ShowImageRelationGroup(selectedFunction);
                    if (IsAKeyDown())
                    {
                        ProcessImageRelationGroup(imageRelationGroupId);
                    }
                }
                else if (GetImageRelationIndex(selectedFunction) >= 0)
                {
                    selectedImageRelationGroupId = null;
                    selectedImageRelationIndex = GetImageRelationIndex(selectedFunction);
                    ShowImageRelationParameterPanel(selectedImageRelationIndex);
                    if (IsAKeyDown())
                    {
                        ProcessImageRelation(selectedImageRelationIndex);
                    }
                }
                else if (selectedFunction == ImagePreprocessingMenuText)
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "右鍵「影像前處理」可新增前處理流程。";
                }
                else if (string.Equals(selectedFunction, OriginalPreprocessingSourceText, StringComparison.Ordinal))
                {
                    activeImageRelationSourceType = "Original";
                    activeImageRelationSourceId = null;
                    parameterPlaceholderLabel.Text = "目前影像來源：原始影像。直接執行影像處理時會使用原圖。";
                }
                else if (IsImagePreprocessingStepMenuItem(selectedFunction))
                {
                    ShowImagePreprocessingFlowTree(selectedFunction);
                }
                else if (GetImagePreprocessingGroupId(selectedFunction) != null)
                {
                    ShowImagePreprocessingGroup(selectedFunction);
                }
                else if (GetImageProcessingGroupId(selectedFunction) != null)
                {
                    ShowImageProcessingGroup(selectedFunction);
                }
                else if (IsImageProcessingStepMenuItem(selectedFunction))
                {
                    ShowImageProcessingFlowTree(selectedFunction);
                }
                else if (IsImageProcessingStepCommandMenuItem(selectedFunction))
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "這裡會放置影像處理步驟的編輯命令。";
                }
                else
                {
                    HideImageProcessingFlowTree();
                    parameterPlaceholderLabel.Text = "這裡會顯示「" + selectedFunction + "」的參數設定與選項。";
                }
            }

            statusLabel.Text = "目前選擇：" + selectedFunction;
        }

        private void ResetRightFunctionPanel()
        {
            bool wasObjectDetectionResultReviewMode = isObjectDetectionResultReviewMode;
            ExitObjectDetectionResultReviewMode();
            HideImageRelationParameterPanel();
            HideObjectJudgementParameterPanel();
            HideObjectDefinitionParameterPanel();
            HideObjectDetectionParameterPanel();
            HideImageProcessingFlowTree();
            if (!wasObjectDetectionResultReviewMode)
            {
                SetObjectDetectionParameterDisplayMode(false);
            }
            parameterPlaceholderLabel.Visible = true;
            parameterPlaceholderLabel.BringToFront();
        }

        private async void FunctionListBox_MouseClick(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Left)
            {
                return;
            }

            int clickedIndex = functionListBox.IndexFromPoint(e.Location);
            if (clickedIndex < 0)
            {
                return;
            }

            string selectedFunction = functionListBox.Items[clickedIndex] as string;
            if (selectedFunction == FindObjectFlowMenuText)
            {
                ToggleFindObjectFlowMenu();
            }
            else if (selectedFunction == DetectionParameterLoadMenuText)
            {
                ImportDetectionParameterSettings();
            }
            else if (selectedFunction == LoadImageMenuText)
            {
                await OpenImageAsync();
            }
            else if (selectedFunction == RoiMenuText)
            {
                ToggleRoiMenu();
            }
            else if (IsRoiMenuItem(selectedFunction))
            {
                selectedRoiIndex = GetRoiIndex(selectedFunction);
                ApplySelectedRoiOverlay();
                statusLabel.Text = "目前選擇：" + selectedFunction.Trim();
            }
            else if (IsImageProcessingStepCommandMenuItem(selectedFunction) &&
                IsImageProcessingStepCommandForClickedStep(clickedIndex))
            {
                HandleImageProcessingStepCommand(selectedFunction);
            }
            else if (selectedFunction == ImageProcessingMenuText)
            {
                ToggleImageProcessingMenu();
            }
            else if (selectedFunction == ImagePreprocessingMenuText)
            {
                ToggleImagePreprocessingMenu();
            }
            else if (selectedFunction == ImageRelationMenuText)
            {
                ToggleImageRelationMenu();
            }
            else if (selectedFunction == ObjectJudgementMenuText)
            {
                ToggleObjectJudgementMenu();
            }
            else if (selectedFunction == ObjectDefinitionMenuText)
            {
                ToggleObjectDefinitionMenu();
            }
            else if (selectedFunction == ObjectDetectionParameterMenuText)
            {
                ToggleObjectDetectionParameterMenu();
            }
            else if (string.Equals(selectedFunction, OriginalPreprocessingSourceText, StringComparison.Ordinal))
            {
                activeImageRelationSourceType = "Original";
                activeImageRelationSourceId = null;
                // Selecting a source only defines the input for a later explicit
                // processing command.  Do not replace any currently displayed
                // preprocessing, processed, block, or object result here.
                statusLabel.Text = "目前選擇：原始影像（尚未執行）";
            }
            else if (GetImageRelationGroupId(selectedFunction) != null)
            {
                // Only a normal left click changes expansion. Ctrl/Shift are
                // reserved for selecting multiple relation groups.
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                    functionListBox.SelectedIndices.Count <= 1)
                {
                    ToggleImageRelationGroup(selectedFunction);
                }
            }
            string objectDetectionParameterId;
            if (TryGetObjectDetectionParameterLocation(
                    clickedIndex,
                    selectedFunction,
                    out objectDetectionParameterId))
            {
                ShowObjectDetectionParameterPanel(objectDetectionParameterId);
                return;
            }

            string objectDefinitionId;
            int objectDefinitionProcessingIndex;
            if (TryGetObjectDefinitionProcessingLocation(
                         clickedIndex,
                         out objectDefinitionId,
                         out objectDefinitionProcessingIndex))
            {
                string processingId = GetObjectDefinitionProcessingId(clickedIndex);
                if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                {
                    ProcessObjectDefinitionProcessing(objectDefinitionId, processingId);
                }

                aKeyProcessedFunctionListIndex = -1;
            }
            else if (!string.IsNullOrEmpty(objectDefinitionId = GetObjectDefinitionId(
                         clickedIndex,
                         selectedFunction)))
            {
                if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                {
                    ProcessObjectDefinition(objectDefinitionId);
                }

                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                    functionListBox.SelectedIndices.Count <= 1)
                {
                    ToggleObjectDefinition(objectDefinitionId);
                }

                aKeyProcessedFunctionListIndex = -1;
            }
            else
            {
                int objectProcessingOwnerIndex;
                int objectProcessingIndex;
                if (TryGetObjectJudgementProcessingLocation(
                        clickedIndex,
                        selectedFunction,
                        out objectProcessingOwnerIndex,
                        out objectProcessingIndex))
                {
                    if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                    {
                        ProcessObjectJudgementProcessing(
                            objectProcessingOwnerIndex,
                            objectProcessingIndex);
                    }

                    aKeyProcessedFunctionListIndex = -1;
                }
                else
                {
                    string objectJudgementGroupId = GetObjectJudgementGroupId(selectedFunction);
                    if (!string.IsNullOrEmpty(objectJudgementGroupId))
                    {
                        if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                        {
                            ProcessObjectJudgementGroup(objectJudgementGroupId);
                        }

                        // A group click expands or collapses its children. Ctrl-click
                        // multi-selection is handled by the ListBox and must not
                        // rebuild the list between selected items.
                        if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                            functionListBox.SelectedIndices.Count <= 1)
                        {
                            ToggleObjectJudgementGroup(objectJudgementGroupId);
                        }
                        aKeyProcessedFunctionListIndex = -1;
                    }
                    else
                    {
                    int objectIndex = GetObjectJudgementIndex(selectedFunction);
                    if (objectIndex >= 0)
                    {
                        if (IsAKeyDown() && aKeyProcessedFunctionListIndex != clickedIndex)
                        {
                            ProcessObjectJudgement(objectIndex);
                        }

                        // A block click only selects the block. Its processing
                        // children are expanded by a normal left click. Ctrl/Shift
                        // are reserved for multi-selection and must not rebuild
                        // the list between selected blocks.
                        if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None &&
                            functionListBox.SelectedIndices.Count <= 1)
                        {
                            ToggleObjectJudgement(objectIndex);
                        }
                        aKeyProcessedFunctionListIndex = -1;
                    }
                    else if (GetObjectDefinitionIndex(selectedFunction) >= 0)
                    {
                        ShowObjectDefinitionParameterPanel(
                            GetObjectDefinitionId(functionListBox.SelectedIndex, selectedFunction));
                    }
                    }
                }
            }
            if (IsImagePreprocessingStepMenuItem(selectedFunction))
            {
                if (IsAKeyDown())
                {
                    ProcessImagePreprocessingStep(selectedFunction);
                }
            }
            else if (GetImagePreprocessingGroupId(selectedFunction) != null)
            {
                if (IsAKeyDown())
                {
                    ProcessImagePreprocessingGroup(
                        GetImagePreprocessingGroupId(selectedFunction));
                }
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None)
                {
                    ToggleImagePreprocessingGroup(selectedFunction);
                }
            }
            else if (GetImageProcessingGroupId(selectedFunction) != null)
            {
                if (IsAKeyDown())
                {
                    ProcessImageProcessingGroup(GetImageProcessingGroupId(selectedFunction));
                }
                if ((ModifierKeys & (Keys.Control | Keys.Shift)) == Keys.None)
                {
                    ToggleImageProcessingGroup(selectedFunction);
                }
            }
            else if (IsImageProcessingStepMenuItem(selectedFunction))
            {
                if (IsAKeyDown())
                {
                    ProcessImageProcessingStep(selectedFunction);
                }
            }
        }

        private static bool IsAKeyDown()
        {
            return (GetAsyncKeyState((int)Keys.A) & 0x8000) != 0;
        }

        private void FunctionListBox_MouseUp(object sender, MouseEventArgs e)
        {
            if (e.Button != MouseButtons.Right)
            {
                return;
            }

            int clickedIndex = functionListBox.IndexFromPoint(e.Location);
            if (clickedIndex < 0)
            {
                return;
            }

            string stepText = functionListBox.Items[clickedIndex] as string;
            if (stepText == RoiMenuText)
            {
                ShowRoiMenuContextMenu(e.Location);
                return;
            }

            if (IsRoiMenuItem(stepText))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowRoiItemContextMenu(stepText, e.Location);
                return;
            }

            if (stepText == ImageProcessingMenuText)
            {
                ShowImageProcessingMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ImageRelationMenuText)
            {
                ShowImageRelationMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ObjectJudgementMenuText)
            {
                ShowObjectJudgementMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ObjectDefinitionMenuText)
            {
                ShowObjectDefinitionMenuContextMenu(e.Location);
                return;
            }

            if (stepText == ObjectDetectionParameterMenuText)
            {
                ShowObjectDetectionParameterMenuContextMenu(e.Location);
                return;
            }

            string objectDetectionParameterId;
            if (TryGetObjectDetectionParameterLocation(
                    clickedIndex,
                    stepText,
                    out objectDetectionParameterId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectDetectionParameterItemContextMenu(objectDetectionParameterId, e.Location);
                return;
            }

            string objectDefinitionId;
            int objectDefinitionProcessingIndex;
            if (TryGetObjectDefinitionProcessingLocation(
                    clickedIndex,
                    out objectDefinitionId,
                    out objectDefinitionProcessingIndex))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectDefinitionProcessingContextMenu(
                    objectDefinitionId,
                    GetObjectDefinitionProcessingId(clickedIndex),
                    e.Location);
                return;
            }

            objectDefinitionId = GetObjectDefinitionId(clickedIndex, stepText);
            if (!string.IsNullOrEmpty(objectDefinitionId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectDefinitionItemContextMenu(objectDefinitionId, e.Location);
                return;
            }

            string objectJudgementGroupId = GetObjectJudgementGroupId(stepText);
            if (!string.IsNullOrEmpty(objectJudgementGroupId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<string> selectedObjectJudgementGroupIds =
                    GetSelectedObjectJudgementGroupIds();
                if (selectedObjectJudgementGroupIds.Count >= 2)
                {
                    ShowObjectJudgementGroupMultiSelectContextMenu(
                        selectedObjectJudgementGroupIds,
                        e.Location);
                }
                else
                {
                    ShowObjectJudgementGroupContextMenu(objectJudgementGroupId, e.Location);
                }
                return;
            }

            int objectProcessingOwnerIndex;
            int objectProcessingIndex;
            if (TryGetObjectJudgementProcessingLocation(
                    clickedIndex,
                    stepText,
                    out objectProcessingOwnerIndex,
                    out objectProcessingIndex))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowObjectJudgementProcessingItemContextMenu(
                    objectProcessingOwnerIndex,
                    objectProcessingIndex,
                    e.Location);
                return;
            }

            int objectJudgementIndex = GetObjectJudgementIndex(stepText);
            if (objectJudgementIndex >= 0)
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<int> selectedObjectJudgementIndexes = GetSelectedObjectJudgementIndexes();
                if (selectedObjectJudgementIndexes.Count >= 2)
                {
                    ShowObjectJudgementMultiSelectContextMenu(
                        selectedObjectJudgementIndexes,
                        e.Location);
                }
                else
                {
                    ShowObjectJudgementItemContextMenu(objectJudgementIndex, e.Location);
                }
                return;
            }

            string clickedRelationGroupId = GetImageRelationGroupId(stepText);
            if (!string.IsNullOrEmpty(clickedRelationGroupId))
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                ShowImageRelationGroupContextMenu(clickedRelationGroupId, e.Location);
                return;
            }

            int relationIndex = GetImageRelationIndex(stepText);
            if (relationIndex >= 0)
            {
                if (!functionListBox.SelectedIndices.Contains(clickedIndex))
                {
                    functionListBox.ClearSelected();
                    functionListBox.SelectedIndex = clickedIndex;
                }

                List<int> selectedRelationIndexes = GetSelectedImageRelationIndexes();
                if (selectedRelationIndexes.Count >= 2)
                {
                    ShowImageRelationMultiSelectContextMenu(selectedRelationIndexes, e.Location);
                    return;
                }

                ShowImageRelationItemContextMenu(relationIndex, e.Location);
                return;
            }

            if (stepText == ImagePreprocessingMenuText)
            {
                ShowImagePreprocessingMenuContextMenu(e.Location);
                return;
            }

            string clickedPreprocessingGroupId = GetImagePreprocessingGroupId(stepText);
            if (!string.IsNullOrEmpty(clickedPreprocessingGroupId))
            {
                ShowImagePreprocessingGroupContextMenu(clickedPreprocessingGroupId, e.Location);
                return;
            }

            if (IsImagePreprocessingStepMenuItem(stepText))
            {
                List<int> selectedPreprocessingSteps = GetSelectedImagePreprocessingStepIndexes();
                if (selectedPreprocessingSteps.Count >= 2)
                {
                    ShowImagePreprocessingMultiSelectContextMenu(selectedPreprocessingSteps, e.Location);
                    return;
                }

                ShowImagePreprocessingStepContextMenu(stepText, e.Location);
                return;
            }

            string clickedGroupId = GetImageProcessingGroupId(stepText);
            if (!IsImageProcessingStepMenuItem(stepText) && string.IsNullOrEmpty(clickedGroupId))
            {
                return;
            }

            if (!functionListBox.SelectedIndices.Contains(clickedIndex))
            {
                functionListBox.ClearSelected();
                functionListBox.SelectedIndex = clickedIndex;
            }

            List<int> selectedStepIndexes = GetSelectedImageProcessingStepIndexes();
            List<string> selectedGroupIds = GetSelectedImageProcessingGroupIds();
            if (selectedStepIndexes.Count + selectedGroupIds.Count >= 2)
            {
                ShowImageProcessingGroupContextMenu(selectedStepIndexes, selectedGroupIds, e.Location);
                return;
            }

            if (IsImageProcessingStepMenuItem(stepText))
            {
                ShowImageProcessingStepContextMenu(stepText, e.Location);
                return;
            }

            if (!string.IsNullOrEmpty(clickedGroupId))
            {
                ShowImageProcessingGroupItemContextMenu(clickedGroupId, e.Location);
            }
        }

    }
}
