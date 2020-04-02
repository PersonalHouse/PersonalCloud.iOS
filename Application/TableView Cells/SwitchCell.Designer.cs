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

namespace Unishare.Apps.DarwinMobile
{
    [Register ("SwitchCell")]
    partial class SwitchCell
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.NSLayoutConstraint LeadingMargin { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UISwitch SwitchButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel TitleLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.NSLayoutConstraint TrailingMargin { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (LeadingMargin != null) {
                LeadingMargin.Dispose ();
                LeadingMargin = null;
            }

            if (SwitchButton != null) {
                SwitchButton.Dispose ();
                SwitchButton = null;
            }

            if (TitleLabel != null) {
                TitleLabel.Dispose ();
                TitleLabel = null;
            }

            if (TrailingMargin != null) {
                TrailingMargin.Dispose ();
                TrailingMargin = null;
            }
        }
    }
}