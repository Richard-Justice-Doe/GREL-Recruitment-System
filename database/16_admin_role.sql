START TRANSACTION;
INSERT INTO `Roles` (`Id`, `ConcurrencyStamp`, `Name`, `NormalizedName`)
VALUES ('admin', 'admin', 'Admin', 'ADMIN');
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261105101830_AdminRole', '10.0.14');

COMMIT;

