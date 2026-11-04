using System;
using Microsoft.EntityFrameworkCore.Migrations;
using MySql.EntityFrameworkCore.Metadata;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Phase04_JobAdverts : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "JobAdverts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    AdvertNumber = table.Column<string>(type: "varchar(40)", maxLength: 40, nullable: false),
                    VacancyId = table.Column<int>(type: "int", nullable: false),
                    DivisionId = table.Column<int>(type: "int", nullable: false),
                    DepartmentId = table.Column<int>(type: "int", nullable: false),
                    RequestingHodUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    Title = table.Column<string>(type: "varchar(200)", maxLength: 200, nullable: false),
                    Body = table.Column<string>(type: "longtext", nullable: false),
                    HowToApply = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    StatusChangedAt = table.Column<DateTime>(type: "datetime(6)", nullable: false),
                    PublishedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    CreatedByUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: false),
                    ClonedFromAdvertId = table.Column<int>(type: "int", nullable: true),
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
                    table.PrimaryKey("PK_JobAdverts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobAdverts_Departments_DepartmentId",
                        column: x => x.DepartmentId,
                        principalTable: "Departments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdverts_Divisions_DivisionId",
                        column: x => x.DivisionId,
                        principalTable: "Divisions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdverts_JobAdverts_ClonedFromAdvertId",
                        column: x => x.ClonedFromAdvertId,
                        principalTable: "JobAdverts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdverts_Users_CreatedByUserId",
                        column: x => x.CreatedByUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdverts_Users_RequestingHodUserId",
                        column: x => x.RequestingHodUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdverts_Vacancies_VacancyId",
                        column: x => x.VacancyId,
                        principalTable: "Vacancies",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "AdvertChannels",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobAdvertId = table.Column<int>(type: "int", nullable: false),
                    Channel = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    Status = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    Reference = table.Column<string>(type: "varchar(500)", maxLength: 500, nullable: true),
                    PostedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true),
                    PostedByName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: true),
                    RemovedAt = table.Column<DateTime>(type: "datetime(6)", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AdvertChannels", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AdvertChannels_JobAdverts_JobAdvertId",
                        column: x => x.JobAdvertId,
                        principalTable: "JobAdverts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateTable(
                name: "JobAdvertActions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "int", nullable: false)
                        .Annotation("MySQL:ValueGenerationStrategy", MySQLValueGenerationStrategy.IdentityColumn),
                    JobAdvertId = table.Column<int>(type: "int", nullable: false),
                    Action = table.Column<string>(type: "varchar(30)", maxLength: 30, nullable: false),
                    FromStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ToStatus = table.Column<string>(type: "varchar(20)", maxLength: 20, nullable: false),
                    ActorUserId = table.Column<string>(type: "varchar(255)", maxLength: 255, nullable: true),
                    ActorName = table.Column<string>(type: "varchar(150)", maxLength: 150, nullable: false),
                    Comments = table.Column<string>(type: "varchar(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("PK_JobAdvertActions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_JobAdvertActions_JobAdverts_JobAdvertId",
                        column: x => x.JobAdvertId,
                        principalTable: "JobAdverts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_JobAdvertActions_Users_ActorUserId",
                        column: x => x.ActorUserId,
                        principalTable: "Users",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                })
                .Annotation("MySQL:Charset", "utf8mb4");

            migrationBuilder.CreateIndex(
                name: "IX_AdvertChannels_JobAdvertId_Channel",
                table: "AdvertChannels",
                columns: new[] { "JobAdvertId", "Channel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobAdvertActions_ActorUserId",
                table: "JobAdvertActions",
                column: "ActorUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdvertActions_IsDeleted",
                table: "JobAdvertActions",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdvertActions_JobAdvertId_ActionAt",
                table: "JobAdvertActions",
                columns: new[] { "JobAdvertId", "ActionAt" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_AdvertNumber",
                table: "JobAdverts",
                column: "AdvertNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_ClonedFromAdvertId",
                table: "JobAdverts",
                column: "ClonedFromAdvertId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_CreatedByUserId",
                table: "JobAdverts",
                column: "CreatedByUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_DepartmentId_Status",
                table: "JobAdverts",
                columns: new[] { "DepartmentId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_DivisionId_Status",
                table: "JobAdverts",
                columns: new[] { "DivisionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_IsDeleted",
                table: "JobAdverts",
                column: "IsDeleted");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_RequestingHodUserId",
                table: "JobAdverts",
                column: "RequestingHodUserId");

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_Status_EndDate",
                table: "JobAdverts",
                columns: new[] { "Status", "EndDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_Status_StartDate",
                table: "JobAdverts",
                columns: new[] { "Status", "StartDate" });

            migrationBuilder.CreateIndex(
                name: "IX_JobAdverts_VacancyId_Status",
                table: "JobAdverts",
                columns: new[] { "VacancyId", "Status" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AdvertChannels");

            migrationBuilder.DropTable(
                name: "JobAdvertActions");

            migrationBuilder.DropTable(
                name: "JobAdverts");
        }
    }
}
