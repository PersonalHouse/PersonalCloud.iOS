using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud.DarwinCore;

using Photos;

using UIKit;

using Zio;
using Zio.FileSystems;

namespace Unishare.Apps.DarwinCore
{

    public class PhotoFileSystem : FileSystem
    {
        public const string FolderName = "Photos";
        class PHPhotoItem
        {
            internal PHAssetCollection Col;
            internal PHAsset Asset;
            internal PHAssetResource Res;
            internal Lazy<long> Length;
            internal Lazy<string> Path;
            internal string FillPath=>Asset.GetIOSFilePath().Result;
            internal long FillLength()
            {
                var fi = new FileInfo(Path.Value);
                return fi.Length;
            }
        }
        private readonly ReaderWriterLockSlim _globalLock;
        private const string FileNameForFolderConfiguration = "desktop.ini";
        Dictionary<string, PHAssetCollection> _cacheDir;
        Dictionary<string, PHPhotoItem> _cachePhoto;
        bool _bCached;
        public PhotoFileSystem()
        {
            _globalLock = new ReaderWriterLockSlim(LockRecursionPolicy.SupportsRecursion);
            _bCached = false;
        }

        protected override UPath ConvertPathFromInternalImpl(string innerPath)
        {
            return new UPath(innerPath);
        }

        protected override string ConvertPathToInternalImpl(UPath path)
        {
            return path.FullName;
        }

