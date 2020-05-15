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
    [Register ("AccentButtonCell")]
    partial class AccentButtonCell
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIButton AccentButton { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (AccentButton != null) {
                AccentButton.Dispose ();
                AccentButton = null;
            }
        }
    }
}
