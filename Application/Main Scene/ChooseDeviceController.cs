using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud.FileSharing;
using NSPersonalCloud.Interfaces.FileSystem;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
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
                0 => workingPath.Length != 1 ? this.Localize("Help.SelectPath") : null,
                1 => (workingPath.Length == 1 && (items?.Count ?? 0) == 0) ? this.Localize("Finder.EmptyRoot") : null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 1 && indexPath.Row == 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentPath)) cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoHome"), this.Localize("Finder.ReturnToRoot"), null);
                else cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoBack"), string.Format(this.Localize("Finder.ReturnTo.Formattable"), parentPath), null);
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
                this.ShowAlert(this.Localize("Finder.CurrentDirectory"), pathString);
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
                    this.ShowAlert(this.Localize("SelectPath.Restricted"), this.Localize("SelectPath.CannotGoBack"));
                    return;
                }
                workingPath = parent;
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 1)
            {
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
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

            var alert = UIAlertController.Create(this.Localize("Global.LoadingStatus"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var files = await FileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                        items = files.Where(x => x.Attributes.HasFlag(FileAttributes.Directory)).SortDirectoryFirstByName().ToList();
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                PresentViewController(CloudExceptions.Explain(exception), true, null);
                                items = null;
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 2)), UITableViewRowAnimation.Automatic);
                            });
                        });

                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert(this.Localize("Error.RefreshDirectory"), exception.GetType().Name);
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
                this.ShowAlert(this.Localize("SelectPath.BadPath"), this.Localize("SelectPath.ChooseADevice"));
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
