using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class DeviceDirectoryController : UITableViewController, IUIDocumentInteractionControllerDelegate, IUIDocumentPickerDelegate
    {
        public DeviceDirectoryController(IntPtr handle) : base(handle) { }

        private const string UploadSegue = "ChooseLocalFiles";

        public RootFileSystem FileSystem { get; set; }
        public string DeviceName { get; set; }

        private string workingPath;
        private List<FileSystemEntry> items;
        private int depth;

        private string moveSource;
        private bool refreshNow;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            workingPath = Path.AltDirectorySeparatorChar + DeviceName;

            NavigationItem.Title = DeviceName;
            NavigationItem.RightBarButtonItem.Clicked += ShowUploadOptions;

            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshDirectory;
            RefreshDirectory(this, EventArgs.Empty);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            if (refreshNow) RefreshDirectory(this, EventArgs.Empty);
            refreshNow = false;
        }

        public override void PrepareForSegue(UIStoryboardSegue segue, NSObject sender)
        {
            base.PrepareForSegue(segue, sender);
            if (segue.Identifier == UploadSegue)
            {
                var navigation = (UINavigationController)segue.DestinationViewController;
                var chooser = (ChooseFileController)navigation.TopViewController;
                chooser.FileSystem = FileSystem;
                chooser.WorkingPath = workingPath;
                return;
            }
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => moveSource is null ? 1 : 2;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int)section switch
            {
                0 => (items?.Count ?? 0) + (depth == 0 ? 0 : 1),
                1 => moveSource is null ? throw new ArgumentOutOfRangeException(nameof(section)) : 1,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if ((items?.Count ?? 0) == 0 && section == 0) return "使用“+”按钮上传文件或新建文件夹。";
            if (moveSource != null && section == 1) return $"准备移动：{moveSource}";
            return null;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                var cell = (FileEntryCell)tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentPath}”", null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 0)
            {
                var cell = (FileEntryCell)tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory)
                {
                    cell.Update(item.Name, new UTI(UTType.Directory));
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

            if (moveSource != null && indexPath.Section == 1)
            {
                var cell = (BasicCell)tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update("移动到当前文件夹", Colors.BlueButton, true);
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
                var pathString = string.Join(" » ", workingPath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert("当前所在目录", pathString);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0)
            {
                workingPath = Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar));
                depth -= 1;
                RefreshDirectory(this, EventArgs.Empty);
                return;
            }

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory)
                {
                    workingPath = Path.Combine(workingPath, item.Name);
                    depth += 1;
                    RefreshDirectory(this, EventArgs.Empty);
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

                PreparePlaceholder(item, filePath, url =>
                {
                    this.PreviewFile(url);
                }, exception =>
                {
                    if (exception is HttpRequestException http) this.ShowAlert("与远程设备通讯时遇到问题", http.Message);
                    else this.ShowAlert("无法下载文件", exception.GetType().Name);
                });

                return;
            }

            if (moveSource != null && indexPath.Section == 1 && indexPath.Row == 0)
            {
                var alert = UIAlertController.Create("正在移动……", null, UIAlertControllerStyle.Alert);
                PresentViewController(alert, true, () =>
                {
                    Task.Run(async () =>
                    {
                        try
                        {
                            var fileName = Path.GetFileName(moveSource);
                            var path = Path.Combine(workingPath, fileName);
                            await FileSystem.RenameAsync(moveSource, path).ConfigureAwait(false);
                            moveSource = null;

                            InvokeOnMainThread(() =>
                            {
                                DismissViewController(true, () =>
                                {
                                    TableView.DeleteSections(new NSIndexSet(1), UITableViewRowAnimation.Automatic);
                                    RefreshDirectory(this, EventArgs.Empty);
                                });
                            });
                        }
                        catch (HttpRequestException exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                DismissViewController(true, () =>
                                {
                                    this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                });
                            });

                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() =>
                            {
                                DismissViewController(true, () =>
                                {
                                    this.ShowAlert("无法移动至当前文件夹", exception.GetType().Name);
                                });
                            });
                        }
                    });
                });

                return;
            }
        }

        // iOS 8.0+
        public override bool CanEditRow(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return false;
            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
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
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var actions = new List<UITableViewRowAction>(4);

                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (!item.IsDirectory)
                {
                    var download = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "收藏", (action, indexPath) =>
                    {
                        TableView.SetEditing(false, true);

                        var filePath = Path.Combine(Paths.Favorites, item.Name);
                        PreparePlaceholder(item, filePath, url =>
                        {
                            this.ShowAlert("已收藏", $"“{item.Name}”已加入收藏夹。");
                        }, exception =>
                        {
                            if (exception is HttpRequestException http) this.ShowAlert("与远程设备通讯时遇到问题", http.Message);
                            else this.ShowAlert("无法下载文件", exception.GetType().Name);
                        });
                    });
                    download.BackgroundColor = Colors.OrangeFlag;
                    actions.Add(download);
                }

                if (item.IsReadOnly) return actions.ToArray();

                var move = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "移动", (action, indexPath) =>
                {
                    TableView.SetEditing(false, true);
                    moveSource = Path.Combine(workingPath, item.Name);
                    TableView.InsertSections(new NSIndexSet(1), UITableViewRowAnimation.Automatic);
                });
                move.BackgroundColor = Colors.BlueButton;
                actions.Add(move);

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

                        var alert = UIAlertController.Create("正在重命名……", null, UIAlertControllerStyle.Alert);
                        PresentViewController(alert, true, () =>
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    var path = Path.Combine(workingPath, item.Name);
                                    await FileSystem.RenameAsync(path, text).ConfigureAwait(false);

                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            RefreshDirectory(this, EventArgs.Empty);
                                        });
                                    });
                                }
                                catch (HttpRequestException exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                        });
                                    });

                                }
                                catch (Exception exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                                        });
                                    });
                                }
                            });
                        });
                    });
                });
                rename.BackgroundColor = Colors.Indigo;
                actions.Add(rename);

                var delete = UITableViewRowAction.Create(UITableViewRowActionStyle.Destructive, "删除", (action, indexPath) =>
                {
                    TableView.SetEditing(false, true);

                    var alert = UIAlertController.Create("删除此项目？", $"将从远程设备上删除“{item.Name}”。" +
                        Environment.NewLine + Environment.NewLine + "如果此项目是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action =>
                    {
                        var progress = UIAlertController.Create("正在删除……", null, UIAlertControllerStyle.Alert);
                        PresentViewController(progress, true, () =>
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    var path = Path.Combine(workingPath, item.Name);
                                    if (item.IsDirectory) path += Path.AltDirectorySeparatorChar;
                                    await FileSystem.DeleteAsync(path).ConfigureAwait(false);

                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            items.Remove(item);
                                            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                                        });
                                    });
                                }
                                catch (HttpRequestException exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                        });
                                    });

                                }
                                catch (Exception exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("无法删除此项目", exception.GetType().Name);
                                        });
                                    });
                                }
                            });
                        });
                    }));
                    var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
                    alert.AddAction(ok);
                    alert.PreferredAction = ok;
                    PresentViewController(alert, true, null);
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
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsDirectory) return null;

                var download = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "收藏", (action, view, handler) =>
                {
                    handler?.Invoke(true);

                    var filePath = Path.Combine(Paths.Favorites, item.Name);
                    PreparePlaceholder(item, filePath, url =>
                    {
                        this.ShowAlert("已收藏", $"“{item.Name}”已加入收藏夹。");
                    }, exception =>
                    {
                        if (exception is HttpRequestException http) this.ShowAlert("与远程设备通讯时遇到问题", http.Message);
                        else this.ShowAlert("无法下载文件", exception.GetType().Name);
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
            if (indexPath.Section == 0 && indexPath.Row == 0 && depth != 0) return null;

            if (indexPath.Section == 0)
            {
                var item = items[depth == 0 ? indexPath.Row : (indexPath.Row - 1)];
                if (item.IsReadOnly) return null;

                var move = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "移动", (action, view, handler) =>
                {
                    handler?.Invoke(true);

                    moveSource = Path.Combine(workingPath, item.Name);
                    tableView.InsertSections(new NSIndexSet(1), UITableViewRowAnimation.Automatic);
                });
                move.BackgroundColor = Colors.BlueButton;

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

                        var alert = UIAlertController.Create("正在重命名……", null, UIAlertControllerStyle.Alert);
                        PresentViewController(alert, true, () =>
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    var path = Path.Combine(workingPath, item.Name);
                                    await FileSystem.RenameAsync(path, text).ConfigureAwait(false);

                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            RefreshDirectory(this, EventArgs.Empty);
                                        });
                                    });
                                }
                                catch (HttpRequestException exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                        });
                                    });

                                }
                                catch (Exception exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                                        });
                                    });
                                }
                            });
                        });
                    });
                });
                rename.BackgroundColor = Colors.Indigo;

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, "删除", (action, view, handler) =>
                {
                    handler?.Invoke(true);

                    var alert = UIAlertController.Create("删除此项目？", $"将从远程设备上删除“{item.Name}”。"
                    + Environment.NewLine + Environment.NewLine + "如果此项目是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
                    alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action =>
                    {
                        var progress = UIAlertController.Create("正在删除……", null, UIAlertControllerStyle.Alert);
                        PresentViewController(progress, true, () =>
                        {
                            Task.Run(async () =>
                            {
                                try
                                {
                                    var path = Path.Combine(workingPath, item.Name);
                                    if (item.IsDirectory) path += Path.AltDirectorySeparatorChar;
                                    await FileSystem.DeleteAsync(path).ConfigureAwait(false);

                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            items.Remove(item);
                                            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                                        });
                                    });
                                }
                                catch (HttpRequestException exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                        });
                                    });

                                }
                                catch (Exception exception)
                                {
                                    InvokeOnMainThread(() =>
                                    {
                                        DismissViewController(true, () =>
                                        {
                                            this.ShowAlert("无法删除此项目", exception.GetType().Name);
                                        });
                                    });
                                }
                            });
                        });
                    }));
                    var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
                    alert.AddAction(ok);
                    alert.PreferredAction = ok;
                    PresentViewController(alert, true, null);
                });

                var actions = UISwipeActionsConfiguration.FromActions(new[] { delete, rename, move });
                actions.PerformsFirstActionWithFullSwipe = false;
                return actions;
            }

            return null;
        }

        #endregion

        #region Bar Button Items

        private void ShowUploadOptions(object sender, EventArgs e)
        {
            var alert = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
            alert.AddAction(UIAlertAction.Create("上传本地收藏文件", UIAlertActionStyle.Default, action =>
            {
                PerformSegue(UploadSegue, this);
                refreshNow = true;
            }));
            alert.AddAction(UIAlertAction.Create("上传其它 App 的文件", UIAlertActionStyle.Default, action =>
            {
                var picker = new UIDocumentPickerViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Import);
                picker.SetAllowsMultipleSelection(false);
                picker.SetShowFileExtensions();
                picker.Delegate = this;
                picker.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                PresentViewController(picker, true, null);
            }));
            alert.AddAction(UIAlertAction.Create("返回", UIAlertActionStyle.Cancel, null));
            this.PresentActionSheet(alert, NavigationItem.RightBarButtonItem.UserInfoGetView());
        }

        #endregion

        #region IUIDocumentPickerDelegate

        // iOS 11.0+
        [Export("documentPicker:didPickDocumentsAtURLs:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl[] urls)
        {
            if (urls.Length != 1)
            {
                this.ShowAlert("仅支持单文件上传", "当前版本只能同时上传 1 个文件。");
                return;
            }

            var url = urls[0];
            if (!url.IsFileUrl)
            {
                this.ShowAlert("不支持此共享方式", "来源 App 正在使用特殊方式交换此文件，个人云无法访问文件数据。");
                return;
            }

            UploadFileAt(url);
        }

        // iOS 8.0+
        [Export("documentPicker:didPickDocumentAtURL:")]
        public void DidPickDocument(UIDocumentPickerViewController controller, NSUrl url)
        {
            UploadFileAt(url);
        }

        private void UploadFileAt(NSUrl url)
        {
            var alert = UIAlertController.Create("正在上传……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () =>
            {
                Task.Run(async () =>
                {
                    var shouldRelease = url.StartAccessingSecurityScopedResource();

                    try
                    {
                        var fileName = Path.GetFileName(url.Path);
                        using var stream = new FileStream(url.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var remotePath = Path.Combine(workingPath, fileName);
                        await FileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                        if (shouldRelease) url.StopAccessingSecurityScopedResource();

                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () => RefreshDirectory(this, EventArgs.Empty));
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () =>
                            {
                                this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                            });
                        });

                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () =>
                            {
                                this.ShowAlert("无法上传此文件", exception.GetType().Name);
                            });
                        });
                    }
                });
            });
        }

        #endregion

        #region IUIDocumentInteractionControllerDelegate

        [Export("documentInteractionControllerViewControllerForPreview:")]
        public UIViewController ViewControllerForPreview(UIDocumentInteractionController controller) => this;

        #endregion

        private void RefreshDirectory(object sender, EventArgs e)
        {
            if (RefreshControl.Refreshing) RefreshControl.EndRefreshing();

            var alert = UIAlertController.Create("正在加载……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var files = await FileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                        items = files.Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden) && !x.Attributes.HasFlag(FileAttributes.System)).ToList();
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () =>
                            {
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 1)), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        if (exception.Message.StartsWith("429"))
                        {
                            InvokeOnMainThread(() =>
                            {
                                DismissViewController(true, () =>
                                {
                                    this.ShowAlert("远程设备忙", "此文件夹内容过多，无法在限定时间内收集内容详情。请稍后查看。");
                                    items = null;
                                    TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 1)), UITableViewRowAnimation.Automatic);
                                });
                            });
                            return;
                        }

                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () =>
                            {
                                this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                items = null;
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 1)), UITableViewRowAnimation.Automatic);
                            });
                        });

                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () =>
                            {
                                this.ShowAlert("无法打开文件夹", exception.GetType().Name);
                                items = null;
                                TableView.ReloadSections(NSIndexSet.FromNSRange(new NSRange(0, 1)), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                });
            });
        }

        private void PreparePlaceholder(FileSystemEntry item, string cachePath, Action<NSUrl> onCompletion, Action<Exception> onError)
        {
            if (File.Exists(cachePath))
            {
                var alert = UIAlertController.Create("替换本地同名文件？", $"本地收藏中已存在同名文件“{item.Name}”，收藏新文件将替换旧文件。" +
                    Environment.NewLine + Environment.NewLine +
                    "如果您想要同时保留新、旧收藏，请在本地收藏管理页面手动重命名冲突的文件。", UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("替换", UIAlertActionStyle.Cancel, action =>
                {
                    try { File.Delete(cachePath); }
                    catch { }
                    PrepareConnection(item, cachePath, onCompletion, onError);
                }));
                var ok = UIAlertAction.Create("取消", UIAlertActionStyle.Default, null);
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
                var alert = UIAlertController.Create("立即下载此文件？", "此文件尚未下载并且大小可能超过 100 MB，下载将需要一段时间。", UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("取消", UIAlertActionStyle.Cancel, null));
                var ok = UIAlertAction.Create("开始下载", UIAlertActionStyle.Default, action =>
                {
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
                this.ShowAlert("无法下载文件", "文件访问冲突，请重试。");
                return;
            }

            var alert = UIAlertController.Create("正在下载……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () =>
            {
                Task.Run(async () =>
                {
                    try
                    {
                        var source = Path.Combine(workingPath, item.Name);
                        var target = new FileStream(cachePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                        await (await FileSystem.ReadFileAsync(source).ConfigureAwait(false)).CopyToAsync(target).ConfigureAwait(false);
                        await target.DisposeAsync().ConfigureAwait(false);

                        var url = NSUrl.FromFilename(cachePath);
                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () => onCompletion?.Invoke(url));
                        });
                    }
                    catch (Exception exception)
                    {
                        try { File.Delete(cachePath); }
                        catch { }

                        InvokeOnMainThread(() =>
                        {
                            DismissViewController(true, () => onError?.Invoke(exception));
                        });
                    }
                });
            });
        }
    }
}
