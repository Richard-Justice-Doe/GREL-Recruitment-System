using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    /// <summary>
    /// 5 November 2026: the built-in Admin role (system administrator, between HR Administrator and Super Admin). Its
    /// default permissions are given at start-up by PermissionSeeder (Permissions.RolesAddedLater). If a Super Admin had
    /// already added a CUSTOM role called "Admin", rename it first: role names are unique.
    /// </summary>
    public partial class AdminRole : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.InsertData(
                table: "Roles",
                columns: new[] { "Id", "ConcurrencyStamp", "Name", "NormalizedName" },
                values: new object[] { "admin", "admin", "Admin", "ADMIN" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DELETE FROM RoleClaims WHERE RoleId = 'admin';");
            migrationBuilder.Sql("DELETE FROM UserRoles WHERE RoleId = 'admin';");
            migrationBuilder.Sql("DELETE FROM KnownPermissions WHERE Name = 'role:Admin';");
            migrationBuilder.DeleteData(
                table: "Roles",
                keyColumn: "Id",
                keyValue: "admin");
        }
    }
}
