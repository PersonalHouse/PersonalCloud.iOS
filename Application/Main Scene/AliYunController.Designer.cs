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
    [Register ("AliYunController")]
    partial class AliYunController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AYAccessID { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AYAccessSecret { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AYBucket { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AYEndpoint { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIBarButtonItem SaveButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField ServiceName { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (AYAccessID != null) {
                AYAccessID.Dispose ();
                AYAccessID = null;
            }

            if (AYAccessSecret != null) {
                AYAccessSecret.Dispose ();
                AYAccessSecret = null;
            }

            if (AYBucket != null) {
                AYBucket.Dispose ();
                AYBucket = null;
            }

            if (AYEndpoint != null) {
                AYEndpoint.Dispose ();
                AYEndpoint = null;
            }

            if (SaveButton != null) {
                SaveButton.Dispose ();
                SaveButton = null;
            }

            if (ServiceName != null) {
                ServiceName.Dispose ();
                ServiceName = null;
            }
        }
    }
}