using System;

using UIKit;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class AccentButtonCell : UITableViewCell
    {
        public const string Identifier = "TableCellButton";

        public AccentButtonCell (IntPtr handle) : base (handle) { }

        public event EventHandler Clicked;

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            AccentButton.Layer.CornerRadius = 10;
            AccentButton.ClipsToBounds = true;
        }

        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            AccentButton.TouchUpInside -= OnButtonTapped;
            Clicked = null;
        }

        public void Update(string text)
        {
            AccentButton.SetTitle(text, UIControlState.Normal);
            SelectionStyle = UITableViewCellSelectionStyle.None;
            AccentButton.TouchUpInside += OnButtonTapped;
        }

        public void Update(string text, UIColor background)
        {
            AccentButton.SetTitle(text, UIControlState.Normal);
            AccentButton.BackgroundColor = background;
            SelectionStyle = UITableViewCellSelectionStyle.None;
            AccentButton.TouchUpInside += OnButtonTapped;
        }

        private void OnButtonTapped(object sender, EventArgs e)
        {
            Clicked?.Invoke(this, EventArgs.Empty);
        }
    }
}
