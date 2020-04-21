using System;
using System.Threading.Tasks;

using NSPersonalCloud;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
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
                this.ShowAlert(Texts.InvalidDeviceName, Texts.InvalidDeviceNameMessage);
                return;
            }

            if (string.IsNullOrWhiteSpace(cloudName))
            {
                this.ShowAlert("云名称无效", "个人云名称不能为空。");
                return;
            }

            var alert = UIAlertController.Create("正在创建个人云……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        await Globals.CloudManager.CreatePersonalCloud(cloudName, deviceName).ConfigureAwait(false);
                        Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("已创建", $"您已创建并加入个人云“{cloudName}”。", action => {
                                    NavigationController.DismissViewController(true, null);
                                });
                            });
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => this.ShowAlert("无法创建个人云", "出现 App 内部错误。"));
                        });
                    }
                });
            });
        }
    }
}
