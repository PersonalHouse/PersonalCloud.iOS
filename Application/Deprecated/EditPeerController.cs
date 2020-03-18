using System;
using System.Globalization;
using System.Net;

using Foundation;

using UIKit;

using Unishare.Apps.Common.Data;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class EditPeerController : UITableViewController
    {
        public EditPeerController(IntPtr handle) : base(handle) { }

        public DeviceModel Device { get; set; }

        private bool hasUnsavedChanges;
        private Guid oldKey;
        private UIGestureRecognizer recognizer;
        private NSObject keyboardShownObserver;
        private NSObject keyboardHiddenObserver;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();

            NavigationItem.LeftBarButtonItem.Clicked += Discard;
            NavigationItem.RightBarButtonItem.Clicked += Submit;

            recognizer = new UITapGestureRecognizer(() => View.EndEditing(false));
            View.AddGestureRecognizer(recognizer);

            if (Device is null) Device = new DeviceModel { Id = Guid.NewGuid() };
            else oldKey = Device.Id;

            keyboardShownObserver = UIKeyboard.Notifications.ObserveDidShow((o, e) => {
                NavigationItem.LeftBarButtonItem.Enabled = false;
                NavigationItem.RightBarButtonItem.Enabled = false;
            });
            keyboardHiddenObserver = UIKeyboard.Notifications.ObserveDidHide((o, e) => {
                NavigationItem.LeftBarButtonItem.Enabled = true;
                NavigationItem.RightBarButtonItem.Enabled = true;
            });
        }

        public override void ViewWillDisappear(bool animated)
        {
            NSNotificationCenter.DefaultCenter.RemoveObservers(new[] { keyboardShownObserver, keyboardHiddenObserver });
            View.RemoveGestureRecognizer(recognizer);
            base.ViewWillDisappear(animated);
        }

        #endregion

        #region UITableView DataSource

        public override nint NumberOfSections(UITableView tableView) => 2;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            switch (section)
            {
                case 0: return 1;
                case 1: return 2;
                default: throw new ArgumentOutOfRangeException(nameof(section));
            }
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
            if (indexPath.Section == 0)
            {
                cell.Update("名称", "例如：笔记本电脑", ValidateName, Device.Name);
                return cell;
            }
            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                cell.Update("IP 地址", "例如：192.168.1.1", ValidateIP, Device.Host, UIKeyboardType.NumbersAndPunctuation);
                return cell;
            }
            if (indexPath.Section == 1 && indexPath.Row == 1)
            {
                cell.Update("连接端口", "例如：61570", ValidatePort,
                    Device.Port == 0 ? null : Device.Port.ToString(CultureInfo.InvariantCulture),
                    UIKeyboardType.AsciiCapableNumberPad);
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private bool ValidateIP(UITextField textField)
        {
            var text = textField.Text;
            if (IPAddress.TryParse(text, out var ip))
            {
                hasUnsavedChanges = true;

                var address = ip.ToString();
                textField.Text = address;
                if (string.IsNullOrEmpty(Device.Name)) Device.Name = address;
                Device.Host = address;
                return true;
            }

            this.ShowAlert("IP 地址格式无效", "请输入有效的 IP (IPv4) 或 IPv6 地址");
            return false;
        }

        private bool ValidatePort(UITextField textField)
        {
            var text = textField.Text;
            if (int.TryParse(text, out var port) && port >= IPEndPoint.MinPort && port <= IPEndPoint.MaxPort)
            {
                hasUnsavedChanges = true;

                Device.Port = port;
                return true;
            }

            this.ShowAlert("连接端口无效", "请输入有效的网络连接端口");
            return false;
        }

        private bool ValidateName(UITextField textField)
        {
            hasUnsavedChanges = true;
            Device.Name = textField.Text;
            return true;
        }

        private void Discard(object sender, EventArgs e)
        {
            if (hasUnsavedChanges) this.ShowDiscardConfirmation();
            else NavigationController.DismissViewController(true, null);
        }

        private void Submit(object sender, EventArgs e)
        {
            if (oldKey != null) Globals.Database.Delete<DeviceModel>(oldKey);

            /*
            if (Globals.Instance.Database.Find<DeviceModel>(Device.Host) != null)
            {

            }
            */

            var result = Globals.Database.Insert(Device);
            if (result == 1) NavigationController.DismissViewController(true, null);
            else this.ShowAlert("保存失败", "App 内部错误，请检查输入后重试。");
        }
    }
}
