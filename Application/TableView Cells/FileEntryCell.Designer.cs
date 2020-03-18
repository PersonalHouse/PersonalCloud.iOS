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
    [Register ("FileEntryCell")]
    partial class FileEntryCell
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIImageView IconImage { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel NameLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel SizeLabel { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UILabel TypeLabel { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (IconImage != null) {
                IconImage.Dispose ();
                IconImage = null;
            }

            if (NameLabel != null) {
                NameLabel.Dispose ();
                NameLabel = null;
            }

            if (SizeLabel != null) {
                SizeLabel.Dispose ();
                SizeLabel = null;
            }

            if (TypeLabel != null) {
                TypeLabel.Dispose ();
                TypeLabel = null;
            }
        }
    }
}