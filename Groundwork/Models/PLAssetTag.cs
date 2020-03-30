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

        public static string ToChineseString(this PLAssetTags[] tags)
        {
            return string.Join("、", tags.Select(x => {
                return x switch
                {
                    PLAssetTags.None => throw new ArgumentOutOfRangeException(),
                    PLAssetTags.Panorama => "全景",
                    PLAssetTags.HDR => "HDR",
                    PLAssetTags.Screenshot => "截屏",
                    PLAssetTags.LivePhoto => "实况",
                    PLAssetTags.Bokeh => "人像",
                    PLAssetTags.UltraWide => "超广角",
                    PLAssetTags.AutoComposition => "自动构图",
                    PLAssetTags.Online => "云端",
                    PLAssetTags.SloMo => "慢动作",
                    PLAssetTags.TimeLapse => "延时摄影",
                    _ => throw new ArgumentOutOfRangeException()
                };
            }));
        }
    }
}
