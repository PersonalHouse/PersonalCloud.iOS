using System;

using Foundation;

using UIKit;

namespace Unishare.Apps.DarwinMobile
{
    public partial class TitleEditorCell : UITableViewCell, IUITextFieldDelegate
    {
        public TitleEditorCell(IntPtr handle) : base(handle) { }

        public const string Identifier = "TableViewKeyEditable";

        private Func<UITextField, bool> Validator { get; set; }
        private Action<UITextField> Observer { get; set; }

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            EditField.Delegate = this;
            EditField.ReturnKeyType = UIReturnKeyType.Done;
            UITextField.Notifications.ObserveTextFieldTextDidChange(EditField, OnTextChange);
        }

        public override void PrepareForReuse()
        {
            Reset();
            base.PrepareForReuse();
        }

        public void Update(string title, string placeholder, Action<UITextField> observer, string field = null, UIKeyboardType keyboard = UIKeyboardType.Default)
        {
            EditField.Enabled = true;
            EditField.SecureTextEntry = false;

            TitleLabel.Text = title;
            EditField.Placeholder = placeholder;
            EditField.Text = field;
            EditField.KeyboardType = keyboard;

            Validator = null;
            Observer = observer;
        }

        public void Update(string title, string placeholder, Func<UITextField, bool> validator, string field = null, UIKeyboardType keyboard = UIKeyboardType.Default)
        {
            EditField.Enabled = true;
            EditField.SecureTextEntry = false;

            TitleLabel.Text = title;
            EditField.Placeholder = placeholder;
            EditField.Text = field;
            EditField.KeyboardType = keyboard;

            Validator = validator;
            Observer = null;
        }

        public void UpdateAsReadOnly(string title, string detail, bool isMasked = false)
        {
            EditField.Enabled = false;
            TitleLabel.Text = title;
            EditField.Text = detail;
            EditField.TextColor = UIColor.LightGray;
            EditField.SecureTextEntry = isMasked;
        }

        public void Reset()
        {
            Validator = null;
            Observer = null;
        }

        #region UITextFieldDelegate

        [Export("textFieldShouldEndEditing:")]
        public bool ShouldEndEditing(UITextField textField)
        {
            return Validator?.Invoke(textField) ?? true;
        }

        [Export("textFieldShouldReturn:")]
        public bool ShouldReturn(UITextField textField)
        {
            textField.EndEditing(false);
            return true;
        }

        public void OnTextChange(object sender, NSNotificationEventArgs e)
        {
            Observer?.Invoke(EditField);
        }

        #endregion
    }
}
