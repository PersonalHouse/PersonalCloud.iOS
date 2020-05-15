using System;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class KeyValueCell : UITableViewCell
    {
        public KeyValueCell (IntPtr handle) : base (handle) { }

        public const string Identifier = "TableViewKeyValue";

        public override void PrepareForReuse()
        {
            base.PrepareForReuse();
            Accessory = UITableViewCellAccessory.None;
        }

        public void Update(string title, string detail, bool selectable = false)
        {
            TitleLabel.Text = title;
            TitleLabel.TextColor = Colors.DefaultText;
            DetailLabel.Text = detail;
            DetailLabel.TextColor = Colors.PlaceholderText;

            SelectionStyle = selectable ? UITableViewCellSelectionStyle.Default : UITableViewCellSelectionStyle.None;
        }

        public void Update(string title, string detail, UIColor titleColor, UIColor detailColor, bool selectable = false)
        {
            TitleLabel.Text = title;
            TitleLabel.TextColor = titleColor;
            DetailLabel.Text = detail;
            DetailLabel.TextColor = detailColor;

            SelectionStyle = selectable ? UITableViewCellSelectionStyle.Default : UITableViewCellSelectionStyle.None;
        }
    }
}
