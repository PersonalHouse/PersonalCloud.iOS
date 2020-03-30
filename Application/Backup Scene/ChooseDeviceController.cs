using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class ChooseDeviceController : UITableViewController
    {
        public ChooseDeviceController(IntPtr handle) : base(handle) { }

        private RootFileSystem fileSystem;
        private string workingPath;
        private List<FileSystemEntry> items;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var cloud = Globals.CloudManager.PersonalClouds[0];
            fileSystem = cloud.RootFS;
            workingPath = "/";

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            CancelButton.Clicked += (o, e) => {
                this.ShowDiscardConfirmation();
            };
            SaveButton.Clicked += SaveBackupPath; ;

            RefreshDirectory(this, EventArgs.Empty);
        }

        #endregion

        #region Data Source

        public override nint NumberOfSections(UITableView tableView) => 3;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 0,
                1 => (items?.Count ?? 0) + (workingPath == "/" ? 0 : 1),
                2 => workingPath == "/" ? 0 : 1,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => workingPath == "/" ? null : "点击“保存”将自动备份相册到此文件夹。如果要使用子文件夹，请点击打开子文件夹后“保存”。如果自动备份执行时选定文件夹无法访问，自动备份将跳过本次执行。",
                1 => workingPath == "/" && (items?.Count ?? 0) == 0 ? "个人云内没有可访问设备" : null,
                2 => null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 1)
            {
                if (workingPath == "/")
                {
                    var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                    var item = items[indexPath.Row];
                    cell.Update(item.Name);
                    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                    return cell;
                }

                if (indexPath.Row == 0)
                {
                    var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                    var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                    if (string.IsNullOrEmpty(parentPath)) parentPath = "/";
                    cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentPath}”", null);
                    cell.Accessory = UITableViewCellAccessory.DetailButton;
                    return cell;
                }

                var folderCell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var folder = items[indexPath.Row - 1];
                folderCell.Update(folder.Name, new UTI(UTType.Directory));
                folderCell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return folderCell;
            }

            if (indexPath.Section == 2)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update("将备份存储在当前文件夹", Colors.BlueButton, true);
                cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 1 && indexPath.Row == 0 && workingPath != "/")
            {
                var pathString = string.Join(" » ", workingPath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert("当前所在目录", pathString);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 1 && indexPath.Row == 0 && workingPath != "/")
            {
                workingPath = Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar));
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 1)
            {
                var item = items[workingPath == "/" ? indexPath.Row : (indexPath.Row - 1)];
                workingPath = Path.Combine(workingPath, item.Name);
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 2 && indexPath.Row == 0)
            {
                SaveBackupPath(this, EventArgs.Empty);
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void RefreshDirectory(object sender, EventArgs e)
        {
            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();

            var alert = UIAlertController.Create("正在加载……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var files = await fileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                        items = files.Where(x => x.Attributes.HasFlag(FileAttributes.Directory) && !x.Attributes.HasFlag(FileAttributes.Hidden) && !x.Attributes.HasFlag(FileAttributes.System)).ToList();
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                TableView.ReloadData();
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        if (exception.Message.StartsWith("429"))
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("远程设备忙", "此文件夹内容过多，无法在限定时间内收集内容详情。请稍后查看。");
                                    items = null;
                                    TableView.ReloadData();
                                });
                            });
                            return;
                        }

                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                items = null;
                                TableView.ReloadData();
                            });
                        });

                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("无法打开文件夹", exception.GetType().Name);
                                items = null;
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                });
            });
        }

        private void SaveBackupPath(object sender, EventArgs e)
        {
            if (workingPath == "/")
            {
                this.ShowAlert("备份存储位置无效", "请至少选择一台设备以存储照片备份。");
                return;
            }
            Globals.Database.SaveSetting(UserSettings.PhotoBackupPrefix, workingPath);
            NavigationController.DismissViewController(true, null);
        }
    }
}