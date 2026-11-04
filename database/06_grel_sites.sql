START TRANSACTION;
UPDATE `Sites` SET `Name` = 'Apeminim'
WHERE `Id` = 3;
SELECT ROW_COUNT();


UPDATE `Sites` SET `SiteType` = 'Rubber Processing Factory (1D1F Expansion Project)'
WHERE `Id` = 5;
SELECT ROW_COUNT();


UPDATE `Sites` SET `IsActive` = FALSE
WHERE `Id` = 13;
SELECT ROW_COUNT();


UPDATE `Sites` SET `IsActive` = FALSE
WHERE `Id` = 14;
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104120006_GrelSites', '10.0.14');

COMMIT;

