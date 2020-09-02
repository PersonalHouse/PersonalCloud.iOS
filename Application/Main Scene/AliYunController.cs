using System;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using Newtonsoft.Json;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.FileSharing.Aliyun;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

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

                this.ShowWarning(this.Localize("Online.ClipboardNoData"), this.Localize("Online.PasteManually"));
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
            foreach (var character in PathConsts.InvalidCharacters)
            {
                if (name?.Contains(character) == true) invalidCharHit = true;
            }
            if (string.IsNullOrEmpty(name) || invalidCharHit)
            {
                this.ShowWarning(this.Localize("Online.BadName"), this.Localize("Online.IllegalName"), () => {
                    ServiceName.BecomeFirstResponder();
                });
                return;
            }

            var endpoint = Endpoint.Text;
            if (string.IsNullOrEmpty(endpoint))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadEndpoint"), () => {
                    Endpoint.BecomeFirstResponder();
                });
                return;
            }

            var bucket = BucketName.Text;
            if (string.IsNullOrEmpty(bucket))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadBucket"), () => {
                    BucketName.BecomeFirstResponder();
                });
                return;
            }

            var accessId = AccessKeyID.Text;
            if (string.IsNullOrEmpty(accessId))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadUserID"), () => {
                    AccessKeyID.BecomeFirstResponder();
                });
                return;
            }

            var accessSecret = AccessKeySecret.Text;
            if (string.IsNullOrEmpty(accessSecret))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("AliYun.BadUserSecret"), () => {
                    AccessKeySecret.BecomeFirstResponder();
                });
                return;
            }

            if (!Globals.Database.IsStorageNameUnique(name))
            {
                this.ShowWarning(this.Localize("Online.ServiceAlreadyExists"), this.Localize("Online.ChooseADifferentName"), () => {
                    ServiceName.BecomeFirstResponder();
                });
                return;
            }

            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Online.Verifying");
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
                            hud.Hide(true);
                            NavigationController.DismissViewController(true, null);
                        });
                    }
                    catch
                    {
                        InvokeOnMainThread(() => {
                            hud.Hide(true);
                            this.ShowError(this.Localize("AliYun.CannotAddService"), this.Localize("Error.Internal"));
                        });
                    }
                }
                else
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.Authentication"), this.Localize("AliYun.Unauthorized"));
                    });
                }
            });
        }
    }
}
