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

using Sentry;
using Sentry.Protocol;

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

namespace NSPersonalCloud.DarwinMobile
{

    [Register("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate
    {
        public UIWindow Window { get; private set; }

        private CFNotificationObserverToken networkNotification;
        ILogger logger;

        [Export("application:willFinishLaunchingWithOptions:")]
        public bool WillFinishLaunching(UIApplication application, NSDictionary launchOptions)
        {
            SQLitePCL.Batteries_V2.Init();
            var appVersion = application.GetBundleVersion();

            AppCenter.Start("60ed8f1c-4c08-4598-beef-c169eb0c2e53", typeof(Analytics), typeof(Crashes));
            Globals.Loggers = new LoggerFactory().AddSentry(config => {
                config.Dsn = "https://d0a8d714e2984642a530aa7deaca3498@o209874.ingest.sentry.io/5174354";
                config.Environment = "iOS";
                config.Release = appVersion;
            });
            logger = Globals.Loggers.CreateLogger<AppDelegate>();


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

            if (networkNotification == null)
            {
                networkNotification = CFNotificationCenter.Darwin.AddObserver(Notifications.NetworkChange, null, ObserveNetworkChange, CFNotificationSuspensionBehavior.Coalesce);
            }



            return true;
        }

        [Export("applicationWillEnterForeground:")]
        public void WillEnterForeground(UIApplication application)
        {
            try
            {
                networkNotification = CFNotificationCenter.Darwin.AddObserver(Notifications.NetworkChange, null, ObserveNetworkChange, CFNotificationSuspensionBehavior.Coalesce);
                Globals.CloudManager?.StartNetwork(false);
            }
            catch
            {
                // Ignored.
            }
        }

        [Export("applicationDidEnterBackground:")]
        public void DidEnterBackground(UIApplication application)
        {
            try
            {
                if (networkNotification != null)
                {
                    CFNotificationCenter.Darwin.RemoveObserver(networkNotification);
                    networkNotification = null;
                }
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
                var tdelay = Task.Delay(25 * 1000);
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
                    completionHandler?.Invoke(UIBackgroundFetchResult.NewData);
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

            SentrySdk.AddBreadcrumb("Background App Refresh triggered.");

            var cloud = Globals.CloudManager.PersonalClouds?[0];
            if (cloud == null)
            {
                SentrySdk.CaptureMessage("Backup triggered while no Personal Cloud configured.", SentryLevel.Error);
                backgroundStatus = UIBackgroundFetchResult.Failed;
                return;
            }

            var worker = Globals.BackupWorker;
            if (worker == null)
            {
                SentrySdk.CaptureMessage("Photo sync worker not initialized.", SentryLevel.Error);
                backgroundStatus = UIBackgroundFetchResult.NoData;
                return;
            }


            try
            {
                Globals.CloudManager.NetworkRefeshNodes();
            }
            catch (Exception exception)
            {
                SentrySdk.CaptureMessage("Exception occurred while refreshing network status or waiting for response.", SentryLevel.Error);
                SentrySdk.CaptureException(exception);
                backgroundStatus = UIBackgroundFetchResult.NoData;
                return;
            }


            var path = Globals.Database.LoadSetting(UserSettings.PhotoBackupPrefix);
            if (string.IsNullOrEmpty(path))
            {
                SentrySdk.CaptureMessage("Photo sync not configured.", SentryLevel.Error);
                backgroundStatus = UIBackgroundFetchResult.Failed;
                return;
            }

            try
            {
                WaitForPath(cloud, path);

            }
            catch (Exception exception)
            {
                SentrySdk.CaptureMessage("Exception occurred while wait for node appearing when backup photos.", SentryLevel.Error);
                SentrySdk.CaptureException(exception);
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

        private void ObserveNetworkChange(string name, NSDictionary userInfo)
        {
            if (name != Notifications.NetworkChange) return;

            try { Globals.CloudManager?.StartNetwork(false); }
            catch { } // Ignored.            
        }
    }
}

