using NSPersonalCloud.FileSharing.Aliyun;

using NSPersonalCloud.Common.Models;
using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile.Utilities
{
    public static class DataExtensions
    {
        public static AlibabaOSS ToModel(this OssConfig config, string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) serviceName = UIKitExtensions.Localize("AliYun.Title");
            return new AlibabaOSS {
                Name = serviceName,
                Endpoint = config.OssEndpoint,
                Bucket = config.BucketName,
                AccessID = config.AccessKeyId,
                AccessSecret = config.AccessKeySecret
            };
        }

        public static OssConfig ToConfig(this AlibabaOSS model)
        {
            return new OssConfig {
                OssEndpoint = model.Endpoint,
                BucketName = model.Bucket,
                AccessKeyId = model.AccessID,
                AccessKeySecret = model.AccessSecret
            };
        }
    }
}
