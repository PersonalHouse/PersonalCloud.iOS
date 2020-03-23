using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using Photos;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class PhotosBackupController : UITableViewController
    {
        public PhotosBackupController(IntPtr handle) : base(handle) { }

        private int state = 1;
        private DirectoryInfo directory;
        private List<FileInfo> items;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            Directory.CreateDirectory(PathHelpers.PhotoRestore);
            AddFileButton.Clicked += ImportFile;
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            directory = new DirectoryInfo(PathHelpers.PhotoRestore);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            directory.Refresh();
            RefreshDirectory(this, EventArgs.Empty);
        }

        #endregion

        #region Data Source

        public override nint NumberOfSections(UITableView tableView) => state;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 1,
                1 => items?.Count ?? 0,
                2 => 1,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => "恢复每个（组）照片或视频前，需要重新进入一次恢复模式。",
                1 => "添加一张照片或一段视频、以及其附属文件。",
                2 => "提交恢复请求将保存当前屏幕上的所有文件为一张照片或一段视频。如果这些文件中包含多张照片或多段视频，恢复将失败。",
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update(state == 1 ? "进入恢复模式" : "重新进入恢复模式", state == 1 ? Colors.BlueButton : Colors.DangerousRed, true);
                return cell;
            }

            if (indexPath.Section == 1)
            {
                var file = items[indexPath.Row];
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                cell.Update(file.Name, file.Length);
                return cell;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update("完成并提交", Colors.BlueButton, true);
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                if (state != 1)
                {
                    directory.Delete(true);
                    directory.Create();
                }
                else
                {
                    PHPhotoLibrary.RequestAuthorization(status => {
                        if (status == PHAuthorizationStatus.Authorized) return;
                        this.ShowAlert("个人云需要授权", "还原备份的照片或视频需要访问“相册”");
                        state = 1;
                        tableView.ReloadData();
                    });
                }

                state = 2;
                RefreshDirectory(true, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
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

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void RefreshDirectory(object sender, EventArgs e)
        {
            try
            {
                items = directory.EnumerateFiles().ToList();
            }
            catch (IOException)
            {
                items = null;
                this.ShowAlert("无法查看此文件夹", "此文件夹已被删除或内容异常。");
            }

            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();

            if (state > 1)
            {
                state = items.Count > 0 ? 3 : 2;
                TableView.ReloadData();
            }
        }

        private void ImportFile(object sender, EventArgs e)
        {
            if (state == 1)
            {
                this.ShowAlert("添加文件前必须进入恢复模式", null);
                return;
            }
            var picker = new UIDocumentPickerViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Open) {
                AllowsMultipleSelection = false
            };
            picker.DidPickDocumentAtUrls += OnDocumentsPicked;
            PresentViewController(picker, true, null);
        }

        private void OnDocumentsPicked(object sender, UIDocumentPickedAtUrlsEventArgs e)
        {
            var alert = UIAlertController.Create("正在导入……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() => {
                var url = e.Urls[0];

                try
                {
                    if (url.StartAccessingSecurityScopedResource())
                    {
                        var fileName = Path.GetFileName(url.Path);
                        if (items.Count == 0 && fileName.IndexOf('(') != -1)
                        {
                            url.StopAccessingSecurityScopedResource();
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("尚未添加原始文件", "此文件为附属修改文件，您必须先添加原始文件才能应用此修改。" + Environment.NewLine + Environment.NewLine +
                                                   "原始文件的文件名与附属文件相似，但不包含任何括号。");
                                });
                            });
                            return;
                        }

                        var filePath = Path.Combine(directory.FullName, fileName);
                        if (File.Exists(filePath))
                        {
                            File.Delete(filePath);
                        }

                        File.Copy(url.Path, filePath, false);
                        url.StopAccessingSecurityScopedResource();

                        items.Add(new FileInfo(filePath));
                    }
                }
                catch (IOException)
                {
                    url.StopAccessingSecurityScopedResource();
                    InvokeOnMainThread(() => {
                        DismissViewController(true, () => {
                            this.ShowAlert("添加文件失败", "内部文件操作错误");
                        });
                    });
                    return;
                }

                InvokeOnMainThread(() => {
                    DismissViewController(true, () => {
                        RefreshDirectory(this, EventArgs.Empty);
                    });
                });
            });
        }
    }
}