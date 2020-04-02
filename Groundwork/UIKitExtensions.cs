using System;

using Foundation;

using UIKit;

namespace Unishare.Apps.DarwinCore
{
    public static partial class UIKitExtensions
    {
        #region Backward Compatibility

        public static void SetPreferredAction(this UIAlertController alert, UIAlertAction action)
        {
            if (!UIDevice.CurrentDevice.CheckSystemVersion(9, 0)) return;
            alert.PreferredAction = action;
        }

        public static void SetAllowsMultipleSelection(this UIDocumentPickerViewController picker, bool isAllowed)
        {
            if (!UIDevice.CurrentDevice.CheckSystemVersion(11, 0)) return;
            picker.AllowsMultipleSelection = isAllowed;
        }

        public static void SetShowFileExtensions(this UIDocumentPickerViewController picker)
        {
            if (!UIDevice.CurrentDevice.CheckSystemVersion(11, 0)) return;
            picker.ShouldShowFileExtensions = true;
        }

        #endregion

        public static string GetBundleVersion(this UIApplication application)
        {
            var version = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleVersion")?.ToString();
            var shortVersion = NSBundle.MainBundle.ObjectForInfoDictionary("CFBundleShortVersionString")?.ToString();

            if (version != null && shortVersion != null) return $"{shortVersion} ({version})";
            return shortVersion ?? version ?? "?";
        }

        public static void ShowAlert(this UIViewController controller, string title, string message)
        {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create("好", UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void ShowAlert(this UIViewController controller, string title, string message, Action<UIAlertAction> onDismiss)
        {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create("好", UIAlertActionStyle.Default, onDismiss);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void ShowAlert(this UIViewController controller, string title, string message,
                                     string dismissAction, bool actionIsDangerous = false,
                                     Action<UIAlertAction> onDismiss = null)
        {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create(dismissAction, actionIsDangerous ? UIAlertActionStyle.Destructive : UIAlertActionStyle.Default, onDismiss);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void ShowDiscardConfirmation(this UIViewController controller, UIViewController parent = null)
        {
            var alert = UIAlertController.Create("放弃所做更改？", "您有尚未保存的更改，要保存这些更改后再返回，请勿直接使用“返回”或“取消”按钮。", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("立即放弃", UIAlertActionStyle.Destructive, action => {
                (parent ?? controller.NavigationController)?.DismissViewController(true, null);
            }));
            var ok = UIAlertAction.Create("查看更改", UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void PreviewFile(this UIViewController controller, NSUrl url)
        {
            if (!(controller is IUIDocumentInteractionControllerDelegate @delegate)) throw new InvalidOperationException($"{controller.GetType().Name} must implement 'UIDocumentInteractionControllerDelegate' protocol.");

            var preview = UIDocumentInteractionController.FromUrl(url);
            preview.Delegate = @delegate;
            if (preview.PresentPreview(true)) return;

            var alert = UIAlertController.Create("无法预览此类文件", "已安装的 App 均不提供此类文件的预览，即时预览已取消。" +
                Environment.NewLine + Environment.NewLine + "您仍然可以将文件分享给他人或在其它 App 中打开。如果您前往其它 App，文件和相簿共享可能中断。", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("返回", UIAlertActionStyle.Cancel, null));
            var ok = UIAlertAction.Create("分享到…", UIAlertActionStyle.Default, action => {
                var share = new UIActivityViewController(new[] { url }, null);
                controller.PresentViewController(share, true, null);
            });
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void PresentActionSheet(this UIViewController controller, UIAlertController alert, UIView anchor = null)
        {
            var popover = alert.PopoverPresentationController;
            if (popover != null)
            {
                popover.SourceView = anchor ?? controller.View;
                popover.SourceRect = anchor?.Bounds ?? controller.View.Bounds;
            }
            controller.PresentViewController(alert, true, null);
        }
    }
}
