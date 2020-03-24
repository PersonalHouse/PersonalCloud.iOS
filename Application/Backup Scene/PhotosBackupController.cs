using System;
using System.Collections.Generic;
using System.Globalization;

using Foundation;

using NSPersonalCloud.RootFS;

using Photos;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class PhotosBackupController : UITableViewController
    {
        public PhotosBackupController(IntPtr handle) : base(handle) { }

        private const string ChooseDeviceSegue = "ChooseBackupPath";
        private const string SelectPhotosSegue = "SelectPhotos";

        private readonly List<PHAsset> photos = new List<PHAsset>();
        private string workingPath;

        private bool isBackupInProgress;
        private RootFileSystem fileSystem;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            fileSystem = Globals.CloudManager.PersonalClouds[0].RootFS;
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            var sections = new NSMutableIndexSet();
            sections.Add(0);
            sections.Add(2);
            TableView.ReloadSections(sections, UITableViewRowAnimation.Automatic);
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            base.PrepareForSegue(segue, sender);
            switch (segue.Identifier)
            {
                case ChooseDeviceSegue:
                {
                    var controller = (ChooseDeviceController) segue.DestinationViewController;
                    controller.SelectedDevice = workingPath;
                    controller.SelectedDeviceChanged += (o, e) => {
                        var sender = (ChooseDeviceController) o;
                        workingPath = sender.SelectedDevice;
                    };
                    return;
                }

                case SelectPhotosSegue:
                {
                    var controller = (SelectPhotosController) segue.DestinationViewController;
                    controller.SelectedPhotos = photos;
                    return;
                }
            }
        }

        #endregion

        #region Data Source

        public override nint NumberOfSections(UITableView tableView) => 3;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 2,
                1 => 1,
                2 => photos.Count,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForHeader(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => null,
                1 => null,
                2 => isBackupInProgress ? "正在备份……" : null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update("选择存储设备", !string.IsNullOrEmpty(workingPath) ? "已选择" : null, true);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update("选择照片或视频", photos.Count > 0 ? photos.Count.ToString(CultureInfo.InvariantCulture) : "无", true);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                if (isBackupInProgress) cell.Update("停止备份", Colors.DangerousRed, true);
                else cell.Update("立即备份", Colors.BlueButton, true);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            if (indexPath.Section == 2)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var photo = photos[indexPath.Row];
                cell.Update(photo);
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                if (isBackupInProgress)
                {
                    this.ShowAlert("不能切换存储设备", "备份已在进行，在备份完成或中断前无法更改备份参数。");
                    return;
                }
                PerformSegue(ChooseDeviceSegue, this);
                return;
            }
            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                if (isBackupInProgress)
                {
                    this.ShowAlert("不能重选待备份文件", "备份已在进行，在备份完成或中断前无法更改备份参数。");
                    return;
                }
                PerformSegue(SelectPhotosSegue, this);
                return;
            }

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                if (isBackupInProgress)
                {
                    isBackupInProgress = false;
                    RefreshStatus();
                    return;
                }

                if (string.IsNullOrEmpty(workingPath) || !(photos?.Count > 0))
                {
                    this.ShowAlert("备份所需信息不完整", "请核对备份存储设备和待备份文件。");
                    return;
                }

                isBackupInProgress = true;

                // Todo: Start backup.

                RefreshStatus();
                return;
            }

            /*
            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                List<FileInfo> items = null;
                var types = new PHAssetResourceType[items.Count];
                for (var i = 0; i < items.Count; i++)
                {
                    var fileName = items[i].Name;
                    if (fileName.IndexOf('(') != -1)
                    {
                        var indexLeft = fileName.IndexOf('(') + 1;
                        var resourceType = fileName.Substring(indexLeft, fileName.IndexOf(')') - indexLeft);
                        var type = (PHAssetResourceType) Enum.Parse(typeof(PHAssetResourceType), resourceType);
                        types[i] = type;
                    }
                    else
                    {
                        var uti = UTI.FromFileName(Path.GetExtension(fileName));
                        if (uti.ConformsTo(UTType.Image)) types[i] = PHAssetResourceType.Photo;
                        else if (uti.ConformsTo(UTType.Video)) types[i] = PHAssetResourceType.Video;
                    }
                }

                var alert = UIAlertController.Create("正在更新相册……", null, UIAlertControllerStyle.Alert);
                PresentViewController(alert, true, null);

                PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() => {
                    var assets = PHAssetCreationRequest.CreationRequestForAsset();

                    var requestOptions = new PHAssetResourceCreationOptions {
                        ShouldMoveFile = true
                    };
                    for (var i = 0; i < items.Count; i++)
                    {
                        var url = NSUrl.FromFilename(items[i].FullName);
                        assets.AddResource(types[i], url, requestOptions);
                    }
                }, (isSuccess, error) => {
                    InvokeOnMainThread(() => {
                        DismissViewController(true, () => {
                            if (!isSuccess)
                            {
                                this.ShowAlert("恢复失败", error.LocalizedDescription);
                            }
                            else
                            {
                                this.ShowAlert("恢复成功", null);
                            }
                        });
                    });
                });
                return;
            }
            */

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void RefreshStatus()
        {
            TableView.ReloadRows(new[] { NSIndexPath.FromRowSection(0, 1) }, UITableViewRowAnimation.Automatic);
            TableView.ReloadSections(new NSIndexSet(2), UITableViewRowAnimation.Automatic);
        }
    }
}