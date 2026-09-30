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
        private void ImageDisplayControl_ViewChanged(object sender, EventArgs e)
        {
            if (isSyncingImageView)
            {
                return;
            }

            var source = sender as ImageDisplayControl;
            if (source == null || !source.HasImage)
            {
                return;
            }

            ImageViewState pendingReviewViewState;
            if (isObjectDetectionResultReviewMode &&
                objectDetectionResultReviewPendingViewRestores.TryGetValue(
                    source,
                    out pendingReviewViewState))
            {
                objectDetectionResultReviewPendingViewRestores.Remove(source);
                if (source.IsUpdatingImageSource)
                {
                    isSyncingImageView = true;
                    try
                    {
                        sharedImageViewState = pendingReviewViewState;
                        hasSharedImageViewState = true;
                        source.ApplyViewState(pendingReviewViewState);
                    }
                    finally
                    {
                        isSyncingImageView = false;
                    }
                    return;
                }
            }

            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (ReferenceEquals(source, active))
                {
                    maximizedImageViewerViewState = source.ViewState;
                    hasMaximizedImageViewerViewState = true;
                }

                return;
            }

            if (isObjectDetectionParameterImageLayout)
            {
                ImageDisplayControl parameterLayoutVisible = GetVisibleLeftImageDisplayControl();
                if (ReferenceEquals(source, parameterLayoutVisible))
                {
                    sharedImageViewState = source.ViewState;
                    hasSharedImageViewState = true;
                }

                // The parameter layout hides the right image viewer. Do not
                // invalidate or synchronously refresh that hidden control on
                // every pan frame.
                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            ImageDisplayControl target = null;

            if (ReferenceEquals(source, leftVisible))
            {
                target = rightVisible;
            }
            else if (ReferenceEquals(source, rightVisible))
            {
                target = leftVisible;
            }

            if (isObjectDetectionParameterImageLayout &&
                ReferenceEquals(source, leftVisible) &&
                IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
            {
                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
            }

            if (target == null || !target.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                ImageViewState viewState = source.ViewState;
                bool isPanning = source.IsPanning;
                sharedImageViewState = viewState;
                hasSharedImageViewState = true;
                target.ApplyViewState(viewState, isPanning);

                if (isPanning)
                {
                    bool leftIsPriorityTarget = ReferenceEquals(target, leftVisible) &&
                        ReferenceEquals(source, rightVisible);
                    if (leftIsPriorityTarget)
                    {
                        // Keep the left viewer as the visual priority even
                        // when the mouse is dragging the right viewer. The
                        // right viewer's own invalidation remains queued and
                        // will paint after this bounded refresh.
                        pendingSynchronizedImagePanTarget = null;
                        synchronizedImagePanTimer.Stop();
                        target.RefreshImageViewNow();
                    }
                    else
                    {
                        // When dragging the left viewer, let it paint first
                        // and refresh the right viewer one frame later.
                        ScheduleSynchronizedImagePanRefresh(target);
                    }
                }
                else
                {
                    pendingSynchronizedImagePanTarget = null;
                    synchronizedImagePanTimer.Stop();
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ApplyMaximizedSideViewState(TabControl changedTabControl)
        {
            bool changedMaximizedSide = (isLeftImageViewerMaximized && ReferenceEquals(changedTabControl, leftImageTabControl)) ||
                (!isLeftImageViewerMaximized && ReferenceEquals(changedTabControl, rightImageTabControl));
            if (!changedMaximizedSide || !hasMaximizedImageViewerViewState)
            {
                return;
            }

            ImageDisplayControl active = isLeftImageViewerMaximized
                ? GetVisibleLeftImageDisplayControl()
                : GetVisibleRightImageDisplayControl();
            if (active == null || !active.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                active.ApplyViewState(maximizedImageViewerViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ScheduleSynchronizedImagePanRefresh(ImageDisplayControl target)
        {
            if (target == null || synchronizedImagePanTimer == null)
            {
                return;
            }

            pendingSynchronizedImagePanTarget = target;
            if (!synchronizedImagePanTimer.Enabled)
            {
                synchronizedImagePanTimer.Start();
            }
        }

        private void SynchronizedImagePanTimer_Tick(object sender, EventArgs e)
        {
            synchronizedImagePanTimer.Stop();
            ImageDisplayControl target = pendingSynchronizedImagePanTarget;
            pendingSynchronizedImagePanTarget = null;
            if (target == null || IsDisposed || !target.IsHandleCreated || !target.IsPanning)
            {
                return;
            }

            target.RefreshImageViewNow();
        }

        private void ImageTabControl_MouseDoubleClick(object sender, MouseEventArgs e)
        {
            var tabControl = sender as TabControl;
            if (tabControl == null || !IsImageTabHeader(tabControl, e.Location))
            {
                return;
            }

            bool maximizeLeft = ReferenceEquals(tabControl, leftImageTabControl);
            if (isImageViewerMaximized && isLeftImageViewerMaximized != maximizeLeft)
            {
                return;
            }

            SetImageViewerMaximized(!isImageViewerMaximized, maximizeLeft);
        }

        private static bool IsImageTabHeader(TabControl tabControl, Point location)
        {
            for (int index = 0; index < tabControl.TabCount; index++)
            {
                if (tabControl.GetTabRect(index).Contains(location))
                {
                    return true;
                }
            }

            return false;
        }

        private void SetImageViewerMaximized(bool maximize, bool maximizeLeft)
        {
            bool wasLeftMaximized = isLeftImageViewerMaximized;
            if (maximize && isImageViewerMaximized && wasLeftMaximized == maximizeLeft)
            {
                return;
            }

            imageLayoutPanel.SuspendLayout();
            try
            {
                isImageViewerMaximized = maximize;
                isLeftImageViewerMaximized = maximize && maximizeLeft;
                if (maximize)
                {
                    leftImageTabControl.Visible = maximizeLeft;
                    rightImageTabControl.Visible = !maximizeLeft;
                    imageLayoutPanel.SetColumnSpan(maximizeLeft ? leftImageTabControl : rightImageTabControl, 2);
                    ImageDisplayControl active = maximizeLeft
                        ? GetVisibleLeftImageDisplayControl()
                        : GetVisibleRightImageDisplayControl();
                    if (active != null && active.HasImage)
                    {
                        maximizedImageViewerViewState = active.ViewState;
                        hasMaximizedImageViewerViewState = true;
                    }
                    else
                    {
                        hasMaximizedImageViewerViewState = false;
                    }
                }
                else
                {
                    imageLayoutPanel.SetColumnSpan(leftImageTabControl, 1);
                    imageLayoutPanel.SetColumnSpan(rightImageTabControl, 1);
                    leftImageTabControl.Visible = true;
                    rightImageTabControl.Visible = true;
                    hasMaximizedImageViewerViewState = false;
                }
            }
            finally
            {
                imageLayoutPanel.ResumeLayout(true);
            }

            if (maximize)
            {
                ImageDisplayControl active = maximizeLeft
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (active != null)
                {
                    active.InvalidateImageView();
                }

                statusLabel.Text = maximizeLeft ? "左側影像已放大顯示" : "右側影像已放大顯示";
                return;
            }

            ImageDisplayControl restoreSource = wasLeftMaximized
                ? GetVisibleLeftImageDisplayControl()
                : GetVisibleRightImageDisplayControl();
            SynchronizeImageViewFrom(restoreSource);
            statusLabel.Text = "已還原雙側影像顯示";
        }

        private void SynchronizeImageViewFrom(ImageDisplayControl source)
        {
            if (source == null || !source.HasImage)
            {
                return;
            }

            ImageDisplayControl target = ReferenceEquals(source, GetVisibleLeftImageDisplayControl())
                ? GetVisibleRightImageDisplayControl()
                : GetVisibleLeftImageDisplayControl();
            if (target == null || !target.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
                target.ApplyViewState(sharedImageViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ImageDisplayControl_FitViewRequested(object sender, EventArgs e)
        {
            if (isSyncingImageView)
            {
                return;
            }

            var source = sender as ImageDisplayControl;
            if (source == null || !source.HasImage)
            {
                return;
            }

            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (ReferenceEquals(source, active))
                {
                    maximizedImageViewerViewState = source.ViewState;
                    hasMaximizedImageViewerViewState = true;
                }

                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            ImageDisplayControl target = null;

            if (ReferenceEquals(source, leftVisible))
            {
                target = rightVisible;
            }
            else if (ReferenceEquals(source, rightVisible))
            {
                target = leftVisible;
            }

            isSyncingImageView = true;
            try
            {
                if (target != null && target.HasImage)
                {
                    target.ResetViewToFit(false);
                }

                sharedImageViewState = source.ViewState;
                hasSharedImageViewState = true;
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void SyncVisibleImageDisplaysFromLeft()
        {
            if (isSyncingImageView || isImageViewerMaximized)
            {
                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            if (leftVisible == null || rightVisible == null || !leftVisible.HasImage || !rightVisible.HasImage)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                sharedImageViewState = leftVisible.ViewState;
                hasSharedImageViewState = true;
                rightVisible.ApplyViewState(leftVisible.ViewState);
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private void ApplySharedImageViewStateToVisibleControls()
        {
            if (isSyncingImageView || isImageViewerMaximized)
            {
                return;
            }

            ImageDisplayControl leftVisible = GetVisibleLeftImageDisplayControl();
            ImageDisplayControl rightVisible = GetVisibleRightImageDisplayControl();
            if (leftVisible == null || rightVisible == null)
            {
                return;
            }

            if (!hasSharedImageViewState)
            {
                if (leftVisible.HasImage)
                {
                    sharedImageViewState = leftVisible.ViewState;
                    hasSharedImageViewState = true;
                }
                else if (rightVisible.HasImage)
                {
                    sharedImageViewState = rightVisible.ViewState;
                    hasSharedImageViewState = true;
                }
            }

            if (!hasSharedImageViewState)
            {
                return;
            }

            isSyncingImageView = true;
            try
            {
                if (leftVisible.HasImage)
                {
                    leftVisible.ApplyViewState(sharedImageViewState);
                }

                if (rightVisible.HasImage)
                {
                    rightVisible.ApplyViewState(sharedImageViewState);
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private ImageDisplayControl GetVisibleLeftImageDisplayControl()
        {
            if (leftImageTabControl.SelectedTab == leftOriginalTabPage)
            {
                return leftOriginalDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftPreprocessedTabPage)
            {
                return leftPreprocessedDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                return leftProcessedDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftBlockProcessingTabPage)
            {
                return leftBlockProcessingDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == leftObjectsTabPage)
            {
                return leftObjectsDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == objectDetectionMeasurementTabPage)
            {
                return objectDetectionMeasurementDisplayControl;
            }

            if (leftImageTabControl.SelectedTab == objectDetectionFlatFieldPreviewTabPage)
            {
                return objectDetectionFlatFieldPreviewDisplayControl;
            }

            if (IsObjectDetectionDefectDisplayTab(leftImageTabControl.SelectedTab))
            {
                int defectIndex = GetObjectDetectionDefectDisplayIndex(leftImageTabControl.SelectedTab);
                return GetObjectDetectionDefectDisplayControl(defectIndex);
            }

            if (leftImageTabControl.SelectedTab == leftDebugTabPage)
            {
                return leftDebugDisplayControl;
            }

            return null;
        }

        private ImageDisplayControl GetVisibleRightImageDisplayControl()
        {
            if (rightImageTabControl.SelectedTab == rightOriginalTabPage)
            {
                return rightOriginalDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightPreprocessedTabPage)
            {
                return rightPreprocessedDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                return rightProcessedDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightBlockProcessingTabPage)
            {
                return rightBlockProcessingDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightObjectsTabPage)
            {
                return rightObjectsDisplayControl;
            }

            if (rightImageTabControl.SelectedTab == rightDebugTabPage)
            {
                return rightDebugDisplayControl;
            }

            return null;
        }

        private List<Rectangle> OrderRoiRectanglesForVisibleArea(IEnumerable<Rectangle> roiRectangles)
        {
            var rois = roiRectangles == null
                ? new List<Rectangle>()
                : roiRectangles.Where(roi => roi.Width > 0 && roi.Height > 0).ToList();
            if (rois.Count < 2)
            {
                return rois;
            }

            var visibleRectangles = new List<Rectangle>();
            ImageDisplayControl leftVisible = leftImageTabControl != null && leftImageTabControl.Visible
                ? GetVisibleLeftImageDisplayControl()
                : null;
            ImageDisplayControl rightVisible = rightImageTabControl != null && rightImageTabControl.Visible
                ? GetVisibleRightImageDisplayControl()
                : null;

            Rectangle visibleSourceRect;
            if (leftVisible != null && leftVisible.IsLargeImageMode &&
                leftVisible.TryGetVisibleSourceRectangle(out visibleSourceRect))
            {
                visibleRectangles.Add(visibleSourceRect);
            }

            if (rightVisible != null && rightVisible.IsLargeImageMode &&
                rightVisible.TryGetVisibleSourceRectangle(out visibleSourceRect))
            {
                visibleRectangles.Add(visibleSourceRect);
            }

            if (visibleRectangles.Count == 0)
            {
                return rois;
            }

            var scored = new List<Tuple<Rectangle, long, double>>();
            foreach (Rectangle roi in rois)
            {
                long bestIntersectionArea = 0;
                double bestCenterDistance = double.MaxValue;
                foreach (Rectangle visible in visibleRectangles)
                {
                    Rectangle intersection = Rectangle.Intersect(roi, visible);
                    long intersectionArea = (long)intersection.Width * intersection.Height;
                    double roiCenterX = roi.Left + (roi.Width / 2.0);
                    double roiCenterY = roi.Top + (roi.Height / 2.0);
                    double visibleCenterX = visible.Left + (visible.Width / 2.0);
                    double visibleCenterY = visible.Top + (visible.Height / 2.0);
                    double dx = roiCenterX - visibleCenterX;
                    double dy = roiCenterY - visibleCenterY;
                    double centerDistance = (dx * dx) + (dy * dy);
                    if (intersectionArea > bestIntersectionArea ||
                        (intersectionArea == bestIntersectionArea && centerDistance < bestCenterDistance))
                    {
                        bestIntersectionArea = intersectionArea;
                        bestCenterDistance = centerDistance;
                    }
                }

                scored.Add(Tuple.Create(roi, bestIntersectionArea, bestCenterDistance));
            }

            return scored
                .OrderByDescending(item => item.Item2 > 0)
                .ThenByDescending(item => item.Item2)
                .ThenBy(item => item.Item3)
                .Select(item => item.Item1)
                .ToList();
        }


        private void CaptureSharedImageViewStateFromVisibleControls()
        {
            ImageDisplayControl source;
            if (isImageViewerMaximized)
            {
                source = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
            }
            else
            {
                source = GetVisibleLeftImageDisplayControl();
                if (source == null || !source.HasImage)
                {
                    source = GetVisibleRightImageDisplayControl();
                }
            }

            if (source == null || !source.HasImage)
            {
                return;
            }

            sharedImageViewState = source.ViewState;
            hasSharedImageViewState = true;
            if (isImageViewerMaximized)
            {
                maximizedImageViewerViewState = sharedImageViewState;
                hasMaximizedImageViewerViewState = true;
            }
        }


        private void CaptureProcessedImageViewState()
        {
            ImageDisplayControl source = null;
            if (isImageViewerMaximized)
            {
                ImageDisplayControl active = isLeftImageViewerMaximized
                    ? GetVisibleLeftImageDisplayControl()
                    : GetVisibleRightImageDisplayControl();
                if (active == leftProcessedDisplayControl || active == rightProcessedDisplayControl)
                {
                    source = active;
                }
            }
            else if (leftImageTabControl.SelectedTab == leftProcessedTabPage)
            {
                source = leftProcessedDisplayControl;
            }
            else if (rightImageTabControl.SelectedTab == rightProcessedTabPage)
            {
                source = rightProcessedDisplayControl;
            }

            if (source == null || !source.HasImage)
            {
                return;
            }

            processedImageViewState = source.ViewState;
            hasProcessedImageViewState = true;
        }

        private void RestoreProcessedImageViewState()
        {
            if (!hasProcessedImageViewState) return;
            isSyncingImageView = true;
            try
            {
                ApplyImageViewState(leftProcessedDisplayControl, processedImageViewState);
                ApplyImageViewState(rightProcessedDisplayControl, processedImageViewState);
                if (isImageViewerMaximized)
                {
                    ImageDisplayControl active = isLeftImageViewerMaximized
                        ? GetVisibleLeftImageDisplayControl()
                        : GetVisibleRightImageDisplayControl();
                    if (active == leftProcessedDisplayControl || active == rightProcessedDisplayControl)
                    {
                        maximizedImageViewerViewState = processedImageViewState;
                        hasMaximizedImageViewerViewState = true;
                    }
                }
                else if (leftImageTabControl.SelectedTab == leftProcessedTabPage ||
                    rightImageTabControl.SelectedTab == rightProcessedTabPage)
                {
                    sharedImageViewState = processedImageViewState;
                    hasSharedImageViewState = true;
                }
            }
            finally
            {
                isSyncingImageView = false;
            }
        }

        private static void ApplyImageViewState(ImageDisplayControl display, ImageViewState viewState)
        {
            if (display != null && display.HasImage)
            {
                display.ApplyViewState(viewState);
            }
        }

    }
}
