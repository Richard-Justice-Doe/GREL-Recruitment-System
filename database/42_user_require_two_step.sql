START TRANSACTION;
ALTER TABLE `Users` ADD `RequireTwoStep` tinyint(1) NOT NULL DEFAULT FALSE;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261122214412_UserRequireTwoStep', '10.0.14');

COMMIT;

