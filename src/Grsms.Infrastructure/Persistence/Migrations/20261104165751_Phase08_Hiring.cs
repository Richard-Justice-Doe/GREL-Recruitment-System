using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase08_Hiring : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "BackgroundChecks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Subject = table.Column<string>(type: "varchar(300)", maxLength: 300, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Notes = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    RecordedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_BackgroundChecks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BackgroundChecks_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_BackgroundChecks_Users_RecordedByUserId",
                        column: x => x.RecordedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "HiringRecommendations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Number = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ProposedSalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    ProposedStartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Justification = table.Column<string>(type: "text", nullable: false),
                    CreatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    HodDecision = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    HodComment = table.Column<string>(type: "text", nullable: true),
                    HodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    HodDecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HrDecision = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    HrComment = table.Column<string>(type: "text", nullable: true),
                    HrUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    HrDecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    MdDecision = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    MdComment = table.Column<string>(type: "text", nullable: true),
                    MdUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    MdDecidedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_HiringRecommendations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Users_HodUserId",
                        column: x => x.HodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Users_HrUserId",
                        column: x => x.HrUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Users_MdUserId",
                        column: x => x.MdUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_HiringRecommendations_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Offers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Number = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    HiringRecommendationId = table.Column<int>(type: "int", nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    JobTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    MonthlySalary = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiresOn = table.Column<DateOnly>(type: "date", nullable: false),
                    Terms = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    SentAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    SentByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    RespondedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_Offers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Offers_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_HiringRecommendations_HiringRecommendationId",
                        column: x => x.HiringRecommendationId,
                        principalTable: "HiringRecommendations",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_Users_SentByUserId",
                        column: x => x.SentByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Offers_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundChecks_IsDeleted",
                table: "BackgroundChecks",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundChecks_JobApplicationId",
                table: "BackgroundChecks",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_BackgroundChecks_RecordedByUserId",
                table: "BackgroundChecks",
                column: "RecordedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_CreatedByUserId",
                table: "HiringRecommendations",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_DepartmentId",
                table: "HiringRecommendations",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_DivisionId_Status",
                table: "HiringRecommendations",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_HodUserId",
                table: "HiringRecommendations",
                column: "HodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_HrUserId",
                table: "HiringRecommendations",
                column: "HrUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_IsDeleted",
                table: "HiringRecommendations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_JobApplicationId_Status",
                table: "HiringRecommendations",
                columns: new[] { "JobApplicationId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_MdUserId",
                table: "HiringRecommendations",
                column: "MdUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_Number",
                table: "HiringRecommendations",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_RequestingHodUserId",
                table: "HiringRecommendations",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_Status_DepartmentId",
                table: "HiringRecommendations",
                columns: new[] { "Status", "DepartmentId" });

            migrationBuilder.CreateIndex(
                name: "IX_HiringRecommendations_VacancyId",
                table: "HiringRecommendations",
                column: "VacancyId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_DepartmentId",
                table: "Offers",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_DivisionId_Status",
                table: "Offers",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Offers_HiringRecommendationId",
                table: "Offers",
                column: "HiringRecommendationId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_IsDeleted",
                table: "Offers",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_JobApplicationId",
                table: "Offers",
                column: "JobApplicationId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_Number",
                table: "Offers",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Offers_RequestingHodUserId",
                table: "Offers",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_SentByUserId",
                table: "Offers",
                column: "SentByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Offers_Status_ExpiresOn",
                table: "Offers",
                columns: new[] { "Status", "ExpiresOn" });

            migrationBuilder.CreateIndex(
                name: "IX_Offers_VacancyId_Status",
                table: "Offers",
                columns: new[] { "VacancyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BackgroundChecks");

            migrationBuilder.DropTable(
                name: "Offers");

            migrationBuilder.DropTable(
                name: "HiringRecommendations");
        }
    }
}
