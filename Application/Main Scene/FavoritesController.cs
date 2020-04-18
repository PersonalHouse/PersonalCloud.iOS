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
            return (int)section switch
            {
                0 => (items?.Count ?? 0) + (depth == 0 ? 0 : 1),
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int)section switch
            {
                0 => (items?.Count ?? 0) == 0 ? "如需了解详情，请使用“帮助”按钮。" : null,
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var cell = (FileEntryCell)tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentName = depth == 1 ? "本地收藏" : directory.Parent.Name;
                cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentName}”", null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                var cell = (FileEntryCell)tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
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
                var pathString = string.Join(" » ", directory.FullName.Replace(Paths.Favorites, @"本地收藏/").Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
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

                if (Path.GetExtension(item.FullName)?.ToUpperInvariant() == ".PLASSET")
                {
                    var alert = UIAlertController.Create("恢复相册备份？", $"“{item.Name}”是个人云相册备份文件，包含可供导入本机相册的照片或视频。"
                                                         + Environment.NewLine + Environment.NewLine
                                                         + "您可以立即恢复此备份，相册中目前所有照片均不会受到影响。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null));
                    var restore = UIAlertAction.Create("恢复备份", UIAlertActionStyle.Default, action =>
                    {
                        Task.Run(() =>
                        {
                            SinglePhotoPackage.RestoreFromArchive(item.FullName, () =>
                            {
                                InvokeOnMainThread(() =>
                                {
                                    var completionAlert = UIAlertController.Create("相册备份恢复成功", $"“{item.Name}”已导入本机相册。", UIAlertControllerStyle.Alert);
                                    completionAlert.AddAction(UIAlertAction.Create("删除备份", UIAlertActionStyle.Destructive, action =>
                                    {
                                        try { item.Delete(); }
                                        catch { }
                                        RefreshDirectory(this, EventArgs.Empty);
                                    }));
                                    var ok = UIAlertAction.Create("好", UIAlertActionStyle.Default, null);
                                    completionAlert.AddAction(ok);
                                    completionAlert.PreferredAction = ok;
                                    PresentViewController(completionAlert, true, null);
                                });
                            }, error =>
                            {
                                InvokeOnMainThread(() =>
                                {
                                    this.ShowAlert("相册备份恢复失败", error?.LocalizedDescription ?? "未能完成操作，因为发生了未知错误。");
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

                var rename = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "重命名", (action, indexPath) =>
                {
                    TableView.SetEditing(false, true);

                    this.CreatePrompt("输入新名称", $"即将重命名“{item.Name}”", item.Name, item.Name, "保存新名称", "取消", text =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            this.ShowAlert("新名称无效", null);
                            return;
                        }

                        if (text == item.Name) return;

                        try
                        {
                            if (item is FileInfo file) file.MoveTo(Path.Combine(Path.GetDirectoryName(file.FullName), text));
                            else
                            {
                                var directory = (DirectoryInfo)item;
                                directory.MoveTo(Path.Combine(directory.Parent.FullName, text));
                            }

                            InvokeOnMainThread(() =>
                            {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                            });
                        }
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UITableViewRowAction.Create(UITableViewRowActionStyle.Destructive, "删除", (action, indexPath) =>
                {
                    TableView.SetEditing(false, true);

                    var alert = UIAlertController.Create("删除此项收藏？", $"将从本地收藏中删除“{item.Name}”。"
                        + Environment.NewLine + Environment.NewLine + "如果此项收藏是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action =>
                    {
                        try
                        {
                            if (item is DirectoryInfo directory) directory.Delete(true);
                            else item.Delete();

                            InvokeOnMainThread(() =>
                            {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                this.ShowAlert("无法删除此项目", exception.GetType().Name);
                            });
                        }
                    }));
                    var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
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

                var rename = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "重命名", (action, view, handler) =>
                {
                    handler?.Invoke(true);

                    this.CreatePrompt("输入新名称", $"即将重命名“{item.Name}”", item.Name, item.Name, "保存新名称", "取消", text =>
                    {
                        if (string.IsNullOrWhiteSpace(text))
                        {
                            this.ShowAlert("新名称无效", null);
                            return;
                        }

                        if (text == item.Name) return;

                        try
                        {
                            if (item is FileInfo file) file.MoveTo(Path.Combine(Path.GetDirectoryName(file.FullName), text));
                            else
                            {
                                var directory = (DirectoryInfo)item;
                                directory.MoveTo(Path.Combine(directory.Parent.FullName, text));
                            }

                            InvokeOnMainThread(() =>
                            {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                            });
                        }
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, "删除", (action, view, handler) =>
                {
                    handler?.Invoke(true);

                    var alert = UIAlertController.Create("删除此项收藏？", $"将从本地收藏中删除“{item.Name}”。"
                        + Environment.NewLine + Environment.NewLine + "如果此项收藏是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action =>
                    {
                        try
                        {
                            if (item is DirectoryInfo directory) directory.Delete(true);
                            else item.Delete();

                            InvokeOnMainThread(() =>
                            {
                                RefreshDirectory(this, EventArgs.Empty);
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                this.ShowAlert("无法删除此项目", exception.GetType().Name);
                            });
                        }
                    }));
                    var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
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
            this.ShowAlert("使用本地收藏管理共享文件", "查看个人云中其它设备上的文件时，您可以向右轻扫某个文件将其“收藏”，此文件将被保存至本地收藏。" +
                Environment.NewLine + Environment.NewLine +
                "本地收藏存储在这台设备上，使您可以在不连接其它设备的情况下查看和编辑。" +
                Environment.NewLine + Environment.NewLine +
                "连接个人云中其它设备后，本地收藏的文件将在其它设备上可见，您也可以将文件手动上传至其它设备。");
        }

        private void NewFolder(object sender, EventArgs e)
        {
            this.CreatePrompt("输入文件夹名称", "将在当前文件夹下创建如下命名的子文件夹", null, "新建文件夹", "创建", "取消", text =>
            {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowAlert("文件夹名称无效", null);
                    return;
                }

                try
                {
                    var path = Path.Combine(directory.FullName, text);
                    Directory.CreateDirectory(path);

                    InvokeOnMainThread(() =>
                    {
                        RefreshDirectory(this, EventArgs.Empty);
                    });
                }
                catch (Exception exception)
                {
                    InvokeOnMainThread(() =>
                    {
                        this.ShowAlert("无法创建文件夹", exception.Message);
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
            var alert = UIAlertController.Create("正在导入……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() =>
            {
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

                InvokeOnMainThread(() =>
                {
                    DismissViewController(true, () =>
                    {
                        RefreshDirectory(this, EventArgs.Empty);
                        if (fails > 0) this.ShowAlert($"{fails} 个文件导入失败", null);
                    });
                });
            });
        }

        [Export("documentPicker:didPickDocumentAtURL:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
        {
            var alert = UIAlertController.Create("正在导入……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() =>
            {
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


                InvokeOnMainThread(() =>
                {
                    DismissViewController(true, () =>
                    {
                        RefreshDirectory(this, EventArgs.Empty);
                        if (failed) this.ShowAlert("文件导入失败", null);
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
                this.ShowAlert("无法查看此文件夹", "此文件夹已被删除或内容异常。");
            }

            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();
            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
        }
    }
}
