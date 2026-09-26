using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DlnaServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddSubtitleFiles : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "SubtitleFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    MediaFileId = table.Column<int>(type: "INTEGER", nullable: false),
                    RelativePath = table.Column<string>(type: "TEXT", maxLength: 1024, nullable: false),
                    Language = table.Column<string>(type: "TEXT", maxLength: 32, nullable: true),
                    Source = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SubtitleFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SubtitleFiles_Files_MediaFileId",
                        column: x => x.MediaFileId,
                        principalTable: "Files",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleFiles_MediaFileId_RelativePath",
                table: "SubtitleFiles",
                columns: new[] { "MediaFileId", "RelativePath" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_SubtitleFiles_PublicId",
                table: "SubtitleFiles",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "SubtitleFiles");
        }
    }
}
