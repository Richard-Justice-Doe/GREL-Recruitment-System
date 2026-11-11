START TRANSACTION;
ALTER TABLE `OnboardingTasks` ADD `Area` varchar(40) NULL;

ALTER TABLE `OnboardingTasks` ADD `AssignedAt` datetime(6) NULL;

ALTER TABLE `OnboardingTasks` ADD `Description` text NULL;

ALTER TABLE `OnboardingTasks` ADD `VerifiedAt` datetime(6) NULL;

ALTER TABLE `OnboardingTasks` ADD `VerifiedByUserId` varchar(255) NULL;

CREATE TABLE `InductionAreaSettings` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `Area` varchar(40) NOT NULL,
    `OwnerUserId` varchar(255) NULL,
    `IsDefault` tinyint(1) NOT NULL,
    `Tasks` text NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_InductionAreaSettings_Users_OwnerUserId` FOREIGN KEY (`OwnerUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `PersonnelFileCategories` (
    `Id` int NOT NULL,
    `Name` varchar(100) NOT NULL,
    `SortOrder` int NOT NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `PersonnelFiles` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `FileNumber` varchar(40) NOT NULL,
    `EmployeeUserId` varchar(255) NOT NULL,
    `EmployeeName` varchar(200) NOT NULL,
    `StaffNumber` varchar(30) NULL,
    `JobTitle` varchar(150) NOT NULL,
    `DepartmentId` int NOT NULL,
    `OnboardingRecordId` int NOT NULL,
    `OpenedAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PersonnelFiles_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonnelFiles_Onboardings_OnboardingRecordId` FOREIGN KEY (`OnboardingRecordId`) REFERENCES `Onboardings` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonnelFiles_Users_EmployeeUserId` FOREIGN KEY (`EmployeeUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `PersonnelFileDocuments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `PersonnelFileId` int NOT NULL,
    `CategoryId` int NOT NULL,
    `Title` varchar(200) NOT NULL,
    `Kind` varchar(20) NOT NULL,
    `AttachmentId` int NULL,
    `Content` text NULL,
    `SourceType` varchar(50) NULL,
    `SourceId` int NULL,
    `DocumentDate` datetime(6) NOT NULL,
    `AddedByUserId` varchar(255) NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_PersonnelFileDocuments_Attachments_AttachmentId` FOREIGN KEY (`AttachmentId`) REFERENCES `Attachments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonnelFileDocuments_PersonnelFileCategories_CategoryId` FOREIGN KEY (`CategoryId`) REFERENCES `PersonnelFileCategories` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonnelFileDocuments_PersonnelFiles_PersonnelFileId` FOREIGN KEY (`PersonnelFileId`) REFERENCES `PersonnelFiles` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_PersonnelFileDocuments_Users_AddedByUserId` FOREIGN KEY (`AddedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (1, 'Recruitment Documents', 1);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (2, 'Employee Records', 2);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (3, 'Identification Records', 3);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (4, 'Compliance Records', 4);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (5, 'Onboarding Records', 5);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (6, 'Probation Records', 6);
SELECT ROW_COUNT();

INSERT INTO `PersonnelFileCategories` (`Id`, `Name`, `SortOrder`)
VALUES (7, 'Generated Letters', 7);
SELECT ROW_COUNT();


CREATE INDEX `IX_OnboardingTasks_VerifiedByUserId` ON `OnboardingTasks` (`VerifiedByUserId`);

CREATE UNIQUE INDEX `IX_InductionAreaSettings_Area` ON `InductionAreaSettings` (`Area`);

CREATE INDEX `IX_InductionAreaSettings_IsDeleted` ON `InductionAreaSettings` (`IsDeleted`);

CREATE INDEX `IX_InductionAreaSettings_OwnerUserId` ON `InductionAreaSettings` (`OwnerUserId`);

CREATE INDEX `IX_PersonnelFileDocuments_AddedByUserId` ON `PersonnelFileDocuments` (`AddedByUserId`);

CREATE INDEX `IX_PersonnelFileDocuments_AttachmentId` ON `PersonnelFileDocuments` (`AttachmentId`);

CREATE INDEX `IX_PersonnelFileDocuments_CategoryId` ON `PersonnelFileDocuments` (`CategoryId`);

CREATE INDEX `IX_PersonnelFileDocuments_IsDeleted` ON `PersonnelFileDocuments` (`IsDeleted`);

CREATE INDEX `IX_PersonnelFileDocuments_PersonnelFileId_CategoryId` ON `PersonnelFileDocuments` (`PersonnelFileId`, `CategoryId`);

CREATE INDEX `IX_PersonnelFileDocuments_SourceType_SourceId` ON `PersonnelFileDocuments` (`SourceType`, `SourceId`);

CREATE INDEX `IX_PersonnelFiles_DepartmentId` ON `PersonnelFiles` (`DepartmentId`);

CREATE UNIQUE INDEX `IX_PersonnelFiles_EmployeeUserId` ON `PersonnelFiles` (`EmployeeUserId`);

CREATE UNIQUE INDEX `IX_PersonnelFiles_FileNumber` ON `PersonnelFiles` (`FileNumber`);

CREATE INDEX `IX_PersonnelFiles_IsDeleted` ON `PersonnelFiles` (`IsDeleted`);

CREATE INDEX `IX_PersonnelFiles_OnboardingRecordId` ON `PersonnelFiles` (`OnboardingRecordId`);

ALTER TABLE `OnboardingTasks` ADD CONSTRAINT `FK_OnboardingTasks_Users_VerifiedByUserId` FOREIGN KEY (`VerifiedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT;

UPDATE OnboardingTasks SET Area = 'HumanResource' WHERE Team = 'HumanResources' AND Area IS NULL;

UPDATE OnboardingTasks SET Area = 'CorporateIt' WHERE Team = 'InformationTechnology' AND Area IS NULL;

UPDATE OnboardingTasks SET AssignedAt = CreatedAt WHERE AssignedAt IS NULL;

UPDATE OnboardingTasks SET VerifiedAt = CompletedAt, VerifiedByUserId = CompletedByUserId WHERE Status <> 'Open' AND VerifiedAt IS NULL;

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261106105451_Phase20_InductionAndPersonnelFile', '10.0.14');

COMMIT;

