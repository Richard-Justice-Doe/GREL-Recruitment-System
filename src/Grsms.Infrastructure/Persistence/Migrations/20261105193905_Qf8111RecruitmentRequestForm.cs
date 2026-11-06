using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class Qf8111RecruitmentRequestForm : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "RequisitionNumber",
                table: "Vacancies",
                type: "varchar(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CertificateRequired",
                table: "RecruitmentRequests",
                type: "varchar(500)",
                maxLength: 500,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "DrivingLicenceClass",
                table: "RecruitmentRequests",
                type: "varchar(50)",
                maxLength: 50,
                nullable: true);

            migrationBuilder.AddColumn<bool>(
                name: "DrivingLicenceRequired",
                table: "RecruitmentRequests",
                type: "tinyint(1)",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<string>(
                name: "EducationLevel",
                table: "RecruitmentRequests",
                type: "varchar(30)",
                maxLength: 30,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "MinYearsExperience",
                table: "RecruitmentRequests",
                type: "int",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PostDescription",
                table: "RecruitmentRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "ProfessionalExperience",
                table: "RecruitmentRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ProposedStartDate",
                table: "RecruitmentRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "PurposeOther",
                table: "RecruitmentRequests",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "QualificationCategory",
                table: "RecruitmentRequests",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "Remarks",
                table: "RecruitmentRequests",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "RequestDate",
                table: "RecruitmentRequests",
                type: "date",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "StaffCategory",
                table: "RecruitmentRequests",
                type: "varchar(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "TrainingLevel",
                table: "RecruitmentRequests",
                type: "varchar(200)",
                maxLength: 200,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_Vacancies_RequisitionNumber",
                table: "Vacancies",
                column: "RequisitionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "IX_Vacancies_RequisitionNumber",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "RequisitionNumber",
                table: "Vacancies");

            migrationBuilder.DropColumn(
                name: "CertificateRequired",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceClass",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "DrivingLicenceRequired",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "EducationLevel",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "MinYearsExperience",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "PostDescription",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "ProfessionalExperience",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "ProposedStartDate",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "PurposeOther",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "QualificationCategory",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "Remarks",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "RequestDate",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "StaffCategory",
                table: "RecruitmentRequests");

            migrationBuilder.DropColumn(
                name: "TrainingLevel",
                table: "RecruitmentRequests");
        }
    }
}
