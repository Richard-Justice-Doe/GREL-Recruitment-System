START TRANSACTION;
ALTER TABLE `EmployeeDocuments` ADD `ExpiresOn` date NULL;

ALTER TABLE `Attachments` ADD `ImageHeight` int NULL;

ALTER TABLE `Attachments` ADD `ImageWidth` int NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261113165447_DocumentChecks', '10.0.14');

COMMIT;

