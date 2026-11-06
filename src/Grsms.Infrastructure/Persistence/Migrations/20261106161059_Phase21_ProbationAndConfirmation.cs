using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase21_ProbationAndConfirmation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ProbationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Number = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    EmployeeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    StaffNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    JobTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    StartDate = table.Column<DateTime>(type: "date", nullable: false),
                    EndDate = table.Column<DateTime>(type: "date", nullable: false),
                    OriginalEndDate = table.Column<DateTime>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    HodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    HrManagerUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    InitialMeetingDueDate = table.Column<DateTime>(type: "date", nullable: false),
                    InitialMeetingDate = table.Column<DateTime>(type: "date", nullable: true),
                    InitialMeetingNotes = table.Column<string>(type: "text", nullable: true),
                    EmployeeSignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    EmployeeSignatureName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    HodSignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    HodSignatureName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    ExtendedMonths = table.Column<int>(type: "int", nullable: false),
                    ExtensionReason = table.Column<string>(type: "text", nullable: true),
                    ExtensionImprovementPlan = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_ProbationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationRecords_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationRecords_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationRecords_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationRecords_Users_HodUserId",
                        column: x => x.HodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationRecords_Users_HrManagerUserId",
                        column: x => x.HrManagerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ConfirmationRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ProbationRecordId = table.Column<int>(type: "int", nullable: false),
                    LetterNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    ConfirmationDate = table.Column<DateTime>(type: "date", nullable: false),
                    ApprovedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    PersonnelFileDocumentId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_ConfirmationRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ConfirmationRecords_PersonnelFileDocuments_PersonnelFileDocu~",
                        column: x => x.PersonnelFileDocumentId,
                        principalTable: "PersonnelFileDocuments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmationRecords_ProbationRecords_ProbationRecordId",
                        column: x => x.ProbationRecordId,
                        principalTable: "ProbationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmationRecords_Users_ApprovedByUserId",
                        column: x => x.ApprovedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ConfirmationRecords_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProbationActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProbationRecordId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
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
                    table.PrimaryKey("PK_ProbationActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationActions_ProbationRecords_ProbationRecordId",
                        column: x => x.ProbationRecordId,
                        principalTable: "ProbationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProbationAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProbationRecordId = table.Column<int>(type: "int", nullable: false),
                    Kind = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    AssessorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    AssessedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    ApplicationOfSkills = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    QualityOfWork = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Productivity = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Cooperation = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Dependability = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AbilityToSupervise = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Comments = table.Column<string>(type: "text", nullable: false),
                    ImprovementPlan = table.Column<string>(type: "text", nullable: true),
                    Outcome = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    ExtensionMonths = table.Column<int>(type: "int", nullable: true),
                    ExtensionReason = table.Column<string>(type: "text", nullable: true),
                    HrApproved = table.Column<bool>(type: "tinyint(1)", nullable: true),
                    HrComments = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_ProbationAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationAssessments_ProbationRecords_ProbationRecordId",
                        column: x => x.ProbationRecordId,
                        principalTable: "ProbationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_ProbationAssessments_Users_AssessorUserId",
                        column: x => x.AssessorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProbationDevelopmentPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProbationRecordId = table.Column<int>(type: "int", nullable: false),
                    TrainingNeed = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    DevelopmentNeed = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Timeline = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbationDevelopmentPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationDevelopmentPlans_ProbationRecords_ProbationRecordId",
                        column: x => x.ProbationRecordId,
                        principalTable: "ProbationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "ProbationObjectives",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    ProbationRecordId = table.Column<int>(type: "int", nullable: false),
                    Objective = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    SuccessIndicators = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: false),
                    DueDate = table.Column<DateTime>(type: "date", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProbationObjectives", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProbationObjectives_ProbationRecords_ProbationRecordId",
                        column: x => x.ProbationRecordId,
                        principalTable: "ProbationRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_ApprovedByUserId",
                table: "ConfirmationRecords",
                column: "ApprovedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_EmployeeUserId",
                table: "ConfirmationRecords",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_IsDeleted",
                table: "ConfirmationRecords",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_LetterNumber",
                table: "ConfirmationRecords",
                column: "LetterNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_PersonnelFileDocumentId",
                table: "ConfirmationRecords",
                column: "PersonnelFileDocumentId");

            migrationBuilder.CreateIndex(
                name: "IX_ConfirmationRecords_ProbationRecordId",
                table: "ConfirmationRecords",
                column: "ProbationRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProbationActions_ActorUserId",
                table: "ProbationActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationActions_IsDeleted",
                table: "ProbationActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationActions_ProbationRecordId_ActionAt",
                table: "ProbationActions",
                columns: new[] { "ProbationRecordId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbationAssessments_AssessorUserId",
                table: "ProbationAssessments",
                column: "AssessorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationAssessments_IsDeleted",
                table: "ProbationAssessments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationAssessments_ProbationRecordId_Kind",
                table: "ProbationAssessments",
                columns: new[] { "ProbationRecordId", "Kind" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbationDevelopmentPlans_ProbationRecordId",
                table: "ProbationDevelopmentPlans",
                column: "ProbationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationObjectives_ProbationRecordId",
                table: "ProbationObjectives",
                column: "ProbationRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_DepartmentId_Status",
                table: "ProbationRecords",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_EmployeeUserId",
                table: "ProbationRecords",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_HodUserId",
                table: "ProbationRecords",
                column: "HodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_HrManagerUserId",
                table: "ProbationRecords",
                column: "HrManagerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_IsDeleted",
                table: "ProbationRecords",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_Number",
                table: "ProbationRecords",
                column: "Number",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_OnboardingRecordId",
                table: "ProbationRecords",
                column: "OnboardingRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ProbationRecords_Status_EndDate",
                table: "ProbationRecords",
                columns: new[] { "Status", "EndDate" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ConfirmationRecords");

            migrationBuilder.DropTable(
                name: "ProbationActions");

            migrationBuilder.DropTable(
                name: "ProbationAssessments");

            migrationBuilder.DropTable(
                name: "ProbationDevelopmentPlans");

            migrationBuilder.DropTable(
                name: "ProbationObjectives");

            migrationBuilder.DropTable(
                name: "ProbationRecords");
        }
    }
}
