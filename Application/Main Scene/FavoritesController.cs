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
    public partial class FavoritesController : UITableViewController, IUIDocumentInteractionControllerDelegate
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
            directory = new DirectoryInfo(PathHelpers.SharedContainer);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            directory.Refresh();
            RefreshDirectory(this, EventArgs.Empty);
        }

        #endregion

        #region TableView DataSource

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
                0 => (items?.Count ?? 0) == 0 ? "如需了解详情，请使用“帮助”按钮。" : null,
                _ => throw new ArgumentNullException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentName = depth == 1 ? "本地收藏" : directory.Parent.Name;
                cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentName}”", null);
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

        public override void AccessoryButtonTapped(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var pathString = string.Join(" » ", directory.FullName.Replace(PathHelpers.SharedContainer, @"本地收藏/").Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
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

                var url = NSUrl.FromFilename(item.FullName);
                this.PreviewFile(url);
                return;
            }
        }

        /*
        public override UISwipeActionsConfiguration GetLeadingSwipeActionsConfiguration(UITableView tableView, NSIndexPath indexPath)
        {
            var upload = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, "同步", (action, view, handler) => {
                handler?.Invoke(true);

                this.ShowAlert(Texts.FeatureUnavailable, Texts.FeatureUnavailableMessage);
                return;

                try
                {
                    var path = items[indexPath.Row].FullName;
                    File.Delete(path);

                    InvokeOnMainThread(() => {
                        items.RemoveAt(indexPath.Row);
                        TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                    });
                }
                catch (Exception exception)
                {
                    InvokeOnMainThread(() => {
                        this.ShowAlert("无法删除此项目", exception.GetType().Name);
                    });
                }
            });
            upload.BackgroundColor = Colors.OrangeFlag;

            var actions = UISwipeActionsConfiguration.FromActions(new[] { upload });
            actions.PerformsFirstActionWithFullSwipe = false;
            return actions;
        }
        */

        public override UISwipeActionsConfiguration GetTrailingSwipeActionsConfiguration(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];

                var rename = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "重命名", (action, view, handler) => {
                    handler?.Invoke(true);

                    this.CreatePrompt("输入新名称", $"即将重命名“{item.Name}”", item.Name, item.Name, "保存新名称", "取消", text => {
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
                                this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                            });
                        }
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, "删除", (action, view, handler) => {
                    handler?.Invoke(true);

                    var alert = UIAlertController.Create("删除此项收藏？", $"将从本地收藏中删除“{item.Name}”。"
                        + Environment.NewLine + Environment.NewLine + "如果此项收藏是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action => {
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

        private void ShowHelp(object sender, EventArgs e)
        {
            this.ShowAlert("使用本地收藏管理共享文件", "查看个人云中其它设备上的文件时，您可以向右轻扫某个文件将其“收藏”，此文件将被保存至本地收藏。" +
                Environment.NewLine + Environment.NewLine +
                "本地收藏存储在这台设备上，使您可以在不连接其它设备的情况下查看和编辑。" +
                Environment.NewLine + Environment.NewLine +
                "连接个人云中其它设备后，本地收藏的文件将在其它设备上可见，您也可以将文件手动上传至其它设备。");
        }

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

        #region Import

        private void ImportFiles(object sender, EventArgs e)
        {
            var picker = new UIDocumentPickerViewController(new string[] { UTType.Content }, UIDocumentPickerMode.Open) {
                AllowsMultipleSelection = true
            };
            picker.DidPickDocumentAtUrls += OnDocumentsPicked;
            PresentViewController(picker, true, null);
        }

        private void OnDocumentsPicked(object sender, UIDocumentPickedAtUrlsEventArgs e)
        {
            var alert = UIAlertController.Create("正在导入……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, null);

            Task.Run(() => {
                var fails = 0;
                foreach (var url in e.Urls)
                {
                    try
                    {
                        if (url.StartAccessingSecurityScopedResource())
                        {
                            var filePath = Path.Combine(directory.FullName, Path.GetFileName(url.Path));
                            if (File.Exists(filePath))
                            {
                                File.Delete(filePath);
                            }

                            File.Copy(url.Path, filePath, false);
                            url.StopAccessingSecurityScopedResource();

                            items.Add(new FileInfo(filePath));
                        }
                        else fails += 1;
                    }
                    catch (IOException)
                    {
                        fails += 1;
                    }
                }

                InvokeOnMainThread(() => {
                    DismissViewController(true, () => {
                        RefreshDirectory(this, EventArgs.Empty);
                        if (fails > 0) this.ShowAlert($"{fails} 个文件导入失败", null);
                    });
                });
            });
        }

        #endregion

        private void NewFolder(object sender, EventArgs e)
        {
            this.CreatePrompt("输入文件夹名称", "将在当前文件夹下创建如下命名的子文件夹", null, "新建文件夹", "创建", "取消", text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowAlert("文件夹名称无效", null);
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
                        this.ShowAlert("无法创建文件夹", exception.Message);
                    });
                }
            });
        }
    }
}
