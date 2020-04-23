using System;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud.RootFS;

using Photos;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class PhotosBackupController : UITableViewController
    {
        public PhotosBackupController(IntPtr handle) : base(handle) { }

        private const string ChooseDeviceSegue = "ChooseBackupPath";
        private const string ViewPhotosSegue = "ViewPhotos";

        private bool autoBackup;
        private string backupPath;
        private int backupIntervalHours;
        private RootFileSystem fileSystem;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            fileSystem = Globals.CloudManager.PersonalClouds[0].RootFS;
            NavigationItem.LeftBarButtonItem.Clicked += ShowHelp;
        }

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            autoBackup = PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized && Globals.Database.CheckSetting(UserSettings.AutoBackupPhotos, "1");
            backupPath = Globals.Database.LoadSetting(UserSettings.PhotoBackupPrefix);
            if (!int.TryParse(Globals.Database.LoadSetting(UserSettings.PhotoBackupInterval) ?? "-1", out backupIntervalHours)) backupIntervalHours = 0;
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 0), NSIndexPath.FromRowSection(1, 0), NSIndexPath.FromRowSection(2, 0) }, UITableViewRowAnimation.Automatic);
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            base.PrepareForSegue(segue, sender);
            if (segue.Identifier == ChooseDeviceSegue)
            {
                var navigation = (UINavigationController) segue.DestinationViewController;
                var chooser = (ChooseDeviceController) navigation.TopViewController;
                chooser.FileSystem = fileSystem;
                chooser.NavigationTitle = this.Localize("Backup.ChooseBackupLocation");
                chooser.PathSelected += (o, e) => {
                    Globals.Database.SaveSetting(UserSettings.PhotoBackupPrefix, e.Path);
                    InvokeOnMainThread(() => {
                        TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 0), NSIndexPath.FromRowSection(1, 0), NSIndexPath.FromRowSection(2, 0) }, UITableViewRowAnimation.Automatic);
                    });
                };
                return;
            }
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 5,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => this.Localize("Backup.AutoBackup"),
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (SwitchCell) tableView.DequeueReusableCell(SwitchCell.Identifier, indexPath);
                cell.Update(this.Localize("Backup.EnableAutoBackup"), autoBackup);
                cell.Accessory = UITableViewCellAccessory.None;
                cell.Clicked += ToggleAutoBackup;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Backup.ChooseLocation"), string.IsNullOrEmpty(backupPath) ? null : this.Localize("Backup.SetUpDone"), true);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 2)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update(this.Localize("Backup.Interval"), backupIntervalHours < 1 ? null : string.Format(this.Localize("Backup.Interval.Formattable"), 1), true);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 3)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update(this.Localize("Backup.ViewItems"), true);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 4)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update(this.Localize("Backup.BackupNow"), Colors.BlueButton, true);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        #region TableView Delegate

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0) return;

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                PerformSegue(ChooseDeviceSegue, this);
                return;
            }

            if (indexPath.Section == 0 && indexPath.Row == 2)
            {
                return;
            }

            if (indexPath.Section == 0 && indexPath.Row == 3)
            {
                if (autoBackup) PerformSegue(ViewPhotosSegue, this);
                else this.ShowAlert(this.Localize("Backup.NotSetUp"), this.Localize("Backup.SetUpBeforeViewingItems"));
                return;
            }

            if (indexPath.Section == 0 && indexPath.Row == 4)
            {
                if ((Globals.BackupWorker?.BackupTask?.IsCompleted ?? true) != true)
                {
                    this.ShowAlert(this.Localize("Backup.CannotExecute"), this.Localize("Backup.AlreadyRunning"));
                    return;
                }

                if (!autoBackup)
                {
                    this.ShowAlert(this.Localize("Backup.NotSetUp"), this.Localize("Backup.SetUpBeforeExecuting"));
                    return;
                }

                Task.Run(() => {
                    if (Globals.BackupWorker == null) Globals.BackupWorker = new PhotoLibraryExporter();
                    Globals.BackupWorker.StartBackup(fileSystem, backupPath);
                });
                this.ShowAlert(this.Localize("Backup.Executed"), this.Localize("Backup.NewBackupInProgress"));
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void ShowHelp(object sender, EventArgs e)
        {
            this.ShowAlert(this.Localize("Help.Backup"), this.Localize("Help.BackupPhotos"));
        }

        private void ToggleAutoBackup(object sender, ToggledEventArgs e)
        {
            if (e.On)
            {
                PHPhotoLibrary.RequestAuthorization(status => {
                    if (status == PHAuthorizationStatus.Authorized) InvokeOnMainThread(() => TurnOnAutoBackup(sender));
                    else InvokeOnMainThread(() => {
                        TurnOffAutoBackup(sender);
                        this.ShowAlert(this.Localize("Backup.CannotSetUp"), this.Localize("Permission.Photos"));
                    });
                });
            }
            else TurnOffAutoBackup(sender);
        }

        private void TurnOnAutoBackup(object obj)
        {
            if (string.IsNullOrEmpty(backupPath))
            {
                TurnOffAutoBackup(obj);
                this.ShowAlert(this.Localize("Backup.CannotSetUp"), this.Localize("Backup.NoBackupLocation"));
                return;
            }

            if (backupIntervalHours < 1)
            {
                TurnOffAutoBackup(obj);
                this.ShowAlert(this.Localize("Backup.CannotSetUp"), this.Localize("Backup.NoInterval"));
                return;
            }

            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(backupIntervalHours * 3600);
            if (UIApplication.SharedApplication.BackgroundRefreshStatus == UIBackgroundRefreshStatus.Available)
            {
                Globals.Database.SaveSetting(UserSettings.AutoBackupPhotos, "1");
                autoBackup = true;
            }
            else
            {
                this.ShowAlert(this.Localize("Backup.BackgroundRefreshDisabled"), this.Localize("Permission.BackgroundRefresh"));
                TurnOffAutoBackup(obj);
            }
        }

        private void TurnOffAutoBackup(object obj)
        {
            if (obj is UISwitch button && button.On) button.On = false;
            UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalNever);
            Globals.Database.SaveSetting(UserSettings.AutoBackupPhotos, "0");
            autoBackup = false;
        }
    }
}