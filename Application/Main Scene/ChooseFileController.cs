using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
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

        private List<FileInfo> pendingFiles;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavigationItem.LeftBarButtonItem.Clicked += (o, e) => NavigationController.DismissViewController(true, null);
            NavigationItem.RightBarButtonItem.Clicked += UploadFiles;
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            directory = new DirectoryInfo(Paths.Favorites);
            pendingFiles = new List<FileInfo>();
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
                var parentName = depth == 1 ? this.Localize("Favorites.Title") : directory.Parent.Name;
                cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoBack"), string.Format(this.Localize("Finder.ReturnTo.Formattable"), parentName), null);
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
                    cell.Accessory = pendingFiles.Contains(item) ? UITableViewCellAccessory.Checkmark : UITableViewCellAccessory.None;
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
                var pathString = string.Join(" » ", directory.FullName.Replace(Paths.Favorites, this.Localize("Favorites.Root")).Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert(this.Localize("Finder.CurrentDirectory"), pathString);
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
                    pendingFiles.Clear();
                    this.ShowAlert("Previous Selections Cleared", null);
                    return;
                }

                var fileInfo = (FileInfo) item;
                if (pendingFiles.Contains(fileInfo)) pendingFiles.Remove(fileInfo);
                else pendingFiles.Add(fileInfo);

                tableView.ReloadRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);

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
                this.ShowAlert(this.Localize("Error.RefreshDirectory"), this.Localize("Favorites.BadFolder"));
            }

            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
        }

        private void UploadFiles(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create(this.Localize("Finder.Uploading"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    var failed = 0;
                    foreach (var item in pendingFiles)
                    {
                        try
                        {
                            var fileName = Path.GetFileName(item.FullName);
                            var stream = new FileStream(item.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);
                            var remotePath = Path.Combine(WorkingPath, fileName);
                            await FileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                            FileUploaded?.Invoke(this, EventArgs.Empty);
                        }
                        catch
                        {
                            failed += 1;
                        }
                    }

                    InvokeOnMainThread(() => {
                        DismissViewController(true, () => {
                            this.ShowAlert($"{pendingFiles.Count - failed} File(s) Uploaded", null, action => {
                                NavigationController.DismissViewController(true, null);
                            });
                        });
                    });
                });
            });
        }
    }
}
