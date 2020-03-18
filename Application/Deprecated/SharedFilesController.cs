using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;
using QuickLook;
using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class SharedFilesController : UITableViewController, IQLPreviewControllerDataSource, IQLPreviewControllerDelegate
    {
        public SharedFilesController(IntPtr handle) : base(handle) { }

        private List<FileSystemInfo> items;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            NavigationItem.RightBarButtonItem = EditButtonItem;

            var directory = new DirectoryInfo(PathHelpers.SharedContainer);
            items = directory.EnumerateFileSystemInfos().ToList();
        }

        public override void SetEditing(bool editing, bool animated)
        {
            base.SetEditing(editing, animated);
            TableView.ReloadSections(NSIndexSet.FromIndex(0), UITableViewRowAnimation.Automatic);
        }

        #endregion

        #region UITableView DataSource

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
            => tableView.Editing ? items.Count + 1 : items.Count;

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (Editing) return "点击“+”来导入其它 App 的文件。您的设备可能需要安装官方“文件” App。";
            if (items.Count == 0) return "使用“编辑”按钮添加文件。";
            return null;
        }

        public override UITableViewCellEditingStyle EditingStyleForRow(UITableView tableView, NSIndexPath indexPath)
        {
            return indexPath.Row < items.Count ? UITableViewCellEditingStyle.Delete : UITableViewCellEditingStyle.Insert;
        }

        public override void CommitEditingStyle(UITableView tableView, UITableViewCellEditingStyle editingStyle, NSIndexPath indexPath)
        {
            if (editingStyle == UITableViewCellEditingStyle.Insert)
            {
                ImportSharedFiles();
                return;
            }

            if (editingStyle == UITableViewCellEditingStyle.Delete)
            {
                var item = items[indexPath.Row];
                Task.Run(() => {
                    try
                    {
                        item.Delete();
                        items.Remove(item);

                        InvokeOnMainThread(() => {
                            tableView.DeleteRows(new[] { indexPath }, UITableViewRowAnimation.Automatic);
                        });
                    }
                    catch (IOException)
                    {
                        InvokeOnMainThread(() => {
                            this.ShowAlert("删除本地文件失败", null);
                        });
                    }
                });
            }
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row < items.Count)
            {
                var item = items[indexPath.Row];
                var cell = (FileEntryCell) tableView.DequeueReusableCell(FileEntryCell.Identifier, indexPath);
                if (item.Attributes.HasFlag(FileAttributes.Directory))
                {
                    cell.Update(item.Name, new UTI(UTType.Directory));
                    return cell;
                }
                else
                {
                    var fileInfo = new FileInfo(item.FullName);
                    cell.Update(fileInfo.Name, fileInfo.Length);
                    return cell;
                }
            }

            if (indexPath.Section == 0 && tableView.Editing)
            {
                var cell = (KeyValueCell) tableView.DequeueReusableCell(KeyValueCell.Identifier, indexPath);
                cell.Update("导入文件", null);
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            var preview = new QLPreviewController {
                DataSource = this,
                Delegate = this,
                CurrentPreviewItemIndex = indexPath.Row
            };
            PresentViewController(preview, true, null);
        }

        #endregion

        #region QLPreview

        public nint PreviewItemCount(QLPreviewController controller) => items.Count;

        public IQLPreviewItem GetPreviewItem(QLPreviewController controller, nint index)
        {
            var path = items[(int) index].FullName;
            var url = NSUrl.FromFilename(path);
            return url;
        }

        // Todo: Delegate.

        #endregion

        private void ImportSharedFiles()
        {
            var picker = new UIDocumentPickerViewController(new string[] { UTType.Content }, UIDocumentPickerMode.Open) {
                AllowsMultipleSelection = true
            };
            picker.DidPickDocumentAtUrls += OnDocumentsPicked;
            picker.WasCancelled += OnPickerCancelled;
            PresentViewController(picker, true, null);
        }

        private void OnPickerCancelled(object sender, EventArgs e)
        {
            this.ShowAlert("您没有导入任何新文件", null);
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
                            var filePath = Path.Combine(PathHelpers.SharedContainer, Path.GetFileName(url.Path));
                            if (File.Exists(filePath)) File.Delete(filePath);

                            File.Copy(url.Path, filePath, false);
                            url.StopAccessingSecurityScopedResource();

                            items.Add(new FileInfo(filePath));
                            InvokeOnMainThread(() => {
                                TableView.BeginUpdates();
                                TableView.InsertRows(new[] { NSIndexPath.FromRowSection(items.Count - 1, 0) }, UITableViewRowAnimation.Automatic);
                                TableView.EndUpdates();
                            });
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
                        if (fails > 0) this.ShowAlert($"{fails} 个文件导入失败", null);
                    });
                });
            });
        }
    }
}
