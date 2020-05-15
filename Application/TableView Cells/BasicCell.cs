using System;

using UIKit;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public partial class BasicCell : UITableViewCell
    {
        public BasicCell(IntPtr handle) : base(handle) { }

        public const string Identifier = "TableViewKey";

        public void Update(string title, bool selectable = false)
        {
            TextLabel.Text = title;
            TextLabel.TextColor = Colors.DefaultText;

            SelectionStyle = selectable ? UITableViewCellSelectionStyle.Default : UITableViewCellSelectionStyle.None;
        }

        public void Update(string title, UIColor color, bool selectable = false)
        {
            TextLabel.Text = title;
            TextLabel.TextColor = color;

            SelectionStyle = selectable ? UITableViewCellSelectionStyle.Default : UITableViewCellSelectionStyle.None;
        }

        public void Update(string title, UIColor color, UITextAlignment alignment, bool selectable = false)
        {
            TextLabel.Text = title;
            TextLabel.TextColor = color;
            TextLabel.TextAlignment = alignment;

            SelectionStyle = selectable ? UITableViewCellSelectionStyle.Default : UITableViewCellSelectionStyle.None;
        }
    }
}
