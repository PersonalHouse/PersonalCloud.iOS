using System;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using CoreFoundation;

using Foundation;

using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
using Microsoft.Extensions.Logging;

using NSPersonalCloud;

using Photos;

using SQLite;

using UIKit;

using NSPersonalCloud.Common;
using NSPersonalCloud.Common.Models;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.DarwinCore.Models;
using System.Threading;
using System.Globalization;
using System.Runtime.CompilerServices;
using NSPersonalCloud.Interfaces.FileSystem;
using System.Net.Http;

namespace NSPersonalCloud.DarwinMobile
{

    [Register("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate
    {
        public UIWindow Window { get; private set; }

        ILogger logger;

        [Export("application:willFinishLaunchingWithOptions:")]
        public bool WillFinishLaunching(UIApplication application, NSDictionary launchOptions)
        {
            //Sentry.SentrySdk.Init("https://d0a8d714e2984642a530aa7deaca3498@o209874.ingest.sentry.io/5174354");

            AppCenter.Start("60ed8f1c-4c08-4598-beef-c169eb0c2e53", typeof(Analytics), typeof(Crashes));
            SQLitePCL.Batteries_V2.Init();
            var appVersion = application.GetBundleVersion();
            Globals.Loggers = new LoggerFactory().AddSentry(config => {
                config.Dsn = "https://d0a8d714e2984642a530aa7deaca3498@o209874.ingest.sentry.io/5174354";
                config.Environment = "iOS";
                config.Release = appVersion;
            });
            logger = Globals.Loggers.CreateLogger<AppDelegate>();



            //Globals.Loggers = new LoggerFactory();
            //logger = Globals.Loggers.CreateLogger<AppDelegate>();


            var databasePath = Path.Combine(Paths.SharedLibrary, "Preferences.sqlite3");
            Globals.Database = new SQLiteConnection(databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
            Globals.Database.CreateTable<KeyValueModel>();
            Globals.Database.CreateTable<CloudModel>();
            Globals.Database.CreateTable<PLAsset>();
            Globals.Database.CreateTable<AlibabaOSS>();
            Globals.Database.CreateTable<AzureBlob>();
            Globals.Database.CreateTable<WebApp>();

            if (Globals.Database.Find<KeyValueModel>(UserSettings.EnableSharing) is null)
            {
                Globals.Database.SaveSetting(UserSettings.EnableSharing, "1");
            }

            var sharingEnabled = false;
            if (Globals.Database.CheckSetting(UserSettings.EnableSharing, "1"))
            {
                sharingEnabled = true;
                UIApplication.SharedApplication.IdleTimerDisabled = true;
            }

            Paths.CreateCommonDirectories();

            SetupFS(sharingEnabled);

            if (PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                Globals.Database.CheckSetting(UserSettings.AutoBackupPhotos, "1"))
            {
                Globals.BackupWorker = new PhotoLibraryExporter();
                currentBackupTask = null;
                _ = Globals.BackupWorker.Init();
            }

            var appsPath = Paths.WebApps;
            Directory.CreateDirectory(appsPath);
            Globals.Storage = new AppleDataStorage();
            Globals.CloudManager = new PCLocalService(Globals.Storage, Globals.Loggers, Globals.FileSystem, appsPath);
            Task.Run(async () => {
                if (!Globals.Database.CheckSetting(UserSettings.LastInstalledVersion, appVersion))
                {

                    await Globals.CloudManager.InstallApps().ConfigureAwait(false);
                    Globals.Database.SaveSetting(UserSettings.LastInstalledVersion, appVersion);
                }

                Globals.CloudManager.StartService();
            });


            return true;
        }

        public static void SetupFS(bool sharingEnabled)
        {
            Zio.IFileSystem fs;
            var rootfs = new Zio.FileSystems.PhysicalFileSystem();
            Zio.IFileSystem fsfav;
            if (sharingEnabled)
            {
                fsfav = new Zio.FileSystems.SubFileSystem(rootfs, Paths.Documents);
            }
            else
            {
                fsfav = new Zio.FileSystems.MemoryFileSystem();
            }
            if (PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                Globals.Database.CheckSetting(UserSettings.EnbalePhotoSharing, "1"))
            {
                var mfs = new Zio.FileSystems.MountFileSystem(fsfav, true);
                mfs.Mount("/" + Unishare.Apps.DarwinCore.PhotoFileSystem.FolderName, new Unishare.Apps.DarwinCore.PhotoFileSystem());
                fs = mfs;
            }
            else
            {
                fs = fsfav;
            }
            Globals.SetupFS(fs);
        }

        private void PingMainPage()
        {
            Task.Run(async () => {
                try
                {
                    using (var client = new HttpClient())
                    using (var request = new HttpRequestMessage(HttpMethod.Head, "https://Personal.House"))
                    {
                        await client.SendAsync(request).ConfigureAwait(false);
                    }
                }
                catch
                {
                    // Ignored.
                }
            });
        }

        private void MonitorNetwork()
        {
            Reachability.InternetConnectionStatus();
            Reachability.LocalWifiConnectionStatus();
            Reachability.RemoteHostStatus();
            SystemConfiguration.NetworkReachabilityFlags? prenet = null;
            Reachability.ReachabilityChanged += args => {
                if (prenet != null)
                {
                    if (prenet!= args)
                    {
                        Task.Run(() => {
                            try { Globals.CloudManager?.NetworkMayChanged(true); }
                            catch { } // Ignored.
                        });
                    }
                }
                prenet = args;
            };
        }

        [Export("application:didFinishLaunchingWithOptions:")]
        public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            if (Globals.Database.Table<CloudModel>().Count() > 0)
            {
                Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("MainScreen");
                if (PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                    Globals.Database.CheckSetting(UserSettings.AutoBackupPhotos, "1"))
                {
                    application.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalMinimum);
                }
            }
            else
            {
                Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("WelcomeScreen");
                application.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalNever);
            }
            Window.MakeKeyAndVisible();

            MonitorNetwork();


            return true;
        }

        [Export("applicationWillEnterForeground:")]
        public void WillEnterForeground(UIApplication application)
        {
            try
            {
                Globals.CloudManager?.NetworkMayChanged(false);
            }
            catch
            {
                // Ignored.
            }
        }

        [Export("applicationWillTerminate:")]
        public void WillTerminate(UIApplication application)
        {
            Globals.CloudManager?.Dispose();
            Globals.Database?.Dispose();
            Globals.Loggers?.Dispose();
        }

        #region Background App Refresh

        UIBackgroundFetchResult backgroundStatus;
        Task currentBackupTask;

        [Export("application:performFetchWithCompletionHandler:")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        public void PerformFetch(UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            try
            {
                var tdelay = Task.Delay(21 * 1000);
                backgroundStatus = UIBackgroundFetchResult.NewData;
                if (currentBackupTask == null)
                {
                    currentBackupTask = Task.Run(BackgroundBackupImages);
                }

                var res = Task.WhenAny(new[] { tdelay, currentBackupTask }).Result;
                if (res == currentBackupTask)
                {
                    currentBackupTask = null;
                    completionHandler?.Invoke(backgroundStatus);
                    return;
                }
                completionHandler?.Invoke(UIBackgroundFetchResult.NewData);
            }
            catch (Exception e)
            {
                logger.LogError(e, "PerformFetch");
                try
                {
                    completionHandler?.Invoke(UIBackgroundFetchResult.Failed);
                }
                catch
                {
                }
            }
        }

        private void WaitForPath(PersonalCloud cloud, string path)
        {
            using var cts = new CancellationTokenSource();
            cts.CancelAfter(27 * 1000);
            var pathsegs = path.Split(new char[] { '\\', '/' }, StringSplitOptions.RemoveEmptyEntries);
            if (pathsegs?.Length > 0)
            {
                var rootnodetofind = pathsegs[0];
                for (int i = 0; i < 1000; i++)
                {
                    var nodes = cloud.RootFS.EnumerateChildrenAsync("/").AsTask().Result;
                    if (nodes.Any(x => string.Compare(x.Name, rootnodetofind, true, CultureInfo.InvariantCulture) == 0))
                    {
                        return;
                    }
                    cts.Token.ThrowIfCancellationRequested();
                    Thread.Sleep(500);
                }
            }
            throw new InvalidDataException("Couldn't backup images to personal cloud root, which is readonly");
        }

        async Task BackgroundBackupImages()
        {

            logger.LogTrace("Background App Refresh triggered.");

            var cloud = Globals.CloudManager.PersonalClouds?[0];
            if (cloud == null)
            {
                logger.LogError("Backup triggered while no Personal Cloud configured.");
                backgroundStatus = UIBackgroundFetchResult.Failed;
                return;
            }

            var worker = Globals.BackupWorker;
            if (worker == null)
            {
                logger.LogError("Photo sync worker not initialized.");
                backgroundStatus = UIBackgroundFetchResult.NoData;
                return;
            }


            var path = Globals.Database.LoadSetting(UserSettings.PhotoBackupPrefix);
            if (string.IsNullOrEmpty(path))
            {
                logger.LogError("Photo sync not configured.");
                backgroundStatus = UIBackgroundFetchResult.Failed;
                return;
            }

            try
            {
                WaitForPath(cloud, path);

            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception occurred while wait for node appearing when backup photos.");
                backgroundStatus = UIBackgroundFetchResult.NoData;
                return;
            }

            try
            {
                var items = await worker.StartBackup(cloud.RootFS, path, true).ConfigureAwait(false);

                backgroundStatus = UIBackgroundFetchResult.NoData;
            }
            catch
            {
                backgroundStatus = UIBackgroundFetchResult.Failed;
            }
        }


        #endregion

    }
}

