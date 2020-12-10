using Microsoft.Extensions.Logging;

using NSPersonalCloud;

using SQLite;

using NSPersonalCloud.DarwinCore;
using Zio.FileSystems;
using System.IO;

namespace NSPersonalCloud.DarwinMobile
{
    public static class Globals
    {
        public static ILoggerFactory Loggers { get; internal set; }
        public static Zio.IFileSystem FileSystem => _FileSystem;
        private static Zio.IFileSystem _FileSystem;
        public static void SetupFS(Zio.IFileSystem fs)
        {
            _FileSystem = fs;
            if (CloudManager != null)
            {
                CloudManager.FileSystem = _FileSystem;
                CloudManager.StopNetwork();
                CloudManager.StartNetwork(true);
            }
        }

        public static SQLiteConnection Database { get; internal set; }
        public static AppleDataStorage Storage { get; internal set; }
        public static PCLocalService CloudManager { get; internal set; }
        public static PhotoLibraryExporter BackupWorker { get; internal set; }
    }
}
