using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 11 November 2026 (docs/29 change 2): job adverts are reviewed by the HR Manager and approved by the IT Manager before they
    /// are published. Adds the built-in IT Manager role (its permissions are given at start-up by PermissionSeeder:
    /// Permissions.RolesAddedLater and the new adverts.* permissions), the review columns, and widens the status columns to 30
    /// characters ("ReturnedForCorrection"). Existing adverts keep their status: published and scheduled ones stay published,
    /// drafts go through the new review. If a Super Admin had already added a CUSTOM role called "IT Manager", rename it first.
    /// </summary>
    public partial class ItManagerAdvertApproval : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "JobAdverts",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "HrReviewedAt",
                table: "JobAdverts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HrReviewedByUserId",
                table: "JobAdverts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ItApprovedAt",
                table: "JobAdverts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ItApprovedByUserId",
                table: "JobAdverts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewComment",
                table: "JobAdverts",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "SubmittedAt",
                table: "JobAdverts",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "SubmittedByUserId",
                table: "JobAdverts",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ToStatus",
                table: "JobAdvertActions",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AlterColumn<string>(
                name: "FromStatus",
                table: "JobAdvertActions",
                type: "varchar(30)",
                maxLength: 30,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "it-manager", "it-manager", "IT Manager", "IT MANAGER" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Rolling back: adverts in the new statuses go back to Draft (the columns become 20 characters again), and the role,
            // its users and the new permissions are removed.
            migrationBuilder.Sql("UPDATE JobAdverts SET Status = 'Draft' WHERE Status IN ('AwaitingHrReview', 'AwaitingItApproval', 'ReturnedForCorrection', 'Rejected');");
            migrationBuilder.Sql("UPDATE JobAdvertActions SET FromStatus = 'Draft' WHERE FromStatus IN ('AwaitingHrReview', 'AwaitingItApproval', 'ReturnedForCorrection', 'Rejected');");
            migrationBuilder.Sql("UPDATE JobAdvertActions SET ToStatus = 'Draft' WHERE ToStatus IN ('AwaitingHrReview', 'AwaitingItApproval', 'ReturnedForCorrection', 'Rejected');");
            migrationBuilder.Sql("DELETE FROM RoleClaims WHERE RoleId = 'it-manager' OR ClaimValue IN ('adverts.review.hr', 'adverts.review.it', 'adverts.publish', 'adverts.return', 'adverts.unpublish');");
            migrationBuilder.Sql("DELETE FROM UserRoles WHERE RoleId = 'it-manager';");
            migrationBuilder.Sql("DELETE FROM KnownPermissions WHERE Name IN ('role:IT Manager', 'adverts.review.hr', 'adverts.review.it', 'adverts.publish', 'adverts.return', 'adverts.unpublish');");

            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "it-manager");

            migrationBuilder.DropColumn(
                name: "HrReviewedAt",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "HrReviewedByUserId",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "ItApprovedAt",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "ItApprovedByUserId",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "ReviewComment",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "SubmittedAt",
                table: "JobAdverts");

            migrationBuilder.DropColumn(
                name: "SubmittedByUserId",
                table: "JobAdverts");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "JobAdverts",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "ToStatus",
                table: "JobAdvertActions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30);

            migrationBuilder.AlterColumn<string>(
                name: "FromStatus",
                table: "JobAdvertActions",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(30)",
                oldMaxLength: 30);
        }
    }
}
