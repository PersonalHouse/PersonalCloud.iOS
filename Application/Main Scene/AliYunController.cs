using System;
using System.Threading.Tasks;

using NSPersonalCloud;
using NSPersonalCloud.FileSharing.Aliyun;

using UIKit;

using Unishare.Apps.Common.Models;
using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public partial class AliYunController : UITableViewController
    {
        public AliYunController(IntPtr handle) : base(handle) { }

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
                this.ShowAlert(this.Localize("AliYun.BadName"), this.Localize("AliYun.NameCannotBeEmpty"), action => {
                    AYEndpoint.BecomeFirstResponder();
                });
                return;
            }

            var endpoint = AYEndpoint.Text;
            if (string.IsNullOrEmpty(endpoint))
            {
                this.ShowAlert(this.Localize("AliYun.BadCredential"), this.Localize("AliYun.BadEndpoint"), action => {
                    AYEndpoint.BecomeFirstResponder();
                });
                return;
            }

            var bucket = AYBucket.Text;
            if (string.IsNullOrEmpty(bucket))
            {
                this.ShowAlert(this.Localize("AliYun.BadCredential"), this.Localize("AliYun.BadBucket"), action => {
                    AYBucket.BecomeFirstResponder();
                });
                return;
            }

            var accessId = AYAccessID.Text;
            if (string.IsNullOrEmpty(accessId))
            {
                this.ShowAlert(this.Localize("AliYun.BadCredential"), this.Localize("AliYun.BadUserID"), action => {
                    AYAccessID.BecomeFirstResponder();
                });
                return;
            }

            var accessSecret = AYAccessSecret.Text;
            if (string.IsNullOrEmpty(accessSecret))
            {
                this.ShowAlert(this.Localize("AliYun.BadCredential"), this.Localize("AliYun.BadUserSecret"), action => {
                    AYAccessSecret.BecomeFirstResponder();
                });
                return;
            }

            if (Globals.Database.Find<AliYunOSS>(x => x.Name == name) != null)
            {
                this.ShowAlert(this.Localize("Online.ServiceAlreadyExists"), this.Localize("Online.ChooseADifferentName"), action => {
                    ServiceName.BecomeFirstResponder();
                });
                return;
            }

            var alert = UIAlertController.Create(this.Localize("Online.Verifying"), null, UIAlertControllerStyle.Alert);
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
                        try
                        {
                            Globals.CloudManager.AddStorageProvider(Globals.CloudManager.PersonalClouds[0].Id, name, config, StorageProviderVisibility.Private);
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    NavigationController.DismissViewController(true, null);
                                });
                            });
                        }
                        catch
                        {
                            InvokeOnMainThread(() => {
                                DismissViewController(true, () => {
                                    this.ShowAlert(this.Localize("AliYun.CannotAddService"), this.Localize("Error.Internal"));
                                });
                            });
                        }
                    }
                    else
                    {
                        InvokeOnMainThread(() => {
                            DismissViewController(true, () => {
                                this.ShowAlert(this.Localize("Error.Authentication"), this.Localize("AliYun.Unauthorized"));
                            });
                        });
                    }
                });
            });
        }
    }
}