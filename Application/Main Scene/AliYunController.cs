using System;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using Newtonsoft.Json;

using NSPersonalCloud;
using NSPersonalCloud.FileSharing.Aliyun;

using UIKit;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class AliYunController : UITableViewController
    {
        public AliYunController(IntPtr handle) : base(handle) { }

        private StorageProviderVisibility visibility = StorageProviderVisibility.Public;

        #region Lifecycle

        public override void ViewDidLoad()
        {
            base.ViewDidLoad();
            SaveButton.Clicked += VerifyCredentials;
        }

        #endregion

        #region TableView Delegate

        public override void RowSelected(UITableView tableView, NSIndexPath indexPath)
        {
            tableView.DeselectRow(indexPath, true);

            if (indexPath.Section == 1 && indexPath.Row == 0)
            {
                var text = UIPasteboard.General.GetValue(UTType.PlainText)?.ToString()?.Trim();
                if (!string.IsNullOrEmpty(text) && text.Length > 1)
                {
                    if (text[0] == '{' && text[^1] == '}')
                    {
                        try
                        {
                            var model = JsonConvert.DeserializeObject<OssConfig>(text);
                            Endpoint.Text = model.OssEndpoint;
                            BucketName.Text = model.BucketName;
                            AccessKeyID.Text = model.AccessKeyId;
                            AccessKeySecret.Text = model.AccessKeySecret;
                            return;
                        }
                        catch
                        {
                            // Ignored.
                        }
                    }
                }

                this.ShowAlert(this.Localize("Online.ClipboardNoData"), this.Localize("Online.PasteManually"));
                return;
            }

            if (indexPath.Section == 3 && indexPath.Row == 0)
            {
                visibility = StorageProviderVisibility.Public;
                ShareCredentialsCell.Accessory = UITableViewCellAccessory.Checkmark;
                StoreCredentialsCell.Accessory = UITableViewCellAccessory.None;
                return;
            }

            if (indexPath.Section == 3 && indexPath.Row == 1)
            {
                visibility = StorageProviderVisibility.Private;
                ShareCredentialsCell.Accessory = UITableViewCellAccessory.None;
                StoreCredentialsCell.Accessory = UITableViewCellAccessory.Checkmark;
                return;
            }
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
                this.ShowAlert(this.Localize("Online.BadName"), this.Localize("Online.IllegalName"), action => {
                    ServiceName.BecomeFirstResponder();
                });
                return;
            }

            var endpoint = Endpoint.Text;
            if (string.IsNullOrEmpty(endpoint))
            {
                this.ShowAlert(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadEndpoint"), action => {
                    Endpoint.BecomeFirstResponder();
                });
                return;
            }

            var bucket = BucketName.Text;
            if (string.IsNullOrEmpty(bucket))
            {
                this.ShowAlert(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadBucket"), action => {
                    BucketName.BecomeFirstResponder();
                });
                return;
            }

            var accessId = AccessKeyID.Text;
            if (string.IsNullOrEmpty(accessId))
            {
                this.ShowAlert(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadUserID"), action => {
                    AccessKeyID.BecomeFirstResponder();
                });
                return;
            }

            var accessSecret = AccessKeySecret.Text;
            if (string.IsNullOrEmpty(accessSecret))
            {
                this.ShowAlert(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadUserSecret"), action => {
                    AccessKeySecret.BecomeFirstResponder();
                });
                return;
            }

            if (!Globals.Database.IsStorageNameUnique(name))
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
                            Globals.CloudManager.AddStorageProvider(Globals.CloudManager.PersonalClouds[0].Id, Guid.NewGuid(), name, config, visibility);
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
