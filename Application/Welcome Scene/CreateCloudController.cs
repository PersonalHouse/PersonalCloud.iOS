using System;
using System.IO;
using System.Threading.Tasks;

using Foundation;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class CreateCloudController : UITableViewController
    {
        public CreateCloudController(IntPtr handle) : base(handle) { }

        private string cloudName;
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
            if (section == 0) return Texts.DeviceNameHint;
            if (section == 1) return Texts.CloudNameHint;
            return null;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
                cell.Update(Texts.DeviceName, Texts.DeviceNamePlaceholder, UpdateDeviceName, deviceName);
                return cell;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
                cell.Update(Texts.CloudName, Texts.CloudNamePlaceholder, UpdateCloudName, cloudName);
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                cell.Update(Texts.CreatePersonalCloud);
                cell.Clicked += SubmitAndCreate;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void UpdateDeviceName(UITextField textField) => deviceName = textField.Text;

        private void UpdateCloudName(UITextField textField) => cloudName = textField.Text;

        private void DiscardChanges(object sender, EventArgs e)
        {
            if (cloudName != null) this.ShowDiscardConfirmation();
            else NavigationController.DismissViewController(true, null);
        }

        private void SubmitAndCreate(object sender, EventArgs e)
        {
            var invalidCharHit = false;
            foreach (var character in Path.GetInvalidFileNameChars())
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

            // SaveData();
        }

        /*
        private void SaveData()
        {
            var result = Globals.Database.Insert(cloud);
            if (result == 1) NavigationController.DismissViewController(true, null);
            else this.ShowAlert("保存失败", "您已加入此个人云或内部数据冲突，请检查输入或向开发人员反馈。");
        }
        */
    }
}
