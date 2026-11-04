using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase03_Vacancies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Vacancies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    VacancyNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    RecruitmentRequestId = table.Column<int>(type: "int", nullable: false),
                    JobTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    ReportingManagerUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    EmploymentType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    NumberOfPositions = table.Column<int>(type: "int", nullable: false),
                    VacancyType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    JobDescription = table.Column<string>(type: "longtext", nullable: false),
                    MinimumEducation = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FieldOfStudy = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    QualificationNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    MinYearsExperience = table.Column<int>(type: "int", nullable: false),
                    ExperienceNotes = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    SalaryMin = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    SalaryMax = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: true),
                    ShowSalary = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    ClosingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    RecruiterUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_Vacancies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vacancies_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_RecruitmentRequests_RecruitmentRequestId",
                        column: x => x.RecruitmentRequestId,
                        principalTable: "RecruitmentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Users_RecruiterUserId",
                        column: x => x.RecruiterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Vacancies_Users_ReportingManagerUserId",
                        column: x => x.ReportingManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VacancyActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_VacancyActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VacancyActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_VacancyActions_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "VacancyRequirements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    IsEssential = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VacancyRequirements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VacancyRequirements_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DepartmentId_Status",
                table: "Vacancies",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_IsDeleted",
                table: "Vacancies",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_RecruiterUserId",
                table: "Vacancies",
                column: "RecruiterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_RecruitmentRequestId",
                table: "Vacancies",
                column: "RecruitmentRequestId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_ReportingManagerUserId",
                table: "Vacancies",
                column: "ReportingManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_SiteId",
                table: "Vacancies",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status",
                table: "Vacancies",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status_ClosingDate",
                table: "Vacancies",
                columns: new[] { "Status", "ClosingDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_UnitId",
                table: "Vacancies",
                column: "UnitId");

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_VacancyNumber",
                table: "Vacancies",
                column: "VacancyNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VacancyActions_ActorUserId",
                table: "VacancyActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyActions_IsDeleted",
                table: "VacancyActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyActions_VacancyId_ActionAt",
                table: "VacancyActions",
                columns: new[] { "VacancyId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRequirements_Name",
                table: "VacancyRequirements",
                column: "Name");

            migrationBuilder.CreateIndex(
                name: "IX_VacancyRequirements_VacancyId_Type",
                table: "VacancyRequirements",
                columns: new[] { "VacancyId", "Type" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "VacancyActions");

            migrationBuilder.DropTable(
                name: "VacancyRequirements");

            migrationBuilder.DropTable(
                name: "Vacancies");
        }
    }
}
