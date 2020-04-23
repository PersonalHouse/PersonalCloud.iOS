using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class FavoritesController : UITableViewController, IUIDocumentInteractionControllerDelegate, IUIDocumentPickerDelegate
    {
        public FavoritesController(IntPtr handle) : base(handle) { }

        private DirectoryInfo directory;
        private List<FileSystemInfo> items;

        private int depth;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavigationItem.LeftBarButtonItem.Clicked += ShowHelp;
            FileButton.Clicked += ImportFiles;
            FolderButton.Clicked += NewFolder;
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            directory = new DirectoryInfo(Paths.Favorites);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            directory.Refresh();
            if (!directory.Exists) directory.Create();
            RefreshDirectory(this, EventArgs.Empty);
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => (items?.Count ?? 0) + (depth == 0 ? 0 : 1),
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => (items?.Count ?? 0) == 0 ? this.Localize("Favorites.Hint") : null,
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

        #endregion

        #region TableView Delegate

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
                    return;
                }

                if (Path.GetExtension(item.FullName)?.ToUpperInvariant() == ".PLASSET")
                {
                    var alert = UIAlertController.Create(this.Localize("Backup.RestoreFromPLAsset"), string.Format(this.Localize("Backup.RestoreThisPhoto.Formattable"), item.Name), UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Cancel, null));
                    var restore = UIAlertAction.Create(this.Localize("Backup.Restore"), UIAlertActionStyle.Default, action => {
                        Task.Run(() => {
                            SinglePhotoPackage.RestoreFromArchive(item.FullName, () => {
                                InvokeOnMainThread(() => {
                                    var completionAlert = UIAlertController.Create(this.Localize("Backup.Restored"), string.Format(this.Localize("Backup.AddedToPhotos.Formattable"), item.Name), UIAlertControllerStyle.Alert);
                                    completionAlert.AddAction(UIAlertAction.Create(this.Localize("Backup.DeleteBackup"), UIAlertActionStyle.Destructive, action => {
                                        try { item.Delete(); }
                                        catch { }
                                        RefreshDirectory(this, EventArgs.Empty);
                                    }));
                                    var ok = UIAlertAction.Create(this.Localize("Global.OKAction"), UIAlertActionStyle.Default, null);
                                    completionAlert.AddAction(ok);
                                    completionAlert.PreferredAction = ok;
                                    PresentViewController(completionAlert, true, null);
                                });
                            }, error => {
                                InvokeOnMainThread(() => {
                                    this.ShowAlert(this.Localize("Error.RestorePhotos"), error?.LocalizedDescription ?? this.Localize("Error.Generic"));
                                });
                            });
                        });
                    });
                    alert.AddAction(restore);
                    alert.PreferredAction = restore;
                    PresentViewController(alert, true, null);
                    return;
                }

                var url = NSUrl.FromFilename(item.FullName);
                this.PreviewFile(url);
                return;
            }
        }

        // iOS 8.0+
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return false;
            if (indexPath.Section == 0) return true;
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
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];

                var rename = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, this.Localize("Finder.Rename"), (action, indexPath) => {
                    TableView.SetEditing(false, true);

                    this.CreatePrompt(this.Localize("Finder.NewName"), string.Format(this.Localize("Finder.RenameItem.Formattable"), item.Name), item.Name, item.Name, this.Localize("Finder.SaveNewName"), this.Localize("Global.CancelAction"), text => {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            this.ShowAlert(this.Localize("Finder.BadFileName"), null);
                            return;
                        }

                        if (text == item.Name) return;

                        try
                        {
                            if (item is FileInfo file) file.MoveTo(Path.Combine(Path.GetDirectoryName(file.FullName), text));
                            else
                            {
                                var directory = (DirectoryInfo) item;
                                directory.MoveTo(Path.Combine(directory.Parent.FullName, text));
                            }

                            InvokeOnMainThread(() => {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                this.ShowAlert(this.Localize("Error.Rename"), exception.GetType().Name);
                            });
                        }
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UITableViewRowAction.Create(UITableViewRowActionStyle.Destructive, this.Localize("Finder.Delete"), (action, indexPath) => {
                    TableView.SetEditing(false, true);

                    var alert = UIAlertController.Create(this.Localize("Favorites.DeleteFile"), string.Format(this.Localize("Favorites.DeleteContents.Formattable"), item.Name), UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create(this.Localize("Finder.Delete"), UIAlertActionStyle.Destructive, action => {
                        try
                        {
                            if (item is DirectoryInfo directory) directory.Delete(true);
                            else item.Delete();

                            InvokeOnMainThread(() => {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                this.ShowAlert(this.Localize("Error.Delete"), exception.GetType().Name);
                            });
                        }
                    }));
                    var ok = UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Default, null);
                    alert.AddAction(ok);
                    alert.PreferredAction = ok;
                    PresentViewController(alert, true, null);
                });

                return new[] { delete, rename };
            }

            return null;
        }

        // iOS 11.0+
        public override UISwipeActionsConfiguration GetTrailingSwipeActionsConfiguration(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];

                var rename = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, this.Localize("Finder.Rename"), (action, view, handler) => {
                    handler?.Invoke(true);

                    this.CreatePrompt(this.Localize("Finder.NewName"), string.Format(this.Localize("Finder.RenameItem.Formattable"), item.Name), item.Name, item.Name, this.Localize("Finder.SaveNewName"), this.Localize("Global.CancelAction"), text => {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            this.ShowAlert(this.Localize("Finder.BadFileName"), null);
                            return;
                        }

                        if (text == item.Name) return;

                        try
                        {
                            if (item is FileInfo file) file.MoveTo(Path.Combine(Path.GetDirectoryName(file.FullName), text));
                            else
                            {
                                var directory = (DirectoryInfo) item;
                                directory.MoveTo(Path.Combine(directory.Parent.FullName, text));
                            }

                            InvokeOnMainThread(() => {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                this.ShowAlert(this.Localize("Error.Rename"), exception.GetType().Name);
                            });
                        }
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, this.Localize("Finder.Delete"), (action, view, handler) => {
                    handler?.Invoke(true);

                    var alert = UIAlertController.Create(this.Localize("Favorites.DeleteFile"), string.Format(this.Localize("Favorites.DeleteContents.Formattable"), item.Name), UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create(this.Localize("Finder.Delete"), UIAlertActionStyle.Destructive, action => {
                        try
                        {
                            if (item is DirectoryInfo directory) directory.Delete(true);
                            else item.Delete();

                            InvokeOnMainThread(() => {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                this.ShowAlert(this.Localize("Error.Delete"), exception.GetType().Name);
                            });
                        }
                    }));
                    var ok = UIAlertAction.Create(this.Localize("Global.CancelAction"), UIAlertActionStyle.Default, null);
                    alert.AddAction(ok);
                    alert.PreferredAction = ok;
                    PresentViewController(alert, true, null);
                });

                var actions = UISwipeActionsConfiguration.FromActions(new[] { delete, rename });
                actions.PerformsFirstActionWithFullSwipe = false;
                return actions;
            }

            return null;
        }

        #endregion

        #region IUIDocumentInteractionControllerDelegate

        [Export("documentInteractionControllerViewControllerForPreview:")]
        public UIViewController ViewControllerForPreview(UIDocumentInteractionController controller) => this;

        #endregion

        #region Bar Button Items

        private void ShowHelp(object sender, EventArgs e)
        {
            this.ShowAlert(this.Localize("Help.Favorites"), this.Localize("Help.ManageFavorites"));
        }

        private void NewFolder(object sender, EventArgs e)
        {
            this.CreatePrompt(this.Localize("Finder.NewFolderName"), this.Localize("Finder.NewFolderHere"), null, this.Localize("Finder.NewFolderPlaceholder"), this.Localize("Finder.CreateNewFolder"), this.Localize("Global.CancelAction"), text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowAlert(this.Localize("Finder.BadFolderName"), null);
                    return;
                }

                try
                {
                    var path = Path.Combine(directory.FullName, text);
                    Directory.CreateDirectory(path);

                    InvokeOnMainThread(() => {
                        RefreshDirectory(this, EventArgs.Empty);
                    });
                }
                catch (Exception exception)
                {
                    InvokeOnMainThread(() => {
                        this.ShowAlert(this.Localize("Error.NewFolder"), exception.Message);
                    });
                }
            });
        }

        private void ImportFiles(object sender, EventArgs e)
        {
            var picker = new UIDocumentPickerViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Import);
            picker.SetAllowsMultipleSelection(true);
            picker.SetShowFileExtensions();
            picker.Delegate = this;
            picker.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
            PresentViewController(picker, true, null);
        }

        #endregion

        #region IUIDocumentPickerDelegate

        [Export("documentPicker:didPickDocumentsAtURLs:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            var alert = UIAlertController.Create(this.Localize("Favorites.Importing"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() => {
                var fails = 0;
                foreach (var url in urls)
                {
                    try
                    {
                        var shouldRelease = url.StartAccessingSecurityScopedResource();

                        var filePath = Path.Combine(directory.FullName, Path.GetFileName(url.Path));
                        if (File.Exists(filePath)) File.Delete(filePath);
                        File.Copy(url.Path, filePath, false);
                        items.Add(new FileInfo(filePath));

                        if (shouldRelease) url.StopAccessingSecurityScopedResource();
                    }
                    catch (IOException)
                    {
                        fails += 1;
                    }
                }

                InvokeOnMainThread(() => {
                    DismissViewController(true, () => {
                        RefreshDirectory(this, EventArgs.Empty);
                        if (fails > 0) this.ShowAlert(string.Format(this.Localize("Error.Import.Formattable"), fails), null);
                    });
                });
            });
        }

        [Export("documentPicker:didPickDocumentAtURL:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
        {
            var alert = UIAlertController.Create(this.Localize("Favorites.Importing"), null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() => {
                var failed = false;
                try
                {
                    var shoudlRelease = url.StartAccessingSecurityScopedResource();

                    var filePath = Path.Combine(directory.FullName, Path.GetFileName(url.Path));
                    if (File.Exists(filePath)) File.Delete(filePath);
                    File.Copy(url.Path, filePath, false);
                    items.Add(new FileInfo(filePath));

                    if (shoudlRelease) url.StopAccessingSecurityScopedResource();
                }
                catch (IOException)
                {
                    failed = true;
                }


                InvokeOnMainThread(() => {
                    DismissViewController(true, () => {
                        RefreshDirectory(this, EventArgs.Empty);
                        if (failed) this.ShowAlert(this.Localize("Error.Import"), null);
                    });
                });
            });
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
    }
}
