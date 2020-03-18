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
    [Register ("FavoritesController")]
    partial class FavoritesController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIBarButtonItem FileButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIBarButtonItem FolderButton { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (FileButton != null) {
                FileButton.Dispose ();
                FileButton = null;
            }

            if (FolderButton != null) {
                FolderButton.Dispose ();
                FolderButton = null;
            }
        }
    }
}