using System;

using Foundation;

using NSPersonalCloud;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class RenamerController : UITableViewController
    {
        public RenamerController(IntPtr handle) : base(handle) { }

        private string deviceName;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            deviceName = Globals.CloudManager.PersonalClouds[0].NodeDisplayName;
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => 2,
                _ => throw new ArgumentOutOfRangeException(nameof(section)),
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            if (section != 0) return null;
            return Texts.DeviceNameHint;
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0 && indexPath.Row == 0)
            {
                var cell = (TitleEditorCell) tableView.DequeueReusableCell(TitleEditorCell.Identifier, indexPath);
                cell.Update(Texts.DeviceName, Texts.DeviceNamePlaceholder, UpdateName, deviceName);
                return cell;
            }

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                cell.Update("保存新名称", Colors.BlueButton, true);
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        #region TableView Delegate

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0 && indexPath.Row == 1)
            {
                var invalidCharHit = false;
                foreach (var character in VirtualFileSystem.InvalidCharacters)
                {
                    if (deviceName?.Contains(character) == true) invalidCharHit = true;
                }
                if (string.IsNullOrWhiteSpace(deviceName) || invalidCharHit)
                {
                    this.ShowAlert(Texts.InvalidDeviceName, Texts.InvalidDeviceNameMessage);
                    return;
                }

                var cloud = Globals.CloudManager.PersonalClouds[0];
                cloud.NodeDisplayName = deviceName;
                Globals.Database.SaveSetting(UserSettings.DeviceName, deviceName);
                try
                {
                    Globals.CloudManager.NetworkRefeshNodes();
                }
                catch
                {
                    // Ignored.
                }
                NavigationController.PopViewController(true);
            }
        }

        #endregion

        private void UpdateName(UITextField textField) => deviceName = textField.Text;
    }
}
