using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase06_Shortlisting : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Skills",
                table: "JobApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(3000)",
                oldMaxLength: 3000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeReason",
                table: "JobApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(2000)",
                oldMaxLength: 2000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverLetter",
                table: "JobApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(4000)",
                oldMaxLength: 4000,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Certifications",
                table: "JobApplications",
                type: "text",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(3000)",
                oldMaxLength: 3000,
                oldNullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HodNote",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HodRecommendation",
                table: "JobApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None"); // existing applications: no recommendation yet

            migrationBuilder.AddColumn<DateTime>(
                name: "HodRecommendedAt",
                table: "JobApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "HodRecommendedByUserId",
                table: "JobApplications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "MeetsMinimum",
                table: "JobApplications",
                type: "tinyint(1)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterNote",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterRecommendation",
                table: "JobApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None");

            migrationBuilder.AddColumn<DateTime>(
                name: "RecruiterRecommendedAt",
                table: "JobApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "RecruiterRecommendedByUserId",
                table: "JobApplications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ScreenedAt",
                table: "JobApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ScreeningDetails",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "ScreeningScore",
                table: "JobApplications",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ShortlistedAt",
                table: "JobApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_HodRecommendedByUserId",
                table: "JobApplications",
                column: "HodRecommendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_RecruiterRecommendedByUserId",
                table: "JobApplications",
                column: "RecruiterRecommendedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_VacancyId_ScreeningScore",
                table: "JobApplications",
                columns: new[] { "VacancyId", "ScreeningScore" });

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_Users_HodRecommendedByUserId",
                table: "JobApplications",
                column: "HodRecommendedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_Users_RecruiterRecommendedByUserId",
                table: "JobApplications",
                column: "RecruiterRecommendedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_Users_HodRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_Users_RecruiterRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_HodRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_RecruiterRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_VacancyId_ScreeningScore",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "HodNote",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "HodRecommendation",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "HodRecommendedAt",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "HodRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "MeetsMinimum",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterNote",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterRecommendation",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterRecommendedAt",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "RecruiterRecommendedByUserId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ScreenedAt",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ScreeningDetails",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ScreeningScore",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "ShortlistedAt",
                table: "JobApplications");

            migrationBuilder.AlterColumn<string>(
                name: "Skills",
                table: "JobApplications",
                type: "varchar(3000)",
                maxLength: 3000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "OutcomeReason",
                table: "JobApplications",
                type: "varchar(2000)",
                maxLength: 2000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "CoverLetter",
                table: "JobApplications",
                type: "varchar(4000)",
                maxLength: 4000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "Certifications",
                table: "JobApplications",
                type: "varchar(3000)",
                maxLength: 3000,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "text",
                oldNullable: true);
        }
    }
}
