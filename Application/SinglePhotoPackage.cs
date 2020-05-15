using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading;

using Foundation;

using Newtonsoft.Json;

using Photos;

using NSPersonalCloud.DarwinCore;
using NSPersonalCloud.DarwinCore.Models;

namespace NSPersonalCloud.DarwinMobile
{
    public class SinglePhotoPackage
    {
        private PLAsset Photo { get; }

        public SinglePhotoPackage(PHAsset photo)
        {
            Photo = new PLAsset { Asset = photo };
            Photo.Refresh();
        }

        public SinglePhotoPackage(PLAsset photo)
        {
            Photo = photo;
        }

        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1308", Justification = "Lookup requires lowercase.")]
        [System.Diagnostics.CodeAnalysis.SuppressMessage("Globalization", "CA1303", Justification = "Logging needs no localization.")]
        public void WriteArchive(Stream zip, Stream standalone = null)
        {
            if (!Photo.IsAvailable) throw new InvalidOperationException("This photo is no longer available.");

            using var archive = new ZipArchive(zip, ZipArchiveMode.Update, true);

            var options = new PHAssetResourceRequestOptions { NetworkAccessAllowed = true };
            var waitables = new List<ManualResetEvent>(Photo.Resources.Count + 1);

            NSError lastError = null;
            var original = Photo.Resources.FirstOrDefault(x => x.ResourceType == PHAssetResourceType.FullSizeVideo ||
                                                               x.ResourceType == PHAssetResourceType.FullSizePhoto ||
                                                               x.ResourceType == PHAssetResourceType.Photo ||
                                                               x.ResourceType == PHAssetResourceType.Video ||
                                                               x.ResourceType == PHAssetResourceType.Audio);

            if (original == null) throw new InvalidOperationException("Backup failed for this photo.");

            if (standalone != null)
            {
                var waitable = new ManualResetEvent(false);
                waitables.Add(waitable);
                PHAssetResourceManager.DefaultManager.RequestData(original, options, data =>
                {
                    var bytes = data.ToArray();
                    standalone.Write(bytes, 0, bytes.Length);
                }, error =>
                {
                    if (error != null) lastError = error;
                    standalone.Flush();
                    waitable.Set();
                });
            }

            var baseName = Path.GetFileNameWithoutExtension(Photo.FileName);
            var extension = Path.GetExtension(Photo.FileName)?.ToLowerInvariant() ?? string.Empty;

            foreach (var resource in Photo.Resources)
            {
                var entryName = $"{baseName} ({resource.ResourceType:G}){extension}";
                var entryStream = archive.CreateEntry(entryName, CompressionLevel.NoCompression).Open();
                var waitable = new ManualResetEvent(false);
                waitables.Add(waitable);
                PHAssetResourceManager.DefaultManager.RequestData(resource, options, data =>
                {
                    var bytes = data.ToArray();
                    entryStream.Write(bytes, 0, bytes.Length);
                }, error =>
                {
                    if (error != null) lastError = error;
                    entryStream.Dispose();
                    waitable.Set();
                });
            }

            WaitHandle.WaitAll(waitables.ToArray());

            if (lastError != null) throw new InvalidOperationException(lastError.LocalizedDescription);

            // Write meta entry.
            Photo.BackupDate = DateTime.Now;
            using var metaStream = archive.CreateEntry("@", CompressionLevel.NoCompression).Open();
            using var textStream = new StreamWriter(metaStream, Encoding.UTF8);
            using var jsonWriter = new JsonTextWriter(textStream) { Indentation = 4, IndentChar = ' ', Formatting = Formatting.Indented };
            JsonSerializer.CreateDefault().Serialize(jsonWriter, Photo);
        }

        public static void RestoreFromArchive(string filePath, Action onSuccess = null, Action<NSError> onFailure = null)
        {
            if (PHPhotoLibrary.AuthorizationStatus != PHAuthorizationStatus.Authorized)
            {
                onFailure?.Invoke(null);
                return;
            }

            var resources = new List<string>();

            var rootPath = Path.Combine(Paths.Temporary, Path.GetFileNameWithoutExtension(filePath));
            Directory.CreateDirectory(rootPath);
            using (var stream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read))
            using (var archive = new ZipArchive(stream, ZipArchiveMode.Read))
            {
                foreach (var entry in archive.Entries)
                {
                    if (entry.Name == "@") continue;
                    var entryPath = Path.Combine(rootPath, entry.Name);
                    using (var fileStream = new FileStream(entryPath, FileMode.Create, FileAccess.ReadWrite, FileShare.None))
                    { entry.Open().CopyTo(fileStream); }
                    if (entry.Name.Contains("(Photo)", StringComparison.InvariantCulture) || entry.Name.Contains("(Video)", StringComparison.InvariantCulture))
                    {
                        resources.Insert(0, entryPath);
                    }
                    else
                    {
                        resources.Add(entryPath);
                    }
                }
            }

            PHPhotoLibrary.SharedPhotoLibrary.PerformChanges(() =>
            {
                var request = PHAssetCreationRequest.CreationRequestForAsset();
                var options = new PHAssetResourceCreationOptions { ShouldMoveFile = true };
                foreach (var path in resources)
                {
                    var fileName = Path.GetFileName(path);
                    if (fileName.IndexOf('(') != -1)
                    {
                        var indexLeft = fileName.IndexOf('(') + 1;
                        var resourceType = fileName.Substring(indexLeft, fileName.IndexOf(')') - indexLeft);
                        var type = (PHAssetResourceType)Enum.Parse(typeof(PHAssetResourceType), resourceType);
                        request.AddResource(type, NSUrl.FromFilename(path), options);
                    }
                }
            }, (isSuccess, error) =>
            {
                try { Directory.Delete(rootPath, true); }
                catch { }

                if (isSuccess) onSuccess?.Invoke();
                else onFailure?.Invoke(error);
            });
        }
    }
}
