using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// Phase 15: the ATS. New columns on JobApplications (CV text and what was read from it, band, ATS decision), knockout
    /// questions and answers, and the one-row AtsSettings table (created by the first save; the defaults apply until then).
    /// </summary>
    public partial class Phase15_AtsEngine : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AtsClassification",
                table: "JobApplications",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtsDecision",
                table: "JobApplications",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "None"); // existing applications were never rejected by the ATS (an empty value could not be read back)

            migrationBuilder.AddColumn<DateTime>(
                name: "AtsDecisionAt",
                table: "JobApplications",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtsDecisionByUserId",
                table: "JobApplications",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtsDecisionNote",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AtsRejectionReason",
                table: "JobApplications",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvParsed",
                table: "JobApplications",
                type: "mediumtext",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CvText",
                table: "JobApplications",
                type: "mediumtext",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "AtsSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EducationWeight = table.Column<int>(type: "int", nullable: false),
                    ExperienceWeight = table.Column<int>(type: "int", nullable: false),
                    SkillsWeight = table.Column<int>(type: "int", nullable: false),
                    CertificationWeight = table.Column<int>(type: "int", nullable: false),
                    HighlyRecommendedFrom = table.Column<int>(type: "int", nullable: false),
                    RecommendedFrom = table.Column<int>(type: "int", nullable: false),
                    ReviewRequiredFrom = table.Column<int>(type: "int", nullable: false),
                    LowMatchFrom = table.Column<int>(type: "int", nullable: false),
                    RejectOnKnockout = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RejectBelowLowMatch = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RejectBelowMinimumEducation = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RejectBelowMinimumExperience = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RejectMissingRequiredCertification = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    RejectMissingRequiredSkill = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    UseAiReader = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    UpdatedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    IsDeleted = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DeletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    DeletedBy = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AtsSettings", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JobApplicationKnockoutAnswers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    Question = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Answer = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_JobApplicationKnockoutAnswers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplicationKnockoutAnswers_JobApplications_JobApplication~",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VacancyKnockoutQuestions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    Text = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyKnockoutQuestions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VacancyKnockoutQuestions_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_AtsDecisionByUserId",
                table: "JobApplications",
                column: "AtsDecisionByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_VacancyId_AtsDecision",
                table: "JobApplications",
                columns: new[] { "VacancyId", "AtsDecision" });

            migrationBuilder.CreateIndex(
                name: "IX_AtsSettings_IsDeleted",
                table: "AtsSettings",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationKnockoutAnswers_JobApplicationId",
                table: "JobApplicationKnockoutAnswers",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyKnockoutQuestions_VacancyId",
                table: "VacancyKnockoutQuestions",
                column: "VacancyId");

            migrationBuilder.AddForeignKey(
                name: "FK_JobApplications_Users_AtsDecisionByUserId",
                table: "JobApplications",
                column: "AtsDecisionByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_JobApplications_Users_AtsDecisionByUserId",
                table: "JobApplications");

            migrationBuilder.DropTable(
                name: "AtsSettings");

            migrationBuilder.DropTable(
                name: "JobApplicationKnockoutAnswers");

            migrationBuilder.DropTable(
                name: "VacancyKnockoutQuestions");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_AtsDecisionByUserId",
                table: "JobApplications");

            migrationBuilder.DropIndex(
                name: "IX_JobApplications_VacancyId_AtsDecision",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsClassification",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsDecision",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsDecisionAt",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsDecisionByUserId",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsDecisionNote",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "AtsRejectionReason",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CvParsed",
                table: "JobApplications");

            migrationBuilder.DropColumn(
                name: "CvText",
                table: "JobApplications");
        }
    }
}
