using System;
using System.IO;

using Humanizer;

using Photos;

using UIKit;

using Unishare.Apps.DarwinCore;
using Unishare.Apps.DarwinCore.Models;

namespace Unishare.Apps.DarwinMobile
{
    public partial class FileEntryCell : UITableViewCell
    {
        public FileEntryCell(IntPtr handle) : base(handle) { }

        public const string Identifier = "TableViewFileEntry";

        public override void AwakeFromNib()
        {
            base.AwakeFromNib();
            SizeLabel.TextColor = Colors.PlaceholderText;
            TypeLabel.TextColor = Colors.PlaceholderText;
        }

        private void Update(UTI uti)
        {
            var description = uti.ToString();
            if (!string.IsNullOrEmpty(description)) TypeLabel.Text = description;
            else TypeLabel.Text = "未知文件类型";

            if (Images.FromUTI(uti) is UIImage image) IconImage.Image = image;
            else IconImage.Image = UIImage.FromBundle("UnknownFile");
        }

        public void Update(PLAsset photo)
        {
            IconImage.Image = null;
            IconImage.ContentMode = UIViewContentMode.ScaleAspectFill;
            NameLabel.Text = photo.FileName;
            var type = photo.Type.ToLocalizedString();
            var tags = photo.Tags.Unpack().ToLocalizedString();
            if (!string.IsNullOrEmpty(tags)) TypeLabel.Text = tags;
            else TypeLabel.Text = type;
            SizeLabel.Text = null;

            PHImageManager.DefaultManager.RequestImageForAsset(photo.Asset, IconImage.Frame.Size, PHImageContentMode.AspectFill, null, (result, info) => {
                var error = info.ObjectForKey(PHImageKeys.Error);
                if (error == null) InvokeOnMainThread(() => IconImage.Image = result);
            });
            SizeLabel.Text = photo.Size.Bytes().Humanize("0.00");
        }

        public void Update(UIImage icon, string title, string subtitle, string detail)
        {
            IconImage.Image = icon;
            NameLabel.Text = title;
            TypeLabel.Text = subtitle;
            SizeLabel.Text = detail;
        }

        public void Update(string fileName)
        {
            NameLabel.Text = fileName;
            SizeLabel.Text = null;

            var extension = Path.GetExtension(fileName);
            if (string.IsNullOrEmpty(extension))
            {
                TypeLabel.Text = "未知文件类型";
                IconImage.Image = UIImage.FromBundle("UnknownFile");
                return;
            }

            var uti = UTI.FromFileName(extension);
            Update(uti);
        }

        public void Update(string fileName, long sizeInBytes)
        {
            Update(fileName);
            SizeLabel.Text = sizeInBytes.Bytes().Humanize("0.00");
        }

        public void Update(string fileName, UTI uti, string typeDescription)
        {
            Update(fileName);
            Update(uti);
            TypeLabel.Text = typeDescription;
        }

        public void Update(string fileName, UTI uti)
        {
            Update(fileName);
            Update(uti);
        }

        public void Update(string fileName, UTI uti, long sizeInBytes)
        {
            Update(fileName, sizeInBytes);
            Update(uti);
        }
    }
}