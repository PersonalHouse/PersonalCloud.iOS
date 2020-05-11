using System;
using System.Collections.Generic;

using Foundation;

using NSPersonalCloud.Interfaces.Apps;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class WebAppsController : UITableViewController
    {
        public WebAppsController(IntPtr handle) : base(handle) { }

        private List<AppLauncher> apps;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshApps;
            apps = Globals.CloudManager.PersonalClouds[0].Apps;
        }

        #endregion

        #region TableView Data Source

        public override nint NumberOfSections(UITableView tableView) => 1;

        public override nint RowsInSection(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => apps?.Count ?? 0,
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override string TitleForFooter(UITableView tableView, nint section)
        {
            return (int) section switch
            {
                0 => apps?.Count > 0 ? null : this.Localize("Apps.NoWebApps"),
                _ => throw new ArgumentOutOfRangeException(nameof(section))
            };
        }

        public override UITableViewCell GetCell(UITableView tableView, NSIndexPath indexPath)
        {
            if (indexPath.Section == 0)
            {
                var cell = (BasicCell) tableView.DequeueReusableCell(BasicCell.Identifier, indexPath);
                var app = apps[indexPath.Row];
                cell.Update(app.Name, true);
                cell.Accessory = UITableViewCellAccessory.DisclosureIndicator;
                return cell;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        #region TableView Delegate

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 0)
            {
                var app = apps[indexPath.Row];
                var url = Globals.CloudManager.PersonalClouds[0].GetWebAppUri(app);
                UIApplication.SharedApplication.OpenUrl(NSUrl.FromString(url.AbsoluteUri), new NSDictionary(), null);
                return;
            }

            throw new ArgumentOutOfRangeException(nameof(indexPath));
        }

        #endregion

        private void RefreshApps(object sender, EventArgs args)
        {
            apps = Globals.CloudManager.PersonalClouds[0].Apps;
            TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
        }
    }
}