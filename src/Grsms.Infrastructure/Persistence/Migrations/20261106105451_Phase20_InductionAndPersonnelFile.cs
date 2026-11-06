using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase20_InductionAndPersonnelFile : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Area",
                table: "OnboardingTasks",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "AssignedAt",
                table: "OnboardingTasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Description",
                table: "OnboardingTasks",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "VerifiedAt",
                table: "OnboardingTasks",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "VerifiedByUserId",
                table: "OnboardingTasks",
                type: "varchar(255)",
                maxLength: 255,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InductionAreaSettings",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Area = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    OwnerUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    IsDefault = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    Tasks = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("PK_InductionAreaSettings", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InductionAreaSettings_Users_OwnerUserId",
                        column: x => x.OwnerUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PersonnelFileCategories",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    SortOrder = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PersonnelFileCategories", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PersonnelFiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    FileNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    EmployeeName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    StaffNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    JobTitle = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
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
                    table.PrimaryKey("PK_PersonnelFiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonnelFiles_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelFiles_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelFiles_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "PersonnelFileDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonnelFileId = table.Column<int>(type: "int", nullable: false),
                    CategoryId = table.Column<int>(type: "int", nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Kind = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    AttachmentId = table.Column<int>(type: "int", nullable: true),
                    Content = table.Column<string>(type: "text", nullable: true),
                    SourceType = table.Column<string>(type: "varchar(50)", maxLength: 50, nullable: true),
                    SourceId = table.Column<int>(type: "int", nullable: true),
                    DocumentDate = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    AddedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
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
                    table.PrimaryKey("PK_PersonnelFileDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PersonnelFileDocuments_Attachments_AttachmentId",
                        column: x => x.AttachmentId,
                        principalTable: "Attachments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelFileDocuments_PersonnelFileCategories_CategoryId",
                        column: x => x.CategoryId,
                        principalTable: "PersonnelFileCategories",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelFileDocuments_PersonnelFiles_PersonnelFileId",
                        column: x => x.PersonnelFileId,
                        principalTable: "PersonnelFiles",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_PersonnelFileDocuments_Users_AddedByUserId",
                        column: x => x.AddedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "PersonnelFileCategories",
                columns: new[] { "Id", "Name", "SortOrder" },
                values: new object[,]
                {
                    { 1, "Recruitment Documents", 1 },
                    { 2, "Employee Records", 2 },
                    { 3, "Identification Records", 3 },
                    { 4, "Compliance Records", 4 },
                    { 5, "Onboarding Records", 5 },
                    { 6, "Probation Records", 6 },
                    { 7, "Generated Letters", 7 }
                });

            migrationBuilder.CreateIndex(
                name: "IX_OnboardingTasks_VerifiedByUserId",
                table: "OnboardingTasks",
                column: "VerifiedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_InductionAreaSettings_Area",
                table: "InductionAreaSettings",
                column: "Area",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_InductionAreaSettings_IsDeleted",
                table: "InductionAreaSettings",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_InductionAreaSettings_OwnerUserId",
                table: "InductionAreaSettings",
                column: "OwnerUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_AddedByUserId",
                table: "PersonnelFileDocuments",
                column: "AddedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_AttachmentId",
                table: "PersonnelFileDocuments",
                column: "AttachmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_CategoryId",
                table: "PersonnelFileDocuments",
                column: "CategoryId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_IsDeleted",
                table: "PersonnelFileDocuments",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_PersonnelFileId_CategoryId",
                table: "PersonnelFileDocuments",
                columns: new[] { "PersonnelFileId", "CategoryId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFileDocuments_SourceType_SourceId",
                table: "PersonnelFileDocuments",
                columns: new[] { "SourceType", "SourceId" });

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFiles_DepartmentId",
                table: "PersonnelFiles",
                column: "DepartmentId");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFiles_EmployeeUserId",
                table: "PersonnelFiles",
                column: "EmployeeUserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFiles_FileNumber",
                table: "PersonnelFiles",
                column: "FileNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFiles_IsDeleted",
                table: "PersonnelFiles",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_PersonnelFiles_OnboardingRecordId",
                table: "PersonnelFiles",
                column: "OnboardingRecordId");

            migrationBuilder.AddForeignKey(
                name: "FK_OnboardingTasks_Users_VerifiedByUserId",
                table: "OnboardingTasks",
                column: "VerifiedByUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            // ---- Existing checklist tasks join the departmental induction model (nothing is lost):
            //   HR and IT tasks get their area; every task's assigned date is when it was created; tasks already finished
            //   count as verified by whoever finished them (verification did not exist then, so it cannot block them now).
            migrationBuilder.Sql("UPDATE OnboardingTasks SET Area = 'HumanResource' WHERE Team = 'HumanResources' AND Area IS NULL;");
            migrationBuilder.Sql("UPDATE OnboardingTasks SET Area = 'CorporateIt' WHERE Team = 'InformationTechnology' AND Area IS NULL;");
            migrationBuilder.Sql("UPDATE OnboardingTasks SET AssignedAt = CreatedAt WHERE AssignedAt IS NULL;");
            migrationBuilder.Sql("UPDATE OnboardingTasks SET VerifiedAt = CompletedAt, VerifiedByUserId = CompletedByUserId " +
                                 "WHERE Status <> 'Open' AND VerifiedAt IS NULL;");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_OnboardingTasks_Users_VerifiedByUserId",
                table: "OnboardingTasks");

            migrationBuilder.DropTable(
                name: "InductionAreaSettings");

            migrationBuilder.DropTable(
                name: "PersonnelFileDocuments");

            migrationBuilder.DropTable(
                name: "PersonnelFileCategories");

            migrationBuilder.DropTable(
                name: "PersonnelFiles");

            migrationBuilder.DropIndex(
                name: "IX_OnboardingTasks_VerifiedByUserId",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "Area",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "AssignedAt",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "Description",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "VerifiedAt",
                table: "OnboardingTasks");

            migrationBuilder.DropColumn(
                name: "VerifiedByUserId",
                table: "OnboardingTasks");
        }
    }
}
