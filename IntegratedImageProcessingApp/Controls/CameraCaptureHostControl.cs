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
        private EventHandler<CameraImageAcquiredEventArgs> imageCapturedHandlers;
        private bool disposing;
        private bool cameraImageAcquiredSubscribed;
        private volatile bool suppressAutomaticInspectionForwarding;
        private volatile bool captureForwardingEnabled;

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

        public bool CaptureForwardingEnabled
        {
            get { return captureForwardingEnabled; }
            set
            {
                if (captureForwardingEnabled == value)
                {
                    return;
                }
                captureForwardingEnabled = value;
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

        public event EventHandler<CameraImageAcquiredEventArgs> ImageCaptured
        {
            add
            {
                if (value == null)
                {
                    return;
                }

                imageCapturedHandlers += value;
                UpdateCameraImageAcquiredSubscription();
            }
            remove
            {
                if (value == null)
                {
                    return;
                }

                imageCapturedHandlers -= value;
                UpdateCameraImageAcquiredSubscription();
            }
        }

        public bool ConnectCameraAndStartPreview(out string message)
        {
            EnsureCameraForm();
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
            EnsureCameraForm();
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

        protected override void OnVisibleChanged(EventArgs e)
        {
            base.OnVisibleChanged(e);
            if (Visible && !disposing)
            {
                EnsureCameraForm();
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
            if (cameraForm != null || IsDisposed || Disposing)
            {
                return;
            }

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
                ((captureForwardingEnabled && imageCapturedHandlers != null) ||
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

            var capturedHandler = imageCapturedHandlers;
            if (capturedHandler != null)
            {
                capturedHandler(this, e);
            }

            var inspectionHandler = imageAcquiredHandlers;
            if (inspectionHandler != null && !suppressAutomaticInspectionForwarding &&
                cameraForm.IsImageProcessingEnabled)
            {
                inspectionHandler(this, e);
            }
        }
    }
}
