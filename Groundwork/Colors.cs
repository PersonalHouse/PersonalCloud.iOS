using UIKit;

namespace Unishare.Apps.DarwinCore
{
    public static class Colors
    {
        public static UIColor DefaultText => UIDevice.CurrentDevice.CheckSystemVersion(13, 0) ? UIColor.LabelColor : UIColor.DarkTextColor;
        public static UIColor PlaceholderText => UIDevice.CurrentDevice.CheckSystemVersion(13, 0) ? UIColor.SecondaryLabelColor : new UIColor(red: 0.24f, green: 0.24f, blue: 0.26f, alpha: 0.6f);

        public static UIColor BlueButton => new UIColor(red: 0.00f, green: 0.48f, blue: 1.00f, alpha: 1.0f);
        public static UIColor OrangeFlag => new UIColor(red: 1.00f, green: 0.58f, blue: 0.00f, alpha: 1.0f);
        public static UIColor DangerousRed => new UIColor(red: 1.00f, green: 0.23f, blue: 0.19f, alpha: 1.0f);

        public static UIColor Indigo => new UIColor(red: 0.35f, green: 0.34f, blue: 0.84f, alpha: 1.0f);
    }
}
