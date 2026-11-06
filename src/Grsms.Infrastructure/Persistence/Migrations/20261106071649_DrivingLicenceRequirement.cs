using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// The QF-81-11 driving licence becomes optional with three choices (Not needed / Desirable / Mandatory) instead of a
    /// yes/no "required" flag. Requests that said "required" become Mandatory; everything else Not needed.
    /// </summary>
    public partial class DrivingLicenceRequirement : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "DrivingLicence",
                table: "RecruitmentRequests",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotNeeded");

            // Keep what was already chosen before the old column goes.
            migrationBuilder.Sql("UPDATE RecruitmentRequests SET DrivingLicence = 'Mandatory' WHERE DrivingLicenceRequired = 1;");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceRequired",
                table: "RecruitmentRequests");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "DrivingLicenceRequired",
                table: "RecruitmentRequests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.Sql("UPDATE RecruitmentRequests SET DrivingLicenceRequired = 1 WHERE DrivingLicence = 'Mandatory';");

            migrationBuilder.DropColumn(
                name: "DrivingLicence",
                table: "RecruitmentRequests");
        }
    }
}
