START TRANSACTION;
ALTER TABLE `Employees` ADD `DateOfBirth` date NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261122105506_EmployeeDateOfBirth', '10.0.14');

COMMIT;

