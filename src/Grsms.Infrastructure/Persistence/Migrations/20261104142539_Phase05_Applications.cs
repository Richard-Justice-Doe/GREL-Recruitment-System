using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase05_Applications : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "CandidateProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    UserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    Location = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    HighestEducation = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    YearsExperience = table.Column<int>(type: "int", nullable: false),
                    CurrentEmployer = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    CurrentPosition = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Skills = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true),
                    Certifications = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    Summary = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_CandidateProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CandidateProfiles_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JobApplications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ApplicationNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    JobAdvertId = table.Column<int>(type: "int", nullable: true),
                    ApplicantUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    IsInternal = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Source = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    FullName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
                    Phone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Location = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    HighestEducation = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    YearsExperience = table.Column<int>(type: "int", nullable: false),
                    CurrentEmployer = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    CurrentPosition = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Skills = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true),
                    Certifications = table.Column<string>(type: "varchar(3000)", maxLength: 3000, nullable: true),
                    LinkedInUrl = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: true),
                    CoverLetter = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    OutcomeReason = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    Version = table.Column<Guid>(type: "char(36)", nullable: false),
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
                    table.PrimaryKey("PK_JobApplications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplications_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_JobAdverts_JobAdvertId",
                        column: x => x.JobAdvertId,
                        principalTable: "JobAdverts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Users_ApplicantUserId",
                        column: x => x.ApplicantUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplications_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JobApplicationActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ActorName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Comments = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
                    ActionAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                    table.PrimaryKey("PK_JobApplicationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobApplicationActions_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobApplicationActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_IsDeleted",
                table: "CandidateProfiles",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_CandidateProfiles_UserId",
                table: "CandidateProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationActions_ActorUserId",
                table: "JobApplicationActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationActions_IsDeleted",
                table: "JobApplicationActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplicationActions_JobApplicationId_ActionAt",
                table: "JobApplicationActions",
                columns: new[] { "JobApplicationId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ApplicantUserId",
                table: "JobApplications",
                column: "ApplicantUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_ApplicationNumber",
                table: "JobApplications",
                column: "ApplicationNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_DepartmentId_Status",
                table: "JobApplications",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_DivisionId_Status",
                table: "JobApplications",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_IsDeleted",
                table: "JobApplications",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_JobAdvertId",
                table: "JobApplications",
                column: "JobAdvertId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_RequestingHodUserId",
                table: "JobApplications",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_SubmittedAt",
                table: "JobApplications",
                column: "SubmittedAt");

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_VacancyId_ApplicantUserId",
                table: "JobApplications",
                columns: new[] { "VacancyId", "ApplicantUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobApplications_VacancyId_Status",
                table: "JobApplications",
                columns: new[] { "VacancyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "CandidateProfiles");

            migrationBuilder.DropTable(
                name: "JobApplicationActions");

            migrationBuilder.DropTable(
                name: "JobApplications");
        }
    }
}
