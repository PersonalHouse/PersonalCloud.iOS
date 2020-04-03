using System;
using System.Net.Http;

using UIKit;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public static class CloudExceptions
    {
        public static UIAlertController Explain(HttpRequestException exception)
        {
            var message = exception.Message;
            if (message.Contains("400")) message = "无法完成此操作。";
            else if (message.Contains("401")) message = "远程设备拒绝处理您的请求。" + Environment.NewLine + Environment.NewLine + "如果您正在访问网络存储服务，请检查您的凭据或帐户是否仍然有效。如果您正在访问其它个人设备，请重启个人云 App 后再试。";
            else if (message.Contains("403")) message = "无权操作指定文件或文件夹。" + Environment.NewLine + Environment.NewLine + "如果您正在访问网络存储服务，请检查您的凭据或帐户是否仍然有效。如果您正在访问其它个人设备，指定设备配置可能存在问题。";
            else if (message.Contains("404")) message = "指定文件或文件夹已不存在，无法完成此操作。";
            else if (message.Contains("429")) message = "远程设备正忙，无法即时处理此文件或文件夹。请稍候再试。";
            else if (message.Contains("500")) message = "执行操作时出现未知问题，请稍候再试或联系技术支持。";
            else if (message.Contains("501")) message = "远程文件管理系统不支持此操作";

            var alert = UIAlertController.Create("执行操作时遇到问题", message, UIAlertControllerStyle.Alert);
            var ok = UIAlertAction.Create("好", UIAlertActionStyle.Default, null);
            alert.AddAction(ok);
            alert.SetPreferredAction(ok);
            return alert;
        }
    }
}
