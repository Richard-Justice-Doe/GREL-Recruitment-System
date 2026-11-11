using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase27_OnboardingDocumentPortal : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "CorrectionMessage",
                table: "Onboardings",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CorrectionRequestedAt",
                table: "Onboardings",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "DocumentsDueDate",
                table: "Onboardings",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DocumentsDueDateExtended",
                table: "Onboardings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsExpatriate",
                table: "Onboardings",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "IsApplicable",
                table: "EmployeeDocuments",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: true); // existing documents all apply

            migrationBuilder.AddColumn<int>(
                name: "RejectionCount",
                table: "EmployeeDocuments",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RejectionReasonCode",
                table: "EmployeeDocuments",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ReviewStartedAt",
                table: "EmployeeDocuments",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ReviewStartedByUserId",
                table: "EmployeeDocuments",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "Version",
                table: "EmployeeDocuments",
                type: "char(36)",
                nullable: false,
                defaultValue: new Guid("00000000-0000-0000-0000-000000000000"));

            migrationBuilder.CreateTable(
                name: "EmployeeDocumentEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeDocumentId = table.Column<int>(type: "int", nullable: false),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: true),
                    FileName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    RejectionReason = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: true),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ActorName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
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
                    table.PrimaryKey("PK_EmployeeDocumentEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDocumentEvents_EmployeeDocuments_EmployeeDocumentId",
                        column: x => x.EmployeeDocumentId,
                        principalTable: "EmployeeDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDocumentEvents_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "OnboardingDocumentTypes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Code = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Description = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Group = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Requirement = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AllowsWordFiles = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    MinFiles = table.Column<int>(type: "int", nullable: false),
                    PersonnelFileCategoryId = table.Column<int>(type: "int", nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    IsLegacy = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_OnboardingDocumentTypes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_OnboardingDocumentTypes_PersonnelFileCategories_PersonnelFil~",
                        column: x => x.PersonnelFileCategoryId,
                        principalTable: "PersonnelFileCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "OnboardingDocumentTypes",
                columns: new[] { "Id", "AllowsWordFiles", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "Group", "IsActive", "IsDeleted", "IsLegacy", "MinFiles", "Name", "PersonnelFileCategoryId", "Requirement", "SortOrder", "UpdatedAt", "UpdatedBy", "Version" },
                values: new object[,]
                {
                    { 1, true, "ApplicationLetter", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "The letter you sent when applying.", "Personal", true, false, false, 1, "Application letter", 1, "Required", 10, null, null, new Guid("00000000-0000-0000-0027-000000000001") },
                    { 2, true, "Cv", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your current CV.", "Personal", true, false, false, 1, "Updated CV", 1, "Required", 20, null, null, new Guid("00000000-0000-0000-0027-000000000002") },
                    { 3, false, "PassportPicture", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "A recent passport-size photograph with a plain background.", "Personal", true, false, false, 1, "Passport-size photograph", 3, "Required", 30, null, null, new Guid("00000000-0000-0000-0027-000000000003") },
                    { 4, false, "GhanaCard", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Both sides of your Ghana Card.", "Personal", true, false, false, 1, "Ghana Card / National ID", 3, "GhanaianOnly", 40, null, null, new Guid("00000000-0000-0000-0027-000000000004") },
                    { 5, false, "SsnitCard", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your SSNIT card or biometric registration slip.", "Personal", true, false, false, 1, "SSNIT card", 3, "Required", 50, null, null, new Guid("00000000-0000-0000-0027-000000000005") },
                    { 6, false, "BirthCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your birth certificate.", "Personal", true, false, false, 1, "Birth certificate", 3, "Required", 60, null, null, new Guid("00000000-0000-0000-0027-000000000006") },
                    { 7, false, "BeceCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Basic Education Certificate Examination results.", "Education", true, false, false, 1, "BECE certificate", 2, "ChosenPerHire", 70, null, null, new Guid("00000000-0000-0000-0027-000000000007") },
                    { 8, false, "WassceCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "West African Senior School Certificate Examination results.", "Education", true, false, false, 1, "WASSCE / SSSCE certificate", 2, "ChosenPerHire", 80, null, null, new Guid("00000000-0000-0000-0027-000000000008") },
                    { 9, false, "DiplomaCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Diploma certificate (and transcript, if you have it).", "Education", true, false, false, 1, "Diploma certificate", 2, "ChosenPerHire", 90, null, null, new Guid("00000000-0000-0000-0027-000000000009") },
                    { 10, false, "HndCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Higher National Diploma certificate.", "Education", true, false, false, 1, "HND certificate", 2, "ChosenPerHire", 100, null, null, new Guid("00000000-0000-0000-0027-000000000010") },
                    { 11, false, "DegreeCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Bachelor's degree certificate (and transcript, if you have it).", "Education", true, false, false, 1, "Degree certificate", 2, "ChosenPerHire", 110, null, null, new Guid("00000000-0000-0000-0027-000000000011") },
                    { 12, false, "MastersCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Master's degree certificate.", "Education", true, false, false, 1, "Master's certificate", 2, "ChosenPerHire", 120, null, null, new Guid("00000000-0000-0000-0027-000000000012") },
                    { 13, false, "ProfessionalCertificates", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Professional or trade certificates (one file each).", "Education", true, false, false, 1, "Professional certifications", 2, "ChosenPerHire", 130, null, null, new Guid("00000000-0000-0000-0027-000000000013") },
                    { 14, true, "RecommendationLetters", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Two recommendation letters (one file each).", "Employment", true, false, false, 2, "Recommendation letters", 1, "Required", 140, null, null, new Guid("00000000-0000-0000-0027-000000000014") },
                    { 15, false, "PoliceClearance", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Police clearance report from the Ghana Police Service.", "Employment", true, false, false, 1, "Police clearance report", 4, "Required", 150, null, null, new Guid("00000000-0000-0000-0027-000000000015") },
                    { 16, false, "MedicalReport", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Medical examination report from a recognised hospital.", "Employment", true, false, false, 1, "Medical examination report", 4, "Required", 160, null, null, new Guid("00000000-0000-0000-0027-000000000016") },
                    { 17, false, "BankConfirmationLetter", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "A letter from your bank confirming your account name and number.", "Banking", true, false, false, 1, "Bank confirmation letter", 4, "Required", 170, null, null, new Guid("00000000-0000-0000-0027-000000000017") },
                    { 18, false, "MarriageCertificate", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "If you are married.", "Family", true, false, false, 1, "Marriage certificate", 2, "Optional", 180, null, null, new Guid("00000000-0000-0000-0027-000000000018") },
                    { 19, false, "ChildBirthCertificates", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "One file per child, if you have children.", "Family", true, false, false, 1, "Children's birth certificates", 2, "Optional", 190, null, null, new Guid("00000000-0000-0000-0027-000000000019") },
                    { 20, false, "DependantPhotographs", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Passport-size photographs of your dependants, if any.", "Family", true, false, false, 1, "Dependants' passport photographs", 2, "Optional", 200, null, null, new Guid("00000000-0000-0000-0027-000000000020") },
                    { 21, false, "PassportBiodataPage", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "The photo page of your passport.", "Expatriate", true, false, false, 1, "Passport biodata page", 3, "ExpatriateOnly", 210, null, null, new Guid("00000000-0000-0000-0027-000000000021") },
                    { 22, false, "NonCitizenCard", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your Ghana non-citizen identification card.", "Expatriate", true, false, false, 1, "Non-citizen card", 3, "ExpatriateOnly", 220, null, null, new Guid("00000000-0000-0000-0027-000000000022") },
                    { 23, false, "WorkPermit", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your Ghana Immigration Service work permit.", "Expatriate", true, false, false, 1, "Work permit", 4, "ExpatriateOnly", 230, null, null, new Guid("00000000-0000-0000-0027-000000000023") },
                    { 24, false, "ResidencePermit", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Your residence permit.", "Expatriate", true, false, false, 1, "Residence permit", 4, "ExpatriateOnly", 240, null, null, new Guid("00000000-0000-0000-0027-000000000024") },
                    { 25, false, "SchoolCertificates", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, "Kept for onboardings started before 10 November 2026.", "Education", false, false, true, 1, "School certificates (old checklist)", 2, "Required", 250, null, null, new Guid("00000000-0000-0000-0027-000000000025") }
                });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentEvents_EmployeeDocumentId_ActionAt",
                table: "EmployeeDocumentEvents",
                columns: new[] { "EmployeeDocumentId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentEvents_IsDeleted",
                table: "EmployeeDocumentEvents",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDocumentEvents_OnboardingRecordId",
                table: "EmployeeDocumentEvents",
                column: "OnboardingRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDocumentTypes_Code",
                table: "OnboardingDocumentTypes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDocumentTypes_IsDeleted",
                table: "OnboardingDocumentTypes",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingDocumentTypes_PersonnelFileCategoryId",
                table: "OnboardingDocumentTypes",
                column: "PersonnelFileCategoryId");

            // Phase 27 data changes (user's decisions, 10 November 2026):
            //  * there is no HR Manager approval any more, and the departmental induction now follows activation. Onboardings
            //    waiting for HR approval, or in the induction stage before activation (the old order), are ready for activation:
            //    their documents were already verified. Their induction tasks are kept; activation does not add them twice.
            //  * existing checklists keep their Phase 18 documents (the codes are the old type names, including the single
            //    "School certificates" document, kept as the legacy type).
            migrationBuilder.Sql(
                "UPDATE `Onboardings` SET `Status` = 'AwaitingActivation', `StatusChangedAt` = UTC_TIMESTAMP() " +
                "WHERE `Status` IN ('AwaitingHrApproval', 'AwaitingDepartmentalInduction');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeDocumentEvents");

            migrationBuilder.DropTable(
                name: "OnboardingDocumentTypes");

            migrationBuilder.DropColumn(
                name: "CorrectionMessage",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "CorrectionRequestedAt",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "DocumentsDueDate",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "DocumentsDueDateExtended",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "IsExpatriate",
                table: "Onboardings");

            migrationBuilder.DropColumn(
                name: "IsApplicable",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "RejectionCount",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "RejectionReasonCode",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ReviewStartedAt",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "ReviewStartedByUserId",
                table: "EmployeeDocuments");

            migrationBuilder.DropColumn(
                name: "Version",
                table: "EmployeeDocuments");
        }
    }
}
