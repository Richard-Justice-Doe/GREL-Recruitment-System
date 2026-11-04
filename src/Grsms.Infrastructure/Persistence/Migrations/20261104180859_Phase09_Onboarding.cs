using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase09_Onboarding : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Onboardings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Number = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    OfferId = table.Column<int>(type: "int", nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    EmployeeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    JobTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    EmployeeNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportTime = table.Column<TimeOnly>(type: "time", nullable: false),
                    ReportLocation = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    ContactPersonUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    FirstDayNotes = table.Column<string>(type: "text", nullable: true),
                    LetterNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: true),
                    LetterTerms = table.Column<string>(type: "text", nullable: true),
                    LetterIssuedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LetterIssuedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    LetterAcknowledgedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    LetterAcknowledgedName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    LetterAcknowledgedIp = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    CancelReason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Onboardings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Onboardings_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Offers_OfferId",
                        column: x => x.OfferId,
                        principalTable: "Offers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Users_ContactPersonUserId",
                        column: x => x.ContactPersonUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Users_LetterIssuedByUserId",
                        column: x => x.LetterIssuedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Onboardings_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OnboardingTasks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    Team = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Title = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: true),
                    AssignedToUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CompletedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
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
                    table.PrimaryKey("PK_OnboardingTasks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingTasks_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingTasks_Users_AssignedToUserId",
                        column: x => x.AssignedToUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingTasks_Users_CompletedByUserId",
                        column: x => x.CompletedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_ContactPersonUserId",
                table: "Onboardings",
                column: "ContactPersonUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_CreatedByUserId",
                table: "Onboardings",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_DepartmentId_Status",
                table: "Onboardings",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_DivisionId_Status",
                table: "Onboardings",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_EmployeeUserId",
                table: "Onboardings",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_IsDeleted",
                table: "Onboardings",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_JobApplicationId",
                table: "Onboardings",
                column: "JobApplicationId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_LetterIssuedByUserId",
                table: "Onboardings",
                column: "LetterIssuedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_LetterNumber",
                table: "Onboardings",
                column: "LetterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_Number",
                table: "Onboardings",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_OfferId",
                table: "Onboardings",
                column: "OfferId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_RequestingHodUserId",
                table: "Onboardings",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_SiteId",
                table: "Onboardings",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_Status_StartDate",
                table: "Onboardings",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_Onboardings_VacancyId",
                table: "Onboardings",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_AssignedToUserId_Status",
                table: "OnboardingTasks",
                columns: new[] { "AssignedToUserId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_CompletedByUserId",
                table: "OnboardingTasks",
                column: "CompletedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_IsDeleted",
                table: "OnboardingTasks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_OnboardingRecordId_SortOrder",
                table: "OnboardingTasks",
                columns: new[] { "OnboardingRecordId", "SortOrder" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "OnboardingTasks");

            migrationBuilder.DropTable(
                name: "Onboardings");
        }
    }
}
