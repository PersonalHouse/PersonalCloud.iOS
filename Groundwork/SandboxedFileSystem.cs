using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.IO;
using System.IO.MemoryMappedFiles;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

using Foundation;

using NSPersonalCloud;
using NSPersonalCloud.Interfaces.Errors;
using NSPersonalCloud.Interfaces.FileSystem;

using Photos;

namespace Unishare.Apps.DarwinCore
{
    public class SandboxedFileSystem : VirtualFileSystem
    {
        #region Constants

        private const long PhotosFolderConfigurationSize = 266;
        private const long RecentFolderConfigurationSize = 270;

        public const string FolderNameForPhotos = "Photo Library";
        private const string FileNameForFolderConfiguration = "desktop.ini";

        #endregion Constants

        private bool sharingPhotos;

        private PHFetchResult collections;
        private ConcurrentDictionary<string, PHAsset> photos;
        private ConcurrentDictionary<string, PHAssetResource> resources;

        public bool ArePhotosShared
        {
            get => sharingPhotos;
            set {
                sharingPhotos = value && PHPhotoLibrary.AuthorizationStatus == PHAuthorizationStatus.Authorized;

                if (sharingPhotos)
                {
                    collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
                }
            }
        }

        public SandboxedFileSystem(string rootPath) : base(rootPath)
        {
        }

        #region Photo Library

        protected override bool IsSpecialPathValid(string path)
        {
            return !string.IsNullOrEmpty(path)
                   && path?.StartsWith(Path.AltDirectorySeparatorChar + FolderNameForPhotos, StringComparison.InvariantCulture) == true
                   && ArePhotosShared;
        }

        private FileSystemEntry MetadataForPhotosDirectory { get; } = new FileSystemEntry {
            Name = FolderNameForPhotos,
            CreationDate = DateTime.UtcNow,
            ModificationDate = DateTime.UtcNow,
            Attributes = FileAttributes.Directory | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
        };

        private FileSystemEntry FolderConfigForPhotosDirectory { get; } = new FileSystemEntry {
            Name = FileNameForFolderConfiguration,
            Size = PhotosFolderConfigurationSize,
            CreationDate = DateTime.UtcNow,
            ModificationDate = DateTime.UtcNow,
            Attributes = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System
        };

        private FileSystemEntry FolderConfigForAllPhotosAlbum { get; } = new FileSystemEntry {
            Name = FileNameForFolderConfiguration,
            Size = RecentFolderConfigurationSize,
            CreationDate = DateTime.UtcNow,
            ModificationDate = DateTime.UtcNow,
            Attributes = FileAttributes.Hidden | FileAttributes.ReadOnly | FileAttributes.System
        };

        protected override ValueTask EnumerateChildrenExtension(string relativePath, List<FileSystemEntry> children, CancellationToken cancellation = default)
        {
            if (string.IsNullOrEmpty(relativePath) || relativePath == Path.AltDirectorySeparatorChar.ToString())
            {
                if (ArePhotosShared) children.Add(MetadataForPhotosDirectory);
                return default;
            }

            if (relativePath.Trim(Path.AltDirectorySeparatorChar) == FolderNameForPhotos)
            {
                collections = PHAssetCollection.FetchAssetCollections(PHAssetCollectionType.SmartAlbum, PHAssetCollectionSubtype.SmartAlbumUserLibrary, null);
                foreach (PHAssetCollection asset in collections)
                {
                    children.Add(new FileSystemEntry {
                        Name = asset.LocalizedTitle,
                        CreationDate = asset.StartDate?.ToDateTime() ?? DateTime.UtcNow,
                        ModificationDate = asset.EndDate?.ToDateTime() ?? DateTime.UtcNow,
                        Attributes = FileAttributes.Directory | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
                    });
                }

                #region Desktop.ini for Photos

                children.Add(FolderConfigForPhotosDirectory);

                #endregion Desktop.ini for Photos

                return default;
            }

            if (!IsSpecialPathValid(relativePath)) return default;

            var segments = relativePath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2)
            {
                if (collections == null) throw new NotReadyException();

                if (resources == null) resources = new ConcurrentDictionary<string, PHAssetResource>();
                if (photos == null) photos = new ConcurrentDictionary<string, PHAsset>();

                var albumName = segments.Last();
                if (!(collections.OfType<PHAssetCollection>().FirstOrDefault(x => x.LocalizedTitle == albumName) is PHAssetCollection album))
                {
                    throw new DirectoryNotFoundException();
                }

                var assets = PHAsset.FetchAssets(album, null);
                foreach (PHAsset photo in assets)
                {
                    var assetResources = PHAssetResource.GetAssetResources(photo);
                    var original = assetResources.FirstOrDefault(x => x.ResourceType == PHAssetResourceType.Video ||
                                                                      x.ResourceType == PHAssetResourceType.Photo ||
                                                                      x.ResourceType == PHAssetResourceType.Audio);

                    if (original == null) continue;
                    photos[original.OriginalFilename] = photo;
                    resources[original.OriginalFilename] = original;

                    children.Add(new FileSystemEntry {
                        Name = original.OriginalFilename,
                        Type = original.UniformTypeIdentifier,
                        Size = original.UserInfoGetSize(),
                        CreationDate = photo.CreationDate?.ToDateTime() ?? DateTime.UtcNow,
                        ModificationDate = photo.ModificationDate?.ToDateTime() ?? DateTime.UtcNow,
                        Attributes = FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
                    });

                    var originalName = Path.GetFileNameWithoutExtension(original.OriginalFilename);
                    foreach (var resource in assetResources)
                    {
                        if (resource == original) continue;
                        if (string.IsNullOrEmpty(resource.OriginalFilename)) continue;

                        var extension = Path.GetExtension(resource.OriginalFilename) ?? string.Empty;
                        var fileName = $"{originalName} ({resource.ResourceType.ToString("G")}){extension}";
                        photos[fileName] = photo;
                        resources[fileName] = resource;

                        children.Add(new FileSystemEntry {
                            Name = fileName,
                            Type = fileName,
                            Size = resource.UserInfoGetSize(),
                            CreationDate = photo.CreationDate?.ToDateTime() ?? DateTime.UtcNow,
                            ModificationDate = photo.ModificationDate?.ToDateTime() ?? DateTime.UtcNow,
                            Attributes = FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
                        });
                    }
                }

                #region Desktop.ini for Smart Album: All Photos

                children.Add(FolderConfigForAllPhotosAlbum);

                #endregion Desktop.ini for Smart Album: All Photos

                return default;
            }

