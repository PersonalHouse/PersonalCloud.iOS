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
            return (int)section switch
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
            return (int)section switch
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
            return (int)section switch
            {
                0 => null,
                1 => this.Localize("Settings.AutoLockDisabled"),
                2 => string.Format(this.Localize("Settings.WritingToPhotosRestricted.Formattable"), UIDevice.CurrentDevice.Model),
                3 => this.Localize("Settings.LeaveHint"),
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (KeyValueCell)tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.DeviceName"), Globals.CloudManager.PersonalClouds[0].NodeDisplayName);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (KeyValueCell)tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.CloudName"), cloud.Name);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 2)
            {
                var button = (AccentButtonCell)tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
                button.Update(this.Localize("Settings.SendInvitation"));
                button.Clicked += ShowInvitation;
                return button;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (SwitchCell)tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.EnableFileSharing"), shareFiles);
                cell.Clicked += ToggleFileSharing;
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (SwitchCell)tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(this.Localize("Settings.EnablePhotoSharing"), sharePhotos);
                cell.Clicked += TogglePhotoSharing;
                return cell;
            }

            if (indexPath.Section == 3 && indexPath.Row == 0)
            {
                var button = (AccentButtonCell)tableView.DequeueReusableCell(AccentButtonCell.Identifier, indexPath);
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
            var alert = UIAlertController.Create(this.Localize("Settings.SendingInvitation"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var inviteCode = await Globals.CloudManager.SharePersonalCloud(Globals.CloudManager.PersonalClouds[0]).ConfigureAwait(false);
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, null);
                            this.ShowAlert(this.Localize("Settings.InvitationGenerated"), string.Format(this.Localize("Settings.InvitationForOtherDevices.Formattable"), inviteCode), this.Localize("Settings.RevokeInvitation"), true, action =>
                                {
                                    try { _ = Globals.CloudManager.StopSharePersonalCloud(Globals.CloudManager.PersonalClouds[0]); }
                                    catch { }
                                });
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, null);
                            this.ShowAlert(this.Localize("Error.Invite"), null);
                        });
                    }
                });
            });
            return;
        }

        private void LeaveCloud(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create(this.Localize("Settings.Leave"), this.Localize("Settings.LeaveWillUnenrollDevice"), UIAlertControllerStyle.Alert);
            alert.AddAction(UIAlertAction.Create(this.Localize("Global.ConfirmAction"), UIAlertActionStyle.Destructive, action =>
            {
                Globals.CloudManager.ExitFromCloud(Globals.CloudManager.PersonalClouds[0]);
                Globals.Database.DeleteAll<CloudModel>();
                UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalNever);

                var rootController = UIApplication.SharedApplication.Windows[0].RootViewController;
                if (rootController == TabBarController)
                {
                    TabBarController.DismissViewController(true, () =>
                    {
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
                PHPhotoLibrary.RequestAuthorization(status =>
                {
                    if (status == PHAuthorizationStatus.Authorized) TurnOnPhotoSharing();
                    else
                    {
                        TurnOffPhotoSharing();
                        InvokeOnMainThread(() =>
                        {
                            TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 2) }, UITableViewRowAnimation.Fade);
                            this.ShowAlert(this.Localize("Settings.CannotReadPhotos"), this.Localize("Permission.Photos"));
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
            if (Globals.BackupWorker == null) Globals.BackupWorker = new PhotoLibraryExporter();
            else Globals.BackupWorker.Refresh();
            Globals.Database.SaveSetting(UserSettings.EnbalePhotoSharing, "1");
        }

        private void TurnOffPhotoSharing()
        {
            sharePhotos = false;
            Globals.FileSystem.ArePhotosShared = false;
            Globals.BackupWorker = null;
            Globals.Database.SaveSetting(UserSettings.EnbalePhotoSharing, "0");
        }

        private void TurnOnFileSharing()
        {
            shareFiles = true;
            Globals.Database.SaveSetting(UserSettings.EnableSharing, "1");
            Globals.FileSystem.RootPath = Paths.Documents;
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
