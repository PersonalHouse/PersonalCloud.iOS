using System;
using System.Linq;

using CoreFoundation;

using Foundation;

using MobileCoreServices;

namespace NSPersonalCloud.DarwinCore
{
    public partial class UTI : IEquatable<UTI>, IEquatable<string>, IEquatable<CFString>, IEquatable<NSString>
    {
        public string Id { get; }
        public CFString AsCFString => new CFString(Id);
        public NSString ASNSString => new NSString(Id);

        public bool IsDeclared => UTType.IsDeclared(Id);
        public bool IsDynamic => UTType.IsDynamic(Id);

        public bool ConformsTo(UTI other) => other is null ? false : UTType.ConformsTo(Id, other.Id);
        public bool ConformsTo(string other) => other is null ? false : UTType.ConformsTo(Id, other);

        public override string ToString()
        {
            if (Id == "com.daoyehuo.photolibraryresources") return UIKitExtensions.Localize("Backup.FileType");
            return UTType.GetDescription(Id);
        }

        #region Constructors

        public UTI(string uti) => Id = uti;

        public static UTI FromFileName(string extension, UTI conformingTo = null)
        {
            if (extension.StartsWith('.')) extension = extension.Substring(1);

            var conformingToString = conformingTo?.Id;
            var rawUTI = UTType.CreatePreferredIdentifier(UTType.TagClassFilenameExtension, extension,
                                                          conformingToString);
            if (rawUTI == null) return null;
            return new UTI(rawUTI);
        }

        public static UTI FromMIMEType(string mime, UTI conformingTo = null)
        {
            var conformingToString = conformingTo?.Id;
            var rawUTI = UTType.CreatePreferredIdentifier(UTType.TagClassMIMEType, mime,
                                                          conformingToString);
            if (rawUTI == null) return null;
            return new UTI(rawUTI);
        }

        #endregion Constructors

        #region Standard UTIs

        private static UTI[] GetStandardUTIs(string tagClass, string tag, UTI conformingTo = null)
        {
            var conformingToString = conformingTo?.Id;
            var rawUTI = UTType.CreateAllIdentifiers(tagClass, tag, conformingToString);
            if (!(rawUTI?.Length > 0)) return null;
            return rawUTI.Select(x => new UTI(x)).ToArray();
        }

        private static UTI[] GetStandardUTIsForFileName(string extension, UTI conformingTo = null)
        {
            return GetStandardUTIs(UTType.TagClassFilenameExtension, extension, conformingTo);
        }

        private static UTI[] GetStandardUTIsForMIMEType(string mime, UTI conformingTo = null)
        {
            return GetStandardUTIs(UTType.TagClassMIMEType, mime, conformingTo);
        }

        #endregion Standard UTIs

        #region Conversion

        private string GetPreferredTag(string tagClass)
        {
            return UTType.GetPreferredTag(Id, tagClass);
        }

        private string[] GetAllTags(string tagClass)
        {
            return UTType.CopyAllTags(Id, tagClass);
        }

        public string ToFileNameExtension() => GetPreferredTag(UTType.TagClassFilenameExtension);

        public string[] ToFileNameExtensions() => GetAllTags(UTType.TagClassFilenameExtension);

        public string ToMIMEType() => GetPreferredTag(UTType.TagClassMIMEType);

        public string[] ToMIMETypes() => GetAllTags(UTType.TagClassMIMEType);

        #endregion Conversion

        #region Equality

        public override int GetHashCode() => Id.GetHashCode();

        public bool Equals(UTI other) => other is null ? false : UTType.Equals(ASNSString, other.ASNSString);

        public bool Equals(string other) => other is null ? false : UTType.Equals(ASNSString, new NSString(other));

        public bool Equals(CFString other) => other is null ? false : UTType.Equals(ASNSString, new NSString(other.ToString()));

        public bool Equals(NSString other) => other is null ? false : UTType.Equals(ASNSString, other);

        public override bool Equals(object obj) => Equals(obj?.ToString());

        public static bool operator ==(UTI lhs, UTI rhs) => lhs?.Equals(rhs) == true;

        public static bool operator !=(UTI lhs, UTI rhs) => lhs?.Equals(rhs) != true;

        public static bool operator ==(UTI lhs, string rhs) => lhs?.Equals(rhs) == true;

        public static bool operator !=(UTI lhs, string rhs) => lhs?.Equals(rhs) != true;

        public static bool operator ==(UTI lhs, NSString rhs) => lhs?.Equals(rhs) == true;

        public static bool operator !=(UTI lhs, NSString rhs) => lhs?.Equals(rhs) != true;

        public static bool operator ==(UTI lhs, CFString rhs) => lhs?.Equals(rhs) == true;

        public static bool operator !=(UTI lhs, CFString rhs) => lhs?.Equals(rhs) != true;

        #endregion Equality
    }
}
