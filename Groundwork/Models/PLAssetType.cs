using System;

namespace Unishare.Apps.DarwinCore.Models
{
    public enum PLAssetType : long
    {
        Unknown,
        Image,
        Video,
        Audio
    }

    public static class PLAssetTypeExtensions
    {
        public static string ToChineseString(this PLAssetType type)
        {
            return type switch
            {
                PLAssetType.Image => "照片",
                PLAssetType.Video => "视频",
                PLAssetType.Audio => "音乐",
                _ => "未知格式"
            };
        }
    }
}
