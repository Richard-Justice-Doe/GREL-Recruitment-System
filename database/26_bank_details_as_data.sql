START TRANSACTION;
ALTER TABLE `EmployeeBankAccounts` MODIFY `Branch` varchar(100) NULL;

UPDATE `OnboardingDocumentTypes` SET `IsActive` = FALSE
WHERE `Id` = 17;
SELECT ROW_COUNT();


UPDATE `EmployeeDocuments` d JOIN `Onboardings` o ON o.`Id` = d.`OnboardingRecordId` SET d.`IsRequired` = 0, d.`IsApplicable` = CASE WHEN EXISTS (SELECT 1 FROM `Attachments` a WHERE a.`OwnerType` = 'EmployeeDocument' AND a.`OwnerId` = d.`Id` AND a.`IsDeleted` = 0) THEN 1 ELSE 0 END WHERE d.`DocumentType` = 'BankConfirmationLetter' AND o.`Status` IN ('Draft', 'AwaitingEmployeeSubmission', 'AwaitingDocumentVerification', 'AwaitingHrApproval', 'AwaitingActivation');

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261111102915_BankDetailsAsData', '10.0.14');

COMMIT;

