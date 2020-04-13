using System;
using System.Threading.Tasks;

using NSPersonalCloud;
using NSPersonalCloud.FileSharing.Aliyun;

using UIKit;
using Unishare.Apps.Common.Models;
using Unishare.Apps.DarwinCore;
using Unishare.Apps.DarwinMobile.Utilities;

namespace Unishare.Apps.DarwinMobile
{
    public partial class AliYunController : UITableViewController
    {
        public AliYunController (IntPtr handle) : base (handle) { }

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SaveButton.Clicked += VerifyCredentials;
        }

        #endregion

        private void VerifyCredentials(object sender, EventArgs e)
        {
            var name = ServiceName.Text;
            var invalidCharHit = false;
            foreach (var character in VirtualFileSystem.InvalidCharacters)
            {
                if (name?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrEmpty(name) || invalidCharHit)
            {
                this.ShowAlert("服务无名称", "您必须填写“服务名称”才能将此服务添加到个人云。", action => {
                    AYEndpoint.BecomeFirstResponder();
                });
                return;
            }

            var endpoint = AYEndpoint.Text;
            if (string.IsNullOrEmpty(endpoint))
            {
                this.ShowAlert("帐户信息不完整", "您必须填写“域名”（也称“访问域名”、Endpoint 或 Extranet Endpoint）才能连接到阿里云。", action => {
                    AYEndpoint.BecomeFirstResponder();
                });
                return;
            }

            var bucket = AYBucket.Text;
            if (string.IsNullOrEmpty(bucket))
            {
                this.ShowAlert("帐户信息不完整", "您必须填写“存储空间”（也称 Bucket）才能连接到阿里云。", action => {
                    AYBucket.BecomeFirstResponder();
                });
                return;
            }

            var accessId = AYAccessID.Text;
            if (string.IsNullOrEmpty(accessId))
            {
                this.ShowAlert("帐户信息不完整", "您必须填写“用户 ID”（也称 Access Key ID）才能连接到阿里云。", action => {
                    AYAccessID.BecomeFirstResponder();
                });
                return;
            }

            var accessSecret = AYAccessSecret.Text;
            if (string.IsNullOrEmpty(accessSecret))
            {
                this.ShowAlert("帐户信息不完整", "您必须填写“访问密钥”（也称 Access Key Secret）才能连接到阿里云。", action => {
                    AYAccessSecret.BecomeFirstResponder();
                });
                return;
            }

            if (Globals.Database.Find<AliYunOSS>(name) != null)
            {
                this.ShowAlert("同名服务已存在", "为避免数据冲突，请为此服务指定不同的名称。", action => {
                    ServiceName.BecomeFirstResponder();
                });
                return;
            }

            var alert = UIAlertController.Create("正在验证……", null, UIAlertControllerStyle.Alert);
            PresentViewController(alert, true, () => {
                Task.Run(() => {
                    var config = new OssConfig {
                        OssEndpoint = endpoint,
                        BucketName = bucket,
                        AccessKeyId = accessId,
                        AccessKeySecret = accessSecret
                    };

                    if (config.Verify())
                    {
                        Globals.Database.InsertOrReplace(config.ToModel(name));
                        Globals.CloudManager.PersonalClouds[0].RootFS.ClientList[name] = new AliyunOSSFileSystemClient(config);
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                NavigationController.DismissViewController(true, null);
                            });
                        });
                    }
                    else
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert("认证失败", "您提供的帐户信息无法连接到有效的阿里云对象存储服务。请检查后重试。");
                            });
                        });
                    }
                });
            });
        }
    }
}