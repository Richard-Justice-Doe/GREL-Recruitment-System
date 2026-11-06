using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase18_OnboardingWorkflow : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Onboardings",
                type: "varchar(40)",
                maxLength: 40,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(20)",
                oldMaxLength: 20);

            migrationBuilder.AddColumn<DateTime>(
                name: "CompletedAt",
                table: "Onboardings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ExpectedCompletionDate",
                table: "Onboardings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Notes",
                table: "Onboardings",
                type: "text",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "EmployeeDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    DocumentType = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsRequired = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    NotApplicableReason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    VerifiedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    VerifiedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocuments_Users_VerifiedByUserId",
                        column: x => x.VerifiedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OnboardingActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ActorName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Comments = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_OnboardingActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingActions_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_OnboardingActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_EmployeeUserId",
                table: "EmployeeDocuments",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_IsDeleted",
                table: "EmployeeDocuments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_OnboardingRecordId_DocumentType",
                table: "EmployeeDocuments",
                columns: new[] { "OnboardingRecordId", "DocumentType" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_Status",
                table: "EmployeeDocuments",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocuments_VerifiedByUserId",
                table: "EmployeeDocuments",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingActions_ActorUserId",
                table: "OnboardingActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingActions_IsDeleted",
                table: "OnboardingActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingActions_OnboardingRecordId_ActionAt",
                table: "OnboardingActions",
                columns: new[] { "OnboardingRecordId", "ActionAt" });

            // ---- Existing onboardings move to the matching stage of the new workflow (nothing is lost):
            //   in progress, letter not issued → Draft; issued but not signed → Awaiting employee submission;
            //   signed → Awaiting departmental induction (they started before the document checklist existed).
            migrationBuilder.Sql(
                "UPDATE Onboardings SET Status = CASE " +
                "WHEN LetterAcknowledgedAt IS NOT NULL THEN 'AwaitingDepartmentalInduction' " +
                "WHEN LetterIssuedAt IS NOT NULL THEN 'AwaitingEmployeeSubmission' " +
                "ELSE 'Draft' END WHERE Status = 'InProgress';");
            migrationBuilder.Sql("UPDATE Onboardings SET CompletedAt = StatusChangedAt WHERE Status = 'Completed';");
            migrationBuilder.Sql("UPDATE Onboardings SET ExpectedCompletionDate = DATE_ADD(StartDate, INTERVAL 30 DAY);");

            // Onboardings still waiting for the new employee get the document checklist (one Pending row per document).
            migrationBuilder.Sql(
                "INSERT INTO EmployeeDocuments (OnboardingRecordId, EmployeeUserId, DocumentType, IsRequired, Status, CreatedAt, CreatedBy, IsDeleted) " +
                "SELECT o.Id, o.EmployeeUserId, t.DocumentType, t.IsRequired, 'Pending', UTC_TIMESTAMP(6), 'system', 0 FROM Onboardings o CROSS JOIN (" +
                "SELECT 'ApplicationLetter' AS DocumentType, 1 AS IsRequired UNION ALL SELECT 'Cv', 1 UNION ALL SELECT 'PassportPicture', 1 " +
                "UNION ALL SELECT 'RecommendationLetters', 1 UNION ALL SELECT 'MedicalReport', 1 UNION ALL SELECT 'PoliceClearance', 1 " +
                "UNION ALL SELECT 'SchoolCertificates', 1 UNION ALL SELECT 'BirthCertificate', 1 UNION ALL SELECT 'GhanaCard', 1 " +
                "UNION ALL SELECT 'SsnitCard', 1 UNION ALL SELECT 'MarriageCertificate', 0 UNION ALL SELECT 'ChildBirthCertificates', 0 " +
                "UNION ALL SELECT 'DependantPhotographs', 0 UNION ALL SELECT 'BankConfirmationLetter', 1 UNION ALL SELECT 'PassportBiodataPage', 0 " +
                "UNION ALL SELECT 'NonCitizenCard', 0) t " +
                "WHERE o.IsDeleted = 0 AND o.Status IN ('Draft', 'AwaitingEmployeeSubmission');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("UPDATE Onboardings SET Status = 'InProgress' WHERE Status NOT IN ('Completed', 'Cancelled');");

            migrationBuilder.DropTable(
                name: "EmployeeDocuments");

            migrationBuilder.DropTable(
                name: "OnboardingActions");

            migrationBuilder.DropColumn(
                name: "CompletedAt",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "ExpectedCompletionDate",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "Notes",
                table: "Onboardings");

            migrationBuilder.AlterColumn<string>(
                name: "Status",
                table: "Onboardings",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                oldClrType: typeof(string),
                oldType: "varchar(40)",
                oldMaxLength: 40);
        }
    }
}
