using System;
using System.Collections.Generic;
using System.Linq;

namespace Unishare.Apps.DarwinCore.Models
{
    [Flags]
    public enum PLAssetTags : ulong
    {
        None = 0,
        Panorama = 1,
        HDR = 1 << 1,
        Screenshot = 1 << 2,
        LivePhoto = 1 << 3,
        Bokeh = 1 << 4,
        UltraWide = 1 << 5,
        AutoComposition = 1 << 7,
        Online = 1 << 16,
        SloMo = 1 << 17,
        TimeLapse = 1 << 18
    }

    public static class PLAssetTagsExtensions
    {
        public static PLAssetTags[] Unpack(this PLAssetTags tags)
        {
            var flags = new List<PLAssetTags>();
            foreach (var tag in (PLAssetTags[]) Enum.GetValues(typeof(PLAssetTags)))
            {
                if (tag == PLAssetTags.None) continue;
                if (tags.HasFlag(tag)) flags.Add(tag);
            }
            return flags.ToArray();
        }

        public static string ToLocalizedString(this PLAssetTags[] tags)
        {
            return string.Join("、", tags.Select(x => {
                return x switch
                {
                    PLAssetTags.None => throw new ArgumentOutOfRangeException(),
                    PLAssetTags.Panorama => UIKitExtensions.Localize("PLAsset.Panorama"),
                    PLAssetTags.HDR => UIKitExtensions.Localize("PLAsset.HDR"),
                    PLAssetTags.Screenshot => UIKitExtensions.Localize("PLAsset.Screenshot"),
                    PLAssetTags.LivePhoto => UIKitExtensions.Localize("PLAsset.LivePhoto"),
                    PLAssetTags.Bokeh => UIKitExtensions.Localize("PLAsset.Portrait"),
                    PLAssetTags.UltraWide => UIKitExtensions.Localize("PLAsset.UltraWide"),
                    PLAssetTags.AutoComposition => UIKitExtensions.Localize("PLAsset.AutoComposition"),
                    PLAssetTags.Online => UIKitExtensions.Localize("PLAsset.Online"),
                    PLAssetTags.SloMo => UIKitExtensions.Localize("PLAsset.SloMo"),
                    PLAssetTags.TimeLapse => UIKitExtensions.Localize("PLAsset.TimeLapse"),
                    _ => throw new ArgumentOutOfRangeException()
                };
            }));
        }
    }
}
