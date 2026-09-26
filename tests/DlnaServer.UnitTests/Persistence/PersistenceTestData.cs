using DlnaServer.Core.Contracts;
using DlnaServer.Core.Dlna;

namespace DlnaServer.UnitTests.Persistence
{
    /// <summary>
    /// The create DTOs the persistence fixtures seed rows with.
    /// </summary>
    internal static class PersistenceTestData
    {
        public static MediaDirectoryCreateDto CreateDirectory(
            string fullPath,
            string name,
            int depth,
            bool isSourceRoot,
            Guid? parent = null)
        {
            return new MediaDirectoryCreateDto
            {
                FullPath = fullPath,
                Name = name,
                Depth = depth,
                IsSourceRoot = isSourceRoot,
                ParentDirectoryPublicId = parent,
            };
        }

        public static MediaFileCreateDto CreateFile(string fullPath)
        {
            var fileName = Path.GetFileName(fullPath);

            return new MediaFileCreateDto
            {
                FullPath = fullPath,
                FileName = fileName,
                Title = Path.GetFileNameWithoutExtension(fileName),
                Extension = Path.GetExtension(fileName).ToLowerInvariant(),
                Mime = DlnaMime.VideoXMatroska,
                UpnpClass = DlnaItemClass.VideoItem,
                SizeInBytes = 1024,
                FileCreatedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                FileModifiedUtc = new DateTime(2026, 1, 1, 0, 0, 0, DateTimeKind.Utc),
                ContentStamp = "1024:638000000000000000",
            };
        }
    }
}
