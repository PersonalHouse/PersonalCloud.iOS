using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Foundation;

using Microsoft.Extensions.Logging;

using MobileCoreServices;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.Interfaces.FileSystem;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
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
        ILogger logger;

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
            logger = Globals.Loggers.CreateLogger<ChooseFileController>();
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
                cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoBack"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.ReturnTo.Formattable"), parentName), null);
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
                this.ShowMsg(this.Localize("Finder.CurrentDirectory") + Environment.NewLine + pathString);
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
                pendingFiles.Clear();
                this.ShowWarning(this.Localize("Finder.SelectionCleared"), this.Localize("Finder.SelectionCleared.PathChange"));
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
                    this.ShowWarning(this.Localize("Finder.SelectionCleared"), this.Localize("Finder.SelectionCleared.PathChange"));
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
                items = directory.EnumerateFileSystemInfos().SortDirectoryFirstByName().ToList();

            }
            catch (IOException)
            {
                items = null;
                this.ShowError(this.Localize("Error.RefreshDirectory"), this.Localize("Favorites.BadFolder"));
            }

            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
        }

        private void UploadFiles(object sender, EventArgs e)
        {
            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Finder.Uploading");

                Task.Run(async () => {
                    try
                    {
                        var total = pendingFiles.Count;
                        var fileSizes = new long[total];
                        for (var i = 0; i < total; i++)
                        {
                            var info = pendingFiles[i];
                            try { fileSizes[i] = info.Length; }
                            catch { }
                        }
                        var progress = NSProgress.FromTotalUnitCount(fileSizes.Sum());
                        InvokeOnMainThread(() => {
                            hud.ProgressObject = progress;
                            hud.Mode = MBProgressHUDMode.AnnularDeterminate;
                        });

                        var failed = 0;
                        Timer progressTimer = null;
                        for (var i = 0; i < total; i++)
                        {
                            var item = pendingFiles[i];
                            InvokeOnMainThread(() => hud.Label.Text = string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.UploadingProgress.Formattable"), i + 1, total));
                            try
                            {
                                var fileName = Path.GetFileName(item.FullName);
                                var stream = new FileStream(item.FullName, FileMode.Open, FileAccess.Read, FileShare.Read);

                                long lastRead = 0;
                                progressTimer = new Timer(obj => {
                                    var position = stream.Position;
                                    if (position == 0 || position <= lastRead) return;
                                    progress.CompletedUnitCount += position - lastRead;
                                    lastRead = position;
                                }, null, TimeSpan.Zero, TimeSpan.FromSeconds(0.1));
                                var remotePath = Path.Combine(WorkingPath, fileName);
                                await FileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                            }
                            catch (Exception)
                            {
                                failed += 1;
                            }
                            finally
                            {
                                progressTimer.Dispose();
                                progressTimer = null;
                            }
                        }

                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            FileUploaded?.Invoke(this, EventArgs.Empty);
                            this.ShowConfirmation(string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.Uploaded.Formattable"), total - failed), null, () => {
                                NavigationController.DismissViewController(true, null);
                            });
                        });
                    }
                    catch (Exception e)
                    {
                        logger.LogError(e, "Exception in UploadFiles");
                        throw;
                    }
            });
        }
    }
}
