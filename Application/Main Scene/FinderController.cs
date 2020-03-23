using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud;
using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class FinderController : UITableViewController
    {
        public FinderController(IntPtr handle) : base(handle) { }

        private const string GoToDeviceSegue = "ExploreDevice";

        private RootFileSystem fileSystem;
        private List<FileSystemEntry> items;

        private DateTime lastNotificationTime;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            lastNotificationTime = new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Local);
            Globals.CloudManager.OnError += OnPersonalCloudError;

            var cloud = Globals.CloudManager.PersonalClouds[0];
            fileSystem = cloud.RootFS;
            cloud.OnNodeChangedEvent += (o,e) => {
                InvokeOnMainThread(() => RefreshTable(this, EventArgs.Empty));
            };

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshTable;
            RefreshTable(this, EventArgs.Empty);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            base.PrepareForSegue(segue, sender);
            if (segue.Identifier == GoToDeviceSegue)
            {
                var destination = (DeviceDirectoryController) segue.DestinationViewController;
                destination.FileSystem = fileSystem;
                destination.DeviceName = items[TableView.IndexPathForSelectedRow.Row].Name;
                return;
            }
        }

        #endregion

        #region TableView DataSource

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => items?.Count ?? 0, // 1 - 1
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => items?.Count > 0 ? null : "个人云内没有可访问设备",
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                var item = items[indexPath.Row];
                cell.Update(item.Name);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0)
            {
                PerformSegue(GoToDeviceSegue, this);
                return;
            }

            tableView.DeselectRow(indexPath, true);
        }

        #endregion

        private void RefreshTable(object sender, EventArgs e)
        {
            Task.Run(async () => {
                try
                {
                    items = await fileSystem.EnumerateChildrenAsync("/").ConfigureAwait(false);
                    InvokeOnMainThread(() => {
                        if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
                        TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                    });
                }
                catch
                {
                    items = null;
                    InvokeOnMainThread(() => {
                        if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
                        this.ShowAlert("无法查询云内设备", "出现 App 内部错误。", action => {
                            TableView.ReloadSections(new NSIndexSet(1), UITableViewRowAnimation.Automatic);
                        });
                    });
                }
            });
        }

        private void OnPersonalCloudError(object sender, ServiceErrorEventArgs e)
        {
            if (e.ErrorCode == ErrorCode.NeedUpdate && DateTime.Now - lastNotificationTime > TimeSpan.FromMinutes(1))
            {
                lastNotificationTime = DateTime.Now;
                InvokeOnMainThread(() => {
                    this.ShowAlert("App 版本不匹配", "个人云内的设备已安装更新版本 App，请升级您设备上的个人云 App 以连接其它设备。");
                });
                return;
            }
        }
    }
}
