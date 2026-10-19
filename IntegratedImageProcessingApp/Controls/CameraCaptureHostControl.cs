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
        private bool disposing;
        private bool cameraImageAcquiredSubscribed;

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

            bool shouldSubscribe = !disposing && imageAcquiredHandlers != null &&
                cameraForm.IsImageProcessingEnabled;
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
            var handler = imageAcquiredHandlers;
            if (handler != null && !disposing && !IsDisposed)
            {
                handler(this, e);
            }
        }
    }
}
