using System;
using System.Drawing;
using System.Linq;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private static readonly string[] ObjectDetectionDefectCoreKeys =
        {
            "FlatField", "Contrast1", "Contrast2", "Contrast3"
        };

        private const int ObjectDetectionDefectFrequencyDisplayIndex = 4;
        private const int ObjectDetectionDefectLineTextureDisplayIndex = 5;
        private const int ObjectDetectionDefectIntegratedDisplayIndex = 6;

        private static readonly string[] ObjectDetectionDefectDisplayNames =
        {
            "缺陷顯示-平場校正", "缺陷顯示-條件一", "缺陷顯示-條件二",
            "缺陷顯示-條件三", "缺陷顯示-頻域異常", "缺陷顯示-線狀紋理異常", "缺陷顯示-綜合"
        };

        private TabPage[] objectDetectionDefectDisplayTabPages;
        private Panel[] objectDetectionDefectDisplayHostPanels;
        private ImageDisplayControl[] objectDetectionDefectDisplayControls;
        private Label[] objectDetectionDefectDisplayPlaceholders;
        private string[] objectDetectionDefectDisplayParameterIds;
        private int[] objectDetectionDefectDisplayImageGenerations;

        // These aliases keep region editing and other existing interactions focused on the active core view.
        private TabPage objectDetectionDefectDisplayTabPage;
        private Panel objectDetectionDefectDisplayHostPanel;
        private ImageDisplayControl objectDetectionDefectDisplayControl;
        private Label objectDetectionDefectDisplayPlaceholder;
        private string objectDetectionDefectDisplayParameterId;
        private int objectDetectionDefectDisplayImageGeneration = -1;
        private bool objectDetectionDefectBasePreparationPending;
        private string objectDetectionDefectBaseAttemptParameterId;
        private int objectDetectionDefectBaseAttemptImageGeneration = -1;
        private int objectDetectionDefectBaseAttemptMaskGeneration = -1;

        private void EnsureObjectDetectionDefectDisplay()
        {
            if (leftImageTabControl == null)
            {
                return;
            }
            if (objectDetectionDefectDisplayControls != null)
            {
                UpdateActiveObjectDetectionDefectDisplayAlias();
                return;
            }

            int displayCount = ObjectDetectionDefectDisplayNames.Length;
            objectDetectionDefectDisplayTabPages = new TabPage[displayCount];
            objectDetectionDefectDisplayHostPanels = new Panel[displayCount];
            objectDetectionDefectDisplayControls = new ImageDisplayControl[displayCount];
            objectDetectionDefectDisplayPlaceholders = new Label[displayCount];
            objectDetectionDefectDisplayParameterIds = new string[displayCount];
            objectDetectionDefectDisplayImageGenerations = Enumerable.Repeat(-1, displayCount).ToArray();
            for (int index = 0; index < objectDetectionDefectDisplayTabPages.Length; index++)
            {
                int coreIndex = index;
                objectDetectionDefectDisplayTabPages[index] = CreateImageTabPage(
                    "leftObjectDetectionDefectDisplay" + index,
                    ObjectDetectionDefectDisplayNames[index],
                    out objectDetectionDefectDisplayHostPanels[index]);
                ImageDisplayControl display = CreateImageDisplayControl(
                    objectDetectionDefectDisplayHostPanels[index],
                    "左側 " + ObjectDetectionDefectDisplayNames[index]);
                objectDetectionDefectDisplayControls[index] = display;
                display.ImageMouseDown += ObjectDetectionDefectDisplayControl_ImageMouseDown;
                display.ImageMouseMove += ObjectDetectionDefectDisplayControl_ImageMouseMove;
                display.ImageMouseUp += ObjectDetectionDefectDisplayControl_ImageMouseUp;
                display.ImagePointerMoved += ObjectDetectionDefectDisplayControl_ImagePointerMoved;
                display.ImageOverlayPaint += ObjectDetectionDefectDisplayControl_ImageOverlayPaint;
                display.ViewChanged += ImageDisplayControl_ViewChanged;
                display.FitViewRequested += ImageDisplayControl_FitViewRequested;

                objectDetectionDefectDisplayPlaceholders[index] = new Label
                {
                    Dock = DockStyle.Fill,
                    BackColor = Color.FromArgb(250, 251, 253),
                    ForeColor = Color.FromArgb(92, 101, 114),
                    TextAlign = ContentAlignment.MiddleCenter,
                    Text = "尚未準備平場校正影像"
                };
                objectDetectionDefectDisplayHostPanels[index].Controls.Add(
                    objectDetectionDefectDisplayPlaceholders[index]);
                objectDetectionDefectDisplayPlaceholders[index].BringToFront();
            }
            UpdateActiveObjectDetectionDefectDisplayAlias();
        }

        private ImageDisplayControl[] GetAllObjectDetectionDefectDisplayControls()
        {
            return objectDetectionDefectDisplayControls ?? new ImageDisplayControl[0];
        }

        private TabPage GetObjectDetectionDefectDisplayTabPage(int coreIndex)
        {
            if (objectDetectionDefectDisplayTabPages == null || coreIndex < 0 ||
                coreIndex >= objectDetectionDefectDisplayTabPages.Length)
            {
                return null;
            }
            return objectDetectionDefectDisplayTabPages[coreIndex];
        }

        private ImageDisplayControl GetObjectDetectionDefectDisplayControl(int coreIndex)
        {
            if (objectDetectionDefectDisplayControls == null || coreIndex < 0 ||
                coreIndex >= objectDetectionDefectDisplayControls.Length)
            {
                return null;
            }
            return objectDetectionDefectDisplayControls[coreIndex];
        }

        private int GetObjectDetectionDefectDisplayIndex(TabPage page)
        {
            if (page == null || objectDetectionDefectDisplayTabPages == null)
            {
                return -1;
            }
            for (int index = 0; index < objectDetectionDefectDisplayTabPages.Length; index++)
            {
                if (ReferenceEquals(page, objectDetectionDefectDisplayTabPages[index]))
                {
                    return index;
                }
            }
            return -1;
        }

        private int GetObjectDetectionDefectDisplayIndex(ImageDisplayControl display)
        {
            if (display == null || objectDetectionDefectDisplayControls == null)
            {
                return -1;
            }
            for (int index = 0; index < objectDetectionDefectDisplayControls.Length; index++)
            {
                if (ReferenceEquals(display, objectDetectionDefectDisplayControls[index]))
                {
                    return index;
                }
            }
            return -1;
        }

        private bool IsObjectDetectionDefectDisplayTab(TabPage page)
        {
            return GetObjectDetectionDefectDisplayIndex(page) >= 0;
        }

        private string GetObjectDetectionDefectDisplayCoreKey(int displayIndex)
        {
            return displayIndex >= 0 && displayIndex < ObjectDetectionDefectCoreKeys.Length
                ? ObjectDetectionDefectCoreKeys[displayIndex]
                : ObjectDetectionDefectCoreKeys[0];
        }

        private int GetObjectDetectionDefectCoreIndex(string coreKey)
        {
            for (int index = 0; index < ObjectDetectionDefectCoreKeys.Length; index++)
            {
                if (string.Equals(ObjectDetectionDefectCoreKeys[index], coreKey,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }
            return 0;
        }

        private void UpdateActiveObjectDetectionDefectDisplayAlias()
        {
            int index = leftImageTabControl == null
                ? -1
                : GetObjectDetectionDefectDisplayIndex(leftImageTabControl.SelectedTab);
            if (objectDetectionDefectDisplayControls == null)
            {
                return;
            }
            if (index < 0)
            {
                if (objectDetectionDefectDisplayControl != null)
                {
                    return;
                }
                index = 0;
            }
            objectDetectionDefectDisplayTabPage = objectDetectionDefectDisplayTabPages[index];
            objectDetectionDefectDisplayHostPanel = objectDetectionDefectDisplayHostPanels[index];
            objectDetectionDefectDisplayControl = objectDetectionDefectDisplayControls[index];
            objectDetectionDefectDisplayPlaceholder = objectDetectionDefectDisplayPlaceholders[index];
            if (index < ObjectDetectionDefectCoreKeys.Length)
            {
                selectedObjectDetectionDefectCoreIndex = index;
            }
        }

        private void RefreshObjectDetectionDefectDisplay()
        {
            EnsureObjectDetectionDefectDisplay();
            if (objectDetectionDefectDisplayControls == null ||
                IsImageDisplayUpdateSuppressed ||
                !isObjectDetectionParameterImageLayout ||
                leftImageTabControl == null ||
                !IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
            {
                return;
            }

            UpdateActiveObjectDetectionDefectDisplayAlias();
            int activeIndex = GetObjectDetectionDefectDisplayIndex(leftImageTabControl.SelectedTab);
            ObjectDetectionParameterSettings parameter =
                FindObjectDetectionParameter(activeObjectDetectionParameterId);
            if (parameter == null)
            {
                ShowObjectDetectionDefectDisplayPlaceholder("請先選擇檢測參數");
                return;
            }

            if (IsCurrentObjectDetectionFlatFieldImage(parameter))
            {
                ImageDisplayControl display = GetObjectDetectionDefectDisplayControl(activeIndex);
                bool alreadyPrepared = display != null && display.HasImage &&
                    string.Equals(objectDetectionDefectDisplayParameterIds[activeIndex], parameter.Id,
                        StringComparison.Ordinal) &&
                    objectDetectionDefectDisplayImageGenerations[activeIndex] == imageSourceGeneration;
                if (!alreadyPrepared)
                {
                    if (objectDetectionFlatFieldCorrectedLargeSource != null)
                    {
                        display.SetSharedLargeImageSource(
                            objectDetectionFlatFieldCorrectedLargeSource,
                            true);
                    }
                    else if (objectDetectionFlatFieldPreviewDisplayControl != null &&
                        objectDetectionFlatFieldPreviewDisplayControl.HasImage)
                    {
                        if (display != null)
                        {
                            Bitmap correctedImage =
                                objectDetectionFlatFieldPreviewDisplayControl.CloneImage();
                            if (correctedImage != null)
                            {
                                display.SetDisplayImage(correctedImage, true);
                            }
                        }
                    }
                }

                if (display != null && display.HasImage)
                {
                    objectDetectionDefectDisplayParameterIds[activeIndex] = parameter.Id;
                    objectDetectionDefectDisplayImageGenerations[activeIndex] = imageSourceGeneration;
                    objectDetectionDefectDisplayParameterId = parameter.Id;
                    objectDetectionDefectDisplayImageGeneration = imageSourceGeneration;
                    ShowObjectDetectionDefectDisplayPlaceholder(null);
                    return;
                }
            }

            ClearObjectDetectionDefectDisplayImage();
            int maskCount;
            lock (objectDetectionFlatFieldMaskLock)
            {
                maskCount = objectDetectionFlatFieldMaskOverlays.Count;
            }

            bool hasCurrentProfile = objectDetectionFlatFieldSmoothedProfile != null &&
                string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionFlatFieldProfileImageGeneration == imageSourceGeneration &&
                objectDetectionFlatFieldProfileMaskGeneration == objectDetectionFlatFieldEvaluationGeneration;
            bool hasSavedProfile = !string.IsNullOrWhiteSpace(parameter.FlatFieldSavedProfileData) &&
                string.Equals(parameter.FlatFieldSavedSettingsSignature,
                    CreateObjectDetectionFlatFieldSettingsSignature(parameter), StringComparison.Ordinal);

            if (maskCount == 0)
            {
                ShowObjectDetectionDefectDisplayPlaceholder("請先套用平場校正來源 MASK");
                return;
            }
            if (!hasCurrentProfile && !hasSavedProfile)
            {
                ShowObjectDetectionDefectDisplayPlaceholder(
                    "尚無有效平場校正結果；請先計算並保存校正曲線");
                return;
            }
            if (string.Equals(objectDetectionDefectBaseAttemptParameterId, parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionDefectBaseAttemptImageGeneration == imageSourceGeneration &&
                objectDetectionDefectBaseAttemptMaskGeneration == objectDetectionFlatFieldEvaluationGeneration)
            {
                ShowObjectDetectionDefectDisplayPlaceholder(
                    "平場校正底圖準備失敗；請至平場校正分頁確認設定與狀態");
                return;
            }

            ShowObjectDetectionDefectDisplayPlaceholder("正在準備平場校正影像...");
            if (!objectDetectionDefectBasePreparationPending &&
                objectDetectionFlatFieldResultLabel != null &&
                !objectDetectionFlatFieldResultLabel.IsDisposed)
            {
                objectDetectionDefectBasePreparationPending = true;
                objectDetectionDefectBaseAttemptParameterId = parameter.Id;
                objectDetectionDefectBaseAttemptImageGeneration = imageSourceGeneration;
                objectDetectionDefectBaseAttemptMaskGeneration = objectDetectionFlatFieldEvaluationGeneration;
                PrepareObjectDetectionDefectDisplayBase(parameter, hasCurrentProfile);
            }
        }

        private bool IsCurrentObjectDetectionFlatFieldImage(ObjectDetectionParameterSettings parameter)
        {
            return parameter != null &&
                objectDetectionFlatFieldPreviewIsCorrected &&
                objectDetectionFlatFieldCorrectedImageGeneration == imageSourceGeneration &&
                string.Equals(objectDetectionFlatFieldProfileParameterId, parameter.Id,
                    StringComparison.Ordinal) &&
                objectDetectionFlatFieldProfileImageGeneration == imageSourceGeneration &&
                objectDetectionFlatFieldProfileMaskGeneration == objectDetectionFlatFieldEvaluationGeneration;
        }

        private async void PrepareObjectDetectionDefectDisplayBase(
            ObjectDetectionParameterSettings parameter,
            bool useCurrentProfile)
        {
            try
            {
                await ShowSavedObjectDetectionFlatFieldCalibration(
                    parameter,
                    objectDetectionFlatFieldResultLabel,
                    null,
                    useCurrentProfile);
            }
            finally
            {
                objectDetectionDefectBasePreparationPending = false;
                if (!IsDisposed && objectDetectionDefectDisplayControls != null &&
                    leftImageTabControl != null &&
                    IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
                {
                    RefreshObjectDetectionDefectDisplay();
                }
            }
        }

        private void ClearObjectDetectionDefectDisplayImage()
        {
            foreach (ImageDisplayControl display in GetAllObjectDetectionDefectDisplayControls())
            {
                if (display != null && display.HasImage)
                {
                    display.ClearImage();
                }
            }
            objectDetectionDefectDisplayParameterId = null;
            objectDetectionDefectDisplayImageGeneration = -1;
            if (objectDetectionDefectDisplayParameterIds != null)
            {
                Array.Clear(objectDetectionDefectDisplayParameterIds, 0,
                    objectDetectionDefectDisplayParameterIds.Length);
            }
            if (objectDetectionDefectDisplayImageGenerations != null)
            {
                for (int index = 0; index < objectDetectionDefectDisplayImageGenerations.Length; index++)
                {
                    objectDetectionDefectDisplayImageGenerations[index] = -1;
                }
            }
            ShowObjectDetectionDefectDisplayPlaceholder("尚未準備平場校正影像");
        }

        private void ShowObjectDetectionDefectDisplayPlaceholder(string text)
        {
            foreach (Label placeholder in objectDetectionDefectDisplayPlaceholders ?? new Label[0])
            {
                if (placeholder == null || placeholder.IsDisposed)
                {
                    continue;
                }
                placeholder.Text = text ?? string.Empty;
                placeholder.Visible = !string.IsNullOrEmpty(text);
                if (placeholder.Visible)
                {
                    placeholder.BringToFront();
                }
            }
        }
    }
}
