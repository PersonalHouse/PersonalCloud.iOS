using System;
using System.IO;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud.Interfaces.Errors;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class AddCloudController : UITableViewController
    {
        public AddCloudController(IntPtr handle) : base(handle) { }

        private string inviteCode;
        private string deviceName;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            deviceName = UIDevice.CurrentDevice.Name;

            NavigationItem.LeftBarButtonItem.Clicked += DiscardChanges;
            TableView.SeparatorColor = TableView.BackgroundColor;
        }

        #endregion

        #region TableView DataSource

        public override nint NumberOfSections(UITableView tableView) => 3;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 1,
                1 => 1,
                2 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (section != 0) return null;
            return Texts.DeviceNameHint;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
                cell.Update(Texts.DeviceName, Texts.DeviceNamePlaceholder, UpdateName, deviceName);
                return cell;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
                cell.Update(Texts.Invitation, Texts.InvitationPlaceholder , UpdateInvite, inviteCode, UIKeyboardType.AsciiCapableNumberPad);
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                cell.Update(Texts.JoinByInvitation);
                cell.Clicked += VerifyInvite;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void UpdateName(UITextField textField) => deviceName = textField.Text;

        private void UpdateInvite(UITextField textField) => inviteCode = textField.Text;

        private void DiscardChanges(object sender, EventArgs e)
        {
            if (inviteCode != null) this.ShowDiscardConfirmation();
            else NavigationController.DismissViewController(true, null);
        }

        private void VerifyInvite(object sender, EventArgs e)
        {
            var invalidCharHit = false;
            foreach(var character in Path.GetInvalidFileNameChars())
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
                        var result = await Globals.CloudManager.JoinPersonalCloud(int.Parse(inviteCode), deviceName).ConfigureAwait(false);
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
                            DismissViewController(true, () => this.ShowAlert("无法查询云信息", "当前网络中没有已加入个人云的设备。"));
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
