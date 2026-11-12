using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <summary>12 November 2026 (docs/35, Wave 1): panel replies (accept / decline with reason), HOD delegates and interview quorum. Adds columns only: existing sessions are not held to quorum (QuorumRequired = false) and existing members are "awaiting reply".</summary>
    public partial class InterviewPanelReplies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "QuorumRequired",
                table: "Interviews",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "DeclineReason",
                table: "InterviewPanelMembers",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "IsHodDelegate",
                table: "InterviewPanelMembers",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<DateTime>(
                name: "RespondedAt",
                table: "InterviewPanelMembers",
                type: "datetime(6)",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Response",
                table: "InterviewPanelMembers",
                type: "varchar(20)",
                maxLength: 20,
                nullable: false,
                defaultValue: "Pending");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "QuorumRequired",
                table: "Interviews");

            migrationBuilder.DropColumn(
                name: "DeclineReason",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "IsHodDelegate",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "RespondedAt",
                table: "InterviewPanelMembers");

            migrationBuilder.DropColumn(
                name: "Response",
                table: "InterviewPanelMembers");
        }
    }
}
