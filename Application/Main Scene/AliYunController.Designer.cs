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
        UIKit.UITextField AccessKeyID { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField AccessKeySecret { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField BucketName { get; set; }

        [Outlet]
        [GeneratedCode ("iOS Designer", "1.0")]
        UIKit.UITextField Endpoint { get; set; }

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
            if (AccessKeyID != null) {
                AccessKeyID.Dispose ();
                AccessKeyID = null;
            }

            if (AccessKeySecret != null) {
                AccessKeySecret.Dispose ();
                AccessKeySecret = null;
            }

            if (BucketName != null) {
                BucketName.Dispose ();
                BucketName = null;
            }

            if (Endpoint != null) {
                Endpoint.Dispose ();
                Endpoint = null;
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