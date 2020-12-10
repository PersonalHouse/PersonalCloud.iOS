using System.Net.Http;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public static class CloudExceptions
    {
        public static UIAlertController Explain(HttpRequestException exception)
        {
            var message = exception.Message;
#pragma warning disable CA1307 // Specify StringComparison
            if (message.Contains("400")) message = UIKitExtensions.Localize("Error.RemoteHTTP.400");
            else if (message.Contains("401")) message = UIKitExtensions.Localize("Error.RemoteHTTP.401");
            else if (message.Contains("403")) message = UIKitExtensions.Localize("Error.RemoteHTTP.403");
            else if (message.Contains("404")) message = UIKitExtensions.Localize("Error.RemoteHTTP.404");
            else if (message.Contains("429")) message = UIKitExtensions.Localize("Error.RemoteHTTP.429");
            else if (message.Contains("500")) message = UIKitExtensions.Localize("Error.RemoteHTTP.500");
            else if (message.Contains("501")) message = UIKitExtensions.Localize("Error.RemoteHTTP.501");
#pragma warning restore CA1307 // Specify StringComparison

            var alert = UIAlertController.Create(UIKitExtensions.Localize("Error.RemoteHTTP"), message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create(UIKitExtensions.Localize("Global.OKAction"), UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            return alert;
        }
    }
}
