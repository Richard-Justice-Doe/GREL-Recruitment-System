using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase19_PersonalRecordDeclaration : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "EmployeePersonalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    OnboardingRecordId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Qualification = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    SsnitNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    ResidentialAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    HomeTown = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Region = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: true),
                    Tribe = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Religion = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    Email = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: true),
                    MobileNumber = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    HasDisability = table.Column<bool>(type: "tinyint(1)", nullable: false),
                    DisabilityDetails = table.Column<string>(type: "text", nullable: true),
                    VisibleMarks = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    NextOfKinName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true),
                    NextOfKinTelephone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true),
                    NextOfKinRelationship = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    NextOfKinDateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    NextOfKinAddress = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    DeclaredAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CurrentDeclarationId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_EmployeePersonalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeePersonalRecords_Onboardings_OnboardingRecordId",
                        column: x => x.OnboardingRecordId,
                        principalTable: "Onboardings",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeePersonalRecords_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeBankAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    BankName = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Branch = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    AccountNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    IsPrimary = table.Column<bool>(type: "tinyint(1)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBankAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBankAccounts_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeBeneficiaries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    Percentage = table.Column<decimal>(type: "decimal(5,2)", precision: 5, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeBeneficiaries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeBeneficiaries_EmployeePersonalRecords_PersonalRecord~",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeChildren",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Gender = table.Column<string>(type: "varchar(10)", maxLength: 10, nullable: false),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: false),
                    PlaceOfBirth = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    CurrentResidence = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeChildren", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeChildren_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeDeclarations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    EmployeeUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    DeclarationText = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: false),
                    SignatureName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    SignedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    IpAddress = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: true),
                    Snapshot = table.Column<string>(type: "text", nullable: false),
                    SnapshotSha256 = table.Column<string>(type: "varchar(64)", maxLength: 64, nullable: false),
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
                    table.PrimaryKey("PK_EmployeeDeclarations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDeclarations_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_EmployeeDeclarations_Users_EmployeeUserId",
                        column: x => x.EmployeeUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeDependants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Age = table.Column<int>(type: "int", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeDependants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeDependants_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeEmergencyContacts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    Name = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Telephone1 = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Telephone2 = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeEmergencyContacts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeEmergencyContacts_EmployeePersonalRecords_PersonalRe~",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeLanguages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    Language = table.Column<string>(type: "varchar(60)", maxLength: 60, nullable: false),
                    Proficiency = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeLanguages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeLanguages_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "EmployeeSpouses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    PersonalRecordId = table.Column<int>(type: "int", nullable: false),
                    FullName = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Occupation = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: true),
                    DateOfBirth = table.Column<DateTime>(type: "date", nullable: true),
                    Address = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    Telephone = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EmployeeSpouses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EmployeeSpouses_EmployeePersonalRecords_PersonalRecordId",
                        column: x => x.PersonalRecordId,
                        principalTable: "EmployeePersonalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBankAccounts_PersonalRecordId",
                table: "EmployeeBankAccounts",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeBeneficiaries_PersonalRecordId",
                table: "EmployeeBeneficiaries",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeChildren_PersonalRecordId",
                table: "EmployeeChildren",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeclarations_EmployeeUserId",
                table: "EmployeeDeclarations",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeclarations_IsDeleted",
                table: "EmployeeDeclarations",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDeclarations_PersonalRecordId_SignedAt",
                table: "EmployeeDeclarations",
                columns: new[] { "PersonalRecordId", "SignedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeDependants_PersonalRecordId",
                table: "EmployeeDependants",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeEmergencyContacts_PersonalRecordId",
                table: "EmployeeEmergencyContacts",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeLanguages_PersonalRecordId",
                table: "EmployeeLanguages",
                column: "PersonalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePersonalRecords_EmployeeUserId",
                table: "EmployeePersonalRecords",
                column: "EmployeeUserId");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePersonalRecords_IsDeleted",
                table: "EmployeePersonalRecords",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_EmployeePersonalRecords_OnboardingRecordId",
                table: "EmployeePersonalRecords",
                column: "OnboardingRecordId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EmployeeSpouses_PersonalRecordId",
                table: "EmployeeSpouses",
                column: "PersonalRecordId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "EmployeeBankAccounts");

            migrationBuilder.DropTable(
                name: "EmployeeBeneficiaries");

            migrationBuilder.DropTable(
                name: "EmployeeChildren");

            migrationBuilder.DropTable(
                name: "EmployeeDeclarations");

            migrationBuilder.DropTable(
                name: "EmployeeDependants");

            migrationBuilder.DropTable(
                name: "EmployeeEmergencyContacts");

            migrationBuilder.DropTable(
                name: "EmployeeLanguages");

            migrationBuilder.DropTable(
                name: "EmployeeSpouses");

            migrationBuilder.DropTable(
                name: "EmployeePersonalRecords");
        }
    }
}
