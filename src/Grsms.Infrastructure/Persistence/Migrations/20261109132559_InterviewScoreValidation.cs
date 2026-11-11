using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class InterviewScoreValidation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "ReturnReason",
                table: "Scorecards",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReturnedAt",
                table: "Scorecards",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReturnedByUserId",
                table: "Scorecards",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ValidatedAt",
                table: "Interviews",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidatedByUserId",
                table: "Interviews",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ValidationNote",
                table: "Interviews",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Attendance",
                table: "InterviewPanelMembers",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "NotRecorded"); // existing panel members: attendance not recorded

            migrationBuilder.AddColumn<string>(
                name: "AttendanceNote",
                table: "InterviewPanelMembers",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AttendanceRecordedAt",
                table: "InterviewPanelMembers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AttendanceRecordedByUserId",
                table: "InterviewPanelMembers",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "ReturnReason",
                table: "Scorecards");

            migrationBuilder.DropColumn(
                name: "ReturnedAt",
                table: "Scorecards");

            migrationBuilder.DropColumn(
                name: "ReturnedByUserId",
                table: "Scorecards");

            migrationBuilder.DropColumn(
                name: "ValidatedAt",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "ValidatedByUserId",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "ValidationNote",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "Attendance",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "AttendanceNote",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "AttendanceRecordedAt",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "AttendanceRecordedByUserId",
                table: "InterviewPanelMembers");
        }
    }
}
