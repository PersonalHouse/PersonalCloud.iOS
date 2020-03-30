using Microsoft.Extensions.Logging;

using NSPersonalCloud;

using SQLite;

using Unishare.Apps.DarwinCore;

namespace Unishare.Apps.DarwinMobile
{
    public static class Globals
    {
        public static ILoggerFactory Loggers { get; internal set; }
        public static SandboxedFileSystem FileSystem { get; internal set; }
        public static SQLiteConnection Database { get; internal set; }
        public static AppleDataStorage Storage { get; internal set; }
        public static PCLocalService CloudManager { get; internal set; }
        public static PhotoLibraryExporter BackupWorker { get; internal set; }
    }
}
