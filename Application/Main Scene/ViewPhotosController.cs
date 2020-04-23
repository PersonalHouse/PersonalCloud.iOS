using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Foundation;

using UIKit;
using Unishare.Apps.DarwinCore;
using Unishare.Apps.DarwinCore.Models;

namespace Unishare.Apps.DarwinMobile
{
    public partial class ViewPhotosController : UITableViewController
    {
        public ViewPhotosController(IntPtr handle) : base(handle) { }

        private IReadOnlyList<PLAsset> photos;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            photos = Globals.BackupWorker?.Photos;
            if (photos == null)
            {
                Task.Run(() => {
                    Globals.BackupWorker = new PhotoLibraryExporter();
                    photos = Globals.BackupWorker.Photos;
                    InvokeOnMainThread(() => TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic));
                });
            }
        }

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section) => photos?.Count ?? 0;

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (photos == null) return this.Localize("Global.LoadingStatus");
            if (photos.Count == 0) return this.Localize("Backup.NoNewItems");
            return null;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
            var photo = photos[indexPath.Row];
            cell.Update(photo);
            return cell;
        }

        #endregion

        #region TableView Delegate

        public override bool CanFocusRow(UITableView tableView, NSIndexPath indexPath) => false;

        #endregion
    }
}