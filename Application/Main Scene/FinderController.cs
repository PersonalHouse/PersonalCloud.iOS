using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.FileSharing;
using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using PCPersonalCloud;

using Photos;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class FinderController : UITableViewController,
                                            IUIDocumentPickerDelegate,
                                            IUIDocumentInteractionControllerDelegate,
                                            IUIImagePickerControllerDelegate
    {
        public FinderController(IntPtr handle) : base(handle) { }

        private const string UploadSegue = "ChooseLocalFile";
        private const string MoveToSegue = "ChooseMoveDestination";
        private const string AddSegue = "AddStorageService";

        private UIBarButtonItem HomeItem { get; set; }
        private UIBarButtonItem NewFolderItem { get; set; }
        private UIBarButtonItem NewFileItem { get; set; }
        private UIBarButtonItem HelpItem { get; set; }
        private UIBarButtonItem AddDeviceItem { get; set; }

        private PersonalCloud cloud;
        private RootFileSystem fileSystem;
        private string workingPath;
        private List<FileSystemEntry> items;

        private DateTime lastNotificationTime;
        private bool refreshNow;
        private string pendingMoveSource;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            HomeItem = new UIBarButtonItem(UIImage.FromBundle("Home"), UIBarButtonItemStyle.Bordered, GoHome);
            NewFolderItem = new UIBarButtonItem(UIImage.FromBundle("NewFolder"), UIBarButtonItemStyle.Bordered, CreateFolder);
            NewFileItem = new UIBarButtonItem(UIImage.FromBundle("NewFile"), UIBarButtonItemStyle.Bordered, UploadFile);
            HelpItem = new UIBarButtonItem(UIImage.FromBundle("Help"), UIBarButtonItemStyle.Bordered, ShowHelp);
            AddDeviceItem = new UIBarButtonItem(UIBarButtonSystemItem.Add, AddDeviceOrService);

            lastNotificationTime = new DateTime(2020, 3, 1, 0, 0, 0, DateTimeKind.Local);
            Globals.CloudManager.OnError += OnPersonalCloudError;
            cloud = Globals.CloudManager.PersonalClouds[0];
            fileSystem = cloud.RootFS;

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            GoHome(this, EventArgs.Empty);
        }


        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            cloud.OnNodeChangedEvent += RefreshDevices;
            RefreshDirectory(this, EventArgs.Empty);
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            cloud.OnNodeChangedEvent -= RefreshDevices;
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            base.PrepareForSegue(segue, sender);
            switch (segue.Identifier)
            {
                case UploadSegue:
                {
                    var navigation = (UINavigationController) segue.DestinationViewController;
                    var chooser = (ChooseFileController) navigation.TopViewController;
                    chooser.FileSystem = fileSystem;
                    chooser.WorkingPath = workingPath;
                    chooser.FileUploaded += (o, e) => refreshNow = true;
                    return;
                }
                case MoveToSegue:
                {
                    var navigation = (UINavigationController) segue.DestinationViewController;
                    var chooser = (ChooseDeviceController) navigation.TopViewController;
                    chooser.FileSystem = fileSystem;
                    chooser.NavigationTitle = this.Localize("Finder.MoveTo");
                    var deviceRoot = workingPath.Substring(1);
                    var nextSeparator = deviceRoot.IndexOf(Path.AltDirectorySeparatorChar);
                    if (nextSeparator == -1) deviceRoot = "/" + deviceRoot;
                    else deviceRoot = "/" + deviceRoot.Substring(0, nextSeparator);
                    chooser.RootPath = deviceRoot;
                    chooser.PathSelected += (o, e) => InvokeOnMainThread(() => MoveFile(e.Path));
                    return;
                }
            }
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => (items?.Count ?? 0) + (workingPath.Length == 1 ? 0 : 1),
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => (workingPath.Length == 1 && (items?.Count ?? 0) == 0) ? this.Localize("Finder.EmptyRoot") : null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentPath)) cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoHome"), this.Localize("Finder.ReturnToRoot"), null);
                else cell.Update(UIImage.FromBundle("DirectoryBack"), this.Localize("Finder.GoBack"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.ReturnTo.Formattable"), parentPath), null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory)
                {
                    if (item.Attributes.HasFlag(FileAttributes.Device)) cell.Update(item.Name, new UTI(UTType.Directory), this.Localize("Finder.Device"));
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
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                var pathString = string.Join(" » ", workingPath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                SPAlert.PresentCustom(this.Localize("Finder.CurrentDirectory") + Environment.NewLine + pathString, SPAlertHaptic.None);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                var parent = Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parent) || parent.Length == 1) workingPath = "/";
                else workingPath = parent;
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 0)
            {
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory)
                {
                    workingPath = Path.Combine(workingPath, item.Name);
                    RefreshDirectory(this, EventArgs.Empty);
                    return;
                }

                if (item.Name.EndsWith(".PLAsset", StringComparison.InvariantCultureIgnoreCase))
                {
                    this.ShowWarning(this.Localize("Backup.RestoreFromPLAsset"),
                                     string.Format(CultureInfo.InvariantCulture, this.Localize("Backup.DownloadBeforeRestore.Formattable"), item.Name));
                    return;
                }

                var filePath = Path.Combine(Paths.Temporary, item.Name);
                if (File.Exists(filePath))
                {
                    var fileInfo = new FileInfo(filePath);
                    if (fileInfo.Length == item.Size)
                    {
                        var cacheUrl = NSUrl.FromFilename(filePath);
                        this.PreviewFile(cacheUrl);
                        return;
                    }
                    else
                    {
                        try { fileInfo.Delete(); }
                        catch { }
                    }
                }

                PreparePlaceholder(item, filePath, url => {
                    this.PreviewFile(url);
                }, exception => {
                    if (exception is HttpRequestException http) PresentViewController(CloudExceptions.Explain(http), true, null);
                    else this.ShowError(this.Localize("Error.Download"), exception.GetType().Name);
                });

                return;
            }
        }

        // iOS 8.0+
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0) return false;
            if (indexPath.Section == 0)
            {
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory && item.IsReadOnly) return false;
                return true;
            }
            return false;
        }

        // Required for iOS < 9.0
        public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            // See EditActionsForRow(UITableView, NSIndexPath).
        }

        // iOS 8.0+
        public override UITableViewRowAction[] EditActionsForRow(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0) return null;

            if (indexPath.Section == 0)
            {
                var actions = new List<UITableViewRowAction>(4);

                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (!item.IsDirectory)
                {
                    var download = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, this.Localize("Finder.Favorite"), (action, indexPath) => {
                        TableView.SetEditing(false, true);

                        var filePath = Path.Combine(Paths.Favorites, item.Name);
                        PreparePlaceholder(item, filePath, url => {
                            this.ShowConfirmation(this.Localize("Finder.AddedToFavorite"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.ItemAddedToFavorite.Formattable"), item.Name));
                        }, exception => {
                            if (exception is HttpRequestException http) PresentViewController(CloudExceptions.Explain(http), true, null);
                            else this.ShowError(this.Localize("Error.Download"), exception.GetType().Name);
                        });
                    });
                    download.BackgroundColor = Colors.OrangeFlag;
                    actions.Add(download);
                }

                if (item.IsReadOnly) return actions.ToArray();

                if (!item.Attributes.HasFlag(FileAttributes.Device))
                {
                    var move = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, this.Localize("Finder.Move"), (action, indexPath) => {
                        TableView.SetEditing(false, true);
                        pendingMoveSource = Path.Combine(workingPath, item.Name);
                        PerformSegue(MoveToSegue, this);
                    });
                    move.BackgroundColor = Colors.BlueButton;
                    actions.Add(move);

                    var rename = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, this.Localize("Finder.Rename"), (action, indexPath) => {
                        TableView.SetEditing(false, true);
                        RenameEntry(item);
                    });
                    rename.BackgroundColor = Colors.Indigo;
                    actions.Add(rename);
                }

                var delete = UITableViewRowAction.Create(UITableViewRowActionStyle.Destructive, this.Localize("Finder.Delete"), (action, indexPath) => {
                    TableView.SetEditing(false, true);
                    DeleteEntry(item);
                });
                actions.Add(delete);
                actions.Reverse();

                return actions.ToArray();
            }

            return null;
        }

        // iOS 11.0+
        public override UISwipeActionsConfiguration GetLeadingSwipeActionsConfiguration(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory) return null;

                var download = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, this.Localize("Finder.Favorite"), (action, view, handler) => {
                    handler?.Invoke(true);
                    PreparePlaceholder(item, Path.Combine(Paths.Favorites, item.Name), url => {
                        this.ShowConfirmation(this.Localize("Finder.AddedToFavorite"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.ItemAddedToFavorite.Formattable"), item.Name));
                    }, exception => {
                        if (exception is HttpRequestException http) PresentViewController(CloudExceptions.Explain(http), true, null);
                        else this.ShowError(this.Localize("Error.Download"), exception.GetType().Name);
                    });
                });
                download.BackgroundColor = Colors.OrangeFlag;

                var actions = UISwipeActionsConfiguration.FromActions(new[] { download });
                actions.PerformsFirstActionWithFullSwipe = false;
                return actions;
            }

            return null;
        }

        // iOS 11.0+
        public override UISwipeActionsConfiguration GetTrailingSwipeActionsConfiguration(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[workingPath.Length == 1 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsReadOnly) return null;
                var actions = new List<UIContextualAction>(4);

                if (!item.Attributes.HasFlag(FileAttributes.Device))
                {
                    var move = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, this.Localize("Finder.Move"), (action, view, handler) => {
                        handler?.Invoke(true);
                        pendingMoveSource = Path.Combine(workingPath, item.Name);
                        PerformSegue(MoveToSegue, this);
                    });
                    move.BackgroundColor = Colors.BlueButton;
                    actions.Add(move);

                    var rename = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, this.Localize("Finder.Rename"), (action, view, handler) => {
                        handler?.Invoke(true);
                        RenameEntry(item);
                    });
                    rename.BackgroundColor = Colors.Indigo;
                    actions.Add(rename);
                }

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, this.Localize("Finder.Delete"), (action, view, handler) => {
                    handler?.Invoke(true);
                    DeleteEntry(item);
                });
                actions.Add(delete);

                actions.Reverse();
                var config = UISwipeActionsConfiguration.FromActions(actions.ToArray());
                config.PerformsFirstActionWithFullSwipe = false;
                return config;
            }

            return null;
        }

        #endregion

        #region Bar Buttons

        private void GoHome(object sender, EventArgs e)
        {
            workingPath = "/";
            RefreshDirectory(this, EventArgs.Empty);
        }

        private void ShowHelp(object sender, EventArgs e)
        {
            this.ShowHelp(this.Localize("Help.Finder"), this.Localize("Help.BrowseInFinder"));
        }

        private void AddDeviceOrService(object sender, EventArgs e)
        {
            refreshNow = true;
            PerformSegue(AddSegue, this);
        }

        private void CreateFolder(object sender, EventArgs e)
        {
            this.CreatePrompt(this.Localize("Finder.NewFolderName"), this.Localize("Finder.NewFolderHere"), null, this.Localize("Finder.NewFolderPlaceholder"), this.Localize("Finder.CreateNewFolder"), this.Localize("Global.CancelAction"), text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowError(this.Localize("Finder.BadFolderName"));
                    return;
                }

                var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
                hud.Label.Text = this.Localize("Finder.MakingNewFolder");
                Task.Run(async () => {
                    try
                    {
                        var path = Path.Combine(workingPath, text);
                        await fileSystem.CreateDirectoryAsync(path).ConfigureAwait(false);

                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            RefreshDirectory(this, EventArgs.Empty);
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            PresentViewController(CloudExceptions.Explain(exception), true, null);
                        });

                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowError(this.Localize("Error.NewFolder"), exception.GetType().Name);
                            });
                        });
                    }
                });
            });
        }

        private void UploadFile(object sender, EventArgs e)
        {
            var choices = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
            choices.AddAction(UIAlertAction.Create(this.Localize("Finder.UploadOpenPhotos"), UIAlertActionStyle.Default, action => {
                UploadPhoto();
            }));
            choices.AddAction(UIAlertAction.Create(this.Localize("Finder.UploadPickFromFavorites"), UIAlertActionStyle.Default, action => {
                PerformSegue(UploadSegue, this);
            }));
            choices.AddAction(UIAlertAction.Create(this.Localize("Finder.UploadOpenFilesApp"), UIAlertActionStyle.Default, action => {
                var picker = new UIDocumentPickerViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Import);
                picker.SetAllowsMultipleSelection(true);
                picker.SetShowFileExtensions();
                picker.Delegate = this;
                picker.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                PresentViewController(picker, true, null);
            }));
            choices.AddAction(UIAlertAction.Create(this.Localize("Global.BackAction"), UIAlertActionStyle.Cancel, null));
            this.PresentActionSheet(choices, NavigationItem.RightBarButtonItem.UserInfoGetView());
        }

        private void UploadPhoto()
        {
            PHPhotoLibrary.RequestAuthorization(status => {
                InvokeOnMainThread(() => {
                    if (status == PHAuthorizationStatus.Authorized)
                    {
                        if (!UIImagePickerController.IsSourceTypeAvailable(UIImagePickerControllerSourceType.PhotoLibrary))
                        {
                            this.ShowError(this.Localize("Settings.CannotReadPhotos"), this.Localize("Permission.Photos"));
                            return;
                        }

                        var picker = new UIImagePickerController() {
                            SourceType = UIImagePickerControllerSourceType.PhotoLibrary,
                            MediaTypes = new string[] { UTType.Image, UTType.Movie },
                            Delegate = this
                        };
                        PresentViewController(picker, true, null);
                    }
                    else
                    {
                        this.ShowError(this.Localize("Settings.CannotReadPhotos"), this.Localize("Permission.Photos"));
                    }
                });
            });
        }

        #endregion

        #region IUIImagePickerControllerDelegate

        [Export("imagePickerController:didFinishPickingMediaWithInfo:")]
        public void FinishedPickingMedia(UIImagePickerController picker, NSDictionary info)
        {
            DismissViewController(true, null);

            var type = (NSString) info.ObjectForKey(UIImagePickerController.MediaType);
            if (type is null)
            {
                this.ShowError(this.Localize("Error.PhotoPicker"));
                return;
            }

            if (type == UTType.Image)
            {
                var asset = (PHAsset) info.ObjectForKey(UIImagePickerController.PHAsset);
                asset = null;

                // Fallback: Read image file from URL.
                if (asset is null)
                {
                    var url = (NSUrl) info.ObjectForKey(UIImagePickerController.ImageUrl);
                    UploadFilesAt(url);
                }

                return;
            }

            if (type == UTType.Movie)
            {
                var asset = (PHAsset) info.ObjectForKey(UIImagePickerController.PHAsset);
                asset = null;

                // Fallback: Read video file from URL.
                if (asset is null)
                {
                    var url = (NSUrl) info.ObjectForKey(UIImagePickerController.MediaURL);
                    UploadFilesAt(url);
                }

                return;
            }

            this.ShowError(this.Localize("Error.PhotoPicker"));
        }

        #endregion

        #region Utility: Refresh

        private void RefreshDevices(object sender, EventArgs e)
        {
            if (workingPath.Length != 1) return;
            InvokeOnMainThread(() => RefreshDirectory(this, EventArgs.Empty));
        }

        private void RefreshDirectory(object sender, EventArgs e)
        {
            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
            refreshNow = false;

            if (workingPath.Length == 1)
            {
                NavigationItem.Title = this.Localize("Finder.Title");
                NavigationItem.SetLeftBarButtonItem(HelpItem, true);
                NavigationItem.SetRightBarButtonItems(new[] { AddDeviceItem }, true);

                try { Globals.CloudManager.StartNetwork(false); }
                catch { } // Ignored.
            }
            else
            {
                NavigationItem.SetLeftBarButtonItem(HomeItem, true);
                NavigationItem.SetRightBarButtonItems(new[] { NewFileItem, NewFolderItem }, true);
            }

            if (!workingPath.EndsWith(Path.AltDirectorySeparatorChar)) workingPath += Path.AltDirectorySeparatorChar;

            var hud = MBProgressHUD.ShowHUD(TabBarController.View, true);
            hud.Label.Text = this.Localize("Global.LoadingStatus");
            Task.Run(async () => {
                string title = null;
                if (workingPath.Length != 1)
                {
                    var deviceNameEnd = workingPath.IndexOf(Path.AltDirectorySeparatorChar, 1);
                    if (deviceNameEnd != -1) title = workingPath.Substring(1, deviceNameEnd).Trim(Path.AltDirectorySeparatorChar);
                }

                try
                {
                    var files = await fileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                    items = files.SortDirectoryFirstByName().ToList();
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                        TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                    });
                }
                catch (HttpRequestException exception)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        PresentViewController(CloudExceptions.Explain(exception), true, null);
                        items = null;
                        if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                        TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                    });
                }
                catch (Exception exception)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.RefreshDirectory"), exception.GetType().Name);
                        items = null;
                        if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                        TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                    });
                }
            });
        }

        #endregion

        #region Utility: Download & Open

        private void PreparePlaceholder(FileSystemEntry item, string cachePath, Action<NSUrl> onCompletion, Action<Exception> onError)
        {
            if (File.Exists(cachePath))
            {
                var alert = UIAlertController.Create(this.Localize("Finder.ReplaceLocalFavorite"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.ReplaceFavoriteAlternatives.Formattable"), item.Name), UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create(this.Localize("Finder.Replace"), UIAlertActionStyle.Cancel, action => {
                    try { File.Delete(cachePath); }
                    catch { }
                    PrepareConnection(item, cachePath, onCompletion, onError);
                }));
                var ok = UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Default, null);
                alert.AddAction(ok);
                alert.PreferredAction = ok;
                PresentViewController(alert, true, null);
                return;
            }

            PrepareConnection(item, cachePath, onCompletion, onError);
        }

        private void PrepareConnection(FileSystemEntry item, string cachePath, Action<NSUrl> onCompletion, Action<Exception> onError)
        {
            if (item.Size > 100000000)
            {
                var alert = UIAlertController.Create(this.Localize("Finder.DownloadOversizedFile"), this.Localize("Finder.SizeOver100MB"), UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Cancel, null));
                var ok = UIAlertAction.Create(this.Localize("Finder.StartDownloading"), UIAlertActionStyle.Default, action => {
                    DownloadFile(item, cachePath, onCompletion, onError);
                });
                alert.AddAction(ok);
                alert.PreferredAction = ok;
                PresentViewController(alert, true, null);
                return;
            }

            DownloadFile(item, cachePath, onCompletion, onError);
        }

        private void DownloadFile(FileSystemEntry item, string cachePath, Action<NSUrl> onCompletion, Action<Exception> onError)
        {
            if (File.Exists(cachePath))
            {
                this.ShowError(this.Localize("Error.Download"), this.Localize("Error.IOConflict"));
                return;
            }

            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Finder.Downloading");
            Task.Run(async () => {
                try
                {
                    var source = Path.Combine(workingPath, item.Name);
                    var target = new FileStream(cachePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                    await (await fileSystem.ReadFileAsync(source).ConfigureAwait(false)).CopyToAsync(target).ConfigureAwait(false);
                    await target.DisposeAsync().ConfigureAwait(false);

                    var url = NSUrl.FromFilename(cachePath);
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        onCompletion?.Invoke(url);
                    });
                }
                catch (Exception exception)
                {
                    try { File.Delete(cachePath); }
                    catch { }

                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        onError?.Invoke(exception);
                    });
                }
            });
        }

        #endregion

        #region Utility: Rename & Delete

        private void RenameEntry(FileSystemEntry item)
        {
            this.CreatePrompt(this.Localize("Finder.NewName"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.RenameItem.Formattable"), item.Name), item.Name, item.Name, this.Localize("Finder.SaveNewName"), this.Localize("Global.CancelAction"), text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowWarning(this.Localize("Finder.BadFileName"));
                    return;
                }

                if (text == item.Name) return;

                var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
                hud.Label.Text = this.Localize("Finder.Renaming");

                Task.Run(async () => {
                    try
                    {
                        var path = Path.Combine(workingPath, item.Name);
                        await fileSystem.RenameAsync(path, text).ConfigureAwait(false);

                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            RefreshDirectory(this, EventArgs.Empty);
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            PresentViewController(CloudExceptions.Explain(exception), true, null);
                        });
                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowError(this.Localize("Error.Rename"), exception.GetType().Name);
                            });
                        });
                    }
                });
            });
        }

        private void MoveFile(string destination)
        {
            if (string.IsNullOrEmpty(pendingMoveSource)) return;
            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Finder.Moving");
            Task.Run(async () => {
                try
                {
                    var fileName = Path.GetFileName(pendingMoveSource);
                    var path = Path.Combine(destination, fileName);
                    await fileSystem.RenameAsync(pendingMoveSource, path).ConfigureAwait(false);
                    pendingMoveSource = null;

                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        RefreshDirectory(this, EventArgs.Empty);
                    });
                }
                catch (HttpRequestException exception)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        PresentViewController(CloudExceptions.Explain(exception), true, null);
                    });
                }
                catch (Exception exception)
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.Move"), exception.GetType().Name);
                    });
                }
            });
        }

        private void DeleteEntry(FileSystemEntry item)
        {
            UIAlertController alert;
            if (item.Attributes.HasFlag(FileAttributes.Device))
            {
                alert = UIAlertController.Create(this.Localize("Finder.RemoveDevice"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.RemoveCredentials.Formattable"), item.Name), UIAlertControllerStyle.Alert);
            }
            else
            {
                alert = UIAlertController.Create(this.Localize("Finder.DeleteFile"), string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.DeleteContents.Formattable"), item.Name), UIAlertControllerStyle.Alert);
            }
            alert.AddAction(UIAlertAction.Create(this.Localize("Finder.Delete"), UIAlertActionStyle.Destructive, action => {
                var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
                hud.Label.Text = this.Localize("Finder.Deleting");
                Task.Run(async () => {
                    try
                    {
                        var path = Path.Combine(workingPath, item.Name);
                        if (item.IsDirectory) path += Path.AltDirectorySeparatorChar;
                        await fileSystem.DeleteAsync(path).ConfigureAwait(false);

                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            items.Remove(item);
                            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);

                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            PresentViewController(CloudExceptions.Explain(exception), true, null);
                        });
                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            this.ShowError(this.Localize("Error.Delete"), exception.GetType().Name);
                        });
                    }
                });
            }));
            var ok = UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.PreferredAction = ok;
            PresentViewController(alert, true, null);
        }

        #endregion

        private void OnPersonalCloudError(object sender, ServiceErrorEventArgs e)
        {
            if (e.ErrorCode == ErrorCode.NeedUpdate && DateTime.Now - lastNotificationTime > TimeSpan.FromMinutes(1))
            {
                lastNotificationTime = DateTime.Now;
                InvokeOnMainThread(() => {
                    this.ShowError(this.Localize("Error.OldVersion.Short"), this.Localize("Error.OldVersion.Long"));
                });
                return;
            }
        }

        #region IUIDocumentPickerDelegate

        // iOS 11.0+
        [Export("documentPicker:didPickDocumentsAtURLs:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            UploadFilesAt(urls);
        }

        // iOS 8.0+
        [Export("documentPicker:didPickDocumentAtURL:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
        {
            UploadFilesAt(url);
        }

        private void UploadFilesAt(params NSUrl[] urls)
        {
            var hud = MBProgressHUD.ShowHUD(TabBarController.View, true);
            hud.Label.Text = this.Localize("Finder.Uploading");
            Task.Run(async () => {
                var total = urls.Length;

                // Calculate total file size.
                var fileSizes = new long[total];
                for (var i = 0; i < total; i++)
                {
                    var url = urls[i];
                    if (url.TryGetResource(NSUrl.FileSizeKey, out var obj) && obj is NSNumber number)
                    {
                        fileSizes[i] = number.Int64Value;
                    }
                }
                var progress = NSProgress.FromTotalUnitCount(fileSizes.Sum());
                InvokeOnMainThread(() => {
                    hud.ProgressObject = progress;
                    hud.Mode = MBProgressHUDMode.AnnularDeterminate;
                });

                var failed = 0;
                for (var i = 0; i < total; i++)
                {
                    var url = urls[i];
                    InvokeOnMainThread(() => hud.Label.Text = string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.UploadingProgress.Formattable"), i + 1, total));
                    if (!url.IsFileUrl)
                    {
                        failed += 1;
                        continue;
                    }

                    var shouldRelease = url.StartAccessingSecurityScopedResource();
                    Timer progressTimer = null;
                    try
                    {
                        var fileName = Path.GetFileName(url.Path);

                        if (Guid.TryParse(Path.GetFileNameWithoutExtension(fileName), out _))
                        {
                            fileName = DateTime.Now.ToString("yyyy-MM-dd HH-mm-ss", CultureInfo.InvariantCulture) + Path.GetExtension(fileName) ?? string.Empty;
                        }

                        using var stream = new FileStream(url.Path, FileMode.Open, FileAccess.Read, FileShare.Read);

                        // Post progress.
                        long lastRead = 0;
                        progressTimer = new Timer(obj => {
                            var position = stream.Position;
                            if (position == 0 || position <= lastRead) return;
                            progress.CompletedUnitCount += position - lastRead;
                            lastRead = position;
                        }, null, TimeSpan.Zero, TimeSpan.FromSeconds(0.1));
                        var remotePath = Path.Combine(workingPath, fileName);
                        await fileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                    }
                    catch
                    {
                        failed += 1;
                    }
                    finally
                    {
                        progressTimer.Dispose();
                        progressTimer = null;
                    }

                    if (shouldRelease) url.StopAccessingSecurityScopedResource();
                }

                InvokeOnMainThread(() => {
                    hud.Hide(true);
                    RefreshDirectory(this, EventArgs.Empty);
                    SPAlert.PresentPreset(string.Format(CultureInfo.InvariantCulture, this.Localize("Finder.Uploaded.Formattable"), total - failed), null,
                        failed == 0 ? SPAlertPreset.Done : SPAlertPreset.Warning);
                });
            });
        }

        #endregion

        #region IUIDocumentInteractionControllerDelegate

        [Export("documentInteractionControllerViewControllerForPreview:")]
        public UIViewController ViewControllerForPreview(UIDocumentInteractionController controller) => this;

        #endregion

    }
}
