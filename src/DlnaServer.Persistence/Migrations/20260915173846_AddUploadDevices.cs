using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace DlnaServer.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AddUploadDevices : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "UploadDevices",
                columns: table => new
                {
                    Id = table.Column<int>(type: "INTEGER", nullable: false)
                        .Annotation("Sqlite:Autoincrement", true),
                    Fingerprint = table.Column<string>(type: "TEXT", maxLength: 128, nullable: false),
                    RemoteAddress = table.Column<string>(type: "TEXT", maxLength: 64, nullable: false),
                    UserAgent = table.Column<string>(type: "TEXT", maxLength: 512, nullable: false),
                    AcceptLanguage = table.Column<string>(type: "TEXT", maxLength: 256, nullable: true),
                    LastDestination = table.Column<string>(type: "TEXT", maxLength: 4096, nullable: false),
                    LastUploadUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    UploadCount = table.Column<int>(type: "INTEGER", nullable: false),
                    PublicId = table.Column<Guid>(type: "TEXT", nullable: false),
                    CreatedUtc = table.Column<DateTime>(type: "TEXT", nullable: false),
                    ModifiedUtc = table.Column<DateTime>(type: "TEXT", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_UploadDevices", x => x.Id);
                });

            migrationBuilder.CreateIndex(
                name: "IX_UploadDevices_Fingerprint",
                table: "UploadDevices",
                column: "Fingerprint",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_UploadDevices_PublicId",
                table: "UploadDevices",
                column: "PublicId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "UploadDevices");
        }
    }
}
