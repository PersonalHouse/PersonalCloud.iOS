using System;
using System.IO;
using System.Threading.Tasks;

using AVFoundation;

using Foundation;

using Photos;

namespace NSPersonalCloud.DarwinMobile
{
    public static class PHAssetExt
    {
        public static async Task<string> GetIOSFilePath(this PHAsset photo)
        {

            var tcs = new TaskCompletionSource<NSUrl>();
            if (photo.MediaType == PHAssetMediaType.Image)
            {
                var options = new PHContentEditingInputRequestOptions();
                options.CanHandleAdjustmentData = _ => true;
                photo.RequestContentEditingInput(options, (contentEditingInput, requestStatusInfo) => {
                    tcs.SetResult(contentEditingInput.FullSizeImageUrl);
                });
            }
            else if (photo.MediaType == PHAssetMediaType.Video)
            {
                var options = new PHVideoRequestOptions();
                options.Version = PHVideoRequestOptionsVersion.Original;
                PHImageManager.DefaultManager.RequestAvAsset(photo, options, (asset, audioMix, info) => {
                    if (asset is AVUrlAsset urlAsset)
                    {
                        tcs.SetResult(urlAsset.Url);
                        return;
                    }
                    tcs.SetException(new InvalidDataException("RequestAvAsset didn't get AVUrlAsset"));
                });
            }
            var origfilepath = await tcs.Task.ConfigureAwait(false);
            return origfilepath.Path;
        }
    }
}

