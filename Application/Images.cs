using MobileCoreServices;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public static class Images
    {
        public static UIImage FromUTI(UTI uti)
        {
            if (uti.ConformsTo(UTType.Directory)) return UIImage.FromBundle("Directory");
            if (uti.ConformsTo(UTType.Text)) return UIImage.FromBundle("TextFile");
            if (uti.ConformsTo(UTType.Movie) || uti.ConformsTo(UTType.Video)) return UIImage.FromBundle("VideoFile");
            if (uti.ConformsTo(UTType.Audio)) return UIImage.FromBundle("AudioFile");
            if (uti.ConformsTo(UTType.Image)) return UIImage.FromBundle("ImageFile");
            if (uti.ConformsTo(UTType.DiskImage)) return UIImage.FromBundle("DiskVolume");
            if (uti.ConformsTo(UTType.Archive)) return UIImage.FromBundle("ArchiveFile");
            return null;
        }
    }
}
