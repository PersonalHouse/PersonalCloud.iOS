using System;
using System.Collections.Generic;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class ChooseDeviceController : UITableViewController
    {
        public ChooseDeviceController (IntPtr handle) : base (handle) { }

        public string SelectedDevice { get; set; }
        public event EventHandler SelectedDeviceChanged;

        private RootFileSystem fileSystem;
        private List<FileSystemEntry> items;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            var cloud = Globals.CloudManager.PersonalClouds[0];
            fileSystem = cloud.RootFS;
            cloud.OnNodeChangedEvent += (o, e) => {
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

        #endregion

        #region Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => items?.Count ?? 0,
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

                if (item.Name == SelectedDevice) cell.Accessory = UITableViewCellAccessory.Checkmark;
                else cell.Accessory = UITableViewCellAccessory.None;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0)
            {
                var item = items[indexPath.Row];
                SelectedDevice = item.Name;
                SelectedDeviceChanged?.Invoke(this, EventArgs.Empty);
                tableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                return;
            }
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
    }
}