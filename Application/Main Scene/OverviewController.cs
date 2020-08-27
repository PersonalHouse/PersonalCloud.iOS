using System;
using System.Globalization;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud.Common;
using NSPersonalCloud.Common.Models;
using NSPersonalCloud.DarwinCore;

using Photos;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class OverviewController : UITableViewController
    {
        public OverviewController(IntPtr handle) : base(handle) { }

        private const string RenameSegue = "Rename";

        private bool sharePhotos;
        private bool shareFiles;

        private CloudModel cloud;

        private bool refreshNames;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            sharePhotos = PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                Globals.Database.CheckSetting(UserSettings.EnbalePhotoSharing, "1");
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

        #region TableView Data Source

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
                0 => this.Localize("Global.Cloud"),
                1 => this.Localize("Settings.FileSharing"),
                2 => this.Localize("Settings.PhotoSharing"),
                3 => null,
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => null,
                1 => this.Localize("Settings.AutoLockDisabled"),
                2 => string.Format(CultureInfo.InvariantCulture, this.Localize("Settings.WritingToPhotosRestricted.Formattable"), UIDevice.CurrentDevice.Model),
                3 => this.Localize("Settings.LeaveHint"),
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.DeviceName"), Globals.CloudManager.PersonalClouds[0].NodeDisplayName);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.CloudName"), cloud.Name);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 2)
            {
                var button = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                button.Update(this.Localize("Settings.SendInvitation"));
                button.Clicked += ShowInvitation;
                return button;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (SwitchCell) tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.EnableFileSharing"), shareFiles);
                cell.Clicked += ToggleFileSharing;
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (SwitchCell) tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.EnablePhotoSharing"), sharePhotos);
                cell.Clicked += TogglePhotoSharing;
                return cell;
            }

            if (indexPath.Section == 3 && indexPath.Row == 0)
            {
                var button = (AccentButtonCell) tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                button.Update(this.Localize("Settings.SwitchPersonalCloud"), Colors.DangerousRed);
                button.Clicked += LeaveCloud;
                return button;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        #region TableView Delegate

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                PerformSegue(RenameSegue, this);
                refreshNames = true;
                return;
            }
        }

        #endregion

        private void ShowInvitation(object sender, EventArgs e)
        {
            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Settings.SendingInvitation");
            Task.Run(async () => {
                try
                {
                    var inviteCode = await Globals.CloudManager.SharePersonalCloud(Globals.CloudManager.PersonalClouds[0]).ConfigureAwait(false);
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowAlert(this.Localize("Settings.InvitationGenerated"),
                            string.Format(CultureInfo.InvariantCulture, this.Localize("Settings.InvitationForOtherDevices.Formattable"), inviteCode),
                            this.Localize("Settings.RevokeInvitation"), true, action => {
                                try { _ = Globals.CloudManager.StopSharePersonalCloud(Globals.CloudManager.PersonalClouds[0]); }
                                catch { }
                            });
                    });
                }
                catch
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.Invite"));
                    });
                }
            });
            return;
        }

        private void LeaveCloud(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create(this.Localize("Settings.Leave"), this.Localize("Settings.LeaveWillUnenrollDevice"), UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create(this.Localize("Global.ConfirmAction"), UIAlertActionStyle.Destructive, action => {
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
            var ok = UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Default, null);
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
                            this.ShowError(this.Localize("Settings.CannotReadPhotos"), this.Localize("Permission.Photos"));
                        });
                    }
                });
            }
            else TurnOffPhotoSharing();
        }

        private void TurnOnPhotoSharing()
        {
            sharePhotos = true;

            if (Globals.BackupWorker == null)
            {
                Globals.BackupWorker = new PhotoLibraryExporter();
            }
            _ = Globals.BackupWorker.Init();
            Globals.Database.SaveSetting(UserSettings.EnbalePhotoSharing, "1");

            AppDelegate.SetupFS(Globals.Database.CheckSetting(UserSettings.EnableSharing, "1"));
            Globals.CloudManager.FileSystem = Globals.FileSystem;
            try
            {
                Globals.CloudManager.StopNetwork();
                Globals.CloudManager.StartNetwork(true);
            }
            catch
            {
                // Ignored.
            }
        }

        private void TurnOffPhotoSharing()
        {
            sharePhotos = false;
            Globals.BackupWorker = null;
            Globals.Database.SaveSetting(UserSettings.EnbalePhotoSharing, "0");

            AppDelegate.SetupFS(Globals.Database.CheckSetting(UserSettings.EnableSharing, "1"));
            Globals.CloudManager.FileSystem = Globals.FileSystem;
            try
            {
                Globals.CloudManager.StopNetwork();
                Globals.CloudManager.StartNetwork(true);
            }
            catch
            {
                // Ignored.
            }
        }

        private void TurnOnFileSharing()
        {
            shareFiles = true;
            Globals.Database.SaveSetting(UserSettings.EnableSharing, "1");
            AppDelegate.SetupFS(true);
            Globals.CloudManager.FileSystem = Globals.FileSystem;
            try
            {
                Globals.CloudManager.StopNetwork();
                Globals.CloudManager.StartNetwork(true);
            }
            catch
            {
                // Ignored.
            }
            UIApplication.SharedApplication.IdleTimerDisabled = true;
        }

        private void TurnOffFileSharing()
        {
            shareFiles = false;
            Globals.Database.SaveSetting(UserSettings.EnableSharing, "0");
            AppDelegate.SetupFS(false);
            Globals.CloudManager.FileSystem = Globals.FileSystem;
            try
            {
                Globals.CloudManager.StopNetwork();
                Globals.CloudManager.StartNetwork(true);
            }
            catch
            {
                // Ignored.
            }
            UIApplication.SharedApplication.IdleTimerDisabled = false;
        }
    }
}
