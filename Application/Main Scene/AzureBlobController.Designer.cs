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
    [Register ("AzureBlobController")]
    partial class AzureBlobController
    {
        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AccountKey { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AccountName { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField Container { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField EndpointSuffix { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UIBarButtonItem SaveButton { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField ServiceName { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITableViewCell ShareCredentialsCell { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITableViewCell StoreCredentialsCell { get; set; }

        void ReleaseDesignerOutlets ()
        {
            if (AccountKey != null) {
                AccountKey.Dispose ();
                AccountKey = null;
            }

            if (AccountName != null) {
                AccountName.Dispose ();
                AccountName = null;
            }

            if (Container != null) {
                Container.Dispose ();
                Container = null;
            }

            if (EndpointSuffix != null) {
                EndpointSuffix.Dispose ();
                EndpointSuffix = null;
            }

            if (SaveButton != null) {
                SaveButton.Dispose ();
                SaveButton = null;
            }

            if (ServiceName != null) {
                ServiceName.Dispose ();
                ServiceName = null;
            }

            if (ShareCredentialsCell != null) {
                ShareCredentialsCell.Dispose ();
                ShareCredentialsCell = null;
            }

            if (StoreCredentialsCell != null) {
                StoreCredentialsCell.Dispose ();
                StoreCredentialsCell = null;
            }
        }
    }
}
