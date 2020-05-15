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
    [Register ("SwitchCell")]
    partial class SwitchCell
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UISwitch SwitchButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel TitleLabel { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (SwitchButton != null) {
                SwitchButton.Dispose ();
                SwitchButton = null;
            }

            if (TitleLabel != null) {
                TitleLabel.Dispose ();
                TitleLabel = null;
            }
        }
    }
}
