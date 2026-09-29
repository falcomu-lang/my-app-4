using System;
using System.Drawing;
using System.Windows.Forms;
using IntegratedImageProcessingApp.Controls;
using IntegratedImageProcessingApp.Services;

namespace IntegratedImageProcessingApp.Forms
{
    public partial class MainForm
    {
        private string editingRoiId;

        private void BeginEditRoi(string roiId)
        {
            editingRoiId = null;
            int roiIndex = FindRoiIndexById(roiId);
            if (roiIndex < 0)
            {
                statusLabel.Text = "找不到指定的 ROI，請重新選取。";
                return;
            }

            ImageDisplayControl target = GetRoiEditingDisplay();
            if (target == null)
            {
                statusLabel.Text = "請先載入圖片，再編輯 ROI。";
                MessageBox.Show(
                    this,
                    "請先載入圖片，再編輯 ROI。",
                    "編輯 ROI",
                    MessageBoxButtons.OK,
                    MessageBoxIcon.Information);
                return;
            }

            selectedRoiIndex = roiIndex;
            SyncLegacyRoiFromSelectedRoi();
            RebuildVisibleRoiItems();
            functionListBox.SelectedItem = CreateRoiText(roiIndex + 1);
            ApplySelectedRoiOverlay();
            SelectRoiEditingDisplay(target);

            RoiRegionSettings roi = systemParameters.RoiRegions[roiIndex];
            if (!target.BeginRoiEditSelection(roi.Bounds))
            {
                statusLabel.Text = "無法在目前圖片範圍內編輯此 ROI。";
                return;
            }

            editingRoiId = roi.Id;
            statusLabel.Text = string.Format(
                "編輯 ROI {0}：Ctrl 拖曳邊線/角落調整，其他拖曳可平移；放開即保存。",
                roiIndex + 1);
        }

        private ImageDisplayControl GetRoiEditingDisplay()
        {
            if (leftImageTabControl.SelectedTab == leftOriginalTabPage &&
                leftOriginalDisplayControl.HasImage)
            {
                return leftOriginalDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightOriginalTabPage &&
                rightOriginalDisplayControl.HasImage)
            {
                return rightOriginalDisplayControl;
            }

            if (leftOriginalDisplayControl.HasImage)
            {
                return leftOriginalDisplayControl;
            }

            return rightOriginalDisplayControl.HasImage
                ? rightOriginalDisplayControl
                : null;
        }

        private void SelectRoiEditingDisplay(ImageDisplayControl display)
        {
            if (ReferenceEquals(display, leftOriginalDisplayControl))
            {
                leftImageTabControl.SelectedTab = leftOriginalTabPage;
            }
            else if (ReferenceEquals(display, rightOriginalDisplayControl))
            {
                rightImageTabControl.SelectedTab = rightOriginalTabPage;
            }
        }

        private int FindRoiIndexById(string roiId)
        {
            if (string.IsNullOrWhiteSpace(roiId))
            {
                return -1;
            }

            for (int index = 0; index < systemParameters.RoiRegions.Count; index++)
            {
                if (string.Equals(
                    systemParameters.RoiRegions[index].Id,
                    roiId,
                    StringComparison.Ordinal))
                {
                    return index;
                }
            }

            return -1;
        }

        private void ImageDisplayControl_RoiEdited(object sender, RoiSelectedEventArgs e)
        {
            int roiIndex = FindRoiIndexById(editingRoiId);
            if (roiIndex < 0 || e == null || e.Roi.Width < 3 || e.Roi.Height < 3)
            {
                editingRoiId = null;
                statusLabel.Text = "ROI 編輯未完成，原設定維持不變。";
                return;
            }

            systemParameters.RoiRegions[roiIndex].Bounds = e.Roi;
            selectedRoiIndex = roiIndex;
            SyncLegacyRoiFromSelectedRoi();
            SaveSystemParameters();
            RebuildVisibleRoiItems();
            functionListBox.SelectedItem = CreateRoiText(roiIndex + 1);
            ApplySelectedRoiOverlay();
            MarkProcessedImageDirty();
            ScheduleProcessedImageUpdateIfVisible();
            editingRoiId = null;
            statusLabel.Text = string.Format(
                "已更新 ROI {0}：X={1}, Y={2}, W={3}, H={4}",
                roiIndex + 1,
                e.Roi.X,
                e.Roi.Y,
                e.Roi.Width,
                e.Roi.Height);
        }
    }
}
