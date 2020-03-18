using System;

using AVFoundation;

using CoreFoundation;

using CoreGraphics;

using Foundation;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class AVScannerController : UIViewController, IAVCaptureMetadataOutputObjectsDelegate
    {
        public AVScannerController() : base("AVScannerController", null) { }

        public event EventHandler<BarcodeDetectedEventArgs> Detected;

        private readonly AVCaptureSession session = new AVCaptureSession();

        private AVCaptureVideoPreviewLayer preview;
        private UIView codeFrame;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            var device = AVCaptureDevice.GetDefaultDevice(AVMediaTypes.Video);
            if (device is null)
            {
                this.ShowAlert("无法访问相机", null);
                return;
            }

            var input = AVCaptureDeviceInput.FromDevice(device);
            if (input is null)
            {
                this.ShowAlert("无法访问相机", null);
                return;
            }

            session.AddInput(input);
            try
            {
                var output = new AVCaptureMetadataOutput();
                output.SetDelegate(this, DispatchQueue.MainQueue);
                session.AddOutput(output);

                output.MetadataObjectTypes = AVMetadataObjectType.QRCode;
            }
            catch
            {
                return;
            }

            preview = AVCaptureVideoPreviewLayer.FromSession(session);
            if (preview is null)
            {
                this.ShowAlert("无法显示扫描预览", null);
                return;
            }
            preview.VideoGravity = AVLayerVideoGravity.Resize;
            preview.Frame = View.Layer.Bounds;
            View.Layer.AddSublayer(preview);

            session.StartRunning();

            codeFrame = new UIView();
            codeFrame.Layer.BorderColor = UIColor.Green.CGColor;
            codeFrame.Layer.BorderWidth = 2;
            View.AddSubview(codeFrame);
            View.BringSubviewToFront(codeFrame);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            session.StopRunning();
        }

        [Export("captureOutput:didOutputMetadataObjects:fromConnection:")]
        public void DidOutputMetadataObjects(AVCaptureMetadataOutput captureOutput, AVMetadataObject[] metadataObjects, AVCaptureConnection connection)
        {
            if (codeFrame is null || preview is null) return;

            if (metadataObjects.Length == 0)
            {
                codeFrame.Frame = CGRect.Empty;
                return;
            }

            var readableObject = metadataObjects[0] as AVMetadataMachineReadableCodeObject;
            if (readableObject.Type != AVMetadataObjectType.QRCode) return;

            var qrObject = preview.GetTransformedMetadataObject(readableObject);
            codeFrame.Frame = qrObject.Bounds;

            if (readableObject.StringValue is string value)
            {
                Detected?.Invoke(this, new BarcodeDetectedEventArgs(value));
            }
        }
    }
}

