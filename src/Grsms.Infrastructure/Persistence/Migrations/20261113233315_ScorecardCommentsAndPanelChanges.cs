using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class ScorecardCommentsAndPanelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "Comment",
                table: "ScorecardRatings",
                type: "varchar(1000)",
                maxLength: 1000,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "InterviewPanelChanges",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    InterviewId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    OriginalUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    ReplacementUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: true),
                    Reason = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    ActorUserId = table.Column<string>(type: "varchar(450)", maxLength: 450, nullable: false),
                    ActorName = table.Column<string>(type: "varchar(256)", maxLength: 256, nullable: false),
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
                    table.PrimaryKey("PK_InterviewPanelChanges", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InterviewPanelChanges_Interviews_InterviewId",
                        column: x => x.InterviewId,
                        principalTable: "Interviews",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelChanges_InterviewId",
                table: "InterviewPanelChanges",
                column: "InterviewId");

            migrationBuilder.CreateIndex(
                name: "IX_InterviewPanelChanges_IsDeleted",
                table: "InterviewPanelChanges",
                column: "IsDeleted");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "InterviewPanelChanges");

            migrationBuilder.DropColumn(
                name: "Comment",
                table: "ScorecardRatings");
        }
    }
}
