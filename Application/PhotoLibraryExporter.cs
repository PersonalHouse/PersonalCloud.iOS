using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.FileSystem;

using Photos;

using Sentry;
using Sentry.Protocol;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.DarwinCore.Models;

namespace NSPersonalCloud.DarwinMobile
{
    public class PhotoLibraryExporter
    {
        public Task<int> BackupTask { get; private set; }
        public Lazy<IReadOnlyList<PLAsset>> Photos { get; private set; }

        public PhotoLibraryExporter()
        {
            Photos = new Lazy<IReadOnlyList<PLAsset>>(() => {
                return GetPhotos();
            });
        }

        public async Task Init()
        {
            await Task.Run(() => {
                _ = Photos.Value;
            }).ConfigureAwait(false);
        }
        static private IReadOnlyList<PLAsset> GetPhotos()
        {
            if (PHPhotoLibrary.AuthorizationStatus != PHAuthorizationStatus.Authorized)
            {
                return null;
            }

            var collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
            var photos = collections.OfType<PHAssetCollection>().SelectMany(x => PHAsset.FetchAssets(x, null).OfType<PHAsset>().Select(x =>
            {
                var asset = new PLAsset { Asset = x };
                asset.Refresh();
                return asset;
            })).ToList();

            foreach (var asset in Globals.Database.Table<PLAsset>())
            {
                if (photos.Contains(asset)) photos.Remove(asset);
            }
            return photos.AsReadOnly();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        public Task<int> StartBackup(IFileSystem fileSystem, string pathPrefix)
        {
            SentrySdk.AddBreadcrumb("Starting photos backup job...");
            CreateBackupTask(fileSystem, pathPrefix);
            return BackupTask;
        }

        private async Task<bool> CopyToDestination(Stream orig, string destPath, IFileSystem fileSystem)
        {
            try
            {
                var destTmpFile = destPath + ".temp";
                await fileSystem.DeleteAsync(destTmpFile, true).ConfigureAwait(false);
                await fileSystem.WriteFileAsync(destTmpFile, orig).ConfigureAwait(false);
                try
                {
                    await fileSystem.RenameAsync(destTmpFile,  destPath ).ConfigureAwait(false);
                }
                catch
                {
                    try
                    {
                        await fileSystem.RenameAsync(destTmpFile,  destPath +$".RenameFailed" ).ConfigureAwait(false);
                    }
                    catch
                    {
                    }
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }

        }

        private async Task<bool> BackupOneImage(PLAsset photo, string remotePath, IFileSystem fileSystem)//or video
        {

            try
            {
                SentrySdk.AddBreadcrumb($"Backing up item: {photo.FileName}");

                var zipFile = Path.Combine(Paths.Temporary, photo.FileName + ".zip");
                var originalFile = Path.Combine(Paths.Temporary, photo.FileName);

                File.Delete(zipFile);
                File.Delete(originalFile);

                FileStream zipStream = null;
                FileStream originalStream = null;
                SinglePhotoPackage package = null;


                originalStream = new FileStream(originalFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                package = new SinglePhotoPackage(photo);

                if (photo.Resources.Count>1)
                {
                    zipStream = new FileStream(zipFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }


                try
                {
                    if (zipStream!=null)
                    {
                        package.WriteArchive(zipStream);

                        zipStream.Seek(0, SeekOrigin.Begin);
                        var destZipFile = Path.Combine(remotePath, Path.GetFileNameWithoutExtension(photo.FileName) + ".PLAsset");
                        if (!await CopyToDestination(zipStream, destZipFile, fileSystem).ConfigureAwait(false))
                        {
                            return false;
                        }
                    }
                    package.CopyToStream(originalStream);

                    
                    originalStream.Seek(0, SeekOrigin.Begin);
                    var destOriginalFile = Path.Combine(remotePath, photo.FileName);
                    if(!await CopyToDestination(originalStream, destOriginalFile, fileSystem).ConfigureAwait(false))
                    {
                        return false;
                    }

                    Globals.Database.Insert(photo);
                }
                catch (Exception exception)
                {
                    SentrySdk.AddBreadcrumb($"Backup failed for item: {photo.FileName}", level: BreadcrumbLevel.Error);
                    SentrySdk.CaptureException(exception);
                }finally
                {
                    zipStream?.Dispose();
                    originalStream?.Dispose();
                }

                try
                {
                    File.Delete(zipFile);
                }
                catch {// Ignored.
                }
                try
                {
                    File.Delete(originalFile);
                }
                catch
                {// Ignored.
                }
                return true;
            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<int> Backup(string pathPrefix, IFileSystem fileSystem)
        {
            try
            {
                var remotePath = Path.Combine(pathPrefix, Globals.Database.LoadSetting(UserSettings.DeviceName), "Photos/");
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

                var failures = new List<PLAsset>(Photos.Value.Count);
                foreach (var photo in Photos.Value)
                {
                    if(await BackupOneImage(photo, remotePath, fileSystem).ConfigureAwait(false) == false)
                    {
                        failures.Add(photo);
                    }

                }

                SentrySdk.AddBreadcrumb($"Backup finished: {failures.Count} failures.");
                var difference = Photos.Value.Count - failures.Count;

                lock (this)
                {
                    Photos = new Lazy<IReadOnlyList<PLAsset>>(failures.AsReadOnly());
                }

                return difference;
            }
            catch (Exception exception)
            {
                SentrySdk.CaptureMessage("Exception occurred when backup photos.", SentryLevel.Error);
                SentrySdk.CaptureException(exception);
                throw;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        private void CreateBackupTask(IFileSystem fileSystem, string pathPrefix)
        {
            if (Photos.Value == null )
            {
                SentrySdk.AddBreadcrumb("No photos to backup.");
                BackupTask = Task.FromResult(0);
                return;
            }
            if (Photos.Value.Count == 0)
            {
                lock (this)
                {
                    Photos = new Lazy<IReadOnlyList<PLAsset>>(() => {
                        return GetPhotos();
                    });
                }

            }

            BackupTask = Task.Run(async () =>
            {
                return await Backup(pathPrefix, fileSystem).ConfigureAwait(false);
            });
        }
    }
}
