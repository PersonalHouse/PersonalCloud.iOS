using System;
using System.Globalization;
using System.Threading.Tasks;

using NSPersonalCloud;

using UIKit;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;

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
            foreach (var character in VirtualFileSystem.InvalidCharacters)
            {
                if (deviceName?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrWhiteSpace(deviceName) || invalidCharHit)
            {
                this.ShowAlert(this.Localize("Settings.BadDeviceName"), this.Localize("Settings.NoSpecialCharacters"));
                return;
            }

            if (string.IsNullOrWhiteSpace(cloudName))
            {
                this.ShowAlert(this.Localize("Settings.BadCloudName"), this.Localize("Settings.CloudNameCannotBeEmpty"));
                return;
            }

            var alert = UIAlertController.Create(this.Localize("Welcome.Creating"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        await Globals.CloudManager.CreatePersonalCloud(cloudName, deviceName).ConfigureAwait(false);
                        Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert(this.Localize("Welcome.Created"), string.Format(CultureInfo.InvariantCulture, this.Localize("Welcome.CreatedCloud.Formattable"), cloudName), action => {
                                    NavigationController.DismissViewController(true, null);
                                });
                            });
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => this.ShowAlert(this.Localize("Error.CreateCloud"), null));
                        });
                    }
                });
            });
        }
    }
}
