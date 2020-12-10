using System;
using System.Collections.Generic;

using Foundation;

using NSPersonalCloud.Interfaces.Apps;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class WebAppsController : UITableViewController
    {
        public WebAppsController(IntPtr handle) : base(handle) { }

        private List<AppLauncher> apps;

        public override void ViewDidAppear(bool animated)
        {
            base.ViewDidAppear(animated);
            var cloud = Globals.CloudManager.PersonalClouds[0];
            if (cloud!=null)
            {
                cloud.OnNodeChangedEvent += RefreshAppsInternal;
                RefreshAppsInternal(this, null);
            }
        }

        public override void ViewDidDisappear(bool animated)
        {
            base.ViewDidDisappear(animated);
            var cloud = Globals.CloudManager.PersonalClouds[0];
            if (cloud != null)
            {
                cloud.OnNodeChangedEvent -= RefreshAppsInternal;
            }
        }

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            RefreshControl = new UIRefreshControl();
            RefreshControl.ValueChanged += RefreshApps;
            apps = Globals.CloudManager?.PersonalClouds[0]?.Apps;
            if (apps == null)
            {
                apps = new List<AppLauncher>();
            }
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

        private void RefreshApps(object sender, EventArgs e)
        {
            RefreshAppsInternal(sender, e);
            RefreshControl.EndRefreshing();
        }
        private void RefreshAppsInternal(object sender, EventArgs e)
        {
            apps = Globals.CloudManager?.PersonalClouds[0]?.Apps;
            if (apps == null)
            {
                apps = new List<AppLauncher>();
            }
            InvokeOnMainThread(() => {
                TableView.ReloadData();
                //TableView.ReloadSections(new NSIndexSet(0), UITableViewRowAnimation.Automatic);
            });
        }
    }
}
