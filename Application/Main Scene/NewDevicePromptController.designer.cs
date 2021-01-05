// WARNING
//
// This file has been generated automatically by Visual Studio to store outlets and
// actions made in the UI designer. If it is removed, they will be lost.
// Manual changes to this file may not be handled correctly.
//
using Foundation;
using System.CodeDom.Compiler;

namespace NSPersonalCloud.DarwinMobile.Base.lproj
{
	[Register ("NewDevicePromptController")]
	partial class NewDevicePromptController
	{
		[Outlet]
		UIKit.UIWebView web { get; set; }

		[Action ("OnClickCancel:")]
		partial void OnClickCancel (Foundation.NSObject sender);

		[Action ("OpenWebSite:")]
		partial void OpenWebSite (Foundation.NSObject sender);
		
		void ReleaseDesignerOutlets ()
		{
			if (web != null) {
				web.Dispose ();
				web = null;
			}
		}
	}
}
