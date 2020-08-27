using System;
using System.Globalization;
using System.Threading.Tasks;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class CreateCloudController : UITableViewController
    {
        public CreateCloudController(IntPtr handle) : base(handle) { }

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            DeviceNameBox.Text = UIDevice.CurrentDevice.Name;
            TableView.SeparatorColor = TableView.BackgroundColor;
            SubmitButton.Layer.CornerRadius = 10;
            SubmitButton.ClipsToBounds = true;
            SubmitButton.TouchUpInside += SubmitAndCreate;
            NavigationItem.LeftBarButtonItem.Clicked += DiscardChanges;
        }

        #endregion

        private void DiscardChanges(object sender, EventArgs e)
        {
            if (string.IsNullOrEmpty(CloudNameBox.Text)) NavigationController.DismissViewController(true, null);
            else this.ShowDiscardConfirmation();
        }

        private void SubmitAndCreate(object sender, EventArgs e)
        {
            var cloudName = CloudNameBox.Text;
            var deviceName = DeviceNameBox.Text;

            var invalidCharHit = false;
            foreach (var character in Consts.InvalidCharacters)
            {
                if (deviceName?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrWhiteSpace(deviceName) || invalidCharHit)
            {
                this.ShowWarning(this.Localize("Settings.BadDeviceName"), this.Localize("Settings.NoSpecialCharacters"));
                return;
            }

            if (string.IsNullOrWhiteSpace(cloudName))
            {
                this.ShowError(this.Localize("Settings.BadCloudName"), this.Localize("Settings.CloudNameCannotBeEmpty"));
                return;
            }

            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Welcome.Creating");
            Task.Run(async () => {
                try
                {
                    await Globals.CloudManager.CreatePersonalCloud(cloudName, deviceName).ConfigureAwait(false);
                    Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowConfirmation(this.Localize("Welcome.Created"),
                            string.Format(CultureInfo.InvariantCulture, this.Localize("Welcome.CreatedCloud.Formattable"), cloudName), () => {
                                NavigationController.DismissViewController(true, null);
                            });
                    });
                }
                catch
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.CreateCloud"));
                    });
                }
            });
        }
    }
}
