using System;
using System.Globalization;
using System.Threading.Tasks;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.Interfaces.Errors;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class AddCloudController : UITableViewController
    {
        public AddCloudController(IntPtr handle) : base(handle) { }

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            DeviceNameBox.Text = UIDevice.CurrentDevice.Name;
            TableView.SeparatorColor = TableView.BackgroundColor;
            SubmitButton.Layer.CornerRadius = 10;
            SubmitButton.ClipsToBounds = true;
            SubmitButton.TouchUpInside += VerifyInvite;
            NavigationItem.LeftBarButtonItem.Clicked += DiscardChanges;
        }

        #endregion

        private void DiscardChanges(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(InviteCodeBox.Text)) NavigationController.DismissViewController(true, null);
            else this.ShowDiscardConfirmation();
        }

        private void VerifyInvite(object sender, EventArgs e)
        {
            var deviceName = DeviceNameBox.Text;
            var inviteCode = InviteCodeBox.Text;

            var invalidCharHit = false;
            foreach (var character in PathConsts.InvalidCharacters)
            {
                if (deviceName?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrWhiteSpace(deviceName) || invalidCharHit)
            {
                this.ShowWarning(this.Localize("Settings.BadDeviceName"), this.Localize("Settings.NoSpecialCharacters"));
                return;
            }

            if (inviteCode?.Length != 4)
            {
                this.ShowError(this.Localize("Settings.BadInvitation"), this.Localize("Settings.EnterValidInvitation"));
                return;
            }

            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Welcome.Verifying");
            Task.Run(async () => {
                try
                {
                    var result = await Globals.CloudManager.JoinPersonalCloud(int.Parse(inviteCode, CultureInfo.InvariantCulture), deviceName).ConfigureAwait(false);
                    Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowConfirmation(this.Localize("Welcome.Accepted"),
                            string.Format(CultureInfo.InvariantCulture, this.Localize("Welcome.AcceptedByCloud.Formattable"), result.DisplayName), () => {
                                NavigationController.DismissViewController(true, null);
                            });
                    });
                }
                catch (NoDeviceResponseException)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.EnrollDevice"), this.Localize("Error.NoCloudInNetwork"));
                    });
                }
                catch (InviteNotAcceptedException)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.EnrollDevice"), this.Localize("Settings.EnterValidInvitation"));
                    });
                }
                catch
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.EnrollDevice"));
                    });
                }
            });
        }
    }
}
