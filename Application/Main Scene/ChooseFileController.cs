using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud.Interfaces.FileSystem;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class ChooseFileController : UITableViewController
    {
        public ChooseFileController(IntPtr handle) : base(handle) { }

        public IFileSystem FileSystem { get; set; }
        public string WorkingPath { get; set; }

        public event EventHandler FileUploaded;

        private DirectoryInfo directory;
        private List<FileSystemInfo> items;

        private int depth;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavigationItem.LeftBarButtonItem.Clicked += (o, e) => NavigationController.DismissViewController(true, null);
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            directory = new DirectoryInfo(PathHelpers.SharedContainer);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            directory.Refresh();
            RefreshDirectory(this, EventArgs.Empty);
        }

        #endregion

        #region TableView

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => (items?.Count ?? 0) + (depth == 0 ? 0 : 1),
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {

            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentName = depth == 1 ? "本地收藏" : directory.Parent.Name;
                cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentName}”", null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                if (item is FileInfo file)
                {
                    cell.Update(file.Name, file.Length);
                    cell.Accessory = UITableViewCellAccessory.None;
                }
                else
                {
                    cell.Update(item.Name, new UTI(UTType.Directory));
                    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                }
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var pathString = string.Join(" » ", directory.FullName.Replace(PathHelpers.SharedContainer, @"本地收藏/").Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert("当前所在目录", pathString);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                if (depth == 0) return;

                directory = directory.Parent;
                depth -= 1;
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (item is DirectoryInfo subdirectory)
                {
                    directory = subdirectory;
                    depth += 1;
                    RefreshDirectory(this, EventArgs.Empty);
                    return;
                }

                var alert = UIAlertController.Create("正在上传……", null, UIAlertControllerStyle.Alert);
                PresentViewController(alert, true, () => {
                    Task.Run(async () => {
                        try
                        {
                            var fileName = Path.GetFileName(item.FullName);
                            var stream = new FileStream(item.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var remotePath = Path.Combine(WorkingPath, fileName);
                            await FileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                            FileUploaded?.Invoke(this, EventArgs.Empty);

                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => NavigationController.DismissViewController(true, null));
                            });
                        }
                        catch (HttpRequestException exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                });
                            });

                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("无法上传此文件", exception.GetType().Name);
                                });
                            });
                        }
                    });
                });

                return;
            }
        }

        #endregion

        private void RefreshDirectory(object sender, EventArgs e)
        {
            try
            {
                items = directory.EnumerateFileSystemInfos().ToList();

            }
            catch (IOException)
            {
                items = null;
                this.ShowAlert("无法查看此文件夹", "此文件夹已被删除或内容异常。");
            }

            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
        }
    }
}
