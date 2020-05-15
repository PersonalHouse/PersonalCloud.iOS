using System;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class AddConnectionController : UITableViewController
    {
        public AddConnectionController (IntPtr handle) : base (handle) { }

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            CancelButton.Clicked += (o, e) => NavigationController.DismissViewController(true, null);
        }

        #endregion
    }
}
