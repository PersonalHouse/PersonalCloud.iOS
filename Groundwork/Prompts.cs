using System;
using System.ComponentModel;
using System.Linq;

using UIKit;

namespace Unishare.Apps.DarwinCore
{
    public static class Prompts
    {
        public static void CreatePrompt(this UIViewController controller,
            [Localizable(true)] string title, [Localizable(true)] string message,
            [Localizable(true)] string defaultText, [Localizable(true)] string placeholderText,
            [Localizable(true)] string actionButton, [Localizable(true)] string cancelButton,
            Action<string> completionHandler)
        {
            var prompt = UIAlertController.Create(title, message, UIAlertControllerStyle.Alert);
            prompt.AddTextField(field => {
                field.Text = defaultText;
                field.Placeholder = placeholderText;
            });
            var preferred = UIAlertAction.Create(actionButton, UIAlertActionStyle.Default, action => {
                completionHandler?.Invoke(prompt.TextFields.FirstOrDefault()?.Text);
            });
            prompt.AddAction(UIAlertAction.Create(cancelButton, UIAlertActionStyle.Cancel, null));
            prompt.AddAction(preferred);
            prompt.SetPreferredAction(preferred);
            controller.PresentViewController(prompt, true, null);
        }
    }
}
