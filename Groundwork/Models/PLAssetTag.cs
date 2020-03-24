using System;

namespace Unishare.Apps.DarwinCore.Models
{
    [Flags]
    public enum PLAssetTag : ulong
    {
        None = 0,
        Panorama = 1,
        HDR = 1 << 1,
        Screenshot = 1 << 2,
        LivePhoto = 1 << 3,
        Bokeh = 1 << 4,
        Online = 1 << 16,
        SloMo = 1 << 17,
        TimeLapse = 1 << 18
    }
}
