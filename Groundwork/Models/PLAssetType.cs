namespace NSPersonalCloud.DarwinCore.Models
{
    public enum PLAssetType : long
    {
        Unknown,
        Image,
        Video,
        Audio
    }

    public static class PLAssetTypeExtensions
    {
        public static string ToLocalizedString(this PLAssetType type)
        {
            return type switch
            {
                PLAssetType.Image => UIKitExtensions.Localize("PLAsset.Photo"),
                PLAssetType.Video => UIKitExtensions.Localize("PLAsset.Video"),
                PLAssetType.Audio => UIKitExtensions.Localize("PLAsset.Audio"),
                _ => UIKitExtensions.Localize("PLAsset.UnknownType")
            };
        }
    }
}
