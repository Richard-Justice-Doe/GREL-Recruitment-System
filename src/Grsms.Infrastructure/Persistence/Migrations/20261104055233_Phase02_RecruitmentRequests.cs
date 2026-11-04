using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase02_RecruitmentRequests : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Attachments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OwnerType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    OwnerId = table.Column<int>(type: "int", nullable: false),
                    Category = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    OriginalFileName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    StoredFileName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    ContentType = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Sha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_Attachments", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "NumberSequences",
                columns: table => new
                {
                    Key = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    LastValue = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NumberSequences", x => x.Key);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecruitmentRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RequestNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    UnitId = table.Column<int>(type: "int", nullable: true),
                    SiteId = table.Column<int>(type: "int", nullable: false),
                    PositionTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    NumberOfPositions = table.Column<int>(type: "int", nullable: false),
                    EmploymentType = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    VacancyReason = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ReplacedEmployee = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    Justification = table.Column<string>(type: "varchar(4000)", maxLength: 4000, nullable: false),
                    BudgetCode = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: false),
                    EstimatedAnnualCost = table.Column<decimal>(type: "decimal(18,2)", precision: 18, scale: 2, nullable: false),
                    BudgetValidated = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    BudgetValidatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    RequestedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    AssignedRecruiterUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    DateRequested = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApprovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
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
                    table.PrimaryKey("PK_RecruitmentRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Sites_SiteId",
                        column: x => x.SiteId,
                        principalTable: "Sites",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Units_UnitId",
                        column: x => x.UnitId,
                        principalTable: "Units",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Users_AssignedRecruiterUserId",
                        column: x => x.AssignedRecruiterUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Users_BudgetValidatedByUserId",
                        column: x => x.BudgetValidatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequests_Users_RequestedByUserId",
                        column: x => x.RequestedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "RecruitmentRequestActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    RecruitmentRequestId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
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
                    table.PrimaryKey("PK_RecruitmentRequestActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequestActions_RecruitmentRequests_RecruitmentReq~",
                        column: x => x.RecruitmentRequestId,
                        principalTable: "RecruitmentRequests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_RecruitmentRequestActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_IsDeleted",
                table: "Attachments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_OwnerType_OwnerId",
                table: "Attachments",
                columns: new[] { "OwnerType", "OwnerId" });

            migrationBuilder.CreateIndex(
                name: "IX_Attachments_StoredFileName",
                table: "Attachments",
                column: "StoredFileName",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequestActions_ActorUserId",
                table: "RecruitmentRequestActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequestActions_IsDeleted",
                table: "RecruitmentRequestActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequestActions_RecruitmentRequestId_ActionAt",
                table: "RecruitmentRequestActions",
                columns: new[] { "RecruitmentRequestId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_AssignedRecruiterUserId",
                table: "RecruitmentRequests",
                column: "AssignedRecruiterUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_BudgetValidatedByUserId",
                table: "RecruitmentRequests",
                column: "BudgetValidatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_DepartmentId_Status",
                table: "RecruitmentRequests",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_IsDeleted",
                table: "RecruitmentRequests",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_RequestedByUserId",
                table: "RecruitmentRequests",
                column: "RequestedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_RequestNumber",
                table: "RecruitmentRequests",
                column: "RequestNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_SiteId",
                table: "RecruitmentRequests",
                column: "SiteId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_Status",
                table: "RecruitmentRequests",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_UnitId",
                table: "RecruitmentRequests",
                column: "UnitId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Attachments");

            migrationBuilder.DropTable(
                name: "NumberSequences");

            migrationBuilder.DropTable(
                name: "RecruitmentRequestActions");

            migrationBuilder.DropTable(
                name: "RecruitmentRequests");
        }
    }
}
