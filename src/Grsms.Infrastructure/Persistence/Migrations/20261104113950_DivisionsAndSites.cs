using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

#pragma warning disable CA1814 // Prefer jagged arrays over multidimensional

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class DivisionsAndSites : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DivisionId",
                table: "Vacancies",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<string>(
                name: "RequestingHodUserId",
                table: "Vacancies",
                type: "varchar(255)",
                maxLength: 255,
                nullable: false,
                defaultValue: "");

            migrationBuilder.AddColumn<string>(
                name: "SiteType",
                table: "Sites",
                type: "varchar(150)",
                maxLength: 150,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "DivisionId",
                table: "RecruitmentRequests",
                type: "int",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "DivisionId",
                table: "Departments",
                type: "int",
                nullable: false,
                defaultValue: 1); // departments added by HR start in Corporate (1); the seeded ones are set below

            migrationBuilder.CreateTable(
                name: "Divisions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "varchar(1000)", maxLength: 1000, nullable: true),
                    IsActive = table.Column<bool>(type: "tinyint(1)", nullable: false),
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
                    table.PrimaryKey("PK_Divisions", x => x.Id);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 1,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 2,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 3,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 4,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 5,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 6,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 7,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 8,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 9,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 10,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 11,
                columns: new[] { "DivisionId", "IsActive" },
                values: new object[] { 2, false });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 12,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 13,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 14,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 15,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 16,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "DivisionId", "IsActive", "Name" },
                values: new object[] { 1, true, "Transport" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 18,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 19,
                columns: new[] { "DivisionId", "IsActive" },
                values: new object[] { 2, false });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 20,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 21,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 22,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 23,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 24,
                column: "DivisionId",
                value: 1);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 25,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 26,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 27,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 28,
                columns: new[] { "DivisionId", "IsActive" },
                values: new object[] { 2, false });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 29,
                columns: new[] { "DivisionId", "Name" },
                values: new object[] { 2, "RuTAP" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 30,
                column: "DivisionId",
                value: 2);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 31,
                columns: new[] { "DivisionId", "Name" },
                values: new object[] { 2, "Factory APM" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 32,
                columns: new[] { "DivisionId", "Name" },
                values: new object[] { 2, "Factory TBU" });

            migrationBuilder.InsertData(
                table: "Divisions",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "IsActive", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 1, "CORP", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, null, true, false, "Corporate", null, null },
                    { 2, "OPS", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, null, true, false, "Operations", null, null }
                });

            migrationBuilder.UpdateData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 1,
                columns: new[] { "Location", "SiteType" },
                values: new object[] { "Takoradi", "Corporate Headquarters" });

            migrationBuilder.InsertData(
                table: "Sites",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "IsActive", "IsDeleted", "Location", "Name", "SiteType", "UpdatedAt", "UpdatedBy" },
                values: new object[,]
                {
                    { 2, "ABU", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Abura", "Rubber Plantation Estate", null, null },
                    { 3, "APM", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Apimenim (APM)", "Rubber Processing Factory", null, null },
                    { 4, "RUTAP", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "RuTAP", "Rubber Outgrower & Technical Assistance Programme Operations", null, null },
                    { 5, "TSI", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Tsibu", null, null, null },
                    { 6, "ALB", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Allabokazo", null, null, null },
                    { 7, "ASA", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Assin Adubease", null, null, null },
                    { 8, "MAN", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Manso Amenfi", null, null, null },
                    { 9, "SUB", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Subri", "Rubber Plantation Estate", null, null },
                    { 10, "AWO", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Awodua", null, null, null },
                    { 11, "NSU", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Nsuame", null, null, null },
                    { 12, "AGA", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Agona Abream", null, null, null },
                    { 13, "TBU", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "TBU Factory", "Rubber Processing Factory (1D1F Expansion Project)", null, null },
                    { 14, "NUC", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, true, false, null, "Nucleus Operations Areas", "Estate Management and Plantation Operations", null, null }
                });

            migrationBuilder.InsertData(
                table: "Departments",
                columns: new[] { "Id", "Code", "CreatedAt", "CreatedBy", "DeletedAt", "DeletedBy", "Description", "DivisionId", "HeadOfDepartmentUserId", "IsActive", "IsDeleted", "Name", "UpdatedAt", "UpdatedBy" },
                values: new object[] { 33, "NUC", new DateTime(2026, 11, 3, 0, 0, 0, 0, DateTimeKind.Utc), "system", null, null, null, 2, null, true, false, "Nucleus Operations", null, null });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_DivisionId_Status",
                table: "Vacancies",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_RequestingHodUserId",
                table: "Vacancies",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_RecruitmentRequests_DivisionId_Status",
                table: "RecruitmentRequests",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments",
                column: "DivisionId");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Code",
                table: "Divisions",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_IsDeleted",
                table: "Divisions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_Divisions_Name",
                table: "Divisions",
                column: "Name",
                unique: true);

            // Existing records: copy the division from their department, and the requesting HOD from the request,
            // so every row is valid before the foreign keys are switched on. (Written to work on MySQL and SQLite.)
            migrationBuilder.Sql("UPDATE RecruitmentRequests SET DivisionId = (SELECT d.DivisionId FROM Departments d WHERE d.Id = RecruitmentRequests.DepartmentId);");
            migrationBuilder.Sql("UPDATE Vacancies SET DivisionId = (SELECT d.DivisionId FROM Departments d WHERE d.Id = Vacancies.DepartmentId), RequestingHodUserId = (SELECT r.RequestedByUserId FROM RecruitmentRequests r WHERE r.Id = Vacancies.RecruitmentRequestId);");

            migrationBuilder.AddForeignKey(
                name: "FK_Departments_Divisions_DivisionId",
                table: "Departments",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RecruitmentRequests_Divisions_DivisionId",
                table: "RecruitmentRequests",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vacancies_Divisions_DivisionId",
                table: "Vacancies",
                column: "DivisionId",
                principalTable: "Divisions",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vacancies_Users_RequestingHodUserId",
                table: "Vacancies",
                column: "RequestingHodUserId",
                principalTable: "Users",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Departments_Divisions_DivisionId",
                table: "Departments");

            migrationBuilder.DropForeignKey(
                name: "FK_RecruitmentRequests_Divisions_DivisionId",
                table: "RecruitmentRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacancies_Divisions_DivisionId",
                table: "Vacancies");

            migrationBuilder.DropForeignKey(
                name: "FK_Vacancies_Users_RequestingHodUserId",
                table: "Vacancies");

            migrationBuilder.DropTable(
                name: "Divisions");

            migrationBuilder.DropIndex(
                name: "IX_Vacancies_DivisionId_Status",
                table: "Vacancies");

            migrationBuilder.DropIndex(
                name: "IX_Vacancies_RequestingHodUserId",
                table: "Vacancies");

            migrationBuilder.DropIndex(
                name: "IX_RecruitmentRequests_DivisionId_Status",
                table: "RecruitmentRequests");

            migrationBuilder.DropIndex(
                name: "IX_Departments_DivisionId",
                table: "Departments");

            migrationBuilder.DeleteData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 33);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 2);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 3);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 4);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 5);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 6);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 7);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 8);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 9);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 10);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 11);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 12);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 13);

            migrationBuilder.DeleteData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 14);

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "RequestingHodUserId",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "SiteType",
                table: "Sites");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "DivisionId",
                table: "Departments");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 11,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 17,
                columns: new[] { "IsActive", "Name" },
                values: new object[] { false, "Transport & Logistics" });

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 19,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 28,
                column: "IsActive",
                value: true);

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 29,
                column: "Name",
                value: "RUTAP");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 31,
                column: "Name",
                value: "APM Factory");

            migrationBuilder.UpdateData(
                table: "Departments",
                keyColumn: "Id",
                keyValue: 32,
                column: "Name",
                value: "TBU Factory");

            migrationBuilder.UpdateData(
                table: "Sites",
                keyColumn: "Id",
                keyValue: 1,
                column: "Location",
                value: null);
        }
    }
}
