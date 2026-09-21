using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DlnaServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InitialSchema : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Directories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullPath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Name = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false, collation: "NOCASE"),
                    ParentDirectoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Depth = table.Column<int>(type: "INTEGER", nullable: false),
                    IsSourceRoot = table.Column<bool>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Directories", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Directories_Directories_ParentDirectoryId",
                        column: x => x.ParentDirectoryId,
                        principalTable: "Directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ServerInstances",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MachineName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    LastStartedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ServerInstances", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Files",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    FullPath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    FileName = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false, collation: "NOCASE"),
                    Title = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false, collation: "NOCASE"),
                    Extension = table.Column<string>(type: "TEXT", maxLength: 32, nullable: false, collation: "NOCASE"),
                    DirectoryId = table.Column<int>(type: "INTEGER", nullable: true),
                    Mime = table.Column<int>(type: "INTEGER", nullable: false),
                    DlnaProfileName = table.Column<string>(type: "TEXT", maxLength: 128, nullable: true),
                    UpnpClass = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    FileCreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    FileModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    IsExcludedFromCache = table.Column<bool>(type: "INTEGER", nullable: false),
                    ContentStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    MetadataStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ThumbnailStamp = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    IsMetadataSuppressed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsThumbnailSuppressed = table.Column<bool>(type: "INTEGER", nullable: false),
                    IsThumbnailRebuildForced = table.Column<bool>(type: "INTEGER", nullable: false),
                    MetadataFailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    ThumbnailFailureCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Files", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Files_Directories_DirectoryId",
                        column: x => x.DirectoryId,
                        principalTable: "Directories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "AudioStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StreamIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Title = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    IsDefault = table.Column<bool>(type: "INTEGER", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Codec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Bitrate = table.Column<long>(type: "INTEGER", nullable: true),
                    SampleRate = table.Column<int>(type: "INTEGER", nullable: true),
                    Channels = table.Column<int>(type: "INTEGER", nullable: true),
                    Language = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AudioStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AudioStreams_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MediaFileTags",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StreamIndex = table.Column<int>(type: "INTEGER", nullable: true),
                    Name = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    Value = table.Column<string>(type: "TEXT", maxLength: 8192, nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MediaFileTags", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MediaFileTags_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "SubtitleStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    StreamIndex = table.Column<int>(type: "INTEGER", nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Codec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    ExternalFilePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: true),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtitleStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtitleStreams_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Thumbnails",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    FilePath = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    Mime = table.Column<int>(type: "INTEGER", nullable: false),
                    Width = table.Column<int>(type: "INTEGER", nullable: false),
                    Height = table.Column<int>(type: "INTEGER", nullable: false),
                    SizeInBytes = table.Column<long>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Thumbnails", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Thumbnails_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VideoStreams",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    Duration = table.Column<TimeSpan>(type: "TEXT", nullable: true),
                    Width = table.Column<int>(type: "INTEGER", nullable: true),
                    Height = table.Column<int>(type: "INTEGER", nullable: true),
                    FrameRate = table.Column<double>(type: "REAL", nullable: true),
                    AspectRatio = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Bitrate = table.Column<long>(type: "INTEGER", nullable: true),
                    PixelFormat = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    Rotation = table.Column<int>(type: "INTEGER", nullable: true),
                    Codec = table.Column<string>(type: "TEXT", maxLength: 64, nullable: true),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VideoStreams", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VideoStreams_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ThumbnailContents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    ThumbnailId = table.Column<int>(type: "INTEGER", nullable: false),
                    Data = table.Column<byte[]>(type: "BLOB", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ThumbnailContents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ThumbnailContents_Thumbnails_ThumbnailId",
                        column: x => x.ThumbnailId,
                        principalTable: "Thumbnails",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AudioStreams_Language",
                table: "AudioStreams",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_AudioStreams_MediaFileId_StreamIndex",
                table: "AudioStreams",
                columns: new[] { "MediaFileId", "StreamIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AudioStreams_PublicId",
                table: "AudioStreams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Directories_FullPath",
                table: "Directories",
                column: "FullPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Directories_IsSourceRoot",
                table: "Directories",
                column: "IsSourceRoot");

            migrationBuilder.CreateIndex(
                name: "IX_Directories_ParentDirectoryId_CreatedUtc",
                table: "Directories",
                columns: new[] { "ParentDirectoryId", "CreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Directories_ParentDirectoryId_Name",
                table: "Directories",
                columns: new[] { "ParentDirectoryId", "Name" });

            migrationBuilder.CreateIndex(
                name: "IX_Directories_PublicId",
                table: "Directories",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_CreatedUtc",
                table: "Files",
                column: "CreatedUtc");

            migrationBuilder.CreateIndex(
                name: "IX_Files_DirectoryId_FileCreatedUtc",
                table: "Files",
                columns: new[] { "DirectoryId", "FileCreatedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_DirectoryId_Title",
                table: "Files",
                columns: new[] { "DirectoryId", "Title" });

            migrationBuilder.CreateIndex(
                name: "IX_Files_FullPath",
                table: "Files",
                column: "FullPath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Files_PublicId",
                table: "Files",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MediaFileTags_MediaFileId",
                table: "MediaFileTags",
                column: "MediaFileId");

            migrationBuilder.CreateIndex(
                name: "IX_MediaFileTags_PublicId",
                table: "MediaFileTags",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerInstances_MachineName",
                table: "ServerInstances",
                column: "MachineName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ServerInstances_PublicId",
                table: "ServerInstances",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleStreams_Language",
                table: "SubtitleStreams",
                column: "Language");

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleStreams_MediaFileId_StreamIndex",
                table: "SubtitleStreams",
                columns: new[] { "MediaFileId", "StreamIndex" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleStreams_PublicId",
                table: "SubtitleStreams",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThumbnailContents_PublicId",
                table: "ThumbnailContents",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ThumbnailContents_ThumbnailId",
                table: "ThumbnailContents",
                column: "ThumbnailId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_FilePath",
                table: "Thumbnails",
                column: "FilePath",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_MediaFileId",
                table: "Thumbnails",
                column: "MediaFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Thumbnails_PublicId",
                table: "Thumbnails",
                column: "PublicId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoStreams_MediaFileId",
                table: "VideoStreams",
                column: "MediaFileId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VideoStreams_PublicId",
                table: "VideoStreams",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AudioStreams");

            migrationBuilder.DropTable(
                name: "MediaFileTags");

            migrationBuilder.DropTable(
                name: "ServerInstances");

            migrationBuilder.DropTable(
                name: "SubtitleStreams");

            migrationBuilder.DropTable(
                name: "ThumbnailContents");

            migrationBuilder.DropTable(
                name: "VideoStreams");

            migrationBuilder.DropTable(
                name: "Thumbnails");

            migrationBuilder.DropTable(
                name: "Files");

            migrationBuilder.DropTable(
                name: "Directories");
        }
    }
}
