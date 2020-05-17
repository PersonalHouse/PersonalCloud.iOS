using Foundation;

using Photos;

using UIKit;

namespace NSPersonalCloud.DarwinCore
{
    public static class UserInfoExtensions
    {
        public static long? UserInfoGetSize(this PHAssetResource resource)
        {
            return (resource.ValueForKey(new NSString("fileSize")) as NSNumber)?.Int64Value;
        }

        public static UIView UserInfoGetView(this UIBarButtonItem item)
        {
            return item.ValueForKey(new NSString("view")) as UIView;
        }
    }
}
