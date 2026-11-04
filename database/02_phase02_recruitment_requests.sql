START TRANSACTION;
CREATE TABLE `Attachments` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `OwnerType` varchar(50) NOT NULL,
    `OwnerId` int NOT NULL,
    `Category` varchar(40) NOT NULL,
    `OriginalFileName` varchar(200) NOT NULL,
    `StoredFileName` varchar(200) NOT NULL,
    `ContentType` varchar(150) NOT NULL,
    `SizeBytes` bigint NOT NULL,
    `Sha256` varchar(64) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`)
);

CREATE TABLE `NumberSequences` (
    `Key` varchar(100) NOT NULL,
    `LastValue` int NOT NULL,
    PRIMARY KEY (`Key`)
);

CREATE TABLE `RecruitmentRequests` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RequestNumber` varchar(40) NOT NULL,
    `DepartmentId` int NOT NULL,
    `UnitId` int NULL,
    `SiteId` int NOT NULL,
    `PositionTitle` varchar(150) NOT NULL,
    `NumberOfPositions` int NOT NULL,
    `EmploymentType` varchar(30) NOT NULL,
    `VacancyReason` varchar(30) NOT NULL,
    `ReplacedEmployee` varchar(150) NULL,
    `Justification` varchar(4000) NOT NULL,
    `BudgetCode` varchar(50) NOT NULL,
    `EstimatedAnnualCost` decimal(18,2) NOT NULL,
    `BudgetValidated` tinyint(1) NOT NULL,
    `BudgetValidatedByUserId` varchar(255) NULL,
    `RequestedByUserId` varchar(255) NOT NULL,
    `AssignedRecruiterUserId` varchar(255) NULL,
    `Status` varchar(30) NOT NULL,
    `DateRequested` datetime(6) NULL,
    `StatusChangedAt` datetime(6) NOT NULL,
    `ApprovedAt` datetime(6) NULL,
    `Version` char(36) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RecruitmentRequests_Departments_DepartmentId` FOREIGN KEY (`DepartmentId`) REFERENCES `Departments` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequests_Sites_SiteId` FOREIGN KEY (`SiteId`) REFERENCES `Sites` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequests_Units_UnitId` FOREIGN KEY (`UnitId`) REFERENCES `Units` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequests_Users_AssignedRecruiterUserId` FOREIGN KEY (`AssignedRecruiterUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequests_Users_BudgetValidatedByUserId` FOREIGN KEY (`BudgetValidatedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequests_Users_RequestedByUserId` FOREIGN KEY (`RequestedByUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE TABLE `RecruitmentRequestActions` (
    `Id` int NOT NULL AUTO_INCREMENT,
    `RecruitmentRequestId` int NOT NULL,
    `Action` varchar(30) NOT NULL,
    `FromStatus` varchar(30) NOT NULL,
    `ToStatus` varchar(30) NOT NULL,
    `ActorUserId` varchar(255) NOT NULL,
    `ActorName` varchar(150) NOT NULL,
    `Comments` varchar(2000) NULL,
    `ActionAt` datetime(6) NOT NULL,
    `CreatedAt` datetime(6) NOT NULL,
    `CreatedBy` varchar(256) NULL,
    `UpdatedAt` datetime(6) NULL,
    `UpdatedBy` varchar(256) NULL,
    `IsDeleted` tinyint(1) NOT NULL,
    `DeletedAt` datetime(6) NULL,
    `DeletedBy` varchar(256) NULL,
    PRIMARY KEY (`Id`),
    CONSTRAINT `FK_RecruitmentRequestActions_RecruitmentRequests_RecruitmentReq~` FOREIGN KEY (`RecruitmentRequestId`) REFERENCES `RecruitmentRequests` (`Id`) ON DELETE RESTRICT,
    CONSTRAINT `FK_RecruitmentRequestActions_Users_ActorUserId` FOREIGN KEY (`ActorUserId`) REFERENCES `Users` (`Id`) ON DELETE RESTRICT
);

CREATE INDEX `IX_Attachments_IsDeleted` ON `Attachments` (`IsDeleted`);

CREATE INDEX `IX_Attachments_OwnerType_OwnerId` ON `Attachments` (`OwnerType`, `OwnerId`);

CREATE UNIQUE INDEX `IX_Attachments_StoredFileName` ON `Attachments` (`StoredFileName`);

CREATE INDEX `IX_RecruitmentRequestActions_ActorUserId` ON `RecruitmentRequestActions` (`ActorUserId`);

CREATE INDEX `IX_RecruitmentRequestActions_IsDeleted` ON `RecruitmentRequestActions` (`IsDeleted`);

CREATE INDEX `IX_RecruitmentRequestActions_RecruitmentRequestId_ActionAt` ON `RecruitmentRequestActions` (`RecruitmentRequestId`, `ActionAt`);

CREATE INDEX `IX_RecruitmentRequests_AssignedRecruiterUserId` ON `RecruitmentRequests` (`AssignedRecruiterUserId`);

CREATE INDEX `IX_RecruitmentRequests_BudgetValidatedByUserId` ON `RecruitmentRequests` (`BudgetValidatedByUserId`);

CREATE INDEX `IX_RecruitmentRequests_DepartmentId_Status` ON `RecruitmentRequests` (`DepartmentId`, `Status`);

CREATE INDEX `IX_RecruitmentRequests_IsDeleted` ON `RecruitmentRequests` (`IsDeleted`);

CREATE INDEX `IX_RecruitmentRequests_RequestedByUserId` ON `RecruitmentRequests` (`RequestedByUserId`);

CREATE UNIQUE INDEX `IX_RecruitmentRequests_RequestNumber` ON `RecruitmentRequests` (`RequestNumber`);

CREATE INDEX `IX_RecruitmentRequests_SiteId` ON `RecruitmentRequests` (`SiteId`);

CREATE INDEX `IX_RecruitmentRequests_Status` ON `RecruitmentRequests` (`Status`);

CREATE INDEX `IX_RecruitmentRequests_UnitId` ON `RecruitmentRequests` (`UnitId`);

INSERT INTO `__EFMigrationsHistory` (`MigrationId`, `ProductVersion`)
VALUES ('20261104055233_Phase02_RecruitmentRequests', '10.0.14');

COMMIT;

