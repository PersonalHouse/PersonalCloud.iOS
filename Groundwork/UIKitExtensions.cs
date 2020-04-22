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
            var ok = UIAlertAction.Create(Localize("Global.OKAction"), UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            controller.PresentViewController(alert, true, null);
        }

        public static void ShowAlert(this UIViewController controller, string title, string message, Action<UIAlertAction> onDismiss)
        {
            var alert = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create(Localize("Global.OKAction"), UIAlertActionStyle.Default, onDismiss);
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
            var alert = UIAlertController.Create(Localize("Global.DiscardChanges.Short"), Localize("Global.DiscardChanges.Long"), UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create(Localize("Global.DiscardChanges.Confirm"), UIAlertActionStyle.Destructive, action => {
                (parent ?? controller.NavigationController)?.DismissViewController(true, null);
            }));
            var ok = UIAlertAction.Create(Localize("Global.DiscardChanges.Back"), UIAlertActionStyle.Default, null);
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

            var alert = UIAlertController.Create(Localize("Global.NoPreview.Short"), Localize("Global.NoPreview.Long"), UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create(Localize("Global.BackAction"), UIAlertActionStyle.Cancel, null));
            var ok = UIAlertAction.Create(Localize("Global.NoPreview.OpenIn"), UIAlertActionStyle.Default, action => {
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

        private static string Localize(string key, string defaultValue = null)
        {
            return NSBundle.MainBundle.GetLocalizedString(key, defaultValue ?? "LOCALIZABLE_TEXT");
        }

        public static string Localize(this UIViewController controller, string key, string defaultValue = null)
        {
            return Localize(key, defaultValue);
        }
    }
}
