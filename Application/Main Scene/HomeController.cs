using System;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class HomeController : UITabBarController
    {
        public HomeController (IntPtr handle) : base (handle) { }

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SelectedIndex = 2;
        }
    }
}
