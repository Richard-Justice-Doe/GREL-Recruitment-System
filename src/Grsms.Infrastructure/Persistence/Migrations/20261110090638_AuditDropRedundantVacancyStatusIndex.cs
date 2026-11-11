using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class AuditDropRedundantVacancyStatusIndex : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vacancies_Status",
                table: "Vacancies");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_Status",
                table: "Vacancies",
                column: "Status");
        }
    }
}
