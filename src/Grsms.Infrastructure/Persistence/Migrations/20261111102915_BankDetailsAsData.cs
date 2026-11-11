using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace Grsms.Infrastructure.Persistence.Migrations
{
    /// <inheritdoc />
    public partial class BankDetailsAsData : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Branch",
                table: "EmployeeBankAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: true,
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100);

            migrationBuilder.UpdateData(
                table: "OnboardingDocumentTypes",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsActive",
                value: false);

            // 11 Nov 2026 (docs/29, change 4): the bank letter is no longer asked for on onboardings not yet activated. It stops
            // counting everywhere; rows that already have files stay visible (history, personnel file), the others are hidden.
            migrationBuilder.Sql(
                "UPDATE `EmployeeDocuments` d JOIN `Onboardings` o ON o.`Id` = d.`OnboardingRecordId` " +
                "SET d.`IsRequired` = 0, d.`IsApplicable` = CASE WHEN EXISTS (SELECT 1 FROM `Attachments` a WHERE a.`OwnerType` = 'EmployeeDocument' " +
                "AND a.`OwnerId` = d.`Id` AND a.`IsDeleted` = 0) THEN 1 ELSE 0 END " +
                "WHERE d.`DocumentType` = 'BankConfirmationLetter' AND o.`Status` IN ('Draft', 'AwaitingEmployeeSubmission', " +
                "'AwaitingDocumentVerification', 'AwaitingHrApproval', 'AwaitingActivation');");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterColumn<string>(
                name: "Branch",
                table: "EmployeeBankAccounts",
                type: "varchar(100)",
                maxLength: 100,
                nullable: false,
                defaultValue: "",
                oldClrType: typeof(string),
                oldType: "varchar(100)",
                oldMaxLength: 100,
                oldNullable: true);

            migrationBuilder.UpdateData(
                table: "OnboardingDocumentTypes",
                keyColumn: "Id",
                keyValue: 17,
                column: "IsActive",
                value: true);
        }
    }
}
