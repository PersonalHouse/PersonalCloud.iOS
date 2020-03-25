using System;
using System.Threading.Tasks;

using Foundation;

using Photos;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.Common.Models;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class OverviewController : UITableViewController
    {
        public OverviewController(IntPtr handle) : base(handle) { }

        private const string RenameSegue = "Rename";

        private const string HeaderSharing = "文件共享";
        private const string FooterSharing = "为保证峰值设备性能，“文件共享”打开时自动锁定将暂时禁用，且屏幕不会关闭；这将影响电池续航。手动锁定屏幕或离开当前网络可能中断共享。请在不用时退出 App 以恢复自动锁定。";
        private const string HeaderPhotoSync = "相簿共享";
        private const string FooterPhotoSync = "由于系统限制，仅向其它设备提供 {0} 相簿只读权限。您无法通过向“相簿”共享文件夹中添加文件来将照片导入 {0} 相簿。";
        private const string HeaderCloud = "个人云";
        private const string FooterCloudNone = "您的设备尚未加入任何个人云。点击右上角“+”按钮即可新建或加入。";
        private const string HeaderDevice = "即传";
        private const string FooterDeviceNone = "您尚未配置任何即传设备。点击右上角“+”按钮即可添加。";

        private const string ActionAddCloud = "新建或加入个人云";
        private const string ActionAddDevice = "连接到即传设备";

        private const string AlertCloudLimitTitle = "您已加入个人云";
        private const string AlertCloudLimitMessage = "当前版本每台设备仅支持加入单个个人云。如果您需要访问多个个人云，请等待版本更新。";

        private const string EnableFileSharing = "允许此设备分享文件";
        private const string ManageSharedFiles = "管理共享文件夹";
        private const string EnablePhotoSharing = "允许此设备分享相簿";
        private const string AutoBackupPhotos = "加入云自动备份";
        private const string CloudActive = "已加入";

        private const string AlertPhotosAccessTitle = "无法访问相簿";
        private const string AlertPhotosAccessMessage = "您已拒绝个人云使用“照片”。请前往系统设置 App 更改隐私授权。";

        private bool sharePhotos;
        private bool shareFiles;

        private CloudModel cloud;

        private bool refreshNames;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            sharePhotos = PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                Globals.Database.CheckSetting(UserSettings.EnablePhotoSync, "1");
            shareFiles = Globals.Database.CheckSetting(UserSettings.EnableSharing, "1");

            cloud = Globals.Database.Table<CloudModel>().First();

            TableView.SeparatorColor = TableView.BackgroundColor;
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            if (refreshNames) TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 0), NSIndexPath.FromRowSection(1, 0) }, UITableViewRowAnimation.Automatic);
            refreshNames = false;
        }

        #endregion

        #region TableView

        public override nint NumberOfSections(UITableView tableView) => 4;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 3,
                1 => 1,
                2 => 1,
                3 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override nfloat GetHeightForRow(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 2) return 65;
            return UITableView.AutomaticDimension;
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => HeaderCloud,
                1 => HeaderSharing,
                2 => HeaderPhotoSync,
                3 => null,
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => null,
                1 => string.Format(FooterSharing, UIDevice.CurrentDevice.Model, Environment.NewLine + Environment.NewLine),
                2 => string.Format(FooterPhotoSync, UIDevice.CurrentDevice.Model),
                3 => "如果此个人云内仍有其它设备，您可以通过其它设备的邀请再次加入。当最后一台设备离开时，此个人云将消失。",
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(Texts.DeviceName, Globals.CloudManager.PersonalClouds[0].NodeDisplayName);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(Texts.CloudName, cloud.Name);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 2)
            {
                var button = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                button.Update("邀请新设备加入");
                button.Clicked += ShowInvitation;
                return button;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (SwitchCell) tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(EnableFileSharing, shareFiles);
                cell.Clicked += ToggleFileSharing;
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (SwitchCell) tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(EnablePhotoSharing, sharePhotos);
                cell.Clicked += TogglePhotoSharing;
                return cell;
            }

            if (indexPath.Section == 3 && indexPath.Row == 0)
            {
                var button = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                button.Update("切换或离开个人云", Colors.DangerousRed);
                button.Clicked += LeaveCloud;
                return button;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                PerformSegue(RenameSegue, this);
                refreshNames = true;
                return;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                this.ShowAlert("暂时不支持修改个人云名称", null);
                return;
            }
        }

        #endregion

        private void ShowInvitation(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create("正在生成……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var inviteCode = await Globals.CloudManager.SharePersonalCloud(Globals.CloudManager.PersonalClouds[0]).ConfigureAwait(false);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, null);
                            this.ShowAlert("已生成邀请码", "请在其它设备输入邀请码：" + Environment.NewLine + Environment.NewLine
                                + inviteCode + Environment.NewLine + Environment.NewLine
                                + "离开此界面邀请码将失效。", "停止邀请", true, action => {
                                    try { _ = Globals.CloudManager.StopSharePersonalCloud(Globals.CloudManager.PersonalClouds[0]); }
                                    catch { }
                                });
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, null);
                            this.ShowAlert("无法生成邀请码", null);
                        });
                    }
                });
            });
            return;
        }

        private void LeaveCloud(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create("从个人云中移除此设备？", "当前设备将离开个人云，本地保存的相关信息也将删除。", UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create("确认", UIAlertActionStyle.Destructive, action => {
                Globals.CloudManager.ExitFromCloud(Globals.CloudManager.PersonalClouds[0]);
                Globals.Database.DeleteAll<CloudModel>();
                UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalNever);

                var rootController = UIApplication.SharedApplication.Windows[0].RootViewController;
                if (rootController == TabBarController)
                {
                    TabBarController.DismissViewController(true, () => {
                        var controller = Storyboard.InstantiateViewController("WelcomeScreen");
                        controller.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                        PresentViewController(controller, true, () => { });
                    });
                }
                else rootController.DismissViewController(true, null);
            }));
            var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.PreferredAction = ok;
            PresentViewController(alert, true, null);

            return;
        }

        private void ToggleFileSharing(object sender, ToggledEventArgs e)
        {
            if (e.On)
            {
                TurnOnFileSharing();
            }
            else
            {
                TurnOffFileSharing();
            }
        }

        private void TogglePhotoSharing(object sender, ToggledEventArgs e)
        {
            if (e.On)
            {
                PHPhotoLibrary.RequestAuthorization(status => {
                    if (status == PHAuthorizationStatus.Authorized) TurnOnPhotoSharing();
                    else
                    {
                        TurnOffPhotoSharing();
                        InvokeOnMainThread(() => {
                            TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 2) }, UITableViewRowAnimation.Fade);
                            this.ShowAlert(AlertPhotosAccessTitle, AlertPhotosAccessMessage);
                        });
                    }
                });
            }
            else TurnOffPhotoSharing();
        }

        private void TurnOnPhotoSharing()
        {
            sharePhotos = true;
            Globals.FileSystem.ArePhotosShared = true;
            Globals.Database.SaveSetting(UserSettings.EnablePhotoSync, "1");
        }

        private void TurnOffPhotoSharing()
        {
            sharePhotos = false;
            Globals.FileSystem.ArePhotosShared = false;
            Globals.Database.SaveSetting(UserSettings.EnablePhotoSync, "0");
        }

        private void TurnOnFileSharing()
        {
            shareFiles = true;
            Globals.Database.SaveSetting(UserSettings.EnableSharing, "1");
            Globals.FileSystem.RootPath = PathHelpers.Documents;
            UIApplication.SharedApplication.IdleTimerDisabled = true;
        }

        private void TurnOffFileSharing()
        {
            shareFiles = false;
            Globals.Database.SaveSetting(UserSettings.EnableSharing, "0");
            Globals.FileSystem.RootPath = null;
            UIApplication.SharedApplication.IdleTimerDisabled = false;
        }
    }
}
