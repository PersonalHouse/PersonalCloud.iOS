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

namespace Unishare.Apps.DarwinMobile
{
    [Register("AppDelegate")]
    public class AppDelegate : UIResponder, IUIApplicationDelegate
    {
        public UIWindow Window { get; private set; }

        private CFNotificationObserverToken networkNotification;

        [Export("application:didFinishLaunchingWithOptions:")]
        public bool FinishedLaunching(UIApplication application, NSDictionary launchOptions)
        {
#if !DEBUG
            AppCenter.Start("60ed8f1c-4c08-4598-beef-c169eb0c2e53", typeof(Analytics), typeof(Crashes));
#endif
            SQLitePCL.Batteries_V2.Init();

            var databasePath = Path.Combine(PathHelpers.SharedLibrary, "Preferences.sqlite3");
            Globals.Database = new SQLiteConnection(databasePath, SQLiteOpenFlags.Create | SQLiteOpenFlags.ReadWrite | SQLiteOpenFlags.FullMutex);
            Globals.Database.CreateTable<KeyValueModel>();
            Globals.Database.CreateTable<CloudModel>();
            Globals.Database.CreateTable<NodeModel>();

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
            Globals.CloudManager = new PCLocalService(Globals.Storage, new LoggerFactory(), Globals.FileSystem);
            Task.Run(() => Globals.CloudManager.StartService());

            Window = new UIWindow(UIScreen.MainScreen.Bounds);
            if (Globals.Database.Table<CloudModel>().Count() > 0) Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("MainScreen");
            else Window.RootViewController = UIStoryboard.FromName("Main", NSBundle.MainBundle).InstantiateViewController("WelcomeScreen");
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
                Globals.CloudManager?.ForceNetworkRefesh();
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
        }

        private void ObserveNetworkChange(string name, NSDictionary userInfo)
        {
            if (name != Notifications.NetworkChange) return;

            try
            {
                Globals.CloudManager?.StopNetwork();
                Globals.CloudManager?.StartNetwork();
            }
            catch (Exception exception)
            {
                Console.WriteLine(exception);
            }
        }
    }
}

