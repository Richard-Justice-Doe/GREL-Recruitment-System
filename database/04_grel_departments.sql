START TRANSACTION;
UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 6;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 7;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 8;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 9;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 10;
SELECT ROW_COUNT();


UPDATE `Departments` SET `Code` = 'PQ', `Name` = 'Progress and Quality'
WHERE `Id` = 11;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 12;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 13;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 14;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 17;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 18;
SELECT ROW_COUNT();


UPDATE `Departments` SET `Name` = 'Project'
WHERE `Id` = 19;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 21;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 22;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 23;
SELECT ROW_COUNT();


UPDATE `Departments` SET `IsActive` = FALSE
WHERE `Id` = 24;
SELECT ROW_COUNT();


INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (25, 'CIV', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Civil & Infrastructure', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (26, 'STR', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Stores', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (27, 'GAR', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Garage', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (28, 'EST', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'Estates', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (29, 'RUTAP', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'RUTAP', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (30, 'DTC', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'DTC', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (31, 'APM', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'APM Factory', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (32, 'TBU', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, NULL, TRUE, FALSE, 'TBU Factory', NULL, NULL);
SELECT ROW_COUNT();


INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104095611_GrelDepartments', '10.0.14');

COMMIT;

