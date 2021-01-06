using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading.Tasks;

using NSPersonalCloud.Interfaces.FileSystem;

using Photos;

using NSPersonalCloud.Common;
using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.DarwinCore.Models;
using Foundation;
using AVFoundation;
using WebKit;
using Microsoft.Extensions.Logging;

namespace NSPersonalCloud.DarwinMobile
{
    public class PhotoLibraryExporter
    {
        public Task<int> BackupTask { get; private set; }
        public Lazy<IEnumerable<PLAsset>> Photos { get; private set; }
        public ILogger logger;

        public PhotoLibraryExporter()
        {
            Photos = new Lazy<IEnumerable<PLAsset>>(() => {
                return GetPhotos();
            });
            logger = Globals.Loggers.CreateLogger("PhotoLibraryExporter");
        }

        public async Task Init()
        {
            await Task.Run(() => {
                _ = Photos.Value;
            }).ConfigureAwait(false);
        }
        static private IEnumerable<PLAsset> GetPhotos()
        {
            if (PHPhotoLibrary.AuthorizationStatus != PHAuthorizationStatus.Authorized)
            {
                yield break;
                //return null;
            }

            var collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
            var photos = collections.OfType<PHAssetCollection>().SelectMany(x => PHAsset.FetchAssets(x, null).OfType<PHAsset>().Select(x => {
                var asset = new PLAsset { Asset = x };
                return asset;
            })).ToList();

            foreach (var asset in Globals.Database.Table<PLAsset>())
            {
                if (photos.Contains(asset)) photos.Remove(asset);
            }
            foreach (var item in photos)
            {
                item.Refresh();
                yield return item;
            }
            //return photos.AsReadOnly();
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        public Task<int> StartBackup(IFileSystem fileSystem, string pathPrefix, bool background)
        {
            logger.LogTrace("Starting photos backup job...");
            CreateBackupTask(fileSystem, pathPrefix, background);
            return BackupTask;
        }



        async Task WriteToDest(byte[] bytes, long datawritten, long datalen, string path, IFileSystem fileSystem)
        {

            using var ms = new MemoryStream(bytes, 0, (int) datalen);
            for (int i = 0; i < 3; i++)
            {
                try
                {
                    ms.Seek(0, SeekOrigin.Begin);
                    if (datawritten == 0)
                    {
                        await fileSystem.WriteFileAsync(path, ms).ConfigureAwait(false);
                    }
                    else
                    {
                        await fileSystem.WritePartialFileAsync(path, datawritten, datalen, ms).ConfigureAwait(false);
                    }
                    break;
                }
                catch
                {
                    if (i >= 2)
                    {
                        throw;
                    }
                }
            }
        }

        public async Task<bool> CopyToDestination(PHAsset photo, string destPath, IFileSystem fileSystem, DateTime ft)
        {
            try
            {
                var origfilepath = await photo.GetIOSFilePath().ConfigureAwait(false);
                if (string.IsNullOrWhiteSpace(origfilepath))
                {
                    return false;
                }
                var fs = new FileStream(origfilepath, FileMode.Open, FileAccess.Read);

                return await CopyToDestination(fs, destPath, fileSystem, ft).ConfigureAwait(false);

            }
            catch (Exception)
            {
                return false;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "Lookup requires lowercase.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        public async Task<bool> CopyToDestination(PLAsset photo, string destPath, IFileSystem fileSystem, DateTime filetime)
        {
            try
            {
                if (!photo.IsAvailable) throw new InvalidOperationException("This photo is no longer available.");

                var options = new PHAssetResourceRequestOptions { NetworkAccessAllowed = true };

                NSError lastError = null;
                var original = photo.Resources.FirstOrDefault(x => x.ResourceType == PHAssetResourceType.FullSizeVideo ||
                                                                   x.ResourceType == PHAssetResourceType.FullSizePhoto ||
                                                                   x.ResourceType == PHAssetResourceType.Photo ||
                                                                   x.ResourceType == PHAssetResourceType.Video ||
                                                                   x.ResourceType == PHAssetResourceType.Audio);

                if (original == null) throw new InvalidOperationException("Backup failed for this photo.");

                var destTmpFile = destPath + ".temp";

                var tcs = new TaskCompletionSource<int>();
                long datawritten = 0;
                PHAssetResourceManager.DefaultManager.RequestData(original, options, data => {
                    var bytes = data.ToArray();
                    WriteToDest(bytes, datawritten, bytes.Length, destTmpFile, fileSystem).Wait();
                    datawritten += bytes.Length;
                }, error => {
                    if (error != null)
                    {
                        tcs.SetException(new InvalidOperationException(lastError.LocalizedDescription));
                    }
                    tcs.SetResult(0);
                });
                await tcs.Task.ConfigureAwait(false);


                return await RenameDestinationFile(fileSystem, destTmpFile, destPath, filetime).ConfigureAwait(false);


            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> RenameDestinationFile(IFileSystem fileSystem, string origname, string newname, DateTime filetime)
        {
            var finalpath = newname;
            try
            {
                await fileSystem.RenameAsync(origname, finalpath).ConfigureAwait(false);
            }
            catch
            {
                try
                {
                    var guid = Guid.NewGuid().ToString("N");
                    finalpath = newname + $".{guid}{Path.GetExtension(newname)}";
                    await fileSystem.RenameAsync(origname, finalpath).ConfigureAwait(false);
                }
                catch
                {
                }
            }

            try
            {
                await fileSystem.SetFileTimeAsync(finalpath, filetime, filetime, filetime).ConfigureAwait(false);
            }
            catch
            {
            }
            return true;
        }

        private async Task<bool> CopyToDestination(Stream fs, string destPath, IFileSystem fileSystem, DateTime filetime)
        {
            try
            {
                const int bufcnt = 2 * 1024 * 1024;


                var destTmpFile = destPath + ".pc.temp";
                long datawritten = 0;
                try
                {
                    var t = await fileSystem.ReadMetadataAsync(destTmpFile).ConfigureAwait(false);
                    datawritten = t.Size.Value - (t.Size.Value % bufcnt);
                    if (datawritten != 0)
                    {
                        fs.Seek(datawritten, SeekOrigin.Begin);
                    }
                    else
                    {
                        await fileSystem.DeleteAsync(destTmpFile, true).ConfigureAwait(false);
                    }
                }
                catch
                {
                }

                var buf = new byte[bufcnt];
                while (true)
                {
                    var read = await fs.ReadAsync(buf, 0, bufcnt).ConfigureAwait(false);
                    if (read == 0)
                    {
                        break;
                    }
                    await WriteToDest(buf, datawritten, read, destTmpFile, fileSystem).ConfigureAwait(false);
                    datawritten += read;
                    if (datawritten >= 1642070016L)
                    {
                        Console.WriteLine(fs.Length);
                    }
                }
                return await RenameDestinationFile(fileSystem, destTmpFile, destPath, filetime).ConfigureAwait(false);

            }
            catch (Exception)
            {
                return false;
            }
        }

        private async Task<bool> BackupOneImage(PLAsset photo, string remotePath, IFileSystem fileSystem, bool background)//or video
        {

            try
            {
                logger.LogTrace($"Backing up item: {photo.FileName}");

                var zipFile = Path.Combine(Paths.Temporary, photo.FileName + ".zip");
                File.Delete(zipFile);

                FileStream zipStream = null;
                SinglePhotoPackage package = null;

                var ft = photo.CreationDate;

                package = new SinglePhotoPackage(photo);

                if (photo.Resources.Count > 1)
                {
                    zipStream = new FileStream(zipFile, FileMode.OpenOrCreate, FileAccess.ReadWrite, FileShare.None);
                }


                try
                {
                    if (zipStream != null)
                    {
                        package.WriteArchive(zipStream);

                        zipStream.Seek(0, SeekOrigin.Begin);
                        var destZipFile = Path.Combine(remotePath, Path.GetFileNameWithoutExtension(photo.FileName) + ".PLAsset");
                        if (!await CopyToDestination(zipStream, destZipFile, fileSystem, ft).ConfigureAwait(false))
                        {
                            return false;
                        }
                    }


                    var destOriginalFile = Path.Combine(remotePath, photo.FileName);
                    if (background && (photo.Size > 30L * 1024 * 1024))
                    {
                        if (!await CopyToDestination(photo.Asset, destOriginalFile, fileSystem, ft).ConfigureAwait(false))
                        {
                            return false;
                        }
                    }
                    else
                    {
                        if (!await CopyToDestination(photo, destOriginalFile, fileSystem, ft).ConfigureAwait(false))
                        {
                            return false;
                        }
                    }

                    Globals.Database.Insert(photo);
                }
                catch (Exception exception)
                {
                    logger.LogError(exception, $"Backup failed for item: {photo.FileName}");
                }
                finally
                {
                    zipStream?.Dispose();
                }

                try
                {
                    File.Delete(zipFile);
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

        private async Task<int> Backup(string pathPrefix, IFileSystem fileSystem, bool background)
        {
            try
            {
                var remotePath = Path.Combine(pathPrefix, Globals.Database.LoadSetting(UserSettings.DeviceName), "Photos/");
                try { await fileSystem.CreateDirectoryAsync(remotePath).ConfigureAwait(false); }
                catch
                {
                    logger.LogTrace("Remote directory is inaccessible or already exists.");
                }

                try
                {
                    await fileSystem.EnumerateChildrenAsync(remotePath).ConfigureAwait(false);
                }
                catch
                {
                    logger.LogError("Remote directory is inaccessible. Backup failed.");
                    throw;
                }

                var failures = new List<PLAsset>();
                int backedupcount = 0;
                foreach (var photo in Photos.Value)
                {
                    if (await BackupOneImage(photo, remotePath, fileSystem, background).ConfigureAwait(false) == false)
                    {
                        failures.Add(photo);
                    }else
                    {
                        ++backedupcount;
                    }

                }

                logger.LogError($"Backup finished: {failures.Count} failures.");

                lock (this)
                {
                    Photos = new Lazy<IEnumerable<PLAsset>>(failures.AsReadOnly());
                }

                return backedupcount;
            }
            catch (Exception exception)
            {
                logger.LogError(exception, "Exception occurred when backup photos.");
                throw;
            }
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        private void CreateBackupTask(IFileSystem fileSystem, string pathPrefix, bool background)
        {
            if (Photos.Value == null)
            {
                logger.LogTrace("No photos to backup.");
                BackupTask = Task.FromResult(0);
                return;
            }
            if (Photos.Value.Any())
            {
                lock (this)
                {
                    Photos = new Lazy<IEnumerable<PLAsset>>(() => {
                        return GetPhotos();
                    });
                }

            }

            BackupTask = Task.Run(async () => {
                return await Backup(pathPrefix, fileSystem, background).ConfigureAwait(false);
            });
        }
    }
}
