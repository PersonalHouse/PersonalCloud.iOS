using System;
using System.Globalization;
using System.IO;
using System.Threading.Tasks;
using Humanizer;
using Photos;
using UIKit;

using Unishare.Apps.DarwinCore;

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

        public void Update(PHAsset photo)
        {
            IconImage.Image = null;
            IconImage.ContentMode = UIViewContentMode.ScaleAspectFill;
            NameLabel.Text = photo.CreationDate.ToDateTime().ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
            TypeLabel.Text = photo.MediaType switch
            {
                PHAssetMediaType.Audio => "声音",
                PHAssetMediaType.Image => "照片",
                PHAssetMediaType.Video => "视频",
                _ => "未知媒体类型"
            };
            SizeLabel.Text = null;

            PHImageManager.DefaultManager.RequestImageForAsset(photo, IconImage.Frame.Size, PHImageContentMode.AspectFill, null, (result, info) => {
                var error = info.ObjectForKey(PHImageKeys.Error);
                if (error == null) InvokeOnMainThread(() => IconImage.Image = result);
            });

            Task.Run(() => {
                var resources = PHAssetResource.GetAssetResources(photo);
                long size = 0;
                foreach (var resource in resources)
                {
                    if (resource.ResourceType == PHAssetResourceType.Photo || resource.ResourceType == PHAssetResourceType.Video)
                    {
                        InvokeOnMainThread(() => NameLabel.Text = Path.GetFileNameWithoutExtension(resource.OriginalFilename));
                    }
                    size += resource.UserInfoGetSize() ?? 0;
                }
                InvokeOnMainThread(() => SizeLabel.Text = size.Bytes().Humanize("0.00"));
            });
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