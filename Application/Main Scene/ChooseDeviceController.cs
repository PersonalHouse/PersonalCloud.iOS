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
    public partial class ChooseDeviceController : UITableViewController
    {
        public ChooseDeviceController(IntPtr handle) : base(handle) { }

        public string NavigationTitle { get; set; }
        public string RootPath { get; set; } = "/";
        public IFileSystem FileSystem { get; set; }

        public event EventHandler<PathSelectedEventArgs> PathSelected;

        private string workingPath;
        private List<FileSystemEntry> items;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavigationItem.Title = NavigationTitle;
            if (RootPath.EndsWith(Path.AltDirectorySeparatorChar)) RootPath.Substring(0, RootPath.Length - 1);
            workingPath = RootPath;

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            CancelButton.Clicked += (o, e) => this.ShowDiscardConfirmation();
            SaveButton.Clicked += SavePath;

            RefreshDirectory(this, EventArgs.Empty);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            Globals.CloudManager.PersonalClouds[0].OnNodeChangedEvent += OnDevicesRefreshed;
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            Globals.CloudManager.PersonalClouds[0].OnNodeChangedEvent -= OnDevicesRefreshed;
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 2;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 0,
                1 => (items?.Count ?? 0) + (workingPath.Length == 1 ? 0 : 1),
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => workingPath.Length != 1 ? "点击“保存”将使用此文件夹。如果要使用子文件夹，请打开子文件夹后点击“保存”。" : null,
                1 => (workingPath.Length == 1 && (items?.Count ?? 0) == 0) ? "个人云内没有可访问设备" : null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentPath)) cell.Update(UIImage.FromBundle("DirectoryBack"), "返回顶层", "后退至设备列表", null);
                else cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentPath}”", null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 1)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory)
                {
                    if (item.Attributes.HasFlag(FileAttributes.Device)) cell.Update(item.Name, new UTI(UTType.Directory), "设备");
                    else cell.Update(item.Name, new UTI(UTType.Directory));
                    cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                }
                else if (item.Size.HasValue)
                {
                    cell.Update(item.Name, item.Size.Value);
                    cell.Accessory = UITableViewCellAccessory.None;
                }
                else
                {
                    cell.Update(item.Name);
                    cell.Accessory = UITableViewCellAccessory.None;
                }
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        #region TableView Delegate

        public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 1 && indexPath.Row == 0)
            {
                var pathString = string.Join(" » ", workingPath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert("当前所在目录", pathString);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (workingPath.Length != 1 && indexPath.Section == 1 && indexPath.Row == 0)
            {
                var parent = Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar));
                if (!parent.StartsWith(RootPath))
                {
                    this.ShowAlert("无法返回上一层", "您要执行的操作必须在此文件夹或更下层级文件夹完成，因此不允许您后退至上一层级。");
                    return;
                }
                workingPath = parent;
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 1)
            {
                var item = items[workingPath.Length == 1? indexPath.Row : (indexPath.Row - 1)];
                workingPath = Path.Combine(workingPath, item.Name);
                RefreshDirectory(this, EventArgs.Empty);
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
                        var files = await FileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                        items = files.Where(x => x.Attributes.HasFlag(FileAttributes.Directory) && !x.Attributes.HasFlag(FileAttributes.Hidden) && !x.Attributes.HasFlag(FileAttributes.System))
                                     .OrderBy(x => x.Name).ToList();
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        if (exception.Message.Contains("429"))
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("远程设备忙", "此文件夹内容过多，无法在限定时间内收集内容详情。请稍后查看。");
                                    items = null;
                                    TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
                                });
                            });
                            return;
                        }

                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                items = null;
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
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

        private void SavePath(object sender, EventArgs e)
        {
            if (workingPath == "/")
            {
                this.ShowAlert("存储位置无效", "无法将数据存储在当前位置，请至少选择一台设备。");
                return;
            }
            NavigationController.DismissViewController(true, () => {
                PathSelected?.Invoke(this, new PathSelectedEventArgs(workingPath));
            });
        }

        private void OnDevicesRefreshed(object sender, EventArgs e)
        {
            if (workingPath.Length == 1) InvokeOnMainThread(() => RefreshDirectory(this, EventArgs.Empty));
        }
    }
}