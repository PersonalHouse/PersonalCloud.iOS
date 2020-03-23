using System;
using System.IO;

using Foundation;

namespace Unishare.Apps.DarwinCore
{
    public static partial class PathHelpers
    {
        public static string SharedDocuments =>
            Path.Combine(NSFileManager.DefaultManager.GetContainerUrl("group.com.daoyehuo.Unishare").Path, "Documents");

        public static string SharedLibrary =>
            Path.Combine(NSFileManager.DefaultManager.GetContainerUrl("group.com.daoyehuo.Unishare").Path, "Library");

        public static string Documents =>
            Environment.GetFolderPath(Environment.SpecialFolder.Personal);

        public static string Cache =>
            NSFileManager.DefaultManager.GetTemporaryDirectory().Path;

        public static string SharedContainer => Path.Combine(Documents, "Favorites");

        public static string PhotoRestore =>
            Path.Combine(Cache, "Restore");

        // public static string Favorites => Path.Combine(SharedDocuments, "Favorites");
    }
}
