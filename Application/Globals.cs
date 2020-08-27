using Microsoft.Extensions.Logging;

using NSPersonalCloud;

using SQLite;

using NSPersonalCloud.DarwinCore;

namespace NSPersonalCloud.DarwinMobile
{
    public static class Globals
    {
        public static ILoggerFactory Loggers { get; internal set; }
        public static Zio.IFileSystem FileSystem { get; internal set; }
        public static SQLiteConnection Database { get; internal set; }
        public static AppleDataStorage Storage { get; internal set; }
        public static PCLocalService CloudManager { get; internal set; }
        public static PhotoLibraryExporter BackupWorker { get; internal set; }
    }
}
