using System;
using System.Threading.Tasks;

using Photos;

using UIKit;

using Xamarin.Essentials;
using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using Foundation;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class HomeController : UITabBarController
    {
        public HomeController(IntPtr handle) : base(handle) { }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SelectedIndex = 0;

            Task.Run(async () => {

                var status = Reachability.RemoteHostStatus();
                if (status == NetworkStatus.NotReachable)
                {
                    await MainThread.InvokeOnMainThreadAsync(() => {
                        this.ShowAlert(null, this.Localize("Error.NoNetwork"), this.Localize("Global.OpenIosSetting"), false,
                            (x) => { UIApplication.SharedApplication.OpenUrl(new NSUrl(UIApplication.OpenSettingsUrlString)); }, true);////"app-settings:"

                    });
                }
            });
        }
        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            Task.Run(async () => {

                if (Globals.Database.CheckSetting(Common.UserSettings.AutoBackupPhotos, "1"))
                {

                    await MainThread.InvokeOnMainThreadAsync(async () => {
                        var status = await Permissions.RequestAsync<Permissions.Photos>();
                        if (status == PermissionStatus.Denied)
                        {
                            await MainThread.InvokeOnMainThreadAsync(() => {
                                this.ShowAlert(null, this.Localize("Permission.Photos"), this.Localize("Global.OpenIosSetting"), false,
                                    (x) => { UIApplication.SharedApplication.OpenUrl(new NSUrl(UIApplication.OpenSettingsUrlString)); }, true);////"app-settings:"

                            });
                        }
                    });
                }
            });
        }
    }
}
