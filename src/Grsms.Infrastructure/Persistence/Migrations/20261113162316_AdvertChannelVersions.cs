using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AdvertChannelVersions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ExternalText",
                table: "JobAdverts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "InternalNotice",
                table: "JobAdverts",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "LinkedInText",
                table: "JobAdverts",
                type: "varchar(3000)",
                maxLength: 3000,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ExternalText",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "InternalNotice",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "LinkedInText",
                table: "JobAdverts");
        }
    }
}
