// WARNING
//
// This file has been generated automatically by Visual Studio from the outlets and
// actions declared in your storyboard file.
// Manual changes to this file will not be maintained.
//
using Foundation;
using System;
using System.CodeDom.Compiler;
using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    [Register ("WelcomeScreenController")]
    partial class WelcomeScreenController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIButton AddCloudButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIButton CreateCloudButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel EarlyBirdLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel ReturningLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel WelcomeTitleLabel { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (AddCloudButton != null) {
                AddCloudButton.Dispose ();
                AddCloudButton = null;
            }

            if (CreateCloudButton != null) {
                CreateCloudButton.Dispose ();
                CreateCloudButton = null;
            }

            if (EarlyBirdLabel != null) {
                EarlyBirdLabel.Dispose ();
                EarlyBirdLabel = null;
            }

            if (ReturningLabel != null) {
                ReturningLabel.Dispose ();
                ReturningLabel = null;
            }

            if (WelcomeTitleLabel != null) {
                WelcomeTitleLabel.Dispose ();
                WelcomeTitleLabel = null;
            }
        }
    }
}
