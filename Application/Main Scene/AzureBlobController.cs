using System;
using System.Collections.Specialized;
using System.Threading.Tasks;

using Foundation;

using MobileCoreServices;

using Newtonsoft.Json;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.FileSharing.Aliyun;

using PCPersonalCloud;

using Ricardo.RMBProgressHUD.iOS;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class AzureBlobController : UITableViewController
    {
        public AzureBlobController(IntPtr handle) : base(handle) { }

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
                            var model = JsonConvert.DeserializeObject<AzureBlobConfig>(text);
                            text = model.ConnectionString;
                            Container.Text = model.BlobName;
                        }
                        catch
                        {
                            // Ignored.
                        }
                    }

                    if (Uri.IsWellFormedUriString(text, UriKind.Absolute))
                    {
                        var uri = new Uri(text);
                        EndpointSuffix.Text = uri.GetLeftPart(UriPartial.Path);
                        AccountKey.Text = uri.Query.Substring(1);
                        Container.Text = uri.LocalPath.Substring(1);
                        return;
                    }

                    var pairs = new NameValueCollection();
                    foreach (var pair in text.Split(';', StringSplitOptions.RemoveEmptyEntries))
                    {
                        var splitIndex = pair.IndexOf('=', StringComparison.Ordinal);
                        pairs.Add(pair.Substring(0, splitIndex), pair.Substring(splitIndex + 1));
                    }

                    if (pairs["BlobEndpoint"] is { } endpoint && Uri.IsWellFormedUriString(endpoint, UriKind.Absolute) && pairs["SharedAccessSignature"] is { } sas)
                    {
                        var uri = new Uri(endpoint);
                        EndpointSuffix.Text = uri.GetLeftPart(UriPartial.Path);
                        AccountKey.Text = sas;
                        Container.Text = uri.LocalPath.Substring(1);
                        return;
                    }

                    if (pairs["AccountName"] is { } account && pairs["AccountKey"] is { } key && pairs["EndpointSuffix"] is { } suffix)
                    {
                        EndpointSuffix.Text = suffix;
                        AccountName.Text = account;
                        AccountKey.Text = key;
                        Container.BecomeFirstResponder();
                        return;
                    }
                }

                SPAlert.PresentPreset(this.Localize("Online.ClipboardNoData"), this.Localize("Online.PasteManually"), SPAlertPreset.Warning);
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

            var endpoint = EndpointSuffix.Text;
            if (string.IsNullOrEmpty(endpoint))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("Azure.BadEndpoint"), () => {
                    EndpointSuffix.BecomeFirstResponder();
                });
                return;
            }

            var accountName = AccountName.Text;

            var accessKey = AccountKey.Text;
            if (string.IsNullOrEmpty(accessKey))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("Azure.BadAccountKey"), () => {
                    AccountKey.BecomeFirstResponder();
                });
                return;
            }

            var container = Container.Text;
            if (string.IsNullOrEmpty(container))
            {
                this.ShowWarning(this.Localize("Online.BadCredential"), this.Localize("Azure.BadContainer"), () => {
                    Container.BecomeFirstResponder();
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

            string connection;
            if (endpoint.Contains(accountName, StringComparison.Ordinal)) accountName = null;
            if (endpoint.StartsWith("http://", StringComparison.Ordinal)) endpoint = endpoint.Replace("http://", "https://");
            if (endpoint.StartsWith("https://", StringComparison.Ordinal))
            {
                if (string.IsNullOrEmpty(accountName))
                {
                    if (string.IsNullOrEmpty(accessKey)) connection = endpoint;
                    else connection = $"BlobEndpoint={endpoint};SharedAccessSignature={accessKey}";
                }
                else
                {
                    this.ShowError(this.Localize("Azure.BadAccount"), this.Localize("Azure.EndpointAndNameMismatch"));
                    return;
                }
            }
            else
            {
                if (string.IsNullOrEmpty(accountName))
                {
                    this.ShowError(this.Localize("Online.BadCredential"), this.Localize("Azure.BadAccountName"));
                    return;
                }
                else connection = $"DefaultEndpointsProtocol=https;AccountName={accountName};AccountKey={accessKey};EndpointSuffix={endpoint}";
            }

            var hud = MBProgressHUD.ShowHUD(NavigationController.View, true);
            hud.Label.Text = this.Localize("Online.Verifying");

            Task.Run(() => {
                var config = new AzureBlobConfig {
                    ConnectionString = connection,
                    BlobName = container
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
                            this.ShowError(this.Localize("Azure.CannotAddService"), this.Localize("Error.Internal"));
                        });
                    }
                }
                else
                {
                    InvokeOnMainThread(() => {
                        hud.Hide(true);
                        this.ShowError(this.Localize("Error.Authentication"), this.Localize("Azure.Unauthorized"));
                    });
                }
            });
        }
    }
}
