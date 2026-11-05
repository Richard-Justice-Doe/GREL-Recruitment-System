using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <summary>
    /// Phase 14: the Super Admin role, granular permissions (stored as RoleClaims rows) and the KnownPermissions table.
    /// RoleClaims was empty until now, so shortening its two text columns loses nothing.
    /// The default grants themselves are added at start-up by PermissionSeeder, not here.
    /// </summary>
    public partial class Phase14_SuperAdminAndPermissions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                table: "RoleClaims",
                type: "varchar(300)",
                maxLength: 300,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                table: "RoleClaims",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "longtext",
                oldNullable: true);

            migrationBuilder.CreateTable(
                name: "KnownPermissions",
                columns: table => new
                {
                    Name = table.Column<string>(type: "varchar(100)", maxLength: 100, nullable: false),
                    AddedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_KnownPermissions", x => x.Name);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "super-admin", "super-admin", "Super Admin", "SUPER ADMIN" });

            // The new index starts with RoleId, so it serves the foreign key; MySQL only lets the old index go once it exists.
            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims",
                columns: new[] { "RoleId", "ClaimType", "ClaimValue" },
                unique: true);

            migrationBuilder.DropIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "KnownPermissions");

            migrationBuilder.CreateIndex(
                name: "IX_RoleClaims_RoleId",
                table: "RoleClaims",
                column: "RoleId");

            migrationBuilder.DropIndex(
                name: "IX_RoleClaims_RoleId_ClaimType_ClaimValue",
                table: "RoleClaims");

            migrationBuilder.Sql("DELETE FROM RoleClaims WHERE RoleId = 'super-admin';");
            migrationBuilder.Sql("DELETE FROM UserRoles WHERE RoleId = 'super-admin';");
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "super-admin");

            migrationBuilder.AlterColumn<string>(
                name: "ClaimValue",
                table: "RoleClaims",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(300)",
                oldMaxLength: 300,
                oldNullable: true);

            migrationBuilder.AlterColumn<string>(
                name: "ClaimType",
                table: "RoleClaims",
                type: "longtext",
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true);
        }
    }
}