        protected override void CopyFileImpl(UPath srcPath, UPath destPath, bool overwrite)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void CreateDirectoryImpl(UPath path)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void DeleteDirectoryImpl(UPath path, bool isRecursive)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void DeleteFileImpl(UPath path)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override bool DirectoryExistsImpl(UPath path)
        {
            if (path == UPath.Root)
            {
                return true;
            }

            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return true;
                }
                else
                {
                    var npath = path.ToString().ToUpperInvariant();
                    var res = _cachePhoto.Any(x => (x.Key.IndexOf(npath) == 0) && (x.Key.Length != npath.Length));
                    return res;
                }
            }
            finally
            {
                ExitFileSystemShared();
            }
        }

        private void EnterFileSystemShared()
        {
            _globalLock.EnterReadLock();
        }

        private void ExitFileSystemShared()
        {
            _globalLock.ExitReadLock();
        }

        private void EnterFileSystemExclusive()
        {
            _globalLock.EnterWriteLock();
        }

        private void ExitFileSystemExclusive()
        {
            _globalLock.ExitWriteLock();
        }

        protected override IEnumerable<UPath> EnumeratePathsImpl(UPath path, string searchPattern, SearchOption searchOption, SearchTarget searchTarget)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return _cacheDir.Select(x => ((UPath) x.Key).ToAbsolute()).ToList();
                }
                else
                {
                    var npath = path.ToString().ToUpperInvariant();
                    return _cachePhoto.Where(x => x.Key.IndexOf(npath) == 0).Select(x => ((UPath) x.Key).ToAbsolute()).ToList();
                }
            }
            finally
            {
                ExitFileSystemShared();
            }
        }


        protected void Internal_FillCache()
        {
            if (_bCached)
            {
                return;
            }
            _globalLock.EnterWriteLock();


            try
            {
                if (_bCached)
                {
                    return;
                }

                var cacheDir = new Dictionary<string, PHAssetCollection>();
                var cachePhoto = new Dictionary<string, PHPhotoItem>();
                var collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
                foreach (PHAssetCollection asset in collections)
                {
                    cacheDir["/" + asset.LocalizedTitle] = asset;

                    var assets = PHAsset.FetchAssets(asset, null);
                    foreach (PHAsset photo in assets)
                    {
                        var assetResources = PHAssetResource.GetAssetResources(photo);
                        var original = assetResources.FirstOrDefault(x => x.ResourceType == PHAssetResourceType.Video ||
                                                                          x.ResourceType == PHAssetResourceType.Photo ||
                                                                          x.ResourceType == PHAssetResourceType.Audio);

                        if (original == null) continue;

                        var dt = photo.CreationDate.ToDateTime();
                        var dtstr = dt.ToLocalTime().ToString("yyyy-MM-dd HH_mm");
                        var filename = $"{dtstr} {original.OriginalFilename}";
                        UPath up = Path.Combine(asset.LocalizedTitle, filename);
                        var item = new PHPhotoItem {
                            Col = asset,
                            Asset = photo,
                            Res = original
                        };
                        item.Path = new Lazy<string>(item.FillPath);
                        item.Length = new Lazy<long>(item.FillLength);

                        cachePhoto[up.ToAbsolute().ToString().ToUpperInvariant()] = item;

                        var originalName = Path.GetFileNameWithoutExtension(original.OriginalFilename);
                        foreach (var resource in assetResources)
                        {
                            if (resource == original) continue;
                            if (string.IsNullOrEmpty(resource.OriginalFilename)) continue;

                            var extension = Path.GetExtension(resource.OriginalFilename) ?? string.Empty;
                            var fileName = $"{dtstr} {originalName} ({resource.ResourceType:G}){extension}";

                            up = Path.Combine(asset.LocalizedTitle, fileName);


                            item = new PHPhotoItem {
                                Col = asset,
                                Asset = photo,
                                Res = resource
                            };
                            item.Path = new Lazy<string>(item.FillPath);
                            item.Length = new Lazy<long>(item.FillLength);

                            cachePhoto[up.ToAbsolute().ToString().ToUpperInvariant()] = item;
                        }
                    }
                }

                _cacheDir = cacheDir;
                _cachePhoto = cachePhoto;
                _bCached = true;
                Task.Run(() => {
                    Parallel.ForEach(cachePhoto, new ParallelOptions { MaxDegreeOfParallelism = 2 * Environment.ProcessorCount },
                        x => {
                            _ = x.Value.Length.Value;
                            _ = x.Value.Path.Value;
                        });
                });
            }
            catch (Exception )
            {

            }
            finally
            {
                _globalLock.ExitWriteLock();
            }
        }

        protected override bool FileExistsImpl(UPath path)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return true;
                }
                else
                {
                    var npath = path.ToString().ToUpperInvariant();
                    var res = _cachePhoto.Any(x => x.Key == npath);
                    return res;
                }
            }
            finally
            {
                ExitFileSystemShared();
            }
        }

        PHPhotoItem GetFileInfo(UPath path)
        {
            var npath = path.ToString().ToUpperInvariant();
            var n = _cachePhoto.Single(x => x.Key.IndexOf(npath) == 0);
            if (n.Key.Length == npath.Length)
            {
                return n.Value;
            }
            else
            {
                return new PHPhotoItem {
                    Col = n.Value.Col
                };
            }
        }

        protected override FileAttributes GetAttributesImpl(UPath path)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return FileAttributes.Directory;
                }
                else
                {
                    var fi = GetFileInfo(path);
                    return FileAttributes.Normal;
                }
            }
            catch (Exception )
            {
                return FileAttributes.Directory;
            }
            finally
            {
                ExitFileSystemShared();
            }
        }

        protected override DateTime GetCreationTimeImpl(UPath path)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return DateTime.Now;
                }
                else
                {
                    var fi = GetFileInfo(path);
                    return fi?.Asset.CreationDate?.ToDateTime() ?? DateTime.UtcNow;
                }
            }
            catch (Exception )
            {
                return DateTime.Now;
            }
            finally
            {
                ExitFileSystemShared();
            }
        }
        protected override DateTime GetLastAccessTimeImpl(UPath path)
        {
            return GetLastWriteTimeImpl(path);
        }
        protected override DateTime GetLastWriteTimeImpl(UPath path)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return DateTime.Now;
                }
                else
                {
                    var fi = GetFileInfo(path);
                    return fi?.Asset.ModificationDate?.ToDateTime() ?? DateTime.UtcNow;
                }
            }
            catch (Exception )
            {
                return DateTime.Now;
            }
            finally
            {
                ExitFileSystemShared();
            }
        }

        protected override long GetFileLengthImpl(UPath path)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    return 0;
                }
                else
                {
                    var fi = GetFileInfo(path);
                    if (fi?.Asset == null)
                    {
                        return 0;
                    }
                    return fi.Length.Value;
                }
            }
            catch (Exception )
            {
                return 0;
            }
            finally
            {
                ExitFileSystemShared();
            }
        }



        protected override void MoveDirectoryImpl(UPath srcPath, UPath destPath)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void MoveFileImpl(UPath srcPath, UPath destPath)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override Stream OpenFileImpl(UPath path, FileMode mode, FileAccess access, FileShare share)
        {
            Internal_FillCache();
            EnterFileSystemShared();
            try
            {
                if (path == UPath.Root)
                {
                    throw new NotImplementedException("Not support open root folder");
                }
                else
                {
                    var fi = GetFileInfo(path);
                    if (fi?.Asset == null)
                    {
                        return Stream.Null;
                    }
                    return new FileStream(fi.Path.Value, FileMode.Open, FileAccess.Read, FileShare.ReadWrite);
                }
            }
            catch (Exception )
            {
                return Stream.Null;
            }
            finally
            {
                ExitFileSystemShared();
            }
        }

        protected override void ReplaceFileImpl(UPath srcPath, UPath destPath, UPath destBackupPath, bool ignoreMetadataErrors)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void SetAttributesImpl(UPath path, FileAttributes attributes)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void SetCreationTimeImpl(UPath path, DateTime time)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void SetLastAccessTimeImpl(UPath path, DateTime time)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override void SetLastWriteTimeImpl(UPath path, DateTime time)
        {
            throw new InvalidOperationException("Photo library is readonly");
        }

        protected override IFileSystemWatcher WatchImpl(UPath path)
        {
            throw new NotImplementedException();
        }
    }
}
