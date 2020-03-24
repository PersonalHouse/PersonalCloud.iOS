using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

using Foundation;

using Photos;

using UIKit;

using Unishare.Apps.DarwinCore;
using Unishare.Apps.DarwinCore.Models;

namespace Unishare.Apps.DarwinMobile
{
    public partial class SelectPhotosController : UITableViewController
    {
        public SelectPhotosController(IntPtr handle) : base(handle) { }

        public List<PLAsset> SelectedPhotos { get; set; }

        private PLAsset[] photos;
        private bool[] selections;

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            PHPhotoLibrary.RequestAuthorization(status => {
                if (status != PHAuthorizationStatus.Authorized)
                {
                    InvokeOnMainThread(() => {
                        this.ShowAlert("无法访问相册", "请前往系统隐私设置授权“个人云”使用“照片”。", action => {
                            NavigationController.DismissViewController(true, null);
                        });
                    });
                    return;
                }

                RefreshPhotos();
            });
        }

        #region Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section) => photos?.Length ?? 0;

        public override string TitleForFooter(UITableView tableView, nint section) => photos == null ? "正在加载……" : null;

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
            var photo = photos[indexPath.Row];
            cell.Update(photo);
            if (selections[indexPath.Row]) cell.Accessory = UITableViewCellAccessory.Checkmark;
            else cell.Accessory = UITableViewCellAccessory.DetailButton;
            return cell;
        }

        public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
        {
            var photo = photos[indexPath.Row];
            this.ShowAlert("点击切换选中状态", null);
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);
            var photo = photos[indexPath.Row];
            if (selections[indexPath.Row]) SelectedPhotos.Remove(photo);
            else SelectedPhotos.Add(photo);

            selections[indexPath.Row] = !selections[indexPath.Row];
            tableView.ReloadRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);
        }

        #endregion

        private void RefreshPhotos()
        {
            Task.Run(() => {
                var collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
                photos = collections.OfType<PHAssetCollection>().SelectMany(x => PHAsset.FetchAssets(x, null).OfType<PHAsset>().Select(x => {
                    var asset = new PLAsset { Asset = x };
                    asset.Refresh();
                    return asset;
                })).ToArray();
                selections = new bool[photos.Length];
                for (var i = 0; i < photos.Length; i++)
                {
                    if (SelectedPhotos.Contains(photos[i])) selections[i] = true;
                }
                InvokeOnMainThread(() => TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic));
            });
        }
    }
}