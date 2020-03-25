using System.IO;
using System.Threading.Tasks;

using Foundation;

using Microsoft.Extensions.Logging;

using NSPersonalCloud;

using Photos;

#if !DEBUG
using Microsoft.AppCenter;
using Microsoft.AppCenter.Analytics;
using Microsoft.AppCenter.Crashes;
#endif

using SQLite;

using UIKit;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;

using CoreFoundation;

using System;

using Unishare.Apps.Common.Models;

using Sentry;
using Sentry.Protocol;

namespace Unishare.Apps.DarwinMobile
{
    [Register("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate
    {
        public UIWindow Window { get; private set; }

        private IDisposable sentry;
        private CFNotificationObserverToken networkNotification;

        [Export("application:didFinishLaunchingWithOptions:")]
        public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
#if !DEBUG
            AppCenter.Start("60ed8f1c-4c08-4598-beef-c169eb0c2e53", typeof(Analytics), typeof(Crashes));
#endif
            sentry = SentrySdk.Init(options => {
                options.Dsn = new Dsn("https://d0a8d714e2984642a530aa7deaca3498@sentry.io/5174354");
                options.Release = application.GetBundleVersion();
            });
            Globals.Loggers = new LoggerFactory().AddSentry(options => {
                options.Dsn = "https://d0a8d714e2984642a530aa7deaca3498@sentry.io/5174354";
                options.InitializeSdk = false;
            });

            SQLitePCL.Batteries_V2.Init();

            var databasePath = Path.Combine(PathHelpers.SharedLibrary, "Preferences.sqlite3");
            Globals.Database = new SQLiteConnection(databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
            Globals.Database.CreateTable<KeyValueModel>();
            Globals.Database.CreateTable<CloudModel>();
            Globals.Database.CreateTable<NodeModel>();

            SentrySdk.ConfigureScope(scope => {
                scope.User = new User {
                    Username = UIDevice.CurrentDevice.Name
                };
                var id = Globals.Database.LoadSetting(UserSettings.DeviceId);
                if (!string.IsNullOrEmpty(id)) scope.User.Id = id;
            });

            if (Globals.Database.Find<KeyValueModel>(UserSettings.EnableSharing) is null)
            {
                Globals.Database.SaveSetting(UserSettings.EnableSharing, "1");
            }

            var sharingEnabled = false;
            if (Globals.Database.LoadSetting(UserSettings.EnableSharing) == "1")
            {
                sharingEnabled = true;
                UIApplication.SharedApplication.IdleTimerDisabled = true;
            }

            try
            {
                if (!Directory.Exists(PathHelpers.Cache)) Directory.CreateDirectory(PathHelpers.Cache);
                Directory.CreateDirectory(PathHelpers.SharedContainer);
            }
            catch
            {
                // Ignore.
            }

            Globals.FileSystem = new SandboxedFileSystem(sharingEnabled ? PathHelpers.Documents : null);

            if (PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized &&
                Globals.Database.CheckSetting(UserSettings.EnablePhotoSync, "1"))
            {
                Globals.FileSystem.ArePhotosShared = true;
            }

            Globals.Storage = new AppleDataStorage();
            Globals.CloudManager = new PCLocalService(Globals.Storage, Globals.Loggers, Globals.FileSystem);
            Task.Run(() => Globals.CloudManager.StartService());

            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            if (Globals.Database.Table<CloudModel>().Count() > 0)
            {
                Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("MainScreen");
                UIApplication.SharedApplication.SetMinimumBackgroundFetchInterval(TimeSpan.FromHours(1).TotalSeconds);
            }
            else
            {
                Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("WelcomeScreen");
                application.SetMinimumBackgroundFetchInterval(UIApplication.BackgroundFetchIntervalNever);
            }
            Window.MakeKeyAndVisible();

            try
            {
                networkNotification = CFNotificationCenter.Darwin.AddObserver(Notifications.NetworkChange, null, ObserveNetworkChange, CFNotificationSuspensionBehavior.DeliverImmediately);
            }
            catch
            {
                // Network monitoring call can fail.
            }

            return true;
        }

        [Export("applicationWillEnterForeground:")]
        public void WillEnterForeground(UIApplication application)
        {
            try
            {
                Globals.CloudManager?.StartNetwork(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }

        [Export("applicationWillTerminate:")]
        public void WillTerminate(UIApplication application)
        {
            try
            {
                if (networkNotification != null) CFNotificationCenter.Darwin.RemoveObserver(networkNotification);
            }
            catch
            {
                // Network monitoring may have failed. Ignore.
            }

            Globals.CloudManager?.Dispose();
            Globals.Database?.Dispose();
            Globals.Loggers?.Dispose();
            sentry?.Dispose();
        }

        #region Background App Refresh

        [Export("application:performFetchWithCompletionHandler:")]
        public void PerformFetch(UIApplication application, Action<UIBackgroundFetchResult> completionHandler)
        {
            SentrySdk.AddBreadcrumb($"HasLocalServiceStarted: {Globals.CloudManager?.PersonalClouds?.Count != null}", level: BreadcrumbLevel.Warning);
            SentrySdk.AddBreadcrumb($"IsDatabaseReadable: {Globals.Database?.Table<CloudModel>()?.Count() != null}", level: BreadcrumbLevel.Warning);
            SentrySdk.AddBreadcrumb($"IsCloudFileSystemReady: {Globals.FileSystem?.RootPath != null}", level: BreadcrumbLevel.Warning);
            SentrySdk.CaptureMessage("Background App Refresh triggered.", SentryLevel.Warning);
            completionHandler?.Invoke(UIBackgroundFetchResult.NewData);
        }

        #endregion

        private void ObserveNetworkChange(string name, NSDictionary userInfo)
        {
            if (name != Notifications.NetworkChange) return;

            try
            {
                Globals.CloudManager?.StartNetwork(false);
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}

