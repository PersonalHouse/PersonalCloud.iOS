using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.FileSystem;

using Photos;

using Sentry;
using Sentry.Protocol;

using Unishare.Apps.Common;
using Unishare.Apps.DarwinCore;
using Unishare.Apps.DarwinCore.Models;

namespace Unishare.Apps.DarwinMobile
{
    public class PhotoLibraryExporter
    {
        public Task<int> BackupTask { get; private set; }
        public IReadOnlyList<PLAsset> Photos { get; private set; }

        public PhotoLibraryExporter()
        {
            Refresh();
        }

        public void Refresh()
        {
            if (PHPhotoLibrary.AuthorizationStatus != PHAuthorizationStatus.Authorized)
            {
                Photos = null;
                return;
            }

            var collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
            var photos = collections.OfType<PHAssetCollection>().SelectMany(x => PHAsset.FetchAssets(x, null).OfType<PHAsset>().Select(x => {
                var asset = new PLAsset { Asset = x };
                asset.Refresh();
                return asset;
            })).ToList();

            foreach (var asset in Globals.Database.Table<PLAsset>())
            {
                if (photos.Contains(asset)) photos.Remove(asset);
            }
            Photos = photos.AsReadOnly();
        }

        public Task<int> StartBackup(IFileSystem fileSystem, string pathPrefix)
        {
            SentrySdk.AddBreadcrumb("Starting photos backup job...");
            CreateBackupTask(fileSystem, pathPrefix);
            return BackupTask;
        }

        private void CreateBackupTask(IFileSystem fileSystem, string pathPrefix)
        {
            if (Photos == null || Photos.Count == 0)
            {
                SentrySdk.AddBreadcrumb("No photos to backup.");
                BackupTask = Task.FromResult(0);
                return;
            }

            BackupTask = Task.Run(async () => {
                var remotePath = Path.Combine(pathPrefix, Globals.Database.LoadSetting(UserSettings.DeviceName), "Photo Library/");
                try { await fileSystem.CreateDirectoryAsync(remotePath).ConfigureAwait(false); }
                catch
                {
                    SentrySdk.AddBreadcrumb("Remote directory is inaccessible or already exists.");
                }

                try
                {
                    await fileSystem.EnumerateChildrenAsync(remotePath).ConfigureAwait(false);
                }
                catch
                {
                    SentrySdk.AddBreadcrumb("Remote directory is inaccessible. Backup failed.", level: BreadcrumbLevel.Error);
                    throw;
                }

                var failures = new List<PLAsset>(Photos.Count);
                foreach (var photo in Photos)
                {
                    SentrySdk.AddBreadcrumb($"Backing up item: {photo.FileName}");

                    var zipFile = Path.Combine(PathHelpers.Cache, photo.FileName + ".zip");
                    var originalFile = Path.Combine(PathHelpers.Cache, photo.FileName);

                    var zipStream = new FileStream(zipFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);
                    var originalStream = new FileStream(originalFile, FileMode.Create, FileAccess.ReadWrite, FileShare.None);

                    var package = new SinglePhotoPackage(photo);

                    try
                    {
                        package.WriteArchive(zipStream, originalStream);
                        var newZipFile = Path.Combine(remotePath, Path.GetFileNameWithoutExtension(photo.FileName) + ".temp");
                        var newOriginalFile = Path.Combine(remotePath, photo.FileName);

                        zipStream.Seek(0, SeekOrigin.Begin);
                        await fileSystem.WriteFileAsync(newZipFile, zipStream).ConfigureAwait(false);
                        await fileSystem.RenameAsync(newZipFile, Path.Combine(remotePath, Path.GetFileNameWithoutExtension(photo.FileName) + ".PLAsset")).ConfigureAwait(false);
                        originalStream.Seek(0, SeekOrigin.Begin);
                        await fileSystem.WriteFileAsync(newOriginalFile, originalStream).ConfigureAwait(false);

                        Globals.Database.Insert(photo);
                    }
                    catch (Exception exception)
                    {
                        SentrySdk.AddBreadcrumb($"Backup failed for item: {photo.FileName}", level: BreadcrumbLevel.Error);
                        SentrySdk.CaptureException(exception);
                        zipStream.Dispose();
                        originalStream.Dispose();
                        failures.Add(photo);
                    }

                    try
                    {
                        File.Delete(zipFile);
                        File.Delete(originalFile);
                    }
                    catch
                    {
                        // Ignored.
                    }
                }

                SentrySdk.AddBreadcrumb($"Backup finished: {failures.Count} failures.");
                var difference = Photos.Count - failures.Count;
                Photos = failures.AsReadOnly();
                return difference;
            });
        }
    }
}
