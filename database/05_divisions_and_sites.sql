START TRANSACTION;
ALTER TABLE `Vacancies` ADD `DivisionId` int NOT NULL DEFAULT 0;

ALTER TABLE `Vacancies` ADD `RequestingHodUserId` varchar(255) NOT NULL DEFAULT '';

ALTER TABLE `Sites` ADD `SiteType` varchar(150) NULL;

ALTER TABLE `RecruitmentRequests` ADD `DivisionId` int NOT NULL DEFAULT 0;

ALTER TABLE `Departments` ADD `DivisionId` int NOT NULL DEFAULT 1;

CREATE TABLE `Divisions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Name` varchar(100) NOT NULL,
    `Code` varchar(20) NOT NULL,
    `Description` varchar(1000) NULL,
    `IsActive` tinyint(1) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 1;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 2;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 3;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 4;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 5;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 6;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 7;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 8;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 9;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 10;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `IsActive` = FALSE
WHERE `Id` = 11;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 12;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 13;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 14;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 15;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 16;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1, `IsActive` = TRUE, `Name` = 'Transport'
WHERE `Id` = 17;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 18;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `IsActive` = FALSE
WHERE `Id` = 19;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 20;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 21;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 22;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 23;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 1
WHERE `Id` = 24;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 25;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 26;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 27;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `IsActive` = FALSE
WHERE `Id` = 28;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `Name` = 'RuTAP'
WHERE `Id` = 29;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2
WHERE `Id` = 30;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `Name` = 'Factory APM'
WHERE `Id` = 31;
SELECT ROW_COUNT();


UPDATE `Departments` SET `DivisionId` = 2, `Name` = 'Factory TBU'
WHERE `Id` = 32;
SELECT ROW_COUNT();


INSERT INTO `Divisions` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (1, 'CORP', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, TRUE, FALSE, 'Corporate', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Divisions` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (2, 'OPS', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, TRUE, FALSE, 'Operations', NULL, NULL);
SELECT ROW_COUNT();


UPDATE `Sites` SET `Location` = 'Takoradi', `SiteType` = 'Corporate Headquarters'
WHERE `Id` = 1;
SELECT ROW_COUNT();


INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (2, 'ABU', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Abura', 'Rubber Plantation Estate', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (3, 'APM', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Apimenim (APM)', 'Rubber Processing Factory', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (4, 'RUTAP', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'RuTAP', 'Rubber Outgrower & Technical Assistance Programme Operations', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (5, 'TSI', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Tsibu', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (6, 'ALB', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Allabokazo', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (7, 'ASA', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Assin Adubease', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (8, 'MAN', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Manso Amenfi', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (9, 'SUB', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Subri', 'Rubber Plantation Estate', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (10, 'AWO', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Awodua', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (11, 'NSU', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Nsuame', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (12, 'AGA', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Agona Abream', NULL, NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (13, 'TBU', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'TBU Factory', 'Rubber Processing Factory (1D1F Expansion Project)', NULL, NULL);
SELECT ROW_COUNT();

INSERT INTO `Sites` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `IsActive`, `IsDeleted`, `Location`, `Name`, `SiteType`, `UpdatedAt`, `UpdatedBy`)
VALUES (14, 'NUC', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, TRUE, FALSE, NULL, 'Nucleus Operations Areas', 'Estate Management and Plantation Operations', NULL, NULL);
SELECT ROW_COUNT();


INSERT INTO `Departments` (`Id`, `Code`, `CreatedAt`, `CreatedBy`, `DeletedAt`, `DeletedBy`, `Description`, `DivisionId`, `HeadOfDepartmentUserId`, `IsActive`, `IsDeleted`, `Name`, `UpdatedAt`, `UpdatedBy`)
VALUES (33, 'NUC', '2026-11-03 00:00:00.000000', 'system', NULL, NULL, NULL, 2, NULL, TRUE, FALSE, 'Nucleus Operations', NULL, NULL);
SELECT ROW_COUNT();


CREATE INDEX `IX_Vacancies_DivisionId_Status` ON `Vacancies` (`DivisionId`, `Status`);

CREATE INDEX `IX_Vacancies_RequestingHodUserId` ON `Vacancies` (`RequestingHodUserId`);

CREATE INDEX `IX_RecruitmentRequests_DivisionId_Status` ON `RecruitmentRequests` (`DivisionId`, `Status`);

CREATE INDEX `IX_Departments_DivisionId` ON `Departments` (`DivisionId`);

CREATE UNIQUE INDEX `IX_Divisions_Code` ON `Divisions` (`Code`);

CREATE INDEX `IX_Divisions_IsDeleted` ON `Divisions` (`IsDeleted`);

CREATE UNIQUE INDEX `IX_Divisions_Name` ON `Divisions` (`Name`);

UPDATE RecruitmentRequests SET DivisionId = (SELECT d.DivisionId FROM Departments d WHERE d.Id = RecruitmentRequests.DepartmentId);

UPDATE Vacancies SET DivisionId = (SELECT d.DivisionId FROM Departments d WHERE d.Id = Vacancies.DepartmentId), RequestingHodUserId = (SELECT r.RequestedByUserId FROM RecruitmentRequests r WHERE r.Id = Vacancies.RecruitmentRequestId);

ALTER TABLE `Departments` ADD CONSTRAINT `FK_Departments_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `RecruitmentRequests` ADD CONSTRAINT `FK_RecruitmentRequests_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `Vacancies` ADD CONSTRAINT `FK_Vacancies_Divisions_DivisionId` FOREIGN KEY (`DivisionId`) REFERENCES `Divisions` (`Id`) ON DELETE RESTRICT;

ALTER TABLE `Vacancies` ADD CONSTRAINT `FK_Vacancies_Users_RequestingHodUserId` FOREIGN KEY (`RequestingHodUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104113950_DivisionsAndSites', '10.0.14');

COMMIT;

