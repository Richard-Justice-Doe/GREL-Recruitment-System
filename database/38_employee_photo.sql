START TRANSACTION;
ALTER TABLE `Employees` ADD `PhotoAttachmentId` int NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261117085359_EmployeePhoto', '10.0.14');

COMMIT;

