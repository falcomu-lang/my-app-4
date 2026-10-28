using System;
using System.Drawing;
using System.Windows.Forms;
using CameraCaptureApp.Forms;
using CameraCaptureApp.Services;

namespace IntegratedImageProcessingApp.Controls
{
    public sealed class CameraCaptureHostControl : UserControl, ICameraImageSource
    {
        private MainForm cameraForm;
        private EventHandler<CameraImageAcquiredEventArgs> imageAcquiredHandlers;
        private EventHandler<CameraImageAcquiredEventArgs> overviewImageCapturedHandlers;
        private EventHandler<CameraImageAcquiredEventArgs> remoteImageCapturedHandlers;
        private bool disposing;
        private bool hasCameraProgramBeenOpened;
        private bool cameraImageAcquiredSubscribed;
        private volatile bool suppressAutomaticInspectionForwarding;
        private volatile bool overviewCaptureForwardingEnabled;
        private volatile bool remoteCaptureForwardingEnabled;

        public bool IsCameraConnected
        {
            get
            {
                return cameraForm != null && !cameraForm.IsDisposed &&
                    cameraForm.IsCameraConnected;
            }
        }

        public bool IsCameraPreviewing
        {
            get
            {
                return cameraForm != null && !cameraForm.IsDisposed &&
                    cameraForm.IsCameraPreviewing;
            }
        }

        public bool IsSoftwareTriggerMode
        {
            get
            {
                return cameraForm != null && !cameraForm.IsDisposed &&
                    cameraForm.IsSoftwareTriggerMode;
            }
        }

        public bool ClearMeterWheelEncoder(out string message)
        {
            if (!hasCameraProgramBeenOpened || cameraForm == null || cameraForm.IsDisposed)
            {
                message = "請先開啟全域設定中的線掃描攝影機程式。";
                return false;
            }

            return cameraForm.ClearMeterWheelEncoderFromHost(out message);
        }

        public bool HasCameraProgramBeenOpened
        {
            get { return hasCameraProgramBeenOpened; }
        }

        public bool InitializeCameraProgram(out string message)
        {
            if (disposing || IsDisposed)
            {
                message = "Camera control is being disposed.";
                return false;
            }

            try
            {
                EnsureCameraForm();
                hasCameraProgramBeenOpened = cameraForm != null && !cameraForm.IsDisposed;
                message = hasCameraProgramBeenOpened ? null : "Camera control is not available.";
                return hasCameraProgramBeenOpened;
            }
            catch (Exception exception)
            {
                hasCameraProgramBeenOpened = false;
                if (cameraForm != null)
                {
                    try
                    {
                        cameraForm.Dispose();
                    }
                    catch
                    {
                    }
                    cameraForm = null;
                }
                message = exception.Message;
                return false;
            }
        }