            return default;
        }

        protected override ValueTask<FileSystemEntry> ReadMetadataExtension(string relativePath, CancellationToken cancellation)
        {
            if (relativePath.TrimEnd(Path.AltDirectorySeparatorChar) == Path.AltDirectorySeparatorChar + FolderNameForPhotos) return new ValueTask<FileSystemEntry>(MetadataForPhotosDirectory);

            var segments = relativePath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length == 2)
            {
                var fileName = segments.Last();

                #region Desktop.ini for Photos

                if (fileName.Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase)) return new ValueTask<FileSystemEntry>(FolderConfigForPhotosDirectory);

                #endregion Desktop.ini for Photos

                if (collections == null) throw new NotReadyException();

                var albumName = segments.Last();
                if (!(collections.OfType<PHAssetCollection>().FirstOrDefault(x => x.LocalizedTitle == albumName) is PHAssetCollection album)) throw new DirectoryNotFoundException();

                return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                    Name = album.LocalizedTitle,
                    CreationDate = album.StartDate?.ToDateTime() ?? DateTime.Now,
                    ModificationDate = album.EndDate?.ToDateTime() ?? DateTime.Now,
                    Attributes = FileAttributes.Directory | FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
                });
            }
            else if (segments.Length == 3)
            {
                if (resources == null || photos == null) throw new NotReadyException();

                var photoName = segments.Last();

                #region Desktop.ini for Smart Album: All Photos

                if (photoName.Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase)) return new ValueTask<FileSystemEntry>(FolderConfigForAllPhotosAlbum);

                #endregion Desktop.ini for Smart Album: All Photos

                if (!resources.TryGetValue(photoName, out var resource) || !photos.TryGetValue(photoName, out var photo)) throw new FileNotFoundException();

                return new ValueTask<FileSystemEntry>(new FileSystemEntry {
                    Name = photoName,
                    Type = resource.UniformTypeIdentifier,
                    Size = resource.UserInfoGetSize(),
                    CreationDate = photo.CreationDate?.ToDateTime() ?? DateTime.Now,
                    ModificationDate = photo.ModificationDate?.ToDateTime() ?? DateTime.Now,
                    Attributes = FileAttributes.NotContentIndexed | FileAttributes.ReadOnly
                });
            }

            throw new FileNotFoundException();
        }

        protected override ValueTask<Stream> ReadFileExtension(string relativePath, CancellationToken cancellation = default)
        {
            var segments = relativePath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 3)
            {
                #region Desktop.ini for Photos

                if (segments.Length == 2 && segments.Last().Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase))
                {
                    var resourcePath = NSBundle.MainBundle.ResourcePath;
                    var filePath = Path.Combine(resourcePath, "Desktop (Photos).ini");
                    return new ValueTask<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
                }

                #endregion Desktop.ini for Photos

                throw new FileNotFoundException();
            }

            if (resources == null) throw new NotReadyException();

            var photoName = segments.Last();

            #region Desktop.ini for Smart Album: All Photos

            if (photoName.Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase))
            {
                var resourcePath = NSBundle.MainBundle.ResourcePath;
                var filePath = Path.Combine(resourcePath, "Desktop (Album).ini");
                return new ValueTask<Stream>(new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read));
            }

            #endregion Desktop.ini for Smart Album: All Photos

            if (!resources.TryGetValue(photoName, out var resource)) throw new FileNotFoundException();

            var stream = new MemoryStream();
            NSError lastError = null;

            using var waiter = new ManualResetEvent(false);
            int? request = null;
            request = PHAssetResourceManager.DefaultManager.RequestData(resource, null, data => {
                stream.Write(data.ToArray());
                if (request.HasValue && cancellation.IsCancellationRequested) PHAssetResourceManager.DefaultManager.CancelDataRequest(request.Value);
            }, error => {
                if (error != null) lastError = error;
                waiter.Set();
            });
            waiter.WaitOne();

            if (lastError != null) throw new IOException(lastError.LocalizedFailureReason);

            stream.Position = 0;
            return new ValueTask<Stream>(stream);
        }

        protected override ValueTask<Stream> ReadPartialFileExtension(string relativePath, long fromPosition, long toPosition, CancellationToken cancellation)
        {
            var segments = relativePath.Split(Path.AltDirectorySeparatorChar, StringSplitOptions.RemoveEmptyEntries);
            if (segments.Length != 3)
            {
                #region Desktop.ini for Photos

                if (segments.Length == 2 && segments.Last().Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase))
                {
                    var resourcePath = NSBundle.MainBundle.ResourcePath;
                    var filePath = Path.Combine(resourcePath, "Desktop (Photos).ini");
                    var fileInfo = new FileInfo(filePath);
                    toPosition = Math.Min(fileInfo.Length - 1, toPosition);
                    if (toPosition < fromPosition || fromPosition < 0 || toPosition < 0)
                    {
                        throw new InvalidOperationException("Read range for this file is unsatisfiable.");
                    }

                    using var map = MemoryMappedFile.CreateFromFile(fileInfo.FullName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                    return new ValueTask<Stream>(map.CreateViewStream(fromPosition, toPosition - fromPosition + 1, MemoryMappedFileAccess.Read));
                }

                #endregion Desktop.ini for Photos

                throw new FileNotFoundException();
            }

            if (resources == null) throw new NotReadyException();

            var photoName = segments.Last();

            #region Desktop.ini for Smart Album: All Photos

            if (photoName.Equals(FileNameForFolderConfiguration, StringComparison.InvariantCultureIgnoreCase))
            {
                var resourcePath = NSBundle.MainBundle.ResourcePath;
                var filePath = Path.Combine(resourcePath, "Desktop (Album).ini");
                var fileInfo = new FileInfo(filePath);
                toPosition = Math.Min(fileInfo.Length - 1, toPosition);
                if (toPosition < fromPosition || fromPosition < 0 || toPosition < 0)
                {
                    throw new InvalidOperationException("Read range for this file is unsatisfiable.");
                }

                using var map = MemoryMappedFile.CreateFromFile(fileInfo.FullName, FileMode.Open, null, 0, MemoryMappedFileAccess.Read);
                return new ValueTask<Stream>(map.CreateViewStream(fromPosition, toPosition - fromPosition + 1, MemoryMappedFileAccess.Read));
            }

            #endregion Desktop.ini for Smart Album: All Photos

            if (!resources.TryGetValue(photoName, out var resource)) throw new FileNotFoundException();

            var stream = new MemoryStream();
            NSError lastError = null;

            using var waiter = new ManualResetEvent(false);
            int? request = null;

            if (toPosition != long.MaxValue) toPosition += 1;
            var bytesRead = 0;
            bool? firstRead = null;
            request = PHAssetResourceManager.DefaultManager.RequestData(resource, null, data => {
                var partSize = (int) data.Length;
                if (bytesRead + partSize - 1 < fromPosition)
                {
                    bytesRead += partSize;
                    return;
                }

                if (firstRead == null) firstRead = true;

                var bytes = data.ToArray();
                var startIndex = (int) (firstRead == true ? fromPosition - bytesRead : 0);
                var takeLength = (int) (Math.Min(toPosition, bytes.Length) - startIndex);
                if (takeLength < 0) takeLength = 0;

                stream.Write(bytes, startIndex, takeLength);

                bytesRead += partSize;
                firstRead = false;

                if (!request.HasValue) return;
                if (cancellation.IsCancellationRequested || bytesRead >= toPosition) PHAssetResourceManager.DefaultManager.CancelDataRequest(request.Value);
            }, error => {
                if (error != null) lastError = error;
                waiter.Set();
            });
            waiter.WaitOne();

            if (lastError != null && lastError.Code != (int) PHPhotosError.UserCancelled) throw new IOException(lastError.LocalizedFailureReason);

            stream.Position = 0;
            return new ValueTask<Stream>(stream);
        }

        #endregion Photo Library
    }
}
