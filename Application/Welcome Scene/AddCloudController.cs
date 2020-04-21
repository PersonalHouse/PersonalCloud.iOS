using System;
using System.Globalization;
using System.Threading.Tasks;

using NSPersonalCloud;
using NSPersonalCloud.Interfaces.Errors;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
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
            foreach (var character in VirtualFileSystem.InvalidCharacters)
            {
                if (deviceName?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrWhiteSpace(deviceName) || invalidCharHit)
            {
                this.ShowAlert(Texts.InvalidDeviceName, Texts.InvalidDeviceNameMessage);
                return;
            }

            if (inviteCode?.Length != 4)
            {
                this.ShowAlert(Texts.InvalidInvitation, Texts.InvalidInvitationMessage);
                return;
            }

            var alert = UIAlertController.Create("正在获取信息……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var result = await Globals.CloudManager.JoinPersonalCloud(int.Parse(inviteCode, CultureInfo.InvariantCulture), deviceName).ConfigureAwait(false);
                        Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert(Texts.AcceptedByCloud, string.Format(Texts.AcceptedByCloudMessage, result.DisplayName), action => {
                                    NavigationController?.DismissViewController(true, null);
                                });
                            });
                        });
                    }
                    catch (NoDeviceResponseException)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => this.ShowAlert("无法查询云信息", "未发现可以加入的个人云。"));
                        });
                    }
                    catch (InviteNotAcceptedException)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => this.ShowAlert(Texts.InvalidInvitation, Texts.InvalidInvitationMessage));
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => this.ShowAlert("无法查询云信息", "出现 App 内部错误。"));
                        });
                    }
                });
            });
        }
    }
}