        public bool SuppressAutomaticInspectionForwarding
        {
            get { return suppressAutomaticInspectionForwarding; }
            set
            {
                if (suppressAutomaticInspectionForwarding == value)
                {
                    return;
                }
                suppressAutomaticInspectionForwarding = value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public bool OverviewCaptureForwardingEnabled
        {
            get { return overviewCaptureForwardingEnabled; }
            set
            {
                if (overviewCaptureForwardingEnabled == value)
                {
                    return;
                }
                overviewCaptureForwardingEnabled = value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public bool RemoteCaptureForwardingEnabled
        {
            get { return remoteCaptureForwardingEnabled; }
            set
            {
                if (remoteCaptureForwardingEnabled == value)
                {
                    return;
                }
                remoteCaptureForwardingEnabled = value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public event EventHandler<CameraImageAcquiredEventArgs> ImageAcquired
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                bool wasUnsubscribed = imageAcquiredHandlers == null;
                imageAcquiredHandlers += value;
                if (wasUnsubscribed)
                {
                    UpdateCameraImageAcquiredSubscription();
                }
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                imageAcquiredHandlers -= value;
                if (imageAcquiredHandlers == null)
                {
                    UpdateCameraImageAcquiredSubscription();
                }
            }
        }

        public event EventHandler<CameraImageAcquiredEventArgs> OverviewImageCaptured
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                overviewImageCapturedHandlers += value;
                UpdateCameraImageAcquiredSubscription();
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                overviewImageCapturedHandlers -= value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public event EventHandler<CameraImageAcquiredEventArgs> RemoteImageCaptured
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                remoteImageCapturedHandlers += value;
                UpdateCameraImageAcquiredSubscription();
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                remoteImageCapturedHandlers -= value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public bool ConnectCameraAndStartPreview(out string message)
        {
            if (!hasCameraProgramBeenOpened)
            {
                message = "請先開啟全域設定中的線掃描攝影機程式。";
                return false;
            }
            if (cameraForm == null || cameraForm.IsDisposed)
            {
                message = "Camera control is not available.";
                return false;
            }

            if (!cameraForm.ConnectCameraFromHost(out message))
            {
                return false;
            }
            return cameraForm.StartPreviewFromHost(out message);
        }

        public bool StartPreview(out string message)
        {
            if (!hasCameraProgramBeenOpened)
            {
                message = "請先開啟全域設定中的線掃描攝影機程式。";
                return false;
            }
            if (cameraForm == null || cameraForm.IsDisposed)
            {
                message = "Camera control is not available.";
                return false;
            }
            return cameraForm.StartPreviewFromHost(out message);
        }

        public void StopPreview()
        {
            if (cameraForm != null && !cameraForm.IsDisposed)
            {
                cameraForm.StopPreviewFromHost();
            }
        }

        public void DisconnectCamera()
        {
            if (cameraForm != null && !cameraForm.IsDisposed)
            {
                cameraForm.DisconnectCameraFromHost();
            }
        }

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !disposing)
            {
                string message;
                InitializeCameraProgram(out message);
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                this.disposing = true;
                if (cameraForm != null)
                {
                    if (cameraImageAcquiredSubscribed)
                    {
                        cameraForm.ImageAcquired -= CameraForm_ImageAcquired;
                        cameraImageAcquiredSubscribed = false;
                    }
                    cameraForm.ImageProcessingEnabledChanged -= CameraForm_ImageProcessingEnabledChanged;
                    if (!cameraForm.IsDisposed)
                    {
                        cameraForm.Close();
                    }
                    cameraForm.Dispose();
                    cameraForm = null;
                }
            }

            base.Dispose(disposing);
        }

        private void EnsureCameraForm()
        {
            if (IsDisposed || Disposing || disposing)
            {
                return;
            }
            if (cameraForm != null && !cameraForm.IsDisposed)
            {
                return;
            }
            cameraForm = null;

            cameraForm = new MainForm(new CameraService(), new SettingsService())
            {
                TopLevel = false,
                FormBorderStyle = FormBorderStyle.None,
                ShowInTaskbar = false,
                StartPosition = FormStartPosition.Manual,
                MinimumSize = Size.Empty,
                Dock = DockStyle.Fill
            };
            cameraForm.ImageProcessingEnabledChanged += CameraForm_ImageProcessingEnabledChanged;
            Controls.Add(cameraForm);
            cameraForm.Show();
            cameraForm.BringToFront();
            UpdateCameraImageAcquiredSubscription();
        }

        private void CameraForm_ImageProcessingEnabledChanged(object sender, EventArgs e)
        {
            UpdateCameraImageAcquiredSubscription();
        }

        private void UpdateCameraImageAcquiredSubscription()
        {
            if (cameraForm == null || cameraForm.IsDisposed)
            {
                return;
            }

            bool shouldSubscribe = !disposing &&
                ((overviewCaptureForwardingEnabled && overviewImageCapturedHandlers != null) ||
                    (remoteCaptureForwardingEnabled && remoteImageCapturedHandlers != null) ||
                    (imageAcquiredHandlers != null && cameraForm.IsImageProcessingEnabled &&
                        !suppressAutomaticInspectionForwarding));
            if (shouldSubscribe == cameraImageAcquiredSubscribed)
            {
                return;
            }

            if (shouldSubscribe)
            {
                cameraForm.ImageAcquired += CameraForm_ImageAcquired;
                cameraImageAcquiredSubscribed = true;
            }
            else
            {
                cameraForm.ImageAcquired -= CameraForm_ImageAcquired;
                cameraImageAcquiredSubscribed = false;
            }
        }

        private void CameraForm_ImageAcquired(object sender, CameraImageAcquiredEventArgs e)
        {
            if (disposing || IsDisposed)
            {
                return;
            }

            var inspectionHandler = imageAcquiredHandlers;
            if (inspectionHandler != null && !suppressAutomaticInspectionForwarding &&
                cameraForm.IsImageProcessingEnabled)
            {
                inspectionHandler(this, e);
            }

            var overviewHandler = overviewImageCapturedHandlers;
            if (overviewCaptureForwardingEnabled && overviewHandler != null)
            {
                overviewHandler(this, e);
            }

            var remoteHandler = remoteImageCapturedHandlers;
            if (remoteCaptureForwardingEnabled && remoteHandler != null)
            {
                remoteHandler(this, e);
            }
        }
    }
}
