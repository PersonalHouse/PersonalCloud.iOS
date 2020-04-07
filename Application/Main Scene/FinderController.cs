using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using NSPersonalCloud;
using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;
using NSPersonalCloud.RootFS;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class FinderController : UITableViewController, IUIDocumentPickerDelegate, IUIDocumentInteractionControllerDelegate
    {
        public FinderController(IntPtr handle) : base(handle) { }

        private const string UploadSegue = "ChooseLocalFile";
        private const string MoveToSegue = "ChooseMoveDestination";

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

        public override void ViewWillAppear(bool animated)
        {
            base.ViewWillAppear(animated);
            if (refreshNow) RefreshDirectory(this, EventArgs.Empty);
        }

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            cloud.OnNodeChangedEvent += RefreshDevices;
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
                    chooser.NavigationTitle = "移动到……";
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
                0 => (workingPath.Length == 1 && (items?.Count ?? 0) == 0) ? "个人云内没有可访问设备" : null,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                var parentPath = Path.GetFileName(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar)).TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(parentPath)) cell.Update(UIImage.FromBundle("DirectoryBack"), "返回顶层", "后退至设备列表", null);
                else cell.Update(UIImage.FromBundle("DirectoryBack"), "返回上层", $"后退至“{parentPath}”", null);
                cell.Accessory = UITableViewCellAccessory.DetailButton;
                return cell;
            }

            if (indexPath.Section == 0)
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
            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                var pathString = string.Join(" » ", workingPath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries));
                this.ShowAlert("当前所在目录", pathString);
                return;
            }
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (workingPath.Length != 1 && indexPath.Section == 0 && indexPath.Row == 0)
            {
                workingPath = Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar));
                if (string.IsNullOrEmpty(workingPath)) workingPath = "/";
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
                    this.ShowAlert("恢复相册备份", $"“{item.Name}”是个人云相册备份文件，包含可供导入本机相册的照片或视频。" +
                        Environment.NewLine + Environment.NewLine +
                        "请轻扫并点击“收藏”以将此备份下载到本地，然后前往“本地收藏”恢复此备份。");
                    return;
                }

                var filePath = Path.Combine(PathHelpers.Cache, item.Name);
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
                    if (exception is HttpRequestException http) this.ShowAlert("与远程设备通讯时遇到问题", http.Message);
                    else this.ShowAlert("无法下载文件", exception.GetType().Name);
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
                    var download = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "收藏", (action, indexPath) => {
                        TableView.SetEditing(false, true);

                        var filePath = Path.Combine(PathHelpers.SharedContainer, item.Name);
                        PreparePlaceholder(item, filePath, url => {
                            this.ShowAlert("已收藏", $"“{item.Name}”已加入收藏夹。");
                        }, exception => {
                            if (exception is HttpRequestException http) this.ShowAlert("与远程设备通讯时遇到问题", http.Message);
                            else this.ShowAlert("无法下载文件", exception.GetType().Name);
                        });
                    });
                    download.BackgroundColor = Colors.OrangeFlag;
                    actions.Add(download);
                }

                if (item.IsReadOnly) return actions.ToArray();

                if (!item.Attributes.HasFlag(FileAttributes.Device))
                {
                    var move = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "移动", (action, indexPath) => {
                        TableView.SetEditing(false, true);
                        pendingMoveSource = Path.Combine(workingPath, item.Name);
                        PerformSegue(MoveToSegue, this);
                    });
                    move.BackgroundColor = Colors.BlueButton;
                    actions.Add(move);

                    var rename = UITableViewRowAction.Create(UITableViewRowActionStyle.Default, "重命名", (action, indexPath) => {
                        TableView.SetEditing(false, true);
                        RenameEntry(item);
                    });
                    rename.BackgroundColor = Colors.Indigo;
                    actions.Add(rename);
                }

                var delete = UITableViewRowAction.Create(UITableViewRowActionStyle.Destructive, "删除", (action, indexPath) => {
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

                var download = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "收藏", (action, view, handler) => {
                    handler?.Invoke(true);
                    PreparePlaceholder(item, Path.Combine(PathHelpers.SharedContainer, item.Name), url => {
                        (this).ShowAlert("已收藏", $"“{item.Name}”已加入收藏夹。");
                    }, exception => {
                        if (exception is HttpRequestException http) (this).ShowAlert("与远程设备通讯时遇到问题", http.Message);
                        else (this).ShowAlert("无法下载文件", exception.GetType().Name);
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
                    var move = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "移动", (action, view, handler) => {
                        handler?.Invoke(true);
                        pendingMoveSource = Path.Combine(workingPath, item.Name);
                        PerformSegue(MoveToSegue, this);
                    });
                    move.BackgroundColor = Colors.BlueButton;
                    actions.Add(move);

                    var rename = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Normal, "重命名", (action, view, handler) => {
                        handler?.Invoke(true);
                        RenameEntry(item);
                    });
                    rename.BackgroundColor = Colors.Indigo;
                    actions.Add(rename);
                }

                var delete = UIContextualAction.FromContextualActionStyle(UIContextualActionStyle.Destructive, "删除", (action, view, handler) => {
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

        private void GoHome(object sender, EventArgs args)
        {
            workingPath = "/";
            RefreshDirectory(this, EventArgs.Empty);
        }

        private void ShowHelp(object sender, EventArgs args)
        {
            this.ShowAlert("管理其它设备上的文件", "同一网络内其它设备上的个人云 App 运行时，您可以直接管理其共享的文件。" + Environment.NewLine + Environment.NewLine +
                "点击一台设备以查看其共享文件夹。左右轻扫文件或文件夹以执行删除等操作。使用屏幕顶部右侧的按钮创建新文件夹或上传新文件。" + Environment.NewLine + Environment.NewLine +
                "要管理此设备上本地收藏的文件，请前往“本地收藏”页或打开系统“文件” App；不推荐在此页面访问本设备上的共享文件夹。");
        }

        private void AddDeviceOrService(object sender, EventArgs args)
        {
            this.ShowAlert(Texts.FeatureUnavailable, Texts.FeatureUnavailableMessage);
        }

        private void CreateFolder(object sender, EventArgs args)
        {
            this.CreatePrompt("输入文件夹名称", "将在当前位置创建如下命名的子文件夹", null, "新文件夹", "创建", "取消", text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowAlert("文件夹名称无效", null);
                    return;
                }

                var alert = UIAlertController.Create("正在创建……", null, UIAlertControllerStyle.Alert);
                PresentViewController(alert, true, () => {
                    Task.Run(async () => {
                        try
                        {
                            var path = Path.Combine(workingPath, text);
                            await fileSystem.CreateDirectoryAsync(path).ConfigureAwait(false);

                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    RefreshDirectory(this, EventArgs.Empty);
                                });
                            });
                        }
                        catch (HttpRequestException exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("与远程设备通讯时遇到问题", exception.Message);
                                });
                            });

                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("无法创建文件夹", exception.GetType().Name);
                                });
                            });
                        }
                    });
                });
            });
        }

        private void UploadFile(object sender, EventArgs args)
        {
            var choices = UIAlertController.Create(null, null, UIAlertControllerStyle.ActionSheet);
            choices.AddAction(UIAlertAction.Create("上传本地收藏文件", UIAlertActionStyle.Default, action => {
                PerformSegue(UploadSegue, this);
            }));
            choices.AddAction(UIAlertAction.Create("上传其它 App 的文件", UIAlertActionStyle.Default, action => {
                var picker = new UIDocumentPickerViewController(new string[] { UTType.Data }, UIDocumentPickerMode.Import);
                picker.SetAllowsMultipleSelection(false);
                picker.SetShowFileExtensions();
                picker.Delegate = this;
                picker.ModalPresentationStyle = UIModalPresentationStyle.FullScreen;
                PresentViewController(picker, true, null);
            }));
            choices.AddAction(UIAlertAction.Create("返回", UIAlertActionStyle.Cancel, null));
            this.PresentActionSheet(choices, NavigationItem.RightBarButtonItem.UserInfoGetView());
        }

        #endregion

        #region Utility: Refresh

        private void RefreshDevices(object sender, EventArgs args)
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
                NavigationItem.Title = "个人云";
                NavigationItem.SetLeftBarButtonItem(HelpItem, true);
                NavigationItem.RightBarButtonItems = null;
                // NavigationItem.SetRightBarButtonItems(new[] { AddDeviceItem }, true);
            }
            else
            {
                NavigationItem.SetLeftBarButtonItem(HomeItem, true);
                NavigationItem.SetRightBarButtonItems(new[] { NewFileItem, NewFolderItem }, true);
            }

            if (!workingPath.EndsWith(Path.AltDirectorySeparatorChar)) workingPath += Path.AltDirectorySeparatorChar;

            var alert = UIAlertController.Create("正在加载……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    string title = null;
                    if (workingPath.Length != 1 && !string.IsNullOrEmpty(Path.GetDirectoryName(workingPath.TrimEnd(Path.AltDirectorySeparatorChar))))
                    {
                        title = workingPath.Substring(1, workingPath.IndexOf(Path.AltDirectorySeparatorChar));
                    }

                    try
                    {
                        var files = await fileSystem.EnumerateChildrenAsync(workingPath).ConfigureAwait(false);
                        items = files.Where(x => !x.Attributes.HasFlag(FileAttributes.Hidden) && !x.Attributes.HasFlag(FileAttributes.System)).ToList();
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                                TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                PresentViewController(CloudExceptions.Explain(exception), true, null);
                                items = null;
                                if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                                TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("无法打开文件夹", exception.GetType().Name);
                                items = null;
                                if (!string.IsNullOrEmpty(title)) NavigationItem.Title = title;
                                TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                            });
                        });
                    }
                });
            });
        }

        #endregion

        #region Utility: Download & Open

        private void PreparePlaceholder(FileSystemEntry item, string cachePath, Action<NSUrl> onCompletion, Action<Exception> onError)
        {
            if (File.Exists(cachePath))
            {
                var alert = UIAlertController.Create("替换本地同名文件？", $"本地收藏中已存在同名文件“{item.Name}”，收藏新文件将替换旧文件。" +
                    Environment.NewLine + Environment.NewLine +
                    "如果您想要同时保留新、旧收藏，请在本地收藏管理页面手动重命名冲突的文件。", UIAlertControllerStyle.Alert);
                alert.AddAction(UIAlertAction.Create("替换", UIAlertActionStyle.Cancel, action => {
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
                var ok = UIAlertAction.Create("开始下载", UIAlertActionStyle.Default, action => {
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
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var source = Path.Combine(workingPath, item.Name);
                        var target = new FileStream(cachePath, FileMode.CreateNew, FileAccess.ReadWrite, FileShare.None);
                        await (await fileSystem.ReadFileAsync(source).ConfigureAwait(false)).CopyToAsync(target).ConfigureAwait(false);
                        await target.DisposeAsync().ConfigureAwait(false);

                        var url = NSUrl.FromFilename(cachePath);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => onCompletion?.Invoke(url));
                        });
                    }
                    catch (Exception exception)
                    {
                        try { File.Delete(cachePath); }
                        catch { }

                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => onError?.Invoke(exception));
                        });
                    }
                });
            });
        }

        #endregion

        #region Utility: Rename & Delete

        private void RenameEntry(FileSystemEntry item)
        {
            this.CreatePrompt("输入新名称", $"即将重命名“{item.Name}”", item.Name, item.Name, "保存新名称", "取消", text => {
                if (string.IsNullOrWhiteSpace(text))
                {
                    this.ShowAlert("新名称无效", null);
                    return;
                }

                if (text == item.Name) return;

                var alert = UIAlertController.Create("正在重命名……", null, UIAlertControllerStyle.Alert);
                PresentViewController(alert, true, () => {
                    Task.Run(async () => {
                        try
                        {
                            var path = Path.Combine(workingPath, item.Name);
                            await fileSystem.RenameAsync(path, text).ConfigureAwait(false);

                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    RefreshDirectory(this, EventArgs.Empty);
                                });
                            });
                        }
                        catch (HttpRequestException exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    PresentViewController(CloudExceptions.Explain(exception), true, null);
                                });
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert("无法重命名此项目", exception.GetType().Name);
                                });
                            });
                        }
                    });
                });
            });
        }

        private void MoveFile(string destination)
        {
            if (string.IsNullOrEmpty(pendingMoveSource)) return;
            var alert = UIAlertController.Create("正在移动……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    try
                    {
                        var fileName = Path.GetFileName(pendingMoveSource);
                        var path = Path.Combine(destination, fileName);
                        await fileSystem.RenameAsync(pendingMoveSource, path).ConfigureAwait(false);
                        pendingMoveSource = null;

                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => RefreshDirectory(this, EventArgs.Empty));
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                PresentViewController(CloudExceptions.Explain(exception), true, null);
                            });
                        });
                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("无法完成移动", exception.GetType().Name);
                            });
                        });
                    }
                });
            });
        }

        private void DeleteEntry(FileSystemEntry item)
        {
            UIAlertController alert;
            if (item.Attributes.HasFlag(FileAttributes.Device))
            {
                alert = UIAlertController.Create("从个人云中移除此存储？", $"移除“{item.Name}”将同时删除已保存的连接和登录凭据。" +
                    Environment.NewLine + Environment.NewLine + "下次添加此存储时，您必须重新提供这些凭据。", UIAlertControllerStyle.Alert);
            }
            else
            {
                alert = UIAlertController.Create("删除此项目？", $"将从远程设备上删除“{item.Name}”。" +
                    Environment.NewLine + Environment.NewLine + "如果此项目是文件夹或包，其中的内容将被一同删除。", UIAlertControllerStyle.Alert);
            }
            alert.AddAction(UIAlertAction.Create("删除", UIAlertActionStyle.Destructive, action => {
                var progress = UIAlertController.Create("正在删除……", null, UIAlertControllerStyle.Alert);
                PresentViewController(progress, true, () => {
                    Task.Run(async () => {
                        try
                        {
                            var path = Path.Combine(workingPath, item.Name);
                            if (item.IsDirectory) path += Path.AltDirectorySeparatorChar;
                            await fileSystem.DeleteAsync(path).ConfigureAwait(false);

                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    items.Remove(item);
                                    TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
                                });
                            });
                        }
                        catch (HttpRequestException exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    PresentViewController(CloudExceptions.Explain(exception), true, null);
                                });
                            });
                        }
                        catch (Exception exception)
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
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
        }

        #endregion

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
            PresentViewController(alert, true, () => {
                Task.Run(async () => {
                    var shouldRelease = url.StartAccessingSecurityScopedResource();

                    try
                    {
                        var fileName = Path.GetFileName(url.Path);
                        using var stream = new FileStream(url.Path, FileMode.Open, FileAccess.Read, FileShare.Read);
                        var remotePath = Path.Combine(workingPath, fileName);
                        await fileSystem.WriteFileAsync(remotePath, stream).ConfigureAwait(false);
                        if (shouldRelease) url.StopAccessingSecurityScopedResource();

                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => RefreshDirectory(this, EventArgs.Empty));
                        });
                    }
                    catch (TaskCanceledException)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("无法上传文件", "等待远程设备响应过程中超时。");
                            });
                        });
                    }
                    catch (HttpRequestException exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                PresentViewController(CloudExceptions.Explain(exception), true, null);
                            });
                        });
                    }
                    catch (Exception exception)
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
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

    }
}
