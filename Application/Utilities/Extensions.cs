using NSPersonalCloud.FileSharing.Aliyun;

using Unishare.Apps.Common.Models;

namespace Unishare.Apps.DarwinMobile.Utilities
{
    public static class Extensions
    {
        public static AliYunOSS ToModel(this OssConfig config, string serviceName)
        {
            if (string.IsNullOrEmpty(serviceName)) serviceName = "阿里云";
            return new AliYunOSS {
                Name = serviceName,
                Endpoint = config.OssEndpoint,
                Bucket = config.BucketName,
                AccessID = config.AccessKeyId,
                AccessSecret = config.AccessKeySecret
            };
        }

        public static OssConfig ToConfig(this AliYunOSS model)
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
