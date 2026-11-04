using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase07_Interviews : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Interviews",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobApplicationId = table.Column<int>(type: "int", nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Round = table.Column<int>(type: "int", nullable: false),
                    Type = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Mode = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    StartsAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    DurationMinutes = table.Column<int>(type: "int", nullable: false),
                    Location = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    CandidateInstructions = table.Column<string>(type: "text", nullable: true),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    StatusReason = table.Column<string>(type: "text", nullable: true),
                    ScheduledByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_Interviews", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Interviews_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_JobApplications_JobApplicationId",
                        column: x => x.JobApplicationId,
                        principalTable: "JobApplications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_Users_ScheduledByUserId",
                        column: x => x.ScheduledByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Interviews_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InterviewCriteria",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Weight = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewCriteria", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewCriteria_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "InterviewPanelMembers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    UserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    IsChair = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InterviewPanelMembers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPanelMembers_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InterviewPanelMembers_Users_UserId",
                        column: x => x.UserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "Scorecards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    PanelUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Recommendation = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Strengths = table.Column<string>(type: "text", nullable: true),
                    Concerns = table.Column<string>(type: "text", nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    WeightedScore = table.Column<decimal>(type: "decimal(5,1)", precision: 5, scale: 1, nullable: false),
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
                    table.PrimaryKey("PK_Scorecards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Scorecards_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Scorecards_Users_PanelUserId",
                        column: x => x.PanelUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ScorecardRatings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ScorecardId = table.Column<int>(type: "int", nullable: false),
                    CriterionId = table.Column<int>(type: "int", nullable: false),
                    Score = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ScorecardRatings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ScorecardRatings_InterviewCriteria_CriterionId",
                        column: x => x.CriterionId,
                        principalTable: "InterviewCriteria",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ScorecardRatings_Scorecards_ScorecardId",
                        column: x => x.ScorecardId,
                        principalTable: "Scorecards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewCriteria_InterviewId",
                table: "InterviewCriteria",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelMembers_InterviewId_UserId",
                table: "InterviewPanelMembers",
                columns: new[] { "InterviewId", "UserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelMembers_UserId",
                table: "InterviewPanelMembers",
                column: "UserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_DepartmentId_Status",
                table: "Interviews",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_DivisionId_Status",
                table: "Interviews",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_IsDeleted",
                table: "Interviews",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_JobApplicationId_Round",
                table: "Interviews",
                columns: new[] { "JobApplicationId", "Round" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_RequestingHodUserId",
                table: "Interviews",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_ScheduledByUserId",
                table: "Interviews",
                column: "ScheduledByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_Status_StartsAt",
                table: "Interviews",
                columns: new[] { "Status", "StartsAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Interviews_VacancyId_Status",
                table: "Interviews",
                columns: new[] { "VacancyId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardRatings_CriterionId",
                table: "ScorecardRatings",
                column: "CriterionId");

            migrationBuilder.CreateIndex(
                name: "IX_ScorecardRatings_ScorecardId_CriterionId",
                table: "ScorecardRatings",
                columns: new[] { "ScorecardId", "CriterionId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scorecards_InterviewId_PanelUserId",
                table: "Scorecards",
                columns: new[] { "InterviewId", "PanelUserId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Scorecards_IsDeleted",
                table: "Scorecards",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Scorecards_PanelUserId",
                table: "Scorecards",
                column: "PanelUserId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPanelMembers");

            migrationBuilder.DropTable(
                name: "ScorecardRatings");

            migrationBuilder.DropTable(
                name: "InterviewCriteria");

            migrationBuilder.DropTable(
                name: "Scorecards");

            migrationBuilder.DropTable(
                name: "Interviews");
        }
    }
}
